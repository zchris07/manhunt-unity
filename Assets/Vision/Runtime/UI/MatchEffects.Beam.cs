using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;

namespace Vision.UI
{
    /// <summary>
    /// The Hemp Beam, whoever fires it (Zach, a survivor Thomas armed, Sexton): it comes out of the palm of the
    /// outstretched hand like a repulsor blast. While it charges, a cloud of green-white motes spirals in and condenses
    /// into a single blinding point in the palm; then a layered beam (a white-hot core, a green body, a wide soft glow,
    /// two energy threads twisting round it, shimmer flecks peeling off) runs to whatever stops it, and there it
    /// splashes: a hot spot, sparks spraying back off the surface and falling, embers, curling smoke and chips of debris.
    /// </summary>
    public sealed partial class MatchEffects
    {
        static readonly Color BeamCore = new Color(1f, 1f, 0.96f, 1f), BeamBody = new Color(0.62f, 1f, 0.6f, 1f), BeamGlow = new Color(0.3f, 1f, 0.36f, 1f);
        readonly Dictionary<int, float> beamEmit = new Dictionary<int, float>();
        readonly List<Vector3> beamPts = new List<Vector3>(48);
        readonly List<Color> beamCols = new List<Color>(48);

        /// <summary>The palm of a character's right hand (world), from its view; false when it isn't drawn.</summary>
        static bool Palm(GameObject character, out Vector3 palm)
        {
            palm = default;
            ActionLayer layer = character != null ? character.GetComponent<ActionLayer>() : null;
            if (layer == null || layer.bones == null || layer.bones.Length <= (int)Bone.HandR || layer.bones[(int)Bone.HandR] == null) return false;
            Transform hand = layer.bones[(int)Bone.HandR];
            // A little way along the hand toward the fingers, on the palm's side.
            float s = hand.lossyScale.x;
            palm = hand.position - hand.up * (0.07f * s) + hand.forward * (0.02f * s);
            return true;
        }

        GameObject CharacterOf(SimPlayer p)
        {
            if (p.IsLocal && world.Player != null && world.Player.View != null) return world.Player.View.gameObject;
            var puppets = world.GetComponent<Vision.Player.PlayerPuppets>();
            Vision.Player.PlayerPuppets.Puppet pup = puppets != null ? puppets.For(p.Id) : null;
            return pup != null ? pup.Go : null;
        }

        /// <summary>A player's beam: charging for the windup, then firing for the beam's time.</summary>
        void PlayerBeam(SimPlayer p)
        {
            if (p.BeamT <= 0f) return;
            bool charging = p.BeamT > Balance.Sexton.Defense.BeamTime;
            float k = charging ? 1f - (p.BeamT - Balance.Sexton.Defense.BeamTime) / Mathf.Max(0.01f, Balance.Hunter.Beam.Windup) : 1f;
            Vector3 palm = Palm(CharacterOf(p), out Vector3 hp) ? hp : Along(p.Pos, p.BeamAng, Balance.HunterRadius * 1.6f, 0f, 1.25f);
            HempBeam(p.Id, p.Pos, p.BeamAng, p.BeamLen, charging, k, palm, p.BeamT);
        }

        /// <summary>
        /// Draws a Hemp Beam from <paramref name="palm"/>: charging (k 0-1) or firing along <paramref name="angle"/> for
        /// <paramref name="lenDesign"/> (design units from the body's centre; 0: all the way).
        /// </summary>
        void HempBeam(int key, Vector2 from, float angle, float lenDesign, bool charging, float k, Vector3 palm, float seed)
        {
            float now = Time.time;
            if (charging)
            {
                ChargeCondense(key, palm, Mathf.Clamp01(k), now);
                return;
            }
            float max = Scale.D(Balance.Sexton.Defense.BeamRange);
            float len = lenDesign > 0f ? lenDesign : max;
            bool blocked = lenDesign > 0f && lenDesign < max - Scale.D(4f);
            // The end, at the palm's height above the ground there.
            Vector3 local = world.transform.InverseTransformPoint(palm);
            float up = local.y - world.GroundHeight(new Vector2(local.x, local.z));
            Vector3 end = Ground(from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len, up);
            Vector3 axis = end - palm;
            float length = axis.magnitude;
            if (length < 1e-3f) return;
            Vector3 dir = axis / length;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float flicker = 0.88f + 0.12f * Mathf.Sin(now * 53f + key) * Mathf.Sin(now * 31f);
            float s = S;

            // The body: a wide soft glow, the green beam, the white-hot core (soft-edged ribbons).
            void Layer(float width, Color c, float taper)
            {
                beamPts.Clear(); beamCols.Clear();
                const int n = 12;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    beamPts.Add(palm + axis * t);
                    // A pulse running down the beam, and a little narrowing toward the end.
                    float pulse = 0.85f + 0.15f * Mathf.Sin(t * 18f - now * 40f);
                    Color col = c * flicker * pulse;
                    col.a = c.a * flicker * Mathf.Lerp(1f, taper, t);
                    beamCols.Add(col);
                }
                Fx.Ribbon(beamPts, beamCols, width * s, false, true, Vfx.Blend.Additive, true);
            }
            Layer(0.95f, new Color(BeamGlow.r, BeamGlow.g, BeamGlow.b, 0.16f), 0.7f);
            Layer(0.42f, new Color(BeamBody.r, BeamBody.g, BeamBody.b, 0.5f), 0.85f);
            Layer(0.16f, new Color(BeamCore.r, BeamCore.g, BeamCore.b, 0.95f), 0.9f);

