using System.Collections.Generic;
using UnityEngine;
using Vision.Effects;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// A body blowing apart (Chacko, Soham, Njaaron): a hot flash, a cloud of red mist, blood sprayed out everywhere in
    /// streaking droplets with lumps of gore among them, smoke rolling up; and where it fell, dark blood stains left on the
    /// ground for the rest of the match: a pool where they stood, splashes and long streaks thrown out round it, wherever
    /// the blast reaches (never through a wall).
    /// </summary>
    public sealed partial class MatchEffects
    {
        /// <summary>The most explosions' stains kept (the oldest go first).</summary>
        public const int MaxStainSets = 24;
        readonly List<GameObject> stainSets = new List<GameObject>();

        static readonly Color BloodDark = new Color(0.055f, 0.002f, 0.004f, 1f), BloodRed = new Color(0.32f, 0.01f, 0.02f, 1f);

        void BloodyExplosion(Vector2 at)
        {
            if (world == null || Fx == null) return;
            float s = S;
            Vector3 c = Ground(at, 0.9f);
            // The flash and the fireball's core.
            Fx.Burst(c, 22, 4f * s, 0.28f, new Color(1f, 0.9f, 0.6f, 1f), 0.24f * s, true, Vfx.Blend.Additive, 0f, 4f, 0.5f, new Color(1f, 0.42f, 0.14f, 1f));
            // Red mist billowing out.
            for (int i = 0; i < 20; i++)
                Fx.Emit(c + Random.insideUnitSphere * 0.3f * s, (Random.insideUnitSphere * 2.4f + Vector3.up * 0.5f) * s, Random.Range(0.9f, 1.7f),
                    Color.Lerp(new Color(0.26f, 0.012f, 0.018f, 0.42f), new Color(0.14f, 0.006f, 0.01f, 0.38f), Random.value), Random.Range(0.25f, 0.5f) * s, 0f, 3f,
                    false, false, 3.4f, true, false, Vfx.Blend.Alpha);
            // Blood everywhere: droplets streaking out and falling.
            for (int i = 0; i < 160; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.9f + 0.1f;
                Fx.Emit(c + Random.insideUnitSphere * 0.15f * s, dir.normalized * Random.Range(2.5f, 10f) * s, Random.Range(0.45f, 1.1f),
                    Color.Lerp(BloodDark, BloodRed, Random.value), Random.Range(0.03f, 0.075f) * s, 9.8f * s, 0.35f, true, false, 1f, true, false, Vfx.Blend.Alpha);
            }
            // Lumps of gore, tumbling.
            for (int i = 0; i < 28; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.25f;
                Color lump = Color.Lerp(new Color(0.24f, 0.02f, 0.03f, 1f), new Color(0.48f, 0.12f, 0.1f, 1f), Random.value);
                Fx.Emit(c, dir.normalized * Random.Range(2f, 6.5f) * s, Random.Range(0.8f, 1.4f), lump, Random.Range(0.07f, 0.15f) * s, 9.8f * s, 0.3f,
                    false, false, 1f, true, false, Vfx.Blend.Alpha);
            }
            // Smoke rolling up after.
            for (int i = 0; i < 12; i++)
                Fx.Emit(c + Vector3.up * 0.3f * s + Random.insideUnitSphere * 0.4f * s, (Random.insideUnitSphere * 0.7f + Vector3.up * 1.3f) * s, Random.Range(1.4f, 2.4f),
                    new Color(0.17f, 0.15f, 0.14f, 0.45f), Random.Range(0.3f, 0.55f) * s, 0f, 2f, false, false, 3f, true, false, Vfx.Blend.Alpha);
            BloodStains(at);
        }

        /// <summary>
        /// The stains an explosion leaves (level-local, laid over the ground just above it): a ragged pool where the body
        /// was, splashes round it out to five metres, long streaks thrown outward, small spatters further out.
        /// </summary>
        public GameObject BloodStains(Vector2 at)
        {
            if (world == null) return null;
            var b = new LowPolyMeshBuilder(new System.Random(Mathf.RoundToInt(at.x * 97f + at.y * 131f)));
            float Y(Vector2 p) => world.SurfaceHeight(p) + 0.025f;
            Vector3 V(Vector2 p) => new Vector3(p.x, Y(p), p.y);
            Color Stain() => Color.Lerp(new Color(0.12f, 0.005f, 0.01f), new Color(0.22f, 0.015f, 0.02f), Random.value);
            // Blood only lands where the blast reaches: a clear line from its middle (never through a wall into the next room).
            int walls = Vfx.SolidMask & ~((1 << Vfx.OverGroundLayer) | (1 << SandboxWorld.GroundLayer) | (1 << SandboxWorld.CharacterLayer) | (1 << SandboxWorld.LowLayer));
            Vector3 from = Ground(at, 0.5f);
            bool Reached(Vector2 p) => !Physics.Linecast(from, Ground(p, 0.5f), walls, QueryTriggerInteraction.Ignore);
            // A ragged blob: a fan of triangles, its rim jittered, two rings so it follows the ground.
            void Splat(Vector2 centre, float r, float stretch, Vector2 dir)
            {
                const int n = 10;
                Color col = Stain();
                Vector2 side = new Vector2(-dir.y, dir.x);
                var rim = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    float rr = r * Random.Range(0.7f, 1.2f);
                    rim[i] = centre + dir * (Mathf.Cos(a) * rr * stretch) + side * (Mathf.Sin(a) * rr);
                }
                Vector3 mid = V(centre);
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = rim[i], p1 = rim[(i + 1) % n];
                    Vector2 h0 = (centre + p0) * 0.5f, h1 = (centre + p1) * 0.5f;
                    Vector3 under = mid + Vector3.down;
                    b.AddTriangleOutward(mid, V(h0), V(h1), col, under);
                    b.AddTriangleOutward(V(h0), V(p0), V(p1), col, under);
                    b.AddTriangleOutward(V(h0), V(p1), V(h1), col, under);
                }
            }
            Splat(at, Random.Range(0.9f, 1.3f), 1f, Vector2.right);
            for (int i = 0; i < 18; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float d = Random.Range(1f, 5f);
                bool streak = Random.value < 0.4f;
                if (!Reached(at + dir * (d + 0.4f))) continue;
                Splat(at + dir * d, Random.Range(0.12f, 0.4f) * (streak ? 0.45f : 1f), streak ? Random.Range(2.5f, 4.5f) : Random.Range(1f, 1.5f), dir);
            }
            for (int i = 0; i < 26; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 q = at + dir * Random.Range(1.5f, 6.5f);
                if (!Reached(q)) continue;
                Splat(q, Random.Range(0.04f, 0.1f), 1f, dir);
            }
            var go = new GameObject("Blood Stains");
            go.transform.SetParent(world.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Blood Stains");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = world.lowPolyMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            stainSets.Add(go);
            while (stainSets.Count > MaxStainSets)
            {
                if (stainSets[0] != null) Destroy(stainSets[0]);
                stainSets.RemoveAt(0);
            }
            return go;
        }
    }
}
