using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// A ruined brick wall: courses of weathered red brick laid staggered on a mortar core, broken off along a ragged top
    /// that falls away toward one end (or both), with gaps where bricks have fallen out and a few of them lying at its foot.
    /// Origin at the bottom centre of the wall, its length along X, its thickness along Z.
    /// </summary>
    public static partial class LowPolyModels
    {
        static readonly Color Brick = new Color(0.50f, 0.24f, 0.17f), BrickDark = new Color(0.36f, 0.17f, 0.13f),
            BrickPale = new Color(0.62f, 0.40f, 0.30f), Mortar = new Color(0.52f, 0.50f, 0.45f);

        /// <summary>A course of brick (its height) and a brick's length, chunky enough for the low-poly look.</summary>
        public const float BrickCourse = 0.22f, BrickLength = 0.5f;

        /// <summary>Which end of a ruin is broken away: the left (-X), the right (+X) or both.</summary>
        public enum RuinBreak { Left, Right, Both }

        /// <summary>
        /// A stretch of ruined brick wall <paramref name="length"/> long and <paramref name="height"/> high where it still
        /// stands whole, crumbling away toward its broken end(s) to a stub a fifth to a third as high.
        /// </summary>
        public static Mesh BrickRuin(System.Random rng, float length, float height, float thickness, RuinBreak broken)
        {
            var b = new LowPolyMeshBuilder(rng);
            float halfT = thickness * 0.5f, left = -length * 0.5f, right = length * 0.5f;
            int courses = Mathf.Max(2, Mathf.RoundToInt(height / BrickCourse));
            float course = height / courses;
            // How far in from each broken end the wall starts to fall away, and how low it gets at the end.
            bool leftBroken = broken != RuinBreak.Right, rightBroken = broken != RuinBreak.Left;
            float share = broken == RuinBreak.Both ? 0.4f : 0.65f;
            float leftRun = length * b.Range(share * 0.6f, share), rightRun = length * b.Range(share * 0.6f, share);
            float leftLow = height * b.Range(0.15f, 0.35f), rightLow = height * b.Range(0.15f, 0.35f);
            // Where a course at height y gives out, toward a broken end (raggedly, a brick or so either way).
            float Edge(float y, float end, float run, float low, float toward)
            {
                if (y <= low) return end;
                float back = run * Mathf.Clamp01((y - low) / Mathf.Max(0.01f, height - low));
                return end - toward * (back + b.Range(-0.6f, 0.6f) * BrickLength);
            }

            for (int c = 0; c < courses; c++)
            {
                float y0 = c * course, y1 = y0 + course, mid = (y0 + y1) * 0.5f;
                float x0 = leftBroken ? Edge(mid, left, leftRun, leftLow, -1f) : left;
                float x1 = rightBroken ? Edge(mid, right, rightRun, rightLow, 1f) : right;
                x0 = Mathf.Clamp(x0, left, right);
                x1 = Mathf.Clamp(x1, left, right);
                if (x1 - x0 < BrickLength * 0.6f) continue;
                // The mortar core the bricks are laid on (what shows where one has fallen out).
                b.AddBox(new Vector3(x0 + 0.02f, y0, -halfT * 0.78f), new Vector3(x1 - x0 - 0.04f, course * 0.98f, halfT * 1.56f), Mortar, 0.06f);
                bool top = c >= courses - 2;
                float x = x0 - (c % 2) * BrickLength * 0.5f;
                while (x < x1 - 0.02f)
                {
                    float w = BrickLength * b.Range(0.9f, 1.1f);
                    float bx0 = Mathf.Max(x, x0), bx1 = Mathf.Min(x + w, x1);
                    x += w;
                    if (bx1 - bx0 < 0.1f) continue;
                    // Fallen out: now and then lower down, often along the crumbling top.
                    if (c >= 2 && b.Next() < (top ? 0.22f : 0.06f)) continue;
                    float roll = b.Next();
                    Color col = roll < 0.6f ? Brick : roll < 0.85f ? BrickDark : BrickPale;
                    float z = halfT * b.Range(0.9f, 1f);
                    const float e = 0.012f;
                    Vector3 J() => new Vector3(b.Range(-e, e), b.Range(-e, e), b.Range(-e, e));
                    b.AddHexahedron(new[]
                    {
                        new Vector3(bx0 + 0.008f, y0, -z) + J(), new Vector3(bx1 - 0.008f, y0, -z) + J(), new Vector3(bx1 - 0.008f, y0, z) + J(), new Vector3(bx0 + 0.008f, y0, z) + J(),
                        new Vector3(bx0 + 0.008f, y1 - 0.012f, -z) + J(), new Vector3(bx1 - 0.008f, y1 - 0.012f, -z) + J(), new Vector3(bx1 - 0.008f, y1 - 0.012f, z) + J(), new Vector3(bx0 + 0.008f, y1 - 0.012f, z) + J(),
                    }, col, 0.1f);
                }
            }

            // Bricks fallen at its foot, either side, more by the broken ends.
            int fallen = 4 + rng.Next(5);
            for (int i = 0; i < fallen; i++)
            {
                float along = b.Next() < 0.6f
                    ? (b.Next() < 0.5f && leftBroken || !rightBroken ? left + b.Range(0f, leftRun) : right - b.Range(0f, rightRun))
                    : b.Range(left, right);
                float side = (b.Next() < 0.5f ? -1f : 1f) * (halfT + b.Range(0.12f, 0.9f));
                var at = new Vector3(along, BrickCourse * 0.25f, side);
                var rot = Quaternion.Euler(b.Range(-25f, 25f), b.Range(0f, 360f), b.Range(-15f, 15f));
                Vector3 size = new Vector3(BrickLength * (b.Next() < 0.35f ? 0.5f : 1f), BrickCourse * 0.9f, thickness * 0.5f);
                Part(b, at, rot, () => b.AddBox(-size * 0.5f, size, b.Next() < 0.6f ? Brick : BrickDark, 0.12f));
            }
            return b.ToMesh("Brick Wall Ruin");
        }
    }
}
