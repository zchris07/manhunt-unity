using UnityEngine;
using Vision.Audio;
using Vision.Effects;
using Vision.Game;
using Vision.Player;
using Vision.World;
using Vision.Visibility;

namespace Vision.UI
{
    /// <summary>
    /// Turns what happens in the match into what the local player sees and hears, as the original's GameView does: the
    /// Soundcloud Burst scare (picture and a slice of the song), The Grapes of Wrath and Waz flashes, notes, the vine boom
    /// (louder the nearer), camera shakes, centre messages, the Penjamin gas loop, and stun stars over the player.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public SandboxWorld world;
        public ScreenOverlays overlays;
        public TopDownCamera cameraRig;

        MatchHost host;
        PlayerController player;
        StunStars stars;
        Material starMaterial;

        public AudioManager Audio => AudioManager.Instance;
        /// <summary>The Hemp Battery is announced once ever on a machine, as in the original.</summary>
        public const string HempAnnouncedKey = "manhunt.hempAnnounced";

        float zoomK, aimK;

        /// <summary>Who the camera follows while you are out of the match (escaped, sacrificed or spectating), or 0.</summary>
        public int Spectating { get; private set; }

        void Update()
        {
            Bind();
            SimPlayer me = host != null ? host.Local : null;
            if (me == null) return;
            UpdateCamera(me);
            DrawTrails(me);
            // Hear from where you are.
            Audio.Listener = Scale.ToUnits(1f) * me.Pos;
            // Penjamin: the gas sound loops while a survivor is in the cloud.
            Audio.GasLoop(me.Role == Role.Survivor && me.VapeT > Balance.Hunter.Vape.AfterTime - 0.15f);
            // Stun stars over the player while stunned (Zach: also knocked down); the dizzy spiral in Penjamin's gas.
            if (stars != null)
            {
                bool dazed = me.StunT > 0f || (me.Role == Role.Hunter && me.KnockT > 0f);
                stars.Dizzy = !dazed && me.VapeT > 0f;
                stars.Show(dazed || stars.Dizzy);
            }
        }

