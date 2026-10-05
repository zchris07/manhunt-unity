using UnityEngine;

namespace Vision.World
{
    /// <summary>Furniture and fittings for cabins and the central building. Origin at the bottom centre, front facing +Z.</summary>
    public static partial class LowPolyModels
    {
        public static class Furn
        {
            public static readonly Color Wood = new Color(0.36f, 0.26f, 0.18f);
            public static readonly Color WoodDark = new Color(0.22f, 0.16f, 0.11f);
            public static readonly Color Fabric = new Color(0.38f, 0.36f, 0.32f);
            public static readonly Color Stained = new Color(0.46f, 0.42f, 0.33f);
            public static readonly Color Metal = new Color(0.36f, 0.38f, 0.39f);
            public static readonly Color MetalDark = new Color(0.20f, 0.21f, 0.22f);
            public static readonly Color Green = new Color(0.24f, 0.32f, 0.26f);
            public static readonly Color Lamp = new Color(1.0f, 0.84f, 0.52f);
        }

        /// <summary>A tall wooden wardrobe with two doors (one ajar), 1.0 x 0.55 x 1.9.</summary>
        public static Mesh Wardrobe(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-0.5f, 0.06f, -0.275f), new Vector3(1.0f, 1.84f, 0.55f), Furn.Wood, 0.06f);
            b.AddBox(new Vector3(-0.52f, 1.88f, -0.29f), new Vector3(1.04f, 0.07f, 0.58f), Furn.WoodDark, 0.05f);
            foreach (float x in new[] { -0.45f, 0.4f }) b.AddBox(new Vector3(x, 0f, -0.25f), new Vector3(0.05f, 0.08f, 0.05f), Furn.WoodDark, 0.05f);
            b.AddBox(new Vector3(-0.48f, 0.12f, 0.275f), new Vector3(0.47f, 1.72f, 0.025f), Furn.WoodDark * 1.15f, 0.05f);
            AddBoxAt(b, new Vector3(0.24f, 0.98f, 0.37f), new Vector3(0.47f, 1.72f, 0.025f), Quaternion.Euler(0f, -24f, 0f), Furn.WoodDark * 1.15f);
            b.AddBox(new Vector3(-0.06f, 0.95f, 0.3f), new Vector3(0.03f, 0.12f, 0.03f), Furn.MetalDark, 0.05f);
            return b.ToMesh("Wardrobe");
        }

        /// <summary>A single iron-framed bed with a stained mattress and a rumpled blanket, 1.0 x 2.0, head to -Z.</summary>
        public static Mesh Bed(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            foreach (float x in new[] { -0.47f, 0.47f })
                foreach (float z in new[] { -0.97f, 0.97f })
                    b.AddFrustum(new Vector3(x, 0f, z), new Vector3(x, z < 0f ? 0.95f : 0.6f, z), 0.03f, 0.03f, 4, Furn.MetalDark, 0.05f);
            b.AddBox(new Vector3(-0.5f, 0.32f, -1f), new Vector3(1f, 0.06f, 2f), Furn.MetalDark, 0.05f);
            b.AddBox(new Vector3(-0.46f, 0.38f, -0.96f), new Vector3(0.92f, 0.16f, 1.92f), Furn.Stained, 0.08f);
            b.AddBlob(new Vector3(0.05f, 0.55f, 0.25f), new Vector3(0.45f, 0.06f, 0.65f), 0, 0.3f, _ => b.Jitter(Furn.Green, 0.12f), true);
            b.AddBox(new Vector3(-0.3f, 0.54f, -0.9f), new Vector3(0.6f, 0.1f, 0.35f), Furn.Fabric * 1.3f, 0.08f);
            foreach (float z in new[] { -0.97f, 0.97f })
                b.AddFrustum(new Vector3(-0.47f, z < 0f ? 0.9f : 0.55f, z), new Vector3(0.47f, z < 0f ? 0.9f : 0.55f, z), 0.025f, 0.025f, 4, Furn.MetalDark, 0.05f);
            return b.ToMesh("Bed");
        }

        /// <summary>A rough table, <paramref name="w"/> x <paramref name="d"/>, 0.75 high.</summary>
        public static Mesh Table(System.Random rng, float w = 1.2f, float d = 0.75f)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-w * 0.5f, 0.7f, -d * 0.5f), new Vector3(w, 0.05f, d), Furn.Wood, 0.08f);
            foreach (float x in new[] { -w * 0.5f + 0.05f, w * 0.5f - 0.1f })
                foreach (float z in new[] { -d * 0.5f + 0.05f, d * 0.5f - 0.1f })
                    b.AddBox(new Vector3(x, 0f, z), new Vector3(0.05f, 0.7f, 0.05f), Furn.WoodDark, 0.06f);
            return b.ToMesh("Table");
        }

        /// <summary>A wooden chair, seat facing +Z; <paramref name="toppled"/> lies on its back.</summary>
        public static Mesh Chair(System.Random rng, bool toppled = false)
        {
            var b = new LowPolyMeshBuilder(rng);
            if (toppled) b.Transform = Matrix4x4.TRS(new Vector3(0f, 0.22f, -0.1f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
            b.AddBox(new Vector3(-0.22f, 0.42f, -0.22f), new Vector3(0.44f, 0.04f, 0.44f), Furn.Wood, 0.08f);
            foreach (float x in new[] { -0.2f, 0.17f })
                foreach (float z in new[] { -0.2f, 0.17f })
                    b.AddBox(new Vector3(x, 0f, z), new Vector3(0.03f, 0.42f, 0.03f), Furn.WoodDark, 0.06f);
            b.AddBox(new Vector3(-0.22f, 0.46f, -0.24f), new Vector3(0.44f, 0.42f, 0.03f), Furn.Wood, 0.08f);
            b.Transform = null;
            return b.ToMesh("Chair");
        }

        /// <summary>A bare bulb hanging from a cord (origin at the ceiling hook, 2.4 up), the glow material's lamp.</summary>
        public static Mesh HangingBulb(System.Random rng, float drop = 0.6f)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddFrustum(Vector3.zero, Vector3.down * drop, 0.01f, 0.01f, 3, Furn.MetalDark, 0.05f);
            b.AddFrustum(Vector3.down * drop, Vector3.down * (drop + 0.05f), 0.025f, 0.025f, 5, Furn.MetalDark, 0.05f);
            b.AddBlob(Vector3.down * (drop + 0.11f), Vector3.one * 0.06f, 0, 0.1f, _ => Furn.Lamp);
            return b.ToMesh("Bulb");
        }

        /// <summary>A pane of dirty glass for a window, along +X from 0 to <paramref name="width"/>, sill at 0.9.</summary>
        public static Mesh WindowPane(float width, float sill = 0.9f, float top = 2.05f)
        {
            var b = new LowPolyMeshBuilder();
            var glass = new Color(0.20f, 0.24f, 0.26f);
            var a = new Vector3(0f, sill, 0f);
            var c = new Vector3(width, top, 0f);
            b.AddDoubleSided(a, new Vector3(0f, top, 0f), c, glass);
            b.AddDoubleSided(a, c, new Vector3(width, sill, 0f), glass * 0.95f);
            foreach (float x in new[] { 0f, width * 0.5f, width })
                b.AddBox(new Vector3(x - 0.03f, sill, -0.04f), new Vector3(0.06f, top - sill, 0.08f), Furn.WoodDark, 0.05f);
            b.AddBox(new Vector3(0f, (sill + top) * 0.5f - 0.03f, -0.04f), new Vector3(width, 0.06f, 0.08f), Furn.WoodDark, 0.05f);
            return b.ToMesh("Window");
        }
    }
}
