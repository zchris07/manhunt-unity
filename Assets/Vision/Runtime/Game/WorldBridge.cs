using System.Collections.Generic;
using UnityEngine;
using Vision.Visibility;
using Vision.World;

namespace Vision.Game
{
    /// <summary>Reads a generated level into the <see cref="SimMap"/> the match rules use (everything in build order).</summary>
    public static class SimMapBuilder
    {
        /// <param name="stakes">How many of the level's stakes the match uses (the original's survivors + 4, 6 to 12).</param>
        public static SimMap Build(SandboxWorld w, int stakes = SandboxWorld.MaxStakes)
        {
            var map = new SimMap { HalfExtent = w.halfExtent };
            Transform root = w.transform;
            Vector2 Local(Vector3 world)
            {
                Vector3 l = root.InverseTransformPoint(world);
                return new Vector2(l.x, l.z);
            }

            foreach (Transform g in w.Generators) map.Generators.Add(Local(g.position));
            if (w.Gate != null)
            {
                map.HasGate = true;
                map.Lever = w.Gate.lever != null ? Local(w.Gate.lever.position) : Local(w.Gate.transform.position);
                map.GatePos = Local(w.Gate.transform.position);
            }
            if (w.Gate2 != null && w.Layout != null && w.Layout.HasSushiGate)
            {
                // The second exit, beside Miga Sushi.
                map.HasGate2 = true;
                map.Lever2 = Local(w.Gate2.lever != null ? w.Gate2.lever.position : w.Gate2.transform.position);
                map.GatePos2 = w.Layout.SushiGateMid();
                map.ExitZone2 = w.Layout.SushiExitZone();
            }
            // A sacrificed survivor's ghost comes back in Miga Sushi or the Diddy Chamber.
            if (w.Layout != null)
                foreach (Rect r in new[] { w.Layout.Sushi, w.Layout.Chamber })
                    if (r.width > 0f && r.height > 0f) map.GhostSpawns.Add(r.center);
            if (w.Hatch != null)
            {
                map.HasHatch = true;
                map.Hatch = Local(w.Hatch.transform.position);
            }
            if (w.Layout != null)
            {
                map.Building = w.Layout.Building;
                Rect yard = w.Layout.Yard;
                // The yard beyond the gate: walking into it (past the gate's line) escapes.
                map.ExitZone = new Rect(yard.xMin, yard.yMin + 1.2f, yard.width, yard.height);
            }
            foreach (Pickup p in w.Pickups)
                if (p != null) map.Loot.Add(new SimMap.LootDef { Pos = Local(p.transform.position), Item = p.item, Golden = p.golden });
            foreach (Door d in w.Doors) map.Doors.Add(new SimMap.DoorDef { A = d.a, B = d.b, StartsOpen = d.IsOpen, Shutter = d.blocksMovementWhenOpen, Gate = d.gate });
            foreach (Barricade b in w.BarricadeList) map.Barricades.Add(new SimMap.BarricadeDef { A = b.a, B = b.b, Pos = (b.a + b.b) * 0.5f });
            foreach (WindowPiece win in w.Windows) map.Windows.Add(new SimMap.WindowDef { A = win.a, B = win.b });
            for (int i = 0; i < w.Stakes.Count; i++)
            {
                bool used = i < stakes;
                // The ones the match doesn't use are taken away.
                if (w.Stakes[i] != null && w.Stakes[i].gameObject.activeSelf != used) w.Stakes[i].gameObject.SetActive(used);
                if (used) map.Stakes.Add(Local(w.Stakes[i].position));
            }
            foreach (Transform n in w.Notes) map.Notes.Add(Local(n.position));
            map.NoteImages.AddRange(w.NoteImages);
            if (w.Ambulance != null)
            {
                map.AmbulanceAt = w.AmbulanceCentre;
                map.AmbulanceAngle = w.AmbulanceAngle;
                map.AmbulanceSize = new Vector2(World.LowPolyModels.AmbulanceSize.z, World.LowPolyModels.AmbulanceSize.x);
            }
            if (w.Layout != null && w.Layout.Plan != null && w.Layout.Plan.LoungeRoom >= 0)
            {
                map.LoungeSeat = w.Layout.Plan.LoungeSeat;
                map.LoungeTv = w.Layout.Plan.LoungeTv;
            }

            if (w.Layout != null)
            {
                // Zach starts in the clearing farthest from the spawn clearing; the survivors each somewhere of their own,
                // scattered over the map and away from each other (and from him).
                Vector2 spawn = w.Layout.Spawn;
                Vector2 far = spawn;
                float best = -1f;
                foreach (MapLayout.Clearing c in w.Layout.Clearings)
                {
                    float d = Vector2.Distance(c.Centre, spawn);
                    if (d > best) { best = d; far = c.Centre; }
                }
                for (int i = 0; i < 9; i++)
                {
                    float a = i * Mathf.PI * 2f / 9f;
                    map.HunterSpawns.Add(far + (i == 0 ? Vector2.zero : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f));
                }
                map.SurvivorSpawns.AddRange(ScatteredSpawns(w, far, Balance.Net.MaxPlayers));
            }
            return map;
        }

