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
            if (w.Layout != null)
            {
                map.Building = w.Layout.Building;
                Rect yard = w.Layout.Yard;
                // The yard beyond the gate: walking into it (past the gate's line) escapes.
                map.ExitZone = new Rect(yard.xMin, yard.yMin + 1.2f, yard.width, yard.height);
            }
            foreach (Pickup p in w.Pickups)
                if (p != null) map.Loot.Add(new SimMap.LootDef { Pos = Local(p.transform.position), Item = p.item, Golden = p.golden });
            foreach (HidingSpot h in w.HidingSpots)
                map.HidingSpots.Add(new SimMap.HideDef { Pos = Local(h.transform.position), Exit = Local(h.ExitPosition), Kind = h.kind, Reach = h.reach });
            foreach (Door d in w.Doors) map.Doors.Add(new SimMap.DoorDef { A = d.a, B = d.b, StartsOpen = d.IsOpen, Shutter = d.blocksMovementWhenOpen });
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
                // Survivors start together in the south clearing; Zach in the clearing farthest from them.
                Vector2 spawn = w.Layout.Spawn;
                for (int i = 0; i < Balance.Net.MaxPlayers; i++)
                {
                    float a = i * Mathf.PI * 2f / Balance.Net.MaxPlayers;
                    map.SurvivorSpawns.Add(spawn + (i == 0 ? Vector2.zero : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f));
                }
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
            }
            return map;
        }
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

        readonly System.Collections.Generic.Dictionary<(int, bool), NavGrid> navs = new System.Collections.Generic.Dictionary<(int, bool), NavGrid>();

        /// <summary>
        /// Built once per radius from the level's colliders (doors set open for the door-opening kind), with physics in
        /// sync with the transforms.
        /// </summary>
        public NavGrid Nav(float radiusUnits, bool doorsOpen = true)
        {
            var key = (Mathf.RoundToInt(radiusUnits), doorsOpen);
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
            n = new NavGrid(world.halfExtent, Scale.D(25f), p => Blocked(p, r));
            foreach (var (c, was) in restore) c.enabled = was;
            navs[key] = n;
            return n;
        }

        public bool InExitZone(Vector2 p) => world.Layout != null && new Rect(world.Layout.Yard.xMin, world.Layout.Yard.yMin + 1.2f, world.Layout.Yard.width, world.Layout.Yard.height).Contains(p);

        public bool InWater(Vector2 p) => TerrainField.InWaterAt(root.TransformPoint(new Vector3(p.x, 0f, p.y)));

        public bool InBrokenWindow(Vector2 p, float radius)
        {
            if (BrokenWindows == null) return false;
            for (int i = 0; i < world.Windows.Count && i < BrokenWindows.Length; i++)
                if (BrokenWindows[i] && SimMap.SegmentDistance(p, world.Windows[i].a, world.Windows[i].b) < radius) return true;
            return false;
        }
    }
}
