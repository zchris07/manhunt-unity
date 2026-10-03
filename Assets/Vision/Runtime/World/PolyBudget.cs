using UnityEngine;
using Vision.Characters;

namespace Vision.World
{
    /// <summary>
    /// One polygon-resolution rule for the whole game, derived from the player.
    ///
    /// The player mannequin is capped at <see cref="MannequinBuilder.MaxTriangles"/> triangles. Its surface
    /// area divided by its triangle count gives the average facet, and from that a reference edge length
    /// <see cref="ReferenceEdge"/> (an equilateral triangle of edge e has area 0.433 e²). The camera is
    /// orthographic, so an edge of a given length covers the same number of pixels anywhere in the level:
    /// the rule is a uniform facet size, relaxed by form class, because big or flat forms read fine with
    /// larger facets while a figure's silhouette needs the finest ones.
    ///
    ///   edge(class)      = ReferenceEdge * factor(class)
    ///   triangles(area)  = area / (0.433 * edge²)
    ///   ring sides       = circumference / edge      (3..12)
    ///   tube segments    = length / (edge * MaxAspect)  (facets may stretch along a straight tube)
    ///   ground cell      = edge(Ground)
    /// </summary>
    public static class PolyBudget
    {
        public enum Class { Character, Prop, Plant, Rock, Vehicle, Tree, Wall, Ground }

        /// <summary>Facet edge multiplier per class (Character = the player itself).</summary>
        public static float Factor(Class c)
        {
            switch (c)
            {
                case Class.Character: return 1f;
                case Class.Prop: return 1.5f;
                case Class.Plant: return 1.75f;
                case Class.Rock: return 2f;
                case Class.Vehicle: return 2.25f;
                case Class.Tree: return 2.5f;
                case Class.Wall: return 4f;
                default: return 5f;    // Ground
            }
        }

        /// <summary>How far a facet may be stretched along a tube relative to its width.</summary>
        public const float MaxAspect = 3f;

        const float EquilateralArea = 0.4330127f;
        static float referenceEdge;

        /// <summary>Average facet edge of the player mannequin, in design units.</summary>
        public static float ReferenceEdge
        {
            get
            {
                if (referenceEdge <= 0f)
                {
                    MannequinBuilder.Measure(out int triangles, out float area);
                    referenceEdge = Mathf.Sqrt(area / (Mathf.Max(1, triangles) * EquilateralArea));
                }
                return referenceEdge;
            }
        }

        public static float Edge(Class c) => ReferenceEdge * Factor(c);

        /// <summary>Triangle budget for a surface of <paramref name="area"/> design units².</summary>
        public static int Triangles(float area, Class c)
        {
            float e = Edge(c);
            return Mathf.Max(1, Mathf.RoundToInt(area / (EquilateralArea * e * e)));
        }

        /// <summary>Number of sides for a ring (tube cross-section) of the given radius.</summary>
        public static int Sides(float radius, Class c, int min = 3, int max = 12) =>
            Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radius / Edge(c)), min, max);

        /// <summary>Number of segments (rings - 1) along a tube of the given length.</summary>
        public static int Segments(float length, Class c, int min = 1) =>
            Mathf.Max(min, Mathf.RoundToInt(length / (Edge(c) * MaxAspect)));

        /// <summary>Icosphere subdivision level (0 = 20 faces, 1 = 80, 2 = 320) closest to the budget of an ellipsoid.</summary>
        public static int BlobSubdivisions(Vector3 radii, Class c, int max = 2)
        {
            // Knud Thomsen's approximation of an ellipsoid's surface area.
            const float p = 1.6075f;
            float ab = Mathf.Pow(radii.x * radii.y, p), bc = Mathf.Pow(radii.y * radii.z, p), ca = Mathf.Pow(radii.z * radii.x, p);
            float area = 4f * Mathf.PI * Mathf.Pow((ab + bc + ca) / 3f, 1f / p);
            float budget = Triangles(area, c);
            int best = 0;
            float bestError = float.MaxValue;
            for (int s = 0, faces = 20; s <= max; s++, faces *= 4)
            {
                float error = Mathf.Abs(Mathf.Log(faces / budget));
                if (error < bestError)
                {
                    bestError = error;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>Average facet edge of an existing mesh (for tests and the bake report).</summary>
        public static float MeasuredEdge(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            float area = 0f;
            for (int i = 0; i < t.Length; i += 3)
                area += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f;
            return Mathf.Sqrt(area / (Mathf.Max(1, t.Length / 3) * EquilateralArea));
        }
    }
}
