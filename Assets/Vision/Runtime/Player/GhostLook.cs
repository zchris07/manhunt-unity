using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// A ghost's look: the whole figure (and anything it holds) drawn glowing white, bright enough to read in the dark and
    /// seen whatever the light (it isn't one of the things hidden outside the viewer's beam). Put back as it was if they
    /// stop being one (a new match).
    /// </summary>
    public sealed class GhostLook : MonoBehaviour
    {
        static Material ghostMaterial;
        Renderer[] renderers;
        Material[][] originals;
        public bool On { get; private set; }

        /// <summary>The ghosts' material: the level's own low-poly material, whitened, glowing and tinted a cold blue-white.</summary>
        public static Material MaterialFrom(Material source)
        {
            if (ghostMaterial != null || source == null) return ghostMaterial;
            ghostMaterial = new Material(source) { name = "Ghost", hideFlags = HideFlags.HideAndDontSave };
            ghostMaterial.DisableKeyword("_VISION_ENTITY");
            if (ghostMaterial.HasProperty("_Entity")) ghostMaterial.SetFloat("_Entity", 0f);
            if (ghostMaterial.HasProperty("_Whiten")) ghostMaterial.SetFloat("_Whiten", 1f);
            if (ghostMaterial.HasProperty("_Emission")) ghostMaterial.SetFloat("_Emission", 1.35f);
            if (ghostMaterial.HasProperty("_Tint")) ghostMaterial.SetColor("_Tint", new Color(0.86f, 0.93f, 1f, 1f));
            return ghostMaterial;
        }

        /// <summary>Makes <paramref name="go"/> a ghost (or puts it back): every renderer on it, held props included.</summary>
        public static void Set(GameObject go, bool ghost, Material source)
        {
            if (go == null) return;
            GhostLook look = go.GetComponent<GhostLook>();
            if (look == null)
            {
                if (!ghost) return;
                look = go.AddComponent<GhostLook>();
            }
            look.Apply(ghost, source);
        }

        void Apply(bool ghost, Material source)
        {
            if (ghost == On) return;
            if (ghost)
            {
                Material m = MaterialFrom(source);
                if (m == null) return;
                renderers = GetComponentsInChildren<Renderer>(true);
                originals = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                    originals[i] = r.sharedMaterials;
                    var mats = new Material[originals[i].Length];
                    for (int k = 0; k < mats.Length; k++) mats[k] = m;
                    r.sharedMaterials = mats;
                }
            }
            else if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
                renderers = null;
                originals = null;
            }
            On = ghost;
        }
    }
}
