using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;

namespace Vision.UI
{
    /// <summary>
    /// The machete's swipe line: a swept band through the air that starts near the tip of the real blade of Zach's model
    /// and reaches out, the way the blade points, to the swing's actual attack radius, so its outer edge shows how far the
    /// cut hits; it sweeps across with the blade as it cuts, sampled every frame and smoothed between frames. A light cut
    /// leaves a pale steel sheen with a crisp bone-white edge along the reach, gone in about an eighth of a second; a
    /// charged cut (with its longer reach) a band dark blood red at its heart and brighter toward the edge, lingering a
    /// little longer, and flinging red droplets off the tip when it connects.
    /// </summary>
    public sealed partial class MatchEffects
    {
        /// <summary>Near the blade's tip, out at the attack radius, the blade's own tip (for the droplets), when.</summary>
        struct BladeSample { public Vector3 Inner, Tip, Blade; public float T; }

        sealed class Trail
        {
            public readonly List<BladeSample> Samples = new List<BladeSample>(48);
            public int Owner;
            public bool Heavy, Live;
            public ActionClip Clip;
            public float ClipT;
        }

        readonly List<Trail> trails = new List<Trail>();
        readonly List<Vector3> stripA = new List<Vector3>(96), stripB = new List<Vector3>(96);
        readonly List<Color> stripCA = new List<Color>(96), stripCB = new List<Color>(96);
        readonly List<(Vector3 inner, Vector3 tip, float age)> smooth = new List<(Vector3, Vector3, float)>(128);

        // The machete as PropModels builds it: the blade runs from 0.10 m past the grip to its tip at about 0.575 m, the
        // edge along its lower side. The swipe line starts a little inside the tip.
        static readonly Vector3 TipLocal = new Vector3(0f, -0.012f, 0.578f), NearTip = new Vector3(0f, -0.014f, 0.46f);
        const float LightLife = 0.13f, HeavyLife = 0.2f;

        static Color Hex(int rgb, float a) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        /// <summary>Samples each hunter's blade while it cuts, then draws every trail still fading.</summary>
        void BladeTrails(MatchSim sim)
        {
            float now = Time.time;
            foreach (Trail t in trails) t.Live = false;
            foreach (SimPlayer p in sim.Order)
            {
                if (p.Role != Role.Hunter || p.Health == Health.Eliminated) continue;
                GameObject go = CharacterOf(p);
                ActionLayer layer = go != null ? go.GetComponent<ActionLayer>() : null;
                float reach = Scale.D(Balance.Hunter.Attack.Range * (p.JadenBonus > 0 ? Balance.Hunter.JadenSlainRangeMul : 1f)) * S;
                if (layer != null) TrackBlade(p.Id, layer, reach, Balance.Hunter.Attack.ChargeRangeMul);
            }
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                Trail tr = trails[i];
                float life = tr.Heavy ? HeavyLife : LightLife;
                int old = 0;
                while (old < tr.Samples.Count && now - tr.Samples[old].T > life) old++;
                // Keep one expired sample while newer ones remain, so the band's tail fades out instead of jumping.
                if (old > 0 && old < tr.Samples.Count) old--;
                if (old > 0) tr.Samples.RemoveRange(0, old);
                if (tr.Samples.Count == 0 || (!tr.Live && now - tr.Samples[tr.Samples.Count - 1].T > life)) { trails.RemoveAt(i); continue; }
                DrawTrail(tr, now, life);
            }
        }

        /// <summary>
        /// Takes this frame's sample of a model's machete while it is cutting (a new swipe line for each new cut):
        /// <paramref name="reach"/> is a light swing's attack radius in the world, times <paramref name="heavyMul"/> when charged.
        /// </summary>
        public void TrackBlade(int owner, ActionLayer layer, float reach, float heavyMul)
        {
            if (layer == null || layer.HeldRight != PropKind.Machete) return;
            Transform blade = layer.PropTransform();
            ActionClip clip = layer.Current;
            if (blade == null || !blade.gameObject.activeInHierarchy || clip == null || !ActionClips.IsCut(clip)) return;
            float ct = layer.Time;
            if (ct < ActionClips.CutFrom || ct > ActionClips.CutTo) return;
            Trail tr = null;
            for (int i = trails.Count - 1; i >= 0; i--)
                if (trails[i].Owner == owner) { tr = trails[i]; break; }
            // A new cut: another clip, or the same one played again.
            if (tr == null || tr.Clip != clip || ct < tr.ClipT)
            {
                tr = new Trail { Owner = owner, Clip = clip, Heavy = clip == ActionClips.SwingHeavy || clip == ActionClips.SwingHeavyBack };
                trails.Add(tr);
            }
            tr.ClipT = ct;
            tr.Live = true;
            // Out from the body's centre through the blade's tip, to the attack radius, at the tip's height.
            Vector3 tip = blade.TransformPoint(TipLocal), centre = layer.transform.position;
            Vector3 flat = tip - centre;
            flat.y = 0f;
            float r = reach * (tr.Heavy ? heavyMul : 1f);
            Vector3 outer = flat.sqrMagnitude > 1e-6f && flat.magnitude < r ? centre + flat.normalized * r + Vector3.up * (tip.y - centre.y) : tip;
            tr.Samples.Add(new BladeSample { Inner = blade.TransformPoint(NearTip), Tip = outer, Blade = tip, T = Time.time });
        }

