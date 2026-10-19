using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// A survivor's quick keys: Space throws (the throwable in hand, else the first in the inventory), E heals when there's
    /// nothing to interact with (the healing item in hand, else the first; held, a mini shield when there's nothing to heal
    /// or no healing item, stopping if E is let go), R drinks a Dr Pepper. The item in hand never changes.
    /// </summary>
    public class QuickKeysTests
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

        /// <summary>A survivor alone (Zach far off), holding a shotgun in slot 1, with the given items after it.</summary>
        static SimRig Rig(out SimPlayer s, params ItemType[] items)
        {
            var rig = new SimRig();
            s = rig.P(2);
            rig.Place(rig.P(1), 2500f, 2500f);
            rig.Place(s, 0f, 0f);
            s.Inv.Clear();
            s.Inv.Add(ItemType.Shotgun, 1);
            foreach (ItemType t in items) s.Inv.Add(t, 1);
            rig.Tap(s.Id, Btn.None, 0f, 1);
            Assert.AreEqual(ItemType.Shotgun, s.Selected.item);
            return rig;
        }

        [Test]
        public void Space_ThrowsTheFirstThrowable_AndTheShotgunStaysInHand()
        {
            SimRig rig = Rig(out SimPlayer s, ItemType.MrBeastBar, ItemType.Bottle, ItemType.Piss);
            rig.Run(SimRig.Secs(0.6f));
            rig.Tap(s.Id, Btn.Space, 0.5f);
            Assert.AreEqual(1, rig.Sim.Thrown.Count, "thrown");
            Assert.AreEqual(ItemType.Bottle, rig.Sim.Thrown[0].Item, "the first throwable: the bottle");
            Assert.AreEqual(0, s.Inv.Count(ItemType.Bottle));
            Assert.AreEqual(1, s.Inv.Count(ItemType.Piss), "the piss kept");
            Assert.AreEqual(0, s.SelSlot, "the shotgun still selected");
            Assert.AreEqual((int)ItemType.Bottle, s.QuickItem, "the bottle in hand as it goes");
            Assert.Greater(s.QuickT, 0f);
            rig.Run(SimRig.Secs(0.6f));
            Assert.AreEqual(0f, s.QuickT, "then the shotgun back in hand");
            Assert.AreEqual(ItemType.Shotgun, s.Selected.item);

            // Holding a throwable: that one goes, though another comes first.
            SimRig r2 = Rig(out SimPlayer s2, ItemType.Bottle, ItemType.Piss);
            int piss = s2.Inv.FirstSlotOf(ItemType.Piss);
            r2.Tap(s2.Id, Btn.None, 0f, piss + 1);
            r2.Tap(s2.Id, Btn.Space);
            Assert.AreEqual(ItemType.Piss, r2.Sim.Thrown[0].Item, "the jar in hand");
            Assert.AreEqual(1, s2.Inv.Count(ItemType.Bottle));

            // Nothing to throw: nothing happens.
            SimRig r3 = Rig(out SimPlayer s3, ItemType.Confit);
            r3.Tap(s3.Id, Btn.Space);
            Assert.AreEqual(0, r3.Sim.Thrown.Count);
        }

        [Test]
        public void E_WithNothingToInteractWith_EatsTheFirstHealingItem_OrTheOneInHand()
        {
            SimRig rig = Rig(out SimPlayer s, ItemType.Bottle, ItemType.MrBeastBar, ItemType.Confit);
            s.Hp = 0.5f;
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(0.5f + Balance.Items.BeastBarHeal, s.Hp, 1e-3f, "the first healing item: the Mr Beast bar");
            Assert.AreEqual(0, s.Inv.Count(ItemType.MrBeastBar));
            Assert.AreEqual(1, s.Inv.Count(ItemType.Confit));
            Assert.AreEqual(0, s.SelSlot, "the shotgun still selected");
            Assert.AreEqual((int)ItemType.MrBeastBar, s.QuickItem);
            rig.Run(SimRig.Secs(1.2f));

            // The duck confit in hand: that's eaten.
            SimRig r2 = Rig(out SimPlayer s2, ItemType.MrBeastBar, ItemType.Confit);
            s2.Hp = 0.5f;
            r2.Tap(s2.Id, Btn.None, 0f, s2.Inv.FirstSlotOf(ItemType.Confit) + 1);
            r2.Tap(s2.Id, Btn.Interact);
            Assert.AreEqual(1f, s2.Hp, 1e-3f, "the confit in hand: healed to full");
            Assert.AreEqual(1, s2.Inv.Count(ItemType.MrBeastBar));

            // Something to interact with (an item lying here): E takes it, no healing.
            var r3 = new SimRig(1, 1, false, 1, m => m.Loot.Add(new SimMap.LootDef { Pos = SimRig.U(0f, 0f), Item = ItemType.Bottle }));
            SimPlayer s3 = r3.P(2);
            r3.Place(r3.P(1), 2500f, 2500f);
            r3.Place(s3, 0f, 0f);
            s3.Inv.Clear();
            s3.Inv.Add(ItemType.Confit, 1);
            s3.Hp = 0.5f;
            r3.Run(2);
            Assert.AreEqual(Prompt.Loot, s3.Prompt);
            Assert.IsFalse(MatchSim.Passive(s3.Prompt));
            r3.Tap(s3.Id, Btn.Interact);
            Assert.AreEqual(0.5f, s3.Hp, 1e-3f, "not healed");
            Assert.AreEqual(1, s3.Inv.Count(ItemType.Bottle), "the bottle taken");
        }

        [Test]
        public void HoldingE_DrinksAMiniShield_AtFullHealthOrWithNothingToEat_LettingGoStopsIt()
        {
            // At full health with a confit and a mini shield: holding E drinks the shield, the shield in hand meanwhile.
            SimRig rig = Rig(out SimPlayer s, ItemType.Confit, ItemType.MiniShield);
            rig.Hold(s.Id, Btn.Interact, SimRig.Secs(0.5f));
            Assert.AreEqual(ActionKind.Drink, s.Action);
            Assert.AreEqual((int)ItemType.MiniShield, s.QuickItem);
            Assert.Greater(s.QuickT, 0f, "the shield in hand");
            rig.Hold(s.Id, Btn.Interact, SimRig.Secs(Balance.Items.ShieldDrinkTime));
            Assert.AreEqual(Balance.Items.ShieldAmount, s.Shield, 1e-4f, "drunk");
            Assert.AreEqual(0, s.Inv.Count(ItemType.MiniShield));
            Assert.AreEqual(1, s.Inv.Count(ItemType.Confit), "the confit kept");
            Assert.AreEqual(0f, s.QuickT, "the shotgun back in hand");
            Assert.AreEqual(0, s.SelSlot);

            // Hurt with nothing to eat: the shield too. Let go part way: it stops, nothing drunk, the shotgun back.
            SimRig r2 = Rig(out SimPlayer s2, ItemType.MiniShield);
            s2.Hp = 0.5f;
            r2.Hold(s2.Id, Btn.Interact, SimRig.Secs(1f));
            Assert.AreEqual(ActionKind.Drink, s2.Action);
            r2.Run(2);
            Assert.AreEqual(ActionKind.None, s2.Action, "let go: stopped");
            Assert.AreEqual(0f, s2.Shield);
            Assert.AreEqual(1, s2.Inv.Count(ItemType.MiniShield), "not used up");
            Assert.AreEqual(0f, s2.QuickT, "the shotgun back in hand at once");
            Assert.AreEqual(ItemType.Shotgun, s2.Selected.item);

            // Hurt with food: E eats; the shield waits.
            SimRig r3 = Rig(out SimPlayer s3, ItemType.MiniShield, ItemType.MrBeastBar);
            s3.Hp = 0.5f;
            r3.Tap(s3.Id, Btn.Interact);
            Assert.AreEqual(ActionKind.None, s3.Action);
            Assert.AreEqual(0.7f, s3.Hp, 1e-3f);
            Assert.AreEqual(1, s3.Inv.Count(ItemType.MiniShield));
        }

        [Test]
        public void R_DrinksADrPepper_AndTheShotgunStaysInHand()
        {
            SimRig rig = Rig(out SimPlayer s, ItemType.Bottle, ItemType.DoctorPepper);
            rig.Tap(s.Id, Btn.Beam);
            Assert.Greater(s.Move.BoostT, 0f, "boosted");
            Assert.AreEqual(0, s.Inv.Count(ItemType.DoctorPepper));
            Assert.AreEqual(0, s.SelSlot);
            Assert.AreEqual((int)ItemType.DoctorPepper, s.QuickItem, "the can in hand for the swig");
            rig.Run(SimRig.Secs(1.1f));
            Assert.AreEqual(ItemType.Shotgun, s.Selected.item);
            Assert.AreEqual(0f, s.QuickT);
        }
    }
}
