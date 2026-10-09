using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// The inventory editor (Tab), as the original's: the slots become clickable; click one, then another, to swap them.
    /// While it is open the player keeps walking but doesn't use items. Clicking a slot outside the editor takes that item
    /// in hand.
    /// </summary>
    public sealed partial class GameHud
    {
        static bool editorOpen;
        int editorPick = -1;
        Text editorHint;

        /// <summary>True while the inventory editor is open (left clicks go to the slots, not to items).</summary>
        public static bool EditorOpen => editorOpen;
        public int EditorPick => editorPick;

        void BuildInventoryEditor()
        {
            for (int i = 0; i < Inventory.TestingSlots; i++)
            {
                int slot = i;
                Image back = slotBoxes[i].GetComponent<Image>();
                back.raycastTarget = true;
                var trigger = slotBoxes[i].AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                entry.callback.AddListener(_ => ClickSlot(slot));
                trigger.triggers.Add(entry);
            }
            editorHint = kit.Label(Node("Editor Hint", strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(700f, 22f)),
                "ARRANGE: CLICK A SLOT, THEN ANOTHER TO SWAP  ·  TAB TO CLOSE", 14, TextAnchor.MiddleCenter, UiKit.Yellow, UiKit.Face.Display, 0.12f);
            editorHint.gameObject.SetActive(false);
        }

        /// <summary>Opens or closes the editor (Tab).</summary>
        public void ToggleEditor(bool? open = null)
        {
            editorOpen = open ?? !editorOpen;
            editorPick = -1;
            if (editorHint != null) editorHint.gameObject.SetActive(editorOpen);
        }

        /// <summary>A click on a slot: in the editor, pick it or swap it with the picked one; otherwise take it in hand.</summary>
        public void ClickSlot(int slot)
        {
            if (player == null || player.Me == null || player.Me.Role != Role.Survivor) return;
            if (!editorOpen)
            {
                player.SelectedSlot = slot;
                return;
            }
            if (editorPick < 0) { editorPick = slot; return; }
            if (editorPick != slot)
            {
                MatchHost h = MatchHost.For(world);
                h?.Sim?.MoveSlot(h.LocalId, editorPick, slot);
                if (player.SelectedSlot == editorPick) player.SelectedSlot = slot;
                else if (player.SelectedSlot == slot) player.SelectedSlot = editorPick;
            }
            editorPick = -1;
        }

        void UpdateEditor(SimPlayer me)
        {
            Keyboard kb = Keyboard.current;
            bool survivor = me != null && me.Role == Role.Survivor && !MainMenuOpen && !escOpen;
            if (!survivor && editorOpen) ToggleEditor(false);
            if (survivor && kb != null && kb.tabKey.wasPressedThisFrame) ToggleEditor();
            if (!editorOpen) return;
            for (int i = 0; i < Inventory.TestingSlots; i++)
                if (i == editorPick) foreach (Image e in slotFrames[i]) e.color = UiKit.Yellow;
        }
    }
}
