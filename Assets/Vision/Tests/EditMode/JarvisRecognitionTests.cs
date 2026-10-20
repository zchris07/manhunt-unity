using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.UI;

namespace Vision.Tests
{
    /// <summary>
    /// JARVIS's recognition boxes (everyone and everything on the screen picked out, named, read out, locking on), and the
    /// whole map shown only to the survivor who set JARVIS going.
    /// </summary>
    public class JarvisRecognitionTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
        }

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        [Test]
        public void JarvisPicksOut_PlayersNpcsWeaponsItemsTrapsAndGenerators_NeverAGhost_OnlyNearby()
        {
            var rig = new SimRig(3);
            SimPlayer zach = rig.P(1), me = rig.P(2), other = rig.P(3), ghost = rig.P(4);
            rig.Place(me, 0f, 0f);
            rig.Place(zach, 200f, 0f);
            rig.Place(other, -200f, 0f);
            rig.Place(ghost, 0f, 200f);
            ghost.Ghost = true;
            var npc = new Marc(rig.Sim) { Pos = SimRig.U(0f, -200f) };
            rig.Sim.Npcs.Add(npc);
            rig.Sim.Drops.Add(new DropItem { Id = 900, Pos = SimRig.U(100f, 100f), Item = ItemType.Pistol, Amount = 1f });
            rig.Sim.Drops.Add(new DropItem { Id = 901, Pos = SimRig.U(-100f, 100f), Item = ItemType.MiniShield, Amount = 1f });
            rig.Sim.Traps.Add(new Trap { Id = 902, Pos = SimRig.U(100f, -100f), Owner = me.Id });
            // Far off: not picked out.
            rig.Sim.Drops.Add(new DropItem { Id = 903, Pos = SimRig.U(5000f, 0f), Item = ItemType.Bottle, Amount = 1f });
            var scans = MatchPresenter.JarvisScans(rig.Sim, null, me);
            JarvisHud.Scan Of(string name) => scans.Single(s => s.Name == name);
            Assert.AreEqual(JarvisHud.ScanKind.Threat, Of("ZACH").Kind, "Zach, the threat");
            StringAssert.Contains("THREAT", Of("ZACH").Detail);
            Assert.AreEqual(JarvisHud.ScanKind.Person, Of(other.Name.ToUpperInvariant()).Kind);
            StringAssert.Contains("MATCH", Of(other.Name.ToUpperInvariant()).Detail, "a match percentage, as face recognition gives");
            Assert.AreEqual(JarvisHud.ScanKind.Person, Of(npc.Name.ToUpperInvariant()).Kind, "NPCs by name");
            Assert.AreEqual(JarvisHud.ScanKind.Weapon, Of("P250").Kind, "a weapon");
            Assert.AreEqual(JarvisHud.ScanKind.Item, Of(Items.Name(ItemType.MiniShield, false).ToUpperInvariant()).Kind, "an item");
            Assert.AreEqual(JarvisHud.ScanKind.Weapon, Of("GALAXY GAS TRAP").Kind);
            Assert.IsTrue(scans.Any(s => s.Kind == JarvisHud.ScanKind.Generator && s.Name == "GENERATOR" && s.Detail.Contains("%")), "the generators, and how far each has got");
            Assert.IsFalse(scans.Any(s => s.Name == ghost.Name.ToUpperInvariant()), "never a ghost");
            Assert.IsFalse(scans.Any(s => s.Name == me.Name.ToUpperInvariant()), "never yourself");
            Assert.IsFalse(scans.Any(s => s.Key == 4_000_000 + 903), "only nearby");
            foreach (JarvisHud.Scan s in scans) Assert.IsTrue(s.Height > 0.05f && s.Radius > 0.02f, "a shape to box");
        }

        [Test]
        public void TheBoxes_HugTheBody_AsItLooksUnderTheTiltedCamera()
        {
            var camGo = new GameObject("Camera");
            var space = new GameObject("Level");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.SetPositionAndRotation(new Vector3(0f, 20f, -14f), Quaternion.Euler(55f, 0f, 0f));
                float ppm = cam.pixelHeight / (2f * cam.orthographicSize);
                var body = new JarvisHud.Scan { Base = Vector3.zero, Radius = 0.25f, Height = 1.8f };
                Assert.IsTrue(JarvisHud.ScreenRect(body, cam, space.transform, out Rect r));
                Assert.AreEqual(0.5f * ppm, r.width, 0.5f * ppm * 0.05f, "as wide as the body itself");
                // Seen from 55 degrees up: its height foreshortened, its foot and top as ellipses.
                float tall = 1.8f * Mathf.Cos(55f * Mathf.Deg2Rad) + 0.5f * Mathf.Sin(55f * Mathf.Deg2Rad);
                Assert.AreEqual(tall * ppm, r.height, tall * ppm * 0.05f, "as tall as it looks");
                // A long thing lying across: its box fits its own outline, not a square round its length.
                var stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    stick.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.Euler(0f, 0f, 0f));
                    stick.transform.localScale = new Vector3(1.2f, 0.1f, 0.1f);
                    var item = new JarvisHud.Scan { Base = Vector3.zero, Radius = 0.6f, Height = 0.1f, Model = stick };
                    Assert.IsTrue(JarvisHud.ScreenRect(item, cam, space.transform, out Rect ir));
                    Assert.AreEqual(1.2f * ppm, ir.width, 1.2f * ppm * 0.05f, "as long as it is");
                    Assert.Less(ir.height, 0.3f * ppm, "and only as deep as it is, not a square");
                }
                finally { Object.DestroyImmediate(stick); }
                // A body's figure: the scan's own height (Zach's taller than a survivor's).
                var rig = new SimRig(1);
                var scans = MatchPresenter.JarvisScans(rig.Sim, null, rig.P(2));
                rig.Place(rig.P(1), 100f, 0f);
                scans = MatchPresenter.JarvisScans(rig.Sim, null, rig.P(2));
                Assert.AreEqual(Vision.Characters.CharacterSpec.Zach().Height, scans.Single(s => s.Name == "ZACH").Height, 1e-4f);
                Assert.Less(scans.Single(s => s.Name == "ZACH").Radius, 0.35f, "the shoulders, not a crate round him");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(space);
            }
        }

        [Test]
        public void TheBoxes_LockOnRoundWhatsOnTheScreen_NamedWithBracketsAtTheirCorners()
        {
            var go = new GameObject("Presenter");
            var camGo = new GameObject("Camera");
            var space = new GameObject("Level");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f));
                var hud = go.AddComponent<JarvisHud>();
                hud.Run(10f, 20f, false, true);
                var scans = new System.Collections.Generic.List<JarvisHud.Scan>
                {
                    new JarvisHud.Scan { Key = 1, Name = "ZACH", Detail = "!! THREAT", Kind = JarvisHud.ScanKind.Threat, Base = Vector3.zero, Radius = 0.3f, Height = 2.2f, Distance = 3f },
                    new JarvisHud.Scan { Key = 2, Name = "P250", Detail = "SCAN // WEAPON", Kind = JarvisHud.ScanKind.Weapon, Base = new Vector3(2.2f, 0f, 2.2f), Radius = 0.16f, Height = 0.12f, Distance = 4f },
                    // Behind the camera: nothing to box.
                    new JarvisHud.Scan { Key = 3, Name = "BEHIND", Detail = "", Kind = JarvisHud.ScanKind.Item, Base = new Vector3(0f, 30f, 0f), Radius = 0.2f, Height = 0.4f, Distance = 1f },
                };
                hud.Recognise(scans, cam, space.transform);
                CollectionAssert.AreEquivalent(new[] { "ZACH", "P250" }, hud.LockedNames.ToList(), "what's on the screen, boxed and named");
                var box = go.GetComponentsInChildren<RectTransform>(true).Where(r => r.name == "Lock" && r.gameObject.activeSelf).ToList();
                Assert.AreEqual(2, box.Count);
                foreach (RectTransform b in box)
                {
                    Assert.AreEqual(8, b.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "Bracket"), "an L at each corner");
                    Assert.IsNotNull(b.Find("Plate"), "a name plate");
                }
                Assert.AreEqual(new Color(1f, 0.22f, 0.18f, 1f), JarvisHud.KindColor(JarvisHud.ScanKind.Threat), "Zach in red");
                // Gone from the screen (or JARVIS over): the boxes go.
                hud.Recognise(null, cam, space.transform);
                Assert.AreEqual(0, hud.LockCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(space);
            }
        }
    }
}
