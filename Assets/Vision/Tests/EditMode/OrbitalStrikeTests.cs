using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Net;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// The K-pop orbital strike: Daphne's single-use tablet; locked on any playable character, a 3 s countdown, then a pink
    /// beam on them for exactly its track's length, following with a lag they can't outrun, half the Hemp Beam's tick, a 20%
    /// slow and every ability silenced, never touching an NPC; sent over the wire; and its tablet (explored ground only).
    /// </summary>
    public class OrbitalStrikeTests
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

        static int Secs(float s) => SimRig.Secs(s);

        [Test]
        public void TheNumbers_AreTheSpecs()
        {
            Assert.AreEqual(3f, Balance.Items.Orbital.Countdown, 1e-6f, "about 3 s");
            Assert.AreEqual(8.725f, Balance.Items.Orbital.Duration, 1e-3f, "exactly the track's length");
            Assert.AreEqual(Balance.Hunter.Beam.TickDamage * 0.5f, Balance.Items.Orbital.TickDamage, 1e-7f, "half the Hemp Beam's tick");
            Assert.AreEqual(Balance.Hunter.Beam.TickRate, Balance.Items.Orbital.TickRate, 1e-6f);
            float total = Balance.Items.Orbital.TickDamage * Balance.Items.Orbital.TickRate * Balance.Items.Orbital.Duration * 100f;
            Assert.That(total, Is.InRange(40f, 46f), "about 45 HP over the track");
            Assert.AreEqual(Balance.HunterWidth * 3f, Balance.Items.Orbital.Radius * 2f, 1e-4f, "three times Zach's width across");
            Assert.AreEqual(0.8f, Balance.Items.Orbital.SlowMul, 1e-6f, "20% slower");
            Assert.AreEqual(3000f, Balance.Items.Orbital.HearRadius, 1e-6f);
            var cue = Audio.SoundBank.Cues["orbital_strike"];
            CollectionAssert.AreEqual(new[] { "local_kpop_strike" }, cue.Prefer, "the user's own track, local only");
            Assert.AreEqual(1.5f, cue.TailFade, 1e-6f, "its last 1.5 s falling away by an inverse square");
            Assert.AreEqual(3000f, cue.Radius, 1e-6f, "heard 3000 units off");
            var laser = Audio.SoundBank.Cues["orbital_laser"];
            CollectionAssert.AreEqual(new[] { "local_orbital_laser" }, laser.Prefer, "the laser under the music, local only");
            Assert.Less(laser.Volume, cue.Volume, "quieter than the music");
            Assert.AreEqual(120f, Balance.Items.Orbital.Cooldown, 1e-6f, "two minutes to recharge");
            Assert.AreEqual(1.3f, GameHud.TabletNoise, 1e-6f, "the title screen's noise, 30% stronger, on the tablet");
            Assert.Less(GameHud.ConfirmTime + GameHud.LowerTime, Balance.Items.Orbital.Countdown, "the tablet's down before the beam lands");
            Assert.AreEqual("K.P.O.S.", Items.Name(ItemType.OrbitalStrike, false));
            Assert.IsNotNull(UI.ItemIcons.For(ItemType.OrbitalStrike, false), "its icon");
            Assert.AreEqual(Characters.PropKind.RuggedTablet, Characters.PropModels.ForItem(ItemType.OrbitalStrike, false), "held as a rugged tablet");
            Assert.IsNotNull(Resources.Load<Texture2D>("Overlays/strike_tablet"), "the tablet as it's held up");
        }

        [Test]
        public void TheTrack_WhenItsThere_LastsExactlyAsLongAsTheStrike()
        {
            // (A local file the user supplies, kept out of git: checked only where it's present.)
            AudioClip clip = Audio.SoundBank.Load("local_kpop_strike");
            if (clip == null) Assert.Ignore("the track isn't on this machine");
            Assert.AreEqual(Balance.Items.Orbital.Duration, clip.length, 0.06f, "the strike lasts exactly as long as the track");
        }

        [Test]
        public void ConfirmedOnATarget_ItsUsedUp_CountsDown_ThenFollowsAndBurnsThemForTheTracksLength()
        {
            var rig = new SimRig(2);
            SimPlayer zach = rig.P(1), me = rig.P(2), mate = rig.P(3);
            rig.Place(me, -1500f, -1500f);
            rig.Place(mate, 1500f, -1500f);
            rig.Place(zach, 0f, 0f);
            var marc = new Marc(rig.Sim) { Pos = zach.Pos + SimRig.U(15f, 0f) };
            rig.Sim.Npcs.Add(marc);
            rig.Run(1);
            Assert.IsFalse(rig.Sim.CallStrike(me.Id, zach.Id), "nothing to call it with");
            rig.Sim.AddItem(me, ItemType.OrbitalStrike);
            Assert.IsFalse(rig.Sim.CallStrike(me.Id, me.Id + 1000), "nobody there");
            Assert.IsTrue(rig.Sim.CallStrike(me.Id, zach.Id));
            Assert.GreaterOrEqual(me.Inv.FirstSlotOf(ItemType.OrbitalStrike), 0, "the tablet stays in their inventory");
            Assert.AreEqual(120f, me.StrikeCd, 1e-4f, "recharging for two minutes");
            Assert.IsFalse(rig.Sim.CallStrike(me.Id, mate.Id), "not again while it recharges");
            Assert.AreEqual(1, rig.Sim.Strikes.Count);
            float hp0 = zach.Hp;
            rig.Run(Secs(Balance.Items.Orbital.Countdown - 0.2f));
            Assert.IsFalse(rig.Sim.Strikes[0].Firing, "counting down");
            Assert.AreEqual(hp0, zach.Hp, 1e-6f, "nothing yet");
            rig.Run(Secs(0.4f));
            Assert.IsTrue(rig.Sim.Strikes[0].Firing, "down it comes");
            rig.Run(2);
            Assert.Greater(zach.SilencedT, 0f, "silenced");
            Assert.IsFalse(MatchSim.AbilitiesOn(zach), "Zach's abilities too");
            Assert.AreEqual(Balance.Items.Orbital.SlowMul, zach.Move.SlowMul, 1e-4f, "slowed 20%");
            rig.Run(Secs(Balance.Items.Orbital.Duration));
            Assert.AreEqual(0, rig.Sim.Strikes.Count, "over with its track");
            float lost = (hp0 - zach.Hp) * 100f;
            Assert.That(lost, Is.InRange(40f, 46f), "about 44 HP");
            Assert.AreEqual(0f, marc.HurtT, "never harms an NPC");
            Assert.IsTrue(marc.Alive);
            // Recharged after two minutes: ready again.
            rig.Run(Secs(Balance.Items.Orbital.Cooldown - Balance.Items.Orbital.Countdown - Balance.Items.Orbital.Duration + 0.5f));
            Assert.AreEqual(0f, me.StrikeCd, 1e-6f);
            Assert.IsTrue(rig.Sim.CallStrike(me.Id, zach.Id), "recharged");
        }

        [Test]
        public void ItDestroysEverythingDestructibleItTouches_AtOnce_AndTheTabletDropsWhereItsHolderIsSlain()
        {
            var rig = new SimRig(2, 1, false, 1, m =>
            {
                m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(-40f, 30f), B = SimRig.U(40f, 30f) });
                m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(-40f, -30f), B = SimRig.U(40f, -30f), Gate = true });
                m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(-40f, 10f), B = SimRig.U(40f, 10f), Shutter = true });
                m.Windows.Add(new SimMap.WindowDef { A = SimRig.U(30f, -40f), B = SimRig.U(30f, 40f) });
                m.Barricades.Add(new SimMap.BarricadeDef { A = SimRig.U(-30f, -40f), B = SimRig.U(-30f, 40f), Pos = SimRig.U(-30f, 0f) });
                m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(1500f, -40f), B = SimRig.U(1500f, 40f) });
            });
            SimPlayer zach = rig.P(1), me = rig.P(2), mate = rig.P(3);
            rig.Place(zach, 0f, 0f);
            rig.Place(me, -1500f, -1500f);
            rig.Place(mate, 1500f, -1500f);
            rig.Run(1);
            rig.Sim.DoorLocked[1] = false;
            rig.Sim.AddItem(me, ItemType.OrbitalStrike);
            Assert.IsTrue(rig.Sim.CallStrike(me.Id, zach.Id));
            rig.Run(Secs(Balance.Items.Orbital.Countdown + 0.1f));
            Assert.IsTrue(rig.Sim.DoorBroken[0], "a door");
            Assert.IsTrue(rig.Sim.DoorBroken[1], "an unlocked gate");
            Assert.IsFalse(rig.Sim.DoorBroken[2], "never a shutter");
            Assert.IsTrue(rig.Sim.WindowsBroken[0], "a window");
            Assert.AreEqual(BarricadeState.Broken, rig.Sim.Barricades[0], "a pallet");
            Assert.IsFalse(rig.Sim.DoorBroken[3], "nothing out of its reach");
            // Slain: the tablet drops where they fall.
            rig.Sim.Eliminate(me, "stake");
            Assert.Less(me.Inv.FirstSlotOf(ItemType.OrbitalStrike), 0);
            Assert.IsTrue(rig.Sim.Drops.Any(d => d.Item == ItemType.OrbitalStrike && Vector2.Distance(d.Pos, me.Pos) < 1f), "lying where they fell");
        }

        [Test]
        public void ItFollowsWithALag_TheyCanKeepPartlyOut_ButNeverGetClear_AndSilencedTheyCantUseItems()
        {
            var rig = new SimRig(2);
            SimPlayer zach = rig.P(1), me = rig.P(2), mate = rig.P(3);
            rig.Place(zach, 1500f, 1500f);
            rig.Place(me, -1500f, -1500f);
            rig.Place(mate, 0f, 0f);
            rig.Run(1);
            rig.Sim.AddItem(me, ItemType.OrbitalStrike);
            Assert.IsTrue(rig.Sim.CallStrike(me.Id, mate.Id), "a teammate too");
            rig.Run(Secs(Balance.Items.Orbital.Countdown + 0.1f));
            // Running flat out: never more than most of its width behind.
            float r = Scale.D(Balance.Items.Orbital.Radius), worst = 0f;
            rig.Run(Secs(3f), p => p.Id == mate.Id ? new InputCmd { Move = Vector2.right, Buttons = Btn.Run, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
            for (int i = 0; i < Secs(2f); i++)
            {
                rig.Run(1, p => p.Id == mate.Id ? new InputCmd { Move = Vector2.right, Buttons = Btn.Run, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                worst = Mathf.Max(worst, Vector2.Distance(rig.Sim.Strikes[0].Pos, mate.Pos));
            }
            Assert.Greater(worst, r * 0.3f, "partly out of it while they keep moving");
            Assert.Less(worst, r, "but never clear of it");
            // Silenced: no item use.
            mate.Inv.Add(ItemType.Bottle, 1);
            int slot = mate.Inv.FirstSlotOf(ItemType.Bottle);
            rig.Run(1, p => p.Id == mate.Id ? new InputCmd { Buttons = Btn.Primary, Item = slot + 1, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
            Assert.AreEqual(0, rig.Sim.Thrown.Count, "nothing thrown while it's on them");
        }

        [Test]
        public void TheStrike_IsSentOverTheWire_AndDaphnesTabletIsCalledByCommand()
        {
            Assert.GreaterOrEqual(Wire.Version, 8, "a new wire");
            Assert.IsTrue(SnapshotFrame.Lists.Any(l => l.name == "Strikes" && l.element == typeof(OrbitalStrike)));
            var rig = new SimRig(1);
            int i = System.Array.FindIndex(SnapshotFrame.Lists, l => l.name == "Strikes");
            Assert.AreSame(rig.Sim.Strikes, SnapshotFrame.ListOf(rig.Sim, i));
            Assert.AreSame(rig.Sim.Bodies, SnapshotFrame.ListOf(rig.Sim, System.Array.FindIndex(SnapshotFrame.Lists, l => l.name == "Bodies")));
            Assert.AreEqual((byte)12, (byte)Command.OrbitalStrike);
        }

        [Test]
        public void TheTablet_OffersOnlyPlayersInExploredGround_AndLocksTheOneClicked()
        {
            var rig = new SimRig(2);
            SimPlayer zach = rig.P(1), me = rig.P(2), mate = rig.P(3);
            rig.Place(me, 0f, 0f);
            rig.Place(zach, 1500f, 0f);
            rig.Place(mate, -1500f, 0f);
            var fog = new FogOfWar(90f, 64);
            Assert.AreEqual(0, GameHud.StrikeTargets(rig.Sim, fog, me).Count, "nothing explored, nothing to select");
            fog.Reveal(new[] { zach.Pos + new Vector2(-3f, -3f), zach.Pos + new Vector2(3f, -3f), zach.Pos + new Vector2(3f, 3f), zach.Pos + new Vector2(-3f, 3f) });
            var targets = GameHud.StrikeTargets(rig.Sim, fog, me);
            CollectionAssert.AreEqual(new[] { zach }, targets, "Zach, where it's been explored");
            fog.RevealAll();
            targets = GameHud.StrikeTargets(rig.Sim, fog, me);
            CollectionAssert.AreEquivalent(new[] { zach, mate }, targets, "teammates too, never yourself");
            mate.Ghost = true;
            CollectionAssert.AreEquivalent(new[] { zach }, GameHud.StrikeTargets(rig.Sim, fog, me), "never a ghost");
            Object.DestroyImmediate(fog.Texture);
        }

        [Test]
        public void InTesting_YouCanStrikeYourself_FromTheTabletOrTheEffectsPanel()
        {
            var rig = new SimRig(1, testMode: true);
            SimPlayer me = rig.P(2);
            rig.Run(1);
            var fog = new FogOfWar(90f, 64);
            fog.RevealAll();
            CollectionAssert.Contains(GameHud.StrikeTargets(rig.Sim, fog, me), me, "yourself, in testing");
            Object.DestroyImmediate(fog.Texture);
            Assert.IsTrue(MatchSim.TestFxButtons.Any(b => b.fx == TestFx.Strike && b.label == "K.P.O.S. on yourself"), "in the testing effects panel");
            int had = me.Inv.FirstSlotOf(ItemType.OrbitalStrike);
            rig.Sim.PlayTestFx(me.Id, TestFx.Strike);
            Assert.AreEqual(1, rig.Sim.Strikes.Count);
            Assert.AreEqual(me.Id, rig.Sim.Strikes[0].Target);
            Assert.AreEqual(had, me.Inv.FirstSlotOf(ItemType.OrbitalStrike), "the panel uses no tablet up");
            float hp0 = me.Hp;
            rig.Run(Secs(Balance.Items.Orbital.Countdown + 1.5f));
            Assert.Less(me.Hp, hp0, "it comes down on you");
            Assert.Greater(me.SilencedT, 0f);
            // Off testing, never yourself.
            var real = new SimRig(1);
            var fog2 = new FogOfWar(90f, 64);
            fog2.RevealAll();
            CollectionAssert.DoesNotContain(GameHud.StrikeTargets(real.Sim, fog2, real.P(2)), real.P(2));
            Object.DestroyImmediate(fog2.Texture);
        }
    }
}
