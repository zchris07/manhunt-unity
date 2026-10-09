using System.Collections.Generic;
using UnityEngine;

namespace Vision.Audio
{
    /// <summary>
    /// The game's sounds, played the way the original's AudioEngine plays them: one-shots, a single restartable "clip" (a
    /// slice of a file with fades: the Soundcloud Burst), the Penjamin gas loop (the first seconds of a file, looped while
    /// it lasts), and positional loops (Sexton's reel, Shane's steps, the Hemp beam) whose loudness falls off with distance
    /// on a curve and which pan left and right. Three volume buses, as the original's settings: Master, "Soundcloud Burst"
    /// (effects) and "Sexton's reel" (ambience), saved between sessions. Distances are in the original's units.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>A sound slot: a file in Resources/Audio, and the part of it the game plays.</summary>
        public readonly struct SoundDef
        {
            public readonly string File;
            /// <summary>Seconds played (0: the whole file); with Random, from a random point.</summary>
            public readonly float Duration;
            public readonly bool Random;
            public SoundDef(string file, float duration = 0f, bool random = false)
            {
                File = file;
                Duration = duration;
                Random = random;
            }
        }

        /// <summary>The original's sound manifest (plus the sounds this port adds).</summary>
        public static readonly Dictionary<string, SoundDef> Sounds = new Dictionary<string, SoundDef>
        {
            ["burst"] = new SoundDef("gmajor", 2.6f, true),
            ["sexton.reel"] = new SoundDef("reel"),
            ["boom"] = new SoundDef("vine-boom"),
            ["penjamin"] = new SoundDef("penjamin", 4f),
            ["repulsor"] = new SoundDef("repulsor"),
        };

        public struct Volumes
        {
            public float Master, Sfx, Ambience;
            public static Volumes Defaults => new Volumes { Master = 0.8f, Sfx = 0.9f, Ambience = 0.7f };
        }

        const string VolumesKey = "manhunt.volumes";

        static AudioManager instance;
        /// <summary>The one audio manager (made on first use).</summary>
        public static AudioManager Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindAnyObjectByType<AudioManager>();
                if (instance == null)
                {
                    var go = new GameObject("Audio");
                    instance = go.AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        public Volumes Volume { get; private set; } = Volumes.Defaults;
        /// <summary>The listener's position on the map (original units); positional loops pan and fall off from here.</summary>
        public Vector2 Listener { get; set; }
        /// <summary>How many clips and one-shots have played (tests and diagnostics).</summary>
        public int Played { get; private set; }
        public string LastPlayed { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> oneShots = new List<AudioSource>();
        AudioSource clipSource, gasSource;
        float clipVolume, clipFadeIn, clipFadeOut, clipStart, clipEnd;
        float gasTarget, gasFade, gasLoopEnd;
        bool gasOn;

        sealed class LoopState
        {
            public string Id;
            public AudioSource Source;
            public Vector2 Pos;
            public float Volume, Near, Radius, Curve, Gain;
            public bool Stopping;
        }

        readonly Dictionary<string, LoopState> loops = new Dictionary<string, LoopState>();

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            Volume = Load();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------------------------------------------------------------- volumes

        public static Volumes Load()
        {
            try
            {
                string s = PlayerPrefs.GetString(VolumesKey, "");
                string[] p = s.Split(',');
                if (p.Length == 3 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float m) &&
                    float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a))
                    return new Volumes { Master = Mathf.Clamp01(m), Sfx = Mathf.Clamp01(x), Ambience = Mathf.Clamp01(a) };
            }
            catch { }
            return Volumes.Defaults;
        }

        public void SetVolumes(Volumes v, bool save = true)
        {
            Volume = new Volumes { Master = Mathf.Clamp01(v.Master), Sfx = Mathf.Clamp01(v.Sfx), Ambience = Mathf.Clamp01(v.Ambience) };
            if (!save) return;
            try
            {
                var c = System.Globalization.CultureInfo.InvariantCulture;
                PlayerPrefs.SetString(VolumesKey, $"{Volume.Master.ToString(c)},{Volume.Sfx.ToString(c)},{Volume.Ambience.ToString(c)}");
            }
            catch { }
        }

        float SfxGain => Volume.Master * Volume.Sfx;
        float AmbienceGain => Volume.Master * Volume.Ambience;

