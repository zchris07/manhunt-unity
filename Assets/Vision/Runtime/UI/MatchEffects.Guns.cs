using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// Rounds in flight and the 0.50 cal's laser. Every round (a pistol's, each pellet, the 0.50 cal's) is drawn as a small
    /// fast projectile: a bright streak racing out from the barrel with a long, dim trail behind it (half again as big as
    /// they were, and twice the trail; the hit isn't touched, only the look), stopping
    /// where the shot stopped (pellets spark there as they arrive). Rounds fly perfectly straight and level at the barrel's
    /// height, whatever the ground does below (where they stop is found along the ground, as the match plays it): they
    /// never strike the ground, and no hill hides them, drawn over the terrain and hidden only where a wall, tree or prop
    /// stands between them and the camera (judged from just over the ground where the line runs under a rise, so the
    /// buried feet of what stands on the rise never cut it; fallen logs never do). The laser shows while a survivor aims the 0.50 cal (as the original draws it, a
    /// faint wide red line and a thin bright one, seen by everyone), stopping at the first solid thing in its way (found
    /// along the ground, the way the round goes) but drawn as a perfectly straight line from the barrel: above the terrain,
    /// so no hill ever covers it, and broken only where a wall, tree or prop stands between it and the camera. The 0.50 cal
    /// round flies that same straight line, the same way; and where it punches into and out of a wall, tree or object,
    /// sparks, dust and chips burst out of the line as it passes.
    /// </summary>
    public sealed partial class MatchEffects
    {
        /// <summary>How a kind of round looks: its speed (original units a second), trail and streak (metres), width and colours.</summary>
        struct RoundLook
        {
            public float Speed, Trail, Head, Width;
            public Color HeadColor, TrailColor;
        }

        static readonly RoundLook PistolRound = new RoundLook
        {
            Speed = 6000f * RoundPace, Trail = 3.2f * 2f, Head = 0.39f * 3f, Width = 0.0195f * 3f,
            HeadColor = new Color(1f, 0.93f, 0.72f, 0.95f), TrailColor = new Color(1f, 0.84f, 0.55f, 0.26f),
        };
        static readonly RoundLook PelletRound = new RoundLook
        {
            Speed = 5000f * RoundPace, Trail = 2.2f * 2f, Head = 0.27f * 3f, Width = 0.015f * 3f,
            HeadColor = new Color(1f, 0.86f, 0.52f, 0.9f), TrailColor = new Color(1f, 0.76f, 0.4f, 0.22f),
        };
        static readonly RoundLook GoldenPellet = new RoundLook
        {
            Speed = 5000f * RoundPace, Trail = 2.2f * 2f, Head = 0.27f * 3f, Width = 0.0165f * 3f,
            HeadColor = new Color(1f, 0.88f, 0.42f, 0.95f), TrailColor = new Color(1f, 0.8f, 0.3f, 0.24f),
        };
        static readonly RoundLook SniperLook = new RoundLook
        {
            Speed = Balance.Items.Sniper.Speed * RoundPace, Trail = 5.2f * 2f, Head = 0.6f * 3f, Width = 0.024f * 3f,
            HeadColor = new Color(1f, 0.95f, 0.82f, 1f), TrailColor = new Color(1f, 0.5f, 0.34f, 0.3f),
        };

        /// <summary>
        /// How fast rounds are drawn flying, against the speeds the match uses (half: they're seen crossing the screen).
        /// Only the look: what they hit, and when, is the match's.
        /// </summary>
        public const float RoundPace = 0.5f;
        /// <summary>How much thicker a round's trail is than at first (its length is doubled in each look).</summary>
        public const float TrailThick = 2f;

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
            /// where it ends (<see cref="B3"/>), whatever the ground does below; drawn over the terrain (no hill covers it)
            /// and hidden only by what stands between it and the camera.
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
        /// Hidden from the camera by something standing upright between it and <paramref name="p"/> (walls, trees, rocks,
        /// props; never the ground nor a fallen log), judged as if <paramref name="p"/> were at least <paramref name="up"/>
        /// over the ground there, as the line is at the barrel, so a stretch of it running under a rise of the ground is
        /// never cut by the buried feet of the trees and rocks on that rise (see <see cref="Vfx.Covered"/>).
        /// </summary>
        bool Hidden(Camera cam, Vector3 p, float up)
        {
            if (cam == null) return false;
            Vector3 back = cam.orthographic ? -cam.transform.forward : (cam.transform.position - p).normalized;
            if (back.y > 1e-3f)
                for (int k = 0; k < 8; k++)
                {
                    float floor = Floor(p, up);
                    if (p.y >= floor - 1e-3f) break;
                    p += back * ((floor - p.y) / back.y);
                }
            return Vfx.Covered(cam, p);
        }

        /// <summary>The world height <paramref name="up"/> over the ground straight under <paramref name="p"/>.</summary>
        float Floor(Vector3 p, float up)
        {
            Vector3 l = world.transform.InverseTransformPoint(p);
            return world.transform.TransformPoint(new Vector3(l.x, world.SurfaceHeight(new Vector2(l.x, l.z)) + up, l.z)).y;
        }

        readonly List<Vector3> runPts = new List<Vector3>(256);
        readonly List<Color> runCols = new List<Color>(256);

        /// <summary>
        /// A straight line through the points in <see cref="pts"/>, drawn above the terrain (so no hill ever covers it), broken
        /// wherever something stands between it and the camera (so walls, trees and props still hide it).
        /// </summary>
        void StraightLine(float width, bool soft, Vfx.Blend blend, float up)
        {
            Camera cam = Camera.main;
            runPts.Clear();
            runCols.Clear();
            for (int i = 0; i <= pts.Count; i++)
            {
                bool shown = i < pts.Count && !Hidden(cam, pts[i], up);
                if (shown)
                {
                    runPts.Add(pts[i]);
                    runCols.Add(cols[i]);
                    continue;
                }
                if (runPts.Count >= 2) Fx.Ribbon(runPts, runCols, width, false, true, blend, soft);
                runPts.Clear();
                runCols.Clear();
            }
        }

        /// <summary>A burst due where a 0.50 cal round goes into or out of something, when the round gets there.</summary>
        struct Pierce { public float At; public Vector3 Pos, Out, Normal; }

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
            for (int i = rounds.Count - 1; i >= 0; i--)
            {
                Round r = rounds[i];
                Vector2 d = r.To - r.From;
                float len = d.magnitude;
                Vector2 dir = len > 1e-5f ? d / len : Vector2.right;
                float head = (now - r.Born) * Scale.D(r.Look.Speed);
                // Gone once its trail has burned out past where it landed (a whole trail's length on).
                if (head - 2f * r.Look.Trail >= len)
                {
                    rounds.RemoveAt(i);
                    continue;
                }
                if (head >= len && r.Impact != 0)
                {
                    bool golden = r.Impact == 2;
                    // Over the terrain like the round (a rise of the ground never covers the sparks).
                    Vector3 hit = RoundPoint(r, dir, len, len);
                    bool over = Vfx.UnderGround(Camera.main, hit);
                    Fx.Burst(hit, 3, 1.4f * s, 0.3f, golden ? new Color(1f, 0.85f, 0.42f, 1f) : new Color(1f, 0.76f, 0.23f, 1f), 0.06f * s, senses: over);
                    r.Impact = 0;
                    rounds[i] = r;
                }
                // The tail follows a trail's length behind; once the round has landed it creeps in at half the pace, so the
                // trail stays on screen twice as long.
                float tail = head < len ? head - r.Look.Trail : len - r.Look.Trail + (head - len) * 0.5f;
                float a = Mathf.Clamp(tail, 0f, len), b = Mathf.Min(head, len);
                if (b - a < 1e-4f) continue;
                // The trail: a long streak of hot air behind it, a soft glow round a bright core, fading toward its tail
                // (and, once the round has landed, the rest of it burning out where it flew).
                pts.Clear(); cols.Clear();
                int n = Mathf.Clamp(Mathf.CeilToInt((b - a) / 0.4f), 1, 24);
                float landed = head >= len ? Mathf.Clamp01(1f - (head - len) / Mathf.Max(0.01f, 2f * r.Look.Trail)) : 1f;
                for (int k = 0; k <= n; k++)
                {
                    float t = k / (float)n;
                    pts.Add(RoundPoint(r, dir, len, Mathf.Lerp(a, b, t)));
                    Color c = r.Look.TrailColor;
                    c.a *= t * t * Mathf.Lerp(0.4f, 1f, landed);
                    cols.Add(c);
                }
                float trailW = r.Look.Width / 3f * 1.2f * TrailThick * s;
                for (int k = 0; k < cols.Count; k++) cols[k] = new Color(cols[k].r, cols[k].g * 0.85f, cols[k].b * 0.7f, cols[k].a * 0.55f);
                StraightLine(trailW * 3f, true, Vfx.Blend.Additive, r.Up);
                for (int k = 0; k < cols.Count; k++) cols[k] = new Color(1f, 0.97f, 0.88f, cols[k].a * 1.6f);
                StraightLine(trailW, false, Vfx.Blend.Additive, r.Up);
                // The round itself: a big bright streak at the front with a white-hot tip and a halo of light round it.
                if (head >= len) continue;
                pts.Clear(); cols.Clear();
                pts.Add(RoundPoint(r, dir, len, Mathf.Max(a, b - r.Look.Head)));
                pts.Add(RoundPoint(r, dir, len, b));
                Color hc = r.Look.HeadColor;
                cols.Add(new Color(hc.r, hc.g, hc.b, hc.a * 0.3f));
                cols.Add(hc);
                StraightLine(r.Look.Width * 1.7f * s, false, Vfx.Blend.Additive, r.Up);
                cols[0] = new Color(1f, 1f, 1f, 0f);
                cols[1] = new Color(1f, 1f, 1f, 0.95f);
                StraightLine(r.Look.Width * 0.6f * s, false, Vfx.Blend.Additive, r.Up);
                Vector3 tip = RoundPoint(r, dir, len, b);
                if (!Hidden(Camera.main, tip, r.Up)) Fx.Glow(tip, r.Look.Width * 9f * s, new Color(hc.r, hc.g * 0.9f, hc.b * 0.75f, 0.55f));
            }
            for (int i = pierces.Count - 1; i >= 0; i--)
            {
                if (pierces[i].At > now) continue;
                PierceBurst(pierces[i].Pos, pierces[i].Out, pierces[i].Normal);
                pierces.RemoveAt(i);
            }
        }

        void DrawLasers(MatchSim sim)
        {
            float max = Scale.D(Balance.Items.Sniper.LaserMax);
            // Walls, trees, objects and other bodies stop it; never the ground, nor a fallen log or tree (shots pass over them).
            int mask = ~((1 << 2) | (1 << Vfx.SensesLayer) | (1 << SandboxWorld.GroundLayer) | (1 << SandboxWorld.LowLayer));
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
                // Drawn as a perfectly straight line from the barrel to there: over the terrain (no hill ever covers it),
                // hidden only where something stands between it and the camera.
                Vector3 end = LevelEnd(muzzle, dir, stop);
                int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(muzzle, end) / (0.35f * S)), 2, 240);
                pts.Clear(); cols.Clear();
                for (int i = 0; i <= samples; i++)
                {
                    pts.Add(Vector3.Lerp(muzzle, end, i / (float)samples));
                    cols.Add(new Color(1f, 0.1f, 0.1f, 0.14f));
                }
                StraightLine(0.06f * S, true, Vfx.Blend.Additive, up);
                for (int i = 0; i < cols.Count; i++) cols[i] = new Color(1f, 0.25f, 0.25f, 0.65f);
                StraightLine(0.016f * S, false, Vfx.Blend.Additive, up);
                Camera cam = Camera.main;
                if (!Hidden(cam, muzzle, up)) Fx.Glow(muzzle, 0.07f * S, new Color(1f, 0.3f, 0.3f, 0.9f));
                if (blocked && !Hidden(cam, end, up)) Fx.Glow(end, 0.12f * S, new Color(1f, 0.2f, 0.2f, 0.95f));
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
                        pierces.Add(new Pierce { At = r.Born + at / speed, Pos = RoundPoint(r, dir, len, at), Out = -sd, Normal = pierceHits[i].normal });
                    }
                    n = Physics.RaycastNonAlloc(next, -sd, pierceHits, sl, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        float at = b - pierceHits[i].distance * k;
                        pierces.Add(new Pierce { At = r.Born + at / speed, Pos = RoundPoint(r, dir, len, at), Out = sd, Normal = pierceHits[i].normal });
                    }
                }
                prev = next;
            }
            if (pierces.Count > 64) pierces.RemoveRange(0, pierces.Count - 64);
        }

        /// <summary>Sparks, dust and chips thrown out of a surface the round passed through, mostly along <paramref name="out_"/>.</summary>
        void PierceBurst(Vector3 at, Vector3 out_, Vector3 normal)
        {
            float s = S;
            Vector3 spray = (out_ + normal * 0.6f).normalized;
            // On the round's line, over the terrain like it: where a rise of the ground would cover it, on the overlay.
            bool over = Vfx.UnderGround(Camera.main, at), masked = !over;
            for (int i = 0; i < 9; i++)
            {
                Vector3 v = (spray + Random.insideUnitSphere * 0.7f + Vector3.up * 0.3f).normalized * Random.Range(3f, 9f) * s;
                Fx.Emit(at, v, Random.Range(0.12f, 0.3f), Color.Lerp(new Color(1f, 0.92f, 0.65f, 1f), new Color(1f, 0.6f, 0.25f, 1f), Random.value),
                    Random.Range(0.02f, 0.04f) * s, 9.8f * s, 1.5f, true, false, 1f, masked, over);
            }
            for (int i = 0; i < 4; i++)
                Fx.Emit(at + Random.insideUnitSphere * 0.05f * s, (spray * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.4f) * s,
                    Random.Range(0.6f, 1.1f), new Color(0.42f, 0.4f, 0.36f, 0.4f), Random.Range(0.1f, 0.18f) * s, 0f, 2.2f, false, false, 2.6f, masked, over, Vfx.Blend.Alpha);
            for (int i = 0; i < 5; i++)
                Fx.Emit(at, (spray + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f).normalized * Random.Range(2f, 5f) * s,
                    Random.Range(0.5f, 0.9f), new Color(0.24f, 0.21f, 0.17f, 1f), Random.Range(0.025f, 0.045f) * s, 12f * s, 0.5f, false, false, 1f, masked, over, Vfx.Blend.Alpha);
        }

        public int RoundCount => rounds.Count;
        /// <summary>The straight, level line a round flies (tests).</summary>
        public (Vector3 from, Vector3 to) RoundLine(int i) => (rounds[i].A3, rounds[i].B3);
    }
}
