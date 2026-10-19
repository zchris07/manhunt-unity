using System.Collections.Generic;
using UnityEngine;
using Vision.Effects;
using Vision.Game;

namespace Vision.UI
{
    /// <summary>
    /// Wading: anyone moving through the lake (a player or an NPC) leaves rings rippling out over the water round their
    /// legs, a new one every third of a second or so (quicker the faster they wade), and a V of small wake rings trailing
    /// behind; standing still, the water settles.
    /// </summary>
    public sealed partial class MatchEffects
    {
        sealed class Wader { public Vector2 Last; public float Next; }
        readonly Dictionary<int, Wader> waders = new Dictionary<int, Wader>();
        readonly List<int> wadersGone = new List<int>();
        /// <summary>Rings started so far (tests).</summary>
        public int RippleCount { get; private set; }

        void DrawRipples(MatchSim sim)
        {
            if (world == null || world.Terrain == null || host == null || host.Geometry == null) return;
            float now = Time.time, dt = Mathf.Max(1e-4f, Time.deltaTime);
            foreach (SimPlayer p in sim.Order)
            {
                if (p.Role == Role.Spectator || p.Health == Health.Carried || p.Health == Health.Escaped) continue;
                if (p.Health == Health.Eliminated && !p.Ghost) continue;
                // (A ghost's wake only for those who can see a ghost.)
                if (Vision.Player.PlayerController.GhostForm(p) && !MatchSim.SeesGhosts(host.Local)) continue;
                Wade(p.Id, p.Pos, p.RadiusD, now, dt);
            }
            foreach (Npc n in sim.Npcs)
                if (n.Solid && !n.Gone) Wade(-n.Id, n.Pos, n.MoveRadius, now, dt);
            // Forget anyone not seen this frame.
            wadersGone.Clear();
            foreach (var kv in waders) if (now - kv.Value.Next > 3f) wadersGone.Add(kv.Key);
            foreach (int id in wadersGone) waders.Remove(id);
        }

        void Wade(int id, Vector2 at, float radius, float now, float dt)
        {
            if (!waders.TryGetValue(id, out Wader w))
            {
                waders[id] = new Wader { Last = at, Next = now };
                return;
            }
            Vector2 moved = at - w.Last;
            w.Last = at;
            float speed = moved.magnitude / dt;
            if (!host.Geometry.InWater(at))
            {
                w.Next = now;
                return;
            }
            if (speed < 0.25f || now < w.Next) return;
            // Quicker rings the faster they wade.
            w.Next = now + Mathf.Lerp(0.42f, 0.22f, Mathf.Clamp01(speed / 4f));
            float y = world.Terrain.LakeLevel + 0.03f;
            Vector3 c = world.transform.TransformPoint(new Vector3(at.x, y, at.y));
            float s = S;
            var water = new Color(0.62f, 0.76f, 0.86f, 0.38f);
            Fx.RingAt(c, radius * 0.6f * s, (radius + 0.9f) * s, 1.1f, water, 0.035f * s, false, true, Vfx.Blend.Alpha);
            // The wake: two small rings either side, just behind them.
            Vector2 back = moved.sqrMagnitude > 1e-8f ? -moved.normalized : Vector2.zero;
            Vector2 side = new Vector2(-back.y, back.x);
            foreach (float k in new[] { -1f, 1f })
            {
                Vector2 q = at + back * (radius + 0.25f) + side * k * (radius * 0.8f);
                Vector3 wc = world.transform.TransformPoint(new Vector3(q.x, y, q.y));
                Fx.RingAt(wc, 0.05f * s, 0.45f * s, 0.7f, new Color(water.r, water.g, water.b, 0.4f), 0.025f * s, false, true, Vfx.Blend.Alpha);
            }
            // A splash of droplets now and then.
            if (Random.value < 0.35f)
                Fx.Burst(c + Vector3.up * 0.1f * s, 4, 1.1f * s, 0.35f, new Color(0.75f, 0.85f, 0.92f, 0.8f), 0.025f * s, true, Vfx.Blend.Alpha, 0f, 4f, 0f);
            RippleCount++;
        }
    }
}