            // Two energy threads twisting round the beam, racing forward.
            for (int strand = 0; strand < 2; strand++)
            {
                beamPts.Clear(); beamCols.Clear();
                int n = Mathf.Clamp(Mathf.CeilToInt(length / (0.12f * s)), 8, 64);
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    float ph = t * length / (0.55f * s) * Mathf.PI * 2f - now * 26f + strand * Mathf.PI;
                    beamPts.Add(palm + axis * t + side * (Mathf.Sin(ph) * 0.13f * s) + Vector3.up * (Mathf.Cos(ph) * 0.06f * s));
                    beamCols.Add(new Color(0.85f, 1f, 0.85f, 0.55f * (1f - t * 0.4f)));
                }
                Fx.Ribbon(beamPts, beamCols, 0.035f * s, false, true, Vfx.Blend.Additive, true);
            }

            // At the palm: a blinding flare and a ring of light round the hand.
            Fx.Glow(palm, 0.55f * s * flicker, new Color(0.75f, 1f, 0.75f, 0.85f));
            Fx.Glow(palm, 1.2f * s * flicker, new Color(0.35f, 1f, 0.4f, 0.3f));
            Fx.Glow(palm, 0.22f * s, new Color(1f, 1f, 1f, 1f));

            // Shimmer: flecks peeling off the beam.
            float dt = Time.deltaTime;
            beamEmit.TryGetValue(key, out float acc);
            acc += dt;
            while (acc > 0.012f)
            {
                acc -= 0.012f;
                float t = Random.value;
                Vector3 at = palm + axis * t + side * Random.Range(-0.08f, 0.08f) * s;
                Vector3 v = (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(0.2f, 1f)) * Random.Range(0.6f, 1.6f) * s + dir * Random.Range(0.5f, 2f) * s;
                Fx.Emit(at, v, Random.Range(0.2f, 0.45f), Random.value < 0.3f ? BeamCore : BeamBody, Random.Range(0.025f, 0.05f) * s, 0f, 3f, true, false, 1f, false, true);
                if (!blocked) continue;
                Impact(end, dir, s);
            }
            beamEmit[key] = acc;