        /// <summary>
        /// Survivors' starting spots: <paramref name="count"/> points over the playable map, each on open, walkable ground
        /// (not in the lake, the building, a site or anything solid), well away from Zach's start and from each other (a
        /// Poisson-disk spread: at least <see cref="SpawnApart"/> m apart, relaxed a little at a time if the map can't fit
        /// them). Seeded by the level, so every machine agrees.
        /// </summary>
        public static List<Vector2> ScatteredSpawns(SandboxWorld w, Vector2 hunter, int count)
        {
            var spots = new List<Vector2>();
            MapLayout L = w.Layout;
            var rng = new System.Random(L.Seed * 977 + 5);
            float lim = L.HalfExtent - 8f, apart = Mathf.Min(SpawnApart, L.HalfExtent * 1.6f / Mathf.Sqrt(Mathf.Max(1, count)));
            int solid = ~((1 << SandboxWorld.GroundLayer) | (1 << SandboxWorld.CharacterLayer) | (1 << 2) | (1 << 10) | (1 << 11));
            bool Open(Vector2 p)
            {
                if (p.magnitude > lim || L.LakeDepth(p) > -1f || L.Blocked(p)) return false;
                if (Vector2.Distance(p, hunter) < SpawnFromHunter) return false;
                Rect b = L.Building;
                if (p.x > b.xMin - 6f && p.x < b.xMax + 6f && p.y > b.yMin - 6f && p.y < b.yMax + 6f) return false;
                Vector3 at = w.transform.TransformPoint(new Vector3(p.x, w.SurfaceHeight(p) + 1f, p.y));
                return !Physics.CheckSphere(at, 0.8f * w.transform.lossyScale.x, solid, QueryTriggerInteraction.Ignore);
            }
            for (int tries = 0; spots.Count < count && tries < 20000; tries++)
            {
                if (tries > 0 && tries % 2000 == 0) apart *= 0.85f;
                var p = new Vector2((float)(rng.NextDouble() * 2.0 - 1.0) * lim, (float)(rng.NextDouble() * 2.0 - 1.0) * lim);
                if (!Open(p)) continue;
                bool near = false;
                foreach (Vector2 q in spots) if ((q - p).sqrMagnitude < apart * apart) { near = true; break; }
                if (!near) spots.Add(p);
            }
            // (Never short: any spots still missing go by the spawn clearing.)
            for (int i = spots.Count; i < count; i++) spots.Add(L.Spawn + new Vector2(Mathf.Cos(i), Mathf.Sin(i)) * 1.6f);
            return spots;
        }

        /// <summary>How far apart survivors start (m) at most, and how far from Zach at least.</summary>
        public const float SpawnApart = 30f, SpawnFromHunter = 35f;
    }

    /// <summary>
    /// The level's geometry for the match rules: sight against the occluders (walls, trees, rocks, closed doors), bodies
    /// against the colliders (not the terrain or characters), water from the terrain, windows from the level. All in design
    /// units on the level's ground plane.
    /// </summary>
    public sealed class WorldGeometry : ISimGeometry
    {
        readonly SandboxWorld world;
        readonly Transform root;
        readonly float scale;
        int mask;
        /// <summary>Which windows are smashed (the match keeps it up to date).</summary>
        public bool[] BrokenWindows;

        public WorldGeometry(SandboxWorld world)
        {
            this.world = world;
            root = world.transform;
            scale = root.lossyScale.x;
            mask = ~((1 << SandboxWorld.GroundLayer) | (1 << SandboxWorld.CharacterLayer) | (1 << 2));
        }

        Vector2 ToPlane(Vector2 local) => VisionWorld.ToPlane(root.TransformPoint(new Vector3(local.x, 0f, local.y)));

        Vector3 ToWorld(Vector2 local, float up)
        {
            float h = world.Terrain != null ? world.Terrain.Height(local.x, local.y) : 0f;
            return root.TransformPoint(new Vector3(local.x, h + up, local.y));
        }

        public bool LineOfSight(Vector2 a, Vector2 b) => !VisionWorld.Occluders.Blocks(ToPlane(a), ToPlane(b));

        public float CastSight(Vector2 from, Vector2 dir, float max)
        {
            if (dir.sqrMagnitude < 1e-8f) return max;
            Vector2 o = ToPlane(from), e = ToPlane(from + dir * max);
            Vector2 d = e - o;
            float len = d.magnitude;
            if (len < 1e-6f) return max;
            return VisionWorld.Occluders.Raycast(o, d / len, len) / scale;
        }

