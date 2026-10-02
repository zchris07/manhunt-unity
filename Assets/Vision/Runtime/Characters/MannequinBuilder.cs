using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vision.Characters
{
    /// <summary>
    /// A plain low-poly mannequin (no clothing, hair, face or equipment) of at most
    /// <see cref="MaxTriangles"/> triangles, skinned to the <see cref="HumanoidSkeleton"/> so the
    /// procedural walk and sprint drive it unchanged. Built from rings joined by quads: an 8-sided
    /// torso, a 6-sided head with a ridge down the face, 4-sided boxy limbs, mitten hands and wedge
    /// feet. Every triangle has its own vertices and face normal (flat shading), one dark grey colour.
    /// Joint rings are weighted half to each bone so knees, elbows and wrists bend without tearing.
    /// </summary>
    public static class MannequinBuilder
    {
        public const int MaxTriangles = 200;

        /// <summary>sRGB dark grey.</summary>
        public static readonly Color Grey = new Color(0.30f, 0.30f, 0.31f);

        struct Point
        {
            public Vector3 Position;
            public BoneWeight Weight;
        }

        sealed class Builder
        {
            public readonly List<Point> Points = new List<Point>(256);
            public readonly List<int> Triangles = new List<int>(MaxTriangles * 3);

            public int Add(Vector3 p, BoneWeight w)
            {
                Points.Add(new Point { Position = p, Weight = w });
                return Points.Count - 1;
            }

            /// <summary>Adds a triangle facing away from <paramref name="inside"/>.</summary>
            public void Tri(int a, int b, int c, Vector3 inside)
            {
                Vector3 pa = Points[a].Position, pb = Points[b].Position, pc = Points[c].Position;
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                if (n.sqrMagnitude < 1e-12f) return;
                bool flip = Vector3.Dot(n, (pa + pb + pc) / 3f - inside) < 0f;
                Triangles.Add(a);
                Triangles.Add(flip ? c : b);
                Triangles.Add(flip ? b : c);
            }

            public void Quad(int a, int b, int c, int d, Vector3 inside)
            {
                Tri(a, b, c, inside);
                Tri(a, c, d, inside);
            }

            /// <summary>
            /// A ring of <paramref name="sides"/> points around <paramref name="center"/>, perpendicular to
            /// <paramref name="axis"/>. Local X is the body's right, local Z its front; <paramref name="front"/>
            /// and <paramref name="back"/> are the half-depths in front of and behind the centre.
            /// <paramref name="phaseDeg"/> 0 puts a point at the front; 45 on a 4-sided ring gives flat faces.
            /// </summary>
            public int[] Ring(Vector3 center, Vector3 axis, int sides, float halfWidth, float front, float back, float phaseDeg, BoneWeight w)
            {
                // Frame from the ring's plane only (not the axis sign), so local X stays the body's right and
                // local Z its front whether a tube runs up (torso) or down (limbs).
                Vector3 n = axis.y < 0f ? -axis.normalized : axis.normalized;
                Vector3 fz = (Vector3.forward - n * Vector3.Dot(Vector3.forward, n)).normalized;
                Vector3 fx = Vector3.Cross(n, fz);
                var ring = new int[sides];
                // For flat-faced rings, push the corners out so the faces sit at the given half-sizes.
                float corner = sides == 4 && Mathf.Approximately(phaseDeg, 45f) ? Mathf.Sqrt(2f) : 1f;
                for (int s = 0; s < sides; s++)
                {
                    float a = (phaseDeg + s * 360f / sides) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(a), z = Mathf.Cos(a);
                    var local = new Vector3(x * halfWidth * corner, 0f, z * (z >= 0f ? front : back) * corner);
                    ring[s] = Add(center + fx * local.x + fz * local.z, w);
                }
                return ring;
            }

            public void Join(int[] a, int[] b, Vector3 inside)
            {
                for (int s = 0; s < a.Length; s++)
                {
                    int s1 = (s + 1) % a.Length;
                    Quad(a[s], b[s], b[s1], a[s1], inside);
                }
            }

            /// <summary>Closes a ring with a fan to an apex point.</summary>
            public void Fan(int[] ring, int apex, Vector3 inside)
            {
                for (int s = 0; s < ring.Length; s++) Tri(apex, ring[s], ring[(s + 1) % ring.Length], inside);
            }
        }

        static BoneWeight W(Bone a) => new BoneWeight { boneIndex0 = (int)a, weight0 = 1f };

        static BoneWeight W(Bone a, Bone b, float wb = 0.5f) =>
            new BoneWeight { boneIndex0 = (int)a, weight0 = 1f - wb, boneIndex1 = (int)b, weight1 = wb };

        static Vector3 J(Bone b) => HumanoidSkeleton.BindPosition(b);

        static Builder Construct()
        {
            var m = new Builder();
            Vector3 up = Vector3.up;

            // Torso: hips, waist, chest, shoulders, then a fan up to the base of the neck (trapezius slope).
            int[] hips = m.Ring(new Vector3(0f, 0.88f, 0f), up, 8, 0.165f, 0.10f, 0.11f, 0f, W(Bone.Pelvis));
            int[] waist = m.Ring(new Vector3(0f, 1.07f, 0f), up, 8, 0.140f, 0.095f, 0.085f, 0f, W(Bone.Pelvis, Bone.Spine));
            int[] chest = m.Ring(new Vector3(0f, 1.30f, -0.005f), up, 8, 0.170f, 0.12f, 0.10f, 0f, W(Bone.Spine, Bone.Chest, 0.7f));
            int[] shoulders = m.Ring(new Vector3(0f, 1.45f, -0.012f), up, 8, 0.205f, 0.075f, 0.085f, 0f, W(Bone.Chest));
            int neckBase = m.Add(new Vector3(0f, 1.505f, -0.015f), W(Bone.Chest));
            m.Join(hips, waist, new Vector3(0f, 0.97f, 0f));
            m.Join(waist, chest, new Vector3(0f, 1.18f, 0f));
            m.Join(chest, shoulders, new Vector3(0f, 1.37f, 0f));
            m.Fan(shoulders, neckBase, new Vector3(0f, 1.35f, 0f));

            // Neck: open tube, its ends buried in the shoulders and the head.
            int[] neckLow = m.Ring(new Vector3(0f, 1.44f, -0.018f), up, 4, 0.052f, 0.052f, 0.052f, 45f, W(Bone.Chest, Bone.Neck));
            int[] neckHigh = m.Ring(new Vector3(0f, 1.61f, 0f), up, 4, 0.047f, 0.047f, 0.047f, 45f, W(Bone.Neck, Bone.Head));
            m.Join(neckLow, neckHigh, new Vector3(0f, 1.52f, -0.01f));

            // Head: chin, cheekbones, crown and apex; a point at the front of each ring makes a ridge down the face.
            var headCenter = new Vector3(0f, 1.69f, 0.005f);
            int[] chin = m.Ring(new Vector3(0f, 1.585f, 0.02f), up, 6, 0.045f, 0.055f, 0.04f, 0f, W(Bone.Head));
            int[] cheeks = m.Ring(new Vector3(0f, 1.685f, 0.005f), up, 6, 0.078f, 0.095f, 0.09f, 0f, W(Bone.Head));
            int[] crown = m.Ring(new Vector3(0f, 1.77f, -0.005f), up, 6, 0.07f, 0.075f, 0.085f, 0f, W(Bone.Head));
            int apex = m.Add(new Vector3(0f, 1.80f, -0.01f), W(Bone.Head));
            m.Join(chin, cheeks, headCenter);
            m.Join(cheeks, crown, headCenter);
            m.Fan(crown, apex, headCenter);

            foreach (int side in new[] { -1, 1 })
            {
                bool left = side < 0;
                Bone upper = left ? Bone.UpperArmL : Bone.UpperArmR, fore = upper + 1, hand = upper + 2;
                Bone thigh = left ? Bone.ThighL : Bone.ThighR, shin = thigh + 1, foot = thigh + 2, toe = thigh + 3;

                // Arm: shoulder, elbow, wrist; then a mitten hand (flat, palm facing the thigh) to a fingertip point.
                Vector3 sh = J(upper), el = J(fore), wr = J(hand);
                var knuckle = wr + new Vector3(side * 0.004f, -0.095f, 0.004f);
                var tip = wr + new Vector3(side * 0.006f, -0.185f, 0.012f);
                int[] a0 = m.Ring(sh, el - sh, 4, 0.055f, 0.052f, 0.052f, 45f, W(upper));
                int[] a1 = m.Ring(el, wr - sh, 4, 0.042f, 0.042f, 0.042f, 45f, W(upper, fore));
                int[] a2 = m.Ring(wr, wr - el, 4, 0.030f, 0.032f, 0.032f, 45f, W(fore, hand));
                int[] a3 = m.Ring(knuckle, knuckle - wr, 4, 0.016f, 0.045f, 0.042f, 45f, W(hand));
                int fingertip = m.Add(tip, W(hand));
                m.Join(a0, a1, (sh + el) * 0.5f);
                m.Join(a1, a2, (el + wr) * 0.5f);
                m.Join(a2, a3, (wr + knuckle) * 0.5f);
                m.Fan(a3, fingertip, knuckle);

                // Leg: top buried in the hips, knee, ankle buried in the foot.
                Vector3 hip = J(thigh), knee = J(shin);
                var hipTop = new Vector3(hip.x, 0.97f, 0f);
                var ankle = new Vector3(hip.x, 0.075f, 0f);
                int[] l0 = m.Ring(hipTop, knee - hipTop, 4, 0.075f, 0.075f, 0.075f, 45f, W(thigh));
                int[] l1 = m.Ring(knee, ankle - hipTop, 4, 0.052f, 0.054f, 0.05f, 45f, W(thigh, shin));
                int[] l2 = m.Ring(ankle, ankle - knee, 4, 0.036f, 0.04f, 0.04f, 45f, W(shin, foot));
                m.Join(l0, l1, (hipTop + knee) * 0.5f);
                m.Join(l1, l2, (knee + ankle) * 0.5f);

                // Foot: a wedge (triangular prism) from heel to toe; the front edge follows the toe bone.
                float x = hip.x;
                int heelL = m.Add(new Vector3(x - 0.042f, 0f, -0.075f), W(foot));
                int heelR = m.Add(new Vector3(x + 0.042f, 0f, -0.075f), W(foot));
                int toeL = m.Add(new Vector3(x - 0.052f, 0f, 0.205f), W(toe));
                int toeR = m.Add(new Vector3(x + 0.052f, 0f, 0.205f), W(toe));
                int topL = m.Add(new Vector3(x - 0.036f, 0.115f, -0.04f), W(foot));
                int topR = m.Add(new Vector3(x + 0.036f, 0.115f, -0.04f), W(foot));
                var footInside = new Vector3(x, 0.04f, 0.02f);
                m.Quad(heelL, heelR, toeR, toeL, footInside);
                m.Quad(toeL, toeR, topR, topL, footInside);
                m.Quad(topL, topR, heelR, heelL, footInside);
                m.Tri(heelL, toeL, topL, footInside);
                m.Tri(heelR, toeR, topR, footInside);
            }
            return m;
        }

        /// <summary>Triangle count and total surface area (design units²) of the mannequin.</summary>
        public static void Measure(out int triangles, out float area)
        {
            Builder m = Construct();
            triangles = m.Triangles.Count / 3;
            area = 0f;
            for (int i = 0; i < m.Triangles.Count; i += 3)
            {
                Vector3 a = m.Points[m.Triangles[i]].Position, b = m.Points[m.Triangles[i + 1]].Position, c = m.Points[m.Triangles[i + 2]].Position;
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
        }

        /// <summary>Builds the skinned mesh (bind poses for <see cref="HumanoidSkeleton"/>).</summary>
        public static Mesh Build()
        {
            Builder m = Construct();
            int n = m.Triangles.Count;
            var vertices = new Vector3[n];
            var normals = new Vector3[n];
            var colors = new Color[n];
            var weights = new BoneWeight[n];
            var indices = new int[n];
            Color linear = Grey.linear;
            for (int i = 0; i < n; i += 3)
            {
                Point a = m.Points[m.Triangles[i]], b = m.Points[m.Triangles[i + 1]], c = m.Points[m.Triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position).normalized;
                vertices[i] = a.Position; vertices[i + 1] = b.Position; vertices[i + 2] = c.Position;
                weights[i] = a.Weight; weights[i + 1] = b.Weight; weights[i + 2] = c.Weight;
                for (int k = 0; k < 3; k++)
                {
                    normals[i + k] = normal;
                    colors[i + k] = linear;
                    indices[i + k] = i + k;
                }
            }

            var mesh = new Mesh { name = "Mannequin", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.boneWeights = weights;
            mesh.bindposes = HumanoidSkeleton.BindPoses();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
