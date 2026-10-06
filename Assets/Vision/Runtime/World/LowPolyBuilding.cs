using UnityEngine;
using BF = Vision.World.BuildingPlan.Furn;

namespace Vision.World
{
    /// <summary>
    /// Models for the central building: its walls, gate and yard fence, and everything in the rooms. Origin at the bottom
    /// centre with the front facing +Z (wall fittings: the back on z = 0); runs of pipe and duct go along +Z.
    /// </summary>
    public static partial class LowPolyModels
    {
        public static class Bldg
        {
            public static readonly Color Paint = new Color(0.47f, 0.47f, 0.42f);
            public static readonly Color PaintGreen = new Color(0.33f, 0.39f, 0.33f);
            public static readonly Color PaintBlue = new Color(0.33f, 0.37f, 0.42f);
            public static readonly Color PaintCream = new Color(0.52f, 0.49f, 0.40f);
            public static readonly Color Grime = new Color(0.22f, 0.21f, 0.18f);
            public static readonly Color Block = new Color(0.40f, 0.39f, 0.37f);
            public static readonly Color Concrete = new Color(0.30f, 0.30f, 0.29f);
            public static readonly Color Cap = new Color(0.16f, 0.16f, 0.16f);
            public static readonly Color Steel = new Color(0.32f, 0.34f, 0.35f);
            public static readonly Color SteelDark = new Color(0.17f, 0.18f, 0.19f);
            public static readonly Color Rack = new Color(0.45f, 0.27f, 0.12f);
            public static readonly Color RackBlue = new Color(0.17f, 0.25f, 0.40f);
            public static readonly Color Cardboard = new Color(0.50f, 0.38f, 0.24f);
            public static readonly Color Porcelain = new Color(0.66f, 0.66f, 0.62f);
            public static readonly Color Appliance = new Color(0.62f, 0.60f, 0.54f);
            public static readonly Color Black = new Color(0.07f, 0.07f, 0.08f);
            public static readonly Color Paper = new Color(0.70f, 0.68f, 0.62f);
            public static readonly Color Rust = new Color(0.40f, 0.21f, 0.11f);
            public static readonly Color Red = new Color(0.55f, 0.12f, 0.10f);
            public static readonly Color Galvanised = new Color(0.42f, 0.44f, 0.44f);
            public static readonly Color[] Fabric = { new Color(0.35f, 0.22f, 0.18f), new Color(0.22f, 0.28f, 0.33f), new Color(0.32f, 0.31f, 0.22f) };
            public static readonly Color[] Print = { new Color(0.55f, 0.15f, 0.12f), new Color(0.15f, 0.20f, 0.35f), new Color(0.60f, 0.52f, 0.25f), new Color(0.12f, 0.12f, 0.12f), new Color(0.25f, 0.40f, 0.30f) };
        }

        static Color Pick(System.Random rng, Color[] colors) => colors[rng.Next(colors.Length)];

        // ------------------------------------------------------------------ walls

        /// <summary>A painted interior wall along X (centred), with grime streaks, a dark baseboard and a dark top so it reads from above.</summary>
        public static Mesh InteriorWall(System.Random rng, float length, float height, float thickness, Color paint)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = thickness * 0.5f;
            b.AddBox(new Vector3(-length * 0.5f, 0f, -h), new Vector3(length, height, thickness), paint, 0.04f);
            b.AddBox(new Vector3(-length * 0.5f, 0f, -h - 0.012f), new Vector3(length, 0.12f, thickness + 0.024f), Bldg.Grime, 0.05f);
            b.AddBox(new Vector3(-length * 0.5f, height, -h), new Vector3(length, 0.04f, thickness), Bldg.Cap, 0.04f);
            int streaks = Mathf.RoundToInt(length / 2.5f * b.Range(0.5f, 1.5f));
            for (int i = 0; i < streaks; i++)
            {
                float x = b.Range(-length * 0.5f + 0.2f, length * 0.5f - 0.6f), w = b.Range(0.15f, 0.6f), top = b.Range(0.5f, height - 0.2f);
                float side = b.Next() < 0.5f ? -h - 0.006f : h + 0.001f;
                b.AddBox(new Vector3(x, 0.12f, side), new Vector3(w, top - 0.12f, 0.005f), Color.Lerp(paint, Bldg.Grime, b.Range(0.3f, 0.6f)), 0.05f);
            }
            return b.ToMesh("Interior Wall");
        }