        public float CastBody(Vector2 from, Vector2 dir, float max, float radius)
        {
            if (dir.sqrMagnitude < 1e-8f || max <= 0f) return max;
            Vector3 o = ToWorld(from, 0.6f);
            Vector3 d = root.TransformDirection(new Vector3(dir.x, 0f, dir.y)).normalized;
            return Physics.SphereCast(o, radius * scale, d, out RaycastHit hit, max * scale, mask, QueryTriggerInteraction.Ignore) ? hit.distance / scale : max;
        }

        public bool Blocked(Vector2 p, float radius)
        {
            Vector3 lo = ToWorld(p, 0.4f), hi = ToWorld(p, 1.4f);
            return Physics.CheckCapsule(lo, hi, radius * scale, mask, QueryTriggerInteraction.Ignore);
        }

        readonly Collider[] overlap = new Collider[24];
        System.Collections.Generic.HashSet<Collider> windowParts;

        /// <summary>A window's sill, glass or boards (a climbing body goes through them).</summary>
        bool IsWindow(Collider c)
        {
            if (windowParts == null)
            {
                windowParts = new System.Collections.Generic.HashSet<Collider>();
                foreach (WindowPiece w in world.Windows)
                {
                    if (w == null) continue;
                    if (w.glass != null) windowParts.Add(w.glass);
                    if (w.sill != null) windowParts.Add(w.sill);
                    if (w.boards != null) foreach (Collider b in w.boards.GetComponentsInChildren<Collider>(true)) windowParts.Add(b);
                }
            }
            return windowParts.Contains(c);
        }

        public bool BlockedClimbing(Vector2 p, float radius)
        {
            Vector3 lo = ToWorld(p, 0.4f), hi = ToWorld(p, 1.4f);
            int climb = mask & ~((1 << SandboxWorld.LowLayer) | (1 << SandboxWorld.VaultLayer));
            int n = Physics.OverlapCapsuleNonAlloc(lo, hi, radius * scale, overlap, climb, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!IsWindow(overlap[i])) return true;
            return false;
        }

        readonly System.Collections.Generic.Dictionary<(int, bool, bool), NavGrid> navs = new System.Collections.Generic.Dictionary<(int, bool, bool), NavGrid>();

        /// <summary>
        /// Built once per radius from the level's colliders (doors set open for the door-opening kind; logs, fences and
        /// windows passable for the climbing kind), with physics in sync with the transforms.
        /// </summary>
        public NavGrid Nav(float radiusUnits, bool doorsOpen = true, bool climbs = false)
        {
            var key = (Mathf.RoundToInt(radiusUnits), doorsOpen, climbs);
            if (navs.TryGetValue(key, out NavGrid n)) return n;
            var restore = new System.Collections.Generic.List<(Collider c, bool was)>();
            if (doorsOpen)
                foreach (Door d in world.Doors)
                    if (d != null && d.blocker != null && !d.blocksMovementWhenOpen)
                    {
                        restore.Add((d.blocker, d.blocker.enabled));
                        d.blocker.enabled = false;
                    }
            Physics.SyncTransforms();
            float r = Scale.D(radiusUnits);
            n = new NavGrid(world.halfExtent, Scale.D(25f), p => climbs ? BlockedClimbing(p, r) : Blocked(p, r));
            foreach (var (c, was) in restore) c.enabled = was;
            navs[key] = n;
            return n;
        }

        public bool InExitZone(Vector2 p) => world.Layout != null && new Rect(world.Layout.Yard.xMin, world.Layout.Yard.yMin + 1.2f, world.Layout.Yard.width, world.Layout.Yard.height).Contains(p);

        public bool InWater(Vector2 p) => TerrainField.InWaterAt(root.TransformPoint(new Vector3(p.x, 0f, p.y)));

        public bool OverLow(Vector2 p, float radius)
        {
            foreach (SandboxWorld.LowObstacle o in world.Lows)
                if (SimMap.SegmentDistance(p, o.A, o.B) < o.Half + radius) return true;
            return false;
        }

        public float Vault(Vector2 p, float radius, bool hunter)
        {
            float v = 0f;
            foreach (SandboxWorld.LowObstacle o in world.Lows)
                if (SimMap.SegmentDistance(p, o.A, o.B) < o.Half + radius) v = Mathf.Max(v, MatchSim.VaultFor(o.Slow));
            return v;
        }

        public bool InBrokenWindow(Vector2 p, float radius)
        {
            if (BrokenWindows == null) return false;
            for (int i = 0; i < world.Windows.Count && i < BrokenWindows.Length; i++)
                if (BrokenWindows[i] && SimMap.SegmentDistance(p, world.Windows[i].a, world.Windows[i].b) < radius) return true;
            return false;
        }
    }
}
