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
    /// fast projectile: a short bright streak racing out from the barrel with a short, thin, dim trail behind it, stopping
    /// where the shot stopped (pellets spark there as they arrive). Rounds fly over the ground at the barrel's height and
    /// follow every rise and dip of the terrain: they never strike the ground, never pass under it, and no hill hides them
    /// (walls, trees and objects still do). The laser shows while a survivor aims the 0.50 cal (as the original draws it, a
    /// faint wide red line and a thin bright one, seen by everyone), along the same path, stopping at the first solid thing
    /// in its way; and where a 0.50 cal round punches into and out of a wall, tree or object, sparks, dust and chips burst
    /// out as it passes.
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
            Speed = 6000f, Trail = 1.6f, Head = 0.26f, Width = 0.013f,
            HeadColor = new Color(1f, 0.93f, 0.72f, 0.95f), TrailColor = new Color(1f, 0.84f, 0.55f, 0.26f),
        };
        static readonly RoundLook PelletRound = new RoundLook
        {
            Speed = 5000f, Trail = 1.1f, Head = 0.18f, Width = 0.01f,
            HeadColor = new Color(1f, 0.86f, 0.52f, 0.9f), TrailColor = new Color(1f, 0.76f, 0.4f, 0.22f),
        };
        static readonly RoundLook GoldenPellet = new RoundLook
        {
            Speed = 5000f, Trail = 1.1f, Head = 0.18f, Width = 0.011f,
            HeadColor = new Color(1f, 0.88f, 0.42f, 0.95f), TrailColor = new Color(1f, 0.8f, 0.3f, 0.24f),
        };
        static readonly RoundLook SniperLook = new RoundLook
        {
            Speed = Balance.Items.Sniper.Speed, Trail = 2.6f, Head = 0.4f, Width = 0.016f,
            HeadColor = new Color(1f, 0.95f, 0.82f, 1f), TrailColor = new Color(1f, 0.5f, 0.34f, 0.3f),
        };

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
            rounds.Add(new Round { From = from, To = to, Up = up, Born = Time.time, Look = look, Impact = impact });
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
                if (head - r.Look.Trail >= len)
                {
                    rounds.RemoveAt(i);
                    continue;
                }
                if (head >= len && r.Impact != 0)
                {
                    bool golden = r.Impact == 2;
                    Fx.Burst(FlightPoint(r.From, dir, r.Up, len), 3, 1.4f * s, 0.3f, golden ? new Color(1f, 0.85f, 0.42f, 1f) : new Color(1f, 0.76f, 0.23f, 1f), 0.04f * s);
                    r.Impact = 0;
                    rounds[i] = r;
                }
                float a = Mathf.Clamp(head - r.Look.Trail, 0f, len), b = Mathf.Min(head, len);
                if (b - a < 1e-4f) continue;
                // The trail: thin and dim, fading out toward its tail.
                pts.Clear(); cols.Clear();
                int n = Mathf.Clamp(Mathf.CeilToInt((b - a) / 0.4f), 1, 12);
                for (int k = 0; k <= n; k++)
                {
                    float t = k / (float)n;
                    pts.Add(FlightPoint(r.From, dir, r.Up, Mathf.Lerp(a, b, t)));
                    Color c = r.Look.TrailColor;
                    c.a *= t * t;
                    cols.Add(c);
                }
                Fx.Ribbon(pts, cols, r.Look.Width * s, false);
                // The round itself: a short bright streak at the front, while it's still flying.
                if (head >= len) continue;
                pts.Clear(); cols.Clear();
                pts.Add(FlightPoint(r.From, dir, r.Up, Mathf.Max(a, b - r.Look.Head)));
                pts.Add(FlightPoint(r.From, dir, r.Up, b));
                Color hc = r.Look.HeadColor;
                cols.Add(new Color(hc.r, hc.g, hc.b, hc.a * 0.3f));
                cols.Add(hc);
                Fx.Ribbon(pts, cols, r.Look.Width * 1.7f * s, false);
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
            // Walls, trees, objects and other bodies stop it; never the ground.
            int mask = ~((1 << 2) | (1 << Vfx.SensesLayer) | (1 << SandboxWorld.GroundLayer));
            foreach (SimPlayer p in sim.Order)
            {
                if (!p.Aiming || !MatchSim.LaserHolder(p)) continue;
                GameObject go = CharacterOf(p);
                ActionLayer layer = go != null ? go.GetComponent<ActionLayer>() : null;
                if (layer == null || !layer.Muzzle(out Vector3 muzzle)) continue;
                Vector2 from = OnPlane(muzzle, out float up);
                Vector2 dir = new Vector2(Mathf.Cos(p.Facing), Mathf.Sin(p.Facing));
                // Along the ground piece by piece, to the first thing in the way.
                pts.Clear(); cols.Clear();
                pts.Add(muzzle);
                Vector3 prev = muzzle;
                bool blocked = false;
                for (float at = FlightStep; !blocked; at += FlightStep)
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
                            if (pierceHits[i].collider.transform.IsChildOf(go.transform)) continue;
                            if (pierceHits[i].distance < first) { first = pierceHits[i].distance; blocked = true; }
                        }
                        next = prev + seg / sl * first;
                    }
                    pts.Add(next);
                    prev = next;
                    if (sAt >= max) break;
                }
                for (int i = 0; i < pts.Count; i++) cols.Add(new Color(1f, 0.1f, 0.1f, 0.14f));
                Fx.Ribbon(pts, cols, 0.06f * S, false, true, Vfx.Blend.Additive, true);
                for (int i = 0; i < cols.Count; i++) cols[i] = new Color(1f, 0.25f, 0.25f, 0.65f);
                Fx.Ribbon(pts, cols, 0.016f * S, false, true);
                Fx.Glow(muzzle, 0.07f * S, new Color(1f, 0.3f, 0.3f, 0.9f));
                if (blocked) Fx.Glow(prev, 0.12f * S, new Color(1f, 0.2f, 0.2f, 0.95f));
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
            float len = d.magnitude, speed = Scale.D(SniperLook.Speed), s = S;
            if (len < 1e-4f) return;
            Vector2 dir = d / len;
            int mask = Vfx.SolidMask;
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
                        pierces.Add(new Pierce { At = r.Born + (a + pierceHits[i].distance * k) / speed, Pos = pierceHits[i].point, Out = -sd, Normal = pierceHits[i].normal });
                    n = Physics.RaycastNonAlloc(next, -sd, pierceHits, sl, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                        pierces.Add(new Pierce { At = r.Born + (b - pierceHits[i].distance * k) / speed, Pos = pierceHits[i].point, Out = sd, Normal = pierceHits[i].normal });
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
            for (int i = 0; i < 9; i++)
            {
                Vector3 v = (spray + Random.insideUnitSphere * 0.7f + Vector3.up * 0.3f).normalized * Random.Range(3f, 9f) * s;
                Fx.Emit(at, v, Random.Range(0.12f, 0.3f), Color.Lerp(new Color(1f, 0.92f, 0.65f, 1f), new Color(1f, 0.6f, 0.25f, 1f), Random.value),
                    Random.Range(0.02f, 0.04f) * s, 9.8f * s, 1.5f, true, false, 1f, true);
            }
            for (int i = 0; i < 4; i++)
                Fx.Emit(at + Random.insideUnitSphere * 0.05f * s, (spray * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.4f) * s,
                    Random.Range(0.6f, 1.1f), new Color(0.42f, 0.4f, 0.36f, 0.4f), Random.Range(0.1f, 0.18f) * s, 0f, 2.2f, false, false, 2.6f, true, false, Vfx.Blend.Alpha);
            for (int i = 0; i < 5; i++)
                Fx.Emit(at, (spray + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f).normalized * Random.Range(2f, 5f) * s,
                    Random.Range(0.5f, 0.9f), new Color(0.24f, 0.21f, 0.17f, 1f), Random.Range(0.025f, 0.045f) * s, 12f * s, 0.5f, false, false, 1f, true, false, Vfx.Blend.Alpha);
        }

        public int RoundCount => rounds.Count;
    }
}
