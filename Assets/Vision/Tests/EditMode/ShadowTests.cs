using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Rendering;

namespace Vision.Tests
{
    public class ShadowTests
    {
        [Test]
        public void Shadow_PointsAwayFromTheLight_AndStartsPastTheFeet()
        {
            var pts = new List<Vector2>();
            var caster = new Vector2(2f, 0f);
            Assert.IsTrue(VisionMaskRenderer.ShadowPolygon(caster, Vector2.zero, 2f, 10f, 0.3f, 3.6f, 1f, pts, out Vector2 start, out float alpha, out float length));
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in pts) centroid += p;
            centroid /= pts.Count;
            Assert.Greater(centroid.x, caster.x, "the shadow lies beyond the caster, away from the light");
            Assert.Greater(start.x, caster.x, "it starts past the feet, so the caster's own body is not darkened");
            Assert.That(alpha, Is.InRange(0.2f, 0.86f), "soft and partly transparent");
            Assert.LessOrEqual(length, 3.6f * 1.2f * 1.08f + 1e-4f, "length is capped");
            foreach (Vector2 p in pts) Assert.Less(Mathf.Abs(p.y), 0.3f * 1.5f + 1e-4f);
        }

        [Test]
        public void NoShadow_OutsideTheLightsRange()
        {
            var pts = new List<Vector2>();
            Assert.IsFalse(VisionMaskRenderer.ShadowPolygon(new Vector2(12f, 0f), Vector2.zero, 2f, 10f, 0.3f, 3.6f, 1f, pts, out _, out _, out _));
            Assert.AreEqual(0, pts.Count);
        }

        [Test]
        public void LowLight_CastsLongerShadows()
        {
            var pts = new List<Vector2>();
            VisionMaskRenderer.ShadowPolygon(new Vector2(1.5f, 0f), Vector2.zero, 8f, 10f, 0.3f, 3.6f, 1f, pts, out _, out _, out float high);
            VisionMaskRenderer.ShadowPolygon(new Vector2(1.5f, 0f), Vector2.zero, 0.9f, 10f, 0.3f, 3.6f, 1f, pts, out _, out _, out float low);
            Assert.Greater(low, high);
        }

        [Test]
        public void OnlyTheFlashlight_CastsCharacterShadows()
        {
            Assert.IsTrue(VisionMaskRenderer.CastsFlashlightShadow(false, false, true, true), "a lit figure in the beam");
            Assert.IsFalse(VisionMaskRenderer.CastsFlashlightShadow(false, false, false, true), "outside the beam: no shadow, even next to a campfire");
            Assert.IsFalse(VisionMaskRenderer.CastsFlashlightShadow(false, true, true, true), "never the viewer's own body");
            Assert.IsFalse(VisionMaskRenderer.CastsFlashlightShadow(true, false, true, false), "an entity the viewer cannot see casts none");
            Assert.IsTrue(VisionMaskRenderer.CastsFlashlightShadow(true, false, true, true));
        }

        [Test]
        public void Beam_FadesSoftlyTowardItsSides()
        {
            const float half = 50f * Mathf.Deg2Rad;
            Vector2 dir = Vector2.right;
            Vector2 At(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * 5f;
            Assert.AreEqual(1f, VisionMaskRenderer.BeamFalloff(At(0f), dir, half, 0.35f), 1e-4f, "full on the axis");
            Assert.AreEqual(1f, VisionMaskRenderer.BeamFalloff(At(30f), dir, half, 0.35f), 1e-4f, "full inside the soft band");
            float mid = VisionMaskRenderer.BeamFalloff(At(41f), dir, half, 0.35f);
            Assert.That(mid, Is.InRange(0.2f, 0.8f), "fading across the band");
            Assert.AreEqual(0f, VisionMaskRenderer.BeamFalloff(At(50f), dir, half, 0.35f), 1e-4f, "dark at the cone's side");
            float prev = 1f;
            for (float a = 0f; a <= 50f; a += 1f)
            {
                float f = VisionMaskRenderer.BeamFalloff(At(a), dir, half, 0.35f);
                Assert.LessOrEqual(f, prev + 1e-5f, "monotonic");
                prev = f;
            }
            Assert.AreEqual(1f, VisionMaskRenderer.BeamFalloff(At(45f), dir, 0f, 0.35f), "no angular falloff on other polygons");
        }

        [Test]
        public void DistanceFalloff_MatchesTheMaskShader()
        {
            Assert.AreEqual(1f, VisionMaskRenderer.DistanceFalloff(2f, 10f, 0.45f));
            Assert.AreEqual(0f, VisionMaskRenderer.DistanceFalloff(10f, 10f, 0.45f), 1e-5f);
            Assert.AreEqual(1f, VisionMaskRenderer.DistanceFalloff(50f, 10f, 2f), "falloff start >= 1 disables it");
        }

        [Test]
        public void PointInPolygon_DecidesWhetherAnEntityIsSeen()
        {
            var cone = new List<Vector2> { Vector2.zero, new Vector2(5f, -2f), new Vector2(5f, 2f) };
            Assert.IsTrue(VisionMaskRenderer.Contains(cone, new Vector2(3f, 0f)));
            Assert.IsFalse(VisionMaskRenderer.Contains(cone, new Vector2(-1f, 0f)), "behind the viewer: its shadow is not drawn");
            Assert.IsFalse(VisionMaskRenderer.Contains(cone, new Vector2(3f, 2f)));
        }
    }
}
