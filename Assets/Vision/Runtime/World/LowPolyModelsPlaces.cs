using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Miga Sushi and the Diddy Chamber: their walls; the restaurant's timber-and-plaster walls, sushi bar and belt, sake
    /// shelf, menu plaques, tatami with low tables and cushions, shoji screens, paper lanterns, a beckoning cat, a bonsai
    /// and a hanging scroll, the noren curtain and sign; a big comfy bed, wall chains and bottles of baby oil. Origin at the
    /// bottom centre, front facing +Z, as the furniture.
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
        // ------------------------------------------------------------------ Miga Sushi, in detail

        static readonly Color Hinoki = new Color(0.80f, 0.68f, 0.50f), DarkTimber = new Color(0.20f, 0.13f, 0.09f), Plaster = new Color(0.90f, 0.87f, 0.79f);
        static readonly Color Vermilion = new Color(0.78f, 0.16f, 0.10f), Indigo = new Color(0.12f, 0.16f, 0.36f), Rush = new Color(0.66f, 0.66f, 0.42f);

        /// <summary>
        /// A Japanese wall, <paramref name="length"/> long, centred on its origin: pale plaster between dark timber posts
        /// about every 1.8 m, a dark sill beam at the foot, a lintel beam (nageshi) across at 2.1 m, a cap along the top;
        /// the same on both faces.
        /// </summary>
        public static Mesh JapaneseWall(System.Random rng, float length, float height, float thickness)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = length * 0.5f, t = thickness * 0.5f;
            b.AddBox(new Vector3(-h, 0f, -t), new Vector3(length, height, thickness), b.Jitter(Plaster, 0.02f), 0.03f);
            b.AddBox(new Vector3(-h, 0f, -t - 0.012f), new Vector3(length, 0.24f, thickness + 0.024f), DarkTimber, 0.03f);
            b.AddBox(new Vector3(-h, 2.05f, -t - 0.016f), new Vector3(length, 0.12f, thickness + 0.032f), DarkTimber, 0.03f);
            b.AddBox(new Vector3(-h, height - 0.1f, -t - 0.012f), new Vector3(length, 0.1f, thickness + 0.024f), DarkTimber * 0.9f, 0.03f);
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / 1.8f));
            for (int i = 0; i <= posts; i++)
            {
                float x = -h + length * i / posts;
                x = Mathf.Clamp(x, -h + 0.06f, h - 0.06f);
                b.AddBox(new Vector3(x - 0.06f, 0f, -t - 0.02f), new Vector3(0.12f, height, thickness + 0.04f), DarkTimber, 0.04f);
            }
            return b.ToMesh("Wall");
        }

        /// <summary>
        /// The sushi bar, <paramref name="length"/> long (front toward +Z): a hinoki counter with a dark top and a raised serving
        /// ledge, the glass neta case of fish, a conveyor belt running round the ledge with coloured plates of sushi on it,
        /// and at each seat soy sauce, a dish of pickled ginger and a teacup.
        /// </summary>
        public static Mesh SushiBar(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = length * 0.5f;
            // The counter and its top, a darker kick at the foot, vertical slats down the front.
            b.AddBox(new Vector3(-h, 0f, -0.4f), new Vector3(length, 1.0f, 0.8f), Hinoki, 0.03f);
            b.AddBox(new Vector3(-h - 0.03f, 1.0f, -0.44f), new Vector3(length + 0.06f, 0.06f, 0.88f), DarkTimber, 0.03f);
            b.AddBox(new Vector3(-h + 0.02f, 0f, 0.39f), new Vector3(length - 0.04f, 0.12f, 0.03f), DarkTimber, 0.03f);
            for (float x = -h + 0.2f; x < h - 0.1f; x += 0.3f)
                b.AddBox(new Vector3(x - 0.02f, 0.14f, 0.39f), new Vector3(0.04f, 0.8f, 0.02f), Hinoki * 0.85f, 0.04f);
            // The raised ledge at the back of the top, where the belt runs; the glass neta case on it.
            b.AddBox(new Vector3(-h + 0.1f, 1.06f, -0.38f), new Vector3(length - 0.2f, 0.14f, 0.34f), Hinoki * 0.92f, 0.03f);
            b.AddBox(new Vector3(-h + 0.12f, 1.2f, -0.36f), new Vector3(length - 0.24f, 0.012f, 0.3f), new Color(0.28f, 0.3f, 0.32f), 0.02f);
            b.AddBox(new Vector3(-h * 0.85f, 1.21f, -0.42f), new Vector3(length * 0.85f, 0.26f, 0.08f), new Color(0.72f, 0.86f, 0.9f), 0.02f);
            Color[] fish = { new Color(0.98f, 0.52f, 0.36f), new Color(0.78f, 0.12f, 0.16f), new Color(0.98f, 0.86f, 0.32f), new Color(0.96f, 0.96f, 0.92f), new Color(0.9f, 0.4f, 0.5f) };
            Color[] plates = { new Color(0.85f, 0.2f, 0.18f), new Color(0.18f, 0.32f, 0.72f), new Color(0.92f, 0.78f, 0.2f), new Color(0.15f, 0.15f, 0.16f), new Color(0.92f, 0.92f, 0.9f) };
            int k = 0;
            for (float x = -h + 0.3f; x < h - 0.25f; x += 0.36f, k++)
            {
                if (b.Next() < 0.2f) continue;
                // A plate on the belt with two pieces of nigiri.
                b.AddFrustum(new Vector3(x, 1.212f, -0.21f), new Vector3(x, 1.232f, -0.21f), 0.1f, 0.12f, 8, plates[k % plates.Length], 0.02f);
                for (int j = 0; j < 2; j++)
                {
                    float px = x - 0.035f + j * 0.07f;
                    b.AddBox(new Vector3(px - 0.025f, 1.232f, -0.25f), new Vector3(0.05f, 0.035f, 0.08f), new Color(0.96f, 0.96f, 0.93f), 0.02f);
                    b.AddBox(new Vector3(px - 0.028f, 1.262f, -0.255f), new Vector3(0.056f, 0.014f, 0.09f), fish[(k + j) % fish.Length], 0.03f);
                }
            }
            // At each seat: a soy sauce dispenser, a dish of ginger, a teacup.
            for (float x = -h + 0.45f; x < h - 0.3f; x += 0.9f)
            {
                b.AddFrustum(new Vector3(x - 0.2f, 1.06f, 0.1f), new Vector3(x - 0.2f, 1.16f, 0.1f), 0.025f, 0.02f, 6, new Color(0.1f, 0.06f, 0.04f), 0.02f);
                b.AddFrustum(new Vector3(x - 0.2f, 1.16f, 0.1f), new Vector3(x - 0.2f, 1.18f, 0.1f), 0.02f, 0.012f, 6, Vermilion, 0.02f);
                b.AddFrustum(new Vector3(x, 1.06f, 0.18f), new Vector3(x, 1.075f, 0.18f), 0.05f, 0.055f, 8, new Color(0.92f, 0.9f, 0.86f), 0.02f);
                b.AddBlob(new Vector3(x, 1.075f, 0.18f), new Vector3(0.03f, 0.012f, 0.03f), 0, 0.2f, _ => new Color(0.98f, 0.76f, 0.72f), true);
                b.AddFrustum(new Vector3(x + 0.18f, 1.06f, 0.12f), new Vector3(x + 0.18f, 1.13f, 0.12f), 0.03f, 0.036f, 8, new Color(0.44f, 0.52f, 0.42f), 0.03f);
            }
            return b.ToMesh("Sushi Counter");
        }

        /// <summary>
        /// The chef's back shelf (against -Z, front toward +Z), <paramref name="length"/> long: a dark cabinet of drawers below,
        /// two open shelves of sake bottles, tokkuri and stacked bowls above, a rice cooker and a cutting board on top.
        /// </summary>
        public static Mesh SakeShelf(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = length * 0.5f;
            b.AddBox(new Vector3(-h, 0f, -0.25f), new Vector3(length, 0.9f, 0.5f), DarkTimber, 0.03f);
            for (float x = -h + 0.3f; x < h - 0.2f; x += 0.6f)
                b.AddBox(new Vector3(x - 0.24f, 0.55f, 0.25f), new Vector3(0.48f, 0.22f, 0.015f), DarkTimber * 1.25f, 0.03f);
            b.AddBox(new Vector3(-h, 0.9f, -0.25f), new Vector3(length, 0.04f, 0.5f), Hinoki, 0.03f);
            // The open shelves on the wall behind.
            foreach (float y in new[] { 1.45f, 1.85f })
                b.AddBox(new Vector3(-h, y, -0.25f), new Vector3(length, 0.035f, 0.22f), Hinoki * 0.9f, 0.03f);
            Color[] glass = { new Color(0.16f, 0.4f, 0.22f), new Color(0.1f, 0.18f, 0.36f), new Color(0.86f, 0.86f, 0.8f), new Color(0.42f, 0.24f, 0.1f) };
            foreach (float y in new[] { 1.485f, 1.885f })
                for (float x = -h + 0.15f; x < h - 0.1f; x += 0.17f)
                {
                    if (b.Next() < 0.25f) continue;
                    Color c = glass[(int)(b.Next() * glass.Length) % glass.Length];
                    float tall = b.Range(0.22f, 0.32f);
                    b.AddFrustum(new Vector3(x, y, -0.15f), new Vector3(x, y + tall * 0.7f, -0.15f), 0.04f, 0.04f, 6, c, 0.04f);
                    b.AddFrustum(new Vector3(x, y + tall * 0.7f, -0.15f), new Vector3(x, y + tall, -0.15f), 0.04f, 0.014f, 6, c, 0.04f);
                    b.AddBox(new Vector3(x - 0.03f, y + tall * 0.25f, -0.11f), new Vector3(0.06f, tall * 0.25f, 0.005f), new Color(0.95f, 0.93f, 0.86f), 0.03f);
                }
            // On the cabinet: a rice cooker, a cutting board with a knife, stacked bowls.
            b.AddFrustum(new Vector3(-h + 0.45f, 0.94f, 0f), new Vector3(-h + 0.45f, 1.2f, 0f), 0.17f, 0.16f, 10, new Color(0.88f, 0.88f, 0.86f), 0.02f);
            b.AddFrustum(new Vector3(-h + 0.45f, 1.2f, 0f), new Vector3(-h + 0.45f, 1.26f, 0f), 0.16f, 0.1f, 10, new Color(0.7f, 0.7f, 0.72f), 0.02f);
            b.AddBox(new Vector3(-0.3f, 0.94f, -0.12f), new Vector3(0.6f, 0.03f, 0.3f), Hinoki * 1.05f, 0.02f);
            b.AddBox(new Vector3(-0.15f, 0.97f, -0.02f), new Vector3(0.28f, 0.006f, 0.03f), new Color(0.82f, 0.84f, 0.86f), 0.02f);
            for (int i = 0; i < 4; i++)
                b.AddFrustum(new Vector3(h - 0.45f, 0.94f + i * 0.05f, 0f), new Vector3(h - 0.45f, 0.99f + i * 0.05f, 0f), 0.07f, 0.1f, 8, i % 2 == 0 ? Vermilion : DarkTimber, 0.03f);
            return b.ToMesh("Sake Shelf");
        }

        /// <summary>Wooden menu plaques hung in a row (against -Z), <paramref name="length"/> long: tall pale boards, dark brushstrokes, a red seal on some.</summary>
        public static Mesh MenuPlaques(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = length * 0.5f;
            b.AddBox(new Vector3(-h, 2.6f, -0.02f), new Vector3(length, 0.04f, 0.03f), DarkTimber, 0.03f);
            for (float x = -h + 0.12f; x < h - 0.1f; x += 0.2f)
            {
                b.AddBox(new Vector3(x - 0.07f, 2.1f, 0f), new Vector3(0.14f, 0.48f, 0.02f), Hinoki * 1.08f, 0.04f);
                for (int i = 0; i < 4; i++)
                    if (b.Next() < 0.85f) b.AddBox(new Vector3(x - 0.035f, 2.48f - i * 0.085f, 0.021f), new Vector3(0.07f, 0.05f, 0.005f), new Color(0.08f, 0.06f, 0.05f), 0.03f);
                if (b.Next() < 0.4f) b.AddBox(new Vector3(x - 0.025f, 2.14f, 0.021f), new Vector3(0.05f, 0.05f, 0.005f), Vermilion, 0.03f);
            }
            return b.ToMesh("Menu Plaques");
        }

        /// <summary>A tatami floor <paramref name="w"/> by <paramref name="d"/> (from its corner): rush mats with dark cloth borders, laid flush.</summary>
        public static Mesh Tatami(System.Random rng, float w, float d)
        {
            var b = new LowPolyMeshBuilder(rng);
            const float matW = 0.9f, matL = 1.8f;
            for (float x = 0f; x < w - 0.05f; x += matW)
                for (float z = 0f; z < d - 0.05f; z += matL)
                {
                    float mw = Mathf.Min(matW, w - x), ml = Mathf.Min(matL, d - z);
                    b.AddBox(new Vector3(x, 0f, z), new Vector3(mw, 0.03f, ml), b.Jitter(Rush, 0.04f), 0.03f);
                    b.AddBox(new Vector3(x, 0.03f, z), new Vector3(0.05f, 0.005f, ml), new Color(0.12f, 0.12f, 0.1f), 0.02f);
                    b.AddBox(new Vector3(x + mw - 0.05f, 0.03f, z), new Vector3(0.05f, 0.005f, ml), new Color(0.12f, 0.12f, 0.1f), 0.02f);
                }
            return b.ToMesh("Tatami");
        }

        /// <summary>A low table (chabudai) with four floor cushions (zabuton) round it, a teapot and cups, plates of sushi.</summary>
        public static Mesh LowTableSet(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-0.6f, 0.3f, -0.4f), new Vector3(1.2f, 0.05f, 0.8f), DarkTimber * 1.3f, 0.03f);
            foreach (float x in new[] { -0.52f, 0.47f })
                foreach (float z in new[] { -0.32f, 0.27f })
                    b.AddBox(new Vector3(x, 0f, z), new Vector3(0.05f, 0.3f, 0.05f), DarkTimber, 0.03f);
            foreach (var (x, z) in new[] { (0f, -0.75f), (0f, 0.75f), (-0.95f, 0f), (0.95f, 0f) })
            {
                Color cushion = b.Next() < 0.5f ? Indigo : Vermilion * 0.85f;
                b.AddBox(new Vector3(x - 0.27f, 0f, z - 0.27f), new Vector3(0.54f, 0.07f, 0.54f), cushion, 0.03f);
                b.AddBox(new Vector3(x - 0.04f, 0.07f, z - 0.04f), new Vector3(0.08f, 0.01f, 0.08f), cushion * 0.7f, 0.02f);
            }
            b.AddBlob(new Vector3(0.25f, 0.4f, 0.05f), new Vector3(0.08f, 0.06f, 0.08f), 0, 0.05f, _ => new Color(0.24f, 0.3f, 0.24f));
            b.AddFrustum(new Vector3(0.25f, 0.44f, 0.05f), new Vector3(0.25f, 0.46f, 0.05f), 0.02f, 0.02f, 6, DarkTimber, 0.02f);
            foreach (float x in new[] { -0.2f, 0f })
                b.AddFrustum(new Vector3(x, 0.35f, 0.2f), new Vector3(x, 0.4f, 0.2f), 0.025f, 0.03f, 6, new Color(0.84f, 0.82f, 0.76f), 0.03f);
            b.AddBox(new Vector3(-0.35f, 0.35f, -0.22f), new Vector3(0.36f, 0.025f, 0.2f), DarkTimber * 0.8f, 0.02f);
            for (int i = 0; i < 4; i++)
            {
                b.AddBox(new Vector3(-0.32f + i * 0.08f, 0.375f, -0.17f), new Vector3(0.05f, 0.03f, 0.08f), new Color(0.96f, 0.96f, 0.93f), 0.02f);
                b.AddBox(new Vector3(-0.323f + i * 0.08f, 0.405f, -0.175f), new Vector3(0.056f, 0.012f, 0.09f), i % 2 == 0 ? new Color(0.98f, 0.52f, 0.36f) : new Color(0.78f, 0.12f, 0.16f), 0.03f);
            }
            return b.ToMesh("Low Table");
        }

        /// <summary>A shoji screen <paramref name="length"/> long and <paramref name="height"/> high: a dark frame, a lattice of thin bars over white paper.</summary>
        public static Mesh Shoji(System.Random rng, float length, float height)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = length * 0.5f;
            b.AddBox(new Vector3(-h, 0.05f, -0.015f), new Vector3(length, height - 0.1f, 0.03f), new Color(0.95f, 0.93f, 0.86f), 0.02f);
            b.AddBox(new Vector3(-h, 0f, -0.03f), new Vector3(length, 0.06f, 0.06f), DarkTimber, 0.03f);
            b.AddBox(new Vector3(-h, height - 0.06f, -0.03f), new Vector3(length, 0.06f, 0.06f), DarkTimber, 0.03f);
            for (float x = -h; x <= h + 0.01f; x += Mathf.Max(0.3f, length / Mathf.Max(1, Mathf.RoundToInt(length / 0.3f))))
                b.AddBox(new Vector3(Mathf.Min(x, h - 0.02f) - 0.012f, 0.05f, -0.022f), new Vector3(0.024f, height - 0.1f, 0.044f), DarkTimber * 1.2f, 0.03f);
            for (float y = 0.35f; y < height - 0.1f; y += 0.3f)
                b.AddBox(new Vector3(-h, y - 0.01f, -0.022f), new Vector3(length, 0.02f, 0.044f), DarkTimber * 1.2f, 0.03f);
            return b.ToMesh("Shoji");
        }

        /// <summary>A paper lantern (chochin) hanging on its cord from <paramref name="top"/> m: red, ribbed, black caps top and bottom.</summary>
        public static Mesh HangingLantern(System.Random rng, float top, Color paper)
        {
            var b = new LowPolyMeshBuilder(rng);
            float y = top - 0.45f;
            b.AddFrustum(new Vector3(0f, y + 0.3f, 0f), new Vector3(0f, top, 0f), 0.006f, 0.006f, 4, new Color(0.08f, 0.08f, 0.08f), 0.02f);
            b.AddTube(new[] { new Vector3(0f, y - 0.2f, 0f), new Vector3(0f, y - 0.1f, 0f), new Vector3(0f, y + 0.05f, 0f), new Vector3(0f, y + 0.2f, 0f), new Vector3(0f, y + 0.26f, 0f) },
                new[] { 0.09f, 0.15f, 0.17f, 0.14f, 0.09f }, 10, paper, 0.04f);
            b.AddFrustum(new Vector3(0f, y - 0.24f, 0f), new Vector3(0f, y - 0.19f, 0f), 0.08f, 0.08f, 8, new Color(0.08f, 0.08f, 0.08f), 0.02f);
            b.AddFrustum(new Vector3(0f, y + 0.25f, 0f), new Vector3(0f, y + 0.3f, 0f), 0.08f, 0.08f, 8, new Color(0.08f, 0.08f, 0.08f), 0.02f);
            // A black character brushed on its face.
            b.AddBox(new Vector3(-0.04f, y - 0.06f, 0.155f), new Vector3(0.08f, 0.14f, 0.01f), new Color(0.08f, 0.06f, 0.05f), 0.03f);
            return b.ToMesh("Paper Lantern");
        }

        /// <summary>A beckoning cat (maneki-neko): white, a red collar with a gold bell, one paw raised, a gold coin held.</summary>
        public static Mesh ManekiNeko(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color white = new Color(0.96f, 0.95f, 0.92f), gold = new Color(0.92f, 0.72f, 0.2f);
            b.AddBlob(new Vector3(0f, 0.1f, 0f), new Vector3(0.09f, 0.1f, 0.08f), 1, 0.03f, _ => white, true);
            b.AddBlob(new Vector3(0f, 0.24f, 0.01f), new Vector3(0.085f, 0.075f, 0.075f), 1, 0.03f, _ => white);
            foreach (float s in new[] { -1f, 1f })
                b.AddCone(new Vector3(s * 0.05f, 0.29f, 0f), new Vector3(s * 0.06f, 0.34f, 0f), 0.025f, 4, white, 0.02f);
            b.AddFrustum(new Vector3(0f, 0.165f, 0f), new Vector3(0f, 0.18f, 0f), 0.08f, 0.078f, 10, Vermilion, 0.02f);
            b.AddBlob(new Vector3(0f, 0.16f, 0.08f), new Vector3(0.018f, 0.018f, 0.018f), 0, 0.02f, _ => gold);
            b.AddFrustum(new Vector3(0.08f, 0.2f, 0.02f), new Vector3(0.1f, 0.33f, 0.03f), 0.025f, 0.028f, 6, white, 0.02f);
            b.AddFrustum(new Vector3(-0.03f, 0.08f, 0.08f), new Vector3(-0.03f, 0.08f, 0.09f), 0.04f, 0.04f, 8, gold, 0.03f);
            return b.ToMesh("Maneki Neko");
        }

        /// <summary>A bonsai in a shallow glazed dish: a twisting trunk and three flat pads of foliage.</summary>
        public static Mesh Bonsai(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.06f, 0f), 0.14f, 0.16f, 8, new Color(0.22f, 0.32f, 0.5f), 0.03f);
            b.AddTube(new[] { new Vector3(0f, 0.06f, 0f), new Vector3(0.04f, 0.14f, 0.01f), new Vector3(-0.03f, 0.22f, 0f), new Vector3(0.02f, 0.3f, -0.01f) },
                new[] { 0.025f, 0.02f, 0.016f, 0.01f }, 5, new Color(0.3f, 0.22f, 0.15f), 0.05f);
            foreach (var (x, y, z, r) in new[] { (0.08f, 0.2f, 0.02f, 0.08f), (-0.08f, 0.26f, -0.01f, 0.07f), (0.03f, 0.33f, 0f, 0.09f) })
                b.AddBlob(new Vector3(x, y, z), new Vector3(r, r * 0.4f, r * 0.8f), 0, 0.2f, _ => b.Jitter(new Color(0.22f, 0.4f, 0.2f), 0.1f));
            return b.ToMesh("Bonsai");
        }

        /// <summary>A hanging scroll (kakejiku) against a wall (-Z): a silk mount, a painted carp and a wave, a wooden roller at the foot.</summary>
        public static Mesh WallScroll(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-0.3f, 0.9f, 0f), new Vector3(0.6f, 1.25f, 0.01f), new Color(0.56f, 0.42f, 0.26f), 0.02f);
            b.AddBox(new Vector3(-0.24f, 1.05f, 0.01f), new Vector3(0.48f, 0.95f, 0.006f), new Color(0.93f, 0.9f, 0.82f), 0.02f);
            b.AddBlob(new Vector3(0.02f, 1.5f, 0.02f), new Vector3(0.14f, 0.05f, 0.01f), 0, 0.05f, _ => new Color(0.86f, 0.36f, 0.14f));
            b.AddBox(new Vector3(0.14f, 1.46f, 0.016f), new Vector3(0.06f, 0.09f, 0.006f), new Color(0.86f, 0.36f, 0.14f), 0.03f);
            b.AddBox(new Vector3(-0.2f, 1.18f, 0.016f), new Vector3(0.4f, 0.04f, 0.006f), new Color(0.2f, 0.3f, 0.45f), 0.03f);
            b.AddBox(new Vector3(-0.16f, 1.24f, 0.016f), new Vector3(0.18f, 0.03f, 0.006f), new Color(0.2f, 0.3f, 0.45f), 0.03f);
            b.AddFrustum(new Vector3(-0.34f, 0.9f, 0.01f), new Vector3(0.34f, 0.9f, 0.01f), 0.02f, 0.02f, 6, DarkTimber, 0.02f);
            b.AddFrustum(new Vector3(-0.3f, 2.15f, 0.005f), new Vector3(0.3f, 2.15f, 0.005f), 0.012f, 0.012f, 6, DarkTimber, 0.02f);
            return b.ToMesh("Wall Scroll");
        }

        /// <summary>A wooden bar stool: a round seat on four splayed legs with a footrest ring.</summary>
        public static Mesh WoodStool(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.AddFrustum(d * 0.17f, d * 0.12f + Vector3.up * 0.7f, 0.022f, 0.02f, 4, DarkTimber, 0.03f);
            }
            b.AddFrustum(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.32f, 0f), 0.16f, 0.16f, 8, DarkTimber * 1.2f, 0.03f);
            b.AddFrustum(new Vector3(0f, 0.7f, 0f), new Vector3(0f, 0.76f, 0f), 0.19f, 0.19f, 10, Hinoki, 0.03f);
            return b.ToMesh("Stool");
        }
    }
}