        /// <summary>Red droplets flung off the tip of a charged cut where it connects, along the blade's swing.</summary>
        void BladeDroplets(int owner)
        {
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                Trail tr = trails[i];
                if (tr.Owner != owner || tr.Samples.Count < 2) continue;
                BladeSample a = tr.Samples[tr.Samples.Count - 2], b = tr.Samples[tr.Samples.Count - 1];
                Vector3 swing = (b.Blade - a.Blade) / Mathf.Max(1f / 240f, b.T - a.T);
                for (int k = 0; k < 9; k++)
                {
                    Vector3 v = swing * Random.Range(0.25f, 0.6f) + Random.insideUnitSphere * 1.6f * S + Vector3.up * Random.Range(0.4f, 1.6f) * S;
                    Vector3 at = Vector3.Lerp(b.Inner, b.Blade, Random.Range(0.3f, 1f));
                    Fx.Emit(at, v, Random.Range(0.35f, 0.6f), Hex(0x0e0001, 0.95f), Random.Range(0.035f, 0.06f) * S, 9.8f * S, 0.6f, true, false, 1f, true, false, Vfx.Blend.Alpha);
                }
                return;
            }
        }

        void DrawTrail(Trail tr, float now, float life)
        {
            // Smooth between frames: Catmull-Rom through the samples, four steps per frame.
            smooth.Clear();
            List<BladeSample> s = tr.Samples;
            int n = s.Count;
            if (n < 2) return;
            for (int i = 0; i < n - 1; i++)
            {
                BladeSample p0 = s[Mathf.Max(0, i - 1)], p1 = s[i], p2 = s[i + 1], p3 = s[Mathf.Min(n - 1, i + 2)];
                const int steps = 4;
                for (int k = 0; k < steps; k++)
                {
                    float u = k / (float)steps;
                    smooth.Add((CatmullRom(p0.Inner, p1.Inner, p2.Inner, p3.Inner, u), CatmullRom(p0.Tip, p1.Tip, p2.Tip, p3.Tip, u),
                        now - Mathf.Lerp(p1.T, p2.T, u)));
                }
            }
            smooth.Add((s[n - 1].Inner, s[n - 1].Tip, now - s[n - 1].T));

            if (tr.Heavy)
            {
                // Blood: dark at the heart of the band, redder toward the edge, a hot thin edge on the tip's path.
                Band(0f, 0.45f, Hex(0x3a0406, 0f), Hex(0x5a0808, 0.2f), life, Vfx.Blend.Alpha);
                Band(0.45f, 0.86f, Hex(0x5a0808, 0.2f), Hex(0xb01818, 0.36f), life, Vfx.Blend.Alpha);
                Band(0.86f, 1f, Hex(0xb01818, 0.36f), Hex(0xd84030, 0.42f), life, Vfx.Blend.Alpha);
                Band(0.97f, 1.008f, Hex(0xffb8a0, 0.0f), Hex(0xffd8c8, 0.6f), life, Vfx.Blend.Additive);
            }
            else
            {
                // Steel: a faint pale sheen deepening toward the tip, a soft glow just inside the edge, the edge itself crisp.
                Band(0f, 0.55f, Hex(0x9aa4ae, 0f), Hex(0xb8c0c8, 0.05f), life, Vfx.Blend.Alpha);
                Band(0.55f, 1f, Hex(0xb8c0c8, 0.05f), Hex(0xe0e4e6, 0.13f), life, Vfx.Blend.Alpha);
                Band(0.85f, 1f, Hex(0xc8d4e0, 0f), Hex(0xc8d4e0, 0.06f), life, Vfx.Blend.Additive);
                Band(0.975f, 1.008f, Hex(0xe8e2d0, 0.04f), Hex(0xf4efe2, 0.45f), life, Vfx.Blend.Additive);
            }
        }

        /// <summary>One band of the trail, between two fractions of the way from the inner point to the tip.</summary>
        void Band(float from, float to, Color cFrom, Color cTo, float life, Vfx.Blend blend)
        {
            stripA.Clear(); stripB.Clear(); stripCA.Clear(); stripCB.Clear();
            foreach (var (inner, tip, age) in smooth)
            {
                // Fades with age, eased so the newest part is strongest and the tail melts away.
                float f = Mathf.Clamp01(1f - age / life);
                f = f * f * (3f - 2f * f);
                stripA.Add(Vector3.LerpUnclamped(inner, tip, from));
                stripB.Add(Vector3.LerpUnclamped(inner, tip, to));
                Color a = cFrom, b = cTo;
                a.a *= f; b.a *= f;
                stripCA.Add(a); stripCB.Add(b);
            }
            Fx.Strip(stripA, stripB, stripCA, stripCB, true, false, blend);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}
