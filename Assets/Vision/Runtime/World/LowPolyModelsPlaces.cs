using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Miga Sushi and the Diddy Chamber: their walls, a sushi bar with stools, the noren curtain and sign, a big comfy bed,
    /// wall chains and bottles of baby oil. Origin at the bottom centre, front facing +Z, as the furniture.
    /// </summary>
    public static partial class LowPolyModels
    {
        /// <summary>A plain painted (or stone) wall, <paramref name="length"/> long, centred on its origin like the plank walls.</summary>
        public static Mesh PlainWall(System.Random rng, float length, float height, float thickness, Color color)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-length * 0.5f, 0f, -thickness * 0.5f), new Vector3(length, height, thickness), color, 0.05f);
            // A darker skirting and a cap along the top, so it reads as built, not a block.
            b.AddBox(new Vector3(-length * 0.5f, 0f, -thickness * 0.5f - 0.01f), new Vector3(length, 0.2f, thickness + 0.02f), color * 0.7f, 0.03f);
            b.AddBox(new Vector3(-length * 0.5f, height - 0.08f, -thickness * 0.5f - 0.01f), new Vector3(length, 0.08f, thickness + 0.02f), color * 0.8f, 0.03f);
            return b.ToMesh("Wall");
        }

        /// <summary>A sushi bar: a long pale-wood counter with a dark top, a glass case of fish along it, <paramref name="length"/> long.</summary>
        public static Mesh SushiCounter(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            var pale = new Color(0.78f, 0.66f, 0.48f);
            b.AddBox(new Vector3(-length * 0.5f, 0f, -0.35f), new Vector3(length, 1.0f, 0.7f), pale, 0.04f);
            b.AddBox(new Vector3(-length * 0.5f - 0.03f, 1.0f, -0.4f), new Vector3(length + 0.06f, 0.06f, 0.8f), new Color(0.22f, 0.15f, 0.1f), 0.03f);
            // The case: glass over trays of salmon, tuna and egg.
            b.AddBox(new Vector3(-length * 0.4f, 1.06f, -0.3f), new Vector3(length * 0.8f, 0.24f, 0.32f), new Color(0.7f, 0.85f, 0.9f), 0.02f);
            Color[] fish = { new Color(0.98f, 0.52f, 0.36f), new Color(0.78f, 0.12f, 0.16f), new Color(0.98f, 0.86f, 0.32f), new Color(0.96f, 0.96f, 0.92f) };
            int n = Mathf.Max(3, Mathf.RoundToInt(length * 2f));
            for (int i = 0; i < n; i++)
            {
                float x = -length * 0.38f + (i + 0.5f) * length * 0.76f / n;
                b.AddBox(new Vector3(x - 0.08f, 1.1f, -0.22f), new Vector3(0.16f, 0.06f, 0.14f), fish[i % fish.Length], 0.05f);
            }
            // Plates of nigiri along the top.
            for (int i = 0; i < 4; i++)
            {
                float x = -length * 0.35f + i * length * 0.23f;
                b.AddFrustum(new Vector3(x, 1.06f, 0.18f), new Vector3(x, 1.08f, 0.18f), 0.12f, 0.12f, 8, new Color(0.12f, 0.12f, 0.14f), 0.02f);
                b.AddBox(new Vector3(x - 0.06f, 1.08f, 0.14f), new Vector3(0.12f, 0.04f, 0.07f), new Color(0.95f, 0.95f, 0.92f), 0.03f);
                b.AddBox(new Vector3(x - 0.065f, 1.12f, 0.135f), new Vector3(0.13f, 0.02f, 0.08f), fish[i % 3], 0.04f);
            }
            return b.ToMesh("Sushi Counter");
        }

        /// <summary>A bar stool: a round seat on a single post.</summary>
        public static Mesh Stool(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.04f, 0f), 0.2f, 0.2f, 8, Furn.MetalDark, 0.03f);
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.68f, 0f), 0.035f, 0.035f, 6, Furn.Metal, 0.03f);
            b.AddFrustum(new Vector3(0f, 0.68f, 0f), new Vector3(0f, 0.75f, 0f), 0.2f, 0.19f, 10, new Color(0.62f, 0.12f, 0.12f), 0.04f);
            return b.ToMesh("Stool");
        }

        /// <summary>A noren: the split cloth curtain over a restaurant's doorway, indigo with a white band, <paramref name="width"/> wide.</summary>
        public static Mesh Noren(System.Random rng, float width)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-width * 0.5f - 0.05f, 2.2f, -0.03f), new Vector3(width + 0.1f, 0.04f, 0.06f), Furn.WoodDark, 0.03f);
            int panels = 3;
            float w = width / panels;
            for (int i = 0; i < panels; i++)
            {
                float x = -width * 0.5f + i * w + 0.02f;
                b.AddBox(new Vector3(x, 1.55f, -0.01f), new Vector3(w - 0.04f, 0.65f, 0.02f), new Color(0.12f, 0.16f, 0.36f), 0.04f);
                b.AddBox(new Vector3(x, 1.8f, -0.015f), new Vector3(w - 0.04f, 0.12f, 0.03f), new Color(0.94f, 0.92f, 0.88f), 0.02f);
            }
            return b.ToMesh("Noren");
        }

        /// <summary>The restaurant's sign over the door: a red board with a white fish on it, a paper lantern hanging each side.</summary>
        public static Mesh SushiSign(System.Random rng, float width)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-width * 0.5f, 2.35f, -0.06f), new Vector3(width, 0.5f, 0.12f), new Color(0.72f, 0.1f, 0.1f), 0.03f);
            // The fish: a white body, a tail.
            b.AddBlob(new Vector3(0f, 2.6f, 0.08f), new Vector3(0.32f, 0.11f, 0.03f), 0, 0.05f, _ => new Color(0.96f, 0.95f, 0.9f));
            b.AddBox(new Vector3(0.3f, 2.5f, 0.06f), new Vector3(0.1f, 0.2f, 0.03f), new Color(0.96f, 0.95f, 0.9f), 0.02f);
            foreach (float x in new[] { -width * 0.5f - 0.25f, width * 0.5f + 0.25f })
            {
                b.AddFrustum(new Vector3(x, 2.25f, 0.2f), new Vector3(x, 2.65f, 0.2f), 0.13f, 0.13f, 8, new Color(0.95f, 0.36f, 0.22f), 0.04f);
                b.AddFrustum(new Vector3(x, 2.65f, 0.2f), new Vector3(x, 2.7f, 0.2f), 0.06f, 0.06f, 6, Furn.WoodDark, 0.03f);
            }
            return b.ToMesh("Sushi Sign");
        }

        /// <summary>
        /// A big, comfy bed: a padded headboard, a thick mattress under deep red satin sheets, a pile of pillows and a fur
        /// throw. 1.8 x 2.1, head to -Z.
        /// </summary>
        public static Mesh ComfyBed(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            var frame = new Color(0.16f, 0.1f, 0.1f);
            b.AddBox(new Vector3(-0.9f, 0f, -1.05f), new Vector3(1.8f, 0.35f, 2.1f), frame, 0.04f);
            b.AddBox(new Vector3(-0.95f, 0f, -1.12f), new Vector3(1.9f, 1.25f, 0.12f), new Color(0.32f, 0.08f, 0.14f), 0.05f);
            b.AddBox(new Vector3(-0.86f, 0.35f, -1.0f), new Vector3(1.72f, 0.26f, 2.0f), new Color(0.92f, 0.9f, 0.86f), 0.03f);
            b.AddBlob(new Vector3(0f, 0.66f, 0.2f), new Vector3(0.88f, 0.07f, 0.85f), 0, 0.25f, _ => b.Jitter(new Color(0.62f, 0.06f, 0.12f), 0.08f), true);
            foreach (float x in new[] { -0.45f, 0.45f })
                b.AddBlob(new Vector3(x, 0.72f, -0.78f), new Vector3(0.36f, 0.11f, 0.2f), 0, 0.12f, _ => b.Jitter(new Color(0.95f, 0.93f, 0.9f), 0.05f), true);
            b.AddBlob(new Vector3(0f, 0.78f, -0.6f), new Vector3(0.3f, 0.1f, 0.16f), 0, 0.12f, _ => b.Jitter(new Color(0.7f, 0.08f, 0.16f), 0.06f), true);
            b.AddBlob(new Vector3(0.2f, 0.72f, 0.75f), new Vector3(0.7f, 0.06f, 0.3f), 0, 0.3f, _ => b.Jitter(new Color(0.85f, 0.82f, 0.78f), 0.1f), true);
            return b.ToMesh("Comfy Bed");
        }

        /// <summary>A bottle of baby oil: a squat translucent pink bottle, a white label and a flip cap.</summary>
        public static Mesh BabyOil(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.17f, 0f), 0.045f, 0.04f, 8, new Color(0.98f, 0.72f, 0.8f), 0.04f);
            b.AddFrustum(new Vector3(0f, 0.17f, 0f), new Vector3(0f, 0.2f, 0f), 0.04f, 0.022f, 8, new Color(0.98f, 0.72f, 0.8f), 0.04f);
            b.AddFrustum(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.24f, 0f), 0.022f, 0.02f, 6, new Color(0.95f, 0.95f, 0.98f), 0.02f);
            b.AddBox(new Vector3(-0.044f, 0.05f, 0.03f), new Vector3(0.088f, 0.08f, 0.02f), new Color(0.96f, 0.96f, 0.98f), 0.02f);
            return b.ToMesh("Baby Oil");
        }

        /// <summary>Chains and manacles hanging on a wall (against -Z): two lengths of dark links from an iron ring.</summary>
        public static Mesh WallChains(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            var iron = new Color(0.17f, 0.17f, 0.18f);
            b.AddBox(new Vector3(-0.35f, 1.7f, -0.05f), new Vector3(0.7f, 0.08f, 0.05f), iron, 0.03f);
            foreach (float x in new[] { -0.28f, 0.28f })
            {
                for (int i = 0; i < 7; i++)
                    b.AddBox(new Vector3(x - 0.02f, 1.65f - i * 0.1f, -0.03f + (i % 2) * 0.01f), new Vector3(0.04f, 0.08f, 0.025f), iron * (i % 2 == 0 ? 1f : 1.3f), 0.04f);
                b.AddFrustum(new Vector3(x, 0.93f, -0.02f), new Vector3(x, 0.99f, -0.02f), 0.06f, 0.06f, 8, new Color(0.25f, 0.24f, 0.24f), 0.03f);
            }
            return b.ToMesh("Wall Chains");
        }
    }
}
