using System;
using UnityEngine;

namespace Vision.Player
{
    public enum ItemType { Bandage, Water, CannedFood }

    public readonly struct ItemInfo
    {
        public readonly string name;
        public readonly float heal, stamina;
        public readonly Color color;
        public readonly int stack;

        public ItemInfo(string name, float heal, float stamina, Color color, int stack)
        {
            this.name = name;
            this.heal = heal;
            this.stamina = stamina;
            this.color = color;
            this.stack = stack;
        }
    }

    public static class Items
    {
        public static ItemInfo Info(ItemType t) => t switch
        {
            ItemType.Bandage => new ItemInfo("Bandage", 35f, 0f, new Color(0.86f, 0.84f, 0.78f), 5),
            ItemType.Water => new ItemInfo("Water", 0f, 60f, new Color(0.45f, 0.62f, 0.72f), 5),
            _ => new ItemInfo("Canned food", 15f, 25f, new Color(0.62f, 0.60f, 0.55f), 5),
        };
    }

    /// <summary>Six slots; items of a kind stack up to their limit, then take a new slot.</summary>
    [Serializable]
    public sealed class Inventory
    {
        public const int Slots = 6;

        [Serializable]
        public struct Stack
        {
            public ItemType item;
            public int count;
        }

        [SerializeField] Stack[] slots = new Stack[Slots];

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
            int limit = Items.Info(item).stack, added = 0;
            for (int pass = 0; pass < 2 && added < count; pass++)
            {
                for (int i = 0; i < Slots && added < count; i++)
                {
                    bool fits = pass == 0 ? slots[i].count > 0 && slots[i].item == item : slots[i].count == 0;
                    if (!fits) continue;
                    if (pass == 1) slots[i].item = item;
                    int take = Mathf.Min(limit - slots[i].count, count - added);
                    slots[i].count += take;
                    added += take;
                }
            }
            if (added > 0) Changed?.Invoke();
            return added;
        }

        public bool Remove(int slot)
        {
            if (CountAt(slot) <= 0) return false;
            slots[slot].count--;
            Changed?.Invoke();
            return true;
        }
    }
}
