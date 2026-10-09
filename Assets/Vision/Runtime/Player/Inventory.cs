using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vision.Player
{
    /// <summary>The original's items (a golden shotgun is Plasma's golden pump: a <see cref="Shotgun"/> slot marked golden).</summary>
    public enum ItemType { Bottle, Book, Goggles, Shotgun, DoctorPepper, Trap, Confit, MrBeastBar, MiniShield, Pistol, Sniper, Piss }

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

        /// <summary>Names, map colours (the original's minimap dots) and map counts (the original's <c>items.counts</c>).</summary>
        /// <summary>How many of an item the original spreads over the map (its balance table).</summary>
        public static int MapCount(ItemType t)
        {
            foreach (var (item, count) in Game.Balance.Items.Counts) if (item == t) return count;
            return 0;
        }

        public static ItemInfo Info(ItemType t) => t switch
        {
            ItemType.Bottle => new ItemInfo("Bottle", Hex(0x4cff6a), false, MapCount(ItemType.Bottle)),
            ItemType.Book => new ItemInfo("The Grapes of Wrath", Hex(0xa86ac8), false, MapCount(ItemType.Book)),
            ItemType.Goggles => new ItemInfo("Night vision goggles", Hex(0x5cff9a), false, MapCount(ItemType.Goggles)),
            ItemType.Shotgun => new ItemInfo("Shotgun", Hex(0xff6a4a), true, MapCount(ItemType.Shotgun)),
            ItemType.DoctorPepper => new ItemInfo("Doctor Pepper", Hex(0xd8283a), false, MapCount(ItemType.DoctorPepper)),
            ItemType.Trap => new ItemInfo("Galaxy gas trap", Hex(0xd06aff), false, MapCount(ItemType.Trap)),
            ItemType.Confit => new ItemInfo("Duck confit", Hex(0xffb04a), false, MapCount(ItemType.Confit)),
            ItemType.MrBeastBar => new ItemInfo("Mr Beast bar", Hex(0x8a5a2a), false, MapCount(ItemType.MrBeastBar)),
            ItemType.MiniShield => new ItemInfo("Mini shield", Hex(0x3aa8ff), false, MapCount(ItemType.MiniShield)),
            ItemType.Pistol => new ItemInfo("P250", Hex(0xb0b0b8), true, MapCount(ItemType.Pistol)),
            ItemType.Sniper => new ItemInfo("0.50 cal", Hex(0xff3030), true, MapCount(ItemType.Sniper)),
            _ => new ItemInfo("Jar of piss", Hex(0xe6c820), false, MapCount(ItemType.Piss)),
        };

        /// <summary>A slot's display name: a golden shotgun is the golden pump.</summary>
        public static string Name(ItemType t, bool golden) => t == ItemType.Shotgun && golden ? "Golden pump" : Info(t).name;

        /// <summary>Short slot label.</summary>
        public static string Short(ItemType t) => t switch
        {
            ItemType.Book => "Book",
            ItemType.Goggles => "Goggles",
            ItemType.DoctorPepper => "Dr Pepper",
            ItemType.Trap => "Gas trap",
            ItemType.Confit => "Confit",
            ItemType.MrBeastBar => "Beast bar",
            ItemType.Piss => "Piss",
            _ => Info(t).name,
        };

        public static bool IsWeapon(ItemType t) => Info(t).weapon;

        /// <summary>Items with a per-unit amount: each pair of goggles' meter, a weapon's rounds.</summary>
        public static bool UsesAmount(ItemType t) => t == ItemType.Goggles || IsWeapon(t);

        /// <summary>A fresh unit's amount (full goggles, a loaded weapon).</summary>
        public static float FreshAmount(ItemType t, bool golden = false) => t switch
        {
            ItemType.Goggles => Vision.Game.Balance.Items.Goggles.Meter,
            ItemType.Shotgun => golden ? Vision.Game.Balance.Items.Golden.Shells : Vision.Game.Balance.Items.Shotgun.Shells,
            ItemType.Pistol => Vision.Game.Balance.Items.Pistol.Shots,
            ItemType.Sniper => Vision.Game.Balance.Items.Sniper.Shots,
            _ => 0f,
        };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }

    /// <summary>
    /// The original's inventory: eight slots (twelve in testing mode). Identical items stack without limit; a weapon is
    /// always alone in its slot. Goggles and weapons keep a per-unit amount (meter seconds, rounds). With every slot full a new
    /// kind of item takes the last slot and what was there is dropped (<see cref="Dropped"/>). With <see cref="Infinite"/>
    /// (testing mode) using an item never uses it up.
    /// </summary>
    [Serializable]
    public sealed class Inventory
    {
        /// <summary>Slots in a real match; testing mode has <see cref="TestingSlots"/>.</summary>
        public const int Slots = 8;
        public const int TestingSlots = 12;

        public sealed class Slot
        {
            public ItemType item;
            public int count;
            public bool golden;
            /// <summary>Per unit: goggle meter seconds or rounds left (weapons and goggles only).</summary>
            public readonly List<float> amounts = new List<float>();

            public bool Empty => count <= 0;

            public void Clear()
            {
                count = 0;
                golden = false;
                amounts.Clear();
            }
        }

        readonly Slot[] slots = BuildSlots();

        static Slot[] BuildSlots()
        {
            var s = new Slot[TestingSlots];
            for (int i = 0; i < s.Length; i++) s[i] = new Slot();
            return s;
        }

        /// <summary>Testing mode: items are never used up, and there are twelve slots.</summary>
        public bool Infinite;

        /// <summary>How many slots are in use: eight, or twelve in testing mode.</summary>
        public int Limit => Infinite ? TestingSlots : Slots;

        public event Action Changed;

        /// <summary>Raised when a full inventory drops a whole stack to make room: item, count, golden, amounts.</summary>
        public event Action<ItemType, int, bool, List<float>> Dropped;

        public Slot SlotAt(int slot) => slot >= 0 && slot < TestingSlots ? slots[slot] : null;
        public ItemType? ItemAt(int slot) => slot >= 0 && slot < TestingSlots && slots[slot].count > 0 ? slots[slot].item : (ItemType?)null;
        public int CountAt(int slot) => slot >= 0 && slot < TestingSlots ? slots[slot].count : 0;
        public bool GoldenAt(int slot) => slot >= 0 && slot < TestingSlots && slots[slot].count > 0 && slots[slot].golden;

        /// <summary>The amount (rounds, goggle seconds) of the unit that will be used next in a slot.</summary>
        public float AmountAt(int slot)
        {
            Slot s = SlotAt(slot);
            return s != null && s.amounts.Count > 0 ? s.amounts[0] : 0f;
        }

        public void SetAmountAt(int slot, float value)
        {
            Slot s = SlotAt(slot);
            if (s == null || s.amounts.Count == 0) return;
            s.amounts[0] = value;
            Changed?.Invoke();
        }

        public int Count(ItemType item)
        {
            int n = 0;
            foreach (Slot s in slots) if (s.count > 0 && s.item == item) n += s.count;
            return n;
        }

        public int FirstSlotOf(ItemType item)
        {
            for (int i = 0; i < TestingSlots; i++) if (slots[i].count > 0 && slots[i].item == item) return i;
            return -1;
        }

        /// <summary>
        /// Adds <paramref name="count"/> items, as the original's addItem one at a time: onto a stack of the same kind, else into
        /// the first empty slot, else into the last slot after dropping what was there. Returns how many were added (all).
        /// </summary>
        public int Add(ItemType item, int count = 1, float? amount = null, bool golden = false)
        {
            for (int k = 0; k < count; k++) AddOne(item, amount ?? Items.FreshAmount(item, golden), golden);
            if (count > 0) Changed?.Invoke();
            return Mathf.Max(0, count);
        }

        void AddOne(ItemType item, float amount, bool golden)
        {
            bool weapon = Items.IsWeapon(item);
            if (!weapon)
                foreach (Slot s in slots)
                    if (s.count > 0 && s.item == item)
                    {
                        s.count++;
                        if (Items.UsesAmount(item)) s.amounts.Add(amount);
                        return;
                    }
            int lim = Limit, free = -1;
            for (int i = 0; i < lim; i++) if (slots[i].count == 0) { free = i; break; }
            if (free < 0)
            {
                free = lim - 1;
                Slot old = slots[free];
                Dropped?.Invoke(old.item, old.count, old.golden, new List<float>(old.amounts));
                old.Clear();
            }
            Slot t = slots[free];
            t.item = item;
            t.count = 1;
            t.golden = item == ItemType.Shotgun && golden;
            t.amounts.Clear();
            if (Items.UsesAmount(item)) t.amounts.Add(amount);
        }

        /// <summary>Uses up one unit of a slot (not in testing mode). False when the slot is empty.</summary>
        public bool Remove(int slot)
        {
            Slot s = SlotAt(slot);
            if (s == null || s.count <= 0) return false;
            if (!Infinite)
            {
                s.count--;
                if (s.amounts.Count > 0) s.amounts.RemoveAt(0);
                if (s.count <= 0) s.Clear();
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes one unit out of a slot without using it (dropping it): its amount, or -1 if the slot is empty.</summary>
        public float TakeOne(int slot, out ItemType item, out bool golden)
        {
            Slot s = SlotAt(slot);
            item = s != null ? s.item : default;
            golden = s != null && s.golden;
            if (s == null || s.count <= 0) return -1f;
            float amt = 0f;
            if (s.amounts.Count > 0)
            {
                amt = s.amounts[s.amounts.Count - 1];
                s.amounts.RemoveAt(s.amounts.Count - 1);
            }
            else if (Items.UsesAmount(s.item)) amt = Items.FreshAmount(s.item, s.golden);
            s.count--;
            if (s.count <= 0) s.Clear();
            Changed?.Invoke();
            return amt;
        }

        /// <summary>Swaps two slots (the Tab inventory editor).</summary>
        public void Move(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= TestingSlots || to >= TestingSlots) return;
            (slots[from], slots[to]) = (slots[to], slots[from]);
            Changed?.Invoke();
        }

        public void Clear()
        {
            foreach (Slot s in slots) s.Clear();
            Changed?.Invoke();
        }
    }
}