            if (blocked)
            {
                // Where it hits: a white-hot spot and a green bloom.
                Fx.Glow(end, 0.7f * s * flicker, new Color(1f, 1f, 0.9f, 0.9f));
                Fx.Glow(end, 1.6f * s * flicker, new Color(0.4f, 1f, 0.45f, 0.35f));
                Fx.Glow(end - dir * 0.1f * s, 0.35f * s, new Color(1f, 0.85f, 0.5f, 0.8f));
            }
        }

        /// <summary>A splash where the beam meets a surface: sparks spraying back and falling, embers, smoke, chips.</summary>
        void Impact(Vector3 at, Vector3 dir, float s)
        {
            Vector3 back = -dir;
            // Sparks: back off the surface in a wide cone, bright white-yellow to green, falling under gravity.
            for (int i = 0; i < 2; i++)
            {
                Vector3 v = (back + Random.insideUnitSphere * 0.9f + Vector3.up * 0.4f).normalized * Random.Range(3f, 8f) * s;
                Color c = Color.Lerp(new Color(1f, 0.95f, 0.7f, 1f), new Color(0.6f, 1f, 0.55f, 1f), Random.value);
                Fx.Emit(at, v, Random.Range(0.25f, 0.6f), c, Random.Range(0.03f, 0.06f) * s, 9.8f * s, 1.2f, true, false, 1f, false, true);
            }
            // Embers: slow, orange, drifting up and out.
            if (Random.value < 0.35f)
                Fx.Emit(at + Random.insideUnitSphere * 0.1f * s, (back * 0.6f + Vector3.up + Random.insideUnitSphere * 0.5f) * Random.Range(0.4f, 1f) * s,
                    Random.Range(0.6f, 1.2f), new Color(1f, 0.55f, 0.2f, 0.9f), Random.Range(0.025f, 0.045f) * s, -0.6f * s, 1.5f, false, true, 1f, false, true);
            // Smoke: soft grey puffs curling up and swelling, in front of the hot spot.
            if (Random.value < 0.18f)
                Fx.Emit(at + back * 0.15f * s, (Vector3.up * Random.Range(0.6f, 1.1f) + back * 0.3f + Random.insideUnitSphere * 0.3f) * s,
                    Random.Range(1f, 1.8f), new Color(0.32f, 0.34f, 0.3f, 0.32f), Random.Range(0.18f, 0.28f) * s, 0f, 1.2f, false, false, 3.2f, false, true, Vfx.Blend.Alpha);
            // Chips of whatever it hit, thrown back and dropping.
            if (Random.value < 0.12f)
                Fx.Emit(at, (back + Random.insideUnitSphere * 0.6f + Vector3.up * 0.8f).normalized * Random.Range(2f, 4.5f) * s,
                    Random.Range(0.5f, 0.9f), new Color(0.22f, 0.2f, 0.17f, 1f), Random.Range(0.03f, 0.05f) * s, 12f * s, 0.5f, false, false, 1f, false, true, Vfx.Blend.Alpha);
        }

        /// <summary>
        /// The charge: forty motes spiralling in from a sphere round the hand, faster and tighter as it fills, condensing
        /// into one point that swells and burns white, with a ring of light gathering round it at the end.
        /// </summary>
        void ChargeCondense(int key, Vector3 palm, float k, float now)
        {
            float s = S;
            const int motes = 40;
            for (int i = 0; i < motes; i++)
            {
                // Each mote has its own start direction, delay and speed (stable from its index).
                float h1 = Hash(i * 3 + key), h2 = Hash(i * 3 + 1 + key), h3 = Hash(i * 3 + 2 + key);
                float delay = h3 * 0.55f;
                float u = Mathf.Clamp01((k - delay) / (1f - delay));
                if (u <= 0f) continue;
                // A fresh mote every cycle near the end, so the stream keeps coming.
                float travel = 1f - Mathf.Pow(1f - u, 2.2f);
                float theta = h1 * Mathf.PI * 2f + travel * (2.5f + h2 * 2f), phi = Mathf.Acos(h2 * 2f - 1f);
                Vector3 d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi) * 0.6f, Mathf.Sin(phi) * Mathf.Sin(theta));
                float r = Mathf.Lerp(1.1f, 0.02f, travel) * s;
                Vector3 at = palm + d * r;
                float a = Mathf.Clamp01(u * 3f) * (1f - travel * 0.3f);
                Fx.Glow(at, (0.08f + 0.04f * h1) * s, new Color(0.7f, 1f, 0.7f, 0.9f * a));
                // A short trail behind it, toward where it came from.
                beamPts.Clear(); beamCols.Clear();
                beamPts.Add(at + d * 0.22f * s * (1f - travel)); beamPts.Add(at);
                beamCols.Add(new Color(0.4f, 1f, 0.45f, 0f)); beamCols.Add(new Color(0.8f, 1f, 0.8f, 0.6f * a));
                Fx.Ribbon(beamPts, beamCols, 0.03f * s, false, true, Vfx.Blend.Additive, true);
            }
            // The point it all condenses into: tiny at first, swelling, pulsing faster, burning white.
            float pulse = 0.8f + 0.2f * Mathf.Sin(now * Mathf.Lerp(12f, 40f, k));
            Fx.Glow(palm, (0.06f + 0.34f * k * k) * s * pulse, new Color(1f, 1f, 0.95f, 0.5f + 0.5f * k));
            Fx.Glow(palm, (0.2f + 0.9f * k) * s * pulse, new Color(0.35f, 1f, 0.4f, 0.12f + 0.3f * k));
            if (k > 0.8f) Fx.Circle(palm - Vector3.up * 0.02f, (1.4f - k) * 2.2f * s, 0.04f * s, new Color(0.7f, 1f, 0.7f, (k - 0.8f) * 3f), false, true);
        }

        static float Hash(int i)
        {
            float h = Mathf.Sin(i * 12.9898f + 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
