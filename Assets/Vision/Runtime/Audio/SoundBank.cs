using System.Collections.Generic;
using UnityEngine;

namespace Vision.Audio
{
    /// <summary>
    /// The game's sound cues and the files behind them (the fetched CC0 packs in Resources/Audio/Fx, see CREDITS.md):
    /// each cue picks one of its variants at random, with a little pitch and volume jitter so repeats don't sound
    /// mechanical. Some cues are layered or pitched to build sounds no pack has (a heavy machete swing, the golden
    /// pump, the 0.50 cal), and the machete's charge swell is synthesised.
    /// </summary>
    public static class SoundBank
    {
        public sealed class Cue
        {
            public string[] Files;
            public float Volume = 1f, Pitch = 1f, PitchJitter = 0.06f, VolumeJitter = 0.1f;
            /// <summary>Original units: full volume within Near, silent past Radius.</summary>
            public float Near = 150f, Radius = 1400f;
        }

        static string[] Range(string prefix, int from, int to, string fmt = "D3")
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++) list.Add(prefix + i.ToString(fmt));
            return list.ToArray();
        }

        public static readonly Dictionary<string, Cue> Cues = new Dictionary<string, Cue>
        {
            // Zach.
            ["swing"] = new Cue { Files = new[] { "swishes_swish-1", "swishes_swish-2", "swishes_swish-3", "swishes_swish-4" }, Volume = 0.8f, Radius = 900f },
            ["swing_heavy"] = new Cue { Files = new[] { "swishes_swish-9", "swishes_swish-10", "swishes_swish-11", "swishes_swish-12", "swishes_swish-13" }, Volume = 1f, Pitch = 0.82f, Radius = 1100f },
            ["slash"] = new Cue { Files = new[] { "rpg_knifeslice", "rpg_knifeslice2", "rpg_chop" }, Volume = 0.9f, Radius = 1000f },
            ["body_hit"] = new Cue { Files = Range("impact_impactpunch_heavy_", 0, 4), Volume = 0.8f, Radius = 900f },
            ["lunge"] = new Cue { Files = new[] { "swishes_swish-7", "swishes_swish-8" }, Volume = 0.8f, Pitch = 0.7f, Radius = 900f },
            ["kick"] = new Cue { Files = Range("impact_impactmetal_heavy_", 0, 4), Volume = 0.9f, Radius = 1200f },
            ["smash"] = new Cue { Files = Range("impact_impactwood_heavy_", 0, 4), Volume = 1f, Radius = 1300f },
            ["splinter"] = new Cue { Files = Range("impact_impactplank_medium_", 0, 4), Volume = 0.8f, Radius = 1000f },
            ["glass"] = new Cue { Files = new[] { "impact_impactglass_heavy_000", "impact_impactglass_heavy_001", "impact_impactglass_heavy_002", "sfx100_glass_01", "sfx100_glass_02" }, Volume = 1f, Radius = 1300f },
            // Guns.
            ["pistol"] = new Cue { Files = new[] { "tabasco_cz" }, Volume = 0.9f, Radius = 2600f, PitchJitter = 0.04f },
            ["shotgun"] = new Cue { Files = new[] { "tabasco_shotty" }, Volume = 1f, Radius = 3000f, PitchJitter = 0.04f },
            ["golden"] = new Cue { Files = new[] { "tabasco_shotty" }, Volume = 1f, Pitch = 1.08f, Radius = 3000f, PitchJitter = 0.02f },
            ["golden_bell"] = new Cue { Files = Range("impact_impactbell_heavy_", 0, 4), Volume = 0.45f, Pitch = 1.2f, Radius = 2000f },
            ["sniper"] = new Cue { Files = new[] { "tabasco_mosin" }, Volume = 1f, Pitch = 0.78f, Radius = 6000f, PitchJitter = 0.02f },
            ["sniper_tail"] = new Cue { Files = new[] { "sfx100_explosion" }, Volume = 0.35f, Pitch = 0.7f, Radius = 6000f },
            ["rack"] = new Cue { Files = new[] { "shotgun_rack" }, Volume = 0.7f, Radius = 700f },
            ["reload"] = new Cue { Files = new[] { "gun_reload", "clip_load" }, Volume = 0.7f, Radius = 700f },
            ["shell"] = new Cue { Files = new[] { "shotgun_first_shell", "shotgun_subsequent_shells" }, Volume = 0.6f, Radius = 600f },
            ["dry"] = new Cue { Files = new[] { "rpg_metalclick" }, Volume = 0.6f, Radius = 400f },
            // Things.
            ["door_open"] = new Cue { Files = new[] { "rpg_dooropen_1", "rpg_dooropen_2" }, Volume = 0.7f, Radius = 900f },
            ["door_close"] = new Cue { Files = new[] { "rpg_doorclose_1", "rpg_doorclose_2", "rpg_doorclose_3", "rpg_doorclose_4" }, Volume = 0.7f, Radius = 900f },
            ["slam"] = new Cue { Files = new[] { "sfx100_slam_01", "sfx100_slam_02", "sfx100_slam_03", "sfx100_slam_04" }, Volume = 1f, Radius = 1300f },
            ["pickup"] = new Cue { Files = new[] { "rpg_handlesmallleather", "rpg_handlesmallleather2", "rpg_cloth1", "rpg_cloth2" }, Volume = 0.6f, Radius = 400f },
            ["throw"] = new Cue { Files = new[] { "swishes_swish-2", "swishes_swish-3" }, Volume = 0.6f, Pitch = 1.15f, Radius = 600f },
            ["bottle_break"] = new Cue { Files = Range("impact_impactglass_light_", 0, 4), Volume = 0.9f, Radius = 1100f },
            ["book_hit"] = new Cue { Files = new[] { "rpg_bookplace1", "rpg_bookplace2", "rpg_bookplace3" }, Volume = 1f, Pitch = 0.8f, Radius = 900f },
            ["splash"] = new Cue { Files = new[] { "sfx100_splash_01", "sfx100_splash_02" }, Volume = 0.8f, Radius = 900f },
            ["drink"] = new Cue { Files = new[] { "sfx100_plop_01", "sfx100_plop_02" }, Volume = 0.6f, Radius = 400f },
            ["eat"] = new Cue { Files = new[] { "rpg_cloth3", "rpg_cloth4" }, Volume = 0.6f, Radius = 400f },
            ["trap"] = new Cue { Files = new[] { "rpg_metallatch", "rpg_metalclick" }, Volume = 0.8f, Radius = 700f },
            ["gas"] = new Cue { Files = new[] { "sfx100_noise_01", "sfx100_noise_02" }, Volume = 0.7f, Radius = 1200f },
            ["stun"] = new Cue { Files = Range("impact_impactplate_heavy_", 0, 4), Volume = 0.8f, Radius = 900f },
            ["generator_done"] = new Cue { Files = new[] { "sfx100_machine_01", "sfx100_machine_02" }, Volume = 0.9f, Radius = 2400f },
            ["gate"] = new Cue { Files = new[] { "sfx100_metal_03", "sfx100_metal_07" }, Volume = 1f, Radius = 3000f },
            ["note"] = new Cue { Files = new[] { "sfx100_paper_01", "sfx100_paper_02", "sfx100_paper_03" }, Volume = 0.8f, Radius = 300f },
            ["hide"] = new Cue { Files = new[] { "rpg_creak1", "rpg_creak2", "rpg_creak3" }, Volume = 0.7f, Radius = 700f },
            // Footsteps (by ground).
            ["step_grass"] = new Cue { Files = Range("impact_footstep_grass_", 0, 4), Volume = 0.35f, Radius = 600f, PitchJitter = 0.08f },
            ["step_concrete"] = new Cue { Files = Range("impact_footstep_concrete_", 0, 4), Volume = 0.35f, Radius = 600f, PitchJitter = 0.08f },
            ["step_wood"] = new Cue { Files = Range("impact_footstep_wood_", 0, 4), Volume = 0.35f, Radius = 600f, PitchJitter = 0.08f },
            ["step_shane"] = new Cue { Files = new[] { "steps_gravel", "steps_leaves01", "steps_leaves02", "steps_mud02" }, Volume = 0.8f, Near = 70f, Radius = 750f, PitchJitter = 0.1f },
        };

        static readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>();

        public static AudioClip Load(string file)
        {
            if (loaded.TryGetValue(file, out AudioClip c)) return c;
            c = Resources.Load<AudioClip>("Audio/Fx/" + file);
            loaded[file] = c;
            return c;
        }

        static AudioClip chargeSwell;

        /// <summary>The machete's charge: a rising, tightening swell of filtered noise and a low tone (0.9 s, synthesised).</summary>
        public static AudioClip ChargeSwell()
        {
            if (chargeSwell != null) return chargeSwell;
            const int rate = 44100;
            int n = Mathf.RoundToInt(rate * 0.9f);
            var data = new float[n];
            var rng = new System.Random(17);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, k = t / 0.9f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float cut = Mathf.Lerp(0.02f, 0.22f, k * k);
                lp += (noise - lp) * cut;
                phase += 2f * Mathf.PI * Mathf.Lerp(55f, 140f, k * k) / rate;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 0.08f)) * Mathf.Lerp(0.25f, 1f, k);
                data[i] = Mathf.Clamp((lp * 1.6f + Mathf.Sin(phase) * 0.35f) * env, -1f, 1f) * 0.7f;
            }
            chargeSwell = AudioClip.Create("charge swell", n, 1, rate, false);
            chargeSwell.SetData(data, 0);
            return chargeSwell;
        }
    }
}
