using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Procedural low-poly models for the sandbox diorama, all built from flat triangles. Each returns a
    /// Mesh with its origin at the bottom centre of the object, in the same sizes the colliders and
    /// occluders in <see cref="SandboxWorld"/> expect. Ring sides, segment counts, subdivisions and
    /// board widths come from <see cref="PolyBudget"/>, so every model shares the player's facet size
    /// (relaxed by form class). Characters are <see cref="Vision.Characters.MannequinBuilder"/>.
    /// </summary>
    public static class LowPolyModels
    {
        public static class Palette
        {
            public static readonly Color Bark = new Color(0.20f, 0.17f, 0.14f);
            public static readonly Color BarkDark = new Color(0.12f, 0.10f, 0.09f);
            public static readonly Color Stone = new Color(0.42f, 0.41f, 0.38f);
            public static readonly Color StoneDark = new Color(0.30f, 0.29f, 0.27f);
            public static readonly Color Plank = new Color(0.36f, 0.27f, 0.19f);
            public static readonly Color PlankDark = new Color(0.22f, 0.16f, 0.11f);
            public static readonly Color Ember = new Color(1.0f, 0.55f, 0.18f);
            public static readonly Color Flame = new Color(1.0f, 0.78f, 0.30f);
            public static readonly Color Ash = new Color(0.25f, 0.23f, 0.21f);
            public static readonly Color Glass = new Color(1.0f, 0.85f, 0.45f);
            public static readonly Color Iron = new Color(0.16f, 0.16f, 0.17f);
            public static readonly Color Crow = new Color(0.06f, 0.06f, 0.07f);
            public static readonly Color Beak = new Color(0.25f, 0.22f, 0.15f);
        }

        // ------------------------------------------------------------------ trees and rocks

        /// <summary>Gnarled, leafless tree: leaning faceted trunk, root wedges and forking spiky branches.</summary>
        const PolyBudget.Class TreeClass = PolyBudget.Class.Tree;
        const PolyBudget.Class PropClass = PolyBudget.Class.Prop;

        public static Mesh DeadTree(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            float height = b.Range(3.6f, 5.8f);
            float leanX = b.Range(-0.07f, 0.07f), leanZ = b.Range(-0.07f, 0.07f);
            int rings = PolyBudget.Segments(height, TreeClass, 3) + 1;

            var centers = new Vector3[rings];
            var radii = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                float y = height * t;
                centers[i] = new Vector3(leanX * y + b.Range(-0.04f, 0.04f) * t, y, leanZ * y + b.Range(-0.04f, 0.04f) * t);
                radii[i] = Mathf.Lerp(0.32f, 0.06f, Mathf.Pow(t, 0.75f));
            }
            b.AddTube(centers, radii, PolyBudget.Sides(0.32f, TreeClass, 5), Palette.Bark, 0.10f, 0.08f, 7f, null, false, true);

            int roots = 4 + b.Rng.Next(2);
            for (int i = 0; i < roots; i++)
            {
                float a = (i + b.Next() * 0.6f) / roots * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.AddTube(new[] { dir * 0.1f + Vector3.up * 0.3f, dir * 0.42f + Vector3.up * 0.1f, dir * 0.8f - Vector3.up * 0.02f },
                          new[] { 0.15f, 0.09f, 0f }, PolyBudget.Sides(0.15f, TreeClass), Palette.BarkDark, 0.1f, 0.1f, 0f, null, false, false);
            }

            int branches = 5 + b.Rng.Next(4);
            for (int i = 0; i < branches; i++)
            {
                float t = b.Range(0.3f, 0.95f);
                float f = t * (rings - 1);
                int i0 = Mathf.Min(Mathf.FloorToInt(f), rings - 2);
                Vector3 start = Vector3.Lerp(centers[i0], centers[i0 + 1], f - i0);
                float trunkRadius = Mathf.Lerp(radii[i0], radii[i0 + 1], f - i0);
                float a = b.Next() * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), b.Range(0.35f, 0.9f), Mathf.Sin(a)).normalized;
                Branch(b, start, dir, b.Range(1.0f, 2.2f), Mathf.Max(0.045f, trunkRadius * 0.5f), 2);
            }
            return b.ToMesh("Dead Tree");
        }

        static void Branch(LowPolyMeshBuilder b, Vector3 start, Vector3 dir, float length, float radius, int depth)
        {
            int points = PolyBudget.Segments(length, TreeClass, 2) + 1;
            var centers = new Vector3[points];
            var radii = new float[points];
            centers[0] = start;
            radii[0] = radius;
            Vector3 p = start;
            float step = length / (points - 1);
            for (int i = 1; i < points; i++)
            {
                dir = (dir + new Vector3(b.Range(-0.5f, 0.5f), b.Range(-0.25f, 0.3f), b.Range(-0.5f, 0.5f)) * 0.7f).normalized;
                p += dir * step;
                centers[i] = p;
                radii[i] = i == points - 1 ? 0f : radius * (1f - i / (float)(points - 1)) * 0.9f;
            }
            b.AddTube(centers, radii, PolyBudget.Sides(radius, TreeClass), Palette.Bark, 0.12f, 0.1f, 0f, null, false, false);

            if (depth > 0 && b.Next() < 0.7f)
            {
                Vector3 side = new Vector3(b.Range(-1f, 1f), b.Range(0.1f, 0.8f), b.Range(-1f, 1f)).normalized;
                Branch(b, centers[1], (dir + side).normalized, length * 0.6f, radius * 0.5f, depth - 1);
            }
        }

        /// <summary>Lumpy boulder with a flat bottom and height-banded colour.</summary>
        public static Mesh Rock(System.Random rng, float radius)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = radius * b.Range(0.75f, 1.1f);
            var radii = new Vector3(radius, h, radius);
            b.AddBlob(Vector3.zero, radii, PolyBudget.BlobSubdivisions(radii, PolyBudget.Class.Rock), 0.16f,
                local => b.Jitter(local.y > 0.35f ? Palette.Stone : Palette.StoneDark, 0.12f), true,
                Quaternion.Euler(0f, b.Range(0f, 360f), 0f));
            return b.ToMesh("Rock");
        }

        // ------------------------------------------------------------------ walls, doors, crates

        /// <summary>Vertical planks with tilted tops and dark beams (length along X, centred).</summary>
        public static Mesh PlankWall(System.Random rng, float length, float height, float thickness)
        {
            var b = new LowPolyMeshBuilder(rng);
            // A board is half a wall facet wide.
            int planks = Mathf.Max(1, Mathf.RoundToInt(length / (PolyBudget.Edge(PolyBudget.Class.Wall) * 0.5f)));
            float w = length / planks;
            float halfT = thickness * 0.5f;
            for (int i = 0; i < planks; i++)
            {
                float x0 = -length * 0.5f + i * w, x1 = x0 + w - 0.015f;
                float h0 = height * b.Range(0.93f, 1f), h1 = height * b.Range(0.93f, 1f);
                float z0 = halfT * b.Range(0.82f, 1f), z1 = halfT * b.Range(0.82f, 1f);
                b.AddHexahedron(new[]
                {
                    new Vector3(x0, 0f, -z0), new Vector3(x1, 0f, -z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1),
                    new Vector3(x0, h0, -z0), new Vector3(x1, h1, -z0), new Vector3(x1, h1, z1), new Vector3(x0, h0, z1),
                }, Palette.Plank, 0.16f);
            }
            float beam = height * 0.1f;
            b.AddBox(new Vector3(-length * 0.5f, height * 0.78f, -halfT - 0.02f), new Vector3(length, beam, thickness + 0.04f), Palette.PlankDark, 0.1f);
            b.AddBox(new Vector3(-length * 0.5f, height * 0.12f, -halfT - 0.02f), new Vector3(length, beam, thickness + 0.04f), Palette.PlankDark, 0.1f);
            return b.ToMesh("Plank Wall");
        }

        /// <summary>Courses of irregular stones (length along X, centred).</summary>
        public static Mesh StoneWall(System.Random rng, float length, float height, float thickness)
        {
            var b = new LowPolyMeshBuilder(rng);
            float edge = PolyBudget.Edge(PolyBudget.Class.Wall);
            int courses = Mathf.Max(2, Mathf.RoundToInt(height / (edge * 0.7f)));
            float courseH = height / courses;
            float halfT = thickness * 0.5f;
            float left = -length * 0.5f, right = length * 0.5f;
            for (int c = 0; c < courses; c++)
            {
                float x = left - (c % 2) * 0.3f;
                int k = 0;
                while (x < right - 0.02f)
                {
                    float w = edge * b.Range(0.68f, 1.29f);
                    float x0 = Mathf.Max(x, left), x1 = Mathf.Min(x + w, right);
                    x += w;
                    if (x1 - x0 < 0.1f) continue;
                    float y0 = c * courseH, y1 = y0 + courseH * b.Range(0.9f, 1.08f);
                    float z = halfT * b.Range(0.86f, 1f);
                    float e = 0.03f;
                    Vector3 J() => new Vector3(b.Range(-e, e), b.Range(-e, e), b.Range(-e, e));
                    b.AddHexahedron(new[]
                    {
                        new Vector3(x0, y0, -z) + J(), new Vector3(x1, y0, -z) + J(), new Vector3(x1, y0, z) + J(), new Vector3(x0, y0, z) + J(),
                        new Vector3(x0, y1, -z) + J(), new Vector3(x1, y1 + b.Range(-0.04f, 0.04f), -z) + J(), new Vector3(x1, y1, z) + J(), new Vector3(x0, y1 + b.Range(-0.04f, 0.04f), z) + J(),
                    }, (c + k++) % 3 == 0 ? Palette.StoneDark : Palette.Stone, 0.15f);
                }
            }
            return b.ToMesh("Stone Wall");
        }

        /// <summary>Door or shutter panel: vertical boards with horizontal braces (width along X, centred).</summary>
        public static Mesh Panel(System.Random rng, float width, float height, float thickness)
        {
            var b = new LowPolyMeshBuilder(rng);
            int boards = Mathf.Max(2, Mathf.RoundToInt(width / (PolyBudget.Edge(PropClass) * 0.75f)));
            float w = width / boards;
            float halfT = thickness * 0.5f;
            for (int i = 0; i < boards; i++)
            {
                float x0 = -width * 0.5f + i * w, x1 = x0 + w - 0.01f;
                float h0 = height * b.Range(0.96f, 1f), h1 = height * b.Range(0.96f, 1f);
                b.AddHexahedron(new[]
                {
                    new Vector3(x0, 0f, -halfT), new Vector3(x1, 0f, -halfT), new Vector3(x1, 0f, halfT), new Vector3(x0, 0f, halfT),
                    new Vector3(x0, h0, -halfT), new Vector3(x1, h1, -halfT), new Vector3(x1, h1, halfT), new Vector3(x0, h0, halfT),
                }, Palette.Plank * 1.1f, 0.14f);
            }
            foreach (float f in new[] { 0.12f, 0.5f, 0.84f })
                b.AddBox(new Vector3(-width * 0.5f, height * f - 0.04f, -halfT - 0.015f), new Vector3(width, 0.08f, thickness + 0.03f), Palette.PlankDark, 0.1f);
            return b.ToMesh("Panel");
        }

        public static Mesh Crate(System.Random rng, float size)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = size * 0.5f;
            b.AddBox(new Vector3(-h, 0f, -h), Vector3.one * size, Palette.Plank, 0.12f);
            const float post = 0.07f;
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                b.AddBox(new Vector3(corner.x * h - post * 0.5f - corner.x * post * 0.2f, -0.005f, corner.y * h - post * 0.5f - corner.y * post * 0.2f),
                         new Vector3(post, size + 0.01f, post), Palette.PlankDark, 0.08f);
            foreach (float f in new[] { 0.16f, 0.84f })
                b.AddBox(new Vector3(-h - 0.012f, size * f - 0.035f, -h - 0.012f), new Vector3(size + 0.024f, 0.07f, size + 0.024f), Palette.PlankDark, 0.08f);
            return b.ToMesh("Crate");
        }

        // ------------------------------------------------------------------ light sources

        /// <summary>Stone ring, crossed logs and flame cones. Drawn with the glow material.</summary>
        public static Mesh Campfire(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            // Stones spaced one and a half prop facets apart around the ring.
            int stones = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * 0.48f / (PolyBudget.Edge(PropClass) * 1.5f)));
            for (int i = 0; i < stones; i++)
            {
                float a = (i + b.Range(-0.2f, 0.2f)) / stones * Mathf.PI * 2f;
                float r = b.Range(0.44f, 0.52f);
                float s = b.Range(0.08f, 0.13f);
                b.AddBlob(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), new Vector3(s, s * 0.75f, s), 0, 0.2f,
                          _ => b.Jitter(b.Next() < 0.5f ? Palette.Stone : Palette.StoneDark, 0.15f), true);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + b.Range(-0.2f, 0.2f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.AddTube(new[] { dir * 0.5f + Vector3.up * 0.07f, dir * 0.12f + Vector3.up * 0.2f },
                          new[] { 0.07f, 0.065f }, PolyBudget.Sides(0.07f, PropClass, 4), i % 2 == 0 ? Palette.Bark : Palette.BarkDark, 0.1f, 0.06f);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = b.Next() * Mathf.PI * 2f, r = i == 0 ? 0f : b.Range(0.06f, 0.14f);
                var baseC = new Vector3(Mathf.Cos(a) * r, 0.18f, Mathf.Sin(a) * r);
                float h = i == 0 ? 0.5f : b.Range(0.22f, 0.4f);
                b.AddCone(baseC, baseC + new Vector3(b.Range(-0.04f, 0.04f), h, b.Range(-0.04f, 0.04f)), i == 0 ? 0.11f : 0.08f, PolyBudget.Sides(i == 0 ? 0.11f : 0.08f, PropClass, 4),
                          i == 0 ? Palette.Flame : Palette.Ember, 0.1f, false);
            }
            return b.ToMesh("Campfire");
        }

        /// <summary>Iron post with an arm and a hanging glass lantern. Drawn with the glow material.</summary>
        public static Mesh LanternPost(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            int post = PolyBudget.Sides(0.07f, PropClass, 4), arm = PolyBudget.Sides(0.035f, PropClass, 3), lamp = PolyBudget.Sides(0.13f, PropClass, 5);
            b.AddFrustum(Vector3.zero, new Vector3(0f, 1.9f, 0f), 0.07f, 0.045f, post, Palette.Iron, 0.06f);
            b.AddFrustum(new Vector3(0f, 1.9f, 0f), new Vector3(0.34f, 1.9f, 0f), 0.035f, 0.03f, arm, Palette.Iron, 0.06f);
            var lantern = new Vector3(0.34f, 1.55f, 0f);
            b.AddFrustum(lantern, lantern + new Vector3(0f, 0.3f, 0f), 0.1f, 0.13f, lamp, Palette.Glass, 0.05f);
            b.AddFrustum(lantern + new Vector3(0f, -0.04f, 0f), lantern, 0.08f, 0.1f, lamp, Palette.Iron, 0.05f);
            b.AddCone(lantern + new Vector3(0f, 0.3f, 0f), lantern + new Vector3(0f, 0.46f, 0f), 0.16f, lamp, Palette.Iron, 0.05f);
            b.AddFrustum(new Vector3(0.34f, 1.9f, 0f), new Vector3(0.34f, 1.76f, 0f), 0.012f, 0.012f, 3, Palette.Iron, 0.05f, 0f, false, false);
            return b.ToMesh("Lantern Post");
        }

        // ------------------------------------------------------------------ animals

        public static Mesh Crow(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color feathers = Palette.Crow;
            var body = new Vector3(0.075f, 0.07f, 0.16f);
            b.AddBlob(new Vector3(0f, 0.11f, 0f), body, PolyBudget.BlobSubdivisions(body, PropClass), 0.08f, _ => b.Jitter(feathers, 0.2f));
            b.AddBlob(new Vector3(0f, 0.17f, 0.15f), new Vector3(0.05f, 0.05f, 0.05f), 0, 0.1f, _ => b.Jitter(feathers, 0.15f));
            b.AddCone(new Vector3(0f, 0.17f, 0.19f), new Vector3(0f, 0.16f, 0.27f), 0.02f, 4, Palette.Beak, 0.05f);
            foreach (float side in new[] { -1f, 1f })
            {
                // A flat wing wedge swept back from the shoulder.
                var a = new Vector3(side * 0.05f, 0.14f, 0.05f);
                var tip = new Vector3(side * 0.28f, 0.1f, -0.1f);
                var rear = new Vector3(side * 0.06f, 0.13f, -0.14f);
                Vector3 inside = new Vector3(0f, 0.11f, 0f);
                b.AddTriangleOutward(a, tip, rear, b.Jitter(feathers * 1.5f, 0.15f), inside + Vector3.down);
                b.AddTriangleOutward(a, tip, rear, b.Jitter(feathers, 0.1f), inside + Vector3.up);
            }
            b.AddFrustum(new Vector3(0f, 0.1f, -0.13f), new Vector3(0f, 0.08f, -0.3f), 0.04f, 0.0f, 3, feathers, 0.1f, 0f, false, false);
            return b.ToMesh("Crow");
        }
    }
}
