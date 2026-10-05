using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// A place to hide, as in the original: tall grass, a wardrobe, a bed, a locker or a barrel. Interact within
    /// reach to hide (the player stops and disappears from view) and again to leave, stepping out at the exit point.
    /// </summary>
    public sealed class HidingSpot : MonoBehaviour
    {
        public enum Kind { Grass, Wardrobe, Bed, Locker, Barrel }

        public Kind kind;
        [Tooltip("Design units: how close the player must be to hide here (tall grass: the patch radius).")]
        public float reach = 1.2f;
        [Tooltip("Where the player steps out, local to this object.")]
        public Vector3 exit = Vector3.forward;

        public static readonly List<HidingSpot> All = new List<HidingSpot>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public string Label => kind switch
        {
            Kind.Grass => "tall grass",
            Kind.Wardrobe => "wardrobe",
            Kind.Bed => "under the bed",
            Kind.Locker => "locker",
            _ => "barrel",
        };

        public Vector3 ExitPosition => transform.TransformPoint(exit);
    }
}
