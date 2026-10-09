using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// A window in a wall: the glass stops you but not your sight; boarded ones stop sight too. Zach's swipe, a shotgun
    /// pellet or a 0.50 cal smashes it: the glass (and boards) are gone and anyone can climb through the frame, slowly.
    /// </summary>
    public sealed class WindowPiece : MonoBehaviour
    {
        /// <summary>The opening from A to B (design units on the level's ground plane).</summary>
        public Vector2 a, b;
        public GameObject pane;
        public Collider glass;
        public Collider sill;
        public GameObject boards;
        public Occluder boardsOccluder;

        public bool Broken { get; private set; }

        public void SetBroken(bool broken)
        {
            if (Broken == broken) return;
            Broken = broken;
            if (pane != null) pane.SetActive(!broken);
            if (glass != null) glass.enabled = !broken;
            if (sill != null) sill.enabled = !broken;
            if (boards != null) boards.SetActive(!broken);
            if (boardsOccluder != null) boardsOccluder.Blocking = !broken;
        }
    }
}
