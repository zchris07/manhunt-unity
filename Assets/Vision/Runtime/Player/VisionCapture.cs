using System;
using System.Collections;
using System.IO;
using UnityEngine;
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

        IEnumerator Stage(PlayerController player, Vector3 position, Vector2 aim, Wanderer wanderer, Vector3 wandererPos)
        {
            player.Teleport(position);
            player.AimOverride = aim;
            wanderer.transform.position = wandererPos;
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
