using UnityEngine;
using UnityEngine.Rendering;
using Vision.Characters;
using Vision.Player;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// Builds one fully configured prop (mesh, material, collider, occluder, light) from a mesh. The
    /// level baker saves these as prefabs, and <see cref="SandboxWorld"/> uses them directly when no
    /// prop library is assigned, so a baked prop and a generated one are always identical.
    /// No renderer casts or receives a real shadow: the only shadows in the game come from the player's
    /// flashlight (its visibility polygon, and <see cref="CharacterShadow"/> drawn into the vision mask).
    /// </summary>
    public static class PropFactory
    {
        public const float TreeTrunkRadius = 0.32f;

        static GameObject Make(string name, Mesh mesh, Material material, bool isStatic)
        {
            var go = new GameObject(name) { isStatic = isStatic };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = material;
            return go;
        }

        /// <summary>Turns off shadow casting and receiving on a renderer.</summary>
        public static T NoShadows<T>(T renderer) where T : Renderer
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public static GameObject CreateTree(Mesh mesh, Material material)
        {
            GameObject go = Make("Dead Tree", mesh, material, true);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = TreeTrunkRadius;
            col.height = 4f;
            col.center = new Vector3(0f, 2f, 0f);
            var occ = go.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Circle;
            occ.radius = TreeTrunkRadius;
            occ.sides = 8;
            return go;
        }

        public static GameObject CreateRock(Mesh mesh, Material material, float radius)
        {
            GameObject go = Make("Rock", mesh, material, true);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = radius * 0.85f;
            col.height = 2f;
            col.center = new Vector3(0f, 0.5f, 0f);
            var occ = go.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Circle;
            occ.radius = radius * 0.85f;
            occ.sides = 9;
            return go;
        }

        public static GameObject CreateCrate(Mesh mesh, Material material, float size)
        {
            GameObject go = Make("Crate", mesh, material, true);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, size * 0.5f, 0f);
            col.size = Vector3.one * size;
            var occ = go.AddComponent<Occluder>();
            occ.size = new Vector2(size, size);
            return go;
        }

        public static GameObject CreateCampfire(Mesh mesh, Material glowMaterial)
        {
            GameObject go = Make("Campfire", mesh, glowMaterial, true);
            var light = go.AddComponent<VisionLight>();
            light.range = 6.5f;
            light.flickerAmount = 0.25f;
            light.flickerSpeed = 5f;
            light.height = 0.45f;
            return go;
        }

        public static GameObject CreateLantern(Mesh mesh, Material glowMaterial)
        {
            GameObject go = Make("Lantern", mesh, glowMaterial, true);
            var light = go.AddComponent<VisionLight>();
            light.range = 4.5f;
            light.intensity = 0.85f;
            light.flickerAmount = 0.08f;
            light.height = 1.62f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.15f;
            col.height = 2f;
            col.center = new Vector3(0f, 1f, 0f);
            return go;
        }

        /// <summary>A dynamic entity: hidden outside the viewer's own light.</summary>
        public static GameObject CreateCrow(Mesh mesh, Material entityMaterial)
        {
            GameObject go = Make("Crow", mesh, entityMaterial, false);
            var shadow = go.AddComponent<CharacterShadow>();
            shadow.radius = 0.08f;
            shadow.height = 0.25f;
            shadow.isEntity = true;
            shadow.strength = 0.8f;
            return go;
        }

        /// <summary>
        /// A rigged, skinned mannequin: Body (yaw pivot) holding the 51-bone skeleton and the skinned
        /// mesh, animated by <see cref="HumanoidAnimator"/>. Returns the root.
        /// </summary>
        static GameObject CreateCharacter(string name, Material material, Mesh mesh)
        {
            var root = new GameObject(name);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var skeleton = new GameObject("Skeleton").transform;
            skeleton.SetParent(body, false);
            Transform[] bones = HumanoidSkeleton.Create(skeleton);

            var skin = NoShadows(new GameObject("Mesh").AddComponent<SkinnedMeshRenderer>());
            skin.transform.SetParent(body, false);
            skin.sharedMesh = mesh != null ? mesh : MannequinBuilder.Build();
            skin.sharedMaterial = material;
            skin.bones = bones;
            skin.rootBone = bones[(int)Bone.Pelvis];
            // Relative to the pelvis: covers the figure from the feet to above the head, arms out.
            skin.localBounds = new Bounds(new Vector3(0f, -0.05f, 0f), new Vector3(1.8f, 2.2f, 1.4f));
            skin.updateWhenOffscreen = false;
            skin.quality = SkinQuality.Bone2;

            var animator = root.AddComponent<HumanoidAnimator>();
            animator.body = body;
            animator.bones = bones;
            return root;
        }

        /// <summary>
        /// The wandering figure: a dynamic entity (hidden outside the viewer's own light) walking its
        /// waypoints. Waypoints are set by the level.
        /// </summary>
        public static GameObject CreateWanderer(Material entityMaterial, Mesh mesh = null)
        {
            GameObject root = CreateCharacter("Wanderer", entityMaterial, mesh);
            root.AddComponent<Wanderer>().animator = root.GetComponent<HumanoidAnimator>();
            root.AddComponent<CharacterShadow>().isEntity = true;
            return root;
        }

        /// <summary>Player root: controller, vision viewer and an animated mannequin. Camera and world are wired by the level.</summary>
        public static GameObject CreatePlayer(Material material, Mesh mesh = null)
        {
            GameObject root = CreateCharacter("Player", material, mesh);
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.03f;
            var viewer = root.AddComponent<VisionViewer>();
            var controller = root.AddComponent<PlayerController>();
            controller.viewer = viewer;
            controller.animator = root.GetComponent<HumanoidAnimator>();
            root.AddComponent<CharacterShadow>();
            return root;
        }
    }
}
