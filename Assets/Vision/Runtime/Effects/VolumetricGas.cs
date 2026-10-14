using UnityEngine;

namespace Vision.Effects
{
    /// <summary>
    /// Volumetric gas, after Black Ops Cold War's smoke (the user's reference): every cloud is a box drawn each frame with
    /// the Vision/Gas shader, which marches the view ray through it and gathers a lit, billowing participating medium.
    /// Shapes: a blob sitting on the ground (galaxy gas, a puff of the scent), or a cone (Penjamin). The medium is a
    /// tiling 64^3 noise volume made here once: Perlin-Worley billows (red), finer Worley detail that frays the edges into
    /// wisps (green), and two smooth noises for the tint and the slow warp that rolls the billows over each other (blue,
    /// alpha). The gas never touches the scene: no collision and no depth test, it just covers the screen where it is.
    /// </summary>
    public static class VolumetricGas
    {
        public const int NoiseSize = 64;

        static Mesh cube;
        static Material plain, masked;
        static Texture3D noise;
        static MaterialPropertyBlock block;
        static readonly int TintAId = Shader.PropertyToID("_GasTintA"), TintBId = Shader.PropertyToID("_GasTintB"), ShapeId = Shader.PropertyToID("_GasShape");
        static readonly int MotionId = Shader.PropertyToID("_GasMotion"), ConeId = Shader.PropertyToID("_GasCone"), NoiseId = Shader.PropertyToID("_GasNoise");
        static readonly int EdgeId = Shader.PropertyToID("_GasEdge");

        /// <summary>
        /// How the medium looks: extinction per metre (how thick), the noise's scale per metre, how far the edges erode, and
        /// how patchy it is (<see cref="Clumps"/> 0 even; 1 broken into thick pockets and thin stretches with gaps between).
        /// </summary>
        public struct Look
        {
            public Color TintA, TintB;
            public float Opacity, Extinction, NoiseScale, Erosion;
            public Vector3 Drift;
            public float Seed;
            public float Clumps;
            /// <summary>How ragged a cone's edges are: its radius pushed in and out by a slow noise (0 smooth).</summary>
            public float Wobble;
        }

        /// <summary>Draws a cloud sitting on the ground at <paramref name="ground"/>, <paramref name="radius"/> across and <paramref name="height"/> tall.</summary>
        public static void Blob(Vector3 ground, float radius, float height, in Look look, bool maskedByLight, bool senses)
        {
            if (Vfx.SkipSmoke || radius <= 0.01f || look.Opacity <= 0.002f || !Ready()) return;
            Matrix4x4 m = Matrix4x4.TRS(ground + Vector3.up * height * 0.4f, Quaternion.identity, new Vector3(radius * 2f, height, radius * 2f));
            Draw(m, look, 0f, new Vector4(0f, 0f, 0f, look.Clumps), maskedByLight, senses);
        }

        /// <summary>
        /// Draws a cone of gas from <paramref name="tip"/> along <paramref name="dir"/> (on the ground plane) for
        /// <paramref name="length"/>, <paramref name="endRadius"/> wide at its open end and <paramref name="baseFraction"/>
        /// of that at the tip, its middle <paramref name="height"/> above the ground; <paramref name="tall"/> stretches it
        /// up (and down) by that much.
        /// </summary>
        public static void Cone(Vector3 tip, Vector3 dir, float length, float endRadius, float baseFraction, float height, in Look look, bool maskedByLight, bool senses, float tall = 1f)
        {
            if (Vfx.SkipSmoke || length <= 0.01f || look.Opacity <= 0.002f || !Ready()) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            Quaternion q = Quaternion.FromToRotation(Vector3.right, dir);
            // Room in the box for the ragged edge to swell past the cone's own radius.
            float pad = 1f + Mathf.Max(0f, look.Wobble) * 0.85f;
            Matrix4x4 m = Matrix4x4.TRS(tip + dir * length * 0.5f + Vector3.up * height, q, new Vector3(length, endRadius * 1.6f * tall * pad, endRadius * 2f * pad));
            Draw(m, look, 1f, new Vector4(baseFraction, 0.08f, 0.18f, look.Clumps), maskedByLight, senses, pad);
        }

        /// <summary>
        /// A tube of gas from <paramref name="from"/> along <paramref name="dir"/> (on the ground plane) for
        /// <paramref name="length"/>, <paramref name="radius"/> thick, its middle <paramref name="height"/> above the ground; its
        /// ends soft, so tubes laid end to end with a little overlap read as one unbroken thread (the scent).
        /// </summary>
        public static void Tube(Vector3 from, Vector3 dir, float length, float radius, float height, in Look look, bool maskedByLight, bool senses)
        {
            if (Vfx.SkipSmoke || length <= 0.01f || look.Opacity <= 0.002f || !Ready()) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            Quaternion q = Quaternion.FromToRotation(Vector3.right, dir);
            Matrix4x4 m = Matrix4x4.TRS(from + dir * length * 0.5f + Vector3.up * height, q, new Vector3(length, radius * 1.6f, radius * 2f));
            Draw(m, look, 1f, new Vector4(1f, 0.25f, 0.25f, 0f), maskedByLight, senses);
        }

