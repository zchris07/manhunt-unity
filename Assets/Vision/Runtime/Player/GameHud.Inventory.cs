using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// The inventory bar and its editor. Slots are perfect squares while empty; an item in one widens it smoothly into
    /// a rectangle that fits its picture and name, and it shrinks back the same way when the item goes (or changes to
    /// fit whatever is swapped in), never smaller than the square. Holding Tab opens the full map and the editor:
    /// drag an item onto another slot to swap them, or off the bar entirely to drop its whole stack on the ground.
    /// While Tab is held the player keeps walking but doesn't use items. Clicking a slot takes that item in hand.
    /// </summary>
    public sealed partial class GameHud
    {
        static bool editorOpen;
        Text editorHint;
        bool tabWas;

        /// <summary>A slot's side when empty, the gap between slots, and how fast slots grow and shrink.</summary>
        public const float SlotSize = 80f, SlotGap = 6f, SlotGrow = 13f;
        readonly float[] slotWidths = new float[Inventory.TestingSlots];

        // Dragging: which slot, and the picture following the pointer.
        int dragFrom = -1;
        RectTransform dragGhost;
        RawImage dragIcon;

        /// <summary>True while the inventory editor is open (left clicks go to the slots, not to items).</summary>
        public static bool EditorOpen => editorOpen;
        /// <summary>The slot being dragged (-1 none).</summary>
        public int Dragging => dragFrom;
        /// <summary>A slot's width as drawn now (tests, captures).</summary>
        public float SlotWidth(int i) => slotWidths[i];

        void BuildInventoryEditor()
        {
            for (int i = 0; i < Inventory.TestingSlots; i++)
            {
                int slot = i;
                Image back = slotBoxes[i].GetComponent<Image>();
                back.raycastTarget = true;
                var trigger = slotBoxes[i].AddComponent<EventTrigger>();
                void On(EventTriggerType type, System.Action<PointerEventData> act)
                {
                    var entry = new EventTrigger.Entry { eventID = type };
                    entry.callback.AddListener(d => act((PointerEventData)d));
                    trigger.triggers.Add(entry);
                }
                On(EventTriggerType.PointerDown, _ => ClickSlot(slot));
                On(EventTriggerType.BeginDrag, d => BeginSlotDrag(slot, d));
                On(EventTriggerType.Drag, d => MoveGhost(d.position));
                On(EventTriggerType.EndDrag, d => EndSlotDrag(d.position, d.pressEventCamera));
                slotWidths[i] = SlotSize;
            }
            editorHint = kit.Label(Node("Editor Hint", strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(760f, 22f)),
                "DRAG TO REARRANGE  ·  DRAG OFF THE BAR TO DROP THE STACK  ·  RELEASE TAB TO CLOSE", 14, TextAnchor.MiddleCenter, UiKit.Yellow, UiKit.Face.Display, 0.12f);
            editorHint.gameObject.SetActive(false);

            dragGhost = Node("Dragging", strip, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            var group = dragGhost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.9f;
            dragIcon = dragGhost.gameObject.AddComponent<RawImage>();
            dragIcon.raycastTarget = false;
            dragGhost.gameObject.SetActive(false);
        }

        /// <summary>Opens or closes the editor (held Tab; captures open it directly).</summary>
        public void ToggleEditor(bool? open = null)
        {
            editorOpen = open ?? !editorOpen;
            if (!editorOpen) CancelDrag();
            if (editorHint != null) editorHint.gameObject.SetActive(editorOpen);
        }

        /// <summary>A click on a slot takes that item in hand.</summary>
        public void ClickSlot(int slot)
        {
            if (player == null || player.Me == null || player.Me.Role != Role.Survivor) return;
            player.SelectedSlot = slot;
        }

        void BeginSlotDrag(int slot, PointerEventData d)
        {
            SimPlayer me = player != null ? player.Me : null;
            if (!editorOpen || me == null || me.Role != Role.Survivor || me.Inv.CountAt(slot) <= 0) return;
            dragFrom = slot;
            ItemType? item = me.Inv.ItemAt(slot);
            dragIcon.texture = item.HasValue ? ItemIcons.For(item.Value, me.Inv.GoldenAt(slot)) : null;
            dragGhost.gameObject.SetActive(true);
            dragGhost.SetAsLastSibling();
            MoveGhost(d.position);
        }

        void MoveGhost(Vector2 screen)
        {
            if (dragFrom < 0) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(strip, screen, null, out Vector2 local))
                dragGhost.anchoredPosition = local - strip.rect.min;
        }

        /// <summary>Dropped on a slot: swap; dropped off the bar: the whole stack goes on the ground.</summary>
        void EndSlotDrag(Vector2 screen, Camera cam)
        {
            int from = dragFrom;
            CancelDrag();
            SimPlayer me = player != null ? player.Me : null;
            if (from < 0 || me == null || !editorOpen) return;
            MatchHost h = MatchHost.For(world);
            int lim = me.Inv.Limit;
            for (int j = 0; j < lim; j++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slotBoxes[j].transform, screen, cam)) continue;
                if (j != from)
                {
                    h?.MoveSlot(from, j);
                    if (player.SelectedSlot == from) player.SelectedSlot = j;
                    else if (player.SelectedSlot == j) player.SelectedSlot = from;
                }
                return;
            }
            // Off the bar (with a little margin round it): drop the stack.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(strip, screen, cam, out Vector2 local))
            {
                Rect r = strip.rect;
                r.xMin -= 24f; r.xMax += 24f; r.yMin -= 24f; r.yMax += 24f;
                if (r.Contains(local)) return;
            }
            h?.DropSlot(from);
        }

        void CancelDrag()
        {
            dragFrom = -1;
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        /// <summary>Holding Tab: the full map (for anyone) and, for survivors, the inventory editor.</summary>
        void UpdateEditor(SimPlayer me)
        {
            Keyboard kb = Keyboard.current;
            bool survivor = me != null && me.Role == Role.Survivor && !MainMenuOpen && !escOpen;
            bool tab = kb != null && kb.tabKey.isPressed && !MainMenuOpen && !escOpen;
            if (tab != tabWas)
            {
                tabWas = tab;
                map.SetOpen(tab);
                ToggleEditor(tab && survivor);
            }
            if (!survivor && editorOpen) ToggleEditor(false);
        }

        /// <summary>Lays the slots out: squares when empty, widened to fit an item's picture and name, eased both ways.</summary>
        void LayoutSlots(Inventory inv, int limit)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f), k = 1f - Mathf.Exp(-SlotGrow * dt);
            float x = 0f;
            for (int i = 0; i < limit; i++)
            {
                bool filled = inv.CountAt(i) > 0;
                float want = SlotSize;
                if (filled) want = Mathf.Max(SlotSize, slotNames[i].preferredWidth + 20f, 64f);
                slotWidths[i] += (want - slotWidths[i]) * k;
                if (Mathf.Abs(want - slotWidths[i]) < 0.25f) slotWidths[i] = want;
                var rt = (RectTransform)slotBoxes[i].transform;
                rt.anchoredPosition = new Vector2(x, 0f);
                rt.sizeDelta = new Vector2(slotWidths[i], SlotSize);
                x += slotWidths[i] + SlotGap;
            }
            strip.sizeDelta = new Vector2(Mathf.Max(SlotSize, x - SlotGap), 86f);
        }
    }
}
