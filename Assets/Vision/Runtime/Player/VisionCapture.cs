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
            Door door = world.Doors[0];
            Door shutter = world.Doors[1];

            // 1. Spawn view: flashlight north towards the clearing, crows and lantern about.
            yield return Stage(player, new Vector3(0f, 0f, -5f), new Vector2(0.3f, -1f), wanderer, new Vector3(1.5f, 0f, -9f));
            yield return Shot("01_clearing_cone");

            // 2. Facing into the dead forest: the cone is cut by trunks and wraps around them.
            yield return Stage(player, new Vector3(-3f, 0f, -2f), new Vector2(-1f, 0.15f), wanderer, new Vector3(-8f, 0f, -2f));
            yield return Shot("02_forest_cone");

            // 3. In front of the closed cabin door, then with it open.
            door.SetOpen(false);
            yield return Stage(player, new Vector3(2.5f, 0f, 7.2f), new Vector2(1f, 0f), wanderer, new Vector3(7f, 0f, 7.2f));
            yield return Shot("03_door_closed");
            door.SetOpen(true);
            yield return Wait(30);
            yield return Shot("04_door_open");
            door.SetOpen(false);

            // 4. Entity right behind the player, next to a campfire: lit ground, but it stays invisible.
            yield return Stage(player, new Vector3(-1f, 0f, -6.5f), new Vector2(0f, 1f), wanderer, new Vector3(-1f, 0f, -9.2f));
            yield return Shot("05_entity_behind_by_fire");
            player.AimOverride = new Vector2(0f, -1f);
            yield return Wait(10);
            yield return Shot("06_entity_turned_towards");

            // 5. Inside the cabin, facing away from the window. The lantern outside lights the ground
            //    south of the cabin, but we only see that once the shutter opens (line of sight, G).
            shutter.SetOpen(false);
            yield return Stage(player, new Vector3(8.3f, 0f, 6.0f), new Vector2(0f, 1f), wanderer, new Vector3(-12f, 0f, -12f));
            yield return Shot("07_shutter_closed_outside_light_hidden");
            shutter.SetOpen(true);
            yield return Wait(30);
            yield return Shot("08_shutter_open_outside_light_seen");
            shutter.SetOpen(false);

            // 6. See-through cone at a wall.
            player.viewer.seeThroughEnabled = true;
            yield return Stage(player, new Vector3(2.5f, 0f, 9.5f), new Vector2(1f, 0f), wanderer, new Vector3(7.5f, 0f, 9.5f));
            yield return Shot("09_see_through_cone");
            player.viewer.seeThroughEnabled = false;

            // 7. Debug views of the spawn shot.
            yield return Stage(player, new Vector3(0f, 0f, -5f), new Vector2(0.3f, -1f), wanderer, new Vector3(1.5f, 0f, -9f));
            composite.debugView = VisionComposite.DebugView.MaskRgb;
            yield return Wait(5);
            yield return Shot("10_debug_mask_rgb");
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            yield return Wait(5);
            yield return Shot("11_debug_scene_only");
            composite.debugView = VisionComposite.DebugView.Final;

            // 8. Moving: walking, sprinting, strafing and backpedalling (input overrides, real gait).
            Vector3 away = new Vector3(-16f, 0f, 16f);
            yield return Stage(player, new Vector3(-1f, 0f, -3f), new Vector2(0f, -1f), wanderer, away);
            player.MoveOverride = new Vector2(0f, -1f);
            yield return Wait(70);
            yield return Shot("12_walking");
            player.SprintOverride = true;
            yield return Wait(50);
            yield return Shot("13_sprinting");
            player.SprintOverride = null;
            yield return Stage(player, new Vector3(-6f, 0f, -12f), new Vector2(0f, 1f), wanderer, away);
            player.MoveOverride = new Vector2(1f, 0f);
            yield return Wait(60);
            yield return Shot("14_strafing");
            player.MoveOverride = new Vector2(0f, -1f);
            yield return Wait(60);
            yield return Shot("15_backpedal");
            player.MoveOverride = null;

            // 9. Close-ups from the gameplay angle: by the campfire, among the trees, at the cabin door.
            float ortho = cameraRig.orthographicSize;
            cameraRig.orthographicSize = 3.2f;
            yield return Stage(player, new Vector3(-0.2f, 0f, -7.4f), new Vector2(-0.3f, -1f), wanderer, new Vector3(-2.2f, 0f, -9.6f));
            yield return Wait(10);
            yield return Shot("16_closeup_campfire");
            composite.debugView = VisionComposite.DebugView.Shadows;
            yield return Wait(3);
            yield return Shot("16b_campfire_shadow_mask");
            composite.debugView = VisionComposite.DebugView.Final;
            yield return Stage(player, new Vector3(-4.5f, 0f, -2f), new Vector2(-1f, 0.2f), wanderer, away);
            yield return Shot("17_closeup_trees");
            door.SetOpen(false);
            yield return Stage(player, new Vector3(3.2f, 0f, 7.2f), new Vector2(1f, 0.1f), wanderer, away);
            yield return Shot("18_closeup_cabin");
            cameraRig.orthographicSize = ortho;

            // 10. The player close up with the flashlight on, from the front, back and side; the wanderer in the beam.
            cameraRig.orthographicSize = 3.2f;
            yield return Stage(player, new Vector3(4f, 0f, -6f), new Vector2(0f, -1f), wanderer, away);
            yield return Shot("20_player_facing_camera");
            WriteCharacterReport(player, wanderer);
            yield return Stage(player, new Vector3(4f, 0f, -6f), new Vector2(0f, 1f), wanderer, away);
            yield return Shot("21_player_back");
            yield return Stage(player, new Vector3(4f, 0f, -6f), new Vector2(1f, 0f), wanderer, away);
            yield return Shot("22_player_side");
            yield return Stage(player, new Vector3(4f, 0f, -6f), new Vector2(-0.25f, -1f), wanderer, new Vector3(3.6f, 0f, -7.6f));
            yield return Shot("23_wanderer_in_beam");
            composite.debugView = VisionComposite.DebugView.Shadows;
            yield return Wait(3);
            yield return Shot("23b_wanderer_flashlight_shadow_mask");
            composite.debugView = VisionComposite.DebugView.Final;
            cameraRig.orthographicSize = ortho;

            // 11. Look: camera effects off, then each look slider low and high (spawn view).
            yield return Stage(player, new Vector3(0f, 0f, -5f), new Vector2(0.3f, -1f), wanderer, away);
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

            // The F4 look panel, as the player sees it.
            if (hud != null)
            {
                hud.visible = true;
                hud.lookPanel = true;
                yield return Wait(5);
                yield return Shot("27_look_panel");
                hud.lookPanel = false;
                hud.visible = false;
            }

            // Soft shadows: trees and the player around a campfire, with the flashlight pointed away; then the mask.
            cameraRig.orthographicSize = 4.5f;
            yield return Stage(player, new Vector3(-8.2f, 0f, 4.4f), new Vector2(1f, 0f), wanderer, new Vector3(-11.5f, 0f, 2.8f));
            wanderer.transform.position = world.transform.TransformPoint(new Vector3(-11.5f, TerrainField.Active != null ? world.Terrain.Height(-11.5f, 2.8f) : 0f, 2.8f));
            yield return Wait(10);
            yield return Shot("29_campfire_soft_shadows");
            composite.debugView = VisionComposite.DebugView.MaskRgb;
            yield return Wait(5);
            yield return Shot("29b_campfire_soft_shadows_mask");
            composite.debugView = VisionComposite.DebugView.Final;
            yield return Stage(player, new Vector3(-1f, 0f, -2f), new Vector2(-1f, 0.05f), wanderer, away);
            yield return Wait(5);
            yield return Shot("30_beam_tree_shadows");
            composite.debugView = VisionComposite.DebugView.MaskRgb;
            yield return Wait(5);
            yield return Shot("30b_beam_tree_shadows_mask");
            composite.debugView = VisionComposite.DebugView.Final;
            cameraRig.orthographicSize = ortho;

            // A tree in the beam, lit from the south and from the west: its shadow starts at the base and its
            // back side is a soft darkness rather than a black silhouette.
            Transform tree = null;
            float bestTree = float.MaxValue;
            foreach (Transform t in world.transform.Find("Static"))
            {
                if (!(t.name.StartsWith("Fir") || t.name.StartsWith("Spruce") || t.name.StartsWith("Dead"))) continue;
                Vector3 lp = world.transform.InverseTransformPoint(t.position);
                float d = new Vector2(lp.x - 2f, lp.z + 16f).sqrMagnitude;
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

            // The hills from high above (raw scene, then as played).
            cameraRig.orthographicSize = 20f;
            yield return Stage(player, new Vector3(0f, 0f, -2f), new Vector2(0f, -1f), wanderer, away);
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            yield return Wait(5);
            yield return Shot("28_hills_survey_scene");
            composite.debugView = VisionComposite.DebugView.Final;
            yield return Wait(5);
            yield return Shot("28b_hills_survey");
            cameraRig.orthographicSize = 44f;
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            yield return Stage(player, new Vector3(0f, 0f, 0f), new Vector2(0f, -1f), wanderer, away);
            yield return Wait(5);
            yield return Shot("28c_whole_map_scene");
            composite.debugView = VisionComposite.DebugView.Final;

            // New content up close, lit by the flashlight: a wreck by its burning barrel, the generator, woods, a fire.
            cameraRig.orthographicSize = 4.5f;
            var closeups = new (string name, Vector3 at, Vector2 aim)[]
            {
                ("31_wreck_and_barrel", new Vector3(25.5f, 0f, -9.5f), new Vector2(0.7f, 0.5f)),
                ("32_generator_by_cabin", new Vector3(15.3f, 0f, 6.7f), new Vector2(0f, 1f)),
                ("33_wreck_north", new Vector3(-3.5f, 0f, 28f), new Vector2(-0.8f, 0.6f)),
            };
            foreach (var c in closeups)
            {
                yield return Stage(player, c.at, c.aim, wanderer, away);
                yield return Wait(5);
                yield return Shot(c.name);
            }
            composite.debugView = VisionComposite.DebugView.SceneOnly;
            foreach (var c in new (string name, Vector3 at)[] { ("34_woods_scene", new Vector3(20f, 0f, 30f)), ("35_meadow_scene", new Vector3(-26f, 0f, 0f)), ("36_dead_forest_scene", new Vector3(-12f, 0f, -18f)) })
            {
                cameraRig.orthographicSize = 9f;
                yield return Stage(player, c.at, Vector2.up, wanderer, away);
                yield return Wait(5);
                yield return Shot(c.name);
            }
            composite.debugView = VisionComposite.DebugView.Final;
            cameraRig.orthographicSize = 2.2f;
            yield return Stage(player, new Vector3(-2.3f, 0f, -9.6f), new Vector2(1f, 0.2f), wanderer, away);
            for (int f = 0; f < 3; f++)
            {
                yield return Wait(9);
                yield return Shot($"37_fire_frame_{f}");
            }
            cameraRig.orthographicSize = ortho;

            // The HUD: prompt at a supply, a filled inventory, hurt, paused, dead.
            if (gameHud != null && player.GetComponent<PlayerStats>() is PlayerStats ps)
            {
                gameHud.visible = true;
                cameraRig.orthographicSize = ortho;
                yield return Stage(player, new Vector3(-4.8f, 0f, 30.2f), new Vector2(0.6f, 0.8f), wanderer, away);
                Pickup near = null;
                foreach (Pickup pk in world.Pickups)
                    if (pk != null && (near == null || Vector3.Distance(pk.transform.position, player.transform.position) < Vector3.Distance(near.transform.position, player.transform.position))) near = pk;
                if (near != null)
                {
                    Vector3 at = near.transform.position - player.transform.position;
                    at.y = 0f;
                    player.Teleport(near.transform.position - at.normalized * (1.2f * player.transform.lossyScale.x));
                    player.AimOverride = new Vector2(at.x, at.z).normalized;
                    cameraRig.Snap();
                }
                yield return Wait(10);
                gameHud.Refresh();
                yield return Shot("40_hud_prompt");
                ps.inventory.Add(ItemType.Bandage, 2);
                ps.inventory.Add(ItemType.Water, 3);
                ps.inventory.Add(ItemType.CannedFood, 1);
                ps.vitals.Tick(2f, true);
                gameHud.Notify("Picked up Water x2");
                gameHud.Notify("Used a bandage (+35 health)");
                yield return Wait(5);
                yield return Shot("41_hud_inventory");
                ps.vitals.TakeDamage(72f);
                yield return Wait(20);
                yield return Shot("42_hud_low_health");
                gameHud.SetMenu(true);
                yield return Wait(5);
                yield return Shot("43_hud_pause_menu");
                gameHud.SetMenu(false);
                ps.vitals.TakeDamage(100f);
                yield return Wait(5);
                yield return Shot("44_hud_death");
                ps.vitals.Reset();
                yield return Wait(5);
                gameHud.visible = false;
            }

            // 12. Gait sheet (side-on, fully lit).
            yield return GaitSheet(player);

            // Frame time over a short run with everything live.
            wanderer.enabled = true;
            player.AimOverride = new Vector2(0f, 1f);
            yield return Wait(30);
            float t0 = Time.realtimeSinceStartup;
            const int frames = 300;
            yield return Wait(frames);
            float ms = (Time.realtimeSinceStartup - t0) * 1000f / frames;
            File.WriteAllText(Path.Combine(folder, "perf.txt"),
                $"avg frame {ms:0.00} ms ({1000f / ms:0} fps) over {frames} frames at {Screen.width}x{Screen.height}\n");
            Application.Quit();
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
            for (float x = -13f; x <= 13f; x += 0.5f)
            {
                for (float z = -13f; z <= 13f; z += 0.5f)
                {
                    foreach (Vector2 d in dirs)
                    {
                        if (Mathf.Abs(d.y) < 0.5f) continue;   // side-on camera looks along X, so climb along Z
                        var a = new Vector2(x, z);
                        Vector2 b = a + d * len;
                        if (Mathf.Abs(b.y) > 13f) continue;
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
