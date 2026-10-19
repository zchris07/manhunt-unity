using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// A cabin's fittings: its floor (weathered boards, or stone flags in grout), wallpaper gone to ruin (faded stripes in
    /// strips, some torn short, some gone, grimy along the floor), stains (water and grime on the walls, blood on the
    /// floor and walls), an iron stove with its flue, and a worn rug.
    /// </summary>
    public static partial class LowPolyModels
    {
        /// <summary>The top of a cabin's floor over the ground it's laid on.</summary>
        public const float CabinFloorTop = 0.012f;

        static void FlatQuad(LowPolyMeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, Color col, Vector3 normal)
        {
            Vector3 inside = (a + c + d + e) * 0.25f - normal;
            b.AddTriangleOutward(a, c, d, col, inside);
            b.AddTriangleOutward(a, d, e, col, inside);
        }

        /// <summary>
        /// A cabin's floor, <paramref name="w"/> by <paramref name="d"/> (x by z, centred), its top at
        /// <see cref="CabinFloorTop"/>: boards running the long way, laid in staggered lengths, a few darker or paler; or
        /// flagstones in rough rows, grey and brown-grey, a little uneven, set in dark grout.
        /// </summary>
        public static Mesh CabinFloor(System.Random rng, float w, float d, bool stone)
        {
            var b = new LowPolyMeshBuilder(rng);
            float hw = w * 0.5f, hd = d * 0.5f, top = CabinFloorTop;
            // What shows between them: the joists' shadow, or the grout.
            FlatQuad(b, new Vector3(-hw, top - 0.006f, -hd), new Vector3(hw, top - 0.006f, -hd), new Vector3(hw, top - 0.006f, hd), new Vector3(-hw, top - 0.006f, hd),
                stone ? new Color(0.17f, 0.16f, 0.14f) : new Color(0.1f, 0.08f, 0.07f), Vector3.up);
            bool alongX = w >= d;
            float len = alongX ? w : d, across = alongX ? d : w;
            Vector3 At(float a, float c, float y) => alongX ? new Vector3(a, y, c) : new Vector3(c, y, a);
            if (!stone)
            {
                for (float c0 = -across * 0.5f; c0 < across * 0.5f - 0.02f;)
                {
                    float bw = Mathf.Min(b.Range(0.16f, 0.22f), across * 0.5f - c0), c1 = c0 + bw - 0.008f;
                    for (float a0 = -len * 0.5f - b.Range(0f, 1.2f); a0 < len * 0.5f;)
                    {
                        float bl = b.Range(1.2f, 2.4f), x0 = Mathf.Max(a0, -len * 0.5f), x1 = Mathf.Min(a0 + bl - 0.008f, len * 0.5f);
                        a0 += bl;
                        if (x1 - x0 < 0.05f) continue;
                        float roll = b.Next();
                        Color col = b.Jitter(roll < 0.12f ? Site.WeatheredDark : roll < 0.2f ? Site.Weathered * 1.18f : Site.Weathered, 0.1f);
                        float y = top + b.Range(-0.002f, 0.002f);
                        FlatQuad(b, At(x0, c0, y), At(x1, c0, y), At(x1, c1, y), At(x0, c1, y), col, Vector3.up);
                    }
                    c0 += bw;
                }
            }
            else
            {
                Color[] tones = { new Color(0.42f, 0.41f, 0.38f), new Color(0.36f, 0.35f, 0.33f), new Color(0.44f, 0.4f, 0.34f), new Color(0.32f, 0.31f, 0.3f) };
                for (float c0 = -across * 0.5f; c0 < across * 0.5f - 0.05f;)
                {
                    float rh = Mathf.Min(b.Range(0.42f, 0.66f), across * 0.5f - c0);
                    for (float a0 = -len * 0.5f - b.Range(0f, 0.3f); a0 < len * 0.5f;)
                    {
                        float sl = b.Range(0.4f, 0.8f), x0 = Mathf.Max(a0, -len * 0.5f) + 0.012f, x1 = Mathf.Min(a0 + sl, len * 0.5f) - 0.012f;
                        a0 += sl;
                        if (x1 - x0 < 0.08f) continue;
                        float y = top + b.Range(-0.003f, 0.003f), j = 0.012f;
                        float y0 = c0 + 0.012f, y1 = c0 + rh - 0.012f;
                        Color col = b.Jitter(tones[b.Rng.Next(tones.Length)], 0.08f);
                        FlatQuad(b, At(x0 + b.Range(-j, j), y0 + b.Range(-j, j), y), At(x1 + b.Range(-j, j), y0 + b.Range(-j, j), y),
                            At(x1 + b.Range(-j, j), y1 + b.Range(-j, j), y), At(x0 + b.Range(-j, j), y1 + b.Range(-j, j), y), col, Vector3.up);
                    }
                    c0 += rh;
                }
            }
            return b.ToMesh(stone ? "Stone Floor" : "Board Floor");
        }

        /// <summary>Faded wallpaper palettes: pairs of stripe colours.</summary>
        static readonly (Color a, Color b)[] Papers =
        {
            (new Color(0.62f, 0.57f, 0.45f), new Color(0.45f, 0.5f, 0.4f)),
            (new Color(0.55f, 0.38f, 0.36f), new Color(0.62f, 0.56f, 0.46f)),
            (new Color(0.4f, 0.45f, 0.5f), new Color(0.58f, 0.58f, 0.55f)),
            (new Color(0.58f, 0.5f, 0.3f), new Color(0.42f, 0.33f, 0.24f)),
        };
        public static int WallpaperPalettes => Papers.Length;

        /// <summary>
        /// Wallpaper over <paramref name="length"/> metres of wall (local x 0 to length, up to <paramref name="height"/>, its
        /// face toward +z): strips half a metre wide in faded stripes, now and then a strip torn short (a ragged top) or
        /// gone altogether, grimy along the floor.
        /// </summary>
        public static Mesh Wallpaper(System.Random rng, float length, float height, int palette)
        {
            var b = new LowPolyMeshBuilder(rng);
            var (ca, cb) = Papers[Mathf.Abs(palette) % Papers.Length];
            const float strip = 0.53f, band = 0.088f, grime = 0.26f;
            for (float s0 = 0f; s0 < length - 0.01f; s0 += strip)
            {
                float s1 = Mathf.Min(length, s0 + strip);
                float roll = b.Next();
                if (roll < 0.1f) continue;
                bool torn = roll < 0.38f;
                float stripTop = torn ? b.Range(0.5f, height * 0.85f) : height;
                float fade = b.Range(0.88f, 1.05f);
                int k = 0;
                for (float x0 = s0; x0 < s1 - 0.005f; x0 += band, k++)
                {
                    float x1 = Mathf.Min(s1, x0 + band);
                    Color c = (k % 2 == 0 ? ca : cb) * fade;
                    float t0 = torn ? stripTop + b.Range(-0.12f, 0.12f) : height, t1 = torn ? stripTop + b.Range(-0.12f, 0.12f) : height;
                    // The grimy band along the floor, then the rest.
                    Color low = c * 0.72f;
                    low.a = c.a = 1f;
                    FlatQuad(b, new Vector3(x0, 0f, 0f), new Vector3(x1, 0f, 0f), new Vector3(x1, grime, 0f), new Vector3(x0, grime, 0f), low, Vector3.forward);
                    if (Mathf.Min(t0, t1) > grime + 0.02f)
                        FlatQuad(b, new Vector3(x0, grime, 0f), new Vector3(x1, grime, 0f), new Vector3(x1, t1, 0f), new Vector3(x0, t0, 0f), c, Vector3.forward);
                }
            }
            return b.ToMesh("Wallpaper");
        }

        /// <summary>
        /// A stain: an irregular blot (and, for blood, spatters about it and a drag smeared off one side), flat on the floor
        /// (facing up) or on a wall (facing +z), a few millimetres proud.
        /// </summary>
        public static Mesh Blot(System.Random rng, float radius, Color color, bool onWall, bool blood)
        {
            var b = new LowPolyMeshBuilder(rng);
            Vector3 P(float x, float y) => onWall ? new Vector3(x, y, 0.003f) : new Vector3(x, 0.003f, y);
            Vector3 n = onWall ? Vector3.forward : Vector3.up;
            void One(Vector2 at, float r, float stretch, Color c, float angle)
            {
                const int k = 10;
                var pts = new Vector3[k];
                for (int i = 0; i < k; i++)
                {
                    float a = i * Mathf.PI * 2f / k, rr = r * b.Range(0.6f, 1.1f);
                    var v = new Vector2(Mathf.Cos(a) * rr * stretch, Mathf.Sin(a) * rr);
                    v = new Vector2(v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle), v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle));
                    pts[i] = P(at.x + v.x, at.y + v.y);
                }
                Vector3 centre = P(at.x, at.y);
                Color dark = c * 0.85f;
                dark.a = 1f;
                for (int i = 0; i < k; i++) b.AddTriangleOutward(centre, pts[i], pts[(i + 1) % k], i % 2 == 0 ? c : dark, centre - n);
            }
            One(Vector2.zero, radius, b.Range(1f, 1.5f), color, b.Range(0f, Mathf.PI));
            if (blood)
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = b.Range(0f, Mathf.PI * 2f), r = radius * b.Range(1.1f, 2f);
                    One(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, radius * b.Range(0.05f, 0.14f), 1f, color * 0.95f, 0f);
                }
                // Dragged off to one side.
                float dir = b.Range(0f, Mathf.PI * 2f);
                for (int i = 1; i <= 4; i++)
                {
                    var at = new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * radius * (0.6f + i * 0.55f);
                    One(at, radius * b.Range(0.18f, 0.28f) * (1f - i * 0.12f), 2.2f, color * (0.9f + i * 0.04f), dir);
                }
            }
            return b.ToMesh(blood ? "Blood" : "Stain");
        }

        /// <summary>A cast-iron stove on short legs, its door in front (+z), its flue pipe up to <paramref name="flue"/>.</summary>
        public static Mesh Stove(System.Random rng, float flue = 2.5f)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color iron = new Color(0.14f, 0.14f, 0.15f), rust = new Color(0.32f, 0.18f, 0.1f);
            foreach (var (x, z) in new[] { (-0.24f, -0.18f), (0.2f, -0.18f), (-0.24f, 0.16f), (0.2f, 0.16f) })
                b.AddBox(new Vector3(x, 0f, z), new Vector3(0.05f, 0.12f, 0.05f), iron, 0.05f);
            b.AddBox(new Vector3(-0.3f, 0.12f, -0.24f), new Vector3(0.6f, 0.58f, 0.48f), b.Jitter(iron, 0.1f), 0.06f);
            b.AddBox(new Vector3(-0.33f, 0.7f, -0.27f), new Vector3(0.66f, 0.04f, 0.54f), iron * 1.15f, 0.05f);
            b.AddBox(new Vector3(-0.18f, 0.24f, 0.24f), new Vector3(0.36f, 0.3f, 0.02f), rust, 0.1f);
            b.AddBox(new Vector3(0.1f, 0.37f, 0.26f), new Vector3(0.06f, 0.03f, 0.03f), iron * 1.4f, 0.05f);
            b.AddFrustum(new Vector3(0f, 0.74f, -0.12f), new Vector3(0f, flue, -0.12f), 0.07f, 0.07f, 7, b.Jitter(iron, 0.1f), 0.06f);
            return b.ToMesh("Stove");
        }

        /// <summary>A worn rug, <paramref name="w"/> by <paramref name="d"/>: a faded border round a field with bands across it.</summary>
        public static Mesh Rug(System.Random rng, float w, float d)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color[] borders = { new Color(0.42f, 0.16f, 0.12f), new Color(0.22f, 0.24f, 0.34f), new Color(0.3f, 0.28f, 0.16f) };
            Color border = borders[rng.Next(borders.Length)], field = new Color(0.52f, 0.42f, 0.28f);
            float hw = w * 0.5f, hd = d * 0.5f, y = 0.006f, inset = 0.14f;
            FlatQuad(b, new Vector3(-hw, y, -hd), new Vector3(hw, y, -hd), new Vector3(hw, y, hd), new Vector3(-hw, y, hd), b.Jitter(border, 0.06f), Vector3.up);
            FlatQuad(b, new Vector3(-hw + inset, y + 0.002f, -hd + inset), new Vector3(hw - inset, y + 0.002f, -hd + inset), new Vector3(hw - inset, y + 0.002f, hd - inset), new Vector3(-hw + inset, y + 0.002f, hd - inset), b.Jitter(field, 0.06f), Vector3.up);
            for (int i = 0; i < 3; i++)
            {
                float x = -hw + inset + (w - 2f * inset) * (i + 1f) / 4f;
                FlatQuad(b, new Vector3(x - 0.05f, y + 0.004f, -hd + inset), new Vector3(x + 0.05f, y + 0.004f, -hd + inset), new Vector3(x + 0.05f, y + 0.004f, hd - inset), new Vector3(x - 0.05f, y + 0.004f, hd - inset), border * 0.9f, Vector3.up);
            }
            return b.ToMesh("Rug");
        }
    }
}
