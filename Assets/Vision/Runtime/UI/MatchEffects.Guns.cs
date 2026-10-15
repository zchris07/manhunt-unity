using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// Rounds in flight and the 0.50 cal's laser, drawn over the ground and behind whatever stands nearer, by the GPU
    /// (Vision/FxOverGround): the ground marks the pixels where it's the nearest thing, the effect always shows there, and
    /// anywhere else it's depth-tested against what's actually drawn, so no hill ever cuts it and every tree, wall, rock,
    /// prop and body in front of it hides it exactly where that thing is drawn. Where the line runs level under a rise of
    /// the ground, its points are first slid toward the camera along its view (the same spot on screen) to the barrel's
    /// height over the ground there, so things standing on the rise behind it never cover it.
    /// Every round (a pistol's, each pellet, the 0.50 cal's) is a single thin streak of white pixels, as Noita's are: bright
    /// at its head and fading to nothing at its tail, racing out from the barrel to where the shot stopped (pellets spark
    /// there as they arrive). Rounds fly perfectly straight and level at the barrel's height, whatever the ground does below
    /// (where they stop is found along the ground, as the match plays it). The laser shows while a survivor aims the 0.50
    /// cal (seen by everyone), stopping at the first solid thing in its way (found along the ground, the way the round goes)
    /// but drawn as a perfectly straight line from the barrel, Noita's laser sight: a faint red haze with a one-pixel core
    /// of flickering red static, broken into dashes, with dust drifting out along it. The 0.50 cal round flies that same
    /// straight line; where it punches into and out of a wall, tree or object, sparks, dust and chips burst out of the line
    /// as it passes.
    /// </summary>
    public sealed partial class MatchEffects
    {
        /// <summary>How a kind of round looks: its speed (original units a second), streak (design units) and width (metres) and colour.</summary>
        struct RoundLook
        {
            public float Speed, Streak, Width;
            public Color Color;
        }

        static readonly RoundLook PistolRound = new RoundLook
        {
            Speed = 6000f * RoundPace, Streak = 1.6f, Width = 0.0195f, Color = new Color(1f, 1f, 1f, 1f),
        };
        static readonly RoundLook PelletRound = new RoundLook
        {
            Speed = 5000f * RoundPace, Streak = 1.1f, Width = 0.015f, Color = new Color(1f, 1f, 1f, 0.95f),
        };
        static readonly RoundLook GoldenPellet = new RoundLook
        {
            Speed = 5000f * RoundPace, Streak = 1.1f, Width = 0.0165f, Color = new Color(1f, 0.96f, 0.82f, 1f),
        };
        static readonly RoundLook SniperLook = new RoundLook
        {
            Speed = Balance.Items.Sniper.Speed * RoundPace, Streak = 2.6f, Width = 0.024f, Color = new Color(1f, 1f, 1f, 1f),
        };

        /// <summary>
        /// How fast rounds are drawn flying, against the speeds the match uses (three quarters: they're seen crossing the
        /// screen). Only the look: what they hit, and when, is the match's.
        /// </summary>
        public const float RoundPace = 0.75f;
        /// <summary>A round's streak is never thinner than this many of the screen's art pixels.</summary>
        public const float RoundPixels = 1f;
        /// <summary>The laser's ribbon: its haze and drifting dust reach this many art pixels across in all.</summary>
        public const float LaserPixels = 9f;
        public static readonly Color LaserRed = new Color(1f, 0.13f, 0.1f, 1f);

        struct Round
        {
            /// <summary>On the ground plane (design units): the barrel's spot, and where the round stopped.</summary>
            public Vector2 From, To;
            /// <summary>The barrel's height over the ground, kept all the way (design units).</summary>
            public float Up;
            public float Born;
            public RoundLook Look;
            /// <summary>Sparks where it stops: 0 none, 1 a pellet's, 2 a golden pellet's.</summary>
            public int Impact;
            /// <summary>
            /// The line it flies: perfectly straight and level from the barrel (<see cref="A3"/>) at the barrel's height to
            /// where it ends (<see cref="B3"/>), whatever the ground does below.
            /// </summary>
            public Vector3 A3, B3;
        }

        /// <summary>Where a round is <paramref name="s"/> design units along its flight.</summary>
        static Vector3 RoundPoint(in Round r, Vector2 dir, float len, float s) => Vector3.Lerp(r.A3, r.B3, len > 1e-5f ? s / len : 0f);

        /// <summary>The level line from the barrel: <paramref name="lenDesign"/> along the ground direction, at the barrel's height.</summary>
        Vector3 LevelEnd(Vector3 muzzle, Vector2 dir, float lenDesign)
        {
            Vector3 d = world.transform.TransformDirection(new Vector3(dir.x, 0f, dir.y));
            d.y = 0f;
            return muzzle + d.normalized * (lenDesign * S);
        }

        /// <summary>
        /// <paramref name="p"/> slid toward the camera along its view (the same spot on screen, nearer) until it's at least
        /// <paramref name="up"/> over the ground straight under it: a stretch of line running level under a rise of the
        /// ground is then judged as lying on the rise, so nothing standing on it behind the line covers it.
        /// </summary>
        public Vector3 Lift(Camera cam, Vector3 p, float up)
        {
            if (cam == null || world == null) return p;
            Vector3 back = cam.orthographic ? -cam.transform.forward : (cam.transform.position - p).normalized;
            if (back.y <= 1e-3f) return p;
            for (int k = 0; k < 8; k++)
            {
                float floor = Floor(p, up);
                if (p.y >= floor - 1e-3f) break;
                p += back * ((floor - p.y) / back.y);
            }
            return p;
        }

        /// <summary>The world height <paramref name="up"/> over the ground straight under <paramref name="p"/>.</summary>
        float Floor(Vector3 p, float up)
        {
            Vector3 l = world.transform.InverseTransformPoint(p);
            return world.transform.TransformPoint(new Vector3(l.x, world.SurfaceHeight(new Vector2(l.x, l.z)) + up, l.z)).y;
        }

        /// <summary>A burst due where a 0.50 cal round goes into or out of something, when the round gets there.</summary>
        struct Pierce { public float At, Up; public Vector3 Pos, Out, Normal; }

        readonly List<Round> rounds = new List<Round>();
        readonly List<Pierce> pierces = new List<Pierce>();
        readonly RaycastHit[] pierceHits = new RaycastHit[32];

        /// <summary>Rounds and the laser are checked against the scene in pieces this long (metres) as they follow the ground.</summary>
        const float FlightStep = 1.5f;

        static Vector3 Flat(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        /// <summary>The direction of a ground angle (design plane) in the world.</summary>
        Vector3 WorldDir(float angle) => world.transform.TransformDirection(Flat(angle)).normalized;

        /// <summary>A point of the world on the ground plane (design units), and its height over the drawn ground there.</summary>
        Vector2 OnPlane(Vector3 worldAt, out float up)
        {
            Vector3 local = world.transform.InverseTransformPoint(worldAt);
            var at = new Vector2(local.x, local.z);
            up = Mathf.Clamp(local.y - world.SurfaceHeight(at), 0.3f, 2f);
            return at;
        }

        /// <summary>Where something flying <paramref name="s"/> design units along from <paramref name="from"/> is: <paramref name="up"/> over the ground there.</summary>
        public Vector3 FlightPoint(Vector2 from, Vector2 dir, float up, float s)
        {
            Vector2 at = from + dir * s;
            return world.transform.TransformPoint(new Vector3(at.x, world.SurfaceHeight(at) + up, at.y));
        }

        /// <summary>A round leaving the barrel for where it stopped (<paramref name="units"/> from the shooter's centre along <paramref name="angle"/>).</summary>
        void Fire(Vector3 muzzle, Vector2 shooter, float angle, float units, in RoundLook look, int impact)
        {
            Vector2 from = OnPlane(muzzle, out float up);
            Vector2 to = shooter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Scale.D(units);
            Vector2 d = to - from;
            float len = d.magnitude;
            Vector3 end = len > 1e-5f ? LevelEnd(muzzle, d / len, len) : muzzle;
            rounds.Add(new Round { From = from, To = to, Up = up, Born = Time.time, Look = look, Impact = impact, A3 = muzzle, B3 = end });
        }

        void DrawRounds()
        {
            float now = Time.time, s = S;
            Camera cam = Camera.main;
            float minW = Vfx.ScreenPixels(cam, RoundPixels);
            for (int i = rounds.Count - 1; i >= 0; i--)
            {
                Round r = rounds[i];
                Vector2 d = r.To - r.From;
                float len = d.magnitude;
                Vector2 dir = len > 1e-5f ? d / len : Vector2.right;
                float head = (now - r.Born) * Scale.D(r.Look.Speed);
                float tail = head - r.Look.Streak;
                // Gone once its tail has reached where it landed.
                if (tail >= len)
                {
                    rounds.RemoveAt(i);
                    continue;
                }
                if (head >= len && r.Impact != 0)
                {
                    bool golden = r.Impact == 2;
                    Vector3 hit = Lift(cam, RoundPoint(r, dir, len, len), r.Up);
                    Fx.Burst(hit, 3, 1.4f * s, 0.3f, golden ? new Color(1f, 0.85f, 0.42f, 1f) : new Color(1f, 0.76f, 0.23f, 1f), 0.06f * s, false, over: true);
                    r.Impact = 0;
                    rounds[i] = r;
                }
                float a = Mathf.Clamp(tail, 0f, len), b = Mathf.Min(head, len);
                if (b - a < 1e-4f) continue;
                // One streak of white pixels: bright at its head, nothing at its tail (cut short where it leaves the barrel
                // and where it lands).
                pts.Clear(); cols.Clear();
                const int n = 4;
                for (int k = 0; k <= n; k++)
                {
                    float sAt = Mathf.Lerp(a, b, k / (float)n);
                    pts.Add(Lift(cam, RoundPoint(r, dir, len, sAt), r.Up));
                    Color c = r.Look.Color;
                    c.a *= Mathf.Clamp01((sAt - tail) / Mathf.Max(1e-4f, r.Look.Streak));
                    cols.Add(c);
                }
                Fx.Ribbon(pts, cols, Mathf.Max(r.Look.Width * s, minW), false, false, Vfx.Blend.Additive, false, over: true);
            }
            for (int i = pierces.Count - 1; i >= 0; i--)
            {
                if (pierces[i].At > now) continue;
                PierceBurst(Lift(cam, pierces[i].Pos, pierces[i].Up), pierces[i].Out, pierces[i].Normal);
                pierces.RemoveAt(i);
            }
        }

        void DrawLasers(MatchSim sim)
        {
            float max = Scale.D(Balance.Items.Sniper.LaserMax);
            // Walls, trees, objects and other bodies stop it; never the ground, nor a fallen log or tree (shots pass over them).
            int mask = ~((1 << 2) | (1 << Vfx.SensesLayer) | (1 << Vfx.OverGroundLayer) | (1 << SandboxWorld.GroundLayer) | (1 << SandboxWorld.LowLayer));
            Camera cam = Camera.main;
            foreach (SimPlayer p in sim.Order)
            {
                if (!p.Aiming || !MatchSim.LaserHolder(p)) continue;
                GameObject go = CharacterOf(p);
                ActionLayer layer = go != null ? go.GetComponent<ActionLayer>() : null;
                if (layer == null || !layer.Muzzle(out Vector3 muzzle)) continue;
                Vector2 from = OnPlane(muzzle, out float up);
                Vector2 dir = new Vector2(Mathf.Cos(p.Facing), Mathf.Sin(p.Facing));
                // Where it stops: found along the ground piece by piece (the way the round goes), at the first thing in the way.
                float stop = LaserStop(go, from, dir, up, max, mask, out bool blocked);
                // Drawn as a perfectly straight line from the barrel to there, over the ground and behind whatever's nearer.
                Vector3 end = LevelEnd(muzzle, dir, stop);
                int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(muzzle, end) / (0.35f * S)), 2, 240);
                pts.Clear();
                for (int i = 0; i <= samples; i++) pts.Add(Lift(cam, Vector3.Lerp(muzzle, end, i / (float)samples), up));
                Fx.Laser(pts, Vfx.ScreenPixels(cam, LaserPixels), LaserRed, p.Id * 0.137f);
                // A small flickering dot at the barrel; where it meets something, a bigger sputtering spot throwing off the odd mote.
                float px = Vfx.ScreenPixels(cam, 1f);
                Fx.Dot(pts[0], px * Random.Range(2f, 3f), new Color(1f, 0.3f, 0.25f, Random.Range(0.55f, 1f)), false, false, Vfx.Blend.Additive, over: true);
                if (!blocked) continue;
                Vector3 hitAt = pts[pts.Count - 1];
                Fx.Glow(hitAt, px * Random.Range(5f, 8f), new Color(1f, 0.2f, 0.16f, Random.Range(0.6f, 1f)), false, false, Vfx.Blend.Additive, over: true);
                Fx.Dot(hitAt, px * 2f, new Color(1f, 0.85f, 0.75f, Random.Range(0.5f, 1f)), false, false, Vfx.Blend.Additive, over: true);
                if (Random.value < 6f * Time.deltaTime)
                {
                    Vector3 back = (pts[0] - hitAt).normalized;
                    Fx.Emit(hitAt, (back + Random.insideUnitSphere * 0.8f).normalized * Random.Range(0.4f, 1.2f) * S, Random.Range(0.25f, 0.5f),
                        new Color(1f, 0.6f, 0.45f, 1f), px * 1.2f, 0f, 2f, false, false, 1f, false, false, Vfx.Blend.Additive, over: true);
                }
            }
        }

        /// <summary>How far (design units) the laser runs along the ground before something stops it.</summary>
        float LaserStop(GameObject shooter, Vector2 from, Vector2 dir, float up, float max, int mask, out bool blocked)
        {
            blocked = false;
            Vector3 prev = FlightPoint(from, dir, up, 0f);
            float prevS = 0f;
            for (float at = FlightStep; ; at += FlightStep)
            {
                float sAt = Mathf.Min(at, max);
                Vector3 next = FlightPoint(from, dir, up, sAt);
                Vector3 seg = next - prev;
                float sl = seg.magnitude;
                if (sl > 1e-5f)
                {
                    float first = sl;
                    int n = Physics.RaycastNonAlloc(prev, seg / sl, pierceHits, sl, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        if (pierceHits[i].collider.transform.IsChildOf(shooter.transform)) continue;
                        if (pierceHits[i].distance < first) { first = pierceHits[i].distance; blocked = true; }
                    }
                    if (blocked) return prevS + (sAt - prevS) * first / sl;
                }
                if (sAt >= max) return max;
                prev = next;
                prevS = sAt;
            }
        }

        /// <summary>
        /// A 0.50 cal round leaving the barrel: it flies the whole way, through everything, and bursts out of every wall, tree
        /// or object it meets, going in and coming out, as it gets there.
        /// </summary>
        void SniperRound(Vector3 muzzle, Vector2 shooter, float angle)
        {
            Fire(muzzle, shooter, angle, Balance.Items.Sniper.LaserMax, SniperLook, 0);
            Round r = rounds[rounds.Count - 1];
            Vector2 d = r.To - r.From;
            float len = d.magnitude, speed = Scale.D(SniperLook.Speed);
            if (len < 1e-4f) return;
            Vector2 dir = d / len;
            // It flies the laser's perfectly straight, level line, from the barrel to the end of its reach (Fire's line).
            int mask = Vfx.SolidMask & ~(1 << SandboxWorld.LowLayer);
            Vector3 prev = FlightPoint(r.From, dir, r.Up, 0f);
            for (float a = 0f; a < len; a += FlightStep)
            {
                float b = Mathf.Min(len, a + FlightStep);
                Vector3 next = FlightPoint(r.From, dir, r.Up, b);
                Vector3 seg = next - prev;
                float sl = seg.magnitude;
                if (sl > 1e-5f)
                {
                    Vector3 sd = seg / sl;
                    float k = (b - a) / sl;
                    // In: every surface met going out along this piece; out: every surface met coming back along it.
                    int n = Physics.RaycastNonAlloc(prev, sd, pierceHits, sl, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        // A burst on the straight line it's drawn along, where it meets the thing.
                        float at = a + pierceHits[i].distance * k;
                        pierces.Add(new Pierce { At = r.Born + at / speed, Up = r.Up, Pos = RoundPoint(r, dir, len, at), Out = -sd, Normal = pierceHits[i].normal });
                    }
                    n = Physics.RaycastNonAlloc(next, -sd, pierceHits, sl, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        float at = b - pierceHits[i].distance * k;
                        pierces.Add(new Pierce { At = r.Born + at / speed, Up = r.Up, Pos = RoundPoint(r, dir, len, at), Out = sd, Normal = pierceHits[i].normal });
                    }
                }
                prev = next;
            }
            if (pierces.Count > 64) pierces.RemoveRange(0, pierces.Count - 64);
        }

        readonly RaycastHit[] bladeHits = new RaycastHit[16];

        /// <summary>
        /// Zach's machete striking: a fan of rays across the swing's arc at chest height, out to its reach (a charged swing's
        /// longer and wider); where the nearest meets something solid (a wall, tree, rock, prop, fence or log; never the
        /// ground or a body), sparks, chips and dust fly off it, more for a charged swing. Only the look: what the swing
        /// hits is the match's.
        /// </summary>
        void MacheteImpact(SimPlayer by, bool heavy)
        {
            GameObject go = CharacterOf(by);
            if (go == null || world == null) return;
            float reach = Scale.D(Balance.Hunter.Attack.Range) * (heavy ? Balance.Hunter.Attack.ChargeRangeMul : 1f) * S;
            float arc = Balance.Hunter.Attack.ArcDeg * (heavy ? Balance.Hunter.Attack.ChargeArcMul : 1f) * Mathf.Deg2Rad;
            Vector3 from = go.transform.position + Vector3.up * 1.05f * S;
            int mask = Vfx.SolidMask & ~((1 << Vfx.OverGroundLayer) | (1 << SandboxWorld.GroundLayer) | (1 << SandboxWorld.CharacterLayer));
            const int rays = 9;
            float best = float.MaxValue;
            Vector3 at = Vector3.zero, normal = Vector3.up, dir = Vector3.forward;
            Collider struck = null;
            for (int i = 0; i < rays; i++)
            {
                Vector3 d = WorldDir(by.Facing + arc * (i / (rays - 1f) - 0.5f));
                int n = Physics.RaycastNonAlloc(from, d, bladeHits, reach, mask, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    if (bladeHits[k].collider.transform.IsChildOf(go.transform) || bladeHits[k].distance >= best) continue;
                    best = bladeHits[k].distance;
                    struck = bladeHits[k].collider;
                    at = bladeHits[k].point;
                    normal = bladeHits[k].normal;
                    dir = d;
                }
            }
            if (best == float.MaxValue) return;
            Surface what = SurfaceOf(struck);
            SurfaceBurst(at, Vector3.Reflect(dir, normal), normal, what, heavy ? 1.7f : 1f);
            if (what == Surface.Wood) Audio.PlayCue("machete_wood", Units(by.Pos));
        }

        /// <summary>What a struck thing is made of, as its debris shows it.</summary>
        public enum Surface { Wood, Stone, Metal, Glass, Plaster }

        static readonly (string word, Surface what)[] SurfaceWords =
        {
            ("Window", Surface.Glass), ("Glass", Surface.Glass), ("Pane", Surface.Glass),
            ("Iron", Surface.Metal), ("Gate", Surface.Metal), ("Generator", Surface.Metal), ("Car", Surface.Metal), ("Ambulance", Surface.Metal),
            ("Flood", Surface.Metal), ("Light Tower", Surface.Metal), ("Locker", Surface.Metal), ("Metal", Surface.Metal), ("Barrel", Surface.Metal),
            ("Steel", Surface.Metal), ("Pole", Surface.Metal), ("Swing", Surface.Metal), ("Slide", Surface.Metal), ("Spool", Surface.Metal),
            ("Rock", Surface.Stone), ("Boulder", Surface.Stone), ("Grave", Surface.Stone), ("Pillar", Surface.Stone), ("Stone", Surface.Stone),
            ("Plank", Surface.Wood), ("Wood", Surface.Wood), ("Log", Surface.Wood), ("Door", Surface.Wood), ("Wardrobe", Surface.Wood),
            ("Table", Surface.Wood), ("Chair", Surface.Wood), ("Crate", Surface.Wood), ("Fence", Surface.Wood), ("Pallet", Surface.Wood),
            ("Barricade", Surface.Wood), ("Bed", Surface.Wood), ("Stake", Surface.Wood), ("Tree", Surface.Wood), ("Trunk", Surface.Wood),
            ("Stump", Surface.Wood), ("Spruce", Surface.Wood), ("Pine", Surface.Wood), ("Fir", Surface.Wood), ("Oak", Surface.Wood),
            ("Snag", Surface.Wood), ("Counter", Surface.Wood), ("Shelf", Surface.Wood), ("Desk", Surface.Wood),
        };

        /// <summary>Reads a struck collider's material from its name and its parents' (walls with nothing else are plaster).</summary>
        public static Surface SurfaceOf(Collider c)
        {
            for (Transform t = c != null ? c.transform : null; t != null; t = t.parent)
                foreach (var (word, what) in SurfaceWords)
                    if (t.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return what;
            return Surface.Plaster;
        }

        /// <summary>
        /// Debris off a struck surface, by what it's made of, flung mostly along <paramref name="out_"/>: wood throws long
        /// splinters, bark chips and a puff of sawdust; stone grey chips, grit and a few sparks with a cloud of dust; metal a
        /// shower of sparks and dark flecks; glass bright shards; plaster and concrete white chips in a big dust cloud.
        /// <paramref name="k"/> scales how much (a charged swing's more).
        /// </summary>
        void SurfaceBurst(Vector3 at, Vector3 out_, Vector3 normal, Surface what, float k)
        {
            float s = S;
            Vector3 spray = (out_ + normal * 0.8f).normalized;
            int N(float n) => Mathf.RoundToInt(n * k);
            Vector3 V(float spread, float up, float lo, float hi) => (spray + Random.insideUnitSphere * spread + Vector3.up * up).normalized * Random.Range(lo, hi) * s;
            void Chip(Color a, Color b, float lo, float hi, float size, float life, bool streak = false, float g = 12f) =>
                Fx.Emit(at, V(0.8f, 0.6f, lo, hi), Random.Range(life * 0.6f, life), Color.Lerp(a, b, Random.value), Random.Range(size * 0.6f, size) * s, g * s, 0.6f, streak, false, 1f, false, false, Vfx.Blend.Alpha, true);
            void Dust(Color c, float size, float life, float grow) =>
                Fx.Emit(at + Random.insideUnitSphere * 0.08f * s, (spray * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.5f) * s, Random.Range(life * 0.6f, life), c, Random.Range(size * 0.6f, size) * s, 0f, 2.4f, false, false, grow, false, false, Vfx.Blend.Alpha, true);
            void Spark(int n)
            {
                for (int i = 0; i < n; i++)
                    Fx.Emit(at, V(0.7f, 0.3f, 3f, 9f), Random.Range(0.12f, 0.3f), Color.Lerp(new Color(1f, 0.93f, 0.66f, 1f), new Color(1f, 0.58f, 0.22f, 1f), Random.value),
                        Random.Range(0.02f, 0.04f) * s, 9.8f * s, 1.5f, true, false, 1f, false, false, Vfx.Blend.Additive, true);
            }
            switch (what)
            {
                case Surface.Wood:
                    for (int i = 0; i < N(12); i++) Chip(new Color(0.62f, 0.46f, 0.28f, 1f), new Color(0.82f, 0.68f, 0.46f, 1f), 2.5f, 6.5f, 0.06f, 0.9f, true);
                    for (int i = 0; i < N(5); i++) Chip(new Color(0.22f, 0.16f, 0.11f, 1f), new Color(0.34f, 0.25f, 0.17f, 1f), 1.5f, 4f, 0.07f, 1.1f);
                    for (int i = 0; i < N(6); i++) Dust(new Color(0.72f, 0.6f, 0.42f, 0.45f), 0.16f, 1.1f, 2.4f);
                    break;
                case Surface.Stone:
                    for (int i = 0; i < N(10); i++) Chip(new Color(0.38f, 0.37f, 0.35f, 1f), new Color(0.58f, 0.56f, 0.53f, 1f), 2f, 5.5f, 0.05f, 0.8f);
                    for (int i = 0; i < N(8); i++) Chip(new Color(0.5f, 0.48f, 0.45f, 1f), new Color(0.66f, 0.64f, 0.6f, 1f), 1f, 3f, 0.025f, 0.6f);
                    for (int i = 0; i < N(7); i++) Dust(new Color(0.55f, 0.53f, 0.5f, 0.42f), 0.2f, 1.4f, 2.8f);
                    Spark(N(4));
                    break;
                case Surface.Metal:
                    Spark(N(20));
                    for (int i = 0; i < N(5); i++) Chip(new Color(0.12f, 0.12f, 0.13f, 1f), new Color(0.3f, 0.3f, 0.32f, 1f), 2f, 5f, 0.025f, 0.6f);
                    for (int i = 0; i < N(2); i++) Dust(new Color(0.4f, 0.4f, 0.42f, 0.3f), 0.14f, 0.9f, 2f);
                    break;
                case Surface.Glass:
                    for (int i = 0; i < N(14); i++)
                        Fx.Emit(at, V(0.9f, 0.5f, 2f, 6f), Random.Range(0.5f, 1f), Color.Lerp(new Color(0.75f, 0.92f, 1f, 0.9f), new Color(1f, 1f, 1f, 1f), Random.value),
                            Random.Range(0.02f, 0.05f) * s, 12f * s, 0.5f, true, true, 1f, false, false, Vfx.Blend.Additive, true);
                    for (int i = 0; i < N(2); i++) Dust(new Color(0.8f, 0.85f, 0.88f, 0.25f), 0.12f, 0.8f, 2f);
                    break;
                default:
                    for (int i = 0; i < N(9); i++) Chip(new Color(0.7f, 0.68f, 0.64f, 1f), new Color(0.88f, 0.86f, 0.82f, 1f), 1.5f, 4.5f, 0.045f, 0.8f);
                    for (int i = 0; i < N(10); i++) Dust(new Color(0.8f, 0.78f, 0.74f, 0.45f), 0.24f, 1.6f, 3.2f);
                    break;
            }
        }

        /// <summary>Sparks, dust and chips thrown out of a surface the round passed through, mostly along <paramref name="out_"/>.</summary>
        void PierceBurst(Vector3 at, Vector3 out_, Vector3 normal)
        {
            float s = S;
            Vector3 spray = (out_ + normal * 0.6f).normalized;
            // On the round's line, drawn like it: over the ground, behind whatever stands nearer.
            const bool masked = false, senses = false;
            for (int i = 0; i < 9; i++)
            {
                Vector3 v = (spray + Random.insideUnitSphere * 0.7f + Vector3.up * 0.3f).normalized * Random.Range(3f, 9f) * s;
                Fx.Emit(at, v, Random.Range(0.12f, 0.3f), Color.Lerp(new Color(1f, 0.92f, 0.65f, 1f), new Color(1f, 0.6f, 0.25f, 1f), Random.value),
                    Random.Range(0.02f, 0.04f) * s, 9.8f * s, 1.5f, true, false, 1f, masked, senses, Vfx.Blend.Additive, true);
            }
            for (int i = 0; i < 4; i++)
                Fx.Emit(at + Random.insideUnitSphere * 0.05f * s, (spray * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.4f) * s,
                    Random.Range(0.6f, 1.1f), new Color(0.42f, 0.4f, 0.36f, 0.4f), Random.Range(0.1f, 0.18f) * s, 0f, 2.2f, false, false, 2.6f, masked, senses, Vfx.Blend.Alpha, true);
            for (int i = 0; i < 5; i++)
                Fx.Emit(at, (spray + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f).normalized * Random.Range(2f, 5f) * s,
                    Random.Range(0.5f, 0.9f), new Color(0.24f, 0.21f, 0.17f, 1f), Random.Range(0.025f, 0.045f) * s, 12f * s, 0.5f, false, false, 1f, masked, senses, Vfx.Blend.Alpha, true);
        }

        public int RoundCount => rounds.Count;
        /// <summary>The straight, level line a round flies (tests).</summary>
        public (Vector3 from, Vector3 to) RoundLine(int i) => (rounds[i].A3, rounds[i].B3);
    }
}
