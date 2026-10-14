using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>The graveyard's way in: a pair of ornate wrought-iron gates between stone pillars, chained and padlocked.</summary>
    public static partial class LowPolyModels
    {
        static readonly Color GateIron = new Color(0.12f, 0.11f, 0.11f), GateGilt = new Color(0.46f, 0.37f, 0.16f);
        static readonly Color PillarStone = new Color(0.43f, 0.41f, 0.38f), PillarCap = new Color(0.36f, 0.35f, 0.33f);

        /// <summary>How tall a gate leaf stands at its hinge and where it meets the other (its arch rises toward the middle).</summary>
        public const float GateHingeHeight = 1.78f, GatePeakHeight = 2.2f;

        /// <summary>The top of the arch over a leaf <paramref name="width"/> wide, <paramref name="x"/> along from the hinge.</summary>
        static float GateArch(float x, float width) => GateHingeHeight + (GatePeakHeight - GateHingeHeight) * Mathf.Sin(Mathf.Clamp01(x / width) * Mathf.PI * 0.5f);

        /// <summary>
        /// One leaf of the graveyard's gates along +X from its hinge (x = 0) for <paramref name="width"/>: stiles and rails,
        /// a top rail arching up toward the middle, bars rising past it into gilded spear points, a row of rings along the
        /// foot, a pair of scrolls under the arch and a ringed cross at its heart.
        /// </summary>
        public static Mesh GateLeaf(System.Random rng, float width)
        {
            var b = new LowPolyMeshBuilder(rng);
            const float t = 0.05f;
            // Stiles, the meeting one taller, each with a finial.
            b.AddBox(new Vector3(0f, 0f, -t * 0.5f), new Vector3(0.06f, GateHingeHeight + 0.08f, t), GateIron, 0.05f);
            b.AddBox(new Vector3(width - 0.06f, 0f, -t * 0.5f), new Vector3(0.06f, GatePeakHeight + 0.08f, t), GateIron, 0.05f);
            b.AddBlob(new Vector3(0.03f, GateHingeHeight + 0.13f, 0f), new Vector3(0.045f, 0.045f, 0.045f), 0, 0.05f, _ => GateGilt);
            b.AddCone(new Vector3(width - 0.03f, GatePeakHeight + 0.08f, 0f), new Vector3(width - 0.03f, GatePeakHeight + 0.3f, 0f), 0.05f, 4, GateGilt, 0.05f);
            // Rails: the foot, above the rings, the middle; the arched top.
            foreach (float y in new[] { 0.1f, 0.5f, 1.02f })
                b.AddBox(new Vector3(0.04f, y - 0.025f, -0.02f), new Vector3(width - 0.08f, 0.05f, 0.04f), GateIron, 0.05f);
            var arch = new List<Vector3>();
            var archR = new List<float>();
            for (int i = 0; i <= 10; i++)
            {
                float x = width * i / 10f;
                arch.Add(new Vector3(x, GateArch(x, width), 0f));
                archR.Add(0.026f);
            }
            b.AddTube(arch, archR, 4, GateIron);
            // Bars up through the arch into spear points.
            for (float x = 0.13f; x < width - 0.1f; x += 0.11f)
            {
                float top = GateArch(x, width) + 0.1f;
                b.AddBox(new Vector3(x - 0.011f, 0.1f, -0.011f), new Vector3(0.022f, top - 0.1f, 0.022f), GateIron, 0.05f);
                b.AddCone(new Vector3(x, top, 0f), new Vector3(x, top + 0.13f, 0f), 0.028f, 4, GateGilt, 0.06f);
            }
            // Rings along the foot, between the rails.
            for (float x = 0.185f; x < width - 0.12f; x += 0.11f) Ring(b, new Vector3(x, 0.3f, 0f), 0.045f, 0.009f, 8, GateIron);
            // Two scrolls curling under the arch, back to back, and a ringed cross in the middle.
            float mid = width * 0.5f;
            Scroll(b, new Vector3(mid - 0.2f, 1.36f, 0f), 0.15f, -1f);
            Scroll(b, new Vector3(mid + 0.2f, 1.36f, 0f), 0.15f, 1f);
            Ring(b, new Vector3(mid, 0.76f, 0f), 0.15f, 0.014f, 12, GateIron);
            b.AddBox(new Vector3(mid - 0.014f, 0.56f, -0.014f), new Vector3(0.028f, 0.42f, 0.028f), GateGilt, 0.05f);
            b.AddBox(new Vector3(mid - 0.12f, 0.8f, -0.014f), new Vector3(0.24f, 0.028f, 0.028f), GateGilt, 0.05f);
            return b.ToMesh("Gate Leaf");
        }

        /// <summary>A ring of iron in the leaf's plane.</summary>
        static void Ring(LowPolyMeshBuilder b, Vector3 c, float radius, float thick, int n, Color col)
        {
            var pts = new List<Vector3>();
            var rs = new List<float>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts.Add(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
                rs.Add(thick);
            }
            b.AddTube(pts, rs, 4, col);
        }

        /// <summary>A C-scroll: a spiral curling in on itself, turning one way or the other.</summary>
        static void Scroll(LowPolyMeshBuilder b, Vector3 c, float radius, float side)
        {
            var pts = new List<Vector3>();
            var rs = new List<float>();
            for (int i = 0; i <= 16; i++)
            {
                float u = i / 16f, a = Mathf.PI * 0.5f + side * u * Mathf.PI * 2.2f, r = radius * (1f - 0.72f * u);
                pts.Add(c + new Vector3(Mathf.Cos(a) * r * side, Mathf.Sin(a) * r - radius * 0.2f, 0f));
                rs.Add(0.013f);
            }
            b.AddTube(pts, rs, 4, GateIron);
            b.AddBlob(pts[pts.Count - 1], new Vector3(0.022f, 0.022f, 0.022f), 0, 0.05f, _ => GateGilt);
        }

        /// <summary>A stone gate pillar: a plinth, a shaft with a sunk panel, a moulded cap and a ball on top.</summary>
        public static Mesh GatePillar(System.Random rng, float height)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-0.27f, 0f, -0.27f), new Vector3(0.54f, 0.3f, 0.54f), PillarStone, 0.06f);
            b.AddBox(new Vector3(-0.22f, 0.3f, -0.22f), new Vector3(0.44f, height - 0.52f, 0.44f), PillarStone, 0.07f);
            foreach (float s in new[] { -1f, 1f })
            {
                b.AddBox(new Vector3(-0.15f, 0.55f, s * 0.22f - 0.01f), new Vector3(0.3f, height - 1.05f, 0.02f), PillarCap, 0.05f);
                b.AddBox(new Vector3(s * 0.22f - 0.01f, 0.55f, -0.15f), new Vector3(0.02f, height - 1.05f, 0.3f), PillarCap, 0.05f);
            }
            b.AddBox(new Vector3(-0.29f, height - 0.22f, -0.29f), new Vector3(0.58f, 0.09f, 0.58f), PillarCap, 0.05f);
            b.AddBox(new Vector3(-0.25f, height - 0.13f, -0.25f), new Vector3(0.5f, 0.07f, 0.5f), PillarStone, 0.05f);
            b.AddBox(new Vector3(-0.08f, height - 0.06f, -0.08f), new Vector3(0.16f, 0.08f, 0.16f), PillarCap, 0.05f);
            b.AddBlob(new Vector3(0f, height + 0.12f, 0f), new Vector3(0.15f, 0.15f, 0.15f), 1, 0.04f, _ => PillarStone);
            return b.ToMesh("Gate Pillar");
        }

        /// <summary>A chain wound round the meeting stiles (centred on x = 0) and a brass padlock hanging from it.</summary>
        public static Mesh GateLock(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color steel = new Color(0.32f, 0.31f, 0.3f), brass = new Color(0.58f, 0.46f, 0.2f);
            // Links, alternately flat and on edge, looping round the two stiles.
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                Vector3 c = new Vector3(Mathf.Cos(a) * 0.12f, 1.08f + Mathf.Sin(a * 2f) * 0.02f, Mathf.Sin(a) * 0.06f);
                var pts = new List<Vector3>();
                var rs = new List<float>();
                Vector3 tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a) * 0.5f).normalized;
                Vector3 up = i % 2 == 0 ? Vector3.up : Vector3.Cross(tang, Vector3.up).normalized;
                for (int k = 0; k <= 6; k++)
                {
                    float u = k * Mathf.PI * 2f / 6f;
                    pts.Add(c + tang * Mathf.Cos(u) * 0.035f + up * Mathf.Sin(u) * 0.018f);
                    rs.Add(0.007f);
                }
                b.AddTube(pts, rs, 3, steel);
            }
            // The padlock: its shackle through the chain, the body below.
            var sh = new List<Vector3>();
            var shr = new List<float>();
            for (int k = 0; k <= 8; k++)
            {
                float u = k * Mathf.PI / 8f;
                sh.Add(new Vector3(Mathf.Cos(u) * 0.035f, 0.99f + Mathf.Sin(u) * 0.05f, 0.07f));
                shr.Add(0.008f);
            }
            b.AddTube(sh, shr, 4, steel);
            b.AddBox(new Vector3(-0.05f, 0.88f, 0.05f), new Vector3(0.1f, 0.11f, 0.04f), brass, 0.06f);
            b.AddBox(new Vector3(-0.008f, 0.91f, 0.089f), new Vector3(0.016f, 0.03f, 0.004f), new Color(0.1f, 0.08f, 0.05f), 0.02f);
            return b.ToMesh("Gate Lock");
        }
    }
}
