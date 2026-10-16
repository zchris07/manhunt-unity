using System.Collections.Generic;
using UnityEngine;
using Vision.Effects;

namespace Vision.UI
{
    /// <summary>
    /// JARVIS coming on: blue psionic waves ripple out from the survivor running it, the first close about their body and
    /// sinking to the ground, then ring after ring spreading out over the ground and pulsing as they go, with a burst of
    /// blue sparks off them.
    /// </summary>
    public sealed partial class MatchEffects
    {
        struct PendingWave { public float At; public Vector2 Pos; public int K; }
        readonly List<PendingWave> jarvisWaves = new List<PendingWave>();

        /// <summary>How many waves go out, and how far apart (seconds).</summary>
        public const int JarvisWaveCount = 8;
        public const float JarvisWaveGap = 0.14f;

        static readonly Color Psionic = new Color(0.32f, 0.78f, 1f, 0.9f), PsionicBright = new Color(0.75f, 0.95f, 1f, 1f);

        void JarvisWaves(Vector2 at)
        {
            if (Fx == null || world == null) return;
            Fx.Burst(Ground(at, 1.1f), 40, 3.2f * S, 0.55f, Psionic, 0.05f * S, false, Vfx.Blend.Additive, 0f, 3f, 0.25f, PsionicBright);
            for (int k = 0; k < JarvisWaveCount; k++) jarvisWaves.Add(new PendingWave { At = Time.time + k * JarvisWaveGap, Pos = at, K = k });
        }

        /// <summary>Sends out each wave as its time comes (called every frame).</summary>
        void DrawJarvisWaves()
        {
            if (jarvisWaves.Count == 0 || Fx == null || world == null) return;
            float now = Time.time, s = S;
            for (int i = jarvisWaves.Count - 1; i >= 0; i--)
            {
                PendingWave w = jarvisWaves[i];
                if (w.At > now) continue;
                jarvisWaves.RemoveAt(i);
                if (w.K < 2)
                    // About the body, dropping toward the ground as it widens.
                    Fx.RingAt(Ground(w.Pos, 1.25f - w.K * 0.55f), 0.3f * s, 2.2f * s, 0.45f, PsionicBright, 0.09f * s, false, false, Vfx.Blend.Additive);
                else
                {
                    // Out over the ground, each further than the last; every other one brighter, so they pulse as they go.
                    float reach = (9f + w.K * 1.6f) * s;
                    Color c = w.K % 2 == 0 ? PsionicBright : Psionic;
                    Fx.RingAt(Ground(w.Pos, 0.05f), 0.5f * s, reach, 1.1f + w.K * 0.08f, c, (0.06f + 0.012f * w.K) * s, false, false, Vfx.Blend.Additive);
                }
            }
        }
    }
}