        /// <summary>An exterior wall along X: cinder-block courses on the outside (+Z when <paramref name="outsideSign"/> is 1), paint inside.</summary>
        public static Mesh ExteriorWall(System.Random rng, float length, float height, float thickness, float outsideSign, Color paint)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = thickness * 0.5f, face = 0.04f;
            float core = thickness - face;
            float zCore = outsideSign > 0f ? -h : -h + face;
            b.AddBox(new Vector3(-length * 0.5f, 0f, zCore), new Vector3(length, height, core), paint, 0.04f);
            float zFace = outsideSign > 0f ? h - face : -h;
            const float course = 0.4f, block = 0.8f;
            int courses = Mathf.CeilToInt(height / course);
            for (int c = 0; c < courses; c++)
            {
                float y0 = c * course, y1 = Mathf.Min(height, y0 + course - 0.015f);
                for (float x = -length * 0.5f - (c % 2) * block * 0.5f; x < length * 0.5f; x += block)
                {
                    float x0 = Mathf.Max(x, -length * 0.5f), x1 = Mathf.Min(x + block - 0.015f, length * 0.5f);
                    if (x1 - x0 < 0.05f) continue;
                    Color col = b.Jitter(Color.Lerp(Bldg.Block, Bldg.Grime, c == 0 ? 0.4f : b.Range(0f, 0.2f)), 0.08f);
                    b.AddBox(new Vector3(x0, y0, zFace), new Vector3(x1 - x0, y1 - y0, face), col, 0.02f);
                }
            }
            b.AddBox(new Vector3(-length * 0.5f, height, -h - 0.02f), new Vector3(length, 0.06f, thickness + 0.04f), Bldg.Cap, 0.04f);
            return b.ToMesh("Exterior Wall");
        }

        /// <summary>The wall above a door or window, from <paramref name="from"/> to the wall top.</summary>
        public static Mesh WallHeader(float length, float from, float to, float thickness, Color paint)
        {
            var b = new LowPolyMeshBuilder();
            b.AddBox(new Vector3(-length * 0.5f, from, -thickness * 0.5f), new Vector3(length, to - from, thickness), paint, 0.03f);
            b.AddBox(new Vector3(-length * 0.5f, to, -thickness * 0.5f), new Vector3(length, 0.04f, thickness), Bldg.Cap, 0.03f);
            b.AddBox(new Vector3(-length * 0.5f, from - 0.06f, -thickness * 0.5f - 0.02f), new Vector3(length, 0.06f, thickness + 0.04f), Furn.WoodDark, 0.05f);
            return b.ToMesh("Header");
        }

        /// <summary>Boards nailed across a window from 0 to <paramref name="width"/> along X.</summary>
        public static Mesh Boards(System.Random rng, float width)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < 4; i++)
            {
                float y = 1.0f + i * 0.27f + b.Range(-0.05f, 0.05f);
                AddBoxAt(b, new Vector3(width * 0.5f, y, 0.09f), new Vector3(width + 0.25f, 0.16f, 0.03f), Quaternion.Euler(0f, 0f, b.Range(-9f, 9f)), b.Jitter(Palette.Plank, 0.12f));
            }
            return b.ToMesh("Boards");
        }

        /// <summary>A corrugated roll-up door (the exit gate), along X from 0 to <paramref name="width"/>.</summary>
        public static Mesh RollUpDoor(System.Random rng, float width, float height)
        {
            var b = new LowPolyMeshBuilder(rng);
            int slats = Mathf.RoundToInt(height / 0.18f);
            for (int i = 0; i < slats; i++)
            {
                float y = i * height / slats;
                b.AddBox(new Vector3(0f, y, -0.04f), new Vector3(width, height / slats - 0.012f, 0.08f), i % 2 == 0 ? Bldg.Galvanised : Bldg.Galvanised * 0.85f, 0.04f);
            }
            b.AddBox(new Vector3(0f, 0f, -0.06f), new Vector3(width, 0.1f, 0.12f), Bldg.SteelDark, 0.04f);
            for (int i = 0; i < 3; i++)
            {
                float x = b.Range(0.2f, width - 0.6f);
                b.AddBox(new Vector3(x, 0.1f, 0.041f), new Vector3(b.Range(0.08f, 0.3f), b.Range(0.4f, 1.4f), 0.004f), Bldg.Rust, 0.1f);
            }
            return b.ToMesh("Roll-up Door");
        }

        /// <summary>The roll-up door's housing and side rails (fixed), along X from 0 to <paramref name="width"/>.</summary>
        public static Mesh RollUpFrame(float width, float height, float wallTop)
        {
            var b = new LowPolyMeshBuilder();
            foreach (float x in new[] { -0.1f, width })
                b.AddBox(new Vector3(x, 0f, -0.12f), new Vector3(0.1f, wallTop, 0.24f), Bldg.SteelDark, 0.04f);
            b.AddBox(new Vector3(-0.1f, height, -0.2f), new Vector3(width + 0.2f, wallTop - height + 0.05f, 0.4f), Bldg.Steel, 0.04f);
            b.AddBox(new Vector3(width * 0.5f - 0.6f, height + 0.15f, 0.2f), new Vector3(1.2f, 0.25f, 0.01f), new Color(0.6f, 0.5f, 0.12f), 0.04f);
            return b.ToMesh("Gate Frame");
        }

        /// <summary>The gate lever box on the wall (back at z = 0); the handle is <see cref="LeverHandle"/>.</summary>
        public static Mesh LeverBox()
        {
            var b = new LowPolyMeshBuilder();
            b.AddBox(new Vector3(-0.2f, 0.9f, 0f), new Vector3(0.4f, 0.55f, 0.16f), Bldg.Steel, 0.05f);
            b.AddBox(new Vector3(-0.17f, 1.38f, 0.161f), new Vector3(0.34f, 0.05f, 0.005f), new Color(0.6f, 0.5f, 0.12f), 0.04f);
            b.AddFrustum(new Vector3(0.12f, 1.45f, 0.06f), new Vector3(0.12f, 2.6f, 0.06f), 0.025f, 0.025f, 4, Bldg.SteelDark, 0.04f);
            return b.ToMesh("Lever Box");
        }

        /// <summary>The lever's handle: pivot at the origin, pointing up (thrown: rotated down).</summary>
        public static Mesh LeverHandle()
        {
            var b = new LowPolyMeshBuilder();
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.32f, 0f), 0.02f, 0.02f, 4, Bldg.SteelDark, 0.04f);
            b.AddBox(new Vector3(-0.04f, 0.3f, -0.04f), new Vector3(0.08f, 0.1f, 0.08f), Bldg.Red, 0.05f);
            return b.ToMesh("Lever");
        }

        /// <summary>Chain-link fence along X from 0 to <paramref name="length"/>: posts, a top rail and diamond mesh (sight passes).</summary>
        public static Mesh ChainLink(System.Random rng, float length, float height = 2.2f)
        {
            var b = new LowPolyMeshBuilder(rng);
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / 2.5f));
            for (int i = 0; i <= posts; i++)
            {
                float x = length * i / posts;
                b.AddFrustum(new Vector3(x, 0f, 0f), new Vector3(x, height + 0.05f, 0f), 0.035f, 0.035f, 5, Bldg.Galvanised, 0.05f);
            }
            b.AddFrustum(new Vector3(0f, height, 0f), new Vector3(length, height, 0f), 0.025f, 0.025f, 4, Bldg.Galvanised, 0.05f);
            b.AddFrustum(new Vector3(0f, 0.08f, 0f), new Vector3(length, 0.08f, 0f), 0.015f, 0.015f, 3, Bldg.Galvanised, 0.05f);
            Color wire = Bldg.Galvanised * 0.8f;
            const float step = 0.3f, w = 0.012f;
            for (float s = -height; s < length; s += step)
            {
                foreach (float dir in new[] { 1f, -1f })
                {
                    float x0 = dir > 0f ? s : s + height, x1 = dir > 0f ? s + height : s;
                    float y0 = 0.08f, y1 = height;
                    // Clip the diagonal to the fence's length.
                    float t0 = 0f, t1 = 1f;
                    if (x1 != x0)
                    {
                        float ta = (0f - x0) / (x1 - x0), tb = (length - x0) / (x1 - x0);
                        t0 = Mathf.Max(t0, Mathf.Min(ta, tb));
                        t1 = Mathf.Min(t1, Mathf.Max(ta, tb));
                    }
                    if (t1 <= t0) continue;
                    var a = new Vector3(Mathf.Lerp(x0, x1, t0), Mathf.Lerp(y0, y1, t0), 0f);
                    var c = new Vector3(Mathf.Lerp(x0, x1, t1), Mathf.Lerp(y0, y1, t1), 0f);
                    var off = new Vector3(w, 0f, 0f);
                    b.AddDoubleSided(a - off, c - off, c + off, wire);
                    b.AddDoubleSided(a - off, c + off, a + off, wire);
                }
            }
            return b.ToMesh("Chain-link Fence");
        }

        /// <summary>A wooden pallet, <paramref name="length"/> along X, lying flat (stand it up by turning it).</summary>
        public static Mesh Pallet(System.Random rng, float length = 1.2f, float width = 1.0f)
        {
            var b = new LowPolyMeshBuilder(rng);
            foreach (float z in new[] { -width * 0.5f + 0.05f, -0.05f, width * 0.5f - 0.15f })
                b.AddBox(new Vector3(-length * 0.5f, 0f, z), new Vector3(length, 0.09f, 0.1f), Palette.PlankDark, 0.1f);
            int slats = Mathf.Max(4, Mathf.RoundToInt(length / 0.2f));
            for (int i = 0; i < slats; i++)
            {
                float x = -length * 0.5f + (i + 0.5f) * length / slats;
                if (b.Next() < 0.08f) continue;
                b.AddBox(new Vector3(x - 0.05f, 0.09f, -width * 0.5f), new Vector3(0.1f, 0.025f, width), b.Jitter(Palette.Plank * 1.1f, 0.12f), 0.05f);
            }
            return b.ToMesh("Pallet");
        }

        /// <summary>A fluorescent fitting hanging on two chains (origin at the ceiling); dead ones may hang from one chain.</summary>
        public static Mesh FluorescentHousing(System.Random rng, bool hanging)
        {
            var b = new LowPolyMeshBuilder(rng);
            if (hanging) b.Transform = Matrix4x4.TRS(new Vector3(-0.6f, -0.3f, 0f), Quaternion.Euler(0f, 0f, -28f), Vector3.one) * Matrix4x4.Translate(new Vector3(0.6f, 0f, 0f));
            foreach (float x in new[] { -0.5f, 0.5f })
                if (!hanging || x < 0f) b.AddFrustum(new Vector3(x, 0f, 0f), new Vector3(x, -0.3f, 0f), 0.008f, 0.008f, 3, Bldg.SteelDark, 0.04f);
            b.AddBox(new Vector3(-0.62f, -0.36f, -0.13f), new Vector3(1.24f, 0.06f, 0.26f), Bldg.Appliance * 0.8f, 0.05f);
            b.Transform = null;
            return b.ToMesh("Fixture");
        }

        /// <summary>The two tubes under a <see cref="FluorescentHousing"/> (drawn glowing when the lamp works).</summary>
        public static Mesh FluorescentTubes()
        {
            var b = new LowPolyMeshBuilder();
            foreach (float z in new[] { -0.06f, 0.06f })
                b.AddFrustum(new Vector3(-0.58f, -0.4f, z), new Vector3(0.58f, -0.4f, z), 0.025f, 0.025f, 4, Furn.Lamp, 0.02f);
            return b.ToMesh("Tubes");
        }

        /// <summary>A desk lamp (origin on the desk top): a base, an arm and a shade over the bulb. The bulb is <see cref="DeskLampBulb"/>.</summary>
        public static Mesh DeskLamp()
        {
            var b = new LowPolyMeshBuilder();
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.03f, 0f), 0.09f, 0.08f, 6, Bldg.Black, 0.03f);
            b.AddFrustum(new Vector3(0f, 0.03f, 0f), new Vector3(0.04f, 0.32f, 0f), 0.012f, 0.012f, 4, Bldg.SteelDark, 0.03f);
            b.AddFrustum(new Vector3(0.04f, 0.3f, 0f), new Vector3(0.04f, 0.4f, 0f), 0.11f, 0.05f, 6, Bldg.SteelDark, 0.05f);
            return b.ToMesh("Desk Lamp");
        }

        public static Mesh DeskLampBulb()
        {
            var b = new LowPolyMeshBuilder();
            b.AddBlob(new Vector3(0.04f, 0.285f, 0f), Vector3.one * 0.05f, 0, 0.05f, _ => Furn.Lamp);
            return b.ToMesh("Desk Lamp Bulb");
        }

        /// <summary>An exit sign's dark housing over a door (origin on the wall line, front +Z, 2.25 up).</summary>
        public static Mesh ExitSign()
        {
            var b = new LowPolyMeshBuilder();
            b.AddBox(new Vector3(-0.22f, 0f, -0.1f), new Vector3(0.44f, 0.18f, 0.1f), Bldg.Black, 0.03f);
            return b.ToMesh("Exit Sign");
        }

        /// <summary>The green face of an exit sign (drawn glowing when it is on).</summary>
        public static Mesh ExitSignGlow()
        {
            var b = new LowPolyMeshBuilder();
            b.AddBox(new Vector3(-0.19f, 0.03f, -0.005f), new Vector3(0.38f, 0.12f, 0.012f), new Color(0.25f, 0.85f, 0.4f), 0.02f);
            return b.ToMesh("Exit Sign Glow");
        }

        static void Panel(LowPolyMeshBuilder b, Vector3 centreBottom, Vector2 size, Color c) =>
            b.AddBox(new Vector3(centreBottom.x - size.x * 0.5f, centreBottom.y, centreBottom.z), new Vector3(size.x, size.y, 0.015f), c, 0.02f);

        /// <summary>The lit part of a piece of furniture, in the item's own frame (front +Z): a vending machine's window, the
        /// boiler's fire door, a server rack's status lights, a stage light's lens.</summary>
        public static Mesh LampGlow(BuildingPlan.LampKind kind, Vector2 size)
        {
            var b = new LowPolyMeshBuilder();
            float hd = size.y * 0.5f, hw = size.x * 0.5f;
            switch (kind)
            {
                case BuildingPlan.LampKind.Vending:
                    Panel(b, new Vector3(-hw + 0.06f + size.x * 0.325f, 0.5f, hd + 0.005f), new Vector2(size.x * 0.65f, 1.2f), new Color(0.75f, 0.88f, 0.92f));
                    break;
                case BuildingPlan.LampKind.Furnace:
                    Panel(b, new Vector3(-0.2f, 0.3f, 0.585f), new Vector2(0.3f, 0.16f), new Color(1f, 0.55f, 0.15f));
                    break;
                case BuildingPlan.LampKind.Server:
                    for (int i = 0; i < 3; i++)
                        Panel(b, new Vector3(i % 2 == 0 ? -0.1f : 0.1f, 1.0f + i * 0.28f, hd + 0.005f), new Vector2(0.28f, 0.035f), i == 1 ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.3f, 0.85f, 0.45f));
                    break;
                default:
                    Panel(b, new Vector3(0f, 1.65f, 0.2f), new Vector2(0.22f, 0.2f), new Color(1f, 0.92f, 0.7f));
                    break;
            }
            return b.ToMesh("Lamp Glow");
        }

        // ------------------------------------------------------------------ furniture and fittings

        /// <summary>The model for a building item of the given size (model-local width x depth) and variant.</summary>
        public static Mesh BuildingItem(BF kind, System.Random rng, Vector2 size, int variant)
        {
            var b = new LowPolyMeshBuilder(rng);
            float w = size.x, d = size.y, hw = w * 0.5f, hd = d * 0.5f;
            switch (kind)
            {
                case BF.Desk:
                    b.AddBox(new Vector3(-hw, 0.71f, -hd), new Vector3(w, 0.04f, d), Furn.Wood, 0.06f);
                    b.AddBox(new Vector3(-hw, 0f, -hd + 0.02f), new Vector3(0.42f, 0.71f, d - 0.04f), Furn.WoodDark, 0.05f);
                    for (int i = 0; i < 3; i++) b.AddBox(new Vector3(-hw + 0.05f, 0.08f + i * 0.21f, hd - 0.019f), new Vector3(0.32f, 0.17f, 0.005f), Furn.Wood, 0.06f);
                    b.AddBox(new Vector3(hw - 0.04f, 0f, -hd + 0.02f), new Vector3(0.04f, 0.71f, d - 0.04f), Furn.WoodDark, 0.05f);
                    b.AddBox(new Vector3(-hw + 0.42f, 0.25f, -hd + 0.02f), new Vector3(w - 0.46f, 0.46f, 0.02f), Furn.WoodDark, 0.05f);
                    if (b.Next() < 0.5f)
                    {
                        b.AddBox(new Vector3(-0.2f, 0.75f, -hd + 0.06f), new Vector3(0.4f, 0.34f, 0.38f), Bldg.Appliance, 0.05f);
                        b.AddBox(new Vector3(-0.16f, 0.8f, -hd + 0.441f), new Vector3(0.32f, 0.25f, 0.005f), Bldg.Black, 0.02f);
                    }
                    for (int i = 0; i < 3; i++) AddBoxAt(b, new Vector3(b.Range(-hw + 0.3f, hw - 0.3f), 0.755f, b.Range(-hd + 0.15f, hd - 0.15f)), new Vector3(0.21f, 0.005f, 0.3f), Quaternion.Euler(0f, b.Range(0f, 360f), 0f), Bldg.Paper);
                    break;
                case BF.OfficeChair:
                    if (variant == 1) b.Transform = Matrix4x4.TRS(new Vector3(0f, 0.28f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        AddBoxAt(b, new Vector3(Mathf.Sin(a) * 0.15f, 0.04f, Mathf.Cos(a) * 0.15f), new Vector3(0.04f, 0.04f, 0.3f), Quaternion.Euler(0f, i * 72f, 0f), Bldg.Black);
                    }
                    b.AddFrustum(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.44f, 0f), 0.025f, 0.025f, 4, Bldg.SteelDark, 0.04f);
                    b.AddBox(new Vector3(-0.24f, 0.44f, -0.24f), new Vector3(0.48f, 0.08f, 0.48f), Pick(b.Rng, Bldg.Fabric), 0.08f);
                    b.AddBox(new Vector3(-0.22f, 0.55f, -0.26f), new Vector3(0.44f, 0.45f, 0.06f), Pick(b.Rng, Bldg.Fabric), 0.08f);
                    b.Transform = null;
                    break;
                case BF.FilingCabinet:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 1.3f, d), Bldg.Steel, 0.05f);
                    for (int i = 0; i < 4; i++)
                    {
                        bool pulled = variant == 1 && i == 2;
                        b.AddBox(new Vector3(-hw + 0.03f, 0.05f + i * 0.31f, pulled ? hd : hd - 0.005f), new Vector3(w - 0.06f, 0.28f, pulled ? 0.35f : 0.01f), Bldg.Steel * 1.08f, 0.05f);
                        b.AddBox(new Vector3(-0.06f, 0.24f + i * 0.31f, hd + (pulled ? 0.35f : 0f)), new Vector3(0.12f, 0.03f, 0.03f), Bldg.SteelDark, 0.04f);
                    }
                    break;
                case BF.Shelf:
                    foreach (float x in new[] { -hw, hw - 0.03f }) b.AddBox(new Vector3(x, 0f, -hd), new Vector3(0.03f, 1.9f, d), Furn.WoodDark, 0.05f);
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 1.9f, 0.02f), Furn.WoodDark * 0.9f, 0.04f);
                    for (int i = 0; i < 5; i++)
                    {
                        float y = 0.05f + i * 0.45f;
                        b.AddBox(new Vector3(-hw, y, -hd), new Vector3(w, 0.03f, d), Furn.Wood, 0.06f);
                        if (i == 4) break;
                        for (float x = -hw + 0.05f; x < hw - 0.1f; x += b.Range(0.05f, 0.09f))
                        {
                            if (b.Next() < 0.25f) { x += 0.12f; continue; }
                            float bh = b.Range(0.2f, 0.32f);
                            b.AddBox(new Vector3(x, y + 0.03f, -hd + 0.04f), new Vector3(0.045f, bh, d * 0.7f), b.Jitter(Pick(b.Rng, Bldg.Print), 0.2f), 0.05f);
                        }
                    }
                    break;
                case BF.Rack:
                {
                    Color frame = variant == 1 ? Bldg.RackBlue : Bldg.Rack;
                    foreach (float x in new[] { -hw, hw - 0.06f })
                        foreach (float z in new[] { -hd, hd - 0.06f })
                            b.AddBox(new Vector3(x, 0f, z), new Vector3(0.06f, 2.1f, 0.06f), frame, 0.05f);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.12f + i * 0.62f;
                        b.AddBox(new Vector3(-hw, y, -hd), new Vector3(w, 0.05f, d), Bldg.Steel, 0.05f);
                        for (float x = -hw + 0.1f; x < hw - 0.4f; x += b.Range(0.45f, 0.8f))
                        {
                            if (b.Next() < 0.3f) continue;
                            float bw = b.Range(0.3f, 0.5f), bh = b.Range(0.2f, 0.45f);
                            b.AddBox(new Vector3(x, y + 0.05f, -hd + 0.05f), new Vector3(bw, bh, d - 0.1f), b.Next() < 0.6f ? b.Jitter(Bldg.Cardboard, 0.1f) : b.Jitter(Palette.Plank, 0.1f), 0.06f);
                        }
                    }
                    break;
                }
                case BF.Crate:
                    return Crate(rng, Mathf.Min(w, d));
                case BF.CrateStack:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 0.9f, d), Palette.Plank, 0.12f);
                    AddBoxAt(b, new Vector3(b.Range(-0.08f, 0.08f), 0.9f + 0.32f, b.Range(-0.08f, 0.08f)), new Vector3(0.64f, 0.64f, 0.64f), Quaternion.Euler(0f, b.Range(-20f, 20f), 0f), Palette.Plank * 1.1f);
                    AddBoxAt(b, new Vector3(0f, 0.45f, hd + 0.001f), new Vector3(w * 0.6f, 0.2f, 0.005f), Quaternion.identity, new Color(0.6f, 0.55f, 0.4f));
                    break;
                case BF.Boxes:
                {
                    int n = 2 + variant;
                    float y = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float bw = b.Range(0.3f, 0.5f), bd = b.Range(0.25f, 0.4f), bh = b.Range(0.22f, 0.38f);
                        bool stack = i > 0 && b.Next() < 0.4f;
                        Vector3 at = stack ? new Vector3(b.Range(-0.1f, 0.1f), y, b.Range(-0.05f, 0.05f)) : new Vector3(b.Range(-hw + bw * 0.5f, hw - bw * 0.5f), 0f, b.Range(-hd + bd * 0.5f, hd - bd * 0.5f));
                        AddBoxAt(b, at + Vector3.up * bh * 0.5f, new Vector3(bw, bh, bd), Quaternion.Euler(0f, b.Range(-25f, 25f), 0f), b.Jitter(Bldg.Cardboard, 0.12f));
                        y = stack ? y + bh : bh;
                    }
                    break;
                }
                case BF.Barrel:
                {
                    Color body = variant switch { 0 => new Color(0.18f, 0.27f, 0.42f), 1 => Bldg.Rust, _ => new Color(0.22f, 0.32f, 0.22f) };
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0.9f, 0f), 0.3f, 0.3f, 9, body, 0.08f);
                    foreach (float y in new[] { 0.02f, 0.3f, 0.6f, 0.86f }) b.AddFrustum(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.04f, 0f), 0.315f, 0.315f, 9, body * 0.75f, 0.05f);
                    b.AddFrustum(new Vector3(0.12f, 0.9f, 0.1f), new Vector3(0.12f, 0.92f, 0.1f), 0.04f, 0.04f, 5, Bldg.SteelDark, 0.04f);
                    break;
                }
                case BF.Pallet:
                    return Pallet(rng, w, w * 0.85f);
                case BF.PalletStack:
                {
                    int n = 3 + variant;
                    for (int i = 0; i < n; i++)
                    {
                        b.Transform = Matrix4x4.TRS(new Vector3(b.Range(-0.04f, 0.04f), i * 0.12f, b.Range(-0.04f, 0.04f)), Quaternion.Euler(0f, b.Range(-4f, 4f), 0f), Vector3.one);
                        foreach (float z in new[] { -hd + 0.05f, -0.05f, hd - 0.15f }) b.AddBox(new Vector3(-hw, 0f, z), new Vector3(w, 0.09f, 0.1f), Palette.PlankDark, 0.1f);
                        b.AddBox(new Vector3(-hw, 0.09f, -hd), new Vector3(w, 0.025f, d), Palette.Plank * 1.05f, 0.1f);
                    }
                    b.Transform = null;
                    if (variant > 0) b.AddBox(new Vector3(-hw + 0.1f, n * 0.12f, -hd + 0.1f), new Vector3(w - 0.2f, b.Range(0.4f, 0.8f), d - 0.2f), b.Jitter(Bldg.Cardboard, 0.1f), 0.08f);
                    break;
                }
                case BF.Table:
                    return Table(rng, w, d);
                case BF.Chair:
                    return Chair(rng, variant == 1);
                case BF.Sofa:
                {
                    Color fabric = Pick(b.Rng, Bldg.Fabric);
                    b.AddBox(new Vector3(-hw, 0.08f, -hd), new Vector3(w, 0.3f, d), fabric * 0.8f, 0.06f);
                    b.AddBox(new Vector3(-hw, 0.08f, -hd), new Vector3(w, 0.8f, 0.2f), fabric, 0.06f);
                    foreach (float x in new[] { -hw, hw - 0.18f }) b.AddBox(new Vector3(x, 0.08f, -hd), new Vector3(0.18f, 0.6f, d), fabric * 0.9f, 0.06f);
                    int missing = b.Rng.Next(3);
                    for (int i = 0; i < 3; i++)
                        if (i != missing || b.Next() < 0.5f)
                            b.AddBox(new Vector3(-hw + 0.2f + i * (w - 0.4f) / 3f, 0.38f, -hd + 0.2f), new Vector3((w - 0.4f) / 3f - 0.02f, 0.12f, d - 0.22f), b.Jitter(fabric * 1.1f, 0.1f), 0.06f);
                    break;
                }
                case BF.Fridge:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 1.8f, d), Bldg.Appliance, 0.05f);
                    if (variant == 1) AddBoxAt(b, new Vector3(-hw + 0.3f, 1.2f, hd + 0.25f), new Vector3(0.04f, 1.1f, w), Quaternion.Euler(0f, 50f, 0f), Bldg.Appliance * 0.95f);
                    else b.AddBox(new Vector3(-hw + 0.02f, 0.62f, hd - 0.01f), new Vector3(w - 0.04f, 0.015f, 0.02f), Bldg.SteelDark, 0.04f);
                    b.AddBox(new Vector3(hw - 0.08f, 0.8f, hd), new Vector3(0.03f, 0.35f, 0.04f), Bldg.SteelDark, 0.04f);
                    b.AddBox(new Vector3(-hw + 0.1f, 0.2f, hd + 0.001f), new Vector3(0.3f, 0.6f, 0.005f), Bldg.Rust * 0.8f, 0.1f);
                    break;
                case BF.Vending:
                {
                    Color body = variant == 0 ? Bldg.Red : new Color(0.15f, 0.25f, 0.45f);
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 1.9f, d), body, 0.05f);
                    b.AddBox(new Vector3(-hw + 0.06f, 0.5f, hd - 0.01f), new Vector3(w * 0.65f, 1.2f, 0.02f), Rust.Glass, 0.03f);
                    for (int r = 0; r < 5; r++)
                        for (int c = 0; c < 5; c++)
                            if (b.Next() < 0.6f) b.AddBox(new Vector3(-hw + 0.1f + c * 0.12f, 0.58f + r * 0.23f, hd - 0.12f), new Vector3(0.07f, 0.13f, 0.07f), Pick(b.Rng, Bldg.Print), 0.1f);
                    b.AddBox(new Vector3(hw - 0.28f, 1.0f, hd - 0.01f), new Vector3(0.18f, 0.4f, 0.02f), Bldg.SteelDark, 0.04f);
                    break;
                }
                case BF.Counter:
                    b.AddBox(new Vector3(-hw, 0.08f, -hd), new Vector3(w, 0.8f, d - 0.03f), Furn.Wood * 0.9f, 0.05f);
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 0.08f, d - 0.08f), Bldg.Black, 0.04f);
                    b.AddBox(new Vector3(-hw - 0.02f, 0.88f, -hd), new Vector3(w + 0.04f, 0.04f, d + 0.02f), Bldg.PaintCream, 0.05f);
                    b.AddBox(new Vector3(-0.25f, 0.85f, -0.2f), new Vector3(0.5f, 0.071f, 0.38f), Bldg.Steel * 0.8f, 0.03f);
                    b.AddFrustum(new Vector3(0f, 0.92f, -0.25f), new Vector3(0f, 1.15f, -0.2f), 0.015f, 0.015f, 4, Bldg.Steel, 0.04f);
                    if (w > 1.4f) b.AddBox(new Vector3(hw - 0.6f, 0.92f, -hd + 0.05f), new Vector3(0.5f, 0.3f, 0.36f), Bldg.Appliance, 0.05f);
                    for (int i = 0; i < Mathf.FloorToInt(w / 0.5f); i++) b.AddBox(new Vector3(-hw + 0.05f + i * 0.5f, 0.15f, hd - 0.031f), new Vector3(0.44f, 0.65f, 0.005f), Furn.Wood, 0.06f);
                    break;
                case BF.Toilet:
                    b.AddBox(new Vector3(-0.2f, 0.35f, -hd), new Vector3(0.4f, 0.4f, 0.18f), Bldg.Porcelain, 0.05f);
                    b.AddFrustum(new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0.4f, 0.08f), 0.14f, 0.2f, 7, Bldg.Porcelain * 0.95f, 0.05f);
                    b.AddFrustum(new Vector3(0f, 0.4f, 0.08f), new Vector3(0f, 0.43f, 0.08f), 0.21f, 0.21f, 7, variant == 1 ? Bldg.Black : Bldg.Porcelain * 0.85f, 0.05f);
                    break;
                case BF.Stall:
                    b.AddBox(new Vector3(-hw, 0.15f, -hd), new Vector3(w, 1.75f, d), Bldg.PaintBlue * 0.9f, 0.05f);
                    break;
                case BF.Sink:
                    b.AddFrustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.7f, 0f), 0.08f, 0.06f, 5, Bldg.Porcelain * 0.9f, 0.05f);
                    b.AddBox(new Vector3(-hw, 0.7f, -hd), new Vector3(w, 0.16f, d), Bldg.Porcelain, 0.05f);
                    b.AddBox(new Vector3(-hw + 0.06f, 0.861f, -hd + 0.08f), new Vector3(w - 0.12f, 0.005f, d - 0.14f), Bldg.Grime, 0.08f);
                    b.AddFrustum(new Vector3(0f, 0.86f, -hd + 0.05f), new Vector3(0f, 1.0f, -hd + 0.12f), 0.015f, 0.015f, 4, Bldg.Steel, 0.04f);
                    break;
                case BF.Locker:
                case BF.LockerBank:
                {
                    int n = kind == BF.Locker ? 1 : 3;
                    float lw = w / n;
                    Color body = variant == 1 ? Bldg.PaintBlue : variant == 2 ? Bldg.PaintGreen : Bldg.Steel;
                    for (int i = 0; i < n; i++)
                    {
                        float x = -hw + i * lw;
                        b.AddBox(new Vector3(x + 0.01f, 0f, -hd), new Vector3(lw - 0.02f, 1.9f, d), body, 0.06f);
                        bool open = kind == BF.LockerBank && b.Next() < 0.2f;
                        if (open) AddBoxAt(b, new Vector3(x + 0.02f + 0.2f, 1.0f, hd + 0.22f), new Vector3(0.02f, 1.7f, lw - 0.06f), Quaternion.Euler(0f, 20f, 0f), body * 1.1f);
                        else
                        {
                            b.AddBox(new Vector3(x + 0.03f, 0.1f, hd), new Vector3(lw - 0.06f, 1.72f, 0.01f), body * 1.08f, 0.05f);
                            for (int s = 0; s < 4; s++) b.AddBox(new Vector3(x + lw * 0.25f, 1.55f + s * 0.05f, hd + 0.01f), new Vector3(lw * 0.5f, 0.015f, 0.004f), Bldg.SteelDark, 0.03f);
                            b.AddBox(new Vector3(x + lw - 0.12f, 0.95f, hd + 0.01f), new Vector3(0.03f, 0.1f, 0.03f), Bldg.SteelDark, 0.03f);
                        }
                    }
                    break;
                }
                case BF.Bench:
                    foreach (float x in new[] { -hw + 0.1f, hw - 0.14f }) b.AddBox(new Vector3(x, 0f, -hd + 0.03f), new Vector3(0.04f, 0.42f, d - 0.06f), Bldg.SteelDark, 0.04f);
                    for (int i = 0; i < 3; i++) b.AddBox(new Vector3(-hw, 0.42f, -hd + i * d / 3f), new Vector3(w, 0.04f, d / 3f - 0.02f), b.Jitter(Furn.Wood, 0.08f), 0.05f);
                    break;
                case BF.Workbench:
                    b.AddBox(new Vector3(-hw, 0.85f, -hd), new Vector3(w, 0.07f, d), Furn.WoodDark, 0.06f);
                    foreach (float x in new[] { -hw + 0.03f, hw - 0.1f })
                        foreach (float z in new[] { -hd + 0.03f, hd - 0.1f })
                            b.AddBox(new Vector3(x, 0f, z), new Vector3(0.07f, 0.85f, 0.07f), Bldg.SteelDark, 0.04f);
                    b.AddBox(new Vector3(-hw + 0.05f, 0.2f, -hd + 0.05f), new Vector3(w - 0.1f, 0.03f, d - 0.1f), Furn.WoodDark, 0.05f);
                    b.AddBox(new Vector3(hw - 0.35f, 0.92f, hd - 0.2f), new Vector3(0.2f, 0.14f, 0.18f), Bldg.RackBlue, 0.05f);
                    b.AddBox(new Vector3(-hw + 0.2f, 0.92f, -hd + 0.1f), new Vector3(0.45f, 0.2f, 0.22f), Bldg.Red, 0.06f);
                    for (int i = 0; i < 3; i++) AddBoxAt(b, new Vector3(b.Range(-hw + 0.3f, hw - 0.3f), 0.93f, b.Range(-hd + 0.1f, hd - 0.1f)), new Vector3(0.25f, 0.025f, 0.04f), Quaternion.Euler(0f, b.Range(0f, 180f), 0f), Bldg.Steel);
                    if (variant > 0) b.AddBox(new Vector3(-hw + 0.1f, 0.23f, -hd + 0.1f), new Vector3(0.5f, 0.3f, 0.4f), Bldg.Cardboard, 0.08f);
                    break;
                case BF.ToolBoard:
                    b.AddBox(new Vector3(-hw, 0f, 0f), new Vector3(w, 0.8f, 0.02f), Bldg.Cardboard * 0.8f, 0.04f);
                    for (int i = 0; i < 6; i++)
                        AddBoxAt(b, new Vector3(b.Range(-hw + 0.15f, hw - 0.15f), b.Range(0.15f, 0.65f), 0.03f), new Vector3(0.04f, b.Range(0.15f, 0.3f), 0.02f), Quaternion.Euler(0f, 0f, b.Range(-30f, 30f)), b.Next() < 0.5f ? Bldg.Steel : Bldg.Red);
                    break;
                case BF.CableSpool:
                    foreach (float z in new[] { -0.32f, 0.26f }) b.AddFrustum(new Vector3(0f, 0.4f, z), new Vector3(0f, 0.4f, z + 0.06f), 0.4f, 0.4f, 9, Palette.Plank, 0.08f);
                    b.AddFrustum(new Vector3(0f, 0.4f, -0.26f), new Vector3(0f, 0.4f, 0.26f), 0.3f, 0.3f, 9, Bldg.Black, 0.06f);
                    break;
                case BF.ServerRack:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 2.0f, d), Bldg.Black, 0.04f);
                    for (int i = 0; i < 10; i++)
                    {
                        if (b.Next() < 0.25f) continue;
                        float y = 0.15f + i * 0.17f;
                        b.AddBox(new Vector3(-hw + 0.04f, y, hd), new Vector3(w - 0.08f, 0.13f, 0.01f), Bldg.SteelDark * 1.3f, 0.06f);
                        if (b.Next() < 0.5f) b.AddBox(new Vector3(hw - 0.12f, y + 0.05f, hd + 0.01f), new Vector3(0.02f, 0.02f, 0.005f), b.Next() < 0.5f ? new Color(0.2f, 0.6f, 0.25f) : new Color(0.7f, 0.2f, 0.15f), 0.02f);
                    }
                    break;
                case BF.Boiler:
                {
                    Color body = Color.Lerp(Bldg.Rust, Bldg.SteelDark, 0.4f);
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 0.15f, d), Bldg.Concrete, 0.04f);
                    b.AddFrustum(new Vector3(0f, 0.15f, 0f), new Vector3(0f, 1.9f, 0f), 0.62f, 0.62f, 10, body, 0.1f);
                    b.AddFrustum(new Vector3(0f, 1.9f, 0f), new Vector3(0f, 2.15f, 0f), 0.62f, 0.3f, 10, body * 0.9f, 0.08f);
                    b.AddFrustum(new Vector3(0f, 2.15f, 0f), new Vector3(0f, 2.6f, 0f), 0.12f, 0.12f, 6, Bldg.SteelDark, 0.05f);
                    foreach (float y in new[] { 0.6f, 1.3f }) b.AddFrustum(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.06f, 0f), 0.64f, 0.64f, 10, Bldg.SteelDark, 0.05f);
                    b.AddFrustum(new Vector3(0.2f, 1.2f, 0.6f), new Vector3(0.2f, 1.2f, 0.66f), 0.1f, 0.1f, 8, Bldg.Paper, 0.03f);
                    b.AddFrustum(new Vector3(-0.25f, 0.9f, 0.6f), new Vector3(-0.25f, 0.9f, 0.7f), 0.13f, 0.13f, 6, Bldg.Red, 0.05f);
                    break;
                }
                case BF.Transformer:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 1.4f, d), new Color(0.25f, 0.32f, 0.26f), 0.05f);
                    for (int i = 0; i < 6; i++) b.AddBox(new Vector3(-hw + 0.1f + i * (w - 0.2f) / 5f, 0.2f, hd), new Vector3(0.03f, 1.0f, 0.08f), new Color(0.22f, 0.28f, 0.23f), 0.04f);
                    b.AddBox(new Vector3(-0.15f, 1.15f, hd + 0.08f), new Vector3(0.3f, 0.2f, 0.005f), new Color(0.65f, 0.55f, 0.12f), 0.03f);
                    break;
                case BF.SetFlat:
                {
                    Color paper = variant switch { 0 => new Color(0.42f, 0.33f, 0.25f), 1 => new Color(0.30f, 0.34f, 0.28f), _ => new Color(0.45f, 0.40f, 0.33f) };
                    b.AddBox(new Vector3(-hw, 0f, hd - 0.06f), new Vector3(w, 2.4f, 0.06f), paper, 0.05f);
                    for (int i = 0; i < Mathf.FloorToInt(w / 0.25f); i++) b.AddBox(new Vector3(-hw + i * 0.25f, 0.05f, hd + 0.001f), new Vector3(0.012f, 2.3f, 0.004f), paper * 0.85f, 0.03f);
                    if (w > 2f) b.AddBox(new Vector3(-0.45f, 1.0f, hd + 0.002f), new Vector3(0.9f, 0.8f, 0.006f), Rust.Glass, 0.03f);
                    b.AddBox(new Vector3(-hw, 0f, hd - 0.09f), new Vector3(w, 2.4f, 0.03f), Palette.Plank, 0.08f);
                    foreach (float x in new[] { -hw + 0.3f, hw - 0.3f })
                        AddBoxAt(b, new Vector3(x, 0.9f, -0.0f), new Vector3(0.05f, 2.1f, 0.05f), Quaternion.Euler(-38f, 0f, 0f), Palette.Plank);
                    break;
                }
                case BF.LightRig:
                    TripodAt(b, 1.6f);
                    AddBoxAt(b, new Vector3(0f, 1.75f, 0.05f), new Vector3(0.32f, 0.3f, 0.25f), Quaternion.Euler(b.Range(-25f, 10f), 0f, 0f), Bldg.Black);
                    AddBoxAt(b, new Vector3(0f, 1.75f, 0.19f), new Vector3(0.26f, 0.24f, 0.01f), Quaternion.Euler(b.Range(-25f, 10f), 0f, 0f), variant == 0 ? new Color(0.65f, 0.62f, 0.5f) : Bldg.SteelDark);
                    break;
                case BF.Camera:
                    TripodAt(b, 1.35f);
                    b.AddBox(new Vector3(-0.12f, 1.38f, -0.2f), new Vector3(0.24f, 0.24f, 0.4f), Bldg.Black, 0.04f);
                    b.AddFrustum(new Vector3(0f, 1.5f, 0.2f), new Vector3(0f, 1.5f, 0.38f), 0.07f, 0.08f, 6, Bldg.SteelDark, 0.04f);
                    b.AddFrustum(new Vector3(0f, 1.66f, -0.1f), new Vector3(0f, 1.66f, 0.05f), 0.09f, 0.09f, 6, Bldg.SteelDark, 0.04f);
                    break;
                case BF.DirectorChair:
                    foreach (float x in new[] { -0.25f, 0.21f })
                    {
                        AddBoxAt(b, new Vector3(x + 0.02f, 0.25f, 0f), new Vector3(0.04f, 0.55f, 0.04f), Quaternion.Euler(30f, 0f, 0f), Furn.Wood);
                        AddBoxAt(b, new Vector3(x + 0.02f, 0.25f, 0f), new Vector3(0.04f, 0.55f, 0.04f), Quaternion.Euler(-30f, 0f, 0f), Furn.Wood);
                        b.AddBox(new Vector3(x, 0f, -0.2f), new Vector3(0.04f, 0.95f, 0.04f), Furn.Wood, 0.05f);
                    }
                    b.AddBox(new Vector3(-0.23f, 0.48f, -0.18f), new Vector3(0.46f, 0.02f, 0.36f), Bldg.Black, 0.04f);
                    b.AddBox(new Vector3(-0.23f, 0.7f, -0.2f), new Vector3(0.46f, 0.22f, 0.02f), Bldg.Black, 0.04f);
                    break;
                case BF.Bed:
                    return Bed(rng);
                case BF.Wardrobe:
                    return Wardrobe(rng);
                case BF.Mattress:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 0.18f, d), Furn.Stained, 0.1f);
                    break;
                case BF.Forklift:
                {
                    Color body = Rust.MachineYellow;
                    b.AddBox(new Vector3(-hw, 0.15f, -hd + 0.3f), new Vector3(w, 0.8f, d - 0.9f), body, 0.06f);
                    b.AddBox(new Vector3(-hw, 0.15f, -hd), new Vector3(w, 0.9f, 0.32f), Bldg.SteelDark, 0.05f);
                    foreach (float x in new[] { -hw - 0.05f, hw - 0.17f })
                        foreach (float z in new[] { -hd + 0.4f, hd - 0.9f })
                            b.AddBox(new Vector3(x, 0f, z - 0.2f), new Vector3(0.22f, 0.42f, 0.42f), Bldg.Black, 0.04f);
                    foreach (float x in new[] { -hw + 0.05f, hw - 0.1f })
                    {
                        b.AddBox(new Vector3(x, 0.95f, -hd + 0.4f), new Vector3(0.05f, 1.15f, 0.05f), Bldg.SteelDark, 0.04f);
                        b.AddBox(new Vector3(x, 0.95f, hd - 0.85f), new Vector3(0.05f, 1.15f, 0.05f), Bldg.SteelDark, 0.04f);
                    }
                    b.AddBox(new Vector3(-hw, 2.08f, -hd + 0.38f), new Vector3(w, 0.04f, d - 1.2f), Bldg.SteelDark, 0.04f);
                    b.AddBox(new Vector3(-0.25f, 0.95f, -0.25f), new Vector3(0.5f, 0.12f, 0.45f), Bldg.Black, 0.04f);
                    foreach (float x in new[] { -0.35f, 0.29f }) b.AddBox(new Vector3(x, 0.1f, hd - 0.62f), new Vector3(0.08f, 2.1f, 0.08f), Bldg.SteelDark, 0.04f);
                    foreach (float x in new[] { -0.3f, 0.2f }) b.AddBox(new Vector3(x, 0.05f, hd - 0.58f), new Vector3(0.1f, 0.05f, 0.58f), Bldg.SteelDark, 0.04f);
                    break;
                }
                case BF.BreakerPanel:
                    b.AddBox(new Vector3(-hw, 0f, 0f), new Vector3(w, 0.7f, 0.12f), Bldg.Steel, 0.05f);
                    if (variant % 2 == 1)
                    {
                        AddBoxAt(b, new Vector3(-hw - 0.15f, 0.35f, 0.3f), new Vector3(0.02f, 0.68f, w), Quaternion.Euler(0f, 60f, 0f), Bldg.Steel * 1.1f);
                        for (int r = 0; r < 6; r++) b.AddBox(new Vector3(-hw + 0.08f, 0.08f + r * 0.1f, 0.12f), new Vector3(w - 0.16f, 0.05f, 0.01f), Bldg.Black, 0.1f);
                    }
                    else b.AddBox(new Vector3(-hw + 0.02f, 0.02f, 0.12f), new Vector3(w - 0.04f, 0.66f, 0.01f), Bldg.Steel * 1.08f, 0.04f);
                    b.AddFrustum(new Vector3(hw - 0.1f, 0.7f, 0.05f), new Vector3(hw - 0.1f, 1.7f, 0.05f), 0.025f, 0.025f, 4, Bldg.SteelDark, 0.03f);
                    break;
                case BF.JunctionBox:
                    b.AddBox(new Vector3(-hw, 0f, 0f), new Vector3(w, w, 0.1f), Bldg.Steel * 0.9f, 0.05f);
                    b.AddFrustum(new Vector3(0f, w, 0.05f), new Vector3(0f, 1.0f, 0.05f), 0.02f, 0.02f, 4, Bldg.SteelDark, 0.03f);
                    b.AddFrustum(new Vector3(-hw, w * 0.5f, 0.05f), new Vector3(-hw - b.Range(0.5f, 1.5f), w * 0.5f, 0.05f), 0.02f, 0.02f, 4, Bldg.SteelDark, 0.03f);
                    break;
                case BF.Poster:
                {
                    Color bg = Pick(b.Rng, Bldg.Print);
                    float ph = w * 1.4f;
                    b.AddBox(new Vector3(-hw, -ph * 0.5f, 0f), new Vector3(w, ph, 0.006f), bg, 0.06f);
                    b.AddBox(new Vector3(-hw + 0.06f, ph * 0.05f, 0.006f), new Vector3(w - 0.12f, ph * 0.35f, 0.003f), Bldg.Black, 0.05f);
                    b.AddBox(new Vector3(-hw + 0.06f, -ph * 0.4f, 0.006f), new Vector3(w - 0.12f, 0.06f, 0.003f), Bldg.Paper, 0.05f);
                    if (variant == 1) b.AddTriangle(new Vector3(hw, ph * 0.5f, 0.012f), new Vector3(hw, ph * 0.2f, 0.012f), new Vector3(hw - 0.25f, ph * 0.5f, 0.012f), Bldg.PaintCream);
                    break;
                }
                case BF.Clock:
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0f, 0.05f), 0.16f, 0.16f, 12, Bldg.SteelDark, 0.03f);
                    b.AddFrustum(new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0f, 0.052f), 0.14f, 0.14f, 12, Bldg.Paper, 0.02f);
                    AddBoxAt(b, new Vector3(0f, 0.04f, 0.056f), new Vector3(0.015f, 0.09f, 0.004f), Quaternion.Euler(0f, 0f, b.Range(0f, 360f)), Bldg.Black);
                    AddBoxAt(b, new Vector3(0f, 0.05f, 0.056f), new Vector3(0.012f, 0.12f, 0.004f), Quaternion.Euler(0f, 0f, b.Range(0f, 360f)), Bldg.Black);
                    break;
                case BF.Extinguisher:
                    b.AddFrustum(new Vector3(0f, -0.3f, 0.09f), new Vector3(0f, 0.15f, 0.09f), 0.075f, 0.075f, 7, Bldg.Red, 0.05f);
                    b.AddFrustum(new Vector3(0f, 0.15f, 0.09f), new Vector3(0f, 0.25f, 0.09f), 0.04f, 0.02f, 5, Bldg.Black, 0.04f);
                    b.AddBox(new Vector3(-0.06f, -0.05f, 0f), new Vector3(0.12f, 0.08f, 0.03f), Bldg.SteelDark, 0.04f);
                    break;
                case BF.VentGrille:
                    b.AddBox(new Vector3(-hw, -0.15f, 0f), new Vector3(w, 0.3f, 0.03f), Bldg.Galvanised * 0.8f, 0.04f);
                    for (int i = 0; i < 5; i++) b.AddBox(new Vector3(-hw + 0.04f, -0.12f + i * 0.055f, 0.03f), new Vector3(w - 0.08f, 0.02f, 0.02f), Bldg.Galvanised, 0.04f);
                    break;
                case BF.Mirror:
                    b.AddBox(new Vector3(-hw, 0f, 0f), new Vector3(w, 0.7f, 0.02f), Bldg.SteelDark, 0.03f);
                    b.AddBox(new Vector3(-hw + 0.03f, 0.03f, 0.02f), new Vector3(w - 0.06f, 0.64f, 0.004f), new Color(0.28f, 0.32f, 0.33f), 0.03f);
                    if (variant == 1) b.AddTriangle(new Vector3(-0.1f, 0.6f, 0.026f), new Vector3(0.15f, 0.1f, 0.026f), new Vector3(0.12f, 0.12f, 0.026f), Bldg.Paper);
                    break;
                case BF.Whiteboard:
                    b.AddBox(new Vector3(-hw, 0f, 0f), new Vector3(w, 0.9f, 0.03f), Bldg.Steel, 0.03f);
                    b.AddBox(new Vector3(-hw + 0.03f, 0.03f, 0.03f), new Vector3(w - 0.06f, 0.84f, 0.004f), Bldg.Paper * 1.05f, 0.03f);
                    for (int i = 0; i < 5; i++) AddBoxAt(b, new Vector3(b.Range(-hw + 0.25f, hw - 0.25f), b.Range(0.2f, 0.75f), 0.036f), new Vector3(b.Range(0.15f, 0.4f), 0.015f, 0.003f), Quaternion.Euler(0f, 0f, b.Range(-20f, 20f)), i == 0 ? Bldg.Red : Bldg.Black);
                    break;
                case BF.Papers:
                    for (int i = 0; i < 3 + variant; i++)
                        AddBoxAt(b, new Vector3(b.Range(-hw, hw), 0.006f + i * 0.002f, b.Range(-hd, hd)), new Vector3(0.21f, 0.003f, 0.3f), Quaternion.Euler(0f, b.Range(0f, 360f), 0f), b.Jitter(Bldg.Paper, 0.1f));
                    break;
                case BF.Bottles:
                    for (int i = 0; i < 2 + variant; i++)
                    {
                        Color glass = b.Next() < 0.5f ? new Color(0.15f, 0.3f, 0.15f) : new Color(0.3f, 0.18f, 0.08f);
                        var at = new Vector3(b.Range(-hw, hw), 0f, b.Range(-hd, hd));
                        if (b.Next() < 0.5f) b.AddFrustum(at, at + Vector3.up * 0.25f, 0.035f, 0.015f, 5, glass, 0.05f);
                        else
                        {
                            float a = b.Range(0f, Mathf.PI * 2f);
                            b.AddFrustum(at + Vector3.up * 0.035f, at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.25f + Vector3.up * 0.03f, 0.035f, 0.015f, 5, glass, 0.05f);
                        }
                    }
                    break;
                case BF.Stain:
                {
                    Color stain = variant == 3 ? new Color(0.24f, 0.13f, 0.11f) : variant == 2 ? new Color(0.21f, 0.23f, 0.19f) : new Color(0.23f, 0.21f, 0.18f);
                    int n = 9;
                    var c = new Vector3(0f, 0.012f, 0f);
                    Vector3 prev = default, first = default;
                    for (int i = 0; i <= n; i++)
                    {
                        float a = (i % n) / (float)n * Mathf.PI * 2f, r = (i == n ? 1f : b.Range(0.55f, 1f)) * hw;
                        var q = i == n ? first : new Vector3(Mathf.Cos(a) * r, 0.012f, Mathf.Sin(a) * r * b.Range(0.6f, 1f));
                        if (i == 0) first = q;
                        else b.AddTriangle(c, q, prev, stain);
                        prev = q;
                    }
                    break;
                }
                case BF.Debris:
                    for (int i = 0; i < 3 + variant; i++)
                        b.AddBlob(new Vector3(b.Range(-hw, hw), 0.03f, b.Range(-hd, hd)), new Vector3(b.Range(0.04f, 0.1f), b.Range(0.02f, 0.05f), b.Range(0.04f, 0.1f)), 0, 0.3f, _ => b.Jitter(b.Next() < 0.5f ? Bldg.Paint : Bldg.Concrete, 0.1f), true);
                    AddBoxAt(b, new Vector3(b.Range(-hw, hw), 0.01f, b.Range(-hd, hd)), new Vector3(0.5f, 0.015f, 0.5f), Quaternion.Euler(0f, b.Range(0f, 90f), b.Range(-6f, 6f)), Bldg.PaintCream * 0.9f);
                    break;
                case BF.Cobweb:
                {
                    Color web = new Color(0.62f, 0.62f, 0.6f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (-30f + i * 30f) * Mathf.Deg2Rad, r = b.Range(0.35f, 0.55f);
                        var tip = new Vector3(Mathf.Sin(a) * r, -b.Range(0.15f, 0.35f), Mathf.Cos(a) * r);
                        b.AddDoubleSided(Vector3.zero, tip, tip + new Vector3(0.012f, 0f, 0f), web);
                    }
                    b.AddDoubleSided(new Vector3(-0.2f, -0.05f, 0.2f), new Vector3(0.2f, -0.05f, 0.2f), new Vector3(0f, -0.2f, 0.3f), web * 0.8f);
                    break;
                }
                case BF.DeadPlant:
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0.32f, 0f), 0.13f, 0.18f, 7, Rust.Orange * 0.9f, 0.06f);
                    b.AddFrustum(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.31f, 0f), 0.16f, 0.16f, 7, Bldg.Grime, 0.05f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = b.Range(0f, Mathf.PI * 2f);
                        var top = new Vector3(Mathf.Cos(a) * b.Range(0.1f, 0.35f), b.Range(0.6f, 1.1f), Mathf.Sin(a) * b.Range(0.1f, 0.35f));
                        b.AddFrustum(new Vector3(0f, 0.3f, 0f), top, 0.012f, 0.004f, 3, new Color(0.32f, 0.26f, 0.16f), 0.08f);
                    }
                    break;
                case BF.Pipes:
                {
                    int n = Mathf.Max(1, variant);
                    float len = w;
                    for (int i = 0; i < n; i++)
                    {
                        float r = b.Range(0.04f, 0.08f), off = i * 0.18f;
                        Color c = i % 3 == 0 ? Bldg.Rust : i % 3 == 1 ? Bldg.PaintGreen * 0.8f : Bldg.Steel;
                        b.AddFrustum(new Vector3(-off, -off * 0.3f, 0f), new Vector3(-off, -off * 0.3f, len), r, r, 6, c, 0.08f);
                    }
                    for (float z = 0.5f; z < len; z += 2f) b.AddBox(new Vector3(-n * 0.18f, 0.06f, z), new Vector3(n * 0.18f + 0.1f, 0.03f, 0.04f), Bldg.SteelDark, 0.04f);
                    break;
                }
                case BF.Duct:
                {
                    float len = w;
                    b.AddBox(new Vector3(-0.22f, -0.16f, 0f), new Vector3(0.44f, 0.32f, len), Bldg.Galvanised, 0.04f);
                    for (float z = 1.2f; z < len; z += 1.2f) b.AddBox(new Vector3(-0.235f, -0.175f, z), new Vector3(0.47f, 0.35f, 0.05f), Bldg.Galvanised * 0.85f, 0.03f);
                    break;
                }
                default:
                    b.AddBox(new Vector3(-hw, 0f, -hd), new Vector3(w, 0.5f, d), Bldg.Concrete, 0.05f);
                    break;
            }
            return b.ToMesh(kind.ToString());
        }

        static void TripodAt(LowPolyMeshBuilder b, float height)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                b.AddFrustum(new Vector3(Mathf.Cos(a) * 0.3f, 0f, Mathf.Sin(a) * 0.3f), new Vector3(0f, height * 0.6f, 0f), 0.015f, 0.015f, 3, Bldg.Black, 0.04f);
            }
            b.AddFrustum(new Vector3(0f, height * 0.55f, 0f), new Vector3(0f, height, 0f), 0.02f, 0.02f, 4, Bldg.SteelDark, 0.04f);
        }
    }
}
