using UnityEngine;

namespace Vision.Player
{
    /// <summary>The player's <see cref="Vitals"/> and <see cref="Inventory"/>, ticked each frame by the controller.</summary>
    public sealed class PlayerStats : MonoBehaviour
    {
        public Vitals vitals = new Vitals();
        public Inventory inventory = new Inventory();

        /// <summary>Uses the item in a slot and applies its effect. False when the slot is empty or the item would do nothing.</summary>
        public bool UseSlot(int slot)
        {
            ItemType? item = inventory.ItemAt(slot);
            if (item == null || vitals.IsDead) return false;
            ItemInfo info = Items.Info(item.Value);
            bool useful = (info.heal > 0f && vitals.Health < vitals.maxHealth) || (info.stamina > 0f && vitals.Stamina < vitals.maxStamina);
            if (!useful) return false;
            inventory.Remove(slot);
            vitals.Heal(info.heal);
            vitals.RestoreStamina(info.stamina);
            return true;
        }
    }
}
