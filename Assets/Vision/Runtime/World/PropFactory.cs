using UnityEngine;
using Vision.Player;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// Builds one fully configured prop (mesh, material, collider, occluder, light) from a mesh. The
    /// level baker saves these as prefabs, and <see cref="SandboxWorld"/> uses them directly when no
    /// prop library is assigned, so a baked prop and a generated one are always identical.
    /// </summary>
    public static class PropFactory
    {
        public const float TreeTrunkRadius = 0.32f;

        static GameObject Make(string name, Mesh mesh, Material material, bool isStatic)
        {
            var go = new GameObject(name) { isStatic = isStatic };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
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
            return go;
        }

        public static GameObject CreateLantern(Mesh mesh, Material glowMaterial)
        {
            GameObject go = Make("Lantern", mesh, glowMaterial, true);
            var light = go.AddComponent<VisionLight>();
            light.range = 4.5f;
            light.intensity = 0.85f;
            light.flickerAmount = 0.08f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.15f;
            col.height = 2f;
            col.center = new Vector3(0f, 1f, 0f);
            return go;
        }

        /// <summary>A dynamic entity: hidden outside the viewer's own light, and casts no shadow.</summary>
        public static GameObject CreateCrow(Mesh mesh, Material entityMaterial)
        {
            GameObject go = Make("Crow", mesh, entityMaterial, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Entity root with a bobbing body child. Waypoints are set by the level.</summary>
        public static GameObject CreateWanderer(Mesh mesh, Material entityMaterial)
        {
            var root = new GameObject("Wanderer");
            GameObject body = Make("Body", mesh, entityMaterial, false);
            body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            body.transform.SetParent(root.transform, false);
            root.AddComponent<Wanderer>().body = body.transform;
            return root;
        }

        /// <summary>Player root: controller, vision viewer and a body child. Camera and world are wired by the level.</summary>
        public static GameObject CreatePlayer(Mesh mesh, Material material)
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.03f;

            GameObject body = Make("Body", mesh, material, false);
            body.transform.SetParent(root.transform, false);

            var viewer = root.AddComponent<VisionViewer>();
            var controller = root.AddComponent<PlayerController>();
            controller.viewer = viewer;
            controller.body = body.transform;
            return root;
        }
    }
}