        static void Draw(Matrix4x4 m, in Look look, float kind, Vector4 cone, bool maskedByLight, bool senses, float pad = 1f)
        {
            block.Clear();
            Color a = look.TintA;
            a.a = look.Opacity;
            block.SetColor(TintAId, a);
            block.SetColor(TintBId, look.TintB);
            block.SetVector(ShapeId, new Vector4(kind, look.Extinction, look.NoiseScale, look.Erosion));
            block.SetVector(MotionId, new Vector4(look.Drift.x, look.Drift.y, look.Drift.z, look.Seed));
            block.SetVector(ConeId, cone);
            block.SetVector(EdgeId, new Vector4(look.Wobble, pad, 0f, 0f));
            block.SetTexture(NoiseId, noise);
            // (The senses overlay camera draws none of this shader, however it is handed over: every cloud is drawn in the
            // scene; Penjamin's unmasked, so it shows past the light.)
            Graphics.DrawMesh(cube, m, maskedByLight ? masked : plain, 0, null, 0, block, false, false, false);
        }

        /// <summary>Makes the noise volume and materials now (while a level loads), so the first gas doesn't stall a frame.</summary>
        public static void Prepare() => Ready();

        static bool Ready()
        {
            if (plain != null) return true;
            Shader s = Shader.Find("Vision/Gas");
            if (s == null) return false;
            plain = new Material(s) { name = "Gas", hideFlags = HideFlags.HideAndDontSave, renderQueue = 3005 };
            masked = new Material(s) { name = "Gas masked", hideFlags = HideFlags.HideAndDontSave, renderQueue = 3005 };
            masked.EnableKeyword("_VISION_MASKED");
            masked.SetFloat("_Masked", 1f);
            cube = Cube();
            noise = MakeNoise();
            block = new MaterialPropertyBlock();
            return true;
        }

        static Mesh Cube()
        {
            var mesh = new Mesh { name = "Gas Box", hideFlags = HideFlags.HideAndDontSave };
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            mesh.vertices = v;
            mesh.triangles = new[]
            {
                0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4,
                2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
            };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        // ---------------------------------------------------------------- the noise volume

        /// <summary>The tiling noise volume (made once, about a quarter of a second on the first gas).</summary>
        public static Texture3D MakeNoise()
        {
            int n = NoiseSize;
            var data = new Color32[n * n * n];
            System.Threading.Tasks.Parallel.For(0, n, z =>
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector3((x + 0.5f) / n, (y + 0.5f) / n, (z + 0.5f) / n);
                        float perlin = Fbm(p, 4, 4);
                        float worley = Worley(p, 4) * 0.625f + Worley(p, 8) * 0.25f + Worley(p, 16) * 0.125f;
                        // Perlin-Worley: the smooth noise remapped by the cells, rounded billows like cauliflower.
                        float billow = Mathf.Clamp01((perlin - (worley - 1f)) / (1f - (worley - 1f)));
                        // Contrast: round lobes with clear gaps between them.
                        billow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.78f, billow));
                        float detail = Worley(p, 16) * 0.6f + Worley(p, 32) * 0.4f;
                        float tint = Fbm(p + new Vector3(0.37f, 0.11f, 0.73f), 2, 3);
                        float warp = Fbm(p + new Vector3(0.71f, 0.53f, 0.19f), 2, 3);
                        data[(z * n + y) * n + x] = new Color32((byte)(billow * 255f), (byte)(detail * 255f), (byte)(tint * 255f), (byte)(warp * 255f));
                    }
            });
            var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false)
            {
                name = "Gas Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(data);
            tex.Apply(false, true);
            return tex;
        }

        static uint Hash(int x, int y, int z, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791) ^ (uint)(s * 2654435761u);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h;
            }
        }

        static float Rand(int x, int y, int z, int s) => (Hash(x, y, z, s) & 0xFFFFFF) / (float)0x1000000;

        static int Wrap(int i, int period) => ((i % period) + period) % period;

        /// <summary>Tiling value noise at <paramref name="period"/> cells across the unit cube.</summary>
        static float Value(Vector3 p, int period, int seed)
        {
            float fx = p.x * period, fy = p.y * period, fz = p.z * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy), z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0, ty = fy - y0, tz = fz - z0;
            tx = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            ty = ty * ty * ty * (ty * (ty * 6f - 15f) + 10f);
            tz = tz * tz * tz * (tz * (tz * 6f - 15f) + 10f);
            float C(int dx, int dy, int dz) => Rand(Wrap(x0 + dx, period), Wrap(y0 + dy, period), Wrap(z0 + dz, period), seed);
            float a = Mathf.Lerp(Mathf.Lerp(C(0, 0, 0), C(1, 0, 0), tx), Mathf.Lerp(C(0, 1, 0), C(1, 1, 0), tx), ty);
            float b = Mathf.Lerp(Mathf.Lerp(C(0, 0, 1), C(1, 0, 1), tx), Mathf.Lerp(C(0, 1, 1), C(1, 1, 1), tx), ty);
            return Mathf.Lerp(a, b, tz);
        }

        static float Fbm(Vector3 p, int period, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(p, period << o, 11 + o * 7) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Tiling Worley (cellular) noise, inverted: 1 at each cell's feature point, falling to 0 between them.</summary>
        static float Worley(Vector3 p, int cells)
        {
            float fx = p.x * cells, fy = p.y * cells, fz = p.z * cells;
            int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy), cz = Mathf.FloorToInt(fz);
            float best = 9f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = cx + dx, y = cy + dy, z = cz + dz;
                        int wx = Wrap(x, cells), wy = Wrap(y, cells), wz = Wrap(z, cells);
                        float px = x + Rand(wx, wy, wz, 101 + cells), py = y + Rand(wx, wy, wz, 202 + cells), pz = z + Rand(wx, wy, wz, 303 + cells);
                        float ex = px - fx, ey = py - fy, ez = pz - fz;
                        best = Mathf.Min(best, ex * ex + ey * ey + ez * ez);
                    }
            return Mathf.Clamp01(1f - Mathf.Sqrt(best));
        }
    }

}
