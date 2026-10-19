using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Ruined brick walls dotted about the map, somewhere new each match: a dozen or so broken stretches of old wall, some
    /// with the stub of a second wall turning a corner off one end, out on the open ground and in the woods (never on a
    /// path, in the lake, at a site or a generator's work site, or crowding a tree, a rock or each other). Each is solid and
    /// blocks sight like any wall; the machete strikes them as stone.
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>How many ruins stand on the default-sized map (more or fewer with its area).</summary>
        public const int RuinCount = 12;
        /// <summary>A ruin's wall: its thickness (a brick and a half), and its length and height where it still stands whole.</summary>
        public const float RuinThickness = 0.36f, RuinMinLength = 2.6f, RuinMaxLength = 4.6f, RuinMinHeight = 1.3f, RuinMaxHeight = 2.3f;

        /// <summary>Every piece of ruined wall (its two ends on the plane), for the map.</summary>
        public readonly List<(Vector2 a, Vector2 b)> MapRuins = new List<(Vector2, Vector2)>();

        void BuildRuins()
        {
            MapRuins.Clear();
            Transform parent = Group("Brick Ruins");
            System.Random r = FeatureRng(43);
            float R(float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);
            int target = Mathf.Max(1, Mathf.RoundToInt(RuinCount * halfExtent * halfExtent / (90f * 90f)));
            float lim = halfExtent - 6f;
            bool Clear(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.6f));
                for (int i = 0; i <= n; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)n);
                    if (p.magnitude > lim || !Free(p, 0.5f, 1.4f)) return false;
                    if ((p - Layout.Spawn).magnitude < 8f) return false;
                    foreach (Vector2 g in Layout.WoodsGenerators) if ((p - g).magnitude < 11f) return false;
                }
                return true;
            }
            void Mark(Vector2 a, Vector2 b)
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.6f));
                for (int i = 0; i <= n; i++) blocked.Add(Vector2.Lerp(a, b, i / (float)n));
                MapRuins.Add((a, b));
            }
            int placed = 0;
            for (int attempt = 0; attempt < target * 60 && placed < target; attempt++)
            {
                var p = new Vector2(R(-lim, lim), R(-lim, lim));
                float len = R(RuinMinLength, RuinMaxLength), height = R(RuinMinHeight, RuinMaxHeight), yaw = R(0f, Mathf.PI);
                var along = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
                Vector2 a = p - along * len * 0.5f, b = p + along * len * 0.5f;
                // Sometimes a second wall turns off one end, lower, to one side.
                bool corner = r.NextDouble() < 0.45;
                bool atB = r.NextDouble() < 0.5;
                float side = r.NextDouble() < 0.5 ? 1f : -1f, len2 = R(1.4f, 2.8f), height2 = R(0.8f, height);
                Vector2 c0 = atB ? b : a, c1 = c0 + new Vector2(-along.y, along.x) * side * len2;
                // (Its far end only reached once clear of the first's thickness.)
                Vector2 c0In = c0 + (c1 - c0).normalized * (RuinThickness * 0.5f);
                if (!Clear(a, b) || (corner && !Clear(c0In, c1))) continue;
                var broken = corner ? (atB ? LowPolyModels.RuinBreak.Left : LowPolyModels.RuinBreak.Right)
                    : (LowPolyModels.RuinBreak)r.Next(3);
                Wall("Brick Wall Ruin", a, b, height, RuinThickness, LowPolyModels.BrickRuin(r, len, height, RuinThickness, broken), true, true)
                    .transform.SetParent(parent, true);
                Mark(a, b);
                if (corner)
                {
                    // Joined to the first at the corner, broken off at its far end.
                    Vector2 s0 = c0 - (c1 - c0).normalized * (RuinThickness * 0.5f);
                    float l2 = Vector2.Distance(s0, c1);
                    Wall("Brick Wall Ruin", s0, c1, height2, RuinThickness, LowPolyModels.BrickRuin(r, l2, height2, RuinThickness, LowPolyModels.RuinBreak.Right), true, true)
                        .transform.SetParent(parent, true);
                    Mark(s0, c1);
                }
                placed++;
            }
        }
    }
}
