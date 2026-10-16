using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.Visibility;
using Vision.World;
using static Vision.Tests.SimRig;

namespace Vision.Tests
{
    /// <summary>
    /// The balance changes (an easier skill check, the shotgun's shorter stun, the 0.50 cal's shorter shove and its fading
    /// slow, piss in the eyes, 50 Nic's wider cone and its linger falling off over four seconds), the first-time aiming
    /// hint, the Hemp Beam as an item that keeps its charges (Zach taking one into his own), Daphne Cheng doing all that
    /// Monique does, and the level's new pieces: Miga Sushi and the Diddy Chamber on the outskirts with what they hold,
    /// flood lights wired to the generators, and survivors starting scattered.
    /// </summary>
    public class OutskirtsItemsAndBalanceTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState() => saved = MatchState.Current;

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        [Test]
        public void TheBalanceChanges()
        {
            Assert.AreEqual(350f, Balance.SkillCheck.Speed, "the skill check's tick a little slower");
            Assert.Greater(Balance.SkillCheck.Speed, Balance.Graveyard.PickSpeeds[Balance.Graveyard.PickSpeeds.Length - 1], "still faster than the lock pick's");
            Assert.AreEqual(2.1f * 0.5f, Balance.Items.Shotgun.Stun, 1e-5f, "the shotgun stuns half as long");
            Assert.AreEqual(1100f * 0.75f, Balance.Items.Sniper.KbPeak, 1e-3f, "the 0.50 cal shoves a quarter less far");
            Assert.AreEqual(2f, Balance.Items.Piss.BlurTime);
            Assert.AreEqual(Balance.Hunter.Vape.HalfAngleDeg * 1.2f, MatchSim.VapeHalfAngle(true), 1e-5f, "50 Nic's cone 20% wider");
            Assert.AreEqual(Balance.Hunter.Vape.HalfAngleDeg, MatchSim.VapeHalfAngle(false), 1e-5f, "Penjamin's as it was");
            Assert.AreEqual(1.1f, Balance.Chacko.NicRangeMul, 1e-5f, "and only 10% further");
        }

        [Test]
        public void TheSniperSlow_AndTheGassLinger_FallOffLinearly()
        {
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            rig.Place(zach, 0f, 0f);
            rig.Place(s, 2000f, 2000f);
            zach.SniperSlowT = Balance.Items.Sniper.SlowTime;
            rig.Run(Secs(0.5f));
            Assert.AreEqual(1f - Balance.Items.Sniper.Slow * 0.5f, zach.Move.FadeMul, 0.05f, "half way through: a quarter slower");
            rig.Run(Secs(0.6f));
            Assert.AreEqual(0f, zach.SniperSlowT);

            // Out of Penjamin's (or 50 Nic's) cloud: everything falls off together over five seconds.
            Assert.AreEqual(5f, Balance.Hunter.Vape.Linger);
            s.VapeSlow = 0.4f;
            s.VapeT = s.VapeSlowT = s.DarkT = Balance.Hunter.Vape.Linger;
            rig.Run(Secs(2.5f));
            Assert.AreEqual(0.5f, MatchSim.VapeLeft(s), 0.03f, "half way out: half left");
            Assert.AreEqual(1f - 0.4f * 0.5f, s.Move.FadeMul, 0.03f, "its slow halved");
            Assert.AreEqual(s.VapeT, s.DarkT, 1e-4f, "the narrowed sight on the same clock");
            Assert.AreEqual(s.VapeT, s.VapeSlowT, 1e-4f, "and the slow");
            rig.Run(Secs(2.6f));
            Assert.AreEqual(0f, MatchSim.VapeLeft(s), "all gone at five seconds");
            Assert.AreEqual(0f, s.DarkT);
            Assert.AreEqual(0f, s.VapeSlowT);
            Assert.AreEqual(1f, s.Move.FadeMul, 1e-4f, "back to full speed");
        }

