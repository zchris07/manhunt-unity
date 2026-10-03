using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Visibility;
using Vision.World;

namespace Vision.Tests
{
    public class WorldContentTests
    {
        static void AssertFacets(Mesh m, PolyBudget.Class c, float lowTolerance = 0.6f)
        {
            float edge = PolyBudget.MeasuredEdge(m), target = PolyBudget.Edge(c);
            Assert.GreaterOrEqual(edge, target * (1f - lowTolerance), $"{m.name}: facets no finer than its class ({edge:0.000} vs {target:0.000})");
        }

        [Test]
        public void Evergreens_ComeInFiveForms_AllTreeSizedAndOnBudget()
        {
            var names = new HashSet<string>();
            for (int style = 0; style < LowPolyModels.ConiferStyles; style++)
            {
                Mesh m = LowPolyModels.Conifer(new System.Random(1200 + style), style);
                names.Add(m.name);
                float minHeight = style == 3 ? 1.9f : 4f;
                Assert.That(m.bounds.max.y, Is.InRange(minHeight, 7.2f), $"{m.name} height");
                Assert.GreaterOrEqual(m.bounds.min.y, -0.01f);
                Assert.Less(m.bounds.size.x, 3.4f, $"{m.name} crown width");
                AssertFacets(m, PolyBudget.Class.Tree);
                Object.DestroyImmediate(m);
            }
            CollectionAssert.AreEquivalent(new[] { "Fir", "Spruce", "Pine", "Dead Pine" }, names, "young firs are firs too");
        }

