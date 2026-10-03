using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.World
{
    /// <summary>A supply lying in the world; the player takes it with Interact when close.</summary>
    public sealed class Pickup : MonoBehaviour
    {
        public ItemType item;
        public int count = 1;

        public static readonly List<Pickup> All = new List<Pickup>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public string Label => count > 1 ? $"{Items.Info(item).name} x{count}" : Items.Info(item).name;

        /// <summary>Moves what fits into the inventory; the pickup disappears once empty. Returns how many were taken.</summary>
        public int TakeInto(Inventory inventory)
        {
            int taken = inventory.Add(item, count);
            count -= taken;
            if (count <= 0)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
            return taken;
        }
    }
}
