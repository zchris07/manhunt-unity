using System;
using UnityEngine;

namespace Vision.Player
{
    /// <summary>The original's supplies.</summary>
    public enum ItemType { Bottle, Book, Goggles, Shotgun, DoctorPepper, Trap, Confit, MrBeastBar, MiniShield }

    public readonly struct ItemInfo
    {
        public readonly string name;
        public readonly Color color;
        /// <summary>Weapons never stack: each takes a slot of its own.</summary>
        public readonly bool weapon;
        /// <summary>How many of it the original spreads over the map.</summary>
        public readonly int mapCount;

        public ItemInfo(string name, Color color, bool weapon, int mapCount)
        {
            this.name = name;
            this.color = color;
            this.weapon = weapon;
            this.mapCount = mapCount;
        }
    }

    public static class Items
    {
        public static readonly ItemType[] All = (ItemType[])Enum.GetValues(typeof(ItemType));

        public static ItemInfo Info(ItemType t) => t switch
        {
            ItemType.Bottle => new ItemInfo("Bottle", new Color(0.30f, 0.52f, 0.30f), false, 20),
            ItemType.Book => new ItemInfo("The Grapes of Wrath", new Color(0.62f, 0.48f, 0.26f), false, 4),
            ItemType.Goggles => new ItemInfo("Night vision goggles", new Color(0.30f, 0.62f, 0.32f), false, 3),
            ItemType.Shotgun => new ItemInfo("Shotgun", new Color(0.45f, 0.32f, 0.20f), true, 2),
            ItemType.DoctorPepper => new ItemInfo("Doctor Pepper", new Color(0.50f, 0.10f, 0.14f), false, 8),
            ItemType.Trap => new ItemInfo("Galaxy gas trap", new Color(0.45f, 0.30f, 0.65f), false, 8),
            ItemType.Confit => new ItemInfo("Duck confit", new Color(0.72f, 0.52f, 0.30f), false, 6),
            ItemType.MrBeastBar => new ItemInfo("Mr Beast bar", new Color(0.25f, 0.52f, 0.85f), false, 15),
            _ => new ItemInfo("Mini shield", new Color(0.35f, 0.65f, 0.95f), false, 20),
        };

        /// <summary>Short slot label.</summary>
        public static string Short(ItemType t) => t switch
        {
            ItemType.Book => "Book",
            ItemType.Goggles => "Goggles",
            ItemType.DoctorPepper => "Dr Pepper",
            ItemType.Trap => "Gas trap",
            ItemType.Confit => "Confit",
            ItemType.MrBeastBar => "Beast bar",
            _ => Info(t).name,
        };
    }

    /// <summary>
    /// Eight slots, as in the original. Identical items stack without limit; a weapon always takes a slot of its own.
    /// With <see cref="Infinite"/> (testing mode) using an item never uses it up.
    /// </summary>
    [Serializable]
    public sealed class Inventory
    {
        public const int Slots = 8;

        [Serializable]
        public struct Stack
        {
            public ItemType item;
            public int count;
        }

        [SerializeField] Stack[] slots = new Stack[Slots];

        /// <summary>Testing mode: items are never used up.</summary>
        public bool Infinite;

        public event Action Changed;

        public ItemType? ItemAt(int slot) => slot >= 0 && slot < Slots && slots[slot].count > 0 ? slots[slot].item : (ItemType?)null;
        public int CountAt(int slot) => slot >= 0 && slot < Slots ? slots[slot].count : 0;

        public int Count(ItemType item)
        {
            int n = 0;
            foreach (Stack s in slots) if (s.count > 0 && s.item == item) n += s.count;
            return n;
        }

        /// <summary>Adds up to <paramref name="count"/> items; returns how many fitted.</summary>
        public int Add(ItemType item, int count = 1)
        {
            int added = 0;
            bool weapon = Items.Info(item).weapon;
            while (added < count)
            {
                int slot = -1;
                if (!weapon)
                    for (int i = 0; i < Slots; i++)
                        if (slots[i].count > 0 && slots[i].item == item) { slot = i; break; }
                if (slot < 0)
                    for (int i = 0; i < Slots; i++)
                        if (slots[i].count == 0) { slot = i; break; }
                if (slot < 0) break;
                slots[slot].item = item;
                int take = weapon ? 1 : count - added;
                slots[slot].count += take;
                added += take;
            }
            if (added > 0) Changed?.Invoke();
            return added;
        }

        /// <summary>Uses up one item from a slot (not in testing mode).</summary>
        public bool Remove(int slot)
        {
            if (CountAt(slot) <= 0) return false;
            if (!Infinite) slots[slot].count--;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < Slots; i++) slots[i] = default;
            Changed?.Invoke();
        }
    }
}
