using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// Guns firing for as long as left click is held, shotguns blowing in doors, Njaaron at Zach's side, JARVIS's thirty
    /// seconds for its user, the reload times.
    /// </summary>
    public class AlliesAndTriggersTests
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

        static int Shots(SimRig rig, SimPlayer s, ItemType gun, float seconds)
        {
            if (s.Inv.Count(gun) == 0) s.Inv.Add(gun);
            int slot = s.Inv.FirstSlotOf(gun) + 1;
            int n = 0;
            for (int i = 0; i < SimRig.Secs(seconds); i++)
            {
                foreach (SimPlayer p in rig.Sim.Order)
                    rig.Sim.SubmitInput(p.Id, p.Id == s.Id ? new InputCmd { Buttons = Btn.Primary, Item = slot, Aim = 0f, AimDist = 100f } : new InputCmd { Aim = p.Facing, AimDist = 100f });
                rig.Sim.Step();
                foreach (GameEvent e in rig.Sim.Events) if (e.Kind == EventKind.Shot && e.A == s.Id) n++;
                rig.Sim.Events.Clear();
            }
            return n;
        }

        [Test]
        public void HoldingLeftClick_FiresAGunAgainAndAgain_AsFastAsItReloads()
        {
            var rig = new SimRig(1, 1, true);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, 2500f);
            int pistol = Shots(rig, s, ItemType.Pistol, 1.0f);
            Assert.AreEqual(0.25f, Balance.Items.Pistol.Reload, 1e-4f, "the P250 40% faster: a shot every 0.25 s");
            Assert.That(pistol, Is.InRange(4, 5), "held down, it keeps firing");
            int shotgun = Shots(rig, s, ItemType.Shotgun, 2.5f);
            Assert.That(shotgun, Is.InRange(2, 3), "the shotgun too, at its own 1.2 s");
            Assert.AreEqual(1f, Balance.Items.Golden.Reload, 1e-5f, "the Legendary pump at a second");
            Assert.AreEqual(1f, Balance.Items.ZachPump.Reload, 1e-5f, "and Zach's golden pump");
        }

        [Test]
        public void AShotgunBlast_BlowsInAShutDoor_ButNotTheGraveyardsGates()
        {
            var rig = new SimRig(1, 1, false, 1, m =>
            {
                m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(150, -60), B = SimRig.U(150, 60) });
                m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(-150, -60), B = SimRig.U(-150, 60), Gate = true });
            });
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, 2500f);
            rig.Sim.DoorLocked[1] = false;
            s.Inv.Add(ItemType.Shotgun);
            int slot = s.Inv.FirstSlotOf(ItemType.Shotgun) + 1;
            rig.Run(SimRig.Secs(0.5f), p => p.Id == 2 ? new InputCmd { Buttons = Btn.Primary, Item = slot, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
            Assert.IsTrue(rig.Sim.DoorBroken[0], $"the door blown in (shells {s.Inv.AmountAt(slot - 1)}, item {s.Inv.ItemAt(slot - 1)}, reload {s.ReloadT})");
            rig.Run(SimRig.Secs(1.5f));
            rig.Run(SimRig.Secs(0.5f), p => p.Id == 2 ? new InputCmd { Buttons = Btn.Primary, Item = slot, Aim = Mathf.PI, AimDist = 100f } : (InputCmd?)null);
            Assert.IsFalse(rig.Sim.DoorBroken[1], "never the gates");
        }

        [Test]
        public void NjaaronAtZachsSide_HuntsWhoeverZachHitsOrWhoeverHitsZach_AndIsSparedByZach()
        {
            var rig = new SimRig(2, 1, true);
            SimPlayer zach = rig.P(1), s = rig.P(2), s2 = rig.P(3);
            var nj = new Njaaron(rig.Sim) { Pos = SimRig.U(0, 0) };
            rig.Sim.Npcs.Add(nj);
            rig.Place(zach, 50f, 0f);
            rig.Place(s, 600f, 0f);
            rig.Place(s2, -600f, 0f);
            rig.Run(1);
            nj.Talk(rig.Sim, zach);
            nj.Answer(rig.Sim, zach, true);
            Assert.AreEqual(Njaaron.Mode.Ally, nj.State);
            Assert.AreEqual(zach.Id, nj.Ally);
            // Zach strikes s: Njaaron goes after them.
            rig.Sim.HurtSurvivor(s, 0.1f, zach, "slash");
            Assert.IsTrue(nj.Aggressive, "hunting");
            float before = Vector2.Distance(nj.Pos, s.Pos);
            rig.Run(SimRig.Secs(1f));
            Assert.Less(Vector2.Distance(nj.Pos, s.Pos), before, "after them");
            // s2 shoots Zach: now Njaaron hunts s2.
            rig.Sim.HurtHunter(zach, 5f, s2, "bullet");
            before = Vector2.Distance(nj.Pos, s2.Pos);
            rig.Run(SimRig.Secs(1f));
            Assert.Less(Vector2.Distance(nj.Pos, s2.Pos), before, "after whoever hit Zach");
            // Zach's own swings, beam and burst never touch him.
            nj.Slash(rig.Sim, zach, 2);
            nj.BeamHit(rig.Sim, zach);
            Assert.IsTrue(nj.Alive);
            Assert.IsFalse(nj.VapeHurts, "Penjamin passes him by");
            rig.Sim.Bursts.Add(new BurstWave { Origin = zach.Pos, Dir = (nj.Pos - zach.Pos).normalized, T0 = rig.Sim.Time, By = zach.Id });
            rig.Run(SimRig.Secs(0.5f));
            Assert.AreEqual(0f, nj.StunT, 1e-4f, "nor his burst");
        }

        [Test]
        public void Jarvis_RunsThirtySecondsForItsUser_TenForEveryoneElsesScreen()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            s.Jarvis = 1;
            rig.Tap(2, Btn.Ability);
            Assert.AreEqual(Balance.Sexton.JarvisUserSec, s.JarvisT, 0.1f);
            Assert.AreEqual(30f, Balance.Sexton.JarvisUserSec);
            Assert.AreEqual(Balance.Sexton.JarvisRadarSec, rig.Sim.RevealT, 0.1f);
            rig.Run(SimRig.Secs(12f));
            Assert.AreEqual(0f, rig.Sim.RevealT, 1e-4f, "everyone else's screens: ten seconds");
            Assert.Greater(s.JarvisT, 15f, "its user's: thirty");
        }
    }
}