        /// <summary>
        /// The camera: the Hemp Battery zooms Zach out (eased in and out), aiming the 0.50 cal widens the view 15%, Waz's field of view widens or narrows it, and once
        /// you are out of the match it follows someone still in it (click or the arrow keys switch who).
        /// </summary>
        void UpdateCamera(SimPlayer me)
        {
            if (cameraRig == null || player == null) return;
            float dt = Time.deltaTime;
            zoomK = Mathf.MoveTowards(zoomK, me.Role == Role.Hunter && me.HempOn ? 1f : 0f, dt * Balance.Hunter.Hemp.ZoomRate);
            float e = zoomK * zoomK * (3f - 2f * zoomK);
            // Aiming the 0.50 cal widens the view 15%, eased over about a quarter of a second like the scope coming up.
            aimK = Mathf.MoveTowards(aimK, me.Aiming ? 1f : 0f, dt * 4f);
            float ae = aimK * aimK * (3f - 2f * aimK);
            float fov = me.FovMul > 0f ? me.FovMul : 1f;
            cameraRig.zoom = (1f + (Balance.Hunter.Hemp.ZoomOut - 1f) * e) * (1f + (Balance.Items.Sniper.AimZoom - 1f) * ae) / fov;

            bool out_ = me.Role == Role.Spectator || me.Health == Health.Escaped || me.Health == Health.Eliminated;
            Transform target = player.transform;
            Spectating = 0;
            if (out_)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                bool next = (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame && !GameHud.MenuOpen);
                bool prev = kb != null && kb.leftArrowKey.wasPressedThisFrame;
                if (next || prev) host.CycleSpectate(next ? 1 : -1);
                if (host.Sim.Get(me.Spectating) == null) me.Spectating = host.Sim.DefaultSpectateTarget(me.Id);
                var puppets = world.GetComponent<PlayerPuppets>();
                PlayerPuppets.Puppet pup = puppets != null ? puppets.For(me.Spectating) : null;
                if (pup != null && pup.Go != null)
                {
                    target = pup.Go.transform;
                    Spectating = me.Spectating;
                }
            }
            if (cameraRig.target != target) cameraRig.target = target;
        }

        /// <summary>A point of the level in world space, just above the ground (design units in).</summary>
        Vector3 Ground(Vector2 at, float up = 0.04f)
        {
            float h = world.GroundHeight(at);
            return world.transform.TransformPoint(new Vector3(at.x, h + up, at.y));
        }

        float S => world != null ? world.transform.lossyScale.x : 1f;

        /// <summary>
        /// The scent: Zach sees the trails survivors leave when they run (red smoke wisps) and blood where the hurt have been,
        /// fading over ten seconds, only where his light falls (survivors see their own in testing mode).
        /// </summary>
        readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<TrailPoint>> byWho = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<TrailPoint>>();
        readonly BloodSplats blood = new BloodSplats();
        public int BloodCount => blood.Count;
        /// <summary>
        /// The order a survivor laid their trail: by how far along it each point is (it only ever grows), then by when it
        /// was laid. Not by its time: points laid walking are stamped four seconds older (so they fade sooner), and sorted
        /// by time they fell in among the running points of four seconds before, somewhere else, and the thread zig-zagged
        /// between the two, crossing itself.
        /// </summary>
        static readonly System.Comparison<TrailPoint> ByLaying = (a, b) => a.Dist != b.Dist ? a.Dist.CompareTo(b.Dist) : a.Id.CompareTo(b.Id);

        /// <summary>Puts a survivor's scent points in the order they laid them (see <see cref="ByLaying"/>).</summary>
        public static void OrderTrail(System.Collections.Generic.List<TrailPoint> pts) => pts.Sort(ByLaying);

        void DrawTrails(SimPlayer me)
        {
            MatchSim sim = host.Sim;
            bool zach = me.Role == Role.Hunter;
            if (!zach && !sim.TestMode) return;
            foreach (var l in byWho.Values) l.Clear();
            // Zach's nose reaches the original's 1300 units (the trail is sent to him only that far about him).
            float nose = Scale.D(Balance.Hunter.ScentRadius);
            blood.Begin();
            foreach (TrailPoint t in sim.Trails)
            {
                if (!zach && t.Who != me.Id) continue;
                if (Mathf.Abs(t.Pos.x - me.Pos.x) > nose || Mathf.Abs(t.Pos.y - me.Pos.y) > nose) continue;
                float age = (sim.Time - t.T) / Balance.Trails.MaxAgeSec;
                if (age >= 1f || age < 0f) continue;
                if (t.Kind == 1)
                {
                    // Blood: a miniature splatter on the ground.
                    blood.Draw(world, t.Id, t.Pos, age);
                    continue;
                }
                if (!byWho.TryGetValue(t.Who, out var list)) byWho[t.Who] = list = new System.Collections.Generic.List<TrailPoint>(64);
                list.Add(t);
            }
            blood.End();
            foreach (var kv in byWho)
            {
                var pts = kv.Value;
                if (pts.Count < 2) continue;
                OrderTrail(pts);
                // One point is laid every 0.12 s moving; further along than a few of those between neighbours, they went on
                // without leaving any (crouched, hid) or the points between are out of Zach's reach: a break.
                float step = Mathf.Max(Scale.D(60f), 3f * Scale.Speed(Balance.Survivor.Run) * Balance.Trails.ScentEvery);
                int start = 0;
                for (int i = 1; i <= pts.Count; i++)
                {
                    // Break where the trail really breaks.
                    bool cut = i == pts.Count || pts[i].Dist - pts[i - 1].Dist > step || Vector2.Distance(pts[i].Pos, pts[i - 1].Pos) > step;
                    if (!cut) continue;
                    int n = i - start;
                    // The scent: a thin, faint, unbroken thread of red smoke along where they ran (the same billowing
                    // volumetric smoke as the gases): a tube from each point of the trail to the next, overlapping its
                    // neighbours so it flows on without a seam (one seed for the whole trail, so its billows run on through
                    // the joins), swaying gently, thinning and fading as it ages. Along its length it swells and thins on a
                    // strict repeat, thicker and more solid at the peaks, thinner and fainter in the valleys, peak to valley
                    // as far as a survivor sprints in two seconds (laid by distance along the trail, so it stays put).
                    if (n >= 2)
                        for (int k = 0; k + 1 < n; k++)
                        {
                            TrailPoint t0 = pts[start + k], t1 = pts[start + k + 1];
                            Vector2 a = Sway(t0, kv.Key, sim.Time), b = Sway(t1, kv.Key, sim.Time);
                            Vector2 d = b - a;
                            float len = d.magnitude;
                            if (len < 1e-3f) continue;
                            d /= len;
                            float age = Mathf.Clamp01((sim.Time - (t0.T + t1.T) * 0.5f) / Balance.Trails.MaxAgeSec);
                            float half = Scale.Speed(Balance.Survivor.Run) * Balance.Trails.ScentWaveSprintSec;
                            float wave = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (t0.Dist + t1.Dist) * 0.5f / Mathf.Max(0.01f, half));
                            // 30% thicker than before and then twice as thick again; at its thinnest twice what it was, at its
                            // thickest 20% less.
                            float radius = Scale.D(3.5f + 3.5f * age) * 1.3f * 2f * S * Mathf.Lerp(0.2375f * 2f, 1.5125f * 0.8f, wave);
                            // How solid it is varies at random along the trail, on its own, not with the swelling and thinning:
                            // smooth noise laid by distance along the trail (so it stays put), on its own scale and seed.
                            float mid = (t0.Dist + t1.Dist) * 0.5f;
                            float solid = Mathf.Clamp01((Mathf.PerlinNoise(mid / Mathf.Max(0.01f, half * 0.37f) + kv.Key * 13.7f, 41.3f + kv.Key * 2.9f) - 0.2f) / 0.6f);
                            var look = new VolumetricGas.Look
                            {
                                TintA = new Color(0.75f, 0.22f, 0.18f),
                                TintB = new Color(0.4f, 0.05f, 0.06f),
                                Opacity = Mathf.Pow(1f - age, 1.2f) * 0.4f * 1.2f * 1.2f * Mathf.Lerp(0.3f, 1.15f, solid),
                                Extinction = 1.6f / S,
                                NoiseScale = 1.1f / S,
                                Erosion = 0.7f,
                                Drift = new Vector3(0f, 0.12f, 0f) * S,
                                Seed = kv.Key * 7,
                            };
                            // Each piece reaches a third of the way into the next and the last, so the soft ends overlap (never
                            // further than the thread is thick, so a sharp turn never pokes out past its corner as a cross).
                            float extra = Mathf.Min(len * 0.33f, radius / S * 1.2f);
                            Vector2 from = a - d * extra;
                            Vector3 tip = Ground(from, 0f);
                            VolumetricGas.Tube(tip, world.transform.TransformDirection(new Vector3(d.x, 0f, d.y)), (len + 2f * extra) * S, radius, radius * 0.6f + Scale.D(2f + 3f * age) * S, look, true, false);
                        }
                    start = i;
                }
            }
        }

        /// <summary>A trail point swayed gently sideways (the same for the point whichever piece of the trail draws it).</summary>
        static Vector2 Sway(TrailPoint t, int who, float now)
        {
            float k = Mathf.Sin(t.T * 1.7f + who + now * 0.6f);
            return t.Pos + new Vector2(Mathf.Cos(t.T * 0.9f + who), Mathf.Sin(t.T * 0.9f + who)) * k * Scale.D(2.5f);
        }

        void Bind()
        {
            if (world == null) return;
            if (cameraRig == null) cameraRig = FindAnyObjectByType<TopDownCamera>();
            if (cameraRig != null && cameraRig.GetComponent<SensesCamera>() == null) cameraRig.gameObject.AddComponent<SensesCamera>();
            MatchHost h = MatchHost.For(world);
            if (h != host)
            {
                if (host != null) host.EventRaised -= OnEvent;
                host = h;
                if (host != null) host.EventRaised += OnEvent;
            }
            if (world.Player != player)
            {
                player = world.Player;
                stars = null;
                if (player != null)
                {
                    stars = player.GetComponent<StunStars>() ?? player.gameObject.AddComponent<StunStars>();
                    if (starMaterial == null && world.entityMaterial != null)
                    {
                        starMaterial = new Material(world.entityMaterial) { name = "Stun Stars", hideFlags = HideFlags.HideAndDontSave };
                        if (starMaterial.HasProperty("_Emission")) starMaterial.SetFloat("_Emission", 0.9f);
                    }
                    stars.material = starMaterial;
                }
            }
        }

        void OnDestroy()
        {
            if (host != null) host.EventRaised -= OnEvent;
            if (starMaterial != null) Destroy(starMaterial);
        }

        /// <summary>The original's near(): 1 at the listener, falling to 0 at <paramref name="radius"/> original units.</summary>
        float Near(Vector2 designPos, float radius) => Audio.Near(Scale.ToUnits(1f) * designPos, radius);

        void Shake(float px)
        {
            if (cameraRig != null && px > 0f) cameraRig.Shake(px);
        }

        /// <summary>What a noise looks like where it happens (the original's particle bursts): sparks, glass, splinters.</summary>
        void NoiseBurst(GameEvent e)
        {
            Vfx fx = Vfx.Instance;
            switch (e.Text)
            {
                case "gen_explode":
                case "gen_kick":
                    fx.Burst(Ground(e.Pos, 0.7f), 40, 3.5f * S, 0.7f, new Color(1f, 0.85f, 0.4f, 1f), 0.05f * S, true, Vfx.Blend.Additive, 5f * S, 2f, 0.6f, new Color(1f, 0.55f, 0.2f, 1f));
                    break;
                case "glass":
                    fx.Burst(Ground(e.Pos, 1.2f), 22, 2.4f * S, 0.45f, new Color(0.72f, 0.78f, 0.78f, 0.9f), 0.045f * S, true, Vfx.Blend.Alpha, 7f * S, 1.5f, 0.3f);
                    break;
                case "smash":
                case "barricade":
                case "door_smash":
                    fx.Burst(Ground(e.Pos, 0.8f), e.Text == "door_smash" ? 30 : 16, 2.1f * S, 0.55f, new Color(0.43f, 0.35f, 0.25f, 1f), 0.07f * S, true, Vfx.Blend.Alpha, 6f * S, 2f, 0.4f, new Color(0.3f, 0.23f, 0.16f, 1f));
                    break;
            }
        }

        public void OnEvent(GameEvent e)
        {
            Bind();
            if (host == null || host.Sim == null) return;
            int me = host.LocalId;
            switch (e.Kind)
            {
                case EventKind.Scare:
                {
                    // 2.5 s: the picture and a snippet of the song both fade in and out.
                    overlays?.JumpScare(Balance.Hunter.Burst.ScareTime, Balance.Hunter.Burst.ScareFade);
                    Audio.PlayClip("burst", Balance.Hunter.Burst.ScareVolume, Balance.Hunter.Burst.ScareFade, Balance.Hunter.Burst.ScareFade, Balance.Hunter.Burst.ScareTime);
                    break;
                }
                case EventKind.Talk:
                    // Zach fired a Soundcloud Burst: only he hears it go out.
                    if (e.Text == "burst" && e.A == me) Audio.PlayClip("burst", Balance.Hunter.Burst.ZachVolume);
                    else if (e.Text == "shane_alerted") overlays?.Center("SHANE JEANS HAS BEEN ALERTED", 3f);
                    break;
                case EventKind.Book:
                    overlays?.FlashImage(ScreenOverlays.Picture($"book-{Mathf.Clamp(e.A, 0, Balance.Items.Book.Images - 1) + 1}"), Balance.Items.FlashTime, Balance.Items.FlashTime * Balance.Items.FlashFade, true);
                    Shake(16f);
                    break;
                case EventKind.WazSlain:
                    overlays?.FlashImage(ScreenOverlays.Picture("waz-slain"), Balance.Items.FlashTime, Balance.Items.FlashTime * Balance.Items.FlashFade, false);
                    Audio.OneShot("boom", 1f);
                    break;
                case EventKind.Boom:
                    Audio.OneShot("boom", Mathf.Max(0.15f, Near(e.Pos, e.F > 0f ? e.F : 1400f)));
                    break;
                case EventKind.Explosion:
                    Audio.OneShot("boom", Mathf.Max(0.2f, Near(e.Pos, 1600f)));
                    Shake(22f * Near(e.Pos, 900f));
                    break;
                case EventKind.Note:
                    Audio.PlayCue("note", null, 0.8f);
                    overlays?.ShowNote(e.A);
                    break;
                case EventKind.Hit:
                    if (e.A == me) Shake(14f);
                    else Shake(6f * Near(e.Pos, 300f));
                    // Blood (a white flash for the Hemp beam).
                    if (e.Text == "beam") Vfx.Instance.Burst(Ground(e.Pos, 0.9f), 14, 2.6f * S, 0.45f, new Color(0.94f, 1f, 0.94f, 1f), 0.06f * S);
                    else Vfx.Instance.Burst(Ground(e.Pos, 0.9f), e.Text == "pellet" ? 6 : 16, 2.2f * S, 0.5f, new Color(0.055f, 0.0016f, 0.0045f, 1f), 0.07f * S, true, Vfx.Blend.Alpha, 6f * S, 2f, 0.4f, new Color(0.032f, 0.001f, 0.0028f, 1f));
                    break;
                case EventKind.Shot:
                    Shake(9f * Near(e.Pos, 500f));
                    break;
                case EventKind.Noise:
                    if (e.Text == "gen_explode" || e.Text == "barricade" || e.Text == "smash" || e.Text == "door_smash") Shake(7f * Near(e.Pos, 450f));
                    NoiseBurst(e);
                    break;
                case EventKind.Downed:
                    if (e.A == me) overlays?.Center("You are down", 4f);
                    break;
                case EventKind.Stun:
                    if (e.A == me)
                    {
                        if (e.Text == "down") overlays?.Center("DOWN", Balance.Hunter.Health.DownTime);
                        else overlays?.Center("STUNNED", 1.5f);
                        Shake(10f);
                    }
                    break;
                case EventKind.Staked:
                    if (e.A == me) overlays?.Center(e.B >= 2 ? "Sacrificed." : "Staked", 4f);
                    break;
                case EventKind.Eliminated:
                    // Gone into the woods: the word written out in the black (after the user's game Stroop).
                    if (e.A == me && e.Text == "woods") overlays?.WriteOut("Sacrificed", 2.6f);
                    else if (e.A == me) overlays?.Center("Sacrificed", 5f);
                    break;
                case EventKind.Escaped:
                    if (e.A == me) overlays?.Center("Escaped", 5f);
                    break;
                case EventKind.GatePowered:
                    overlays?.Center("The exit gate has power", 4f);
                    break;
                case EventKind.GateOpen:
                    overlays?.Center("THE GATE IS OPEN", 4f);
                    break;
                case EventKind.Jarvis:
                    overlays?.Big("JARVIS ONLINE");
                    Audio.Announce("voice.jarvis");
                    break;
                case EventKind.Hemp:
                    // Only the very first time it's used (ever): the text and the voice, never again.
                    if (PlayerPrefs.GetInt(HempAnnouncedKey, 0) == 0)
                    {
                        if (e.A == me) overlays?.Big("HEMP BATTERY ACTIVATED");
                        Audio.Announce("voice.hemp");
                        PlayerPrefs.SetInt(HempAnnouncedKey, 1);
                    }
                    break;
            }
        }
    }
}
