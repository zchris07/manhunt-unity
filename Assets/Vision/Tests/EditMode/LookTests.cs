using NUnit.Framework;
using UnityEngine;
using Vision.Rendering;
using Vision.Visibility;

namespace Vision.Tests
{
    public class LookTests
    {
        [Test]
        public void Defaults_LeaveTheTunedLookUnchanged()
        {
            VisionComposite.Look l = VisionComposite.Look.Defaults;
            Assert.AreEqual(1f, l.contrast);
            Assert.AreEqual(1f, l.saturation);
            Assert.AreEqual(1f, l.litBrightness);
            Assert.AreEqual(1f, l.unlitBrightness);
            Assert.Greater(l.beamIntensity, 1f, "a stronger flashlight beam");
            Assert.IsTrue(l.cameraEffects);
            Assert.AreEqual(l, l.Clamped(), "defaults are inside every slider range");
        }

        [Test]
        public void Clamped_KeepsValuesInsideTheSliders()
        {
            var l = new VisionComposite.Look { contrast = 9f, saturation = -1f, litBrightness = 3f, unlitBrightness = -2f, beamIntensity = 0f, blurStart = 20f, blurEnd = 5f, blurMaxPixels = 50f };
            l = l.Clamped();
            Assert.AreEqual(2f, l.contrast);
            Assert.AreEqual(0f, l.saturation);
            Assert.AreEqual(2f, l.litBrightness);
            Assert.AreEqual(0f, l.unlitBrightness);
            Assert.AreEqual(0.5f, l.beamIntensity);
            Assert.Greater(l.blurEnd, l.blurStart);
            Assert.AreEqual(8f, l.blurMaxPixels);
        }

        [Test]
        public void CopyValues_ListsEverySlider()
        {
            string text = VisionComposite.Look.Defaults.ToString();
            foreach (string name in new[] { "contrast", "saturation", "lit brightness", "unlit brightness", "beam intensity", "beam falloff", "blur start", "blur end", "blur max px", "camera effects" })
                StringAssert.Contains(name, text);
        }

        [Test]
        public void CameraEffectsOff_StopsLightFlicker()
        {
            var go = new GameObject("light");
            var light = go.AddComponent<VisionLight>();
            light.intensity = 0.8f;
            light.flickerAmount = 0.25f;
            try
            {
                VisionLight.FlickerEnabled = false;
                Assert.AreEqual(0.8f * (1f - 0.125f), light.CurrentIntensity, 1e-5f, "steady at the average flickering brightness");
            }
            finally
            {
                VisionLight.FlickerEnabled = true;
                Object.DestroyImmediate(go);
            }
        }
    }
}
