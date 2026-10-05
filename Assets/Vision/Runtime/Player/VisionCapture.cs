using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Vision.Characters;
using Vision.Rendering;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// Automated visual check. Launch a player build with <c>-visionCapture &lt;folder&gt;</c> and it
    /// stages a fixed set of situations, saves a screenshot of each and quits. Does nothing otherwise.
    /// </summary>
    public sealed class VisionCapture : MonoBehaviour
    {
        public SandboxWorld world;
        public TopDownCamera cameraRig;
        public VisionComposite composite;
        public VisionDebugHud hud;
        public GameHud gameHud;

        string folder;

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-visionCapture") folder = args[i + 1];
            if (folder == null) return;
            Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (hud != null) hud.visible = false;
            if (gameHud != null) gameHud.visible = false;
            composite.look = VisionComposite.Look.Defaults;   // ignore look values saved by a play session
            PlayerController player = world.Player;
            Wanderer wanderer = world.Wanderer;
            wanderer.enabled = false;
            MapLayout L = world.Layout;
            Vector3 away = new Vector3(L.Spawn.x + 60f, 0f, L.Spawn.y);
            Vector3 V(Vector2 p, float dx = 0f, float dz = 0f) => new Vector3(p.x + dx, 0f, p.y + dz);
            float ortho = cameraRig.orthographicSize;

            // 1. Spawn view, flashlight north into the woods; a forest view; debug views of the first.
            yield return Stage(player, V(L.Spawn), new Vector2(0.2f, 1f), wanderer, away);
            yield return Shot("01_spawn_cone");
            composite.debugView = VisionComposite.DebugView.MaskRgb;
            yield return Wait(5);
            yield return Shot("10_debug_mask_rgb");
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            yield return Wait(5);
            yield return Shot("11_debug_scene_only");
            composite.debugView = VisionComposite.DebugView.Final;
            MapLayout.Clearing woods = L.Clearings[L.Clearings.Count > 2 ? 2 : 1];
            yield return Stage(player, V(woods.Centre, -woods.Radius - 3f), new Vector2(-1f, 0.15f), wanderer, away);
            yield return Shot("02_forest_cone");

            // 2. A cabin: outside its closed door, then open; inside facing a window.
            MapLayout.Cabin cabin = L.Cabins[0];
            Door door = null;
            float best = float.MaxValue;
            foreach (Door d in world.Doors)
            {
                Vector3 lp = world.transform.InverseTransformPoint(d.transform.position);
                float dist = (new Vector2(lp.x, lp.z) - cabin.Area.center).sqrMagnitude;
                if (dist < best) { best = dist; door = d; }
            }
            Vector2 outward = cabin.DoorSide switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
            Vector2 doorAt = cabin.Area.center + outward * ((cabin.DoorSide % 2 == 0 ? cabin.Area.height : cabin.Area.width) * 0.5f);
            if (door != null)
            {
                door.SetOpen(false);
                yield return Stage(player, V(doorAt + outward * 2.4f), -outward, wanderer, away);
                yield return Shot("03_cabin_door_closed");
                door.SetOpen(true);
                yield return Wait(30);
                yield return Shot("04_cabin_door_open");
            }
            yield return Stage(player, V(cabin.Area.center - outward * 0.5f), new Vector2(outward.y, -outward.x), wanderer, away);
            yield return Shot("07_inside_cabin");
            player.viewer.seeThroughEnabled = true;
            yield return Stage(player, V(doorAt + outward * 2.5f + new Vector2(outward.y, -outward.x) * 2f), -outward, wanderer, away);
            yield return Shot("09_see_through_cone");
            player.viewer.seeThroughEnabled = false;
            if (door != null) door.SetOpen(false);

            // 3. Entity behind the player by a campfire: lit ground, but it stays invisible until the light finds it.
            if (L.Campfires.Count > 0)
            {
                Vector2 fire = L.Campfires[0];
                yield return Stage(player, V(fire, 1.2f, 0.8f), Vector2.up, wanderer, V(fire, 1.2f, -1.8f));
                yield return Shot("05_entity_behind_by_fire");
                player.AimOverride = Vector2.down;
                yield return Wait(10);
                yield return Shot("06_entity_turned_towards");
                cameraRig.orthographicSize = 3.2f;
                yield return Stage(player, V(fire, 1.4f, 1.2f), new Vector2(-0.3f, -1f), wanderer, V(fire, -1.6f, -1.4f));
                yield return Shot("16_closeup_campfire");
                composite.debugView = VisionComposite.DebugView.Shadows;
                yield return Wait(3);
                yield return Shot("16b_campfire_shadow_mask");
                composite.debugView = VisionComposite.DebugView.Final;
                cameraRig.orthographicSize = 2.2f;
                yield return Stage(player, V(fire, -1.3f, 0f), new Vector2(1f, 0.2f), wanderer, away);
                for (int f = 0; f < 3; f++)
                {
                    yield return Wait(9);
                    yield return Shot($"37_fire_frame_{f}");
                }
                cameraRig.orthographicSize = 4.5f;
                yield return Stage(player, V(fire, 3f, 0.5f), Vector2.right, wanderer, V(fire, -2.4f, 1.2f));
                yield return Shot("29_campfire_soft_shadows");
                composite.debugView = VisionComposite.DebugView.MaskRgb;
                yield return Wait(5);
                yield return Shot("29b_campfire_soft_shadows_mask");
                composite.debugView = VisionComposite.DebugView.Final;
                cameraRig.orthographicSize = ortho;
            }

            // 4. Moving: walking, sprinting, strafing and backpedalling (input overrides, real gait).
            yield return Stage(player, V(L.Spawn, 0f, 2f), Vector2.up, wanderer, away);
            player.MoveOverride = Vector2.up;
            yield return Wait(70);
            yield return Shot("12_walking");
            player.SprintOverride = true;
            yield return Wait(50);
            yield return Shot("13_sprinting");
            player.SprintOverride = null;
            yield return Stage(player, V(L.Spawn, -4f, 4f), Vector2.up, wanderer, away);
            player.MoveOverride = Vector2.right;
            yield return Wait(60);
            yield return Shot("14_strafing");
            player.MoveOverride = Vector2.down;
            yield return Wait(60);
            yield return Shot("15_backpedal");
            player.MoveOverride = null;

            // 5. The player close up from the front, back and side; the wanderer in the beam and its shadow.
            cameraRig.orthographicSize = 3.2f;
            Vector2 open = L.Spawn + new Vector2(2f, 1f);
            yield return Stage(player, V(open), Vector2.down, wanderer, away);
            yield return Shot("20_player_facing_camera");
            WriteCharacterReport(player, wanderer);
            yield return Stage(player, V(open), Vector2.up, wanderer, away);
            yield return Shot("21_player_back");
            yield return Stage(player, V(open), Vector2.right, wanderer, away);
            yield return Shot("22_player_side");
            yield return Stage(player, V(open), new Vector2(-0.25f, -1f), wanderer, V(open, -0.6f, -2.4f));
            yield return Shot("23_wanderer_in_beam");
            composite.debugView = VisionComposite.DebugView.Shadows;
            yield return Wait(3);
            yield return Shot("23b_wanderer_flashlight_shadow_mask");
            composite.debugView = VisionComposite.DebugView.Final;
            cameraRig.orthographicSize = ortho;

            // 6. Look: camera effects off, then each look slider low and high (spawn view).
            yield return Stage(player, V(L.Spawn), new Vector2(0.2f, 1f), wanderer, away);
            composite.look.cameraEffects = false;
            yield return Wait(5);
            yield return Shot("24_camera_effects_off");
            composite.look = VisionComposite.Look.Defaults;
            yield return Wait(5);
            yield return Shot("25_look_default");
            string[] names = { "contrast", "saturation", "lit_brightness", "unlit_brightness" };
            for (int i = 0; i < names.Length; i++)
            {
                foreach (float value in new[] { 0.6f, 1.4f })
                {
                    VisionComposite.Look l = VisionComposite.Look.Defaults;
                    if (i == 0) l.contrast = value;
                    else if (i == 1) l.saturation = value;
                    else if (i == 2) l.litBrightness = value;
                    else l.unlitBrightness = value;
                    composite.look = l;
                    yield return Wait(5);
                    yield return Shot($"26_look_{names[i]}_{value:0.0}");
                }
            }
            composite.look = VisionComposite.Look.Defaults;
            if (hud != null)
            {
                hud.visible = true;
                hud.lookPanel = true;
                yield return Wait(5);
                yield return Shot("27_look_panel");
                hud.lookPanel = false;
                hud.visible = false;
            }

            // 7. A tree in the beam, lit from the south and from the west (shadow from the base, soft back side).
            yield return TreeShots(player, wanderer, away);

            // 8. The new places, lit by the flashlight and seen raw: the lake and dock, the graveyard, the playground,
            //    the hanging tree, a power pole, a fence, a tall-grass patch, a woods generator with its cover.
            var places = new System.Collections.Generic.List<(string name, Vector2 at, Vector2 aim)>
            {
                ("31_lake_and_dock", L.DockStart - (L.DockEnd - L.DockStart).normalized * 3f, (L.DockEnd - L.DockStart).normalized),
                ("32_graveyard", L.Graveyard.center + new Vector2(0f, -2f), Vector2.up),
                ("33_playground", L.Playground.center + new Vector2(0f, -1.5f), Vector2.up),
                ("34_hanging_tree", L.HangingTree + new Vector2(0f, -2.5f), Vector2.up),
            };
            if (L.PowerPoles.Count > 2) places.Add(("35_power_line", L.PowerPoles[2] + new Vector2(-2.5f, -3f), new Vector2(0.5f, 1f)));
            if (L.Fences.Count > 0) places.Add(("36_fence", (L.Fences[0].A + L.Fences[0].B) * 0.5f + new Vector2(0f, -3f), Vector2.up));
            if (L.GrassPatches.Count > 0) places.Add(("39_tall_grass", L.GrassPatches[0].Centre + new Vector2(0f, -L.GrassPatches[0].Radius - 2f), Vector2.up));
            if (L.WoodsGenerators.Count > 0) places.Add(("40_woods_generator", L.WoodsGenerators[0] + new Vector2(-2.5f, -2.5f), new Vector2(1f, 1f)));
            foreach (MapLayout.Kit k in L.Kits)
                if (k.Kind == MapLayout.KitKind.Shack) { places.Add(("41_shack", k.Centre + new Vector2(0f, -5f), Vector2.up)); break; }
            foreach (MapLayout.Kit k in L.Kits)
                if (k.Kind == MapLayout.KitKind.Wreck) { places.Add(("42_wreck_kit", k.Centre + new Vector2(-1f, -5f), Vector2.up)); break; }
            // The building: its longest hallway, the loading bay at the gate, the yard, and one room of each kind seen from its door.
            BuildingPlan plan = L.Plan;
            if (plan != null)
            {
                BuildingPlan.Room hall = null;
                foreach (BuildingPlan.Room r in plan.Rooms)
                    if (r.IsHallway && (hall == null || Mathf.Max(r.Area.width, r.Area.height) > Mathf.Max(hall.Area.width, hall.Area.height))) hall = r;
                if (hall != null)
                {
                    bool alongX = hall.Area.width > hall.Area.height;
                    places.Add(("60_building_hallway", alongX ? new Vector2(hall.Area.xMin + 3f, hall.Area.center.y) : new Vector2(hall.Area.center.x, hall.Area.yMin + 3f), alongX ? Vector2.right : Vector2.up));
                }
                places.Add(("61_building_loading_bay_gate", new Vector2(plan.GateX - 1f, plan.Bounds.yMax - 2.2f), Vector2.up));
                places.Add(("62_exit_yard", new Vector2(plan.GateX, L.Yard.yMax - 1.5f), Vector2.down));
                var seen = new System.Collections.Generic.HashSet<BuildingPlan.RoomType>();
                foreach (BuildingPlan.Room r in plan.Rooms)
                {
                    if (r.IsHallway || r.Id == plan.GateRoom) continue;
                    string key = r.HasGenerator ? "generator_" + r.Name : r.Name;
                    if (!r.HasGenerator && !seen.Add(r.Type)) continue;
                    if (Entry(plan, r, out Vector2 at, out Vector2 aim))
                        places.Add(($"63_room_{key.ToLowerInvariant().Replace(' ', '_')}_{r.Id}", at, aim));
                }
            }
            cameraRig.orthographicSize = 14f;
            foreach (var (name, at, aim) in places)
            {
                yield return Stage(player, V(at), aim, wanderer, away);
                yield return Wait(5);
                yield return Shot(name);
                composite.debugView = VisionComposite.DebugView.SceneOnly;
                yield return Wait(3);
                yield return Shot(name + "_scene");
                composite.debugView = VisionComposite.DebugView.Final;
            }
            // Hiding in the tall grass: the player vanishes, the prompt offers the way out.
            if (L.GrassPatches.Count > 0 && gameHud != null)
            {
                gameHud.visible = true;
                yield return Stage(player, V(L.GrassPatches[0].Centre), Vector2.up, wanderer, away);
                foreach (HidingSpot h in HidingSpot.All)
                    if (h.kind == HidingSpot.Kind.Grass) { player.EnterHiding(h); break; }
                yield return Wait(5);
                gameHud.Refresh();
                yield return Shot("39b_hiding_in_grass");
                player.LeaveHiding();
                gameHud.visible = false;
            }
            cameraRig.orthographicSize = ortho;

            // 9. The whole map from above (raw scene), and a closer survey around the building's site.
            yield return Overview("28c_whole_map_scene", Vector2.zero, 100f);
            yield return Overview("28_survey_building_site", Vector2.zero, 32f);
            yield return Overview("28d_building_plan", L.Building.center + new Vector2(0f, 2f), 21f);
            yield return Overview("28b_survey_spawn", L.Spawn + new Vector2(0f, 14f), 22f);

            // 10. The HUD: prompt at a supply, a filled inventory, hurt, paused, dead.
            if (gameHud != null && player.GetComponent<PlayerStats>() is PlayerStats ps)
            {
                gameHud.visible = true;
                Pickup near = world.Pickups.Count > 0 ? world.Pickups[0] : null;
                if (near != null)
                {
                    Vector3 lp = world.transform.InverseTransformPoint(near.transform.position);
                    yield return Stage(player, new Vector3(lp.x, 0f, lp.z - 1.1f), Vector2.up, wanderer, away);
                }
                yield return Wait(10);
                gameHud.Refresh();
                yield return Shot("50_hud_prompt");
                ps.inventory.Add(ItemType.Bandage, 2);
                ps.inventory.Add(ItemType.Water, 3);
                ps.inventory.Add(ItemType.CannedFood, 1);
                ps.vitals.Tick(2f, true);
                gameHud.Notify("Picked up Water x2");
                gameHud.Notify("Used a bandage (+35 health)");
                yield return Wait(5);
                yield return Shot("51_hud_inventory");
                ps.vitals.TakeDamage(72f);
                yield return Wait(20);
                yield return Shot("52_hud_low_health");
                gameHud.SetMenu(true);
                yield return Wait(5);
                yield return Shot("53_hud_pause_menu");
                gameHud.SetMenu(false);
                ps.vitals.TakeDamage(100f);
                yield return Wait(5);
                yield return Shot("54_hud_death");
                ps.vitals.Reset();
                yield return Wait(5);
                gameHud.visible = false;
            }

            // 11. Tree catalogues and the gait sheet (side-on, fully lit).
            yield return TreeSheets();
            yield return GaitSheet(player);

            // Frame time over a short run with everything live, and how long the map took to build.
            yield return Stage(player, V(L.Spawn), Vector2.up, wanderer, away);
            wanderer.enabled = true;
            player.AimOverride = new Vector2(0f, 1f);
            yield return Wait(30);
            float t0 = Time.realtimeSinceStartup;
            const int frames = 300;
            yield return Wait(frames);
            float ms = (Time.realtimeSinceStartup - t0) * 1000f / frames;
            File.WriteAllText(Path.Combine(folder, "perf.txt"),
                $"avg frame {ms:0.00} ms ({1000f / ms:0} fps) over {frames} frames at {Screen.width}x{Screen.height}\n" +
                $"generation {world.LastGenerationReport}\n");
            Application.Quit();
        }

        /// <summary>The raw scene from high above, centred on a design-unit point, with the clip planes opened up.</summary>
        /// <summary>Just inside a room's door, looking in.</summary>
        static bool Entry(BuildingPlan plan, BuildingPlan.Room room, out Vector2 at, out Vector2 aim)
        {
            at = aim = default;
            foreach (BuildingPlan.Opening o in plan.Openings)
            {
                if (!o.IsPassage) continue;
                BuildingPlan.Interface f = plan.Interfaces[o.Interface];
                if (f.A != room.Id && f.B != room.Id) continue;
                aim = f.A == room.Id ? -f.Normal : f.Normal;
                at = o.Centre + aim * 0.8f;
                return true;
            }
            return false;
        }

        IEnumerator Overview(string name, Vector2 centre, float size)
        {
            Camera cam = cameraRig.GetComponent<Camera>();
            float far = cam.farClipPlane, dist = cameraRig.distance, ortho = cameraRig.orthographicSize;
            float s = world.transform.lossyScale.x;
            cameraRig.orthographicSize = size * s;
            cameraRig.distance = 400f;
            cam.farClipPlane = 1400f;
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            yield return Stage(world.Player, new Vector3(centre.x, 0f, centre.y), Vector2.up, world.Wanderer, new Vector3(world.Layout.Spawn.x + 60f, 0f, world.Layout.Spawn.y));
            yield return Wait(5);
            yield return Shot(name);
            composite.debugView = VisionComposite.DebugView.Final;
            cam.farClipPlane = far;
            cameraRig.distance = dist;
            cameraRig.orthographicSize = ortho;
        }

        /// <summary>A tree near the spawn in the beam, lit from the south and from the west, with its neighbours hidden.</summary>
        IEnumerator TreeShots(PlayerController player, Wanderer wanderer, Vector3 away)
        {
            float ortho = cameraRig.orthographicSize;
            Vector2 near = world.Layout.Spawn + new Vector2(0f, 20f);
            // A tree in the beam, lit from the south and from the west: its shadow starts at the base and its
            // back side is a soft darkness rather than a black silhouette.
            Transform tree = null;
            float bestTree = float.MaxValue;
            foreach (Transform t in world.transform.Find("Static"))
            {
                if (!(t.name.StartsWith("Fir") || t.name.StartsWith("Spruce") || t.name.StartsWith("Dead"))) continue;
                Vector3 lp = world.transform.InverseTransformPoint(t.position);
                float d = new Vector2(lp.x - near.x, lp.z - near.y).sqrMagnitude;
                if (d < bestTree) { bestTree = d; tree = t; }
            }
            if (tree != null)
            {
                Vector3 tp = world.transform.InverseTransformPoint(tree.position);
                // Clear the stage: hide (and stop the shadows of) every other solid object within 7 units.
                var hidden = new System.Collections.Generic.List<GameObject>();
                foreach (Transform o in world.transform.Find("Static"))
                {
                    if (o == tree || o.name.StartsWith("Ground") || o.GetComponent<Vision.Visibility.Occluder>() == null) continue;
                    Vector3 op = world.transform.InverseTransformPoint(o.position);
                    if (new Vector2(op.x - tp.x + 1.7f, op.z - tp.z + 1.7f).magnitude > 7f || o.name.Contains("Wall") || o.GetComponent<Door>() != null) continue;
                    o.gameObject.SetActive(false);
                    hidden.Add(o.gameObject);
                }
                cameraRig.orthographicSize = 7f;
                yield return Stage(player, new Vector3(tp.x, 0f, tp.z - 2.4f), Vector2.up, wanderer, away);
                yield return Shot("38_tree_lit_from_south");
                yield return Stage(player, new Vector3(tp.x - 2.4f, 0f, tp.z), Vector2.right, wanderer, away);
                yield return Shot("38b_tree_lit_from_west");
                foreach (GameObject h in hidden) h.SetActive(true);
                cameraRig.orthographicSize = ortho;
            }

        }

        /// <summary>
        /// Renders a contact sheet with a lit side camera: row 1 is eight frames across one walking stride,
        /// row 2 one sprinting stride, row 3 the standing figure from the front, side, back, three-quarter
        /// view and a close-up of the head.
        /// </summary>
        IEnumerator GaitSheet(PlayerController player)
        {
            var animator = player.GetComponent<Vision.Characters.HumanoidAnimator>();
            if (animator == null) yield break;
            float scale = player.transform.lossyScale.x;
            const int w = 300, h = 420, cols = 8, rows = 5;
            var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var camGo = new GameObject("Sheet Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.64f, 0.67f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.targetTexture = rt;

            Light moon = FindAnyObjectByType<Light>();
            float moonIntensity = moon != null ? moon.intensity : 0f;
            Quaternion moonRot = moon != null ? moon.transform.rotation : Quaternion.identity;
            Color ambient = RenderSettings.ambientLight;
            if (moon != null)
            {
                moon.intensity = 1.7f;
                moon.transform.rotation = Quaternion.Euler(35f, -60f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.53f);
            Time.captureFramerate = 240;

            yield return Stage(player, new Vector3(0f, 0f, -16f), new Vector2(0f, 1f), world.Wanderer, new Vector3(-16f, 0f, 16f));
            for (int row = 0; row < 2; row++)
            {
                player.MoveOverride = new Vector2(0f, 1f);
                player.SprintOverride = row == 1;
                yield return Wait(240);
                float cycle = animator.Solver.CycleTime;
                int step = Mathf.Max(1, Mathf.RoundToInt(cycle / cols * 240f));
                for (int k = 0; k < cols; k++)
                {
                    Vector3 p = player.transform.position;
                    cam.orthographicSize = 1.05f * scale;
                    cam.transform.SetPositionAndRotation(p + new Vector3(2.2f * scale, 0.9f * scale, 0f), Quaternion.LookRotation(Vector3.left));
                    yield return new WaitForEndOfFrame();
                    Blit(rt, sheet, k * w, (rows - 1 - row) * h);
                    yield return Wait(step);
                }
            }
            player.SprintOverride = null;

            // Rows 3 and 4: walking up, then down, the steepest clear stretch of hillside, side-on.
            var gaps = new System.Text.StringBuilder();
            if (FindSlope(out Vector2 low, out Vector2 high, out float grade))
            {
                gaps.AppendLine($"slope rows: {Vector3.Angle(Vector3.up, new Vector3(0f, 1f, -grade)):0.0} degrees average, from {low} to {high} (design units)");
                for (int row = 2; row < 4; row++)
                {
                    Vector2 from = row == 2 ? low : high, to = row == 2 ? high : low;
                    Vector2 dir = (to - from).normalized;
                    yield return Stage(player, new Vector3(from.x, 0f, from.y), dir, world.Wanderer, new Vector3(-16f, 0f, 16f));
                    player.MoveOverride = dir;
                    float minGap = float.MaxValue, maxGap = float.MinValue;
                    string worstNote = "";
                    for (int f = 0; f < 120; f++)
                    {
                        yield return null;
                        TrackFootGap(animator, scale, ref minGap, ref maxGap, ref worstNote);
                    }
                    float cycle = animator.Solver.CycleTime;
                    int step = Mathf.Max(1, Mathf.RoundToInt(cycle / cols * 240f));
                    for (int k = 0; k < cols; k++)
                    {
                        Vector3 p = player.transform.position;
                        cam.orthographicSize = 1.15f * scale;
                        cam.transform.SetPositionAndRotation(p + new Vector3(2.2f * scale, 0.9f * scale, 0f), Quaternion.LookRotation(Vector3.left));
                        yield return new WaitForEndOfFrame();
                        Blit(rt, sheet, k * w, (rows - 1 - row) * h);
                        for (int f = 0; f < step; f++)
                        {
                            yield return null;
                            TrackFootGap(animator, scale, ref minGap, ref maxGap, ref worstNote);
                        }
                    }
                    gaps.AppendLine($"{(row == 2 ? "uphill" : "downhill")}: lowest point of either foot above the ground {minGap:0.000}, planted foot ankle above the ground {maxGap:0.000} at most (design units; ankle height {Vision.Characters.HumanoidSkeleton.AnkleHeight:0.000}){worstNote}");
                }
            }
            else gaps.AppendLine("no clear slope found");
            File.AppendAllText(Path.Combine(folder, "characters.txt"), gaps.ToString());

            player.MoveOverride = Vector2.zero;
            yield return Wait(480);
            player.MoveOverride = null;

            // Standing: front, side, back, three-quarter, head close-up.
            Vector3 c = player.transform.position;
            var views = new (Vector3 dir, float size, float height)[]
            {
                (Vector3.forward, 1.05f, 0.9f), (Vector3.right, 1.05f, 0.9f), (Vector3.back, 1.05f, 0.9f),
                (new Vector3(1f, 0.25f, 1f).normalized, 1.05f, 0.9f), (new Vector3(0.6f, 0.1f, 1f).normalized, 0.26f, 1.66f),
                (new Vector3(-0.8f, 0.6f, 0.8f).normalized, 1.05f, 0.9f),
            };
            for (int k = 0; k < views.Length; k++)
            {
                var v = views[k];
                cam.orthographicSize = v.size * scale;
                Vector3 target = c + Vector3.up * (v.height * scale);
                cam.transform.SetPositionAndRotation(target + v.dir * 10f, Quaternion.LookRotation(-v.dir));
                yield return new WaitForEndOfFrame();
                Blit(rt, sheet, k * w, 0);
                yield return Wait(2);
            }

            sheet.Apply();
            File.WriteAllBytes(Path.Combine(folder, "19_gait_and_anatomy_sheet.png"), sheet.EncodeToPNG());
            Time.captureFramerate = 0;
            if (moon != null)
            {
                moon.intensity = moonIntensity;
                moon.transform.rotation = moonRot;
            }
            RenderSettings.ambientLight = ambient;
            cam.targetTexture = null;
            Destroy(camGo);
            rt.Release();
            Destroy(rt);
            Destroy(sheet);
        }

        /// <summary>Writes what each character's renderer is doing, to diagnose a figure that does not show up.</summary>
        void WriteCharacterReport(PlayerController player, Wanderer wanderer)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Component c in new Component[] { player, wanderer })
            {
                foreach (SkinnedMeshRenderer r in c.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh m = r.sharedMesh;
                    Material mat = r.sharedMaterial;
                    sb.AppendLine($"{c.name}/{r.name}: enabled {r.enabled}, active {r.gameObject.activeInHierarchy}, visible {r.isVisible}, " +
                        $"mesh {(m != null ? m.name : "none")} ({(m != null ? m.vertexCount : 0)} verts, {(m != null ? m.triangles.Length / 3 : 0)} tris, " +
                        $"{(m != null ? m.bindposeCount : 0)} bind poses, weights {(m != null && m.boneWeights.Length == m.vertexCount)}), bones {r.bones.Length}, " +
                        $"root {(r.rootBone != null ? r.rootBone.name : "none")}, bounds {r.bounds.center} size {r.bounds.size}, " +
                        $"material {(mat != null ? mat.name + " / " + mat.shader.name + " [" + string.Join(" ", mat.shaderKeywords) + "]" : "none")}, " +
                        $"shadows {r.shadowCastingMode}");
                }
            }
            File.WriteAllText(Path.Combine(folder, "characters.txt"), sb.ToString());
        }

        /// <summary>
        /// Two lit catalogue images: every dead tree design (three sizes each, small to large), and every evergreen form at
        /// a spread of per-tree heights and girths. Built far from the level and photographed side-on.
        /// </summary>
        IEnumerator TreeSheets()
        {
            var root = new GameObject("Tree Sheet").transform;
            root.position = new Vector3(5000f, 0f, 5000f);
            Material mat = world.lowPolyMaterial;
            var camGo = new GameObject("Tree Sheet Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.64f, 0.67f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            var rt = new RenderTexture(2400, 1200, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            Light moon = FindAnyObjectByType<Light>();
            float moonIntensity = moon != null ? moon.intensity : 0f;
            Quaternion moonRot = moon != null ? moon.transform.rotation : Quaternion.identity;
            Color ambient = RenderSettings.ambientLight;
            if (moon != null)
            {
                moon.intensity = 1.6f;
                moon.transform.rotation = Quaternion.Euler(35f, -50f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.53f);

            GameObject Place(Mesh m, Vector3 at, Vector3 scale)
            {
                var go = new GameObject(m.name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = at;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                return go;
            }

            // Dead trees: three sheets of four designs, each design with its three sizes side by side; then the
            // evergreens on two sheets, each form at short-thick, medium and tall-thin. Every sheet has its own spot.
            var sheets = new System.Collections.Generic.List<(string name, Vector3 centre)>();
            for (int sheet = 0; sheet < 3; sheet++)
            {
                float x0 = sheet * 200f;
                for (int d = 0; d < 4; d++)
                {
                    int k = sheet * 4 + d;
                    for (int size = 0; size < 3; size++)
                        Place(LowPolyModels.DeadTree((LowPolyModels.DeadTreeKind)k, size), new Vector3(x0 + d * 11.5f + size * 3.4f, 0f, 0f), Vector3.one)
                            .transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                }
                sheets.Add(($"45_dead_trees_{sheet + 1}", new Vector3(x0 + 21.5f, 4.2f, 0f)));
            }
            var spread = new[] { new Vector2(0.65f, 0.7f), new Vector2(1f, 1f), new Vector2(1.45f, 0.85f) };
            for (int sheet = 0; sheet < 2; sheet++)
            {
                float x0 = 600f + sheet * 200f;
                for (int f = 0; f < 4 && sheet * 4 + f < LowPolyModels.ConiferStyles; f++)
                {
                    int style = sheet * 4 + f;
                    for (int v = 0; v < 3; v++)
                    {
                        Vector2 hg = spread[v];
                        Place(LowPolyModels.Conifer(new System.Random(1200 + style * 3 + v), style), new Vector3(x0 + f * 11.5f + v * 3.6f, 0f, 0f), new Vector3(hg.y, hg.x, hg.y));
                    }
                }
                sheets.Add(($"46_evergreens_{sheet + 1}", new Vector3(x0 + 21.5f, 4.8f, 0f)));
            }

            float s = world.transform.lossyScale.x;
            root.localScale = Vector3.one * s;
            foreach (var shot in sheets)
            {
                Vector3 c = root.TransformPoint(shot.centre);
                cam.orthographicSize = 12f * s;
                cam.transform.SetPositionAndRotation(c + new Vector3(0f, 0f, -60f), Quaternion.LookRotation(Vector3.forward));
                yield return new WaitForEndOfFrame();
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                cam.Render();
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = prev;
                tex.Apply();
                File.WriteAllBytes(Path.Combine(folder, shot.name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }

            if (moon != null)
            {
                moon.intensity = moonIntensity;
                moon.transform.rotation = moonRot;
            }
            RenderSettings.ambientLight = ambient;
            cam.targetTexture = null;
            Destroy(camGo);
            rt.Release();
            Destroy(rt);
            Destroy(root.gameObject);
        }

        /// <summary>Lowest point of either foot (ankle joint) and highest planted ankle above the ground under it.</summary>
        static void TrackFootGap(Vision.Characters.HumanoidAnimator animator, float scale, ref float min, ref float max, ref string note)
        {
            Vision.Characters.GaitPose pose = animator.Solver.Evaluate();
            foreach (var (bone, leg) in new[] { (Vision.Characters.Bone.FootL, pose.Left), (Vision.Characters.Bone.FootR, pose.Right) })
            {
                Vector3 ankle = animator.bones[(int)bone].position;
                float gap = (ankle.y - TerrainField.WorldHeight(ankle, ankle.y)) / scale;
                min = Mathf.Min(min, gap);
                if (leg.Grounded && gap > max)
                {
                    max = gap;
                    Vector3 root = animator.transform.position;
                    float rootGap = (root.y - TerrainField.WorldHeight(root, root.y)) / scale;
                    note = $"; worst at root {root / scale} (root {rootGap:0.000} above the ground)";
                }
            }
        }

        /// <summary>
        /// A straight 4-unit stretch climbing at 19-40° throughout (along X or Z), clear of trees, rocks and walls,
        /// inside the arena. Returns its low and high ends in design units.
        /// </summary>
        bool FindSlope(out Vector2 low, out Vector2 high, out float grade)
        {
            TerrainField f = world.Terrain;
            low = high = Vector2.zero;
            grade = 0f;
            if (f == null) return false;
            float best = float.MaxValue;
            const float len = 4f;
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.right, Vector2.left };
            for (float x = -80f; x <= 80f; x += 1f)
            {
                for (float z = -80f; z <= 80f; z += 1f)
                {
                    foreach (Vector2 d in dirs)
                    {
                        if (Mathf.Abs(d.y) < 0.5f) continue;   // side-on camera looks along X, so climb along Z
                        var a = new Vector2(x, z);
                        Vector2 b = a + d * len;
                        if (Mathf.Abs(b.y) > 80f || world.Layout.Blocked(a) || world.Layout.LakeDepth(a) > -3f) continue;
                        bool ok = true;
                        for (float t = 0f; t < len && ok; t += 0.5f)
                        {
                            Vector2 p = a + d * t, q = a + d * (t + 0.5f);
                            float g = (f.Height(q.x, q.y) - f.Height(p.x, p.y)) / 0.5f;
                            ok = g > 0.35f && g < 0.85f;
                        }
                        if (!ok) continue;
                        float avg = (f.Height(b.x, b.y) - f.Height(a.x, a.y)) / len;
                        float score = Mathf.Abs(avg - 0.58f);
                        if (score >= best || !Clear(a, b)) continue;
                        best = score;
                        low = a;
                        high = b;
                        grade = avg;
                    }
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>No collider other than the ground along the stretch (a body-wide capsule).</summary>
        bool Clear(Vector2 a, Vector2 b)
        {
            Transform root = world.transform;
            float s = root.lossyScale.x;
            for (float t = 0f; t <= 1f; t += 0.1f)
            {
                Vector2 p = Vector2.Lerp(a, b, t);
                Vector3 w = root.TransformPoint(new Vector3(p.x, world.Terrain.Height(p.x, p.y), p.y));
                foreach (Collider c in Physics.OverlapCapsule(w + Vector3.up * (0.5f * s), w + Vector3.up * (1.6f * s), 0.6f * s))
                    if (!(c is MeshCollider) && !c.GetComponent<CharacterController>()) return false;
            }
            return true;
        }

        static void Blit(RenderTexture rt, Texture2D sheet, int x, int y)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, y);
            RenderTexture.active = previous;
        }

        /// <summary>Positions are in the level's design units and converted through the (scaled) level root.</summary>
        IEnumerator Stage(PlayerController player, Vector3 position, Vector2 aim, Wanderer wanderer, Vector3 wandererPos)
        {
            player.Teleport(world.transform.TransformPoint(position));
            player.AimOverride = aim;
            Vector3 wp = world.transform.TransformPoint(wandererPos);
            wp.y = TerrainField.WorldHeight(wp, wp.y);
            wanderer.transform.position = wp;
            cameraRig.Snap();
            yield return Wait(10);
        }

        static IEnumerator Wait(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return Wait(3);
        }
    }
}
