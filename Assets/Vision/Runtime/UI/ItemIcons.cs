using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.UI
{
    /// <summary>
    /// The HUD's item and ability icons (Resources/Icons): the original's own item art, rendered from its canvas drawing
    /// code (tools/icons), plus icons in the same style for the abilities the original leaves blank (lunge, the
    /// Soundcloud Burst, Penjamin and 50 Nic).
    /// </summary>
    public static class ItemIcons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>An icon by name (bottle, machete, lunge, ...); null when it is missing.</summary>
        public static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out Texture2D t) && t != null) return t;
            t = Resources.Load<Texture2D>("Icons/" + name);
            cache[name] = t;
            return t;
        }

        /// <summary>The icon name for an item (the golden pump has its own).</summary>
        public static string Name(ItemType item, bool golden) => item switch
        {
            ItemType.Bottle => "bottle",
            ItemType.Book => "book",
            ItemType.Goggles => "goggles",
            ItemType.Shotgun => golden ? "goldenPump" : "shotgun",
            ItemType.DoctorPepper => "energy",
            ItemType.Trap => "trap",
            ItemType.Confit => "confit",
            ItemType.MrBeastBar => "beastBar",
            ItemType.MiniShield => "shield",
            ItemType.Pistol => "pistol",
            ItemType.Sniper => "sniper",
            ItemType.Piss => "piss",
            _ => null,
        };

        public static Texture2D For(ItemType item, bool golden) => Get(Name(item, golden));

        /// <summary>Every icon name, for the checks.</summary>
        public static readonly string[] All =
        {
            "bottle", "book", "goggles", "shotgun", "goldenPump", "energy", "trap", "confit", "beastBar", "shield", "pistol", "sniper",
            "piss", "tablet", "hemp", "leaf", "machete", "lunge", "burst", "vape", "nic",
        };
    }
}
