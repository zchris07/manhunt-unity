using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// A survivor's quick keys. Space throws: the throwable in hand, or else the first in the inventory. E, when there's
    /// nothing to interact with, heals: the healing item in hand, or else the first in the inventory; with no healing item,
    /// or at full health, holding E drinks a mini shield instead (let go part way and it stops). R drinks a Dr Pepper. None
    /// of them changes the item in hand: the item used shows in it for the moment it's used (a mini shield for as long as E
    /// is held drinking it), then the one selected is back.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>How long the item a quick key used stays in hand (the throw's, the bite's, the swig's length).</summary>
        public const float QuickThrowShow = 0.45f, QuickEatShow = 1.05f, QuickSwigShow = 1f;

        /// <summary>Thrown by hand: a bottle, a jar of piss, the Grapes of Wrath.</summary>
        public static bool Throwable(ItemType t) => t == ItemType.Bottle || t == ItemType.Piss || t == ItemType.Book;
        /// <summary>Eaten for health: duck confit, a Mr Beast bar.</summary>
        public static bool Healing(ItemType t) => t == ItemType.Confit || t == ItemType.MrBeastBar;

        /// <summary>Prompts that show something but leave E nothing to do (a name, a gate with no power, a full inventory).</summary>
        public static bool Passive(Prompt q) => q == Prompt.None || q == Prompt.NameNpc || q == Prompt.NameLoot || q == Prompt.NameDrop
            || q == Prompt.GatePowerless || q == Prompt.InventoryFull;

        /// <summary>Free to use an item off a quick key: on their feet, hands free, not hidden, busy or climbing.</summary>
        bool QuickReady(SimPlayer p) => p.Role == Role.Survivor && !p.Ghost && p.CanAct && p.Action == ActionKind.None && p.HideState == 0 && !p.Move.Climbing;

        /// <summary>The slot a quick key uses: the one in hand if it's the right kind, else the first in the inventory that is.</summary>
        int QuickSlotFor(SimPlayer p, System.Func<ItemType, bool> kind)
        {
            Inventory.Slot held = p.Selected;
            if (held != null && kind(held.item)) return p.SelSlot;
            for (int i = 0; i < p.Inv.Limit; i++)
            {
                Inventory.Slot s = p.Inv.SlotAt(i);
                if (s != null && !s.Empty && kind(s.item)) return i;
            }
            return -1;
        }

        static void ShowQuick(SimPlayer p, ItemType item, float seconds)
        {
            p.QuickItem = (int)item;
            p.QuickT = seconds;
        }

        /// <summary>Space: throws the throwable in hand, or the first in the inventory, where they aim.</summary>
        public void QuickThrow(SimPlayer p, InputCmd cmd)
        {
            if (!QuickReady(p)) return;
            int slot = QuickSlotFor(p, Throwable);
            if (slot < 0)
            {
                Tell(p, "Nothing to throw");
                return;
            }
            ItemType item = p.Inv.SlotAt(slot).item;
            if (UseItemAt(p, cmd, slot)) ShowQuick(p, item, QuickThrowShow);
        }

        /// <summary>
        /// E with nothing to interact with: eats the healing item in hand, or the first in the inventory; with a mini shield
        /// in hand, or none to eat, or at full health, starts drinking a mini shield (for as long as E is held).
        /// </summary>
        public void QuickHeal(SimPlayer p, InputCmd cmd)
        {
            if (!QuickReady(p)) return;
            bool hurt = p.Hp < 0.999f;
            int heal = QuickSlotFor(p, Healing);
            int shield = p.Inv.FirstSlotOf(ItemType.MiniShield);
            Inventory.Slot held = p.Selected;
            bool shieldInHand = held != null && held.item == ItemType.MiniShield;
            if (hurt && heal >= 0 && !shieldInHand)
            {
                ItemType item = p.Inv.SlotAt(heal).item;
                if (UseItemAt(p, cmd, heal)) ShowQuick(p, item, QuickEatShow);
                return;
            }
            if (shield >= 0)
            {
                if (p.Shield >= Balance.Items.ShieldMax - 1e-6f)
                {
                    Tell(p, "Shield is full");
                    return;
                }
                StartAction(p, ActionKind.Drink, Balance.Items.ShieldDrinkTime, 0);
                p.QuickShield = true;
                ShowQuick(p, ItemType.MiniShield, 0.2f);
                return;
            }
            if (heal >= 0) Tell(p, "Already at full health");
        }

        /// <summary>R: drinks a Dr Pepper (the one in hand, or the first in the inventory).</summary>
        public void QuickDrink(SimPlayer p, InputCmd cmd)
        {
            if (!QuickReady(p)) return;
            int slot = QuickSlotFor(p, t => t == ItemType.DoctorPepper);
            if (slot < 0)
            {
                Tell(p, "No Dr Pepper");
                return;
            }
            if (UseItemAt(p, cmd, slot)) ShowQuick(p, ItemType.DoctorPepper, QuickSwigShow);
        }
    }
}
