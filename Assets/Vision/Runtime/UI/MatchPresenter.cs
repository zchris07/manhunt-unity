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

        float zoomK;

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
        /// The camera: the Hemp Battery zooms Zach out (eased in and out), Waz's field of view widens or narrows it, and once
        /// you are out of the match it follows someone still in it (click or the arrow keys switch who).
        /// </summary>
        void UpdateCamera(SimPlayer me)
        {
            if (cameraRig == null || player == null) return;
            float dt = Time.deltaTime;
            zoomK = Mathf.MoveTowards(zoomK, me.Role == Role.Hunter && me.HempOn ? 1f : 0f, dt * Balance.Hunter.Hemp.ZoomRate);
            float e = zoomK * zoomK * (3f - 2f * zoomK);
            float fov = me.FovMul > 0f ? me.FovMul : 1f;
            cameraRig.zoom = (1f + (Balance.Hunter.Hemp.ZoomOut - 1f) * e) / fov;

            bool out_ = me.Role == Role.Spectator || me.Health == Health.Escaped || me.Health == Health.Eliminated;
            Transform target = player.transform;
            Spectating = 0;
            if (out_)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                bool next = (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame && !GameHud.MenuOpen);
                bool prev = kb != null && kb.leftArrowKey.wasPressedThisFrame;
                if (next || prev) host.Sim.CycleSpectate(me, next ? 1 : -1);
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
        readonly System.Collections.Generic.List<Vector3> ribbonPts = new System.Collections.Generic.List<Vector3>(128);
        readonly System.Collections.Generic.List<Color> ribbonCols = new System.Collections.Generic.List<Color>(128);
        static readonly System.Comparison<TrailPoint> ByTime = (a, b) => a.T.CompareTo(b.T);

        void DrawTrails(SimPlayer me)
        {
            MatchSim sim = host.Sim;
            bool zach = me.Role == Role.Hunter;
            if (!zach && !sim.TestMode) return;
            Vfx fx = Vfx.Instance;
            foreach (var l in byWho.Values) l.Clear();
            foreach (TrailPoint t in sim.Trails)
            {
                if (!zach && t.Who != me.Id) continue;
                float age = (sim.Time - t.T) / Balance.Trails.MaxAgeSec;
                if (age >= 1f || age < 0f) continue;
                if (t.Kind == 1)
                {
                    float a = Mathf.Pow(1f - age, 1.3f) * 0.62f;
                    fx.Dot(Ground(t.Pos, 0.05f), (0.12f + age * 0.12f) * S, new Color(0.82f, 0.06f, 0.12f, a), true);
                    continue;
                }
                if (!byWho.TryGetValue(t.Who, out var list)) byWho[t.Who] = list = new System.Collections.Generic.List<TrailPoint>(64);
                list.Add(t);
            }
            foreach (var kv in byWho)
            {
                var pts = kv.Value;
                if (pts.Count < 2) continue;
                pts.Sort(ByTime);
                int start = 0;
                for (int i = 1; i <= pts.Count; i++)
                {
                    // Break where the trail really breaks (they stopped sprinting for a while).
                    bool cut = i == pts.Count || pts[i].T - pts[i - 1].T > 0.7f || Vector2.Distance(pts[i].Pos, pts[i - 1].Pos) > Scale.D(160f);
                    if (!cut) continue;
                    int n = i - start;
                    if (n >= 2)
                        for (int strand = 0; strand < 3; strand++)
                        {
                            ribbonPts.Clear();
                            ribbonCols.Clear();
                            for (int k = 0; k < n; k++)
                            {
                                TrailPoint t = pts[start + k];
                                Vector2 dir = k + 1 < n ? pts[start + k + 1].Pos - t.Pos : t.Pos - pts[start + k - 1].Pos;
                                Vector2 side = new Vector2(-dir.y, dir.x).normalized;
                                float off = Mathf.Sin(t.T * 3.1f + strand * 2.1f + kv.Key) * Scale.D(6f + strand * 3f);
                                ribbonPts.Add(Ground(t.Pos + side * off, 0.06f));
                                float age = (sim.Time - t.T) / Balance.Trails.MaxAgeSec;
                                ribbonCols.Add(new Color(strand == 0 ? 1f : 0.88f, strand == 0 ? 0.35f : 0.12f, strand == 0 ? 0.35f : 0.18f, Mathf.Pow(1f - age, 1.1f) * (strand == 0 ? 0.5f : 0.22f)));
                            }
                            fx.Ribbon(ribbonPts, ribbonCols, Scale.D(strand == 0 ? 3f : 9f) * S, true);
                        }
                    start = i;
                }
            }
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
                    overlays?.ShowNote(e.A);
                    break;
                case EventKind.Hit:
                    if (e.A == me) Shake(14f);
                    else Shake(6f * Near(e.Pos, 300f));
                    // Blood (a white flash for the Hemp beam).
                    if (e.Text == "beam") Vfx.Instance.Burst(Ground(e.Pos, 0.9f), 14, 2.6f * S, 0.45f, new Color(0.94f, 1f, 0.94f, 1f), 0.06f * S);
                    else Vfx.Instance.Burst(Ground(e.Pos, 0.9f), e.Text == "pellet" ? 6 : 16, 2.2f * S, 0.5f, new Color(0.48f, 0.05f, 0.08f, 1f), 0.07f * S, true, Vfx.Blend.Alpha, 6f * S, 2f, 0.4f, new Color(0.3f, 0.02f, 0.03f, 1f));
                    break;
                case EventKind.Breath:
                    // Breathing heard through a hiding spot's door (a gasp is louder): rings through the dark.
                    Vfx.Instance.RingAt(Ground(e.Pos, 0.1f), Scale.D(10f) * S, Scale.D(e.F > 0f ? 70f : 50f) * S, 1.4f, new Color(0.85f, 0.94f, 1f, 0.7f), 0.05f * S, true);
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
                    if (e.A == me) overlays?.Center("Sacrificed", 5f);
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
                    break;
                case EventKind.Hemp:
                    if (e.A == me) overlays?.Big("HEMP BATTERY ACTIVATED");
                    break;
            }
        }
    }
}
