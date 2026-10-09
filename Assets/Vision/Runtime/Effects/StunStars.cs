using UnityEngine;

namespace Vision.Effects
{
    /// <summary>
    /// The original's stun effect, in 3D: four five-pointed stars (bone-yellow #c8b890 with a black ink outline) orbiting
    /// above a stunned character's head at about 0.8 turns a second, bobbing a little, each turned to face the camera.
    /// They are entity geometry: like the character, they show only inside the viewer's light.
    /// </summary>
    public sealed class StunStars : MonoBehaviour
    {
        [Tooltip("Height of the orbit above the character's feet (design units).")]
        public float height = 2.05f;
        public float radius = 0.32f;
        public float starSize = 0.085f;
        public float turnsPerSecond = 0.8f;
        public Material material;

        public bool Showing { get; private set; }
        /// <summary>The kind of daze: stars, or the Penjamin spiral (dizzy).</summary>
        public bool Dizzy { get; set; }

        static Mesh starMesh;
        readonly Transform[] stars = new Transform[4];
        Transform pivot;
        float shown, spin;

        public static Mesh StarMesh()
        {
            if (starMesh != null) return starMesh;
            // Ten points (five tips) filled in the original's colour, and a slightly larger black star just behind it.
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            void Star(float r, float z, Color c)
            {
                int centre = verts.Count;
                verts.Add(new Vector3(0f, 0f, z));
                cols.Add(c);
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float rr = i % 2 == 0 ? r : r * 0.45f;
                    verts.Add(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z));
                    cols.Add(c);
                }
                for (int i = 0; i < 10; i++)
                {
                    tris.Add(centre);
                    tris.Add(centre + 1 + (i + 1) % 10);
                    tris.Add(centre + 1 + i);
                }
            }
            Star(1.25f, 0.02f, Color.black);
            Star(1f, 0f, new Color(0xc8 / 255f, 0xb8 / 255f, 0x90 / 255f));
            starMesh = new Mesh { name = "Stun Star", hideFlags = HideFlags.HideAndDontSave };
            starMesh.SetVertices(verts);
            starMesh.SetColors(cols);
            starMesh.SetTriangles(tris, 0);
            starMesh.RecalculateNormals();
            starMesh.RecalculateBounds();
            return starMesh;
        }

        void Build()
        {
            if (pivot != null) return;
            pivot = new GameObject("Stun Stars").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.up * height;
            for (int i = 0; i < stars.Length; i++)
            {
                var go = new GameObject("Star " + i);
                go.transform.SetParent(pivot, false);
                go.AddComponent<MeshFilter>().sharedMesh = StarMesh();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                stars[i] = go.transform;
            }
            pivot.gameObject.SetActive(false);
        }

        /// <summary>Shows or hides the stars (they pop in and shrink away).</summary>
        public void Show(bool on)
        {
            Showing = on;
            if (on) Build();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            shown = Mathf.MoveTowards(shown, Showing ? 1f : 0f, dt * (Showing ? 6f : 4f));
            if (pivot == null) return;
            bool visible = shown > 0.001f;
            if (pivot.gameObject.activeSelf != visible) pivot.gameObject.SetActive(visible);
            if (!visible) return;
            spin += dt * turnsPerSecond * Mathf.PI * 2f;
            Camera cam = Camera.main;
            float k = 1f - (1f - shown) * (1f - shown);
            for (int i = 0; i < stars.Length; i++)
            {
                float a = spin + i * Mathf.PI * 0.5f;
                float r = Dizzy ? radius * (0.55f + 0.45f * Mathf.Repeat(i * 0.25f + spin * 0.15f, 1f)) : radius;
                float bob = Mathf.Sin(spin * 2f + i * 1.7f) * 0.04f;
                stars[i].localPosition = new Vector3(Mathf.Cos(a) * r, bob, Mathf.Sin(a) * r);
                if (cam != null) stars[i].rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, spin * 40f * Mathf.Rad2Deg / 40f);
                stars[i].localScale = Vector3.one * starSize * k;
            }
        }
    }
}
