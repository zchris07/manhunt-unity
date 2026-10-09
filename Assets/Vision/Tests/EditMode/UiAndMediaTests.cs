using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Audio;
using Vision.Game;
using Vision.Player;
using Vision.UI;
using Vision.World;

namespace Vision.Tests
{
    public class UiAndMediaTests
    {
        GameObject root, hudGo;
        Material mat;
        MatchState saved;

        [SetUp]
        public void SetUp()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
            root = new GameObject("World");
            mat = new Material(Shader.Find("Vision/LowPoly"));
            hudGo = new GameObject("HUD");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mat);
            var audio = Object.FindAnyObjectByType<AudioManager>();
            if (audio != null) Object.DestroyImmediate(audio.gameObject);
            MatchState.Current = saved;
        }

        SandboxWorld NewWorld()
        {
            var world = root.AddComponent<SandboxWorld>();
            world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
            world.Generate();
            return world;
        }

        [Test]
        public void TheOriginalsMedia_AreInTheProject()
        {
            foreach (string id in new[] { "burst", "sexton.reel", "boom", "penjamin", "repulsor" })
                Assert.NotNull(AudioManager.Instance.Clip(id), $"sound {id}");
            foreach (string pic in new[] { "book-1", "book-2", "book-3", "book-4", "note-1", "note-2", "note-3", "note-4", "scare", "waz-slain" })
            {
                Texture2D t = ScreenOverlays.Picture(pic);
                Assert.NotNull(t, $"picture {pic}");
                Assert.Greater(t.width, 300, $"{pic} keeps its size");
            }
            var kit = new UiKit();
            Assert.AreEqual("Oswald", kit.Display.fontNames.FirstOrDefault()?.Split(' ')[0], "Oswald for headings");
            StringAssert.Contains("Special Elite", string.Join(",", kit.Type.fontNames));
            StringAssert.Contains("IBM Plex Mono", string.Join(",", kit.Mono.fontNames));
        }

        [Test]
        public void Names_FollowTheOriginalsRule()
        {
            Assert.AreEqual("Zach", GameHud.SanitizeName("  Zach  "));
            Assert.AreEqual("Mr Beast", GameHud.SanitizeName("Mr   Beast"));
            Assert.AreEqual("abc", GameHud.SanitizeName("a<b>c"));
            Assert.AreEqual(16, GameHud.SanitizeName(new string('x', 30)).Length);
            Assert.AreEqual("O'Neil-2", GameHud.SanitizeName("O'Neil-2!"));
        }

        [Test]
        public void Volumes_SaveAndClamp()
        {
            AudioManager a = AudioManager.Instance;
            AudioManager.Volumes before = AudioManager.Load();
            try
            {
                a.SetVolumes(new AudioManager.Volumes { Master = 0.5f, Sfx = 2f, Ambience = 0.25f });
                AudioManager.Volumes v = AudioManager.Load();
                Assert.AreEqual(0.5f, v.Master, 1e-4f);
                Assert.AreEqual(1f, v.Sfx, 1e-4f, "clamped");
                Assert.AreEqual(0.25f, v.Ambience, 1e-4f);
                Assert.AreEqual(1f, AudioManager.Falloff(50f, 60f, 950f, 3f), 1e-5f, "full within near");
                Assert.AreEqual(0f, AudioManager.Falloff(1000f, 60f, 950f, 3f), 1e-5f, "silent past the radius");
                Assert.Less(AudioManager.Falloff(500f, 60f, 950f, 3f), AudioManager.Falloff(500f, 60f, 950f, 1f), "a steep curve is quieter farther out");
            }
            finally { a.SetVolumes(before); }
        }

        [Test]
        public void TestEffects_PlayOnYourself_AsTheOriginal()
        {
            var rig = new SimRig(1, 1, testMode: true);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            rig.Sim.PlayTestFx(1, TestFx.Stun);
            Assert.Greater(zach.StunT, 0f, "Zach stunned (bottle)");
            Assert.IsTrue(rig.Sim.Events.Any(e => e.Kind == EventKind.Stun && e.A == 1));
            zach.ImmuneT = 5f;
            rig.Sim.PlayTestFx(1, TestFx.Down);
            Assert.Greater(zach.KnockT, 0f, "knocked down even while immune: testing clears it");
            rig.Sim.Events.Clear();
            rig.Sim.PlayTestFx(2, TestFx.Book);
            Assert.IsTrue(rig.Sim.Events.Any(e => e.Kind == EventKind.Book && e.To != null && e.To.Contains(2)), "the book picture, for you");
            Assert.IsTrue(rig.Sim.Events.Any(e => e.Kind == EventKind.Boom), "and the boom");
            rig.Sim.PlayTestFx(2, TestFx.Scare);
            Assert.Greater(s.ScareT, 0f);
            rig.Sim.PlayTestFx(2, TestFx.Blast);
            Assert.Greater(s.Move.KbT, 0f, "knocked back");
            int gases = rig.Sim.Gases.Count;
            rig.Sim.PlayTestFx(2, TestFx.Gas);
            Assert.AreEqual(gases + 1, rig.Sim.Gases.Count);
            rig.Sim.PlayTestFx(2, TestFx.Vape);
            rig.Run(30);
            Assert.Greater(s.VapeT, 0f, "caught in Penjamin's gas");
            var normal = new SimRig(1, 1);
            normal.Sim.PlayTestFx(1, TestFx.Stun);
            Assert.AreEqual(0f, normal.P(1).StunT, "only in testing mode");
        }

        [Test]
        public void Overlays_ShowTheScare_TheFlashes_AndNotes()
        {
            var o = hudGo.AddComponent<ScreenOverlays>();
            o.JumpScare(2.5f, 0.6f);
            Assert.IsTrue(o.ScareShowing);
            o.FlashImage(ScreenOverlays.Picture("book-1"), 0.8f, 0.24f, true);
            Assert.IsTrue(o.FlashShowing);
            o.ShowNote(2);
            Assert.IsTrue(o.NoteShowing);
            Assert.AreEqual(2, o.NoteIndex);
            o.HideNote();
            Assert.IsFalse(o.NoteShowing);
            o.Big("Jarvis online");
            Assert.AreEqual("JARVIS ONLINE", o.BigShowing);
            o.Center("STUNNED", 1.5f);
            Assert.AreEqual("STUNNED", o.CenterShowing);
            o.ClearAll();
            Assert.IsFalse(o.ScareShowing || o.FlashShowing || o.NoteShowing);
        }

        [Test]
        public void OtherPlayers_AreDrawn_LyingDownWhenDowned_GoneOnceOut()
        {
            SandboxWorld world = NewWorld();
            MatchState.Current.TestingMode = true;
            MatchHost host = MatchHost.For(world);
            host.Begin();
            var puppets = world.GetComponent<PlayerPuppets>();
            Assert.NotNull(puppets, "the match draws the other players");
            SimPlayer d = host.SpawnDummy(Role.Survivor);
            SimPlayer z = host.SpawnDummy(Role.Hunter);
            puppets.Sync(0.1f);
            Assert.AreEqual(2, puppets.All.Count, "a figure each, none for yourself");
            PlayerPuppets.Puppet pd = puppets.For(d.Id), pz = puppets.For(z.Id);
            Assert.Greater(pz.Go.transform.localScale.x, pd.Go.transform.localScale.x, "Zach is bigger");
            Vector3 l = pd.Go.transform.localPosition;
            Assert.Less(Vector2.Distance(new Vector2(l.x, l.z), d.Pos), 0.01f, "where the match has it");
            d.Hp = 0f;
            d.Health = Health.Downed;
            puppets.Sync(0.1f);
            Assert.IsTrue(pd.Animator.Prone, "lying down");
            z.StunT = 2f;
            puppets.Sync(0.1f);
            Assert.IsTrue(pz.Stars.Showing, "stun stars");
            d.Health = Health.Escaped;
            puppets.Sync(0.1f);
            Assert.IsFalse(pd.Visible, "gone once out");
            host.Sim.ClearDummies();
            puppets.Sync(0.1f);
            Assert.AreEqual(0, puppets.All.Count);
        }

        [Test]
        public void TheLevel_HasSpreadOutStakes_AndTheMatchUsesSurvivorsPlusFour()
        {
            SandboxWorld world = NewWorld();
            Assert.That(world.Stakes.Count, Is.InRange(6, SandboxWorld.MaxStakes), "scarecrow stakes in the clearings");
            for (int i = 0; i < world.Stakes.Count; i++)
            {
                Vector3 a = world.transform.InverseTransformPoint(world.Stakes[i].position);
                Assert.Greater(Vector2.Distance(new Vector2(a.x, a.z), world.Layout.Spawn), 14.9f, "none by the spawn");
                for (int j = i + 1; j < world.Stakes.Count; j++)
                {
                    Vector3 b = world.transform.InverseTransformPoint(world.Stakes[j].position);
                    float d = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    Assert.Greater(d, 4f, "never on top of each other");
                    if (j < MatchRules.StakeCount(4)) Assert.Greater(d, 15f, "the ones a match uses are spread across the map");
                }
            }
            MatchHost host = MatchHost.For(world);
            host.Begin();
            Assert.AreEqual(Mathf.Min(world.Stakes.Count, MatchRules.StakeCount(4)), host.Sim.Map.Stakes.Count);
            int shown = world.Stakes.Count(s => s.gameObject.activeSelf);
            Assert.AreEqual(host.Sim.Map.Stakes.Count, shown, "the spare stakes are taken away");
        }

        [Test]
        public void TheResults_ShowTheWinner_TheTime_AndEveryonesNumbers()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            var r = new MatchResult { Winner = Winner.Hunters, Reason = "0 escaped, 1 eliminated", DurationSec = 754, GeneratorsRepaired = 2, GeneratorsRequired = 5 };
            r.Stats.Add((1, "Zach", Role.Hunter, new MatchStats { Outcome = "hunter", Hits = 7, Downs = 3, Stakes = 2 }));
            r.Stats.Add((2, "Ana", Role.Survivor, new MatchStats { Outcome = "eliminated", RepairSec = 61.4f, Heals = 1, Revives = 1, TimeAlive = 640f }));
            hud.ShowResults(r);
            Assert.IsTrue(hud.ResultsShowing);
            var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text).ToList();
            Assert.Contains("ZACH WINS", texts);
            Assert.IsTrue(texts.Any(t => t.Contains("12:34") && t.Contains("generators 2/5")), "the time and the generators");
            Assert.Contains("Sacrificed", texts);
            Assert.Contains("61", texts, "repair seconds");
            Assert.Contains("10m", texts, "time alive");
            Assert.Contains("7", texts, "Zach's hits");
        }

        [Test]
        public void MatchEvents_BecomePicturesAndSounds()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.StartTesting(false);
            MatchHost host = MatchHost.For(world);
            Assert.NotNull(host.Sim);
            hud.Presenter.world = world;
            hud.Refresh();
            int played = AudioManager.Instance.Played;
            hud.Presenter.OnEvent(new GameEvent { Kind = EventKind.Scare });
            Assert.IsTrue(hud.Overlays.ScareShowing, "the scare picture");
            Assert.AreEqual("burst", AudioManager.Instance.LastPlayed, "with a slice of the song");
            hud.Presenter.OnEvent(new GameEvent { Kind = EventKind.Book, A = 3 });
            Assert.IsTrue(hud.Overlays.FlashShowing);
            hud.Presenter.OnEvent(new GameEvent { Kind = EventKind.WazSlain });
            Assert.AreEqual("boom", AudioManager.Instance.LastPlayed);
            hud.Presenter.OnEvent(new GameEvent { Kind = EventKind.Note, A = 1 });
            Assert.AreEqual(1, hud.Overlays.NoteIndex);
            hud.Presenter.OnEvent(new GameEvent { Kind = EventKind.Stun, A = host.LocalId, Text = "bottle" });
            Assert.AreEqual("STUNNED", hud.Overlays.CenterShowing);
            Assert.Greater(AudioManager.Instance.Played, played);
            var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text).ToList();
            Assert.Contains("TEST EFFECTS", texts, "the testing panel");
            foreach (var (_, label) in MatchSim.TestFxButtons) Assert.Contains(label.ToUpperInvariant(), texts);
        }
    }
}
