using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Vision.Effects;
using Vision.Game;
using Vision.UI;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// The laser and rounds drawn over the ground by the GPU: the ground (and fallen logs) mark their pixels, everything
    /// else drawn marks its own as not the ground; the over-ground effects' material shows over the ground always and is
    /// depth-tested anywhere else; the overlay camera that draws them keeps the scene's depth; a line under a rise is slid
    /// toward the camera to just over the ground without moving on screen; the laser is one unbroken ribbon of Noita's
    /// static; rounds are thin white streaks drawn three quarters as fast as the match flies them.
    /// </summary>
    public class OverGroundTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState() => saved = MatchState.Current;

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        static float StencilOf(Renderer r) => r.sharedMaterial != null && r.sharedMaterial.HasProperty("_StencilRef") ? r.sharedMaterial.GetFloat("_StencilRef") : -1f;

        [Test]
        public void TheGroundAndFallenLogsMarkTheirPixels_AndNothingElseDoes_AndALiftedPointStaysPutOnScreen()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            var camGo = new GameObject("Lift Camera");
            try
            {
                Assert.IsTrue(mat.HasProperty("_StencilRef"), "the low-poly material writes a stencil");
                Assert.AreEqual(0f, mat.GetFloat("_StencilRef"), "and it's 0 (not the ground) unless told otherwise");
                MatchState.Current.TestingMode = true;
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();

                MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(true);
                MeshRenderer[] ground = all.Where(r => r.name == "Ground").ToArray();
                Assert.Greater(ground.Length, 0);
                foreach (MeshRenderer r in ground) Assert.AreEqual(1f, StencilOf(r), "every piece of the ground marks its pixels");
                MeshRenderer[] logs = all.Where(r => r.gameObject.layer == SandboxWorld.LowLayer && r.sharedMaterial != null && r.sharedMaterial.shader == mat.shader).ToArray();
                Assert.Greater(logs.Length, 0, "fallen logs and trees lie about");
                foreach (MeshRenderer r in logs) Assert.AreEqual(1f, StencilOf(r), $"{r.name} lies low: shots and the laser pass over it, drawn as the ground");
                int upright = 0;
                foreach (MeshRenderer r in all)
                {
                    if (r.name == "Ground" || r.gameObject.layer == SandboxWorld.LowLayer || r.sharedMaterial != mat) continue;
                    Assert.AreEqual(0f, StencilOf(r), $"{r.name} stands up: whatever's behind it is hidden by it");
                    upright++;
                }
                Assert.Greater(upright, 100, "trees, rocks, walls and props");

                // A point of a level line buried under a rise: slid toward the camera to just over the ground there, and
                // still at the same spot on screen.
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                Vector2 at = world.Layout.Spawn;
                Vector3 surface = world.transform.TransformPoint(new Vector3(at.x, world.SurfaceHeight(at), at.y));
                camGo.transform.SetPositionAndRotation(surface + new Vector3(0f, 40f, -40f), Quaternion.Euler(45f, 0f, 0f));
                var fxGo = new GameObject("Effects");
                try
                {
                    var effects = fxGo.AddComponent<MatchEffects>();
                    effects.world = world;
                    Vector3 buried = surface - Vector3.up * 2f * world.transform.lossyScale.y;
                    const float up = 1.2f;
                    Vector3 lifted = effects.Lift(cam, buried, up);
                    Vector3 l = world.transform.InverseTransformPoint(lifted);
                    Assert.GreaterOrEqual(l.y, world.SurfaceHeight(new Vector2(l.x, l.z)) + up - 0.01f, "at least the barrel's height over the ground under it");
                    Vector3 s0 = cam.WorldToScreenPoint(buried), s1 = cam.WorldToScreenPoint(lifted);
                    Assert.AreEqual(s0.x, s1.x, 0.05f, "the same spot on screen");
                    Assert.AreEqual(s0.y, s1.y, 0.05f, "the same spot on screen");
                    Assert.Less(s1.z, s0.z, "only nearer the camera");
                    Vector3 above = surface + Vector3.up * 3f * world.transform.lossyScale.y;
                    Assert.AreEqual(above, effects.Lift(cam, above, up), "a point already over the ground stays where it is");
                }
                finally { Object.DestroyImmediate(fxGo); }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void OverGroundEffects_ShowOverTheGroundAlways_AndAreDepthTestedAnywhereElse_DrawnByAnOverlayThatKeepsTheDepth()
        {
            Shader s = Shader.Find("Vision/FxOverGround");
            Assert.IsNotNull(s, "the over-ground effects' shader");
            Assert.AreEqual(2, s.passCount, "one pass over the ground, one behind whatever else is nearer");
            Material m = Vfx.MakeOverGroundMaterial(Vfx.Blend.Additive);
            try
            {
                Assert.IsNotNull(m);
                Assert.AreEqual(1, m.FindPass("OverGround") >= 0 ? 1 : 0);
                Assert.AreEqual(1, m.FindPass("BehindProps") >= 0 ? 1 : 0);
            }
            finally { Object.DestroyImmediate(m); }

            var camGo = new GameObject("Main");
            try
            {
                Camera main = camGo.AddComponent<Camera>();
                var senses = camGo.AddComponent<SensesCamera>();
                senses.Ensure();
                Assert.IsNotNull(senses.OverGround);
                Assert.AreEqual(1 << Vfx.OverGroundLayer, senses.OverGround.cullingMask, "it draws only the over-ground layer");
                Assert.AreEqual(0, main.cullingMask & (1 << Vfx.OverGroundLayer), "which the main camera skips");
                UniversalAdditionalCameraData data = senses.OverGround.GetUniversalAdditionalCameraData();
                Assert.AreEqual(CameraRenderType.Overlay, data.renderType);
                Assert.IsFalse(data.clearDepth, "the scene's depth and stencil stay for it to be judged against");
                var stack = main.GetUniversalAdditionalCameraData().cameraStack;
                Assert.Less(stack.IndexOf(senses.OverGround), stack.IndexOf(senses.Overlay), "before the senses, which clear the depth");
            }
            finally { Object.DestroyImmediate(camGo); }
        }

        [Test]
        public void TheLaserIsOneUnbrokenRibbonOfStatic_AndRoundsAreThinWhiteStreaksDrawnThreeQuartersAsFast()
        {
            var fxGo = new GameObject("Vfx");
            var camGo = new GameObject("Cam");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 10f;
                camGo.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
                var fx = fxGo.AddComponent<Vfx>();
                var pts = new System.Collections.Generic.List<Vector3>();
                for (int i = 0; i <= 20; i++) pts.Add(new Vector3(i * 0.5f, 1f, 0f));
                float w = Vfx.ScreenPixels(cam, MatchEffects.LaserPixels);
                Assert.Greater(w, 0f);
                fx.Laser(pts, w, MatchEffects.LaserRed, 0.3f);
                fx.Step(0.016f, cam);
                MeshFilter mf = fxGo.GetComponentsInChildren<MeshFilter>().Single(f => f.gameObject.layer == Vfx.OverGroundLayer);
                Mesh mesh = mf.sharedMesh;
                Assert.AreEqual(pts.Count * 2, mesh.vertexCount, "one strip through every point: nothing cut out of it");
                Assert.AreEqual((pts.Count - 1) * 6, mesh.triangles.Length);
                var uv = new System.Collections.Generic.List<Vector2>();
                var uv2 = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(0, uv);
                mesh.GetUVs(1, uv2);
                Assert.IsTrue(uv.All(u => u.x == 8f), "drawn as a laser sight (Noita's static)");
                Assert.AreEqual(10f, uv2[uv2.Count - 1].x, 1e-3f, "the distance along it, for its pixels and drifting dust");
                Assert.IsTrue(uv2.All(u => Mathf.Approximately(u.y, 0.3f)), "its own seed");
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                Assert.AreEqual("Vision/FxOverGround", mr.sharedMaterial.shader.name);
                // Facing the camera: as wide on screen whichever way it runs.
                Vector3 across = mesh.vertices[1] - mesh.vertices[0];
                Assert.AreEqual(0f, Vector3.Dot(across, cam.transform.forward), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(fxGo);
                Object.DestroyImmediate(camGo);
            }
            Assert.AreEqual(0.75f, MatchEffects.RoundPace, "rounds drawn half again as fast as before (half the match's speed before)");
            Assert.AreEqual(1f, MatchEffects.RoundPixels);
        }
    }
}
