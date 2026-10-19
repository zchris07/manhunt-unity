using System.Collections.Generic;
using UnityEngine;
using Vision.Player;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// The places the story NPCs need, as the original's map generator makes them: Chris Zelley's ambulance parked in the
    /// woods off the paths (with a clear ring for him to pace and a row of supplies beside it), and the Four Notes left
    /// beside the paths. Each draws from its own random stream, so nothing else on the map moves.
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>The ambulance's centre on the ground plane, and the direction its length runs (radians, x toward z).</summary>
        public Vector2 AmbulanceCentre { get; private set; }
        public float AmbulanceAngle { get; private set; }
        public Transform Ambulance { get; private set; }

        const float U = MapLayout.Unit;

        /// <summary>
        /// The height of the ground a body stands on: the building's floor inside it, the lake dock's boards on it, the
        /// terrain everywhere else.
        /// </summary>
        public float GroundHeight(Vector2 at)
        {
            if (Layout != null && Layout.Plan != null && Layout.Building.Contains(at)) return BuildingFloor + 0.015f;
            if (OnDock(at, out float deck)) return deck;
            return Terrain != null ? Terrain.Height(at.x, at.y) : 0f;
        }

        /// <summary>How far the dock's deck stands above the lake's water (or the shore at its foot, if that's higher).</summary>
        public const float DockDeckAbove = 0.35f;
        /// <summary>The tops of the dock's boards over its deck line (the planks sit a little proud of it, a few higher still).</summary>
        public const float DockBoardTop = 0.03f;

        /// <summary>
        /// Whether a point on the level's plane is on the lake dock's boards (from the shore at its foot to its end, its
        /// width across), and the height of its deck: solid ground to walk on, the water a step down either side.
        /// </summary>
        public bool OnDock(Vector2 at, out float deck)
        {
            deck = 0f;
            if (Layout == null || Terrain == null || Terrain.LakeLayout == null) return false;
            Vector2 a = Layout.DockStart, ab = Layout.DockEnd - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return false;
            float t = Vector2.Dot(at - a, ab) / len2;
            if (t < 0f || t > 1f || (at - (a + ab * t)).magnitude > Layout.DockHalfWidth) return false;
            // Standing on the tops of its boards.
            deck = Mathf.Max(Terrain.Height(a.x, a.y), Terrain.LakeLevel) + DockDeckAbove + DockBoardTop;
            return true;
        }

        /// <summary>
        /// The height of the ground as it is drawn (its low-poly facets, or the building's floor), for markings that lie
        /// on it: <see cref="GroundHeight"/> is the smooth terrain the facets are cut from, a few centimetres off them.
        /// </summary>
        public float SurfaceHeight(Vector2 at)
        {
            if (Layout != null && Layout.Plan != null && Layout.Building.Contains(at)) return BuildingFloor + 0.015f;
            if (OnDock(at, out float deck)) return deck + 0.005f;
            if (hasSurface && surface.SurfaceHeight(at.x, at.y, out float h)) return h;
            return GroundHeight(at);
        }

        LowPolyMeshBuilder.TerrainGrid surface;
        bool hasSurface;

        void BuildAmbulance()
        {
            System.Random r = FeatureRng(9);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            Vector3 size = LowPolyModels.AmbulanceSize;
            float halfDiag = new Vector2(size.x, size.z).magnitude * 0.5f;
            float clear = halfDiag + Game.Balance.Chris.Pace * U + 40f * U;
            Vector2 hunter = Layout.Spawn;
            float far = -1f;
            foreach (MapLayout.Clearing c in Layout.Clearings)
            {
                float d = Vector2.Distance(c.Centre, Layout.Spawn);
                if (d > far) { far = d; hunter = c.Centre; }
            }
            PathNetwork paths = Terrain.Paths;
            bool Ok(Vector2 p, bool strictPaths)
            {
                if (p.magnitude > halfExtent - 450f * U) return false;
                if (Expand(Layout.Building, clear + 200f * U).Contains(p) || Expand(Layout.Yard, clear + 100f * U).Contains(p)) return false;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    if (Layout.LakeDepth(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (clear + 60f * U)) > -1f) return false;
                }
                if (Vector2.Distance(Layout.Spawn, p) < 700f * U || Vector2.Distance(hunter, p) < 700f * U) return false;
                foreach (MapLayout.Cabin c in Layout.Cabins) if (Expand(c.Area, clear + 80f * U).Contains(p)) return false;
                foreach (MapLayout.Kit k in Layout.Kits) if (Vector2.Distance(k.Centre, p) < 160f * U + clear + 60f * U) return false;
                foreach (Transform g in Generators)
                {
                    Vector3 l = transform.InverseTransformPoint(g.position);
                    if (Vector2.Distance(new Vector2(l.x, l.z), p) < clear + 140f * U) return false;
                }
                foreach (MapLayout.Segment f in Layout.Fences)
                    if (Game.SimMap.SegmentDistance(p, f.A, f.B) < clear + 40f * U) return false;
                if (paths != null && paths.Distance(p) < (strictPaths ? clear + 20f * U : halfDiag + 10f * U) + paths.HalfWidth) return false;
                return !blocked.AnyWithin(p, halfDiag);
            }
            Vector2 at = Layout.Spawn + new Vector2(0f, -900f * U);
            float angle = 0f;
            for (int tries = 0; tries < 4000; tries++)
            {
                var p = new Vector2(R(-halfExtent, halfExtent), R(-halfExtent, halfExtent));
                if (!Ok(p, tries < 2500)) continue;
                at = p;
                angle = R(0f, Mathf.PI);
                break;
            }
            AmbulanceCentre = at;
            AmbulanceAngle = angle;
            Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), across = new Vector2(-along.y, along.x);

            Transform parent = Group("Ambulance");
            float yaw = 90f - angle * Mathf.Rad2Deg;
            GameObject van = Piece("Ambulance", LowPolyModels.Ambulance(r), parent, Upright(at, halfDiag * 0.7f), yaw);
            van.isStatic = true;
            AddBox(van, new Vector3(0f, size.y * 0.5f, 0f), size);
            var occ = van.AddComponent<Occluder>();
            occ.size = new Vector2(size.x, size.z);
            // It glows faintly on both sides, as the original's does.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 lp = at + new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * side * (size.x * 0.5f + 12f * U);
                var glow = new GameObject("Ambulance Light");
                glow.transform.SetParent(parent, false);
                glow.transform.localPosition = Upright(lp, 0f);
                var light = glow.AddComponent<VisionLight>();
                light.range = Game.Balance.Chris.AmbulanceLightRadius * U;
                light.intensity = Game.Balance.Chris.AmbulanceLightIntensity;
                light.height = 1.6f;
                light.flickerAmount = 0.1f;
                light.flickerSpeed = 3.1f;
            }
            // The light bar's own light, up on the roof: it shows the roof and the ground just round the van, not far beyond
            // (its red and blue tint is laid over what it lights by MatchEffects).
            {
                var bar = new GameObject("Ambulance Light Bar");
                bar.transform.SetParent(parent, false);
                bar.transform.localPosition = Upright(at + along * (size.z * 0.3f), 0f);
                var light = bar.AddComponent<VisionLight>();
                light.range = Game.Balance.Chris.BarLightRange;
                light.intensity = Game.Balance.Chris.BarLightIntensity;
                light.height = size.y + 0.4f;
                light.flickerAmount = 0.05f;
                light.flickerSpeed = 2f;
            }
            Ambulance = van.transform;
            for (float t = -size.z * 0.5f; t <= size.z * 0.5f + 0.01f; t += 1.5f)
                for (float s = -size.x * 0.5f; s <= size.x * 0.5f + 0.01f; s += size.x * 0.5f)
                    blocked.Add(at + along * t + across * s);

            // The supplies beside it, in a row just outside the ring he paces.
            ItemType[] kit = Game.Balance.Items.AmbulanceKit;
            float gap = 44f * U;
            Transform supplies = Group("Ambulance kit");
            foreach (float side in new[] { 1f, -1f })
                foreach (float offUnits in new[] { 75f + Game.Balance.Chris.Pace + 34f, 75f + Game.Balance.Chris.Pace + 70f })
                {
                    var spots = new Vector2[kit.Length];
                    bool fine = true;
                    for (int i = 0; i < kit.Length; i++)
                    {
                        spots[i] = at + along * ((i - (kit.Length - 1) * 0.5f) * gap) + across * (offUnits * U * side);
                        if (Layout.LakeDepth(spots[i]) > -1f || blocked.AnyWithin(spots[i], 0.4f) || (paths != null && paths.Distance(spots[i]) < paths.HalfWidth * 0.5f)) fine = false;
                    }
                    if (!fine) continue;
                    for (int i = 0; i < kit.Length; i++)
                    {
                        var go = new GameObject($"Pickup {kit[i]}");
                        go.transform.SetParent(supplies, false);
                        go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(r, kit[i]);
                        PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                        var pickup = go.AddComponent<Pickup>();
                        pickup.item = kit[i];
                        pickup.count = 1;
                        Conform(go.transform, spots[i], 0.1f, angle * Mathf.Rad2Deg, 0.8f, 0f);
                        go.transform.localScale = Vector3.one * 1.6f;
                        Pickups.Add(pickup);
                        blocked.Add(spots[i]);
                    }
                    return;
                }
        }

        /// <summary>How much bigger than its model a note lies on the ground (a big sheet, easy to spot).</summary>
        public const float NoteScale = 2.2f;

        /// <summary>
        /// The paper notes, in a random order of their photos (<see cref="NotePictures"/>: note-1 to note-6, and any the user
        /// added): one beside every generator, and every photo not yet down along the paths (or, failing that, on the
        /// building's floor), so every photo always lies somewhere on the map.
        /// </summary>
        void BuildNotes()
        {
            System.Random r = FeatureRng(10);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var order = new List<int>();
            int images = NotePictures.Count;
            for (int i = 0; i < images; i++) order.Insert(r.Next(order.Count + 1), i);
            int next = 0;
            Transform parent = Group("Notes");
            var placed = new List<Vector2>();
            // Notes catch the eye: their paper glows faintly, so it reads in any light.
            var noteMat = lowPolyMaterial != null ? new Material(lowPolyMaterial) { name = "Note", hideFlags = HideFlags.DontSave } : null;
            if (noteMat != null && noteMat.HasProperty("_Emission")) noteMat.SetFloat("_Emission", 0.45f);
            void Put(Vector2 p, float? floorY)
            {
                var go = new GameObject($"Note {Notes.Count + 1}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Note(r);
                PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = noteMat != null ? noteMat : lowPolyMaterial;
                if (floorY.HasValue) go.transform.SetLocalPositionAndRotation(new Vector3(p.x, floorY.Value + 0.01f, p.y), Quaternion.Euler(0f, R(0f, 360f), 0f));
                else Conform(go.transform, p, 0.2f, R(0f, 360f), 0.9f, 0f);
                go.transform.localScale = Vector3.one * NoteScale;
                Notes.Add(go.transform);
                NoteImages.Add(order[next++ % order.Count]);
                placed.Add(p);
            }

            // Beside each generator: the first clear spot round it, on the floor indoors or on the ground outside.
            Physics.SyncTransforms();
            int solid = ~((1 << GroundLayer) | (1 << CharacterLayer) | (1 << 2) | (1 << LowLayer));
            foreach (Transform gen in Generators)
            {
                if (gen == null) continue;
                Vector3 gl = transform.InverseTransformPoint(gen.position);
                var g = new Vector2(gl.x, gl.z);
                bool inside = Layout.Building.Contains(g);
                float start = R(0f, 360f);
                Vector2 spot = g + new Vector2(Game.Balance.Notes.BesideGenerator, 0f);
                for (int k = 0; k < 16; k++)
                {
                    float a = (start + k * 22.5f) * Mathf.Deg2Rad;
                    Vector2 p = g + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Game.Balance.Notes.BesideGenerator;
                    float y = inside ? gl.y : H(p);
                    Vector3 w = transform.TransformPoint(new Vector3(p.x, y + 0.45f, p.y));
                    if (Physics.CheckSphere(w, 0.32f * transform.lossyScale.x, solid, QueryTriggerInteraction.Ignore)) continue;
                    if (!inside && (Layout.LakeDepth(p) > -1.5f || Layout.Building.Contains(p))) continue;
                    spot = p;
                    break;
                }
                Put(spot, inside ? gl.y : (float?)null);
            }

            // The rest along the paths.
            PathNetwork paths = Terrain.Paths;
            int pathNotes = Mathf.Max(0, images - Notes.Count);
            if (paths == null || paths.Paths.Count == 0)
            {
                PutIndoors(pathNotes);
                return;
            }
            Vector2 hunter = Layout.Spawn;
            float far = -1f;
            foreach (MapLayout.Clearing c in Layout.Clearings)
            {
                float d = Vector2.Distance(c.Centre, Layout.Spawn);
                if (d > far) { far = d; hunter = c.Centre; }
            }
            int target = Notes.Count, besideGens = Notes.Count;
            target += pathNotes;
            for (int tries = 0; tries < 4000 && Notes.Count < target; tries++)
            {
                List<Vector2> path = paths.Paths[r.Next(paths.Paths.Count)];
                if (path.Count < 2) continue;
                int i = r.Next(path.Count - 1);
                Vector2 a = path[i], b = path[i + 1];
                if ((b - a).sqrMagnitude < 1e-4f) continue;
                Vector2 on = Vector2.Lerp(a, b, R(0f, 1f));
                Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x) * (r.Next(2) == 0 ? 1f : -1f);
                Vector2 p = on + side * (paths.HalfWidth + R(30f, 70f) * U);
                if (p.magnitude > halfExtent - 300f * U) continue;
                if (Layout.LakeDepth(p) > -1.5f || Expand(Layout.Building, 40f * U).Contains(p) || Layout.Yard.Contains(p)) continue;
                if (Vector2.Distance(Layout.Spawn, p) < 500f * U || Vector2.Distance(hunter, p) < 500f * U) continue;
                // Far from the other path notes; half as far from those beside the generators.
                bool near = false;
                for (int k = 0; k < placed.Count && !near; k++)
                    near = Vector2.Distance(placed[k], p) < Game.Balance.Notes.Spacing * U * (k < besideGens ? 0.5f : 1f);
                if (near) continue;
                if (Vector2.Distance(AmbulanceCentre, p) < 260f * U || blocked.AnyWithin(p, 0.5f)) continue;
                bool nearLoot = false;
                foreach (Pickup k in Pickups)
                {
                    Vector3 l = transform.InverseTransformPoint(k.transform.position);
                    if (Vector2.Distance(new Vector2(l.x, l.z), p) < 90f * U) { nearLoot = true; break; }
                }
                if (nearLoot) continue;
                Put(p, null);
            }
            // Any photo the paths had no room for lies inside the building.
            PutIndoors(target - Notes.Count);

            void PutIndoors(int n)
            {
                BuildingPlan plan = Layout.Plan;
                if (plan == null || n <= 0) return;
                var spots = new List<Vector2>(plan.LootSpots);
                for (int k = spots.Count - 1; k > 0; k--)
                {
                    int j = r.Next(k + 1);
                    (spots[k], spots[j]) = (spots[j], spots[k]);
                }
                foreach (Vector2 p in spots)
                {
                    if (n <= 0) break;
                    bool near = false;
                    foreach (Vector2 q in placed) if (Vector2.Distance(q, p) < 3f) near = true;
                    if (near) continue;
                    Put(p, BuildingFloor);
                    n--;
                }
            }
        }
    }
}
