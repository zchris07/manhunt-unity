using System.Collections.Generic;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Vision.Characters;
using Vision.Game;
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

        /// <summary>True when the player was launched to take the captures (it skips the title screen).</summary>
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-visionCapture") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-visionGasLab") >= 0;

        string folder;
        bool gasLab;

        /// <summary>
        /// A short run of the gases alone (<c>-visionGasLab &lt;folder&gt;</c>), for tuning their look: galaxy gas and Penjamin
        /// as they grow, hang and fade, close and far, and Zach's scent trail.
        /// </summary>
        IEnumerator GasLab()
        {
            if (hud != null) hud.visible = false;
            composite.look = VisionComposite.Look.Defaults;
            PlayerController player = world.Player;
            Wanderer wanderer = world.Wanderer;
            wanderer.enabled = false;
            MapLayout L = world.Layout;
            Vector3 away = new Vector3(L.Spawn.x + 60f, 0f, L.Spawn.y);
            Vector3 V(Vector2 p) => new Vector3(p.x, 0f, p.y);
            yield return Wait(10);
            gameHud.visible = true;
            gameHud.StartTesting(false);
            MatchHost host = MatchHost.For(world);
            yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
            yield return new WaitForSeconds(0.5f);
            gameHud.PlayTestFx(TestFx.Gas);
            foreach (float t in new[] { 0.3f, 1.2f, 3f, 6.2f })
            {
                yield return new WaitForSeconds(t - (t > 0.3f ? 0f : 0f));
                yield return Shot($"gas_galaxy_{t:0.0}");
            }
            yield return new WaitForSeconds(2f);
            if (host.SwitchRole())
            {
                yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                yield return new WaitForSeconds(0.5f);
                player.ExtraButtons = Btn.Vape;
                yield return Wait(3);
                player.ExtraButtons = Btn.None;
                foreach (float t in new[] { 0.3f, 0.8f, 2f, 4.5f })
                {
                    yield return new WaitForSeconds(t);
                    yield return Shot($"gas_penjamin_{t:0.0}");
                }
                // The scent: a survivor dummy running a loop ahead of Zach.
                SimPlayer d = host.SpawnDummy(Role.Survivor);
                if (d != null)
                {
                    for (int i = 0; i < 40; i++)
                    {
                        float a = i / 40f * Mathf.PI * 1.5f;
                        host.Sim.Teleport(d.Id, host.Local.Pos + new Vector2(Mathf.Cos(a) * 4f, 3f + Mathf.Sin(a) * 3f));
                        host.Sim.Trails.Add(new TrailPoint { Id = 900000 + i, Pos = d.Pos, T = host.Sim.Time, Kind = 0, Who = d.Id });
                        yield return new WaitForSeconds(0.05f);
                    }
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("gas_scent");
                }
            }
            File.WriteAllText(Path.Combine(folder, "perf.txt"), "gas lab");
            Application.Quit();
        }

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-visionCapture") folder = args[i + 1];
                if (args[i] == "-visionGasLab") { folder = args[i + 1]; gasLab = true; }
            }
            if (folder == null) return;
            Directory.CreateDirectory(folder);
            StartCoroutine(gasLab ? GasLab() : Run());
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
            if (L.Sushi.width > 0f) places.Add(("43_miga_sushi", L.Sushi.center, Vector2.up));
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
            // The building's longest hallway again, its generators running: every room lit by its ceiling lights.
            BuildingPower power = world.GetComponentInChildren<BuildingPower>();
            int hallShot = places.FindIndex(x => x.name == "60_building_hallway");
            if (power != null && hallShot >= 0)
            {
                MatchHost litHost = MatchHost.For(world);
                var was = new System.Collections.Generic.List<(float, bool)>();
                if (litHost != null && litHost.Sim != null)
                    foreach (GenState o in litHost.Sim.Gens) { was.Add((o.Progress, o.Repaired)); o.Progress = 1f; o.Repaired = true; }
                foreach (GeneratorObjective g in power.generators) g.SetState(1f, true);
                power.Refresh();
                var (_, hallAt, hallAim) = places[hallShot];
                yield return Stage(player, V(hallAt), hallAim, wanderer, away);
                yield return Wait(8);
                yield return Shot("60b_building_hallway_lit");
                // A body blown apart a few metres up the lit hallway: the blast, then the stains it leaves.
                var effects = UnityEngine.Object.FindAnyObjectByType<Vision.UI.MatchEffects>();
                if (effects != null)
                {
                    effects.OnEvent(new GameEvent { Kind = EventKind.Explosion, Pos = hallAt + hallAim.normalized * 1.8f });
                    yield return Wait(4);
                    yield return Shot("60c_bloody_explosion");
                    yield return Wait(90);
                    yield return Shot("60d_blood_stains");
                }
                if (litHost != null && litHost.Sim != null)
                    for (int i = 0; i < was.Count && i < litHost.Sim.Gens.Length; i++) { litHost.Sim.Gens[i].Progress = was[i].Item1; litHost.Sim.Gens[i].Repaired = was[i].Item2; }
                foreach (GeneratorObjective g in power.generators) g.SetState(0f, false);
                power.Refresh();
            }

            cameraRig.orthographicSize = ortho;

            // 9. The whole map from above (raw scene), and a closer survey around the building's site.
            yield return Overview("28c_whole_map_scene", Vector2.zero, 100f);
            yield return Overview("28_survey_building_site", Vector2.zero, 32f);
            yield return Overview("28d_building_plan", L.Building.center + new Vector2(0f, 2f), 21f);
            yield return Overview("28b_survey_spawn", L.Spawn + new Vector2(0f, 14f), 22f);

            // 10. The HUD: prompt at a supply, a filled inventory, hurt, paused, downed.
            MatchHost host = MatchHost.For(world);
            if (gameHud != null && host != null && host.Local != null)
            {
                SimPlayer me = host.Local;
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
                me.Inv.Add(ItemType.Bottle, 3);
                me.Inv.Add(ItemType.Goggles, 1);
                me.Inv.Add(ItemType.Shotgun, 1);
                me.Inv.Add(ItemType.DoctorPepper, 2);
                me.Inv.Add(ItemType.Trap, 1);
                me.Inv.Add(ItemType.Confit, 1);
                me.Inv.Add(ItemType.MrBeastBar, 4);
                me.Inv.Add(ItemType.MiniShield, 2);
                me.Shield = 0.5f;
                player.SelectedSlot = 2;
                gameHud.Notify("Picked up Mini shield");
                gameHud.Notify("Mini shield: +25% shield");
                yield return Wait(5);
                yield return Shot("51_hud_inventory");
                host.Sim.HurtSurvivor(me, 1.35f, null, "capture");
                yield return Wait(20);
                yield return Shot("52_hud_low_health");
                gameHud.SetMenu(true);
                yield return Wait(5);
                yield return Shot("53_hud_pause_menu");
                gameHud.SetMenu(false);
                host.Sim.HurtSurvivor(me, 1f, null, "capture");
                yield return new WaitForSeconds(1.2f);
                yield return Shot("54_hud_downed");
                MatchSim.RestoreSurvivor(me, 1f);
                me.Shield = 0f;
                me.Inv.Clear();
                player.SelectedSlot = -1;
                host.Begin();
                yield return new WaitForSeconds(1f);

                // The objective: a woods generator half started, then running; the gate lever without and with power;
                // the gate rolled open.
                if (L.WoodsGenerators.Count > 0 && host.Sim.Gens.Length > 0)
                {
                    Vector2 g = L.WoodsGenerators[0];
                    int gen = 0;
                    float bestD = float.MaxValue;
                    for (int i = 0; i < host.Sim.Map.Generators.Count; i++)
                    {
                        float d = (host.Sim.Map.Generators[i] - g).sqrMagnitude;
                        if (d < bestD) { bestD = d; gen = i; }
                    }
                    yield return Stage(player, V(g + new Vector2(0f, -1.7f)), Vector2.up, wanderer, away);
                    host.Sim.Gens[gen].Progress = 0.4f;
                    yield return Wait(10);
                    gameHud.Refresh();
                    yield return Shot("55_hud_generator_repair");
                    host.Sim.Gens[gen].Progress = 1f;
                    host.Sim.Gens[gen].Repaired = true;
                    gameHud.Notify($"Generator running (1/{host.Sim.Bal.RequiredGenerators})");
                    yield return Wait(10);
                    gameHud.Refresh();
                    yield return Shot("56_generator_running");
                }
                if (L.Plan != null && world.Gate != null)
                {
                    yield return Stage(player, V(L.Plan.Lever + new Vector2(-0.3f, -1.0f)), Vector2.up, wanderer, away);
                    yield return Wait(10);
                    gameHud.Refresh();
                    yield return Shot("57_gate_lever_no_power");
                    foreach (GenState o in host.Sim.Gens) { o.Progress = 1f; o.Repaired = true; }
                    yield return Wait(5);
                    gameHud.Refresh();
                    yield return Shot("57b_gate_lever_powered");
                    host.Sim.Gate.Progress = 1f;
                    host.Sim.Gate.Open = true;
                    yield return new WaitForSeconds(3f);
                    gameHud.Refresh();
                    yield return Stage(player, V(new Vector2(L.Plan.GateX, L.Plan.Bounds.yMax - 2.5f)), Vector2.up, wanderer, away);
                    yield return Wait(10);
                    yield return Shot("58_gate_open");
                }
                yield return Wait(5);

                // Testing mode with speed mode, the minimap (explored where the captures went), the game menu, the full
                // map under its fog and revealed, and the title screen.
                gameHud.StartTesting(false);
                gameHud.HideResults();
                GameSession.SpeedMode = true;
                yield return Stage(player, V(L.Spawn + new Vector2(0f, 6f)), Vector2.up, wanderer, away);
                yield return new WaitForSeconds(0.5f);
                gameHud.Refresh();
                yield return Shot("70_testing_mode_minimap");
                gameHud.SetMenu(true);
                yield return Wait(5);
                gameHud.Refresh();
                yield return Shot("71_game_menu");
                gameHud.SetMenu(false);
                gameHud.Map.SetOpen(true);
                yield return new WaitForSeconds(0.4f);
                yield return Shot("72_full_map_fog");
                gameHud.Map.RevealAll();
                yield return new WaitForSeconds(0.4f);
                yield return Shot("73_full_map_revealed");
                gameHud.Map.SetOpen(false);
                gameHud.ShowMainMenu();
                yield return Wait(5);
                yield return Shot("74_main_menu");
                gameHud.OpenHowToPlay();
                yield return Wait(5);
                yield return Shot("74b_how_to_play");
                gameHud.StartTesting(false);
                GameSession.SpeedMode = false;

                // The original's screen effects, played on yourself from the TEST EFFECTS panel.
                yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                gameHud.PlayTestFx(TestFx.Scare);
                yield return new WaitForSeconds(1.2f);
                yield return Shot("77_burst_scare");
                yield return new WaitForSeconds(1.6f);
                gameHud.PlayTestFx(TestFx.Book);
                yield return new WaitForSeconds(0.35f);
                yield return Shot("78_book_flash");
                yield return new WaitForSeconds(0.8f);
                gameHud.Overlays.ShowNote(0);
                yield return new WaitForSeconds(0.8f);
                yield return Shot("79_note");
                gameHud.Overlays.HideNote();
                if (host.SwitchRole())
                {
                    gameHud.PlayTestFx(TestFx.Stun);
                    float o = cameraRig.orthographicSize;
                    cameraRig.orthographicSize = o * 0.45f;
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("80_stun_stars");
                    yield return new WaitForSeconds(0.45f);
                    yield return Shot("142_zach_reeling");
                    cameraRig.orthographicSize = o;
                    host.SwitchRole();
                }
                // Items in use: a thrown bottle, a shotgun blast, galaxy gas, a dropped item, the editor, night vision.
                {
                    SimPlayer me8 = host.Local;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                    IEnumerator UseItem(ItemType item, int frames = 2)
                    {
                        player.SelectedSlot = me8.Inv.FirstSlotOf(item);
                        yield return Wait(3);
                        player.ExtraButtons = Btn.Primary;
                        yield return Wait(frames);
                        player.ExtraButtons = Btn.None;
                    }
                    yield return UseItem(ItemType.Bottle);
                    yield return new WaitForSeconds(0.12f);
                    gameHud.Refresh();
                    yield return Shot("93_bottle_throw");
                    yield return new WaitForSeconds(1f);
                    yield return UseItem(ItemType.Shotgun);
                    yield return new WaitForSeconds(0.03f);
                    yield return Shot("94_shotgun");
                    yield return new WaitForSeconds(1f);
                    gameHud.PlayTestFx(TestFx.Gas);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot("95_galaxy_gas");
                    yield return new WaitForSeconds(6f);
                    player.SelectedSlot = me8.Inv.FirstSlotOf(ItemType.Book);
                    yield return Wait(3);
                    player.ExtraButtons = Btn.Drop;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.3f);
                    gameHud.ToggleEditor(true);
                    gameHud.ClickSlot(0);
                    gameHud.Refresh();
                    yield return Wait(3);
                    yield return Shot("96_drop_and_inventory_editor");
                    gameHud.ToggleEditor(false);
                    // Night vision: an ability on right click.
                    yield return Wait(3);
                    player.ExtraButtons = Btn.Lunge;
                    yield return new WaitForSeconds(0.5f);
                    gameHud.Refresh();
                    yield return Shot("97_night_vision");
                    player.ExtraButtons = Btn.None;
                    player.SelectedSlot = -1;
                    yield return new WaitForSeconds(0.3f);
                }
                // Close-ups: how each gun is held, the shotgun's tracers leaving the barrel, the goggles worn and the x-ray
                // growing out, a quick throw, and the item icons.
                {
                    SimPlayer meG = host.Local;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                    meG.Inv.Clear();
                    meG.Inv.Add(ItemType.Shotgun, 1);
                    meG.Inv.Add(ItemType.Pistol, 1);
                    meG.Inv.Add(ItemType.Sniper, 1);
                    meG.Inv.Add(ItemType.Goggles, 1);
                    meG.Inv.Add(ItemType.Bottle, 3);
                    meG.Inv.Add(ItemType.MiniShield, 2);
                    meG.Inv.Add(ItemType.Trap, 1);
                    meG.Inv.Add(ItemType.Book, 1);
                    float o = cameraRig.orthographicSize;
                    cameraRig.orthographicSize = o * 0.38f;
                    player.AimOverride = new Vector2(1f, 0.25f).normalized;
                    foreach (var (item, name) in new[] { (ItemType.Shotgun, "111_hold_shotgun"), (ItemType.Pistol, "112_hold_pistol"), (ItemType.Sniper, "113_hold_sniper") })
                    {
                        player.SelectedSlot = meG.Inv.FirstSlotOf(item);
                        yield return new WaitForSeconds(0.7f);
                        gameHud.Refresh();
                        yield return Shot(name);
                    }
                    // A pistol round flying out: the short bright streak and its thin dim trail.
                    player.SelectedSlot = meG.Inv.FirstSlotOf(ItemType.Pistol);
                    cameraRig.orthographicSize = o * 0.7f;
                    yield return new WaitForSeconds(0.5f);
                    yield return FireAndCatch(player, "143_pistol_round_in_flight", 0.012f);
                    LogState("143 after the trigger", host);
                    yield return new WaitForSeconds(0.5f);
                    // The 0.50 cal aimed: right click brings the scope to the eye and the laser on; then a round.
                    player.SelectedSlot = meG.Inv.FirstSlotOf(ItemType.Sniper);
                    cameraRig.orthographicSize = o * 0.7f;
                    player.ExtraButtons = Btn.Lunge;
                    yield return new WaitForSeconds(0.8f);
                    gameHud.Refresh();
                    yield return Shot("131_sniper_aimed_laser");
                    int roundsS = RoundsNow();
                    player.ExtraButtons = Btn.Lunge | Btn.Primary;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.Lunge;
                    yield return RoundInFlight("132_sniper_round_through", roundsS, 0.006f);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("133_sniper_at_the_hip");
                    // The laser run just behind a boulder, past ground rising toward the camera: unbroken over the ground,
                    // hidden only where the rock is drawn in front of it.
                    {
                        Camera cam = cameraRig.GetComponent<Camera>();
                        Vector3 f3 = world.transform.InverseTransformDirection(cam.transform.forward), r3 = world.transform.InverseTransformDirection(cam.transform.right);
                        Vector2 f2 = new Vector2(f3.x, f3.z).normalized, r2 = new Vector2(r3.x, r3.z).normalized;
                        Vector2 rock = Vector2.zero;
                        float bestScore = float.NegativeInfinity;
                        foreach (MeshCollider mc in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                        {
                            if (mc.name != "Rock Wall" || mc.transform.lossyScale.x / world.transform.lossyScale.x > 1.1f) continue;
                            Vector3 l = world.transform.InverseTransformPoint(mc.transform.position);
                            var rp = new Vector2(l.x, l.z);
                            if (Vector2.Distance(rp, L.Spawn) > 45f) continue;
                            float score = world.SurfaceHeight(rp - f2 * 4f) - world.SurfaceHeight(rp) - 0.02f * Vector2.Distance(rp, L.Spawn);
                            if (score > bestScore) { bestScore = score; rock = rp; }
                        }
                        if (bestScore > float.NegativeInfinity)
                        {
                            Vector2 standAt = rock + f2 * 1.3f - r2 * 5f;
                            yield return Stage(player, V(standAt), r2, wanderer, away);
                            player.SelectedSlot = meG.Inv.FirstSlotOf(ItemType.Sniper);
                            player.ExtraButtons = Btn.Lunge;
                            yield return new WaitForSeconds(0.8f);
                            yield return Shot("153_laser_behind_a_boulder");
                            Debug.Log($"[Capture] 153 boulder at {rock}, ground rising toward the camera by {bestScore:0.00}");
                            player.ExtraButtons = Btn.None;
                            yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), new Vector2(1f, 0.25f).normalized, wanderer, away);
                        }
                    }
                    // Miga Sushi and the Diddy Chamber, each with its name up as the player walks in; a flood light lit by
                    // its running generator; the full map zoomed in; piss in the eyes.
                    {
                        float o2 = cameraRig.orthographicSize;
                        cameraRig.orthographicSize = o * 0.8f;
                        foreach (var (name, area) in world.Places)
                        {
                            yield return Stage(player, V(area.center + new Vector2(0f, 0.3f)), Vector2.up, wanderer, away);
                            yield return new WaitForSeconds(0.9f);
                            yield return Shot(name == "MIGA SUSHI" ? "154_miga_sushi" : "155_diddy_chamber");
                            yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                        }
                        // Out in the woods (the generator farthest from the middle), once the place's name has faded.
                        yield return new WaitForSeconds(3.5f);
                        GeneratorObjective lit = null;
                        foreach (GeneratorObjective g in GeneratorObjective.All)
                            if (g != null && g.floods.Count > 0 && g.transform.IsChildOf(world.transform)
                                && (lit == null || g.transform.position.sqrMagnitude > lit.transform.position.sqrMagnitude)) lit = g;
                        if (lit != null)
                        {
                            int gi = world.Generators.IndexOf(lit.transform);
                            if (gi >= 0 && gi < host.Sim.Gens.Length) { host.Sim.Gens[gi].Progress = 1f; host.Sim.Gens[gi].Repaired = true; }
                            lit.SetState(1f, true);
                            // Standing by the generator, looking away from the towers' side, the camera pulled back to see the site.
                            Vector3 gl = world.transform.InverseTransformPoint(lit.transform.position);
                            Vector3 tl = world.transform.InverseTransformPoint(lit.floods[0].transform.position);
                            Vector2 g2 = new Vector2(gl.x, gl.z), away2 = (g2 - new Vector2(tl.x, tl.z)).normalized;
                            cameraRig.orthographicSize = o * 2.2f;
                            yield return Stage(player, V(g2 + away2 * 1.5f), away2, wanderer, away);
                            yield return new WaitForSeconds(0.6f);
                            yield return Shot("156_flood_light");
                        }
                        cameraRig.orthographicSize = o2;
                        yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), new Vector2(1f, 0.25f).normalized, wanderer, away);
                        gameHud.Map.SetOpen(true);
                        yield return Wait(3);
                        gameHud.Map.ZoomBy(2.6f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                        yield return new WaitForSeconds(0.4f);
                        yield return Shot("157_full_map_zoomed");
                        gameHud.Map.SetOpen(false);
                        host.Local.PissBlurT = Balance.Items.Piss.BlurTime * 0.8f;
                        yield return Wait(2);
                        yield return Shot("158_piss_in_the_eyes");
                        yield return new WaitForSeconds(2f);
                    }
                    cameraRig.orthographicSize = o * 0.38f;
                    player.SelectedSlot = meG.Inv.FirstSlotOf(ItemType.Shotgun);
                    yield return new WaitForSeconds(0.5f);
                    yield return FireAndCatch(player, "114_shotgun_from_the_barrel", 0.02f);
                    yield return new WaitForSeconds(0.8f);
                    // Space with the shotgun in hand: a quick throw, the bottle in hand as it goes, then the shotgun back.
                    player.ExtraButtons = Btn.Space;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.09f);
                    yield return Shot("115_quick_throw");
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot("115b_shotgun_back_in_hand");
                    // Goggles: the x-ray grows out of the torch over half a second; then worn over the eyes.
                    cameraRig.orthographicSize = o;
                    // (Night vision stays on while right click is held.)
                    yield return Wait(3);
                    player.ExtraButtons = Btn.Lunge;
                    yield return new WaitForSeconds(0.17f);
                    yield return Shot("116_xray_growing");
                    yield return new WaitForSeconds(0.6f);
                    gameHud.Refresh();
                    yield return Shot("117_xray_full_and_icons");
                    cameraRig.orthographicSize = o * 0.38f;
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("118_goggles_worn");
                    player.ExtraButtons = Btn.None;
                    cameraRig.orthographicSize = o;
                    player.AimOverride = null;
                    player.SelectedSlot = -1;
                    yield return new WaitForSeconds(0.4f);
                }
                // JARVIS: the waves going out, the boot half way through, then its screen running.
                {
                    SimPlayer meJ = host.Local;
                    meJ.Jarvis = 1;
                    player.ExtraButtons = Btn.Ability;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("119_jarvis_waves");
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("119b_jarvis_boot");
                    yield return new WaitForSeconds(1.8f);
                    yield return Shot("119c_jarvis_running");
                    meJ.JarvisT = 0f;
                    host.Sim.RevealT = 0f;
                    meJ.Jarvis = 0;
                    yield return new WaitForSeconds(0.8f);
                }
                // The view turned 45 degrees at the building (its doors and windows on every side show), an office door up
                // close, a vault over a fallen log, JARVIS lighting the whole screen, and the blur of Penjamin's gas.
                {
                    SimPlayer meV = host.Local;
                    TopDownCamera rigV = cameraRig;
                    // Outside a corner of the building, zoomed out a little: its walls run diagonally across the screen.
                    Rect bld = world.Layout.Building;
                    Vector2 corner = new Vector2(bld.xMin, bld.yMin);
                    yield return Stage(player, V(corner + new Vector2(-4f, -4f)), new Vector2(1f, 1f).normalized, wanderer, away);
                    float oz = cameraRig.orthographicSize;
                    cameraRig.orthographicSize = oz * 1.25f;
                    // JARVIS lights the whole screen, so the building shows.
                    player.ExtraButtons = Btn.Ability;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.None;
                    if (rigV != null) rigV.Turn(1);
                    yield return new WaitForSeconds(0.7f);
                    gameHud.Refresh();
                    yield return Shot("119_view_turned_45");
                    if (rigV != null) rigV.ResetYaw();
                    cameraRig.orthographicSize = oz;
                    if (world.Lows.Count > 0)
                    {
                        SandboxWorld.LowObstacle log = world.Lows[0];
                        Vector2 mid = (log.A + log.B) * 0.5f, along = (log.B - log.A).normalized, normal = new Vector2(-along.y, along.x);
                        yield return Stage(player, V(mid + normal * 1.6f), -normal, wanderer, away);
                        float o = cameraRig.orthographicSize;
                        cameraRig.orthographicSize = o * 0.4f;
                        player.MoveOverride = -normal;
                        float until = Time.time + 3f;
                        while (!meV.Move.Climbing && Time.time < until) yield return null;
                        yield return new WaitForSeconds(0.12f);
                        yield return Shot("121_vault_over_log");
                        player.MoveOverride = null;
                        cameraRig.orthographicSize = o;
                        yield return new WaitForSeconds(0.5f);
                    }
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                    player.ExtraButtons = Btn.Ability;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.5f);
                    gameHud.Refresh();
                    yield return Shot("122_jarvis_whole_screen");
                    yield return new WaitForSeconds(10f);
                    gameHud.PlayTestFx(TestFx.Vape);
                    yield return new WaitForSeconds(1.6f);
                    yield return Shot("123_penjamin_blur");
                    yield return new WaitForSeconds(5f);
                }
                // The NPCs: all of them in a row, then Marc talking, Shane's alert building and Jaden shooting.
                {
                    SimPlayer me9 = host.Local;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                    var npcs = host.Sim.Npcs;
                    for (int i = 0; i < npcs.Count; i++)
                    {
                        npcs[i].Pos = me9.Pos + new Vector2((i - (npcs.Count - 1) * 0.5f) * 0.85f, 1.9f);
                        npcs[i].Facing = -Mathf.PI / 2f;
                    }
                    yield return new WaitForSeconds(0.25f);
                    gameHud.Refresh();
                    yield return Shot("98_npc_lineup");
                    foreach (Npc n in npcs) n.Pos += new Vector2(0f, 40f);
                    Marc marc = npcs.Find(n => n is Marc) as Marc;
                    Shane shane = npcs.Find(n => n.GetType() == typeof(Shane)) as Shane;
                    Jaden jaden = npcs.Find(n => n is Jaden) as Jaden;
                    SimPlayer mark = host.SpawnDummy(Role.Survivor);
                    if (marc != null && shane != null && jaden != null && mark != null)
                    {
                        marc.Pos = me9.Pos + new Vector2(-0.5f, 1.4f);
                        marc.Talk(host.Sim, me9);
                        shane.Pos = me9.Pos + new Vector2(-2.3f, 1.7f);
                        shane.Meter[me9.Id] = 0.45f;
                        host.Sim.Teleport(mark.Id, me9.Pos + new Vector2(0.9f, 3.1f));
                        jaden.Pos = me9.Pos + new Vector2(2.3f, 1.3f);
                        jaden.Avenge(host.Sim, mark);
                        yield return new WaitForSeconds(0.75f);
                        gameHud.Refresh();
                        yield return Shot("99_npc_talk_alert_and_pistol");
                    }
                    host.Sim.ClearDummies();
                    host.Sim.RespawnNpcs();
                }
                // The story NPCs: Chris at his ambulance, Chacko in the lounge, Sexton's Hemp Beam, Plasma's beast, Chris ascending.
                {
                    SimPlayer me10 = host.Local;
                    var npcs = host.Sim.Npcs;
                    Chris chris = npcs.Find(n => n is Chris) as Chris;
                    Sexton sexton = npcs.Find(n => n is Sexton) as Sexton;
                    Plasma plasma = npcs.Find(n => n is Plasma) as Plasma;
                    if (world.Ambulance != null)
                    {
                        Vector2 across = new Vector2(-Mathf.Sin(world.AmbulanceAngle), Mathf.Cos(world.AmbulanceAngle));
                        Vector2 spot = world.AmbulanceCentre + across * 4.6f;
                        if (chris != null) chris.Pos = world.AmbulanceCentre + across * 3.2f + new Vector2(Mathf.Cos(world.AmbulanceAngle), Mathf.Sin(world.AmbulanceAngle)) * 1.5f;
                        yield return Stage(player, V(spot - Vector2.up * 1.2f), Vector2.up, wanderer, away);
                        yield return new WaitForSeconds(0.6f);
                        gameHud.Refresh();
                        yield return Shot("100_ambulance_and_chris");
                        // The ambulance in its own light: the player's turned away, the camera on the van.
                        Vector2 awayFromVan = (spot - world.AmbulanceCentre).normalized;
                        yield return Stage(player, V(world.AmbulanceCentre + awayFromVan * 7.5f), awayFromVan, wanderer, away);
                        player.AimOverride = awayFromVan;
                        yield return new WaitForSeconds(0.9f);
                        gameHud.Refresh();
                        yield return Shot("149_ambulance_lights");
                        player.AimOverride = null;
                    }
                    // The graveyard's gates: chained shut, the lock being picked, then swung open.
                    {
                        Vector2 gate = world.Layout.GraveyardGate(1.4f);
                        Vector2 inward = (world.Layout.Graveyard.center - gate).normalized;
                        yield return Stage(player, V(gate), inward, wanderer, away);
                        player.AimOverride = inward;
                        yield return new WaitForSeconds(0.7f);
                        gameHud.Refresh();
                        yield return Shot("150_graveyard_gates_locked");
                        // E until the picking starts (anything lying at the gateway is picked up first).
                        for (int tries = 0; tries < 4 && host.Local.Action != ActionKind.PickLock; tries++)
                        {
                            host.Sim.CancelAction(host.Local);
                            player.ExtraButtons = Btn.Interact;
                            yield return new WaitForSeconds(0.1f);
                            player.ExtraButtons = Btn.None;
                            yield return new WaitForSeconds(0.25f);
                        }
                        LogState("151 after E", host);
                        yield return new WaitForSeconds(0.85f);
                        gameHud.Refresh();
                        yield return Shot("151_lockpick_dial");
                        int gi = world.Doors.FindIndex(d => d != null && d.gate);
                        if (gi >= 0)
                        {
                            host.Sim.CancelAction(host.Local);
                            host.Sim.DoorLocked[gi] = false;
                            host.Sim.SetDoor(gi, true);
                        }
                        yield return new WaitForSeconds(1.3f);
                        gameHud.Refresh();
                        yield return Shot("152_graveyard_gates_open");
                        player.AimOverride = null;
                    }
                    if (world.Layout.Plan != null && world.Layout.Plan.LoungeRoom >= 0)
                    {
                        Vector2 seat = world.Layout.Plan.LoungeSeat, tv = world.Layout.Plan.LoungeTv;
                        Vector2 mid = seat + (tv - seat).normalized * Mathf.Min(1.1f, Vector2.Distance(seat, tv) * 0.6f);
                        player.Teleport(world.transform.TransformPoint(new Vector3(mid.x, world.BuildingFloor + 0.05f, mid.y)));
                        player.AimOverride = (seat - mid).normalized;
                        cameraRig.Snap();
                        yield return new WaitForSeconds(0.8f);
                        gameHud.Refresh();
                        yield return Shot("101_lounge_chacko");
                        // A survivor's bottle: it kills him, and he blows apart (a dummy throws it, so the NPCs it sets on
                        // the thrower give up as soon as the dummy is gone).
                        Chacko chacko = null;
                        foreach (Npc n in host.Sim.Npcs) if (n is Chacko c) chacko = c;
                        SimPlayer thrower = host.SpawnDummy(Role.Survivor);
                        if (chacko != null && thrower != null)
                        {
                            host.Sim.Teleport(thrower.Id, mid);
                            chacko.ItemHit(host.Sim, thrower, "bottle");
                            yield return new WaitForSeconds(0.14f);
                            gameHud.Refresh();
                            yield return Shot("141_chacko_slain_by_a_survivor");
                        }
                        host.Sim.ClearDummies();
                    }
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                    SimPlayer foe = host.SpawnDummy(Role.Survivor);
                    if (sexton != null && foe != null)
                    {
                        sexton.Pos = me10.Pos + new Vector2(-1.8f, 2.6f);
                        host.Sim.Teleport(foe.Id, me10.Pos + new Vector2(1.6f, 2.2f));
                        sexton.ItemHit(host.Sim, foe, "bottle");
                        float waited = 0f;
                        while (!sexton.Beaming && waited < 4f) { waited += Time.deltaTime; yield return null; }
                        yield return new WaitForSeconds(0.4f);
                        gameHud.Refresh();
                        yield return Shot("102_sexton_hemp_beam");
                        sexton.Pos += new Vector2(0f, 60f);
                    }
                    if (plasma != null && foe != null)
                    {
                        plasma.Pos = me10.Pos + new Vector2(-1.2f, 1.7f);
                        host.Sim.Teleport(foe.Id, me10.Pos + new Vector2(1.0f, 1.3f));
                        plasma.ItemHit(host.Sim, foe, "bottle");
                        yield return new WaitForSeconds(Balance.Plasma.TransformTime + 0.5f);
                        gameHud.Refresh();
                        yield return Shot("103_plasma_beast");
                        plasma.Pos += new Vector2(0f, 60f);
                    }
                    if (chris != null)
                    {
                        chris.Pos = me10.Pos + new Vector2(0.4f, 1.0f);
                        chris.State = Chris.Mode.Ascend;
                        chris.AscendT = 0.6f;
                        yield return new WaitForSeconds(0.9f);
                        gameHud.Refresh();
                        yield return Shot("104_chris_ascends");
                    }
                    host.Sim.ClearDummies();
                    host.Sim.RespawnNpcs();
                    // Zach sees every stake in play on his map.
                    if (host.SwitchRole())
                    {
                        gameHud.Map.SetOpen(true);
                        yield return new WaitForSeconds(0.5f);
                        yield return Shot("105_zach_map_stakes");
                        gameHud.Map.SetOpen(false);
                        host.SwitchRole();
                    }
                }
                // Testing dummies: a survivor standing, one downed, and a stunned Zach.
                SimPlayer d1 = host.SpawnDummy(Role.Survivor);
                SimPlayer z1 = host.SpawnDummy(Role.Hunter);
                if (d1 != null && z1 != null)
                {
                    SimPlayer me2 = host.Local;
                    host.Sim.Teleport(d1.Id, me2.Pos + new Vector2(-1.2f, 2f));
                    host.Sim.Teleport(z1.Id, me2.Pos + new Vector2(1.4f, 2.4f));
                    SimPlayer d2 = host.SpawnDummy(Role.Survivor);
                    host.Sim.Teleport(d2.Id, me2.Pos + new Vector2(0.2f, 3.4f));
                    host.Sim.HurtSurvivor(d2, 1f, null, "capture");
                    host.Sim.PlayTestFx(z1.Id, TestFx.Stun);
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot("81_testing_dummies");
                    host.Sim.ClearDummies();
                }
                // A survivor on a stake (its aura shows to teammates), then the results screen.
                if (host.Sim.Map.Stakes.Count > 0)
                {
                    Vector2 stake = host.Sim.Map.Stakes[0];
                    SimPlayer zd = host.SpawnDummy(Role.Hunter);
                    SimPlayer sd = host.SpawnDummy(Role.Survivor);
                    if (zd != null && sd != null)
                    {
                        host.Sim.Teleport(zd.Id, stake + new Vector2(1.2f, -0.8f));
                        sd.Pos = stake;
                        sd.Facing = -Mathf.PI / 2f;
                        host.Sim.StakeSurvivor(zd, sd, 0);
                        yield return Stage(player, new Vector3(stake.x, 0f, stake.y - 3.2f), Vector2.up, wanderer, away);
                        yield return new WaitForSeconds(1.2f);
                        gameHud.Refresh();
                        yield return Shot("84_staked");
                        host.Sim.ClearDummies();
                    }
                }
                // The paths: a trail branching off a main path, up close.
                {
                    PathNetwork net = world.Terrain.Paths;
                    int trail = net != null ? net.Infos.FindIndex(i => !i.Main) : -1;
                    if (trail >= 0)
                    {
                        List<Vector2> tp = net.Paths[trail];
                        Vector2 end = tp[tp.Count - 1], before = tp[Mathf.Max(0, tp.Count - 12)];
                        yield return Stage(player, V(end + (before - end).normalized * 3.5f), (end - before).normalized, wanderer, away);
                        yield return new WaitForSeconds(0.4f);
                        yield return Shot("106_path_junction");
                    }
                    if (net != null && net.Paths.Count > 0)
                    {
                        List<Vector2> mp = net.Paths[0];
                        Vector2 mid = mp[mp.Count / 2], next = mp[Mathf.Min(mp.Count - 1, mp.Count / 2 + 6)];
                        yield return Stage(player, V(mid), (next - mid).normalized, wanderer, away);
                        yield return new WaitForSeconds(0.4f);
                        yield return Shot("107_main_path");
                    }
                }
                // A survivor in Penjamin's gas, hands over the eyes (close up).
                {
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 2f)), Vector2.up, wanderer, away);
                    gameHud.PlayTestFx(TestFx.Vape);
                    float o = cameraRig.orthographicSize;
                    cameraRig.orthographicSize = o * 0.45f;
                    yield return new WaitForSeconds(1.4f);
                    yield return Shot("108_penjamin_cover_eyes");
                    cameraRig.orthographicSize = o;
                    yield return new WaitForSeconds(3f);
                }
                // A bottle through a window: the glass gone, the frame open to climb through.
                if (world.Windows.Count > 0 && world.Windows[0] != null)
                {
                    SimPlayer me11 = host.Local;
                    WindowPiece win = world.Windows[0];
                    Vector2 mid = (win.a + win.b) * 0.5f, along = (win.b - win.a).normalized, normal = new Vector2(-along.y, along.x);
                    Vector2 stand = mid + normal * 3f;
                    if (world.GroundHeight(stand) > world.GroundHeight(mid + normal * -3f) + 0.5f || world.Layout.Building.Contains(stand)) stand = mid - normal * 3f;
                    player.Teleport(world.transform.TransformPoint(new Vector3(stand.x, world.GroundHeight(stand) + 0.05f, stand.y)));
                    player.AimOverride = (mid - stand).normalized;
                    cameraRig.Snap();
                    yield return Wait(10);
                    player.SelectedSlot = me11.Inv.FirstSlotOf(ItemType.Bottle);
                    yield return Wait(3);
                    player.ExtraButtons = Btn.Primary;
                    yield return Wait(2);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.8f);
                    gameHud.Refresh();
                    yield return Shot("109_window_broken_by_bottle");
                    player.SelectedSlot = -1;
                    player.AimOverride = null;
                }
                // Health over heads (fresh, then faded), and the edge of the woods: the light flickering past the first ring,
                // dying past the second, the dark, and its bite pulsing red.
                {
                    SimPlayer meB = host.Local;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 3f)), Vector2.up, wanderer, away);
                    SimPlayer dummyB = host.SpawnDummy(Role.Survivor);
                    if (dummyB != null)
                    {
                        host.Sim.Teleport(dummyB.Id, meB.Pos + new Vector2(1.6f, 2.2f));
                        dummyB.Shield = 0.6f;
                        yield return new WaitForSeconds(0.3f);
                        host.Sim.HurtSurvivor(dummyB, 0.3f, null, "test");
                        host.Sim.HurtSurvivor(dummyB, 0.3f, null, "vape");
                        yield return new WaitForSeconds(0.25f);
                        gameHud.Refresh();
                        yield return Shot("134_head_bars_fresh");
                        yield return new WaitForSeconds(6f);
                        yield return Shot("135_head_bars_faded");
                        host.Sim.ClearDummies();
                    }
                    // Blood: hurt, a survivor drips it as they go (in testing mode they see their own trail), tiny splatters
                    // of every shape lying on the ground; and standing still, only a few land under them.
                    {
                        host.Sim.HurtSurvivor(meB, 0.4f, null, "test");
                        player.MoveOverride = new Vector2(1f, 0.35f).normalized;
                        yield return new WaitForSeconds(2.2f);
                        player.MoveOverride = null;
                        yield return new WaitForSeconds(3f);
                        float ob = cameraRig.orthographicSize;
                        cameraRig.orthographicSize = ob * 0.32f;
                        player.AimOverride = new Vector2(-1f, -0.3f).normalized;
                        yield return new WaitForSeconds(0.3f);
                        gameHud.Refresh();
                        yield return Shot("144_blood_splatters");
                        player.AimOverride = null;
                        cameraRig.orthographicSize = ob;
                        Game.MatchSim.RestoreSurvivor(meB, 1f);
                    }
                    Vector2 edgeOut = new Vector2(0.6f, -0.8f).normalized;
                    yield return Stage(player, V(edgeOut * (host.Sim.Ring1 + 5f)), edgeOut, wanderer, away);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot("136_edge_flicker");
                    yield return Stage(player, V(edgeOut * (host.Sim.Ring2 + 5f)), edgeOut, wanderer, away);
                    yield return new WaitForSeconds(1.5f);
                    yield return Shot("137_edge_light_dying");
                    yield return new WaitForSeconds(1.8f);
                    yield return Shot("138_edge_dark");
                    int hits = meB.BoundaryHits;
                    float until = Time.time + 5f;
                    while (meB.BoundaryHits == hits && Time.time < until) yield return null;
                    yield return new WaitForSeconds(0.1f);
                    yield return Shot("139_edge_red_pulse");
                    // Gone into the woods: no lit spot where they fell, the word written out in the black. (Staged without
                    // eliminating the capture's survivor, which would end the match for the shots that follow.)
                    meB.LostInWoods = true;
                    FindAnyObjectByType<Vision.UI.ScreenOverlays>()?.WriteOut("Sacrificed", 2.6f);
                    yield return new WaitForSeconds(0.55f);
                    yield return Shot("145_sacrificed_writing");
                    yield return new WaitForSeconds(1.65f);
                    yield return Shot("146_sacrificed_written");
                    meB.LostInWoods = false;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 3f)), Vector2.up, wanderer, away);
                    host.Sim.Get(meB.Id).Hp = 1f;
                    yield return new WaitForSeconds(2f);
                }
                // Zach's kit: his HUD, a swing, the Burst, Penjamin, the Hemp Battery and Beam.
                if (host.SwitchRole())
                {
                    SimPlayer z = host.Local;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 4f)), Vector2.up, wanderer, away);
                    SimPlayer target = host.SpawnDummy(Role.Survivor);
                    if (target != null) host.Sim.Teleport(target.Id, z.Pos + new Vector2(0f, 3.2f));
                    IEnumerator Press(Btn b, int frames = 3)
                    {
                        player.ExtraButtons = b;
                        yield return Wait(frames);
                        player.ExtraButtons = Btn.None;
                    }
                    // The X, up close: a charged forehand (cocked over his right shoulder, then its blood-red trail), a
                    // charged backhand straight after (cocked over his left), then after a pause a light forehand and its
                    // backhand (steel trails).
                    player.ExtraButtons = Btn.Primary;
                    yield return new WaitForSeconds(0.9f);
                    gameHud.Refresh();
                    yield return Shot("86_zach_charge_and_hud");
                    float zoomX = cameraRig.orthographicSize;
                    cameraRig.orthographicSize = zoomX * 0.5f;
                    yield return new WaitForSeconds(1.15f);
                    yield return Shot("124_charged_forehand_cocked");
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.2f);
                    yield return Shot("87_zach_heavy_swing");
                    yield return new WaitForSeconds(0.4f);
                    player.ExtraButtons = Btn.Primary;
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("125_charging_backhand");
                    yield return new WaitForSeconds(1.4f);
                    player.ExtraButtons = Btn.None;
                    yield return new WaitForSeconds(0.2f);
                    yield return Shot("126_charged_backhand_trail");
                    yield return new WaitForSeconds(1.6f);
                    yield return Press(Btn.Primary, 2);
                    yield return new WaitForSeconds(0.17f);
                    yield return Shot("127_light_forehand_trail");
                    yield return new WaitForSeconds(0.35f);
                    yield return Press(Btn.Primary, 2);
                    yield return new WaitForSeconds(0.17f);
                    yield return Shot("110_zach_backhand");
                    // The blade low: a generator and a gravestone struck, each throwing off dust and debris (and clanking).
                    {
                        var marks = new System.Collections.Generic.List<(string shot, Vector2 at, Vector2 face)>();
                        if (host.Sim.Map.Generators.Count > 0) marks.Add(("130_machete_generator", host.Sim.Map.Generators[host.Sim.Map.Generators.Count - 1], Vector2.up));
                        foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
                            if (t.name == "Headstone")
                            {
                                Vector3 sl = world.transform.InverseTransformPoint(t.position);
                                marks.Add(("131_machete_gravestone", new Vector2(sl.x, sl.z), Vector2.up));
                                break;
                            }
                        // A ruined brick wall (struck from one side of it, at its middle).
                        foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
                            if (t.name == "Brick Wall Ruin")
                            {
                                Vector3 sl = world.transform.InverseTransformPoint(t.position);
                                Vector3 across = world.transform.InverseTransformDirection(t.forward);
                                var side = new Vector2(across.x, across.z).normalized;
                                marks.Add(("132_machete_brick_ruin", new Vector2(sl.x, sl.z), -side));
                                break;
                            }
                        foreach (var (shot, at, face) in marks)
                        {
                            yield return Stage(player, V(at - face * 1.3f), face, wanderer, away);
                            yield return new WaitForSeconds(0.9f);
                            yield return Press(Btn.Primary, 2);
                            yield return new WaitForSeconds(0.24f);
                            yield return Shot(shot);
                        }
                        // Soham struck once: he blows up, leaving three jars of piss where he stood.
                        Soham soham = null;
                        foreach (Npc n in host.Sim.Npcs) if (n is Soham sh && sh.Alive) { soham = sh; break; }
                        if (soham != null)
                        {
                            yield return Stage(player, V(L.Spawn + new Vector2(0f, 4f)), Vector2.up, wanderer, away);
                            soham.Pos = host.Local.Pos + Vector2.up * Scale.D(70f);
                            yield return new WaitForSeconds(0.5f);
                            soham.Pos = host.Local.Pos + Vector2.up * Scale.D(70f);
                            yield return Press(Btn.Primary, 2);
                            yield return new WaitForSeconds(0.4f);
                            yield return Shot("172_soham_slain");
                            yield return new WaitForSeconds(1.2f);
                            yield return Shot("172b_soham_jars");
                        }
                        // Zach pissed on twice (the testing effect): yellow all over, the extra damage stacked in his status.
                        gameHud.PlayTestFx(TestFx.Piss);
                        yield return new WaitForSeconds(0.3f);
                        gameHud.PlayTestFx(TestFx.Piss);
                        yield return new WaitForSeconds(2.3f);
                        yield return Shot("171_zach_pissed_on");
                        host.Local.PissStacks = new float[0];
                        yield return Stage(player, V(L.Spawn + new Vector2(0f, 4f)), Vector2.up, wanderer, away);
                        if (target != null) host.Sim.Teleport(target.Id, z.Pos + new Vector2(0f, 3.2f));
                    }
                    // Facing the camera, a burst of frames through each cut of the X: a light forehand, its backhand, then a
                    // charged forehand.
                    player.AimOverride = Vector2.down;
                    // Fixed steps of game time, so a frame spent saving a screenshot doesn't skip part of a cut.
                    Time.captureFramerate = 240;
                    yield return new WaitForSeconds(1.5f);
                    yield return Press(Btn.Primary, 2);
                    for (int f = 0; f < 8; f++)
                    {
                        yield return new WaitForSeconds(0.035f);
                        yield return Shot($"128_x_forehand_{f}");
                    }
                    yield return Press(Btn.Primary, 2);
                    for (int f = 0; f < 8; f++) { yield return new WaitForSeconds(0.035f); yield return Shot($"129_x_backhand_{f}"); }
                    yield return new WaitForSeconds(1.5f);
                    player.ExtraButtons = Btn.Primary;
                    yield return new WaitForSeconds(2.1f);
                    player.ExtraButtons = Btn.None;
                    for (int f = 0; f < 8; f++) { yield return new WaitForSeconds(0.035f); yield return Shot($"130_x_charged_{f}"); }
                    Time.captureFramerate = 0;
                    player.AimOverride = Vector2.up;
                    cameraRig.orthographicSize = zoomX;
                    yield return new WaitForSeconds(0.8f);
                    yield return Press(Btn.Secondary, 1);
                    yield return new WaitForSeconds(0.1f);
                    yield return Shot("88_soundcloud_burst");
                    yield return new WaitForSeconds(1.5f);
                    // Penjamin, fired straight from the match (a key press can fall between ticks on a slow frame).
                    {
                        SimPlayer pz = host.Local;
                        host.Sim.SpawnVape(pz.Pos, pz.Facing, Scale.D(Balance.Hunter.Vape.DefaultView * Balance.Hunter.Vape.ReachMul), pz.Id, false);
                    }
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot("89_penjamin");

                    // Blown on its wind: drifted off, wider, taller and thinner, a few seconds on.
                    yield return new WaitForSeconds(2.6f);
                    yield return Shot("89b_penjamin_blown");
                    yield return new WaitForSeconds(2.4f);
                    yield return Press(Btn.Ability);
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot("90_hemp_battery");
                    // The beam aimed at the nearest tree or wall, to see it splash.
                    {
                        Vector2 me12 = z.Pos, aimAt = me12 + Vector2.up * 6f;
                        float bestD = 9f;
                        for (int a = 0; a < 24; a++)
                        {
                            var dir = new Vector2(Mathf.Cos(a * Mathf.PI / 12f), Mathf.Sin(a * Mathf.PI / 12f));
                            float hit = host.Sim.Geo.CastSight(me12, dir, 9f);
                            if (hit > 2.5f && hit < bestD) { bestD = hit; aimAt = me12 + dir * hit; }
                        }
                        player.AimOverride = (aimAt - me12).normalized;
                        yield return Wait(5);
                    }
                    yield return Press(Btn.Beam);
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("91_hemp_beam_charging");
                    yield return new WaitForSeconds(1.0f);
                    yield return Shot("92_hemp_beam");
                    player.AimOverride = Vector2.up;
                    yield return new WaitForSeconds(3.5f);
                    yield return Press(Btn.Ability);
                    // Zach past the second ring: his light dies, but a small halo stays round him.
                    {
                        Vector2 haloOut = new Vector2(0.6f, -0.8f).normalized;
                        yield return Stage(player, V(haloOut * (host.Sim.Ring2 + 4f)), haloOut, wanderer, away);
                        yield return new WaitForSeconds(1.4f);
                        yield return Shot("147_zach_light_dying");
                        yield return new WaitForSeconds(2.6f);
                        yield return Shot("148_zach_halo_in_the_dark");
                    }
                    // Through the end of the world and out on the far side.
                    {
                        Vector2 crossOut = new Vector2(-0.8f, 0.6f).normalized;
                        yield return Stage(player, V(crossOut * (host.Sim.Ring3 - 1f)), crossOut, wanderer, away);
                        player.MoveOverride = crossOut;
                        float until = Time.time + 4f;
                        while (z.CrossCd <= 0f && Time.time < until) yield return null;
                        player.MoveOverride = null;
                        yield return new WaitForSeconds(0.4f);
                        gameHud.Refresh();
                        yield return Shot("140_zach_crossed_over");
                        yield return Stage(player, V(L.Spawn + new Vector2(0f, 4f)), Vector2.up, wanderer, away);
                        yield return new WaitForSeconds(1.5f);
                    }
                    host.Sim.ClearDummies();
                    host.SwitchRole();
                }
                // The newer rules: Miga Sushi's gate (powered, then open), the hatch open by the dock, a ghost (through a
                // wall, taking a slain Jaden's body, walking as him, knocked out of it and seeing everything), Monique's
                // arrow, wading through the lake, and the lobby's balance card.
                {
                    SimPlayer meG = host.Local;
                    if (L.HasSushiGate)
                    {
                        host.Sim.Gate.Powered = host.Sim.Gate2.Powered = true;
                        Vector2 o = L.SushiGateOut();
                        yield return Stage(player, V(L.SushiGateLever + o * 1.2f), -o, wanderer, away);
                        yield return new WaitForSeconds(0.6f);
                        gameHud.Refresh();
                        yield return Shot("160_sushi_gate_powered");
                        host.Sim.Gate2.Progress = 1f;
                        host.Sim.Gate2.Open = true;
                        yield return new WaitForSeconds(3f);
                        yield return Stage(player, V(L.SushiGateMid() + o * 2.5f), -o, wanderer, away);
                        yield return Wait(10);
                        yield return Shot("161_sushi_gate_open");
                    }
                    host.Sim.HatchOpen = true;
                    Vector2 toMid = (-L.LakeCentre).normalized;
                    Vector2 hatchSide = new Vector2(-toMid.y, toMid.x);
                    yield return Stage(player, V(L.Hatch + hatchSide * 1.8f), -hatchSide, wanderer, away);
                    yield return new WaitForSeconds(1.6f);
                    gameHud.Refresh();
                    yield return Shot("162_hatch_open");
                    host.Sim.HatchOpen = false;
                    host.Sim.HatchClosed = true;
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("162b_hatch_slammed");
                    host.Sim.HatchClosed = false;

                    // A ghost: Jaden slain beside us, then T (our own body left at our feet), through the building's wall,
                    // Jaden's body taken (rising, then walking as him), knocked out of it by a Zach, and seeing everything.
                    Vector2 spot = L.Spawn + new Vector2(0f, 6f);
                    yield return Stage(player, V(spot), Vector2.up, wanderer, away);
                    Jaden jd = null;
                    foreach (Npc n in host.Sim.Npcs) if (n is Jaden j) jd = j;
                    if (jd != null)
                    {
                        jd.Pos = spot + new Vector2(2.4f, 1.6f);
                        jd.Snipe(host.Sim, meG);
                    }
                    yield return new WaitForSeconds(1f);
                    host.CycleRole();
                    meG = host.Local;
                    yield return Wait(12);
                    gameHud.Refresh();
                    yield return Shot("163_ghost");
                    Rect bld = L.Building;
                    yield return Stage(player, V(new Vector2(bld.center.x, bld.yMin - 1.4f)), Vector2.up, wanderer, away);
                    player.MoveOverride = Vector2.up;
                    yield return new WaitForSeconds(1.6f);
                    player.MoveOverride = null;
                    Debug.Log($"[Vision] a ghost through the building's wall: inside {bld.Contains(meG.Pos)} at {meG.Pos.y - bld.yMin:0.0} m past it");
                    yield return Wait(6);
                    yield return Shot("163b_ghost_through_wall");
                    Body jb = jd != null ? host.Sim.Bodies.Find(b => b.OfNpc && b.Of == jd.Id) : null;
                    if (jb != null)
                    {
                        yield return Stage(player, V(jb.Pos + new Vector2(-1.2f, -0.6f)), (jb.Pos - meG.Pos).normalized, wanderer, away);
                        yield return Wait(4);
                        player.ExtraButtons = Btn.Interact;
                        yield return Wait(3);
                        player.ExtraButtons = Btn.None;
                        yield return new WaitForSeconds(Balance.Ghost.PossessTime * 0.45f);
                        yield return Shot("164_body_rising");
                        yield return new WaitForSeconds(Balance.Ghost.PossessTime * 0.55f + 0.5f);
                        gameHud.Refresh();
                        Debug.Log($"[Vision] reanimated: {meG.Reanimated} as {meG.BodyName}, banner \"{gameHud.BannerText}\"");
                        yield return Shot("165_reanimated_as_jaden");
                        SimPlayer z = host.Sim.AddDummy(Role.Hunter, meG.Pos + new Vector2(1.4f, 0f));
                        if (z != null) z.Facing = Mathf.PI;
                        host.Sim.KnockOut(meG, z, "slash");
                        yield return new WaitForSeconds(0.2f);
                        yield return Shot("165b_knocked_out");
                        yield return new WaitForSeconds(1.4f);
                        gameHud.Refresh();
                        yield return Shot("165c_ghost_sees_everything");
                        host.Sim.ClearDummies();
                    }
                    // Back to Zach, then a survivor again, for the shots that follow.
                    host.CycleRole();
                    host.SwitchRole();
                    meG = host.Local;
                    TopDownCamera.TestZoom = 1f;
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 6f)), Vector2.up, wanderer, away);
                    yield return new WaitForSeconds(0.7f);

                    // Monique's arrow, pointing at a Zach off to the north-east.
                    SimPlayer far = host.Sim.AddDummy(Role.Hunter, meG.Pos + new Vector2(30f, 30f));
                    meG.ArrowT = Balance.Monique.ArrowSec;
                    yield return new WaitForSeconds(0.6f);
                    gameHud.Refresh();
                    yield return Shot("166_monique_arrow");
                    meG.ArrowT = 0f;
                    host.Sim.ClearDummies();
                    _ = far;

                    // Wading.
                    Vector2 inLake = L.DockStart - toMid * 4f + new Vector2(-toMid.y, toMid.x) * 3f;
                    yield return Stage(player, V(inLake), Vector2.up, wanderer, away);
                    player.MoveOverride = new Vector2(-toMid.y, toMid.x);
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot("167_wading_ripples");
                    player.MoveOverride = null;

                    // The dock: out of the water and up onto its boards, walking along them.
                    Vector2 dockAlong = (L.DockEnd - L.DockStart).normalized;
                    Vector2 dockSide = new Vector2(-dockAlong.y, dockAlong.x);
                    Vector2 besideDock = Vector2.Lerp(L.DockStart, L.DockEnd, 0.5f) + dockSide * (L.DockHalfWidth + 1.2f);
                    yield return Stage(player, V(besideDock), -dockSide, wanderer, away);
                    player.MoveOverride = -dockSide;
                    // Wading in until the dock's boards are underfoot.
                    float waded = 0f;
                    while (waded < 4f)
                    {
                        Vector3 lw = world.transform.InverseTransformPoint(player.transform.position);
                        if (world.OnDock(new Vector2(lw.x, lw.z), out _)) break;
                        waded += Time.deltaTime;
                        yield return null;
                    }
                    Vector3 up = world.transform.InverseTransformPoint(player.transform.position);
                    world.OnDock(new Vector2(up.x, up.z), out float upDeck);
                    Debug.Log($"[Vision] onto the dock after {waded:0.00} s of wading, at {up.y - upDeck:0.000} m off its deck");
                    yield return new WaitForSeconds(0.3f);
                    player.MoveOverride = dockAlong;
                    yield return new WaitForSeconds(0.6f);
                    player.MoveOverride = null;
                    Vector3 onDock = world.transform.InverseTransformPoint(player.transform.position);
                    bool dockUnder = world.OnDock(new Vector2(onDock.x, onDock.z), out float deckY);
                    Debug.Log($"[Vision] the dock: on its boards {dockUnder}, standing {onDock.y - deckY:0.000} m off the deck");
                    {
                        // The feet: their ankles' and toes' heights over the boards (they sank through them when the legs
                        // reached for the lake bed).
                        var legs = player.GetComponentInChildren<Vision.Characters.HumanoidAnimator>();
                        float deckW = world.transform.TransformPoint(new Vector3(onDock.x, deckY, onDock.z)).y, k = world.transform.lossyScale.y;
                        if (legs != null && legs.bones != null)
                            Debug.Log($"[Vision] the dock: ankles {(legs.bones[(int)Vision.Characters.Bone.FootL].position.y - deckW) / k:0.000} / {(legs.bones[(int)Vision.Characters.Bone.FootR].position.y - deckW) / k:0.000} m and toes {(legs.bones[(int)Vision.Characters.Bone.ToeL].position.y - deckW) / k:0.000} / {(legs.bones[(int)Vision.Characters.Bone.ToeR].position.y - deckW) / k:0.000} m over its boards");
                    }
                    yield return Shot("169_on_the_dock");

                    // The lobby: its balance card, a joiner's slider moved (in amber).
                    Vision.Net.Listener.LoopbackOnly = true;
                    gameHud.CreateLobby("Capture", 0);
                    if (Vision.Net.NetSession.Active != null)
                    {
                        Vision.Net.NetSession.Active.Tune(2, 1.3f);
                        Vision.Net.NetSession.Active.Tune(0, 4f);
                        yield return Wait(10);
                        yield return Shot("168_lobby_balance");
                        gameHud.PressTuningLock();
                        yield return Wait(10);
                        yield return Shot("168b_lobby_sliders_locked");
                        gameHud.LeaveOnline();
                    }
                    Vision.Net.Listener.LoopbackOnly = false;
                    gameHud.StartTesting(false);
                    yield return Stage(player, V(L.Spawn + new Vector2(0f, 4f)), Vector2.up, wanderer, away);
                }
                var fake = new MatchResult { Winner = Winner.Survivors, Reason = "3 escaped, 1 eliminated", DurationSec = 1043, GeneratorsRepaired = 5, GeneratorsRequired = 5 };
                fake.Stats.Add((1, "Zach", Role.Hunter, new MatchStats { Outcome = "hunter", Hits = 9, Downs = 4, Stakes = 2, StunnedTimes = 5, GensDamaged = 3 }));
                fake.Stats.Add((2, "Ana", Role.Survivor, new MatchStats { Outcome = "escaped", RepairSec = 140f, Heals = 2, Revives = 1, Unstakes = 1, Stuns = 3, TimeAlive = 1000f }));
                fake.Stats.Add((3, "Ben", Role.Survivor, new MatchStats { Outcome = "eliminated", RepairSec = 61f, Heals = 0, Revives = 0, Unstakes = 0, Stuns = 1, TimeAlive = 712f }));
                fake.Stats.Add((4, "Cam", Role.Survivor, new MatchStats { Outcome = "escaped", RepairSec = 88f, Heals = 1, Revives = 0, Unstakes = 1, Stuns = 2, TimeAlive = 1000f }));
                gameHud.ShowResults(fake);
                yield return Wait(5);
                yield return Shot("85_results");
                gameHud.ShowMainMenu();
                gameHud.StartTesting(false);
                gameHud.visible = false;
            }

            // The nine supplies in a row, close up and fully lit.
            yield return SupplyLineup(player, wanderer, away);

            // 11. Tree catalogues and the gait sheet (side-on, fully lit).
            yield return TreeSheets();
            yield return GaitSheet(player);
            yield return CharacterSheets();

            // Frame time over a short run with everything live, and how long the map took to build.
            yield return Stage(player, V(L.Spawn), Vector2.up, wanderer, away);
            wanderer.enabled = true;
            player.AimOverride = new Vector2(0f, 1f);
            yield return Wait(30);
            float t0 = Time.realtimeSinceStartup;
            const int frames = 300;
            yield return Wait(frames);
            float ms = (Time.realtimeSinceStartup - t0) * 1000f / frames;
            // The same again without the HUD (and its maps), to see what the interface costs.
            float hudMs = float.NaN;
            if (gameHud != null)
            {
                gameHud.enabled = false;
                yield return Wait(30);
                float t1 = Time.realtimeSinceStartup;
                yield return Wait(frames);
                hudMs = (Time.realtimeSinceStartup - t1) * 1000f / frames;
                gameHud.enabled = true;
            }
            File.WriteAllText(Path.Combine(folder, "perf.txt"),
                $"avg frame {ms:0.00} ms ({1000f / ms:0} fps) over {frames} frames at {Screen.width}x{Screen.height}; {hudMs:0.00} ms without the HUD\n" +
                $"lights {FindObjectsByType<Vision.Visibility.VisionLight>(FindObjectsSortMode.None).Length}, generators running {GeneratorObjective.RunningCount}\n" +
                $"generation {world.LastGenerationReport}\n");

            // New map: a fresh random seed, the whole map revealed on the full map.
            if (gameHud != null)
            {
                int before = world.seed;
                gameHud.visible = true;
                gameHud.NewMap();
                yield return new WaitForSeconds(1.5f);
                while (world.seed == before) yield return null;
                yield return new WaitForSeconds(1.5f);
                gameHud.Map.SetOpen(true);
                gameHud.Map.RevealAll();
                yield return new WaitForSeconds(0.5f);
                yield return Shot("75_new_map_revealed");
                File.AppendAllText(Path.Combine(folder, "perf.txt"), $"new map {world.LastGenerationReport}\n");
                gameHud.Map.SetOpen(false);
            }

            // The building's footprint on other seeds: L, T/U and S shapes, from above.
            foreach (int shapeSeed in new[] { 2, 3, 18, 7 })
            {
                world.Regenerate(shapeSeed);
                yield return new WaitForSeconds(1f);
                yield return Overview($"76_footprint_seed_{shapeSeed}", world.Layout.Building.center + new Vector2(0f, 2f), 21f);
            }
            Application.Quit();
        }

        /// <summary>The raw scene from high above, centred on a design-unit point, with the clip planes opened up.</summary>
        IEnumerator SupplyLineup(PlayerController player, Wanderer wanderer, Vector3 away)
        {
            float ortho = cameraRig.orthographicSize;
            Vector2 at = world.Layout.Spawn + new Vector2(0f, 3f);
            var row = new System.Collections.Generic.List<GameObject>();
            var rng = new System.Random(7);
            for (int i = 0; i < Items.All.Length; i++)
            {
                var go = new GameObject("Lineup " + Items.All[i]);
                go.transform.SetParent(world.transform, false);
                Vector2 p = at + new Vector2((i - 4) * 0.55f, 0f);
                go.transform.localPosition = new Vector3(p.x, TerrainField.Active != null ? TerrainField.Active.Height(p.x, p.y) + 0.02f : 0f, p.y);
                go.transform.localScale = Vector3.one * 1.6f;
                go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(rng, Items.All[i]);
                go.AddComponent<MeshRenderer>().sharedMaterial = world.lowPolyMaterial;
                row.Add(go);
            }
            yield return Stage(player, new Vector3(at.x, 0f, at.y - 0.9f), Vector2.up, wanderer, away);
            cameraRig.orthographicSize = 1.9f * world.transform.lossyScale.x;
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            yield return Wait(8);
            yield return Shot("59_supplies_lineup");
            composite.debugView = VisionComposite.DebugView.Final;
            cameraRig.orthographicSize = ortho;
            foreach (GameObject go in row) Destroy(go);
        }

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

        /// <summary>
        /// The character models (every one, front and three-quarter) and the action clips (each at its key moment), rendered
        /// off to the side in daylight, as contact sheets.
        /// </summary>
        IEnumerator CharacterSheets()
        {
            const int w = 260, h = 380;
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
                moon.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.53f);
            float scale = world.transform.lossyScale.x;

            // Who: the survivor, Zach, then the NPCs.
            var specs = new System.Collections.Generic.List<CharacterSpec> { CharacterSpec.Survivor(), CharacterSpec.Zach() };
            foreach (string n in Vision.Game.Texts.NpcNames) specs.Add(CharacterSpec.Npc(n));
            var made = new System.Collections.Generic.List<GameObject>();
            GameObject Make(CharacterSpec spec, Vector3 at)
            {
                GameObject go = PropFactory.CreateCharacter(spec.Name, world.lowPolyMaterial, null);
                go.transform.SetParent(world.transform, false);
                go.transform.localPosition = at;
                go.GetComponent<CharacterView>().SetSpec(spec);
                go.GetComponent<HumanoidAnimator>().Drive(Vector3.zero, new Vector2(0f, 1f));
                made.Add(go);
                return go;
            }
            Vector3 origin = new Vector3(0f, 120f, 0f);
            var figures = new GameObject[specs.Count];
            for (int i = 0; i < specs.Count; i++) figures[i] = Make(specs[i], origin + new Vector3(i * 3f, 0f, 0f));
            figures[1].GetComponent<ActionLayer>().Hold(PropKind.Machete);
            yield return Wait(30);
            int cols = specs.Count;
            var lineup = new Texture2D(w * cols, h * 2, TextureFormat.RGB24, false);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < cols; i++)
                {
                    Vector3 dir = row == 0 ? Vector3.forward : new Vector3(1f, 0.3f, 1f).normalized;
                    Vector3 target = figures[i].transform.position + Vector3.up * (1.0f * scale * specs[i].RootScale);
                    cam.orthographicSize = 1.2f * scale;
                    cam.transform.SetPositionAndRotation(target + dir * 10f, Quaternion.LookRotation(-dir));
                    yield return new WaitForEndOfFrame();
                    Blit(rt, lineup, i * w, (1 - row) * h);
                }
            lineup.Apply();
            File.WriteAllBytes(Path.Combine(folder, "82_character_lineup.png"), lineup.EncodeToPNG());
            Destroy(lineup);
            foreach (GameObject go in made) Destroy(go);
            made.Clear();
            yield return null;

            // What: each clip at its key moment, on a survivor (or Zach for his), three-quarter view.
            var clips = new System.Collections.Generic.List<ActionClip>(ActionClips.All);
            var zachClips = new System.Collections.Generic.HashSet<ActionClip>
            {
                ActionClips.Charge, ActionClips.Swing, ActionClips.SwingHeavy, ActionClips.Lunge, ActionClips.Carry, ActionClips.LiftBody, ActionClips.StakeBody,
                ActionClips.Kick, ActionClips.Burst, ActionClips.Vape, ActionClips.Hemp, ActionClips.Beam, ActionClips.SwingBack, ActionClips.RecoilPump,
            };
            const int perRow = 10;
            int rows = (clips.Count + perRow - 1) / perRow;
            var sheet = new Texture2D(w * perRow, h * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < clips.Count; i++)
            {
                ActionClip clip = clips[i];
                bool zach = zachClips.Contains(clip);
                GameObject go = Make(zach ? CharacterSpec.Zach() : CharacterSpec.Survivor(), origin + new Vector3(60f, 0f, i * 4f));
                var layer = go.GetComponent<ActionLayer>();
                if (zach) layer.Hold(clip == ActionClips.RecoilPump ? PropKind.GoldenPump : PropKind.Machete);
                else if (clip == ActionClips.AimLong || clip == ActionClips.RecoilLong) layer.Hold(PropKind.Shotgun);
                else if (clip == ActionClips.AimRifle || clip == ActionClips.RecoilRifle) layer.Hold(PropKind.Sniper);
                else if (clip == ActionClips.AimPistol || clip == ActionClips.RecoilPistol) layer.Hold(PropKind.Pistol);
                else if (clip == ActionClips.Drink) layer.Hold(PropKind.Can);
                else if (clip == ActionClips.Throw) layer.Hold(PropKind.Bottle);
                else if (clip == ActionClips.Tablet) layer.Hold(PropKind.Tablet);
                else layer.Hold(PropKind.Flashlight);
                // The key moment: the release or strike of a one-shot, the middle key of a loop.
                float at = clip.Events.Count > 0 ? clip.Events[0].t : clip.Keys[clip.Keys.Count / 2].t;
                layer.Play(clip, 0f, true, at);
                if (clip == ActionClips.Crawl || clip == ActionClips.Carried) go.GetComponent<HumanoidAnimator>().Prone = true;
            }
            yield return Wait(120);
            for (int i = 0; i < clips.Count; i++)
            {
                GameObject go = made[i];
                // Side-on from the right: forward is to the right of each tile.
                Vector3 dir = new Vector3(1f, 0.12f, 0.15f).normalized;
                Vector3 target = go.transform.position + Vector3.up * (0.95f * scale);
                cam.orthographicSize = 1.25f * scale;
                cam.transform.SetPositionAndRotation(target + dir * 10f, Quaternion.LookRotation(-dir));
                yield return new WaitForEndOfFrame();
                Blit(rt, sheet, (i % perRow) * w, (rows - 1 - i / perRow) * h);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(folder, "83_action_sheet.png"), sheet.EncodeToPNG());
            Destroy(sheet);
            foreach (GameObject go in made) Destroy(go);

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

            // Dead trees: sheets of four designs, each design with its three sizes side by side; then the
            // evergreens on two sheets, each form at short-thick, medium and tall-thin. Every sheet has its own spot.
            var sheets = new System.Collections.Generic.List<(string name, Vector3 centre)>();
            int deadSheets = (LowPolyModels.DeadTreeKinds + 3) / 4;
            for (int sheet = 0; sheet < deadSheets; sheet++)
            {
                float x0 = sheet < 3 ? sheet * 200f : -200f * (sheet - 2);
                for (int d = 0; d < 4; d++)
                {
                    int k = sheet * 4 + d;
                    if (k >= LowPolyModels.DeadTreeKinds) break;
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
            // The central building's office door in its frame, drawn large: face on, and turned to show its edge.
            {
                var rng = new System.Random(5);
                for (int k = 0; k < 2; k++)
                {
                    var door = Place(LowPolyModels.MetalDoor(rng, 1.06f, 2.12f, 0.06f), new Vector3(1000f + k * 7f + 2.2f, 0f, 0f), Vector3.one * 4f);
                    var frame = Place(LowPolyModels.MetalDoorFrame(rng, 1.1f, 2.15f, 0.24f), new Vector3(1000f + k * 7f, 0f, 0f), Vector3.one * 4f);
                    if (k == 1)
                    {
                        door.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                        frame.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                        door.transform.localPosition = frame.transform.localPosition + Quaternion.Euler(0f, 35f, 0f) * new Vector3(2.2f, 0f, 0f);
                    }
                }
                sheets.Add(("47_office_door", new Vector3(1005.5f, 4.6f, 0f)));
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

        /// <summary>The local player's state, into the player log (to see why a staged press did or didn't take).</summary>
        static void LogState(string what, MatchHost host)
        {
            SimPlayer p = host != null ? host.Local : null;
            if (p == null) return;
            Debug.Log($"[Capture] {what}: action {p.Action} ({p.ActionT:0.00}/{p.ActionDur:0.00}) prompt {p.Prompt} target {p.PromptTarget} canAct {p.CanAct} climbing {p.Move.Climbing} slot {p.SelSlot} item {p.Inv.ItemAt(p.SelSlot)} health {p.Health} rounds {RoundsNow()} tick {host.Sim.Tick} timeScale {Time.timeScale} captureFps {Time.captureFramerate}");
        }

        static IEnumerator Wait(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>The rounds flying right now (the match's effects).</summary>
        static int RoundsNow()
        {
            var fx = FindAnyObjectByType<Vision.UI.MatchEffects>();
            return fx != null ? fx.RoundCount : 0;
        }

        /// <summary>
        /// Once a round has left the barrel (more in flight than <paramref name="before"/>), slows time right down and
        /// shoots it <paramref name="gameSeconds"/> into its flight.
        /// </summary>
        /// <summary>
        /// Pulls the trigger in slow motion and holds it until a round leaves the barrel, then lets it fly
        /// <paramref name="gameSeconds"/> and takes the shot (fast rounds are long gone in the time a held press takes
        /// at full speed).
        /// </summary>
        IEnumerator FireAndCatch(PlayerController player, string name, float gameSeconds)
        {
            int before = RoundsNow();
            Time.timeScale = 0.05f;
            player.ExtraButtons = Btn.Primary;
            float t0 = Time.realtimeSinceStartup;
            while (RoundsNow() <= before && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            player.ExtraButtons = Btn.None;
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(gameSeconds / 0.02f);
            yield return Shot(name);
            Time.timeScale = 1f;
        }

        IEnumerator RoundInFlight(string name, int before, float gameSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (RoundsNow() <= before && Time.realtimeSinceStartup - t0 < 2f) yield return null;
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(gameSeconds / 0.02f);
            yield return Shot(name);
            Time.timeScale = 1f;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return Wait(3);
        }
    }
}
