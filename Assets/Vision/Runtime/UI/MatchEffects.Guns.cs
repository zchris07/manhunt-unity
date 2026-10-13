using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;

namespace Vision.UI
{
    /// <summary>
    /// The 0.50 cal: its laser while a survivor aims it (as the original draws it, a faint wide red line and a thin bright
    /// one from the barrel along the aim, seen by everyone, stopping at the first solid thing in its way), and its round
    /// (the original's streak racing out from the barrel, and bursts of sparks, dust and chips where it punches into and
    /// out of every wall, tree or object it goes through).
    /// </summary>
    public sealed partial class MatchEffects
    {
        struct Streak { public Vector3 From, Dir; public float Born, Length; }
        readonly List<Streak> streaks = new List<Streak>();
        readonly RaycastHit[] pierceHits = new RaycastHit[32];

        static Vector3 Flat(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        /// <summary>The direction of a ground angle (design plane) in the world.</summary>
        Vector3 WorldDir(float angle) => world.transform.TransformDirection(Flat(angle)).normalized;

        void DrawLasers(MatchSim sim)
        {
            float max = Scale.D(Balance.Items.Sniper.LaserMax) * S;
            foreach (SimPlayer p in sim.Order)
            {
                if (!p.Aiming || !MatchSim.LaserHolder(p)) continue;
                GameObject go = CharacterOf(p);
                ActionLayer layer = go != null ? go.GetComponent<ActionLayer>() : null;
                if (layer == null || !layer.Muzzle(out Vector3 from)) continue;
                Vector3 dir = WorldDir(p.Facing);
                float len = max;
                Vector3 hitAt = from + dir * max;
                bool blocked = false;
                int n = Physics.RaycastNonAlloc(from, dir, pierceHits, max, ~((1 << 2) | (1 << Vfx.SensesLayer)), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (pierceHits[i].collider.transform.IsChildOf(go.transform)) continue;
                    if (pierceHits[i].distance < len) { len = pierceHits[i].distance; hitAt = pierceHits[i].point; blocked = true; }
                }
                pts.Clear(); cols.Clear();
                pts.Add(from); pts.Add(from + dir * len);
                cols.Add(new Color(1f, 0.1f, 0.1f, 0.14f)); cols.Add(new Color(1f, 0.1f, 0.1f, 0.14f));
                Fx.Ribbon(pts, cols, 0.06f * S, false, true, Vfx.Blend.Additive, true);
                cols[0] = cols[1] = new Color(1f, 0.25f, 0.25f, 0.65f);
                Fx.Ribbon(pts, cols, 0.016f * S, false, true);
                Fx.Glow(from, 0.07f * S, new Color(1f, 0.3f, 0.3f, 0.9f));
                if (blocked) Fx.Glow(hitAt, 0.12f * S, new Color(1f, 0.2f, 0.2f, 0.95f));
            }
        }

        /// <summary>A 0.50 cal round leaving the barrel: its streak, and a burst wherever it enters or leaves something solid.</summary>
        void SniperRound(Vector3 muzzle, float angle)
        {
            Vector3 dir = WorldDir(angle);
            float len = Scale.D(Balance.Items.Sniper.LaserMax) * S;
            streaks.Add(new Streak { From = muzzle, Dir = dir, Born = Time.time, Length = len });
            int mask = Vfx.SolidMask | (1 << Vision.World.SandboxWorld.LowLayer);
            // Into: every surface met going out; out of: every surface met coming back from the far end.
            int n = Physics.RaycastNonAlloc(muzzle, dir, pierceHits, len, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < Mathf.Min(n, 16); i++) PierceBurst(pierceHits[i].point, -dir, pierceHits[i].normal);
            Vector3 far = muzzle + dir * len;
            n = Physics.RaycastNonAlloc(far, -dir, pierceHits, len, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < Mathf.Min(n, 16); i++) PierceBurst(pierceHits[i].point, dir, pierceHits[i].normal);
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

        /// <summary>The original's 0.50 cal streak: a bright segment racing out at 8000 units a second with a 380-unit tail.</summary>
        void DrawStreaks()
        {
            float now = Time.time, speed = Scale.D(Balance.Items.Sniper.Speed) * S, tail = Scale.D(380f) * S;
            for (int i = streaks.Count - 1; i >= 0; i--)
            {
                Streak st = streaks[i];
                float front = (now - st.Born) * speed;
                if (front - tail >= st.Length) { streaks.RemoveAt(i); continue; }
                Vector3 a = st.From + st.Dir * Mathf.Clamp(front - tail, 0f, st.Length), b = st.From + st.Dir * Mathf.Min(front, st.Length);
                pts.Clear(); cols.Clear();
                pts.Add(a); pts.Add(b);
                cols.Add(new Color(1f, 0.42f, 0.29f, 0f)); cols.Add(new Color(1f, 0.42f, 0.29f, 0.25f));
                Fx.Ribbon(pts, cols, 0.1f * S, false, true, Vfx.Blend.Additive, true);
                cols[0] = new Color(1f, 0.95f, 0.82f, 0f); cols[1] = new Color(1f, 0.95f, 0.82f, 0.95f);
                Fx.Ribbon(pts, cols, 0.032f * S, false, true);
            }
        }
    }
}
