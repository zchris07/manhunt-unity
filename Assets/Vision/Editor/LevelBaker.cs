using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vision.Characters;
using Vision.World;

namespace Vision.EditorTools
{
    /// <summary>
    /// Turns the procedural world into saved assets: prop meshes and prefabs (Assets/Vision/Prefabs),
    /// the <see cref="PropLibrary"/> that lists them, and a mesh asset for every other generated mesh
    /// (ground, walls, doors; Assets/Vision/Meshes/Level). Existing assets are overwritten in place, so
    /// their GUIDs and every reference to them stay stable between bakes.
    /// </summary>
    public static class LevelBaker
    {
        const string Root = "Assets/Vision";
        const string PropMeshes = Root + "/Meshes/Props";
        const string LevelMeshes = Root + "/Meshes/Level";
        const string PrefabFolder = Root + "/Prefabs";
        public const string LibraryPath = PrefabFolder + "/PropLibrary.asset";

        /// <summary>Builds every prop variant, saves its mesh and prefab, and returns the library that lists them.</summary>
        public static PropLibrary BakeProps(Material lowPoly, Material entity, Material glow)
        {
            EnsureFolder(PropMeshes);
            EnsureFolder(PrefabFolder);

            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PropLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.trees = new GameObject[PropLibrary.TreeVariants];
            for (int i = 0; i < PropLibrary.TreeVariants; i++)
            {
                Mesh mesh = SaveMesh(LowPolyModels.DeadTree(new System.Random(1000 + i)), $"{PropMeshes}/DeadTree_{i:00}.asset");
                library.trees[i] = SavePrefab(PropFactory.CreateTree(mesh, lowPoly), $"DeadTree_{i:00}");
            }

            library.rocks = new GameObject[PropLibrary.RockRadii.Length];
            for (int i = 0; i < library.rocks.Length; i++)
            {
                float radius = PropLibrary.RockRadii[i];
                Mesh mesh = SaveMesh(LowPolyModels.Rock(new System.Random(2000 + i), radius), $"{PropMeshes}/Rock_{i:00}.asset");
                library.rocks[i] = SavePrefab(PropFactory.CreateRock(mesh, lowPoly, radius), $"Rock_{i:00}");
            }

            library.crates = new GameObject[PropLibrary.CrateSizes.Length];
            for (int i = 0; i < library.crates.Length; i++)
            {
                float size = PropLibrary.CrateSizes[i];
                Mesh mesh = SaveMesh(LowPolyModels.Crate(new System.Random(3000 + i), size), $"{PropMeshes}/Crate_{i:00}.asset");
                library.crates[i] = SavePrefab(PropFactory.CreateCrate(mesh, lowPoly, size), $"Crate_{i:00}");
            }

            Mesh fire = SaveMesh(LowPolyModels.Campfire(new System.Random(4000)), $"{PropMeshes}/Campfire.asset");
            library.campfire = SavePrefab(PropFactory.CreateCampfire(fire, glow), "Campfire");

            Mesh lantern = SaveMesh(LowPolyModels.LanternPost(new System.Random(4001)), $"{PropMeshes}/LanternPost.asset");
            library.lantern = SavePrefab(PropFactory.CreateLantern(lantern, glow), "LanternPost");

            library.crows = new GameObject[PropLibrary.CrowVariants];
            for (int i = 0; i < library.crows.Length; i++)
            {
                Mesh mesh = SaveMesh(LowPolyModels.Crow(new System.Random(5000 + i)), $"{PropMeshes}/Crow_{i:00}.asset");
                library.crows[i] = SavePrefab(PropFactory.CreateCrow(mesh, entity), $"Crow_{i:00}");
            }

            // One mannequin mesh for every character; the wanderer only differs by its entity material.
            Mesh mannequin = SaveMesh(MannequinBuilder.Build(), $"{PropMeshes}/Mannequin.asset");
            library.wanderer = SavePrefab(PropFactory.CreateWanderer(entity, mannequin), "Wanderer");
            library.player = SavePrefab(PropFactory.CreatePlayer(lowPoly, mannequin), "Player");
            foreach (string stale in new[] { "Player", "Wanderer" })
                if (AssetDatabase.LoadAssetAtPath<Mesh>($"{PropMeshes}/{stale}.asset") != null) AssetDatabase.DeleteAsset($"{PropMeshes}/{stale}.asset");

            Report(library, mannequin);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>
        /// Saves every mesh under <paramref name="root"/> that only exists in memory as an asset in
        /// Meshes/Level and points the renderer at the saved copy. Assets from the previous bake that are
        /// no longer used are deleted.
        /// </summary>
        public static int SaveLooseMeshes(Transform root)
        {
            EnsureFolder(LevelMeshes);
            var used = new HashSet<string>();
            var counters = new Dictionary<string, int>();
            int saved = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || EditorUtility.IsPersistent(mesh)) continue;
                string baseName = Sanitize(filter.gameObject.name);
                counters.TryGetValue(baseName, out int n);
                counters[baseName] = n + 1;
                string path = $"{LevelMeshes}/{baseName}_{n:000}.asset";
                filter.sharedMesh = SaveMesh(mesh, path);
                used.Add(path);
                saved++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { LevelMeshes }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!used.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.SaveAssets();
            return saved;
        }

