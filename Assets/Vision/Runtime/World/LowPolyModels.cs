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
    public static partial class LowPolyModels
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

        const PolyBudget.Class TreeClass = PolyBudget.Class.Tree;
        const PolyBudget.Class PropClass = PolyBudget.Class.Prop;

        /// <summary>Lumpy boulder with a flat bottom and height-banded colour.</summary>
        public static Mesh Rock(System.Random rng, float radius)
        {
            var b = new LowPolyMeshBuilder(rng);
            // Big boulders are flatter and longer; colours vary between cool grey, warm sandstone and mossy.
            bool boulder = radius > 1.2f;
            float h = radius * (boulder ? b.Range(0.5f, 0.7f) : b.Range(0.75f, 1.1f));
            var radii = new Vector3(radius * (boulder ? b.Range(1.1f, 1.3f) : 1f), h, radius);
            int tint = b.Rng.Next(3);
            Color light = tint == 1 ? new Color(0.48f, 0.43f, 0.36f) : Palette.Stone;
            Color dark = tint == 1 ? new Color(0.34f, 0.30f, 0.25f) : Palette.StoneDark;
            Color moss = new Color(0.29f, 0.34f, 0.22f);
            b.AddBlob(Vector3.zero, radii, PolyBudget.BlobSubdivisions(radii, PolyBudget.Class.Rock, 3), 0.16f,
                local => b.Jitter(tint == 2 && local.y > 0.6f ? moss : (local.y > 0.35f ? light : dark), 0.12f), true,
                Quaternion.Euler(0f, b.Range(0f, 360f), 0f));
            return b.ToMesh(boulder ? "Boulder" : "Rock");
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

        /// <summary>
        /// A standard office steel door, centred on x = 0 with the hinges at -x: a painted light-grey slab, a brushed kick
        /// plate, a narrow wired-glass vision panel toward the latch side, a lever handle on a rose on both faces, three
        /// hinge knuckles and a closer arm along the top.
        /// </summary>
        public static Mesh MetalDoor(System.Random rng, float width, float height, float thickness)
        {
            var b = new LowPolyMeshBuilder(rng);
            float hw = width * 0.5f, ht = thickness * 0.5f;
            Color slab = new Color(0.60f, 0.62f, 0.63f), kick = new Color(0.70f, 0.71f, 0.72f), glass = new Color(0.20f, 0.26f, 0.30f),
                wire = new Color(0.42f, 0.45f, 0.47f), lever = new Color(0.78f, 0.79f, 0.80f), dark = new Color(0.22f, 0.23f, 0.24f);
            b.AddBox(new Vector3(-hw, 0f, -ht), new Vector3(width - 0.01f, height, thickness), slab, 0.02f);
            foreach (float side in new[] { -1f, 1f })
            {
                float z0 = side > 0 ? ht : -ht - 0.008f;
                // Kick plate along the bottom.
                b.AddBox(new Vector3(-hw + 0.03f, 0.02f, z0), new Vector3(width - 0.07f, 0.24f, 0.008f), kick, 0.015f);
                // The vision panel: dark glass with a lighter wire grid, set in a thin steel bead.
                float gx = hw - 0.12f - 0.16f, gy = height * 0.5f, gw = 0.16f, gh = height * 0.34f;
                b.AddBox(new Vector3(gx - 0.015f, gy - 0.015f, z0), new Vector3(gw + 0.03f, gh + 0.03f, 0.008f), dark, 0.01f);
                b.AddBox(new Vector3(gx, gy, z0 + side * 0.002f), new Vector3(gw, gh, 0.008f), glass, 0.01f);
                for (int k = 1; k < 4; k++)
                    b.AddBox(new Vector3(gx, gy + gh * k / 4f - 0.003f, z0 + side * 0.004f), new Vector3(gw, 0.006f, 0.008f), wire, 0.01f);
                b.AddBox(new Vector3(gx + gw * 0.5f - 0.003f, gy, z0 + side * 0.004f), new Vector3(0.006f, gh, 0.008f), wire, 0.01f);
                // Lever on a rose, pointing toward the hinges.
                float lx = hw - 0.09f, ly = 1.0f;
                b.AddBox(new Vector3(lx - 0.035f, ly - 0.035f, side > 0 ? ht : -ht - 0.012f), new Vector3(0.07f, 0.07f, 0.012f), lever, 0.01f);
                b.AddBox(new Vector3(lx - 0.13f, ly - 0.012f, side > 0 ? ht + 0.012f : -ht - 0.045f), new Vector3(0.15f, 0.024f, 0.033f), lever, 0.01f);
            }
            // Hinge knuckles on the hinge edge, and the closer arm across the top on one face.
            foreach (float f in new[] { 0.12f, 0.5f, 0.88f })
                b.AddBox(new Vector3(-hw - 0.012f, height * f - 0.06f, -0.02f), new Vector3(0.024f, 0.12f, 0.04f), dark, 0.01f);
            b.AddBox(new Vector3(-hw + 0.08f, height - 0.11f, ht), new Vector3(0.34f, 0.06f, 0.05f), dark, 0.01f);
            return b.ToMesh("Metal Door");
        }

        /// <summary>The door's steel frame: two jambs and a head, darker than the slab (x from 0 to the opening's width).</summary>
        public static Mesh MetalDoorFrame(System.Random rng, float width, float height, float depth)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color frame = new Color(0.33f, 0.34f, 0.36f);
            float hd = depth * 0.5f;
            b.AddBox(new Vector3(-0.07f, 0f, -hd), new Vector3(0.08f, height + 0.07f, depth), frame, 0.02f);
            b.AddBox(new Vector3(width - 0.01f, 0f, -hd), new Vector3(0.08f, height + 0.07f, depth), frame, 0.02f);
            b.AddBox(new Vector3(-0.07f, height, -hd), new Vector3(width + 0.14f, 0.07f, depth), frame, 0.02f);
            return b.ToMesh("Metal Door Frame");
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

        /// <summary>Stone ring, crossed logs and an ember bed (flames are separate, animated meshes). Drawn with the glow material.</summary>
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
            // A glowing ember bed; the flames are separate meshes (LowPolyModels.Flames) so they can move.
            var bed = new Vector3(0.2f, 0.06f, 0.2f);
            b.AddBlob(new Vector3(0f, 0.12f, 0f), bed, 0, 0.25f, _ => b.Jitter(Palette.Ember, 0.2f), true);
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
