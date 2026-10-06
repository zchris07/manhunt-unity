using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// Frame-time check under play. Launch a player build with <c>-visionPerf &lt;report file&gt;</c>: it starts
    /// testing mode, walks and sprints the player from spawn into the building while sweeping the flashlight,
    /// records every frame and (in a development build) the costliest profiler markers, writes the report and quits.
    /// </summary>
    public sealed class VisionPerf : MonoBehaviour
    {
        public SandboxWorld world;
        public GameHud gameHud;

        string report;

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-visionPerf") >= 0;

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-visionPerf") report = args[i + 1];
            if (report != null) StartCoroutine(Run());
        }

        static readonly string[] EngineMarkers =
        {
            "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "FixedUpdate.PhysicsFixedUpdate", "PostLateUpdate.FinishFrameRendering", "Gfx.WaitForPresentOnGfxThread",
            "Gfx.WaitForGfxCommandsFromMainThread", "Canvas.SendWillRenderCanvases", "PostLateUpdate.PlayerUpdateCanvases",
            "GC.Collect", "GC.Alloc", "Physics.Simulate", "Camera.Render", "Inl_UniversalRenderPipeline.RenderSingleCameraInternal",
            "RenderLoop.ScheduleDraw", "PostLateUpdate.UpdateAllRenderers", "Animators.Update",
        };

        IEnumerator Run()
        {
            yield return null;
            while (world.Player == null) yield return null;
            gameHud.StartTesting(false);
            yield return new WaitForSeconds(2f);
            PlayerController player = world.Player;
            MapLayout L = world.Layout;

            // A route: spawn, along to a building entrance, in, across the building and back out.
            var route = new List<Vector2> { L.Spawn };
            Vector2 door = L.BuildingEntrances.Count > 1 ? L.BuildingEntrances[1] : L.Building.center;
            route.Add(door);
            route.Add(L.Building.center);
            route.Add(L.Building.center + new Vector2(8f, 8f));
            route.Add(L.Building.center + new Vector2(-8f, 4f));

            // Every available marker in the Scripts category, plus the engine's main ones.
            var recorders = new List<(string name, ProfilerRecorder rec, bool count)>();
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var names = new HashSet<string>(EngineMarkers);
            foreach (ProfilerRecorderHandle h in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(h);
                if (d.Category == ProfilerCategory.Scripts || d.Name.StartsWith("Vision", StringComparison.Ordinal)) names.Add(d.Name);
            }
            foreach (string n in names)
            {
                var r = new ProfilerRecorder(n, 1, ProfilerRecorderOptions.Default);
                r.Start();
                recorders.Add((n, r, n == "GC.Alloc"));
            }
            var totals = new double[recorders.Count];
            var peaks = new double[recorders.Count];

            var frames = new List<float>(4000);
            var slow = new List<string>();
            int gc0 = GC.CollectionCount(0);
            float t0 = Time.realtimeSinceStartup;
            int leg = 0, stuck = 0;
            Vector3 lastPos = player.transform.position;
            float duration = 30f;
            while (Time.realtimeSinceStartup - t0 < duration)
            {
                Vector3 lp = world.transform.InverseTransformPoint(player.transform.position);
                Vector2 me = new Vector2(lp.x, lp.z);
                Vector2 target = route[Mathf.Min(leg + 1, route.Count - 1)];
                if ((target - me).magnitude < 1.5f || stuck > 90)
                {
                    if (stuck > 90)   // walls in the way: hop to the target, as a player who knows the way
                        player.Teleport(world.transform.TransformPoint(new Vector3(target.x, 0f, target.y)));
                    stuck = 0;
                    leg = (leg + 1) % (route.Count - 1);
                }
                Vector2 dir = (target - me).normalized;
                player.MoveOverride = dir;
                player.SprintOverride = (Time.frameCount / 120) % 2 == 0;
                float sweep = Mathf.Sin(Time.realtimeSinceStartup * 1.3f) * 0.9f;
                player.AimOverride = new Vector2(dir.x * Mathf.Cos(sweep) - dir.y * Mathf.Sin(sweep), dir.x * Mathf.Sin(sweep) + dir.y * Mathf.Cos(sweep));
                if ((player.transform.position - lastPos).sqrMagnitude < 1e-4f) stuck++;
                else stuck = 0;
                lastPos = player.transform.position;

                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                if (Time.unscaledDeltaTime > 0.03f && slow.Count < 25)
                {
                    Vector3 at = world.transform.InverseTransformPoint(player.transform.position);
                    var mr = FindAnyObjectByType<Vision.Rendering.VisionMaskRenderer>();
                    slow.Add($"{Time.unscaledDeltaTime * 1000f:0} ms at ({at.x:0.0}, {at.z:0.0}) frame {frames.Count}, rays {(mr != null ? mr.LastRayCount : 0)}, queried {Vision.Visibility.VisibilityComputer.LastQueried} kept {Vision.Visibility.VisibilityComputer.LastKept}, cover {(mr != null ? mr.CoverSize : 0):0}");
                }
                for (int i = 0; i < recorders.Count; i++)
                {
                    if (!recorders[i].rec.Valid) continue;
                    double v = recorders[i].rec.LastValue;
                    totals[i] += v;
                    if (v > peaks[i]) peaks[i] = v;
                }
            }
            player.MoveOverride = null;
            player.SprintOverride = null;
            player.AimOverride = null;

            var sorted = new List<float>(frames);
            sorted.Sort();
            float avg = 0f;
            foreach (float f in frames) avg += f;
            avg /= Mathf.Max(1, frames.Count);
            float P(float q) => sorted[Mathf.Clamp(Mathf.FloorToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            int over33 = 0, over16 = 0;
            foreach (float f in frames) { if (f > 33.3f) over33++; if (f > 16.7f) over16++; }
            var sb = new StringBuilder();
            sb.AppendLine($"{frames.Count} frames at {Screen.width}x{Screen.height} {(Screen.fullScreen ? "fullscreen" : "windowed")}, {SystemInfo.graphicsDeviceName}, dev build {Debug.isDebugBuild}");
            sb.AppendLine($"avg {avg:0.00} ms ({1000f / avg:0} fps), median {P(0.5f):0.00}, p95 {P(0.95f):0.00}, p99 {P(0.99f):0.00}, max {sorted[sorted.Count - 1]:0.00} ms; frames over 16.7 ms: {over16}, over 33 ms: {over33}; gen0 GCs {GC.CollectionCount(0) - gc0}");
            {
                var occ = Vision.Visibility.VisionWorld.Occluders;
                float[] pk = occ.Packed;
                int near = 0;
                for (int i = 0; i < occ.ActiveSegmentCount; i++) if (Mathf.Abs(pk[i * 4]) < 3f && Mathf.Abs(pk[i * 4 + 1]) < 3f) near++;
                sb.AppendLine($"segments within 3 units of the origin (spawned props left behind): {near}");
            }
            sb.AppendLine($"occluder version changes {Vision.Rendering.VisionMaskRenderer.VersionChanges}, light polygon rebuilds {Vision.Rendering.VisionMaskRenderer.LightRebuilds}, active segments {Vision.Visibility.VisionWorld.Occluders.ActiveSegmentCount}");
            sb.AppendLine("marker: avg per frame (ms), peak (ms)   [GC.Alloc: bytes]");
            var order = new List<int>();
            for (int i = 0; i < recorders.Count; i++) order.Add(i);
            order.Sort((a, b) => totals[b].CompareTo(totals[a]));
            foreach (int i in order)
            {
                if (totals[i] <= 0) continue;
                if (recorders[i].count) sb.AppendLine($"  {recorders[i].name}: {totals[i] / frames.Count:0} bytes/frame, peak {peaks[i]:0}");
                else if (totals[i] / frames.Count > 5000) sb.AppendLine($"  {recorders[i].name}: {totals[i] / frames.Count / 1e6:0.000} ms, peak {peaks[i] / 1e6:0.00} ms");
            }
            // The worst frames, in order, with where the player was.
            sb.AppendLine("frame times (ms), every 30th frame:");
            for (int i = 0; i < frames.Count; i += 30) sb.Append($"{frames[i]:0.0} ");
            sb.AppendLine();
            sb.AppendLine("slow frames:");
            foreach (string line in slow) sb.AppendLine("  " + line);
            foreach (var r in recorders) r.rec.Dispose();
            File.WriteAllText(report, sb.ToString());

            // The maps as a player sees them after the walk (this run starts from the title screen, as play does).
            string shots = Path.GetDirectoryName(report);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, "perf_minimap.png"));
            yield return new WaitForSeconds(0.3f);
            gameHud.Map.SetOpen(true);
            yield return new WaitForSeconds(0.6f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, "perf_fullmap.png"));
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
        }
    }
}
