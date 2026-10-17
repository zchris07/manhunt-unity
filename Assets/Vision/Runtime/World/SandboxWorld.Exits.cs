using System.Collections.Generic;
using UnityEngine;
using Vision.Player;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// The second exit beside Miga Sushi (a chain-link yard with a roll-up gate of its own and a lever outside it), the
    /// hatch on the bank by the lake's dock, and the extra weapons: a golden pump in Miga Sushi and another somewhere on
    /// the map, and five P250s spread out over the playable circle, about as far from each other as they can be.
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>The second exit gate (Miga Sushi's), and the hatch.</summary>
        public ExitGate Gate2 { get; private set; }
        public Hatch Hatch { get; private set; }
        /// <summary>How many P250s lie out on the map at the start (the user's rule).</summary>
        public const int MapPistols = 5;

        void BuildSushiGate()
        {
            if (Layout == null || !Layout.HasSushiGate) return;
            // A stream of its own: nothing built after it moves for it.
            System.Random rng = FeatureRng(31);
            Transform parent = Group("Miga Sushi Gate");
            Rect r = Layout.SushiGate;
            float w = BuildingPlan.GateWidth, h = 2.4f, top = 2.9f;
            Vector2 mid = Layout.SushiGateMid(), outward = Layout.SushiGateOut(), along = Layout.SushiGateAlong();
            float y = H(r.center) - 0.05f;
            var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) };
            // The yard's gravel, then the fence round it: whole sides but the gate's, which has the gate in the middle.
            var gravel = new LowPolyMeshBuilder(rng);
            gravel.AddBox(new Vector3(-r.width * 0.5f, -0.12f, -r.height * 0.5f), new Vector3(r.width, 0.15f, r.height), new Color(0.32f, 0.31f, 0.29f), 0.05f);
            MakeStatic("Gate Yard Gravel", gravel.ToMesh("Gravel"), new Vector3(r.center.x, y + 0.05f, r.center.y), Quaternion.identity, lowPolyMaterial).SetActive(true);
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                Vector2 m = (a + b) * 0.5f;
                bool gateSide = Vector2.Dot(m - r.center, outward) > 0.1f && Mathf.Abs(Vector2.Dot((b - a).normalized, outward)) < 0.1f;
                if (!gateSide)
                {
                    Fence(a, b);
                    continue;
                }
                // Either side of the gate.
                Vector2 g0 = mid - along * (w * 0.5f + 0.1f), g1 = mid + along * (w * 0.5f + 0.1f);
                Vector2 near0 = Vector2.Dot(a - mid, along) < 0f ? a : b, near1 = near0 == a ? b : a;
                Fence(near0, g0);
                Fence(g1, near1);
            }

            // The gate: the roll-up door in its frame, facing out of the yard.
            var go = new GameObject("Exit Gate 2");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            Vector2 start = mid - along * (w * 0.5f);
            go.transform.SetLocalPositionAndRotation(new Vector3(start.x, y, start.y), Quaternion.Euler(0f, Yaw(along), 0f));
            Piece("Gate Frame", LowPolyModels.RollUpFrame(w, h, top), go.transform, Vector3.zero, 0f);
            GameObject door = Piece("Roll-up Door", LowPolyModels.RollUpDoor(rng, w, h), go.transform, Vector3.zero, 0f);
            door.isStatic = false;
            BoxCollider blocker = AddBox(go, new Vector3(w * 0.5f, 1.2f, 0f), new Vector3(w, 2.4f, 0.3f));
            var occ = go.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Box;
            occ.size = new Vector2(w, 0.1f);
            occ.offset = new Vector2(w * 0.5f, 0f);

            // The lever outside, on a post of its own beside the gate.
            Vector2 lv = Layout.SushiGateLever;
            var post = new LowPolyMeshBuilder(rng);
            post.AddBox(new Vector3(-0.14f, 0f, -0.3f), new Vector3(0.28f, 1.55f, 0.16f), LowPolyModels.Bldg.SteelDark, 0.05f);
            float postYaw = Yaw(along) + 90f;
            GameObject postGo = Piece("Gate Lever Post", post.ToMesh("Lever Post"), parent, new Vector3(lv.x, H(lv) - 0.05f, lv.y), postYaw);
            AddBox(postGo, new Vector3(0f, 0.75f, -0.2f), new Vector3(0.3f, 1.5f, 0.2f));
            GameObject box = Piece("Gate Lever", LowPolyModels.LeverBox(), postGo.transform, new Vector3(0f, 0f, -0.14f), 0f);
            box.transform.localRotation = Quaternion.identity;
            var handle = new GameObject("Handle").transform;
            handle.SetParent(box.transform, false);
            handle.localPosition = new Vector3(0f, 1.17f, 0.17f);
            handle.gameObject.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.LeverHandle();
            PropFactory.NoShadows(handle.gameObject.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            // A light over the gate (on once the gates have power), so it reads as the way out.
            var lamp = new GameObject("Gate Lamp");
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(w * 0.5f, 0f, 1.2f);
            var light = lamp.AddComponent<VisionLight>();
            light.range = 4.5f;
            light.height = 2.6f;
            light.intensity = 0.6f;
            light.flickerAmount = 0.02f;
            light.enabled = false;

            Gate2 = go.AddComponent<ExitGate>();
            Gate2.door = door.transform;
            Gate2.leverHandle = handle;
            Gate2.lever = box.transform;
            Gate2.blocker = blocker;
            Gate2.occluder = occ;
            Gate2.height = h;
            Gate2.lamp = light;
            go.SetActive(true);

            void Fence(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                if (len < 0.2f) return;
                GameObject fence = Piece("Gate Yard Fence", LowPolyModels.ChainLink(rng, len), parent, new Vector3(a.x, Mathf.Min(H(a), H(b)) - 0.05f, a.y), Yaw(b - a));
                AddBox(fence, new Vector3(len * 0.5f, 1.1f, 0f), new Vector3(len, 2.2f, 0.12f));
            }
        }

        void BuildHatch()
        {
            if (Layout == null) return;
            Transform parent = Group("Hatch");
            Vector2 p = Layout.Hatch;
            Vector2 toCentre = (-Layout.LakeCentre).normalized;
            var go = new GameObject("Hatch");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(p.x, H(p) - 0.02f, p.y), Quaternion.Euler(0f, Yaw(new Vector2(-toCentre.y, toCentre.x)), 0f));
            Hatch = go.AddComponent<Hatch>();
            Hatch.Build(FeatureRng(37), lowPolyMaterial);
        }

        /// <summary>A golden pump lying on a surface (Miga Sushi's counter), its model from a stream of its own.</summary>
        void PlaceGoldenPump(Vector2 at, float y, float yaw, Transform parent)
        {
            var go = new GameObject("Pickup Golden pump");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(FeatureRng(41), ItemType.Shotgun, true);
            PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            var pickup = go.AddComponent<Pickup>();
            pickup.item = ItemType.Shotgun;
            pickup.golden = true;
            pickup.count = 1;
            go.transform.SetLocalPositionAndRotation(new Vector3(at.x, y, at.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * 1.6f;
            Pickups.Add(pickup);
        }

        /// <summary>
        /// The extra weapons, from a stream of their own (nothing else on the map moves for them): a golden pump somewhere
        /// open on the map, and five P250s anywhere open inside the playable circle, each the best of many tries at being
        /// as far as it can from the others and the edge, so they come out about evenly spread.
        /// </summary>
        void BuildExtraWeapons(Transform parent)
        {
            System.Random r = FeatureRng(23);
            float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
            float limit = halfExtent - PickupEdgeMargin;
            bool Open(Vector2 p) => p.magnitude <= limit && !(Layout.LakeDepth(p) > -1f) && !Layout.Yard.Contains(p) && !Layout.Building.Contains(p)
                && !(Layout.HasSushiGate && Layout.SushiGate.Contains(p)) && !blocked.AnyWithin(p, 0.6f);
            var taken = new List<Vector2>();
            foreach (Pickup k in Pickups)
            {
                Vector3 l = transform.InverseTransformPoint(k.transform.position);
                taken.Add(new Vector2(l.x, l.z));
            }
            Vector2? Spot(List<Vector2> spread, bool edge)
            {
                Vector2? best = null;
                float bestScore = float.MinValue;
                for (int tries = 0; tries < 400; tries++)
                {
                    float ang = Rn(0f, Mathf.PI * 2f), rad = limit * Mathf.Sqrt(Rn(0f, 1f));
                    var c = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                    if (!Open(c)) continue;
                    bool crowded = false;
                    foreach (Vector2 q in taken) if ((q - c).sqrMagnitude < 2f * 2f) { crowded = true; break; }
                    if (crowded) continue;
                    float score = float.MaxValue;
                    foreach (Vector2 q in spread) score = Mathf.Min(score, (q - c).magnitude);
                    // Keep off the edge too, as if it were another of them.
                    if (edge) score = Mathf.Min(score, (limit - c.magnitude) * 2f);
                    if (spread.Count == 0 && !edge) score = Rn(0f, 1f);
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                return best;
            }
            Pickup Put(ItemType item, bool golden, Vector2 at)
            {
                var go = new GameObject(golden ? "Pickup Golden pump" : $"Pickup {item}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(r, item, golden);
                PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                var pickup = go.AddComponent<Pickup>();
                pickup.item = item;
                pickup.golden = golden;
                pickup.count = 1;
                Conform(go.transform, at, 0.1f, Rn(0f, 360f), 0.8f, 0f);
                go.transform.localScale = Vector3.one * 1.6f;
                Pickups.Add(pickup);
                taken.Add(at);
                return pickup;
            }
            Vector2? g = Spot(new List<Vector2>(), false);
            if (g.HasValue) Put(ItemType.Shotgun, true, g.Value);
            var pistols = new List<Vector2>();
            for (int i = 0; i < MapPistols; i++)
            {
                Vector2? s = Spot(pistols, true);
                if (!s.HasValue) continue;
                pistols.Add(s.Value);
                Put(ItemType.Pistol, false, s.Value);
            }
        }
    }
}
