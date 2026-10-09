using UnityEngine;

namespace Vision.World
{
    public static partial class LowPolyModels
    {
        /// <summary>
        /// A scarecrow stake (the original's): a weathered post with a crossbar at shoulder height, rope wound at the top
        /// and round the crossbar where the wrists go, a few tufts of old straw, and dark stains down the post.
        /// </summary>
        public static Mesh Stake(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color wood = new Color(0.33f, 0.26f, 0.19f), dark = new Color(0.20f, 0.15f, 0.11f), rope = new Color(0.46f, 0.40f, 0.29f);
            Color straw = new Color(0.58f, 0.50f, 0.28f), stain = new Color(0.22f, 0.06f, 0.05f);
            float lean = b.Range(-0.04f, 0.04f);
            Vector3 top = new Vector3(lean, 2.35f, b.Range(-0.03f, 0.03f));
            b.AddFrustum(Vector3.zero, top, 0.075f, 0.055f, 6, wood, 0.06f);
            b.AddCone(top, top + new Vector3(0f, 0.12f, 0f), 0.056f, 6, dark, 0.05f, false);
            // The crossbar where the arms are tied, a little off level.
            float tilt = b.Range(-0.05f, 0.05f);
            Vector3 bar = new Vector3(lean * 0.8f, 1.78f, 0f);
            b.AddTube(new[] { bar + new Vector3(-0.62f, -tilt, -0.06f), bar + new Vector3(0.62f, tilt, -0.06f) }, new[] { 0.042f, 0.038f }, 5, wood, 0.06f);
            // Rope: round the top of the post and at each end of the bar.
            b.AddFrustum(new Vector3(lean * 0.95f, 2.0f, 0f), new Vector3(lean * 0.95f, 2.1f, 0f), 0.072f, 0.07f, 6, rope, 0.05f, 0f, false, false);
            foreach (float s in new[] { -1f, 1f })
                b.AddTube(new[] { bar + new Vector3(s * 0.5f, s * tilt * 0.8f, -0.06f), bar + new Vector3(s * 0.58f, s * tilt * 0.93f, -0.06f) }, new[] { 0.055f, 0.055f }, 5, rope, 0.05f, 0f, 0f, null, false, false);
            // Straw sticking out of the bindings.
            for (int i = 0; i < 4; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                Vector3 at = bar + new Vector3(s * b.Range(0.5f, 0.58f), s * tilt * 0.85f, -0.06f);
                b.AddDoubleSided(at, at + new Vector3(s * b.Range(0.05f, 0.12f), b.Range(-0.18f, -0.08f), b.Range(-0.05f, 0.05f)), at + new Vector3(s * 0.02f, -0.02f, 0.03f), straw);
            }
            // Old stains running down the front of the post.
            Vector3 f0 = new Vector3(lean * 0.6f, 1.5f, 0.071f), f1 = new Vector3(lean * 0.2f, 0.6f, 0.074f);
            b.AddDoubleSided(f0 + new Vector3(-0.03f, 0f, 0f), f0 + new Vector3(0.03f, 0f, 0f), f1, stain);
            return b.ToMesh("Stake");
        }
    }
}
