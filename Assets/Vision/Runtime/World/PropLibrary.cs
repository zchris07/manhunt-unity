using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The saved prop prefabs the level is built from (Assets/Vision/Prefabs). <see cref="SandboxWorld"/>
    /// picks variants from here; with no library assigned it falls back to generating props in memory.
    /// Rebuilt by the Vision/Bake Level menu.
    /// </summary>
    [CreateAssetMenu(menuName = "Vision/Prop Library", fileName = "PropLibrary")]
    public sealed class PropLibrary : ScriptableObject
    {
        /// <summary>Rock variant radii in metres; index matches <see cref="rocks"/>.</summary>
        public static readonly float[] RockRadii = { 0.45f, 0.6f, 0.75f, 0.9f };

        /// <summary>Crate edge lengths in metres; index matches <see cref="crates"/>.</summary>
        public static readonly float[] CrateSizes = { 0.7f, 0.8f, 0.9f };

        public const int TreeVariants = 6;
        public const int CrowVariants = 2;

        public GameObject[] trees;
        public GameObject[] rocks;
        public GameObject[] crates;
        public GameObject campfire;
        public GameObject lantern;
        public GameObject[] crows;
        public GameObject player;
        public GameObject wanderer;

        public bool IsComplete =>
            Has(trees, TreeVariants) && Has(rocks, RockRadii.Length) && Has(crates, CrateSizes.Length) && Has(crows, CrowVariants)
            && campfire != null && lantern != null && player != null && wanderer != null;

        static bool Has(GameObject[] a, int count)
        {
            if (a == null || a.Length != count) return false;
            foreach (GameObject g in a) if (g == null) return false;
            return true;
        }
    }
}
