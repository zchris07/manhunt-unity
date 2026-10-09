using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// Zach's HUD, as the original's: an ability bar in place of the inventory (machete or golden pump, lunge, Soundcloud
    /// Burst, Hemp Battery, Penjamin, Hemp Beam), each with its key, its icon, a cooldown shade, charges or a meter, and
    /// lit while active (a survivor's JARVIS and Hemp Beam sit beside the inventory the same way); his status lines (speed lost to his wounds, recovery, stunned, abilities off, the battery, the beam, gassed,
    /// stun immunity); and the generators he has heard being repaired.
    /// </summary>
    public sealed partial class GameHud
    {
        sealed class AbilitySlot
        {
            public GameObject Root;
            public Text Name, Count;
            public RawImage Icon;
            public RectTransform Shade, Meter;
            public Image[] Frame;
            public Image Back;
        }

        static readonly (string key, string name, string icon)[] HunterAbilities =
        {
            ("LMB", "Machete Swipe", "machete"), ("RMB", "Lunge", "lunge"), ("F", "Soundcloud Burst", "burst"), ("Q", "Hemp Battery", "hemp"),
            ("Space", "Penjamin", "vape"), ("R", "Hemp Beam", "hemp"),
        };

        RectTransform abilityStrip, survivorStrip;
        readonly AbilitySlot[] abilities = new AbilitySlot[6];
        // A survivor's JARVIS (Q) and the Hemp Beam Thomas gave them (R), beside the inventory as in the original.
        AbilitySlot jarvisSlot, beamSlot;
        Text hunterStatus, heard;

        /// <summary>An ability slot: its key cap, its icon, the count at the top right, the name, a cooldown shade and a meter.</summary>
        AbilitySlot MakeAbility(Transform parent, string key, string name, string icon, float x, float w)
        {
            RectTransform slot = Node(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w, 82f));
            var a = new AbilitySlot { Root = slot.gameObject };
            a.Back = UiKit.Box(slot, SlotColor);
            a.Frame = UiKit.Frame(slot, SlotBorder);
            a.Icon = Node("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(50f, 50f)).gameObject.AddComponent<RawImage>();
            a.Icon.raycastTarget = false;
            a.Icon.texture = ItemIcons.Get(icon);
            // The cooldown shade grows down from the top, over the icon.
            a.Shade = Node("Cooldown", slot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(w, 0f));
            UiKit.Box(a.Shade, new Color(0f, 0f, 0f, 0.62f));
            RectTransform cap = Node("Key Cap", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -4f), new Vector2(40f, 16f));
            UiKit.Box(cap, UiKit.Bone);
            Text kt = kit.Label(Node("Key", cap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 16f)), key, 12, TextAnchor.MiddleCenter, UiKit.Hex(0x0a0a08), UiKit.Face.Display, 0.06f, false);
            cap.sizeDelta = new Vector2(Mathf.Max(16f, kt.preferredWidth + 10f), 16f);
            a.Name = kit.Label(Node("Name", slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(w - 6f, 16f)), name.ToUpperInvariant(), 11, TextAnchor.LowerCenter, UiKit.Muted, UiKit.Face.Display, 0.06f);
            a.Count = kit.Label(Node("Count", slot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -3f), new Vector2(w - 50f, 18f)), "", 14, TextAnchor.UpperRight, UiKit.Yellow, UiKit.Face.Type, 0f);
            RectTransform meterBack = Node("Meter", slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(w - 12f, 4f));
            UiKit.Box(meterBack, new Color(0.04f, 0.04f, 0.035f, 0.95f));
            a.Meter = UiKit.Fill("Fill", meterBack);
            UiKit.Box(a.Meter, UiKit.Bone);
            return a;
        }

        void BuildHunterHud(Transform parent)
        {
            const float w = 124f, gap = 6f;
            abilityStrip = Node("Abilities", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(6 * (w + gap) - gap, 86f));
            for (int i = 0; i < 6; i++) abilities[i] = MakeAbility(abilityStrip, HunterAbilities[i].key, HunterAbilities[i].name, HunterAbilities[i].icon, i * (w + gap), w);
            abilityStrip.gameObject.SetActive(false);
            survivorStrip = Node("Survivor Abilities", parent, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 22f), new Vector2(2 * 82f + 14f, 86f));
            jarvisSlot = MakeAbility(survivorStrip, "Q", "JARVIS", "tablet", 14f, 76f);
            beamSlot = MakeAbility(survivorStrip, "R", "Hemp Beam", "leaf", 14f + 82f, 76f);
            survivorStrip.gameObject.SetActive(false);
            hunterStatus = kit.Label(Node("Zach Status", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 170f), new Vector2(520f, 160f)), "", 15, TextAnchor.LowerLeft, UiKit.Bone);
            hunterStatus.lineSpacing = 1.25f;
            heard = kit.Label(Node("Heard", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -96f), new Vector2(520f, 22f)), "", 14, TextAnchor.MiddleLeft, UiKit.Muted);
        }

        static void SetAbility(AbilitySlot a, bool on, string count, float cd, float? meter, bool active, bool hidden = false, Color? tint = null)
        {
            a.Root.SetActive(!hidden);
            if (hidden) return;
            a.Count.text = count;
            a.Count.color = on ? (tint ?? UiKit.Yellow) : UiKit.Muted;
            if (a.Icon != null) a.Icon.color = on ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.6f);
            a.Name.color = on ? UiKit.Bone : UiKit.Muted;
            float h = Mathf.Clamp01(cd) * 82f;
            a.Shade.sizeDelta = new Vector2(a.Shade.sizeDelta.x, h);
            a.Meter.parent.gameObject.SetActive(meter.HasValue);
            if (meter.HasValue) a.Meter.anchorMax = new Vector2(Mathf.Clamp01(meter.Value), 1f);
            foreach (Image e in a.Frame) e.color = active ? SlotBorderSelected : SlotBorder;
            a.Back.color = active ? new Color(0.16f, 0.05f, 0.05f, 0.85f) : SlotColor;
        }

        static string Dots(int on, int of) => new string('●', Mathf.Max(0, on)) + new string('○', Mathf.Max(0, of - on));

        /// <summary>A survivor's JARVIS and Hemp Beam slots, to the right of the inventory (hidden until they have them).</summary>
        void RefreshSurvivorAbilities(SimPlayer me, float stripWidth)
        {
            bool survivor = me.Role == Role.Survivor && !MainMenuOpen;
            bool any = survivor && (me.Jarvis != 0 || me.BeamCharges > 0 || me.BeamT > 0f);
            survivorStrip.gameObject.SetActive(any);
            if (!any) return;
            survivorStrip.anchoredPosition = new Vector2(stripWidth * 0.5f, 22f);
            int j = me.Jarvis;
            SetAbility(jarvisSlot, j == 1 || j == 3, j == 3 ? "∞" : j == 2 ? "used" : "", 0f, null, me.JarvisT > 0f, j == 0);
            SetAbility(beamSlot, me.BeamCharges > 0 && me.BeamCd <= 0f && me.BeamT <= 0f,
                me.BeamT > 0f ? $"{me.BeamT:0.0}s" : me.BeamCd > 0f ? $"{Mathf.CeilToInt(me.BeamCd)}s" : me.BeamCharges.ToString(),
                me.BeamCd / Balance.Hunter.Beam.Cooldown, null, me.BeamT > 0f, me.BeamCharges <= 0 && me.BeamT <= 0f, new Color(0.7f, 1f, 0.72f));
        }

        void RefreshHunter(SimPlayer me, MatchSim sim)
        {
            bool zach = me.Role == Role.Hunter;
            abilityStrip.gameObject.SetActive(zach && !MainMenuOpen);
            hunterStatus.gameObject.SetActive(zach);
            heard.gameObject.SetActive(zach);
            if (!zach) return;
            bool test = sim.TestMode, locked = me.AbilityLockT > 0f;

            // Machete (or the golden pump while he has shots).
            AbilitySlot m0 = abilities[0];
            bool pump = me.Pump > 0;
            m0.Name.text = pump ? "GOLDEN PUMP" : "MACHETE SWIPE";
            m0.Icon.texture = ItemIcons.Get(pump ? "goldenPump" : "machete");
            if (pump) SetAbility(m0, me.ReloadT <= 0f, test ? "∞" : me.Pump.ToString(), me.ReloadT / Balance.Items.ZachPump.Reload, (float)me.Pump / Balance.Items.ZachPump.Shots, false, false, new Color(1f, 0.76f, 0.23f));
            else
            {
                float? charge = me.ChargeT >= 0f ? me.ChargeT / Balance.Hunter.Attack.ChargeMax : (float?)null;
                bool heavy = me.ChargeT >= Balance.Hunter.Attack.HeavyAt;
                SetAbility(m0, true, heavy ? "HEAVY" : "hold", 0f, charge, charge.HasValue, false, heavy ? new Color(0.9f, 0.25f, 0.2f) : (Color?)null);
            }
            int maxLunge = Balance.Hunter.Lunge.Charges + me.JadenBonus * Balance.Hunter.JadenSlainLunge;
            SetAbility(abilities[1], me.Move.LungeCharges > 0 && !locked, Dots(me.Move.LungeCharges, maxLunge), me.Move.LungeCharges < maxLunge ? me.Move.LungeRecharge / Balance.Hunter.Lunge.Recharge : 0f, null, me.Move.LungeT > 0f);
            SetAbility(abilities[2], me.BurstCd <= 0f && !locked, me.BurstCd > 0f ? $"{Mathf.CeilToInt(me.BurstCd)}s" : "", me.BurstCd / Balance.Hunter.Burst.Cooldown, null, false);
            bool infinite = me.Hemp == 2;
            SetAbility(abilities[3], !locked && me.HempLock <= 0f && (infinite || me.HempLeft > 0f) && me.Hemp > 0,
                me.Hemp == 0 ? "—" : infinite ? "∞" : me.HempLock > 0f ? $"{Mathf.CeilToInt(me.HempLock)}s" : $"{Mathf.CeilToInt(me.HempLeft)}s",
                0f, me.Hemp == 0 ? 0f : infinite ? 1f : me.HempLeft / Balance.Hunter.Hemp.Duration, me.HempOn, false, new Color(0.43f, 1f, 0.42f));
            SetAbility(abilities[4], me.VapeCharges > 0 && !locked, Dots(me.VapeCharges, Balance.Hunter.Vape.Charges), me.VapeCharges < Balance.Hunter.Vape.Charges ? me.VapeCd / Balance.Hunter.Vape.Cooldown : 0f, null, false, false, me.Nic ? new Color(0.45f, 0.74f, 0.94f) : (Color?)null);
            abilities[4].Name.text = me.Nic ? "50 NIC" : "PENJAMIN";
            abilities[4].Icon.texture = ItemIcons.Get(me.Nic ? "nic" : "vape");
            SetAbility(abilities[5], me.BeamCharges > 0 && me.BeamCd <= 0f && me.BeamT <= 0f && !locked,
                me.BeamT > 0f ? $"{me.BeamT:0.0}s" : me.BeamCd > 0f ? $"{Mathf.CeilToInt(me.BeamCd)}s" : me.BeamCharges.ToString(),
                me.BeamCd / Balance.Hunter.Beam.Cooldown, null, me.BeamT > 0f, me.BeamCharges <= 0 && me.BeamT <= 0f, new Color(0.7f, 1f, 0.72f));

            // Status lines.
            var sb = new System.Text.StringBuilder();
            float slow = (1f - MatchRules.HunterHealthMul(me.Hp, me.Downs)) * 100f;
            if (me.Carrying != 0) sb.Append("Carrying ").Append(sim.Get(me.Carrying)?.Name ?? "someone").Append('\n');
            if (me.KnockT <= 0f) sb.Append($"<color=#8a8574>Speed -{slow:0.0}% from health{(me.Downs > 0 ? $" ({Mathf.Min(Balance.Hunter.Health.DownPenaltyMax, me.Downs * Balance.Hunter.Health.DownPenalty) * 100f:0}% for good)" : "")}</color>\n");
            if (me.Hemp > 0)
            {
                float c = me.Hemp == 2 ? 1f : me.HempLeft / Balance.Hunter.Hemp.Duration;
                sb.Append($"<color=#8fae7a>Recovery x{MatchRules.HempRegenMul(c):0.000} (hemp {c * 100f:0}%)</color>\n");
            }
            if (me.KnockT > 0f) sb.Append($"<color=#d05a4a>DOWN: getting back up {me.KnockT:0.0}s</color>\n");
            if (me.StunT > 0f) sb.Append($"<color=#d05a4a>STUNNED {me.StunT:0.0}s</color>\n");
            if (me.PissT > 0f) sb.Append($"<color=#d05a4a>SOAKED IN PISS: +50% damage {me.PissT:0.0}s</color>\n");
            if (locked) sb.Append($"<color=#d05a4a>ABILITIES OFF {me.AbilityLockT:0.0}s</color>\n");
            if (me.HempOn) sb.Append($"<color=#8fae7a>HEMP BATTERY {(test && me.Hemp == 2 ? "∞" : $"{me.HempLeft:0.0}s")}</color>\n");
            if (me.BeamT > 0f) sb.Append($"<color=#8fae7a>HEMP BEAM {(me.BeamT > Balance.Sexton.Defense.BeamTime ? "CHARGING" : $"{me.BeamT:0.0}s")}</color>\n");
            if (me.Gassed) sb.Append("<color=#a888d8>IN GALAXY GAS: slowed</color>\n");
            if (me.ImmuneT > 0f && me.StunT <= 0f) sb.Append($"<color=#8a8574>Stun immune {me.ImmuneT:0.0}s</color>\n");
            hunterStatus.text = sb.ToString().TrimEnd('\n');

            // The generators he has heard being worked on (within earshot), as the original lists them.
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < sim.Gens.Length; i++)
            {
                GenState g = sim.Gens[i];
                if (g.Repaired || g.Progress <= 0.01f) continue;
                if (Vector2.Distance(sim.Map.Generators[i], me.Pos) > Scale.D(Balance.Hunter.GenKnownRadius)) continue;
                parts.Add($"{Mathf.RoundToInt(g.Progress * 100f)}%");
            }
            heard.text = parts.Count > 0 ? "Heard repairs: " + string.Join(" ", parts) : "";
        }
    }
}
