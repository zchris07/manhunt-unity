using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vision.Visibility;
using Vision.World;

namespace Vision.Tests
{
    public class LevelBuildTests
    {
        GameObject root;
        Material lowPoly, entity, glow;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Vision/LowPoly");
            Assert.NotNull(shader, "Vision/LowPoly shader must exist");
            lowPoly = new Material(shader);
            entity = new Material(shader);
            glow = new Material(shader);
            root = new GameObject("World");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(lowPoly);
            Object.DestroyImmediate(entity);
            Object.DestroyImmediate(glow);
        }

        SandboxWorld NewWorld(PropLibrary library)
        {
            var world = root.AddComponent<SandboxWorld>();
            world.lowPolyMaterial = lowPoly;
            world.entityMaterial = entity;
            world.glowMaterial = glow;
            world.library = library;
            if (library != null) world.placeHook = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return world;
        }

        static void AssertLevelShape(SandboxWorld world)
        {
            Assert.AreEqual(2, world.Doors.Count, "door and shutter");
            Assert.AreEqual(6, world.Crows.Count);
            Assert.NotNull(world.Player);
            Assert.NotNull(world.Wanderer);
            Assert.AreEqual(4, world.Wanderer.waypoints.Length);
            Assert.AreEqual(9, world.GetComponentsInChildren<VisionLight>().Length, "3 campfires + 6 lanterns");
            Assert.Greater(world.GetComponentsInChildren<Occluder>().Length, 60, "walls, trunks, rocks, crates, doors");
        }

        [Test]
        public void Generate_InEditMode_BuildsTheLevelWithoutALibrary()
        {
            SandboxWorld world = NewWorld(null);
            world.Generate();
            AssertLevelShape(world);
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            SandboxWorld world = NewWorld(null);
            world.Generate();
            int occluders = world.GetComponentsInChildren<Occluder>().Length;
            Vector3 firstTree = world.transform.Find("Static").GetChild(0).position;
            world.Generate();
            Assert.AreEqual(occluders, world.GetComponentsInChildren<Occluder>().Length);
            Assert.AreEqual(firstTree, world.transform.Find("Static").GetChild(0).position);
        }

        [Test]
        public void Generate_Twice_DoesNotDuplicateTheLevel()
        {
            SandboxWorld world = NewWorld(null);
            world.Generate();
            world.Generate();
            Assert.AreEqual(3, root.transform.childCount, "Static, Entities and the player only");
            AssertLevelShape(world);
        }

        [Test]
        public void SavedPropLibrary_IsCompleteAndBuildsTheSameLevel()
        {
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>("Assets/Vision/Prefabs/PropLibrary.asset");
            Assume.That(library, Is.Not.Null, "run Vision/Bake Level and Scene first");
            Assert.IsTrue(library.IsComplete);
            SandboxWorld world = NewWorld(library);
            world.Generate();
            AssertLevelShape(world);
        }

        [Test]
        public void SavedPrefabs_CarryTheirCollidersOccludersAndLights()
        {
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>("Assets/Vision/Prefabs/PropLibrary.asset");
            Assume.That(library, Is.Not.Null, "run Vision/Bake Level and Scene first");
            foreach (GameObject tree in library.trees)
            {
                Assert.NotNull(tree.GetComponent<CapsuleCollider>());
                Assert.AreEqual(PropFactory.TreeTrunkRadius, tree.GetComponent<Occluder>().radius, 1e-4f);
                Assert.NotNull(tree.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsTrue(EditorUtility.IsPersistent(tree.GetComponent<MeshFilter>().sharedMesh), "mesh is a saved asset");
            }
            for (int i = 0; i < library.rocks.Length; i++)
                Assert.AreEqual(PropLibrary.RockRadii[i] * 0.85f, library.rocks[i].GetComponent<Occluder>().radius, 1e-4f);
            for (int i = 0; i < library.crates.Length; i++)
                Assert.AreEqual(Vector2.one * PropLibrary.CrateSizes[i], library.crates[i].GetComponent<Occluder>().size);
            Assert.NotNull(library.campfire.GetComponent<VisionLight>());
            Assert.NotNull(library.lantern.GetComponent<VisionLight>());
            Assert.NotNull(library.player.GetComponent<CharacterController>());
        }
    }
}
