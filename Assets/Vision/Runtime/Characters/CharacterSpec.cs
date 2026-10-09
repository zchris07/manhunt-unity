using System;
using UnityEngine;

namespace Vision.Characters
{
    public enum HairStyle { None, Buzz, Short, Long, Curly, Cap }

    [Flags]
    public enum Feature
    {
        None = 0,
        Glasses = 1 << 0,
        /// <summary>A white hockey mask with red chevrons and vent holes (Zach).</summary>
        HockeyMask = 1 << 1,
        /// <summary>A gaming headset: pink band, cyan-lit ear cups (Plasma).</summary>
        Headset = 1 << 2,
        /// <summary>A hood lying down behind the neck.</summary>
        Hood = 1 << 3,
        /// <summary>Hi-vis bands round the chest and forearms (Chris).</summary>
        HiVis = 1 << 4,
        /// <summary>A blue star of life on the back (Chris).</summary>
        StarOfLife = 1 << 5,
        /// <summary>A checked shirt: alternate facets a darker shade.</summary>
        Flannel = 1 << 6,
        /// <summary>A white band across the chest of a jersey.</summary>
        Jersey = 1 << 7,
        /// <summary>The shirt shows down the front of an open jacket.</summary>
        OpenJacket = 1 << 8,
        /// <summary>A work shirt hanging in ragged tails below the belt, sleeves torn off at the forearm (Zach).</summary>
        TornShirt = 1 << 9,
    }

    /// <summary>
    /// What a character looks like: body proportions (as multiples of the survivor's), colours by region (the original's
    /// look table: skin, hair, shirt, jacket, trousers, shoes) and a few shape features. Every character is built on the
    /// same 51-bone rig by <see cref="CharacterBuilder"/>, at most 500 triangles.
    /// </summary>
    public sealed class CharacterSpec
    {
        public string Name;
        /// <summary>Standing height in metres (the rig is 1.8 m; the root is scaled).</summary>
        public float Height = 1.8f;
        public float Shoulders = 1f, Chest = 1f, Waist = 1f, Hips = 1f, Arms = 1f, Legs = 1f, Neck = 1f, Head = 1f;
        /// <summary>Chest and shoulders pushed forward (metres): a heavy, hunched build.</summary>
        public float Hunch;
        public Color Skin, Hair, Shirt, Pants, Shoes;
        /// <summary>A jacket over the shirt (clear: none, the shirt has short sleeves).</summary>
        public Color Jacket = Color.clear;
        /// <summary>Gloves (clear: bare hands).</summary>
        public Color Gloves = Color.clear;
        public Color Accent = Color.white, Accent2 = Color.white;
        public HairStyle HairStyle;
        public Feature Features;

        public float RootScale => Height / HumanoidSkeleton.Height;
        public bool Has(Feature f) => (Features & f) != 0;