        [Test]
        public void CarWrecks_FitTheirCollidersAndFootprints()
        {
            for (int kind = 0; kind < PropLibrary.CarVariants; kind++)
            {
                Vector3 size = LowPolyModels.CarSize(kind);
                Mesh m = LowPolyModels.Car(new System.Random(6000 + kind), kind);
                Assert.LessOrEqual(m.bounds.size.x, size.x + 0.15f, $"{m.name} width");
                Assert.LessOrEqual(m.bounds.size.z, size.z + 0.15f, $"{m.name} length");
                Assert.That(m.bounds.max.y, Is.InRange(size.y * 0.75f, size.y + 0.6f), $"{m.name} height (an open hood may stand above the roof)");
                Assert.GreaterOrEqual(m.bounds.min.y, -0.01f, "nothing below the ground");
                AssertFacets(m, PolyBudget.Class.Vehicle, 0.75f);
                GameObject go = PropFactory.CreateCar(m, null, kind);
                Assert.AreEqual(new Vector2(size.x, size.z), go.GetComponent<Occluder>().size);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void Generator_Barrel_AndPlants_AreSmallAndOnTheGround()
        {
            Mesh gen = LowPolyModels.Generator(new System.Random(6100));
            Assert.LessOrEqual(gen.bounds.size.x, LowPolyModels.GeneratorSize.x + 0.4f);
            Assert.GreaterOrEqual(gen.bounds.min.y, -0.01f);
            Mesh barrel = LowPolyModels.BurningBarrel(new System.Random(4004));
            Assert.That(barrel.bounds.max.y, Is.InRange(0.8f, 1.1f));
            Object.DestroyImmediate(gen);
            Object.DestroyImmediate(barrel);

            var b = new LowPolyMeshBuilder(new System.Random(3));
            LowPolyModels.AddBush(b, 0.6f, false);
            LowPolyModels.AddFern(b, 0.5f);
            LowPolyModels.AddTallGrass(b, 0.7f, true);
            LowPolyModels.AddReeds(b, 1.2f);
            LowPolyModels.AddDeadShrub(b, 0.7f);
            LowPolyModels.AddFlowers(b);
            LowPolyModels.AddMushrooms(b);
            Mesh plants = b.ToMesh("plants");
            Assert.Less(plants.bounds.max.y, 1.8f, "ground plants stay below head height");
            Assert.Less(plants.triangles.Length / 3, 900, "cheap enough to scatter hundreds");
            Object.DestroyImmediate(plants);
        }

        [Test]
        public void BuilderTransform_PlacesAPlantInTheWorld()
        {
            var b = new LowPolyMeshBuilder(new System.Random(1)) { Transform = Matrix4x4.TRS(new Vector3(10f, 2f, -5f), Quaternion.identity, Vector3.one * 2f) };
            LowPolyModels.AddMushrooms(b);
            Mesh m = b.ToMesh("m");
            Assert.AreEqual(10f, m.bounds.center.x, 0.5f);
            Assert.GreaterOrEqual(m.bounds.min.y, 1.99f);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Flames_LickAndSway_AndEmbersRiseAndShrink()
        {
            var go = new GameObject("fire");
            var flame = new GameObject("f").transform;
            var ember = new GameObject("e").transform;
            flame.SetParent(go.transform, false);
            ember.SetParent(go.transform, false);
            var anim = go.AddComponent<FlameAnimator>();
            anim.flames = new[] { flame };
            anim.embers = new[] { ember };
            try
            {
                var heights = new HashSet<float>();
                float lowest = float.MaxValue, highest = float.MinValue;
                for (float t = 0f; t < 3f; t += 0.1f)
                {
                    anim.Pose(t);
                    heights.Add(Mathf.Round(flame.localScale.y * 100f));
                    lowest = Mathf.Min(lowest, ember.localPosition.y);
                    highest = Mathf.Max(highest, ember.localPosition.y);
                    Assert.LessOrEqual(ember.localScale.x, 1f + 1e-5f);
                }
                Assert.Greater(heights.Count, 5, "the flame keeps changing height");
                Assert.Greater(highest - lowest, 0.5f, "embers rise");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static TerrainField Hills() => new TerrainField(1337, 40f);

        [Test]
        public void Paths_ConnectEveryPoint_FollowTheLandAndAreDeterministic()
        {
            var points = new List<Vector2> { new Vector2(0f, -5f), new Vector2(-27f, 21f), new Vector2(25f, 27f), new Vector2(29f, -7f), new Vector2(-21f, -25f) };
            TerrainField f = Hills();
            PathNetwork a = PathNetwork.Build(points, f.Height, 37f, _ => false), b = PathNetwork.Build(points, f.Height, 37f, _ => false);
            Assert.AreEqual(points.Count - 1, a.Paths.Count, "a spanning tree");
            Assert.AreEqual(a.Length, b.Length, 1e-4f, "deterministic");
            foreach (Vector2 p in points) Assert.Less(a.Distance(p), 0.5f, $"{p} is on a path");
            // Least-slope routing: the climb along each step is gentler than the hills around.
            float worst = 0f;
            foreach (List<Vector2> path in a.Paths)
                for (int i = 0; i < path.Count - 1; i++)
                {
                    a.Distance(path[i].x, path[i].y, out float h0);
                    a.Distance(path[i + 1].x, path[i + 1].y, out float h1);
                    float len = Vector2.Distance(path[i], path[i + 1]);
                    if (len > 0.2f) worst = Mathf.Max(worst, Mathf.Abs(h1 - h0) / len);
                }
            Assert.Less(worst, Mathf.Tan(25f * Mathf.Deg2Rad), "paths climb at most about 25 degrees");
        }

        [Test]
        public void Paths_FlattenTheGroundAcrossThem()
        {
            TerrainField f = Hills();
            PathNetwork net = PathNetwork.Build(new List<Vector2> { new Vector2(-20f, -20f), new Vector2(20f, 18f) }, f.Height, 37f, _ => false);
            f.SetPaths(net);
            List<Vector2> path = net.Paths[0];
            for (int i = 2; i < path.Count - 2; i += 3)
            {
                Vector2 d = (path[i + 1] - path[i]).normalized;
                var side = new Vector2(-d.y, d.x) * 0.6f;
                Assert.AreEqual(f.Height(path[i].x + side.x, path[i].y + side.y), f.Height(path[i].x - side.x, path[i].y - side.y), 0.05f, "level across the path");
            }
        }

        [Test]
        public void Level_HasTwiceTheTrees_OnlyEvergreensAndDeadTrees_SoftGround_AndClearPaths()
        {
            var root = new GameObject("World");
            var shader = Shader.Find("Vision/LowPoly");
            var mat = new Material(shader);
            try
            {
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();
                Assert.GreaterOrEqual(world.TreeCount, 480, "about twice the 260 trees of before");

                // No borders: the ground colour drifts slowly from point to point.
                float worst = 0f, sum = 0f;
                int n = 0;
                PathNetwork paths = world.Terrain.Paths;
                for (float x = -36f; x <= 36f; x += 1.7f)
                    for (float z = -36f; z <= 36f; z += 1.3f)
                    {
                        if (paths.Distance(new Vector2(x, z)) < 3f || paths.Distance(new Vector2(x + 0.5f, z)) < 3f) continue;
                        Color a = world.GroundColor(x, z), b = world.GroundColor(x + 0.5f, z);
                        float d = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
                        worst = Mathf.Max(worst, d);
                        sum += d;
                        n++;
                    }
                Assert.Less(sum / n, 0.02f, "neighbouring ground half a metre apart is nearly the same colour");
                Assert.Less(worst, 0.15f, "no hard borders");

                Assert.Greater(paths.Paths.Count, 5);
                var names = new Dictionary<string, int>();
                foreach (Transform t in world.transform.Find("Static"))
                {
                    string kind = t.name.Split(' ')[0];
                    names[kind] = names.TryGetValue(kind, out int c) ? c + 1 : 1;
                    if (t.GetComponent<Occluder>() == null || t.GetComponent<Door>() != null || t.name.Contains("Wall")) continue;
                    Vector3 local = world.transform.InverseTransformPoint(t.position);
                    Assert.Greater(paths.Distance(new Vector2(local.x, local.z)), paths.HalfWidth, $"{t.name} stands off the path");
                }
                foreach (string kind in new[] { "Dead", "Fir", "Spruce", "Pine", "Rock", "Sedan", "Van", "Pickup", "Generator", "Burning", "Campfire", "Lantern" })
                    Assert.IsTrue(names.ContainsKey(kind), $"the level has {kind}");
                foreach (string kind in new[] { "Leafy", "Autumn" })
                    Assert.IsFalse(names.ContainsKey(kind), $"no {kind} trees");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