        /// <summary>
        /// Saves a mesh as an asset; if one already exists at the path it is overwritten in place (same GUID).
        /// The data is copied field by field rather than with EditorUtility.CopySerialized, which does not
        /// reliably carry skinning data (bone weights, bind poses) into an existing mesh.
        /// </summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            CopyMesh(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void CopyMesh(Mesh from, Mesh to)
        {
            to.Clear();
            to.indexFormat = from.indexFormat;
            to.vertices = from.vertices;
            to.normals = from.normals;
            to.colors = from.colors;
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 4; channel++)
            {
                from.GetUVs(channel, uv);
                if (uv.Count > 0) to.SetUVs(channel, uv);
            }
            to.subMeshCount = from.subMeshCount;
            for (int i = 0; i < from.subMeshCount; i++) to.SetTriangles(from.GetTriangles(i), i);
            if (from.bindposeCount > 0)
            {
                to.boneWeights = from.boneWeights;
                to.bindposes = from.bindposes;
            }
            to.bounds = from.bounds;
            if (to.vertexCount != from.vertexCount || to.bindposeCount != from.bindposeCount)
                throw new System.InvalidOperationException($"Mesh copy into {to.name} lost data.");
        }

        /// <summary>Logs each prop's triangle count against the shared polygon budget.</summary>
        static void Report(PropLibrary library, Mesh mannequin)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Vision] Polygon budget: reference edge {PolyBudget.ReferenceEdge:0.000}; mannequin {mannequin.triangles.Length / 3} tris");
            void Line(string name, GameObject prefab, PolyBudget.Class c)
            {
                Mesh m = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                sb.Append($"; {name} {m.triangles.Length / 3} tris (edge {PolyBudget.MeasuredEdge(m):0.00} vs {PolyBudget.Edge(c):0.00})");
            }
            Line("tree", library.trees[0], PolyBudget.Class.Tree);
            Line("rock", library.rocks[library.rocks.Length - 1], PolyBudget.Class.Rock);
            Line("crate", library.crates[0], PolyBudget.Class.Prop);
            Line("campfire", library.campfire, PolyBudget.Class.Prop);
            Line("lantern", library.lantern, PolyBudget.Class.Prop);
            Line("crow", library.crows[0], PolyBudget.Class.Prop);
            Debug.Log(sb.ToString());
        }

        /// <summary>Saves a built prop as a prefab (overwriting any previous one) and discards the temporary object.</summary>
        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            go.name = name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static string Sanitize(string name) => name.Replace(' ', '_').Replace('/', '_');

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
