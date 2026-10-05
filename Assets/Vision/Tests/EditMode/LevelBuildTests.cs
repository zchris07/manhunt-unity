using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vision.Player;
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
            Assert.GreaterOrEqual(world.Doors.Count, 3, "a door in each cabin (and the shacks)");
            Assert.GreaterOrEqual(world.Crows.Count, 6);
            Assert.NotNull(world.Player);
            Assert.NotNull(world.Wanderer);
            Assert.AreEqual(4, world.Wanderer.waypoints.Length);
            int lights = world.GetComponentsInChildren<VisionLight>().Length;
            Assert.That(lights, Is.InRange(15, 60), "campfires, cabin lamps, the dock lantern and lanterns along the paths");
            Assert.AreEqual(3, world.Generators.Count, "three generators in the woods");
            Assert.AreEqual(world.Layout.GrassPatches.Count, System.Linq.Enumerable.Count(world.GetComponentsInChildren<HidingSpot>(), h => h.kind == HidingSpot.Kind.Grass));
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
        public void NoAsset_HasAShadowOfItsOwn()
        {
            SandboxWorld world = NewWorld(null);
            world.Generate();
            Renderer[] renderers = world.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 100);
            foreach (Renderer r in renderers)
            {
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, r.name);
                Assert.IsFalse(r.receiveShadows, r.name);
            }
            Assert.AreEqual(0, world.Player.GetComponentsInChildren<Light>().Length);
        }

        [Test]
        public void Characters_AreRenderable_PlayerAlwaysAndWandererAsAnEntity()
        {
            SandboxWorld world = NewWorld(null);
            world.Generate();
            var player = world.Player.GetComponentInChildren<SkinnedMeshRenderer>();
            var wanderer = world.Wanderer.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer r in new[] { player, wanderer })
            {
                Assert.IsTrue(r.enabled && r.gameObject.activeInHierarchy, r.name);
                Assert.Greater(r.sharedMesh.triangles.Length, 0);
                Assert.AreEqual(r.sharedMesh.vertexCount, r.sharedMesh.boneWeights.Length, "skinned");
                Assert.AreEqual(r.bones.Length, r.sharedMesh.bindposeCount);
            }
            Assert.AreSame(lowPoly, player.sharedMaterial, "the player is never hidden in the dark");
            Assert.AreSame(entity, wanderer.sharedMaterial, "the wanderer is hidden outside the viewer's light");
        }

        [Test]
        public void OnlyThePlayer_CountsAsTheViewersOwnBody_ForShadows()
        {
            SandboxWorld world = NewWorld(null);
            world.Generate();
            Transform viewer = world.Player.GetComponent<VisionViewer>().transform;
            foreach (Vision.Characters.CharacterShadow caster in world.GetComponentsInChildren<Vision.Characters.CharacterShadow>())
                Assert.AreEqual(caster.GetComponentInParent<PlayerController>() != null, caster.transform.IsChildOf(viewer), caster.name);
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
                Assert.NotNull(tree.GetComponent<Collider>());
                Occluder occ = tree.GetComponent<Occluder>();
                if (occ.shape == Occluder.Shape.Circle) Assert.That(occ.radius, Is.InRange(0.14f, 0.56f), $"{tree.name} trunk footprint");
                else Assert.Greater(occ.size.x, 3f, $"{tree.name} lies along its length");
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
            foreach (GameObject character in new[] { library.player, library.wanderer })
            {
                Mesh m = character.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                Assert.IsTrue(EditorUtility.IsPersistent(m), "saved mannequin mesh");
                Assert.LessOrEqual(m.triangles.Length / 3, Vision.Characters.MannequinBuilder.MaxTriangles);
                Assert.AreEqual(m.vertexCount, m.boneWeights.Length, "the saved mesh keeps its bone weights");
                Assert.AreEqual(Vision.Characters.HumanoidSkeleton.BoneCount, m.bindposeCount, "and its bind poses");
            }
        }

        [Test]
        public void RenderPipeline_HasRealTimeShadowsOff()
        {
            // The pipelines the game renders with (not every URP asset in the packages).
            var pipelines = new System.Collections.Generic.List<UnityEngine.Rendering.RenderPipelineAsset> { UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline };
            for (int i = 0; i < QualitySettings.count; i++) pipelines.Add(QualitySettings.GetRenderPipelineAssetAt(i));
            int checkedCount = 0;
            foreach (var pipeline in pipelines)
            {
                if (!(pipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)) continue;
                Assert.IsFalse(urp.supportsMainLightShadows, urp.name);
                checkedCount++;
            }
            Assert.Greater(checkedCount, 0, "the project renders with URP");
        }
    }
}
