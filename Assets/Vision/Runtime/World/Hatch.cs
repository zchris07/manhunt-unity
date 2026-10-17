using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// The hatch on the bank by the lake's dock: a square steel frame set in a concrete collar with a heavy lid. Shut it lies
    /// flat; open (one survivor left) the lid stands up on its hinge over a black shaft, a cold light glowing up out of it;
    /// slammed shut by Zach it's shut again, chained.
    /// </summary>
    public sealed class Hatch : MonoBehaviour
    {
        /// <summary>The opening's side (metres).</summary>
        public const float Size = 1.3f;
        Transform lid;
        GameObject shaft, chain;
        VisionLight glow;
        float openK;
        public bool Open { get; private set; }
        public bool Closed { get; private set; }

        public void Build(System.Random rng, Material mat)
        {
            var b = new LowPolyMeshBuilder(rng);
            float s = Size, c = 0.32f;
            Color concrete = new Color(0.46f, 0.45f, 0.42f), steel = LowPolyModels.Bldg.SteelDark;
            // The concrete collar and the steel rim.
            b.AddBox(new Vector3(-s * 0.5f - c, -0.1f, -s * 0.5f - c), new Vector3(s + 2f * c, 0.14f, c), concrete, 0.05f);
            b.AddBox(new Vector3(-s * 0.5f - c, -0.1f, s * 0.5f), new Vector3(s + 2f * c, 0.14f, c), concrete, 0.05f);
            b.AddBox(new Vector3(-s * 0.5f - c, -0.1f, -s * 0.5f), new Vector3(c, 0.14f, s), concrete, 0.05f);
            b.AddBox(new Vector3(s * 0.5f, -0.1f, -s * 0.5f), new Vector3(c, 0.14f, s), concrete, 0.05f);
            foreach (float z in new[] { -s * 0.5f - 0.05f, s * 0.5f })
                b.AddBox(new Vector3(-s * 0.5f - 0.05f, 0.03f, z), new Vector3(s + 0.1f, 0.05f, 0.05f), steel, 0.04f);
            foreach (float x in new[] { -s * 0.5f - 0.05f, s * 0.5f })
                b.AddBox(new Vector3(x, 0.03f, -s * 0.5f), new Vector3(0.05f, 0.05f, s), steel, 0.04f);
            Add("Hatch Frame", b.ToMesh("Hatch Frame"), transform, Vector3.zero, mat);

            // The shaft: black, seen only with the lid up.
            var sh = new LowPolyMeshBuilder(rng);
            sh.AddBox(new Vector3(-s * 0.5f, -0.02f, -s * 0.5f), new Vector3(s, 0.03f, s), new Color(0.01f, 0.01f, 0.012f), 0f);
            shaft = Add("Hatch Shaft", sh.ToMesh("Hatch Shaft"), transform, Vector3.zero, mat);
            shaft.SetActive(false);

            // The lid, hinged along its back edge.
            var hinge = new GameObject("Hatch Hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(0f, 0.06f, s * 0.5f);
            var l = new LowPolyMeshBuilder(rng);
            l.AddBox(new Vector3(-s * 0.5f, 0f, -s), new Vector3(s, 0.06f, s), LowPolyModels.Bldg.Steel, 0.06f);
            for (int i = 0; i < 4; i++)
                l.AddBox(new Vector3(-s * 0.5f + 0.08f, 0.06f, -s + 0.12f + i * (s - 0.24f) / 3f), new Vector3(s - 0.16f, 0.02f, 0.05f), LowPolyModels.Bldg.SteelDark, 0.04f);
            l.AddBox(new Vector3(-0.12f, 0.06f, -s + 0.05f), new Vector3(0.24f, 0.06f, 0.05f), LowPolyModels.Bldg.Rust, 0.08f);
            Add("Hatch Lid", l.ToMesh("Hatch Lid"), hinge, Vector3.zero, mat);
            lid = hinge;

            // Chains across it, once Zach has slammed it.
            var ch = new LowPolyMeshBuilder(rng);
            foreach (float a in new[] { -40f, 40f })
            {
                Quaternion q = Quaternion.Euler(0f, a, 0f);
                for (int i = -4; i <= 4; i++)
                {
                    Vector3 at = q * new Vector3(i * 0.17f, 0.13f, 0f);
                    ch.AddBox(at - new Vector3(0.05f, 0f, 0.025f), new Vector3(0.1f, 0.03f, 0.05f), new Color(0.3f, 0.29f, 0.27f), 0.06f);
                }
            }
            chain = Add("Hatch Chains", ch.ToMesh("Hatch Chains"), transform, Vector3.zero, mat);
            chain.SetActive(false);

            var lightGo = new GameObject("Hatch Glow");
            lightGo.transform.SetParent(transform, false);
            glow = lightGo.AddComponent<VisionLight>();
            glow.range = 3.2f;
            glow.height = 0.4f;
            glow.intensity = 0.55f;
            glow.flickerAmount = 0.05f;
            glow.enabled = false;
        }

        static GameObject Add(string name, Mesh mesh, Transform parent, Vector3 at, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = mat;
            return go;
        }

        /// <summary>The match's state for it: open (the lid up), or slammed shut and chained.</summary>
        public void SetState(bool open, bool closed)
        {
            Open = open;
            Closed = closed;
            if (shaft != null) shaft.SetActive(open || openK > 0.01f);
            if (chain != null) chain.SetActive(closed && !open);
            if (glow != null) glow.enabled = open;
        }

        void Update()
        {
            float want = Open ? 1f : 0f;
            if (Mathf.Approximately(openK, want)) return;
            // Up over a second; slammed down in a fifth of one.
            openK = Mathf.MoveTowards(openK, want, Time.deltaTime * (Open ? 1f : 5f));
            if (lid != null) lid.localRotation = Quaternion.Euler(105f * openK * openK * (3f - 2f * openK), 0f, 0f);
            if (shaft != null) shaft.SetActive(Open || openK > 0.01f);
        }
    }
}
