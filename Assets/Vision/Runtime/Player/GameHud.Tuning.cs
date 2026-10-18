using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// The lobby's balance and difficulty sliders (<see cref="Tuning"/>): generators, each side's speed, sprint and sprint
    /// recovery, the damage each side deals, each gun's damage and starting rounds. Anyone in the lobby (host or joiner)
    /// moves them and everyone sees them move; a number off its default shows in amber. Only the host can put them all back
    /// to the defaults, and only the host can lock them, so that nobody else can move them until they're unlocked.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>The lobby's three cards side by side: players, balance, settings.</summary>
        const float LobbyWidth = 1760f, LobbyGap = 20f, TuningWidth = 600f;

        Slider[] tuneSliders;
        Text[] tuneValues;
        float[] tuneTouched;
        Button tuneReset, tuneLock;
        Text tuneNote;

        /// <summary>The balance sliders' current values on screen (tests).</summary>
        public float TuningShown(int knob) => tuneSliders != null && knob >= 0 && knob < tuneSliders.Length ? tuneSliders[knob].value : float.NaN;
        public bool TuningResetInteractable => tuneReset != null && tuneReset.interactable;
        /// <summary>Whether this player can move the sliders on screen, and the lock button's state (tests).</summary>
        public bool TuningSlidersInteractable => tuneSliders != null && tuneSliders.Length > 0 && tuneSliders[0].interactable;
        public bool TuningLockInteractable => tuneLock != null && tuneLock.interactable;
        public string TuningLockText => tuneLock != null ? tuneLock.GetComponentInChildren<Text>().text : null;
        /// <summary>The host's lock button, as pressed (tests and captures).</summary>
        public void PressTuningLock() => tuneLock?.onClick.Invoke();

        void BuildTuning(RectTransform grid, float x, float height)
        {
            RectTransform card = kit.Card("Balance", grid, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(TuningWidth, height));
            kit.FieldLabel(card, "Balance & difficulty", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), 300f);
            tuneNote = kit.Label(Node("Note", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -44f), new Vector2(TuningWidth - 56f, 18f)),
                "Anyone can move these; everyone sees them.", 12, TextAnchor.MiddleLeft, UiKit.Muted, UiKit.Face.Mono, 0f, false);
            int n = Tuning.Knobs.Length;
            tuneSliders = new Slider[n];
            tuneValues = new Text[n];
            tuneTouched = new float[n];
            for (int i = 0; i < n; i++) tuneTouched[i] = -999f;
            const float top = -72f, row = 33f;
            for (int i = 0; i < n; i++)
            {
                Tuning.Knob k = Tuning.Knobs[i];
                int index = i;
                float y = top - i * row;
                kit.Label(Node(k.Label, card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(250f, 26f)), k.Label, 14, TextAnchor.MiddleLeft, UiKit.Bone, UiKit.Face.Mono, 0f, false);
                tuneSliders[i] = kit.Slider(card, k.Label + " Slider", new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(286f, y - 13f), 200f, k.Min, k.Max, k.Default, v =>
                {
                    float snapped = Tuning.Snap(Tuning.Knobs[index], v);
                    if (!Mathf.Approximately(snapped, v)) tuneSliders[index].SetValueWithoutNotify(snapped);
                    tuneTouched[index] = Time.unscaledTime;
                    tuneValues[index].text = Tuning.Knobs[index].Format(snapped);
                    session?.Tune(index, snapped);
                });
                // A slimmer handle than the menus' sliders: seventeen of them stack up.
                tuneSliders[i].handleRect.sizeDelta = new Vector2(8f, 16f);
                tuneValues[i] = kit.Label(Node("Value", card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, y), new Vector2(76f, 26f)), k.Format(k.Default), 15, TextAnchor.MiddleRight, UiKit.Bone, UiKit.Face.DisplayBold, 0f, false);
            }
            const float half = (TuningWidth - 56f - 12f) / 2f;
            tuneReset = kit.Button(card, "Reset to defaults", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 20f), new Vector2(half, 40f), UiKit.ButtonStyle.Secondary, () =>
            {
                if (session != null && session.IsHost) session.ResetTuning();
            }, 15);
            // The host's lock: while it's on, only the host moves the sliders.
            tuneLock = kit.Button(card, "Lock sliders", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f + half + 12f, 20f), new Vector2(half, 40f), UiKit.ButtonStyle.Normal, () =>
            {
                if (session != null && session.IsHost && session.Lobby != null) session.LockTuning(!session.Lobby.Settings.TuningLocked);
            }, 15);
        }

        /// <summary>Puts the lobby's balance sliders on screen (leaving alone any this player is moving right now).</summary>
        void RefreshTuning(Tuning t)
        {
            if (tuneSliders == null || t == null) return;
            var defaults = new Tuning();
            float now = Time.unscaledTime;
            for (int i = 0; i < tuneSliders.Length; i++)
            {
                Tuning.Knob k = Tuning.Knobs[i];
                float v = k.Get(t);
                bool moving = now - tuneTouched[i] < 0.6f;
                if (!moving)
                {
                    tuneSliders[i].SetValueWithoutNotify(v);
                    tuneValues[i].text = k.Format(v);
                }
                bool changed = Mathf.Abs(v - k.Get(defaults)) > 1e-4f;
                tuneValues[i].color = changed ? UiKit.Hex(0xe0b050) : UiKit.Bone;
            }
            bool host = session != null && session.IsHost;
            bool locked = session != null && session.Lobby != null && session.Lobby.Settings.TuningLocked;
            bool mine = host || !locked;
            foreach (Slider s in tuneSliders) if (s.interactable != mine) s.interactable = mine;
            tuneReset.interactable = host && !t.IsDefault;
            tuneReset.GetComponentInChildren<Text>().text = host ? "Reset to defaults" : "Reset (host only)";
            tuneLock.interactable = host;
            tuneLock.GetComponentInChildren<Text>().text = locked ? (host ? "Unlock sliders" : "Locked by the host") : host ? "Lock sliders" : "Lock (host only)";
            tuneNote.text = locked ? (host ? "Locked: only you can move these; everyone sees them." : "The host has locked these: only they can move them.")
                : t.IsDefault ? "Anyone can move these; everyone sees them." : "Changed from the defaults (in amber); anyone can move them.";
        }
    }
}
