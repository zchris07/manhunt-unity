using System.Collections.Generic;
using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// Every generator's work site, laid out by hand (one fixed arrangement, turned with the generator): three trailer
    /// light towers as tall as a big tree round it, each wired to it by a cable winding and looping over the ground; four
    /// yellow work lights about it, wired too; and the mess of a work site gone to seed: broken glass, planks, a sheet of
    /// corrugated metal, a cable spool, a jerry can, a toolbox, a small portable generator, loose wires, and ferns and
    /// grass grown up through it all. The graveyard's is bare: the generator, two towers either side of it on the walk
    /// between the graves (<see cref="GraveTowerSpots"/>) and their two cables, nothing else. Dark until the generator
    /// runs, then on for good: each tower lights the ground all round it as brightly as a player's own light, the work
    /// lights a little round each. The building's generators have no work site: either one running lights the whole
    /// building, every room (<see cref="BuildingPower"/>).
    /// Anything that would stand in a wall, a tree or a rock moves to the nearest clear ground (the towers of a generator
    /// inside the building stand outside it, near its room).
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>A tower's light: how far round it (design units), lit as brightly as a player's own light.</summary>
        public const float TowerLightRange = 13f, WorkLightRange = 3.4f;

        enum Clutter { Glass, Planks, MetalSheet, Spool, JerryCan, ToolBox, PortableGenerator, Overgrowth, BigOvergrowth }

        // The layout, in metres from the generator: x to its right, z to its front; yaws in degrees from its front.
        static readonly (float x, float z)[] TowerSpots = { (-5.2f, 3.4f), (5.4f, 2.2f), (0.6f, -5.8f) };
        static readonly (float x, float z, int kind, float yaw)[] WorkSpots =
            { (-2.4f, -2.9f, 3, 30f), (3.1f, -2.6f, 0, -35f), (-3.6f, 0.2f, 2, 80f), (2.2f, 4.4f, 1, 200f) };
        static readonly (float x, float z, Clutter what, float yaw)[] ClutterSpots =
        {
            (-1.9f, 1.8f, Clutter.Glass, 0f), (3.6f, 0.3f, Clutter.Glass, 70f),
            (-3.9f, -2.0f, Clutter.Planks, 35f), (0.2f, 3.2f, Clutter.MetalSheet, 100f), (-1.5f, -1.2f, Clutter.Spool, 60f),
            (1.4f, 1.3f, Clutter.JerryCan, 15f), (-0.9f, 2.5f, Clutter.ToolBox, -25f), (4.2f, -1.4f, Clutter.PortableGenerator, 140f),
            (-4.6f, 1.6f, Clutter.BigOvergrowth, 0f), (3.9f, 3.6f, Clutter.Overgrowth, 0f), (-2.6f, -4.4f, Clutter.BigOvergrowth, 0f),
            (2.4f, -4.6f, Clutter.Overgrowth, 0f), (-5.6f, -0.9f, Clutter.Overgrowth, 0f), (5.8f, -0.4f, Clutter.BigOvergrowth, 0f),
            (0.8f, -3.4f, Clutter.Glass, 210f),
        };
        // Each cable: where it runs (a tower 0-2, a work light 3-6, or a loose end at a point), and how it winds: how far
        // it sways, how many times, where it throws a loop (part of the way along; 0 none).
        static readonly (int to, float sway, float waves, float loop)[] CableRuns =
        {
            (0, 0.55f, 2.5f, 0.45f), (1, 0.7f, 3f, 0.6f), (2, 0.5f, 2f, 0.35f), (3, 0.3f, 1.5f, 0f), (4, 0.35f, 2f, 0.5f), (5, 0.4f, 1.5f, 0f), (6, 0.3f, 2f, 0f),
        };
        static readonly (float x, float z)[] LooseEnds = { (-1.2f, -3.6f), (3.4f, 1.8f) };

        readonly List<(GameObject gen, GeneratorObjective objective)> pendingSets = new List<(GameObject, GeneratorObjective)>();

        /// <summary>Builds every generator's work site (after the sites, so it sees everything already standing).</summary>
        void BuildFloodSets()
        {
            Physics.SyncTransforms();
            var indoors = new List<GeneratorObjective>();
            foreach (var (gen, objective) in pendingSets)
            {
                if (gen == null) continue;
                Vector3 l = transform.InverseTransformPoint(gen.transform.position);
                if (Layout.Building.Contains(new Vector2(l.x, l.z))) indoors.Add(objective);
                else BuildFloodSet(gen, objective);
            }
            pendingSets.Clear();
            if (indoors.Count > 0) BuildBuildingLights(indoors);
        }

        /// <summary>The building's ceiling lights are spaced about this far apart (metres), room by room.</summary>
        public const float RoomLightSpacing = 7f;

        /// <summary>
        /// The building's ceiling lights: every room (hallways too) lit by a grid of lights, each reaching past the corners
        /// of its share of the room, all dark until one of the building's generators runs.
        /// </summary>
        void BuildBuildingLights(List<GeneratorObjective> generators)
        {
            BuildingPlan plan = Layout.Plan;
            if (plan == null) return;
            Transform root = Group("Building Lights");
            BuildingPower power = root.gameObject.AddComponent<BuildingPower>();
            power.generators.AddRange(generators);
            foreach (BuildingPlan.Room r in plan.Rooms)
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt(r.Area.width / RoomLightSpacing)), nz = Mathf.Max(1, Mathf.CeilToInt(r.Area.height / RoomLightSpacing));
                float cw = r.Area.width / nx, ch = r.Area.height / nz;
                float range = Mathf.Sqrt(cw * cw + ch * ch) * 0.5f + 1.5f;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        var go = new GameObject("Room Light");
                        go.transform.SetParent(root, false);
                        go.transform.localPosition = new Vector3(r.Area.xMin + cw * (i + 0.5f), BuildingFloor, r.Area.yMin + ch * (j + 0.5f));
                        var light = go.AddComponent<VisionLight>();
                        light.range = range;
                        light.height = 2.2f;
                        light.intensity = 0.9f;
                        light.falloffStart = 0.75f;
                        light.flickerAmount = 0.01f;
                        light.enabled = false;
                        power.lights.Add(light);
                    }
            }
        }

        void BuildFloodSet(GameObject gen, GeneratorObjective objective)
        {
            Transform parent = Group("Generator Work Site");
            Vector3 gl = transform.InverseTransformPoint(gen.transform.position);
            Vector3 f3 = transform.InverseTransformDirection(gen.transform.forward);
            var fwd = new Vector2(f3.x, f3.z);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector2.up;
            var right = new Vector2(fwd.y, -fwd.x);
            var g = new Vector2(gl.x, gl.z);
            float floorY = gl.y;
            Rect building = Layout.Building;
            bool Indoors(Vector2 p) => building.Contains(p);
            bool genIndoors = Indoors(g);
            float Y(Vector2 p) => Indoors(p) ? floorY : H(p);
            Vector2 At(float x, float z) => g + right * x + fwd * z;
            float YawTo(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            float s = transform.lossyScale.x;
            int solid = ~((1 << GroundLayer) | (1 << CharacterLayer) | (1 << 2) | (1 << LowLayer) | (1 << 10) | (1 << 11));
            bool Clear(Vector2 p, float half, float height)
            {
                if (Layout.LakeDepth(p) > -1f) return false;
                Vector3 c = transform.TransformPoint(new Vector3(p.x, Y(p) + height * 0.5f + 0.1f, p.y));
                return !Physics.CheckBox(c, new Vector3(half, height * 0.5f, half) * s, transform.rotation, solid, QueryTriggerInteraction.Ignore);
            }
            var lampOn = glowMaterial != null ? glowMaterial : lowPolyMaterial;
            // The graveyard's: two towers on the walk between the graves, and only their cables.
            bool graveyard = Layout.Graveyard.width > 0f && (g - Layout.Graveyard.center).sqrMagnitude < 0.25f;
            var spots = new List<(Vector2 at, bool fixedSpot)>();
            if (graveyard) foreach (Vector2 t in GraveTowerSpots) spots.Add((GraveLocal(t.x, t.y), true));
            else foreach (var (tx, tz) in TowerSpots) spots.Add((At(tx, tz), false));

            // The towers: at their places, or the nearest clear ground outside (round the generator, further out); the
            // graveyard's where its graves leave room for them.
            var towers = new List<Vector2>();
            foreach (var (want, fixedSpot) in spots)
            {
                Vector2 spot = want;
                bool Good(Vector2 p) => !(building.xMin - 1.8f < p.x && p.x < building.xMax + 1.8f && building.yMin - 1.8f < p.y && p.y < building.yMax + 1.8f)
                    && Clear(p, 1.3f, 3f) && towers.TrueForAll(o => (o - p).sqrMagnitude > 9f);
                if (!fixedSpot && !Good(spot))
                {
                    float a0 = Mathf.Atan2(want.y - g.y, want.x - g.x), r0 = (want - g).magnitude;
                    bool found = false;
                    for (float r = r0; r < r0 + 24f && !found; r += 1f)
                        for (int k = 0; k < 24 && !found; k++)
                        {
                            float a = a0 + (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * Mathf.PI / 12f;
                            Vector2 p = g + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            if (Good(p)) { spot = p; found = true; }
                        }
                    if (!found) continue;
                }
                towers.Add(spot);
                float yaw = YawTo(g - spot) + 180f;
                GameObject t = MakeStatic("Light Tower", LowPolyModels.LightTower(rng), new Vector3(spot.x, Y(spot), spot.y), Quaternion.Euler(0f, yaw, 0f), lowPolyMaterial);
                AddBox(t, new Vector3(0f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 2.6f));
                AddBox(t, new Vector3(0f, LowPolyModels.TowerHeight * 0.5f, -0.68f), new Vector3(0.3f, LowPolyModels.TowerHeight, 0.3f));
                t.transform.SetParent(parent, true);
                t.SetActive(true);
                GameObject lamps = MakeStatic("Light Tower Lamps", LowPolyModels.LightTowerFaces(rng), new Vector3(spot.x, Y(spot), spot.y), Quaternion.Euler(0f, yaw, 0f), lowPolyMaterial);
                lamps.isStatic = false;
                lamps.transform.SetParent(t.transform, true);
                lamps.SetActive(true);
                objective.lamps.Add(lamps.GetComponent<MeshRenderer>());
                var lightGo = new GameObject("Light Tower Light");
                lightGo.transform.SetParent(t.transform, false);
                var light = lightGo.AddComponent<VisionLight>();
                light.range = TowerLightRange;
                light.height = LowPolyModels.TowerHeight;
                light.intensity = 1f;
                light.bright = true;
                light.falloffStart = 0.72f;
                light.flickerAmount = 0.015f;
                light.flickerSpeed = 3f;
                light.enabled = false;
                objective.floods.Add(light);
                blocked.Add(spot);
            }

            // The work lights, where they're clear.
            var works = new List<Vector2?>();
            foreach (var (wx, wz, kind, wyaw) in WorkSpots)
            {
                if (graveyard) break;
                Vector2 p = At(wx, wz);
                if (!Clear(p, 0.4f, kind >= 2 ? 1.8f : 0.6f)) { works.Add(null); continue; }
                works.Add(p);
                float yaw = YawTo(fwd) + wyaw;
                GameObject w = MakeStatic("Work Light", LowPolyModels.WorkLight(rng, kind), new Vector3(p.x, Y(p), p.y), Quaternion.Euler(0f, yaw, 0f), lowPolyMaterial);
                w.transform.SetParent(parent, true);
                w.SetActive(true);
                GameObject faces = MakeStatic("Work Light Lamps", LowPolyModels.WorkLightFaces(rng, kind), new Vector3(p.x, Y(p), p.y), Quaternion.Euler(0f, yaw, 0f), lowPolyMaterial);
                faces.isStatic = false;
                faces.transform.SetParent(w.transform, true);
                faces.SetActive(true);
                objective.lamps.Add(faces.GetComponent<MeshRenderer>());
                var lightGo = new GameObject("Work Light Light");
                lightGo.transform.SetParent(w.transform, false);
                var light = lightGo.AddComponent<VisionLight>();
                light.range = WorkLightRange;
                light.height = kind >= 2 ? 1.9f : 0.3f;
                light.intensity = 0.75f;
                light.flickerAmount = 0.03f;
                light.enabled = false;
                objective.floods.Add(light);
                blocked.Add(p);
            }
            objective.lampOn = lampOn;
            objective.lampOff = lowPolyMaterial;

            // What lies about.
            foreach (var (cx, cz, what, cyaw) in ClutterSpots)
            {
                if (graveyard) break;
                Vector2 p = At(cx, cz);
                bool green = what == Clutter.Overgrowth || what == Clutter.BigOvergrowth;
                if ((green && Indoors(p)) || !Clear(p, 0.3f, 0.4f)) continue;
                Mesh m = what switch
                {
                    Clutter.Glass => LowPolyModels.GlassShards(rng),
                    Clutter.Planks => LowPolyModels.Planks(rng),
                    Clutter.MetalSheet => LowPolyModels.MetalSheet(rng),
                    Clutter.Spool => LowPolyModels.CableSpool(rng),
                    Clutter.JerryCan => LowPolyModels.JerryCan(rng),
                    Clutter.ToolBox => LowPolyModels.ToolBox(rng),
                    Clutter.PortableGenerator => LowPolyModels.PortableGenerator(rng),
                    Clutter.BigOvergrowth => LowPolyModels.Overgrowth(rng, 1.5f),
                    _ => LowPolyModels.Overgrowth(rng, 1f),
                };
                GameObject c = MakeStatic(what.ToString(), m, new Vector3(p.x, Y(p), p.y), Quaternion.Euler(0f, YawTo(fwd) + cyaw, 0f), lowPolyMaterial);
                c.transform.SetParent(parent, true);
                c.SetActive(true);
            }

            // The cables, winding from the generator to each light (and two loose ends), looping where they tangle. Out
            // of the building, a cable stops at its wall (it runs in under the door).
            var cableColor = new Color(0.07f, 0.07f, 0.07f);
            void Cable(Vector2 to, float sway, float waves, float loop, float phase)
            {
                Vector2 a = g + (to - g).normalized * 0.6f, d = to - a;
                float len = d.magnitude;
                if (len < 0.5f) return;
                Vector2 u = d / len, n = new Vector2(-u.y, u.x);
                var pts = new List<Vector3>();
                int steps = Mathf.Max(4, Mathf.CeilToInt(len / 0.3f));
                bool wasIn = genIndoors;
                void Add(Vector2 p, float lift)
                {
                    // Crossing the building's wall: the piece so far ends there, and the cable carries on from the far side.
                    bool inside = Indoors(p);
                    if (inside != wasIn) { BuildCablePiece(pts, cableColor, parent); pts.Clear(); wasIn = inside; }
                    pts.Add(new Vector3(p.x, Y(p) + lift, p.y));
                }
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector2 p = a + d * t + n * (sway * Mathf.Sin(t * Mathf.PI * waves + phase) * Mathf.Sin(t * Mathf.PI));
                    Add(p, 0.03f);
                    if (loop > 0f && Mathf.Abs(t - loop) < 0.5f / steps)
                        // A loop of slack lying over itself.
                        for (int k = 1; k <= 10; k++)
                        {
                            float q = k / 10f * Mathf.PI * 2f;
                            Add(p + n * 0.35f + (u * Mathf.Sin(q) - n * Mathf.Cos(q)) * 0.35f, 0.03f + 0.025f * Mathf.Sin(q * 0.5f));
                        }
                }
                BuildCablePiece(pts, cableColor, parent);
            }
            for (int i = 0; i < CableRuns.Length; i++)
            {
                var (to, sway, waves, loop) = CableRuns[i];
                Vector2? end = to < 3 ? (to < towers.Count ? towers[to] : (Vector2?)null) : to - 3 < works.Count ? works[to - 3] : null;
                if (end.HasValue) Cable(end.Value, sway, waves, loop, i * 1.7f);
            }
            if (!graveyard) foreach (var (lx, lz) in LooseEnds) Cable(At(lx, lz), 0.4f, 2.5f, 0.6f, lx);
        }

        void BuildCablePiece(List<Vector3> pts, Color color, Transform parent)
        {
            if (pts.Count < 2) return;
            GameObject c = MakeStatic("Cable", LowPolyModels.Cable(rng, pts, 0.028f, color), Vector3.zero, Quaternion.identity, lowPolyMaterial);
            c.transform.SetParent(parent, true);
            c.SetActive(true);
        }
    }
}
