using NUnit.Framework;
using UnityEngine;
using Vision.Player;
using Vision.Rendering;

namespace Vision.Tests
{
    public class ViewportTests
    {
        [Test]
        public void LargerScreens_SeeMore_AtTheSamePixelSize()
        {
            Assert.AreEqual(7.2f, TopDownCamera.SizeFor(7.2f, 900f, 900f), 1e-5f, "unchanged at the reference height");
            Assert.AreEqual(8.64f, TopDownCamera.SizeFor(7.2f, 1080f, 900f), 1e-4f, "1080p sees 20% more");
            Assert.AreEqual(17.28f, TopDownCamera.SizeFor(7.2f, 2160f, 900f), 1e-3f, "4K sees 2.4x more");
            foreach (float h in new[] { 720f, 1080f, 1440f, 2160f })
                Assert.AreEqual(900f / (2f * 7.2f), h / (2f * TopDownCamera.SizeFor(7.2f, h, 900f)), 1e-3f, "pixels per world unit");
        }

        [Test]
        public void Mask_CoversTheWholeView()
        {
            var go = new GameObject("cam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            try
            {
                cam.orthographicSize = 7.2f;
                cam.aspect = 16f / 9f;
                Assert.AreEqual(48f, VisionMaskRenderer.CoverSizeFor(cam, 48f), 1e-4f, "the original window keeps the original mask");
                cam.orthographicSize = 17.28f;
                float size = VisionMaskRenderer.CoverSizeFor(cam, 48f);
                Assert.Greater(size, 2f * 17.28f * 16f / 9f, "covers the full width of a 4K view");
                Assert.Greater(size, 2f * 17.28f / Mathf.Sin(60f * Mathf.Deg2Rad), "and its full depth");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FullScreen_YieldsToCommandLineScreenOptions()
        {
            Assert.IsTrue(FullScreen.ShouldApply(new[] { "VisionSandbox.exe" }));
            Assert.IsFalse(FullScreen.ShouldApply(new[] { "VisionSandbox.exe", "-screen-fullscreen", "0" }));
            Assert.IsFalse(FullScreen.ShouldApply(new[] { "VisionSandbox.exe", "-visionCapture", "Captures" }));
        }
    }
}