        static Color H(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        static Color G(float v) => new Color(v, v, v);

        /// <summary>The survivor: grey, tonal (darker trousers, gloves and soles), identical for everyone.</summary>
        public static CharacterSpec Survivor() => new CharacterSpec
        {
            Name = "Survivor",
            Skin = G(0.40f), Hair = G(0.30f), Shirt = G(0.34f), Pants = G(0.25f), Shoes = G(0.14f), Gloves = G(0.22f),
            HairStyle = HairStyle.None,
        };

        /// <summary>Zach Branch, exactly Jason Voorhees: hulking, 2 m, a white hockey mask, a torn dark work shirt, gloves.</summary>
        public static CharacterSpec Zach() => new CharacterSpec
        {
            Name = "Zach",
            Height = 2.0f,
            Shoulders = 1.32f, Chest = 1.35f, Waist = 1.25f, Hips = 1.12f, Arms = 1.5f, Legs = 1.4f, Neck = 1.6f, Head = 1.06f,
            Hunch = 0.035f,
            Skin = H(0xc9b99a), Hair = H(0x6a5e50), Shirt = H(0x3b3f2a), Jacket = H(0x2c3024), Pants = H(0x23262e), Shoes = H(0x1a1614),
            Gloves = H(0x3e3226), Accent = H(0xf2efe6), Accent2 = H(0xb3121b),
            HairStyle = HairStyle.Buzz,
            Features = Feature.HockeyMask | Feature.TornShirt,
        };

        /// <summary>The NPCs, from the original's look table (features to be fine-tuned).</summary>
        public static CharacterSpec Npc(string name)
        {
            switch (name)
            {
                case "Sexton Science":
                    return new CharacterSpec { Name = name, Skin = H(0xf4cfae), Hair = H(0xf5d86a), Shirt = H(0x2f86f0), Pants = H(0xcdb57c), Shoes = H(0x5a3a22), HairStyle = HairStyle.Short, Features = Feature.Glasses, Accent = H(0x1a1a1a) };
                case "Shane Jeans":
                    return new CharacterSpec { Name = name, Skin = H(0xe0b890), Hair = H(0x3a2a1c), Shirt = H(0xd8d0c0), Jacket = H(0x4a6a9a), Pants = H(0x3a5a8a), Shoes = H(0x2a2622), HairStyle = HairStyle.Long, Features = Feature.OpenJacket };
                case "Chris Zelley":
                    return new CharacterSpec { Name = name, Skin = H(0xd7a67c), Hair = H(0x2a1e16), Shirt = H(0xe8ecee), Jacket = H(0x2f7a4a), Pants = H(0x1f3a2a), Shoes = H(0x1a1a1a), HairStyle = HairStyle.Buzz, Features = Feature.HiVis | Feature.StarOfLife, Accent = H(0xd8e83a), Accent2 = H(0x2a5ad8) };
                case "Marc Cortez":
                    return new CharacterSpec { Name = name, Skin = H(0xc68a5e), Hair = H(0x1e1612), Shirt = H(0xe8e4dc), Jacket = H(0x9a2a24), Pants = H(0x3c4a6a), Shoes = H(0x4a3424), HairStyle = HairStyle.Short, Features = Feature.Flannel | Feature.OpenJacket };
                case "Plasma.TTV":
                    return new CharacterSpec { Name = name, Skin = H(0xe8c0a0), Hair = H(0x2a1e1a), Shirt = H(0x2a2a30), Jacket = H(0x1c1c22), Pants = H(0x26262c), Shoes = H(0xe8e8ec), HairStyle = HairStyle.Curly, Features = Feature.Headset | Feature.Hood, Accent = H(0xff4fb0), Accent2 = H(0x3af0ff) };
                case "Jaden Nguyen":
                    return new CharacterSpec { Name = name, Skin = H(0xd8b08a), Hair = H(0x1e1e22), Shirt = H(0xeeeeee), Jacket = H(0x4e5a36), Pants = H(0x2a2a30), Shoes = H(0xe8e8e8), HairStyle = HairStyle.Cap, Features = Feature.OpenJacket };
                case "Waz":
                    return new CharacterSpec { Name = name, Skin = H(0x8a5a38), Hair = H(0x0e0c0c), Shirt = H(0x2f8a3a), Pants = H(0x2e3442), Shoes = H(0x1c1c1e), HairStyle = HairStyle.Short };
                case "Chacko":
                    return new CharacterSpec { Name = name, Skin = H(0xb9825a), Hair = H(0x161212), Shirt = H(0x176a4a), Pants = H(0x2a2e3a), Shoes = H(0xd8d8d8), HairStyle = HairStyle.Short, Features = Feature.Jersey, Accent = H(0xf0f0f0) };
                case "Njaaron":
                    return new CharacterSpec { Name = name, Skin = H(0xa9714a), Hair = H(0x15110f), Shirt = H(0x1f3a78), Jacket = H(0x26468a), Pants = H(0x2c2f38), Shoes = H(0xf0f0f0), HairStyle = HairStyle.Short, Features = Feature.Hood };
                case "Monique Bourgeois":
                    return new CharacterSpec { Name = name, Height = 1.7f, Shoulders = 0.92f, Chest = 0.95f, Waist = 0.9f, Arms = 0.9f, Legs = 0.95f, Neck = 0.9f, Skin = H(0xefc9a6), Hair = H(0x7a3a1c), Shirt = H(0xf0e8d8), Pants = H(0x3a2a4a), Shoes = H(0x5a3a2a), HairStyle = HairStyle.Long };
                case "Thomas Bourgeois":
                    return new CharacterSpec { Name = name, Skin = H(0xe4b894), Hair = H(0x9a9a9a), Shirt = H(0xe8e4dc), Jacket = H(0xb88a2a), Pants = H(0x4a4a3a), Shoes = H(0x3a2a1c), HairStyle = HairStyle.Curly, Features = Feature.Glasses | Feature.OpenJacket, Accent = H(0x2a2a2a) };
                case "Soham":
                    return new CharacterSpec { Name = name, Skin = H(0xb9825a), Hair = H(0x161212), Shirt = H(0x2a2a2a), Jacket = H(0xc8322a), Pants = H(0x2a3a5a), Shoes = H(0xe8e8e8), HairStyle = HairStyle.Buzz, Features = Feature.Hood };
                default:
                    return Survivor();
            }
        }
    }
}
