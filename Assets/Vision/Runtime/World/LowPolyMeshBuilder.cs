using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vision.World
{
    /// <summary>
    /// Builds stylized low-poly meshes out of flat triangles. Every triangle has its own three
    /// vertices, its own face normal and its own (slightly jittered) colour, so lighting is faceted.
    /// Winding is clockwise seen from outside (Unity front faces); the *Outward helpers flip a
    /// triangle when needed so closed shapes never end up inside out.
    /// Colours are authored as sRGB and stored as linear vertex colours.
    /// </summary>
    public sealed class LowPolyMeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        /// <summary>Seeded random source, so every model is deterministic.</summary>
        public readonly System.Random Rng;

        public LowPolyMeshBuilder(System.Random rng = null) => Rng = rng ?? new System.Random(0);

        public int VertexCount => vertices.Count;
        public int TriangleCount => triangles.Count / 3;

        public float Next() => (float)Rng.NextDouble();

        public float Range(float min, float max) => min + Next() * (max - min);

        public Color Jitter(Color c, float amount)
        {
            float k = 1f + (Next() * 2f - 1f) * amount;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        // ------------------------------------------------------------------ triangles

        /// <summary>Adds one triangle; vertices must be clockwise seen from the front.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            Color linear = color.linear;
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int k = 0; k < 3; k++)
            {
                normals.Add(n);
                colors.Add(linear);
            }
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        /// <summary>Adds a triangle facing away from <paramref name="inside"/> whatever its input winding.</summary>
        public void AddTriangleOutward(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) AddTriangle(a, c, b, color);
            else AddTriangle(a, b, c, color);
        }

        /// <summary>Two triangles facing away from <paramref name="inside"/>; each gets its own colour jitter.</summary>
        public void AddQuadOutward(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float colorJitter, Vector3 inside)
        {
            AddTriangleOutward(a, b, c, Jitter(color, colorJitter), inside);
            AddTriangleOutward(a, c, d, Jitter(color, colorJitter), inside);
        }

        // ------------------------------------------------------------------ boxes

        /// <summary>
        /// A hexahedron from 8 corners: 0-3 are the bottom ring (x0z0, x1z0, x1z1, x0z1), 4-7 the top ring
        /// above them. Corners may be moved freely for tilted tops and irregular stones.
        /// </summary>
        public void AddHexahedron(Vector3[] c, Color color, float colorJitter = 0.06f)
        {
            Vector3 inside = Vector3.zero;
            for (int i = 0; i < 8; i++) inside += c[i];
            inside /= 8f;
            AddQuadOutward(c[0], c[1], c[2], c[3], color, colorJitter, inside);
            AddQuadOutward(c[4], c[5], c[6], c[7], color, colorJitter, inside);
            AddQuadOutward(c[0], c[1], c[5], c[4], color, colorJitter, inside);
            AddQuadOutward(c[1], c[2], c[6], c[5], color, colorJitter, inside);
            AddQuadOutward(c[2], c[3], c[7], c[6], color, colorJitter, inside);
            AddQuadOutward(c[3], c[0], c[4], c[7], color, colorJitter, inside);
        }

        public void AddBox(Vector3 min, Vector3 size, Color color, float colorJitter = 0.04f)
        {
            Vector3 m = min, M = min + size;
            AddHexahedron(new[]
            {
                new Vector3(m.x, m.y, m.z), new Vector3(M.x, m.y, m.z), new Vector3(M.x, m.y, M.z), new Vector3(m.x, m.y, M.z),
                new Vector3(m.x, M.y, m.z), new Vector3(M.x, M.y, m.z), new Vector3(M.x, M.y, M.z), new Vector3(m.x, M.y, M.z),
            }, color, colorJitter);
        }

        // ------------------------------------------------------------------ tubes (trunks, limbs, cones)

        /// <summary>
        /// A faceted tube along a polyline of ring centres. A radius of ~0 makes that end a point, so a
        /// tube that tapers to zero is a spike. <paramref name="squash"/> scales the ring's local X and Z
        /// (for elliptical torsos); <paramref name="twistPerRing"/> rotates successive rings (degrees).
        /// </summary>
        public void AddTube(IReadOnlyList<Vector3> centers, IReadOnlyList<float> radii, int sides, Color color,
            float colorJitter = 0.08f, float radiusJitter = 0f, float twistPerRing = 0f, Vector2? squash = null,
            bool capStart = true, bool capEnd = true)
        {
            int n = centers.Count;
            if (n < 2) return;
            Vector2 sq = squash ?? Vector2.one;
            var rings = new Vector3[n][];
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = i == 0 ? centers[1] - centers[0]
                            : i == n - 1 ? centers[n - 1] - centers[n - 2]
                            : centers[i + 1] - centers[i - 1];
                dir.Normalize();
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.AngleAxis(twistPerRing * i, Vector3.up);
                rings[i] = new Vector3[sides];
                for (int s = 0; s < sides; s++)
                {
                    if (radii[i] <= 1e-4f)
                    {
                        rings[i][s] = centers[i];
                        continue;
                    }
                    float a = s * Mathf.PI * 2f / sides;
                    float r = radii[i] * (1f + (Next() * 2f - 1f) * radiusJitter);
                    rings[i][s] = centers[i] + rot * new Vector3(Mathf.Cos(a) * sq.x * r, 0f, Mathf.Sin(a) * sq.y * r);
                }
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 inside = (centers[i] + centers[i + 1]) * 0.5f;
                bool tipA = radii[i] <= 1e-4f, tipB = radii[i + 1] <= 1e-4f;
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    Vector3 p00 = rings[i][s], p01 = rings[i][s1], p10 = rings[i + 1][s], p11 = rings[i + 1][s1];
                    if (tipA && tipB) continue;
                    if (tipB) AddTriangleOutward(p00, p10, p01, Jitter(color, colorJitter), inside);
                    else if (tipA) AddTriangleOutward(p00, p10, p11, Jitter(color, colorJitter), inside);
                    else AddQuadOutward(p00, p10, p11, p01, color, colorJitter, inside);
                }
            }

            if (capStart && radii[0] > 1e-4f) AddCap(rings[0], centers[0], centers[1], color, colorJitter);
            if (capEnd && radii[n - 1] > 1e-4f) AddCap(rings[n - 1], centers[n - 1], centers[n - 2], color, colorJitter);
        }

        void AddCap(Vector3[] ring, Vector3 center, Vector3 inside, Color color, float colorJitter)
        {
            for (int s = 0; s < ring.Length; s++)
                AddTriangleOutward(center, ring[s], ring[(s + 1) % ring.Length], Jitter(color, colorJitter), inside);
        }

        /// <summary>A two-ring tube: a prism, frustum or (with a zero top radius) a cone.</summary>
        public void AddFrustum(Vector3 bottom, Vector3 top, float bottomRadius, float topRadius, int sides, Color color,
            float colorJitter = 0.08f, float radiusJitter = 0f, bool capBottom = true, bool capTop = true, Vector2? squash = null)
        {
            AddTube(new[] { bottom, top }, new[] { bottomRadius, topRadius }, sides, color, colorJitter, radiusJitter, 0f, squash, capBottom, capTop);
        }

        public void AddCone(Vector3 baseCenter, Vector3 tip, float radius, int sides, Color color, float colorJitter = 0.08f, bool capBase = true)
        {
            AddFrustum(baseCenter, tip, radius, 0f, sides, color, colorJitter, 0f, capBase, false);
        }

        // ------------------------------------------------------------------ blobs (rocks, heads, stones)

        static readonly Vector3[] IcoVertices = BuildIcoVertices();
        static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        static Vector3[] BuildIcoVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++) v[i].Normalize();
            return v;
        }

        /// <summary>
        /// A jittered, subdivided icosahedron scaled by <paramref name="radii"/>. The colour function gets
        /// each face's centroid divided by the radii (so y is about -1..1), for height-based banding.
        /// <paramref name="flatBottom"/> clamps local y to zero for a rock that sits on the ground.
        /// </summary>
        public void AddBlob(Vector3 center, Vector3 radii, int subdivisions, float jitter, Func<Vector3, Color> colorFn,
            bool flatBottom = false, Quaternion? rotation = null)
        {
            var verts = new List<Vector3>(IcoVertices);
            var faces = new List<int>(IcoFaces);
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>(faces.Count * 4);
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Midpoint(verts, cache, a, b), bc = Midpoint(verts, cache, b, c), ca = Midpoint(verts, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }

            Quaternion rot = rotation ?? Quaternion.identity;
            var pos = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i] * (1f + (Next() * 2f - 1f) * jitter);
                p = Vector3.Scale(p, radii);
                if (flatBottom) p.y = Mathf.Max(p.y, 0f);
                pos[i] = center + rot * p;
            }

            Vector3 inside = center + rot * (flatBottom ? new Vector3(0f, radii.y * 0.3f, 0f) : Vector3.zero);
            for (int f = 0; f < faces.Count; f += 3)
            {
                Vector3 a = pos[faces[f]], b = pos[faces[f + 1]], c = pos[faces[f + 2]];
                Vector3 local = Quaternion.Inverse(rot) * ((a + b + c) / 3f - center);
                local = new Vector3(local.x / Mathf.Max(radii.x, 1e-4f), local.y / Mathf.Max(radii.y, 1e-4f), local.z / Mathf.Max(radii.z, 1e-4f));
                AddTriangleOutward(a, b, c, colorFn(local), inside);
            }
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            index = verts.Count - 1;
            cache.Add(key, index);
            return index;
        }

        // ------------------------------------------------------------------ ground

        /// <summary>
        /// A jittered triangulated grid covering [-halfExtent, halfExtent] on X and Z. Interior vertices are
        /// nudged in XZ and Y so the surface reads as hand-faceted terrain; <paramref name="jitterScale"/>
        /// (0..1) turns the nudging off where the ground must stay flat and regular.
        /// </summary>
        public void AddFacetedGround(float halfExtent, float cell, Func<float, float, float> height, Func<float, float, Color> color,
            Func<float, float, float> jitterScale, float jitterXZ, float jitterY)
        {
            int n = Mathf.CeilToInt(2f * halfExtent / cell);
            float step = 2f * halfExtent / n;
            var grid = new Vector3[(n + 1) * (n + 1)];
            for (int j = 0; j <= n; j++)
            {
                for (int i = 0; i <= n; i++)
                {
                    float x = -halfExtent + i * step, z = -halfExtent + j * step;
                    float k = jitterScale(x, z);
                    bool edge = i == 0 || j == 0 || i == n || j == n;
                    float jx = edge ? 0f : (Next() * 2f - 1f) * jitterXZ * step * k;
                    float jz = edge ? 0f : (Next() * 2f - 1f) * jitterXZ * step * k;
                    float y = height(x + jx, z + jz) + (Next() * 2f - 1f) * jitterY * k;
                    grid[j * (n + 1) + i] = new Vector3(x + jx, y, z + jz);
                }
            }

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 p00 = grid[j * (n + 1) + i], p10 = grid[j * (n + 1) + i + 1];
                    Vector3 p01 = grid[(j + 1) * (n + 1) + i], p11 = grid[(j + 1) * (n + 1) + i + 1];
                    if (((i + j) & 1) == 0)
                    {
                        AddGroundTriangle(p00, p01, p11, color);
                        AddGroundTriangle(p00, p11, p10, color);
                    }
                    else
                    {
                        AddGroundTriangle(p00, p01, p10, color);
                        AddGroundTriangle(p10, p01, p11, color);
                    }
                }
            }
        }

        /// <summary>
        /// A terrain grid covering [-halfExtent, halfExtent] in <paramref name="cells"/> cells per side. Vertex (i, j) is
        /// nudged in XZ by a hash of its index (so separately built patches share their border vertices exactly) and
        /// sits at <c>height</c> of its nudged position, so the mesh matches the height function at every vertex.
        /// </summary>
        public readonly struct TerrainGrid
        {
            public readonly int Cells;
            public readonly float HalfExtent, Step, JitterXZ;
            readonly Func<float, float, float> height, jitterScale;
            readonly int salt;

            public TerrainGrid(float halfExtent, float cell, Func<float, float, float> height, Func<float, float, float> jitterScale, float jitterXZ, int salt)
            {
                Cells = Mathf.CeilToInt(2f * halfExtent / cell);
                HalfExtent = halfExtent;
                Step = 2f * halfExtent / Cells;
                JitterXZ = jitterXZ;
                this.height = height;
                this.jitterScale = jitterScale;
                this.salt = salt;
            }

            public Vector3 Vertex(int i, int j)
            {
                float x = -HalfExtent + i * Step, z = -HalfExtent + j * Step;
                if (i > 0 && j > 0 && i < Cells && j < Cells)
                {
                    float k = jitterScale(x, z) * JitterXZ * Step;
                    x += (Hash01(i, j, salt) * 2f - 1f) * k;
                    z += (Hash01(i, j, salt + 7919) * 2f - 1f) * k;
                }
                return new Vector3(x, height(x, z), z);
            }

            /// <summary>The cell's two triangles, diagonals alternating in a checkerboard.</summary>
            public void Cell(int i, int j, out Vector3 a0, out Vector3 b0, out Vector3 c0, out Vector3 a1, out Vector3 b1, out Vector3 c1)
            {
                Vector3 p00 = Vertex(i, j), p10 = Vertex(i + 1, j), p01 = Vertex(i, j + 1), p11 = Vertex(i + 1, j + 1);
                if (((i + j) & 1) == 0) { a0 = p00; b0 = p01; c0 = p11; a1 = p00; b1 = p11; c1 = p10; }
                else { a0 = p00; b0 = p01; c0 = p10; a1 = p10; b1 = p01; c1 = p11; }
            }

            static float Hash01(int i, int j, int s)
            {
                unchecked
                {
                    uint h = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)(s * 83492791);
                    h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                    return (h & 0xFFFFFF) / (float)0x1000000;
                }
            }

            /// <summary>A shared-vertex mesh of cells [i0, i1) x [j0, j1), for a MeshCollider (no flat shading needed).</summary>
            public Mesh CollisionMesh(int i0, int i1, int j0, int j1, string name)
            {
                int w = i1 - i0 + 1;
                var verts = new Vector3[w * (j1 - j0 + 1)];
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++) verts[(j - j0) * w + (i - i0)] = Vertex(i, j);
                var tris = new List<int>((i1 - i0) * (j1 - j0) * 6);
                for (int j = j0; j < j1; j++)
                {
                    for (int i = i0; i < i1; i++)
                    {
                        int v00 = (j - j0) * w + (i - i0), v10 = v00 + 1, v01 = v00 + w, v11 = v01 + 1;
                        if (((i + j) & 1) == 0) tris.AddRange(new[] { v00, v01, v11, v00, v11, v10 });
                        else tris.AddRange(new[] { v00, v01, v10, v10, v01, v11 });
                    }
                }
                var mesh = new Mesh { name = name, indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.vertices = verts;
                mesh.triangles = tris.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>Flat-shaded triangles for cells [i0, i1) x [j0, j1) of <paramref name="grid"/>.</summary>
        public void AddTerrainPatch(in TerrainGrid grid, int i0, int i1, int j0, int j1, Func<float, float, Color> color)
        {
            for (int j = j0; j < j1; j++)
            {
                for (int i = i0; i < i1; i++)
                {
                    grid.Cell(i, j, out Vector3 a0, out Vector3 b0, out Vector3 c0, out Vector3 a1, out Vector3 b1, out Vector3 c1);
                    AddGroundTriangle(a0, b0, c0, color);
                    AddGroundTriangle(a1, b1, c1, color);
                }
            }
        }

        void AddGroundTriangle(Vector3 a, Vector3 b, Vector3 c, Func<float, float, Color> color)
        {
            Vector3 centroid = (a + b + c) / 3f;
            AddTriangleOutward(a, b, c, color(centroid.x, centroid.z), centroid - Vector3.up);
        }

        // ------------------------------------------------------------------ output

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
