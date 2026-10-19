using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// A small golden halo floating level over a reanimated character's head (a ghost back in a body), turning slowly and
    /// bobbing a little. Drawn like the body under it: seen only where the body is (in the viewer's light).
    /// </summary>
    public static class Halo
    {
        // (Not "Halo": the player already carries one of those, Zach's little light at the edge of the woods.)
        const string Name = "Golden Halo";
        /// <summary>The ring's radius and thickness (metres), and how far over the top of the head it floats.</summary>
        public const float Radius = 0.15f, Thickness = 0.022f, Above = 0.16f;

        static Mesh mesh;
        static Material material;

        /// <summary>Puts the halo over <paramref name="character"/> (its root, a figure of the skeleton's height) or takes it off.</summary>
        public static void Set(GameObject character, bool on, Material entity)
        {
            if (character == null) return;
            Transform t = character.transform.Find(Name);
            if (t == null)
            {
                if (!on || entity == null) return;
                t = Create(character.transform, entity);
            }
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (!on) return;
            float time = Time.time;
            t.localPosition = new Vector3(0f, Characters.HumanoidSkeleton.Height + Above + 0.025f * Mathf.Sin(time * 2.2f), 0f);
            t.localRotation = Quaternion.Euler(0f, time * 35f, 0f);
        }

        /// <summary>Whether a character has its halo up (tests and captures).</summary>
        public static bool Showing(GameObject character)
        {
            Transform t = character != null ? character.transform.Find(Name) : null;
            return t != null && t.gameObject.activeSelf;
        }

        static Transform Create(Transform parent, Material entity)
        {
            var go = new GameObject(Name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : mesh = Ring();
            if (material == null)
            {
                // The body's own material (so it shows only where the body does), glowing gold.
                material = new Material(entity) { name = "Halo", hideFlags = HideFlags.HideAndDontSave };
                if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 1.6f);
                if (material.HasProperty("_Tint")) material.SetColor("_Tint", Color.white);
            }
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>A flat ring (a torus), gold, lying level.</summary>
        static Mesh Ring()
        {
            const int around = 28, tube = 6;
            var verts = new Vector3[around * tube];
            var normals = new Vector3[verts.Length];
            var colors = new Color[verts.Length];
            // (Both windings: it shows from above and below whatever the culling.)
            var tris = new int[around * tube * 12];
            var gold = new Color(1f, 0.8f, 0.28f, 1f);
            for (int i = 0; i < around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j < tube; j++)
                {
                    float b = j * Mathf.PI * 2f / tube;
                    Vector3 n = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    int k = i * tube + j;
                    verts[k] = c * Radius + n * Thickness;
                    normals[k] = n;
                    colors[k] = gold;
                }
            }
            int t = 0;
            for (int i = 0; i < around; i++)
                for (int j = 0; j < tube; j++)
                {
                    int a0 = i * tube + j, a1 = i * tube + (j + 1) % tube, b0 = (i + 1) % around * tube + j, b1 = (i + 1) % around * tube + (j + 1) % tube;
                    tris[t++] = a0; tris[t++] = a1; tris[t++] = b0;
                    tris[t++] = b0; tris[t++] = a1; tris[t++] = b1;
                    tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                    tris[t++] = b0; tris[t++] = b1; tris[t++] = a1;
                }
            var m = new Mesh { name = "Halo", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = verts;
            m.normals = normals;
            m.colors = colors;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
