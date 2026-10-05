using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// The player's <see cref="Vitals"/> and <see cref="Inventory"/>, ticked each frame by the controller, and what using
    /// a supply does. As the original: duck confit heals to full, a Mr Beast bar gives back a fifth of the health bar,
    /// and a mini shield is drunk over two seconds (moving spills it) for a quarter of a bar of shield. The other
    /// supplies are only collected for now.
    /// </summary>
    public sealed class PlayerStats : MonoBehaviour
    {
        public Vitals vitals = new Vitals();
        public Inventory inventory = new Inventory();

        public const float BeastBarHeal = 0.2f, ShieldAmount = 0.25f, ShieldDrinkTime = 2f;

        public enum UseResult { Used, Drinking, AlreadyFull, NotYet, Empty }

        /// <summary>Seconds left on the mini shield being drunk (0 when not drinking), and its slot.</summary>
        public float DrinkLeft { get; private set; }
        int drinkSlot = -1;

        /// <summary>Uses the item in a slot. A mini shield starts being drunk; <see cref="Tick"/> finishes it.</summary>
        public UseResult UseSlot(int slot)
        {
            ItemType? item = inventory.ItemAt(slot);
            if (item == null || vitals.IsDowned) return UseResult.Empty;
            switch (item.Value)
            {
                case ItemType.Confit:
                    if (vitals.Health >= 1f) return UseResult.AlreadyFull;
                    inventory.Remove(slot);
                    vitals.Heal(1f);
                    return UseResult.Used;
                case ItemType.MrBeastBar:
                    if (vitals.Health >= 1f) return UseResult.AlreadyFull;
                    inventory.Remove(slot);
                    vitals.Heal(BeastBarHeal);
                    return UseResult.Used;
                case ItemType.MiniShield:
                    if (vitals.Shield >= 1f) return UseResult.AlreadyFull;
                    if (DrinkLeft > 0f) return UseResult.Drinking;
                    DrinkLeft = ShieldDrinkTime;
                    drinkSlot = slot;
                    return UseResult.Drinking;
                default:
                    return UseResult.NotYet;
            }
        }

        /// <summary>Advances a drink: moving or being downed spills it. Returns true the frame it is finished.</summary>
        public bool Tick(float dt, bool moving)
        {
            if (DrinkLeft <= 0f) return false;
            if (moving || vitals.IsDowned || inventory.ItemAt(drinkSlot) != ItemType.MiniShield)
            {
                CancelDrink();
                return false;
            }
            DrinkLeft -= dt;
            if (DrinkLeft > 0f) return false;
            DrinkLeft = 0f;
            inventory.Remove(drinkSlot);
            vitals.AddShield(ShieldAmount);
            drinkSlot = -1;
            return true;
        }

        public void CancelDrink()
        {
            DrinkLeft = 0f;
            drinkSlot = -1;
        }
    }
}