        [Test]
        public void PissInTheEyes_And_TheFirstTimeAHintToAim()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 2000f, 2000f);
            s.Inv.Clear();
            s.Inv.Add(ItemType.Sniper);
            int slot = s.Inv.FirstSlotOf(ItemType.Sniper);
            Assert.IsFalse(s.SniperHintShown);
            rig.Tap(s.Id, Btn.None, 0f, slot + 1);
            Assert.IsTrue(s.SniperHintShown, "told how to aim the first time it's in hand");

            SimPlayer zach = rig.P(1);
            zach.PissBlurT = Balance.Items.Piss.BlurTime;
            rig.Run(Secs(1f));
            Assert.AreEqual(1f, zach.PissBlurT, 0.05f, "the blur falls off over two seconds");
        }

        [Test]
        public void TheHempBeam_IsAnItem_ThatKeepsItsCharges_AndZachCanTakeIt()
        {
            var rig = new SimRig(2, 1);
            SimPlayer a = rig.P(2), b = rig.P(3), zach = rig.P(1);
            rig.Place(a, 0f, 0f);
            rig.Place(b, 30f, 0f);
            rig.Place(zach, 2000f, -2000f);
            a.Inv.Clear();
            a.Inv.Add(ItemType.HempBeam);
            int slot = a.Inv.FirstSlotOf(ItemType.HempBeam);
            Assert.AreEqual(Balance.Hunter.Beam.Charges, a.Inv.AmountAt(slot), 1e-4f);
            rig.Tap(a.Id, Btn.Primary, 0f, slot + 1);
            Assert.Greater(a.BeamT, 0f, "a left click fires it");
            Assert.AreEqual(Balance.Hunter.Beam.Charges - 1, a.Inv.AmountAt(slot), 1e-4f, "one charge spent from the item");
            rig.Run(Secs(Balance.Hunter.Beam.Windup + Balance.Sexton.Defense.BeamTime + 0.5f), p => p.Id == a.Id ? new InputCmd { Item = slot + 1, AimDist = 100f } : (InputCmd?)null);
            rig.Tap(a.Id, Btn.Drop, 0f, slot + 1);
            DropItem dropped = rig.Sim.Drops.Find(d => d.Item == ItemType.HempBeam);
            Assert.IsNotNull(dropped, "dropped like any item");
            Assert.AreEqual(Balance.Hunter.Beam.Charges - 1, dropped.Amount, 1e-4f, "with the charges it had left");
            rig.Sim.Teleport(b.Id, dropped.Pos + U(10, 0));
            rig.Run(1);
            Assert.AreEqual(Prompt.PickDrop, b.Prompt);
            rig.Tap(b.Id, Btn.Interact);
            int bs = b.Inv.FirstSlotOf(ItemType.HempBeam);
            Assert.GreaterOrEqual(bs, 0);
            Assert.AreEqual(Balance.Hunter.Beam.Charges - 1, b.Inv.AmountAt(bs), 1e-4f, "the next survivor gets what was left");
            Assert.AreEqual(0, b.BeamCharges, "survivors have no R beam any more");

            rig.Sim.DropHempBeam(zach.Pos + U(10, 0));
            zach.BeamCharges = 1;
            rig.Run(1);
            Assert.AreEqual(Prompt.PickDrop, zach.Prompt, "Zach can take one too");
            rig.Tap(zach.Id, Btn.Interact);
            Assert.AreEqual(Balance.Hunter.Beam.Charges, zach.BeamCharges, "its charges into his own, up to a full set");
        }

        [Test]
        public void Daphne_GivesAP250_CantBeSlain_AndShootsWhoeverAttacksHer_WithoutChasing()
        {
            Assert.IsTrue(Texts.NpcNames.Contains("Daphne Cheng"));
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), s2 = rig.P(3), zach = rig.P(1);
            rig.Place(zach, 2000f, 2000f);
            var daphne = new Daphne(rig.Sim) { Pos = U(0, 0) };
            rig.Sim.Npcs.Add(daphne);
            rig.Place(s, 50f, 0f);
            rig.Place(s2, -2000f, -2000f);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkDaphne, s.Prompt);
            s.Inv.Clear();
            rig.Tap(s.Id, Btn.Interact);
            Assert.GreaterOrEqual(s.Inv.FirstSlotOf(ItemType.Pistol), 0, "a P250");
            Assert.IsNull(daphne.HealthFor(Role.Hunter), "nobody can slay her: no health bar");
            Assert.IsNull(daphne.HealthFor(Role.Survivor));

            // Attacked: she shoots back from where she stands, and still stands after all of it.
            rig.Place(s, -2000f, 2000f);
            rig.Place(s2, 200f, 0f);
            Vector2 at = daphne.Pos;
            float hp = s2.Hp;
            for (int i = 0; i < 6; i++) { daphne.ItemHit(rig.Sim, s2, "bottle"); daphne.Slash(rig.Sim, zach, 2); }
            daphne.ItemHit(rig.Sim, s2, "bottle");
            Assert.IsTrue(daphne.Armed, "she draws her pistol");
            rig.Run(Secs(3f));
            Assert.Less(s2.Hp, hp, "and fires at her attacker");
            Assert.AreEqual(at, daphne.Pos, "without chasing");
            Assert.IsTrue(daphne.Alive, "never slain");
            Assert.IsTrue(NpcRoster.Makers.Select(m => m(rig.Sim)).Any(n => n is Daphne), "she's in every match");
        }

        [Test]
        public void TheOutskirtsPlaces_TheFloodLights_AndScatteredSpawns()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            try
            {
                MatchState.Current.TestingMode = false;
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();
                MapLayout L = world.Layout;

                // Miga Sushi and the Diddy Chamber, out on the edge, apart, named, holding what they hold.
                foreach (Rect r in new[] { L.Sushi, L.Chamber })
                {
                    float d = r.center.magnitude / L.HalfExtent;
                    Assert.That(d, Is.InRange(0.7f, 0.9f), "on the outskirts");
                }
                Assert.Less(L.Chamber.width, L.Sushi.width, "the chamber very small");
                Assert.Greater(Vector2.Distance(L.Sushi.center, L.Chamber.center), L.HalfExtent * 0.5f, "far apart");
                CollectionAssert.AreEquivalent(new[] { "MIGA SUSHI", "DIDDY CHAMBER" }, world.Places.Select(p => p.name));
                Vector2 Plane(Component c) { Vector3 l = world.transform.InverseTransformPoint(c.transform.position); return new Vector2(l.x, l.z); }
                var inSushi = world.Pickups.Where(p => L.Sushi.Contains(Plane(p))).Select(p => p.item).ToList();
                CollectionAssert.AreEquivalent(new[] { ItemType.HempBeam, ItemType.Sniper }, inSushi, "a Hemp Beam and a 0.50 cal");
                // A Japanese restaurant, in detail: 13 by 10, the bar and its stools, the sake shelf, menu plaques, a tatami room
                // with low tables behind shoji, tables and chairs, paper lanterns, the cat and the bonsai, a scroll.
                Assert.AreEqual(13f * 10f, L.Sushi.width * L.Sushi.height, 0.01f);
                Transform sushi = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Miga Sushi");
                int Pieces(string name) => sushi.GetComponentsInChildren<Transform>(true).Count(t => t.name == name);
                foreach (string piece in new[] { "Sushi Counter", "Sake Shelf", "Menu Plaques", "Tatami", "Shoji", "Wall Scroll", "Maneki Neko", "Bonsai" })
                    Assert.AreEqual(1, Pieces(piece), piece);
                Assert.AreEqual(2, Pieces("Low Table"));
                Assert.GreaterOrEqual(Pieces("Stool"), 8, "a row of stools at the bar");
                Assert.GreaterOrEqual(Pieces("Paper Lantern"), 6, "lanterns down the middle, over the bar and by the door");
                Assert.AreEqual(8, Pieces("Chair"), "two tables of four");
                // No rocks or trees inside it.
                foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!(col.name.StartsWith("Rock") || col.name.StartsWith("Conifer") || col.name.StartsWith("DeadTree"))) continue;
                    Assert.IsFalse(L.Sushi.Contains(Plane(col)), $"{col.name} inside Miga Sushi");
                }
                Transform chamber = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Diddy Chamber");
                Assert.Greater(chamber.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Baby Oil"), 6, "baby oil all over");
                Assert.AreEqual(1, chamber.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Comfy Bed"));

                // Every generator's work site: three light towers as tall as a big tree, lighting all round them as brightly as
                // a player's own light, work lights, cables and clutter; all dark until it runs, then on for good. The
                // graveyard's: two towers and their cables alone.
                // The building's generators have no work site: either one running lights every room of the building.
                BuildingPower power = root.GetComponentInChildren<BuildingPower>();
                Assert.NotNull(power, "the building's lights");
                Assert.AreEqual(2, power.generators.Count, "wired to both of the building's generators");
                foreach (GeneratorObjective g in power.generators) Assert.AreEqual(0, g.floods.Count, "no work site in the building");
                foreach (BuildingPlan.Room r in L.Plan.Rooms)
                    Assert.IsTrue(power.lights.Any(l => r.Area.Contains(Plane(l))), $"a light in the {r.Name}");
                Assert.IsTrue(power.lights.All(l => !l.enabled), "dark until one runs");
                power.generators[0].SetState(1f, true);
                power.Refresh();
                Assert.IsTrue(power.lights.All(l => l.enabled), "one running lights the whole building");
                power.generators[0].SetState(0f, false);
                power.Refresh();
                foreach (GeneratorObjective g in GeneratorObjective.All.Where(g => g.transform.IsChildOf(root.transform) && !power.generators.Contains(g)))
                {
                    bool grave = (Plane(g) - L.Graveyard.center).magnitude < 0.5f;
                    var towers = g.floods.Where(l => l.bright).ToList();
                    Assert.AreEqual(grave ? 2 : 3, towers.Count, $"{g.name}: its light towers");
                    if (grave) Assert.AreEqual(0, g.floods.Count(l => !l.bright), "the graveyard's: no work lights");
                    else Assert.Greater(g.floods.Count(l => !l.bright), 0, "and work lights");
                    foreach (VisionLight t in towers)
                    {
                        Assert.IsFalse(t.IsCone, "a radius of light, not a beam");
                        Assert.AreEqual(SandboxWorld.TowerLightRange, t.range);
                        Assert.Greater(t.transform.parent.GetComponent<MeshFilter>().sharedMesh.bounds.size.y, 8.5f, "as tall as a big tree");
                        Assert.IsFalse(t.enabled, "dark until it runs");
                    }
                    g.SetState(1f, true);
                    Assert.IsTrue(g.floods.All(l => l.enabled), "on for good once it runs");
                    Assert.IsTrue(g.lamps.All(r => r.sharedMaterial == g.lampOn), "its lamps lit");
                }
                Assert.Greater(root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Cable"), 10, "cables winding to every light");
                Assert.AreEqual(0, root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Tarp"), "no tarps");
                Assert.Greater(root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Glass" || t.name == "Planks" || t.name == "Overgrowth" || t.name == "BigOvergrowth"), 10, "a work site gone to seed");

                // Survivors start scattered over the map, apart, away from Zach, on open ground.
                SimMap map = SimMapBuilder.Build(world);
                var spots = map.SurvivorSpawns.Take(4).ToList();
                for (int i = 0; i < spots.Count; i++)
                {
                    Assert.Less(spots[i].magnitude, L.HalfExtent, "inside the playable map");
                    Assert.Less(L.LakeDepth(spots[i]), -1f, "not in the lake");
                    Assert.GreaterOrEqual(Vector2.Distance(spots[i], map.HunterSpawns[0]), SimMapBuilder.SpawnFromHunter - 0.01f, "away from Zach");
                    for (int j = 0; j < i; j++) Assert.Greater(Vector2.Distance(spots[i], spots[j]), 15f, "away from each other");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
