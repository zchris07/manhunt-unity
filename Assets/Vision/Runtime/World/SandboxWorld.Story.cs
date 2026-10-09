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

        /// <summary>The height of the ground a body stands on: the building's floor inside it, the terrain everywhere else.</summary>
        public float GroundHeight(Vector2 at)
        {
            if (Layout != null && Layout.Plan != null && Layout.Building.Contains(at)) return BuildingFloor + 0.015f;
            return Terrain != null ? Terrain.Height(at.x, at.y) : 0f;
        }

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
                float lim = halfExtent - 450f * U;
                if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim) return false;
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
            Ambulance = van.transform;
            for (float t = -size.z * 0.5f; t <= size.z * 0.5f + 0.01f; t += 1.5f)
                for (float s = -size.x * 0.5f; s <= size.x * 0.5f + 0.01f; s += size.x * 0.5f)
                    blocked.Add(at + along * t + across * s);

            // The supplies beside it, in a row just outside the ring he paces.
            ItemType[] kit = { ItemType.MiniShield, ItemType.MiniShield, ItemType.MrBeastBar, ItemType.MrBeastBar, ItemType.Confit };
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

        /// <summary>The Four Notes: creepy photos left beside the paths, far apart, away from the spawns, supplies and ambulance.</summary>
        void BuildNotes()
        {
            PathNetwork paths = Terrain.Paths;
            if (paths == null || paths.Paths.Count == 0) return;
            System.Random r = FeatureRng(10);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            Vector2 hunter = Layout.Spawn;
            float far = -1f;
            foreach (MapLayout.Clearing c in Layout.Clearings)
            {
                float d = Vector2.Distance(c.Centre, Layout.Spawn);
                if (d > far) { far = d; hunter = c.Centre; }
            }
            var placed = new List<Vector2>();
            Transform parent = Group("Notes");
            for (int tries = 0; tries < 4000 && placed.Count < Game.Balance.Notes.Count; tries++)
            {
                List<Vector2> path = paths.Paths[r.Next(paths.Paths.Count)];
                if (path.Count < 2) continue;
                int i = r.Next(path.Count - 1);
                Vector2 a = path[i], b = path[i + 1];
                if ((b - a).sqrMagnitude < 1e-4f) continue;
                Vector2 on = Vector2.Lerp(a, b, R(0f, 1f));
                Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x) * (r.Next(2) == 0 ? 1f : -1f);
                Vector2 p = on + side * (paths.HalfWidth + R(30f, 70f) * U);
                float lim = halfExtent - 300f * U;
                if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim) continue;
                if (Layout.LakeDepth(p) > -1.5f || Expand(Layout.Building, 40f * U).Contains(p) || Layout.Yard.Contains(p)) continue;
                if (Vector2.Distance(Layout.Spawn, p) < 500f * U || Vector2.Distance(hunter, p) < 500f * U) continue;
                if (placed.Exists(q => Vector2.Distance(q, p) < Game.Balance.Notes.Spacing * U)) continue;
                if (Vector2.Distance(AmbulanceCentre, p) < 260f * U || blocked.AnyWithin(p, 0.5f)) continue;
                bool nearLoot = false;
                foreach (Pickup k in Pickups)
                {
                    Vector3 l = transform.InverseTransformPoint(k.transform.position);
                    if (Vector2.Distance(new Vector2(l.x, l.z), p) < 90f * U) { nearLoot = true; break; }
                }
                if (nearLoot) continue;
                placed.Add(p);
                var go = new GameObject($"Note {placed.Count}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Note(r);
                PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                Conform(go.transform, p, 0.2f, R(0f, 360f), 0.9f, 0f);
                go.transform.localScale = Vector3.one * 1.6f;
                Notes.Add(go.transform);
            }
        }
    }
}