        // ---------------------------------------------------------------- clips

        public AudioClip Clip(string id)
        {
            if (clips.TryGetValue(id, out AudioClip c)) return c;
            c = Sounds.TryGetValue(id, out SoundDef d) ? Resources.Load<AudioClip>("Audio/" + d.File) : Resources.Load<AudioClip>("Audio/" + id);
            clips[id] = c;
            return c;
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        /// <summary>Plays a whole sound once, on its own (it doesn't cut off other sounds).</summary>
        public bool OneShot(string id, float volume = 1f)
        {
            if (volume <= 0f) return false;
            AudioClip c = Clip(id);
            if (c == null) return false;
            AudioSource s = oneShots.Find(x => x != null && !x.isPlaying);
            if (s == null)
            {
                s = NewSource("One-shot");
                oneShots.Add(s);
            }
            s.clip = c;
            s.loop = false;
            s.volume = Mathf.Clamp01(volume) * SfxGain;
            s.panStereo = 0f;
            s.Play();
            Played++;
            LastPlayed = id;
            return true;
        }

        /// <summary>
        /// Plays a slice of a file (restarting the clip if one is playing): <paramref name="duration"/> seconds (the manifest's
        /// by default) from its start, or from a random point for a "random" sound, fading in and out.
        /// </summary>
        public bool PlayClip(string id, float volume = 1f, float fadeIn = 0.04f, float fadeOut = 1.4f, float duration = -1f)
        {
            AudioClip c = Clip(id);
            if (c == null) return false;
            Sounds.TryGetValue(id, out SoundDef d);
            float want = Mathf.Min(duration > 0f ? duration : d.Duration > 0f ? d.Duration : c.length, c.length);
            float from = d.Random ? UnityEngine.Random.value * Mathf.Max(0f, c.length - want) : 0f;
            from = Mathf.Clamp(from, 0f, Mathf.Max(0f, c.length - 0.1f));
            if (clipSource == null) clipSource = NewSource("Clip");
            clipSource.Stop();
            clipSource.clip = c;
            clipSource.loop = false;
            clipSource.time = from;
            clipVolume = volume;
            clipFadeIn = Mathf.Max(0.001f, fadeIn);
            clipFadeOut = Mathf.Max(0.001f, fadeOut);
            clipStart = Time.unscaledTime;
            clipEnd = clipStart + Mathf.Min(want, c.length - from);
            clipSource.volume = 0f;
            clipSource.Play();
            Played++;
            LastPlayed = id;
            return true;
        }

        public bool ClipPlaying => clipSource != null && clipSource.isPlaying && Time.unscaledTime < clipEnd;

        /// <summary>While on, loops the first seconds of a sound (fading in); off, it fades out and stops. Call every frame.</summary>
        public void GasLoop(bool on, string id = "penjamin", float volume = 0.9f, float fadeIn = 0.8f, float fadeOut = 0.8f)
        {
            if (on == gasOn) return;
            gasOn = on;
            if (on)
            {
                AudioClip c = Clip(id);
                if (c == null) { gasOn = false; return; }
                if (gasSource == null) gasSource = NewSource("Gas");
                Sounds.TryGetValue(id, out SoundDef d);
                gasLoopEnd = d.Duration > 0f ? Mathf.Min(d.Duration, c.length) : c.length;
                if (!gasSource.isPlaying || gasSource.clip != c)
                {
                    gasSource.clip = c;
                    gasSource.loop = true;
                    gasSource.time = 0f;
                    gasSource.volume = 0f;
                    gasSource.Play();
                }
                gasTarget = volume;
                gasFade = fadeIn;
                Played++;
                LastPlayed = id;
            }
            else
            {
                gasTarget = 0f;
                gasFade = fadeOut;
            }
        }

        public bool GasPlaying => gasOn;

        /// <summary>
        /// Starts, moves or (with <paramref name="id"/> null) stops a named positional loop at <paramref name="pos"/> (original
        /// units): full volume within <paramref name="near"/>, silent past <paramref name="radius"/>, falling off linearly or as
        /// ((radius - d) / (radius - near)) ^ <paramref name="curve"/>. A long track joins where a clock started at zero would
        /// be; <paramref name="fromStart"/> starts it from the top.
        /// </summary>
        public void Loop(string key, string id, Vector2 pos = default, float volume = 1f, float near = 100f, float radius = 1000f, float curve = 1f, bool fromStart = false)
        {
            loops.TryGetValue(key, out LoopState cur);
            if (id == null)
            {
                if (cur != null) cur.Stopping = true;
                return;
            }
            if (cur != null && cur.Id == id && !cur.Stopping)
            {
                cur.Pos = pos;
                cur.Volume = volume;
                cur.Near = near;
                cur.Radius = radius;
                cur.Curve = curve;
                return;
            }
            AudioClip c = Clip(id);
            if (c == null) return;
            if (cur == null)
            {
                cur = new LoopState { Source = NewSource("Loop " + key) };
                loops[key] = cur;
            }
            cur.Id = id;
            cur.Stopping = false;
            cur.Pos = pos;
            cur.Volume = volume;
            cur.Near = near;
            cur.Radius = radius;
            cur.Curve = curve;
            cur.Gain = 0f;
            cur.Source.clip = c;
            cur.Source.loop = true;
            cur.Source.volume = 0f;
            cur.Source.time = fromStart ? 0f : Mathf.Repeat(Time.unscaledTime, c.length);
            cur.Source.Play();
        }

        public bool LoopPlaying(string key) => loops.TryGetValue(key, out LoopState l) && !l.Stopping && l.Source != null && l.Source.isPlaying;

        /// <summary>Gain at a distance: full within near, none past radius, on the curve between.</summary>
        public static float Falloff(float d, float near, float radius, float curve)
        {
            float k = Mathf.Clamp01((radius - d) / Mathf.Max(1f, radius - near));
            return Mathf.Pow(k, Mathf.Max(0.01f, curve));
        }

        /// <summary>The original's distance volume for one-shots: 1 near, fading to 0 at the radius.</summary>
        public float Near(Vector2 at, float radius) => Mathf.Clamp01(1f - Vector2.Distance(at, Listener) / Mathf.Max(1f, radius));

        public void StopAll()
        {
            GasLoop(false);
            foreach (LoopState l in loops.Values) l.Stopping = true;
            if (clipSource != null) clipSource.Stop();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            if (clipSource != null && clipSource.isPlaying)
            {
                if (now >= clipEnd) clipSource.Stop();
                else
                {
                    float k = Mathf.Min(Mathf.Clamp01((now - clipStart) / clipFadeIn), Mathf.Clamp01((clipEnd - now) / clipFadeOut));
                    clipSource.volume = clipVolume * k * SfxGain;
                }
            }
            if (gasSource != null && gasSource.isPlaying)
            {
                if (gasSource.time >= gasLoopEnd) gasSource.time = 0f;
                float v = gasSource.volume / Mathf.Max(1e-4f, SfxGain);
                v = Mathf.MoveTowards(v, gasTarget, dt / Mathf.Max(0.01f, gasFade) * 0.9f);
                gasSource.volume = v * SfxGain;
                if (!gasOn && v <= 0.001f) gasSource.Stop();
            }
            List<string> dead = null;
            foreach (var kv in loops)
            {
                LoopState l = kv.Value;
                if (l.Source == null) continue;
                Vector2 rel = l.Pos - Listener;
                float target = l.Stopping ? 0f : l.Volume * Falloff(rel.magnitude, l.Near, l.Radius, l.Curve);
                // Eased like the original's setTargetAtTime (0.1-0.3 s).
                l.Gain = Mathf.Lerp(l.Gain, target, 1f - Mathf.Exp(-dt / (l.Stopping ? 0.2f : 0.1f)));
                l.Source.volume = l.Gain * AmbienceGain;
                l.Source.panStereo = Mathf.Clamp(rel.x / Mathf.Max(1f, l.Radius) * 2f, -0.8f, 0.8f);
                if (l.Stopping && l.Gain < 0.002f)
                {
                    l.Source.Stop();
                    (dead ??= new List<string>()).Add(kv.Key);
                }
            }
            if (dead != null)
                foreach (string k in dead)
                {
                    if (loops[k].Source != null) Destroy(loops[k].Source.gameObject);
                    loops.Remove(k);
                }
        }
    }
}
