using UnityEngine;
using Vision.Game;

namespace Vision.Player
{
    /// <summary>
    /// Zach soaked in piss: his whole figure (and whatever he holds) tinted yellow, as strongly as the piss on him is fresh:
    /// a fresh splash full yellow, falling off linearly with it to nothing fifteen seconds on. The tint rides on each
    /// renderer's property block, so the materials (and anything that copies them) are left alone; it comes off when the
    /// last of the piss does.
    /// </summary>
    public sealed class PissLook : MonoBehaviour
    {
        /// <summary>The yellow at its strongest (the tint over the figure's own colours).</summary>
        public static readonly Color Yellow = new Color(1f, 0.84f, 0.16f, 1f);
        /// <summary>How far the figure's own colours are washed toward the yellow at its strongest.</summary>
        public const float Wash = 0.35f;

        static readonly int TintId = Shader.PropertyToID("_Tint"), WhitenId = Shader.PropertyToID("_Whiten");
        static readonly System.Collections.Generic.List<Renderer> found = new System.Collections.Generic.List<Renderer>();
        static MaterialPropertyBlock block;

        /// <summary>How yellow (0-1) a figure is for so much extra damage from piss (a fresh splash's +50% the most).</summary>
        public static float Amount(float bonus) => Mathf.Clamp01(bonus / (Balance.Items.Piss.Mul - 1f));

        /// <summary>The yellow showing now (0 none).</summary>
        public float Shown { get; private set; }

        /// <summary>Tints <paramref name="go"/> (every renderer on it, held props included) <paramref name="amount"/> yellow.</summary>
        public static void Set(GameObject go, float amount)
        {
            if (go == null) return;
            PissLook look = go.GetComponent<PissLook>();
            if (look == null)
            {
                if (amount <= 0f) return;
                look = go.AddComponent<PissLook>();
            }
            look.Apply(amount);
        }

        void Apply(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f && Shown <= 0f) return;
            if (Mathf.Abs(amount - Shown) < 0.004f && amount > 0f) return;
            Shown = amount;
            GetComponentsInChildren(true, found);
            if (amount <= 0f)
            {
                foreach (Renderer r in found) if (Tintable(r)) r.SetPropertyBlock(null);
                return;
            }
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(TintId, Color.Lerp(Color.white, Yellow, amount));
            block.SetFloat(WhitenId, Wash * amount);
            foreach (Renderer r in found) if (Tintable(r)) r.SetPropertyBlock(block);
        }

        static bool Tintable(Renderer r) => r is MeshRenderer || r is SkinnedMeshRenderer;
    }
}
