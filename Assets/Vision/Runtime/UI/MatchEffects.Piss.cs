using System.Collections.Generic;
using UnityEngine;
using Vision.Effects;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// Zach soaked in piss: small drops of it falling off him (more the fresher it is), and tiny puddles of it left on the
    /// ground wherever he walks while it's on him (closer together the fresher it is). The puddles are one mesh laid over
    /// the ground; the oldest dry up first once there are many, and a new match starts clean.
    /// </summary>
    public sealed partial class MatchEffects
    {
        /// <summary>The most puddles kept (the oldest dry up first).</summary>
        public const int MaxPissPuddles = 160;
        static readonly Color PissDrop = new Color(0.95f, 0.82f, 0.22f, 0.85f), PissPool = new Color(0.62f, 0.52f, 0.14f);

        readonly List<(Vector2 at, float r, int seed)> puddles = new List<(Vector2, float, int)>();
        readonly Dictionary<int, Vector2> lastPuddle = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> dripDue = new Dictionary<int, float>();
        GameObject puddleObject;
        MatchSim puddleSim;
        bool puddlesDirty;

        /// <summary>How many puddles lie about now, and their mesh (tests).</summary>
        public int PissPuddles => puddles.Count;
        public Mesh PissPuddleMesh => puddleObject != null ? puddleObject.GetComponent<MeshFilter>().sharedMesh : null;

        void PissDrips(MatchSim sim)
        {
            if (sim != puddleSim)
            {
                puddleSim = sim;
                puddles.Clear();
                lastPuddle.Clear();
                puddlesDirty = true;
            }
            float dt = Time.deltaTime, s = S;
            foreach (SimPlayer p in sim.Order)
            {
                if (p.Role != Role.Hunter || p.Health == Health.Eliminated) continue;
                // How fresh it is: 1 a fresh splash, falling off to nothing with it.
                float k = PissLook.Amount(MatchSim.PissBonus(p)) / PissLook.Strongest;
                if (k <= 0f)
                {
                    lastPuddle.Remove(p.Id);
                    continue;
                }
                // Drops falling off him.
                float due = (dripDue.TryGetValue(p.Id, out float d0) ? d0 : 0f) + dt * Mathf.Lerp(5f, 22f, k);
                for (; due >= 1f && Fx != null; due -= 1f)
                {
                    Vector2 off = Random.insideUnitCircle * 0.24f;
                    Vector3 at = Ground(p.Pos + off, Random.Range(0.35f, 1.5f));
                    Vector3 v = new Vector3(off.x, 0f, off.y) * Random.Range(0.4f, 1.2f) * s + Vector3.down * Random.Range(0.1f, 0.5f) * s;
                    Fx.Emit(at, v, Random.Range(0.45f, 0.8f), PissDrop, Random.Range(0.018f, 0.032f) * s, 9.8f * s, 0.4f, true, false, 1f, true, false, Vfx.Blend.Alpha);
                }
                dripDue[p.Id] = due;
                // Tiny puddles where he walks.
                float spacing = Mathf.Lerp(1f, 0.45f, k);
                if (!lastPuddle.TryGetValue(p.Id, out Vector2 last) || (p.Pos - last).magnitude > spacing)
                {
                    lastPuddle[p.Id] = p.Pos;
                    puddles.Add((p.Pos + Random.insideUnitCircle * 0.18f, Random.Range(0.022f, 0.05f) * Mathf.Lerp(0.7f, 1f, k), Random.Range(0, 100000)));
                    while (puddles.Count > MaxPissPuddles) puddles.RemoveAt(0);
                    puddlesDirty = true;
                }
            }
            if (puddlesDirty) DrawPuddles();
        }

        /// <summary>Lays every puddle into one mesh over the ground (each a small ragged blot).</summary>
        void DrawPuddles()
        {
            puddlesDirty = false;
            if (world == null) return;
            if (puddleObject == null)
            {
                puddleObject = new GameObject("Piss Puddles");
                puddleObject.transform.SetParent(world.transform, false);
                puddleObject.AddComponent<MeshFilter>();
                var r = puddleObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = world.lowPolyMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            var b = new LowPolyMeshBuilder(new System.Random(7));
            foreach (var (at, radius, seed) in puddles)
            {
                var rng = new System.Random(seed);
                const int n = 7;
                Vector3 V(Vector2 q) => new Vector3(q.x, world.SurfaceHeight(q) + 0.012f, q.y);
                Vector3 mid = V(at), under = mid + Vector3.down;
                float shade = 0.85f + 0.3f * (float)rng.NextDouble();
                Color c = new Color(PissPool.r * shade, PissPool.g * shade, PissPool.b * shade);
                Vector3 prev = default, first = default;
                for (int i = 0; i <= n; i++)
                {
                    float a = (i % n) * Mathf.PI * 2f / n, rr = radius * (0.7f + 0.5f * (float)rng.NextDouble());
                    Vector3 rim = i == n ? first : V(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                    if (i == 0) first = rim;
                    else b.AddTriangleOutward(mid, prev, rim, c, under);
                    prev = rim;
                }
            }
            MeshFilter mf = puddleObject.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(mf.sharedMesh);
                else DestroyImmediate(mf.sharedMesh);
            }
            mf.sharedMesh = b.ToMesh("Piss Puddles");
        }
    }
}
