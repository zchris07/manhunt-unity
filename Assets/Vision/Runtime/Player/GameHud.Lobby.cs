using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.Net;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// The original's lobby screen: the room code (this machine's IP:port) to give the others, the players with their
    /// roles and readiness, your role preference and Ready, and the host's match settings (hunters, survivors, seed,
    /// testing mode) with a preview of the night, Shuffle roles and Start the night; a chat beside them.
    /// </summary>
    public sealed partial class GameHud
    {
        sealed class LobbyRow
        {
            public RectTransform Root;
            public Text Name, Role, Ready, Ping;
            public Button RoleButton;
        }

        GameObject lobbyScreen;
        static bool lobbyOpen;
        Text lobbyCode, lobbyError, lobbyPreview, lobbyNote, chatLog, huntersValue, survivorsValue, testLabel;
        RectTransform lobbyList;
        readonly List<LobbyRow> lobbyRows = new List<LobbyRow>();
        readonly List<string> chatLines = new List<string>();
        Button readyButton, startButton, shuffleButton, testButton, huntersMinus, huntersPlus, survivorsMinus, survivorsPlus;
        Button[] prefButtons;
        InputField seedField, chatField;
        NetSession session, joining;
        string lastFailure;

        void BuildLobby(Transform parent)
        {
            RectTransform screen = UiKit.Fill("Lobby", parent);
            var back = screen.gameObject.AddComponent<RawImage>();
            back.texture = UiKit.Backdrop();
            back.raycastTarget = true;
            RectTransform grid = Node("Grid", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 720f));

            // Left: the room and the players.
            const float lw = 780f, lh = 720f;
            RectTransform left = kit.Card("Players", grid, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(lw, lh));
            kit.FieldLabel(left, "Room code", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), 300f);
            lobbyCode = kit.Label(Node("Code", left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -50f), new Vector2(460f, 46f)), "", 34, TextAnchor.MiddleLeft, UiKit.Bone, UiKit.Face.DisplayBold, 0.08f, false);
            kit.Button(left, "Copy", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -52f), new Vector2(120f, 40f), UiKit.ButtonStyle.Normal, () =>
            {
                GUIUtility.systemCopyBuffer = session != null ? session.RoomCode : "";
                Notify("Room code copied");
            }, 17);
            kit.Label(Node("Hint", left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -100f), new Vector2(lw - 56f, 20f)),
                "Others join with this IP:port (over the internet the host forwards the port, or uses a VPN).", 13, TextAnchor.MiddleLeft, UiKit.Muted, UiKit.Face.Mono, 0f, false);
            lobbyList = Node("List", left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -132f), new Vector2(lw - 56f, 400f));
            kit.FieldLabel(left, "Role", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -548f), 200f);
            prefButtons = new Button[3];
            string[] prefs = { "Survivor", "Zach (hunter)", "Either" };
            RolePref[] values = { RolePref.Survivor, RolePref.Hunter, RolePref.Any };
            for (int i = 0; i < 3; i++)
            {
                RolePref v = values[i];
                prefButtons[i] = kit.Button(left, prefs[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f + i * 170f, -574f), new Vector2(160f, 44f), UiKit.ButtonStyle.Normal, () => session?.SetPref(v), 17);
            }
            readyButton = kit.Button(left, "Ready", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -574f), new Vector2(170f, 44f), UiKit.ButtonStyle.Primary, () =>
            {
                LobbyPlayer me = session?.Lobby?.Get(session.YourId);
                session?.SetReady(me == null || !me.Ready);
            }, 18);
            lobbyError = kit.Label(Node("Error", left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -630f), new Vector2(lw - 56f, 22f)), "", 15, TextAnchor.MiddleLeft, UiKit.Red, UiKit.Face.Mono, 0f, false);
            kit.Button(left, "Leave", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(160f, 44f), UiKit.ButtonStyle.Secondary, LeaveOnline, 18);

            // Right: the night's settings and the chat.
            const float rw = 590f;
            RectTransform right = kit.Card("Settings", grid, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(rw, lh));
            kit.FieldLabel(right, "Match settings", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), 300f);
            float y = -60f;
            Stepper(right, "Hunters (Zach)", ref y, out huntersValue, out huntersMinus, out huntersPlus, d => ChangeSettings(s => s.Hunters += d));
            Stepper(right, "Max survivors", ref y, out survivorsValue, out survivorsMinus, out survivorsPlus, d => ChangeSettings(s => s.Survivors += d));
            kit.FieldLabel(right, "Map seed (blank = random)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), 400f);
            y -= 26f;
            seedField = kit.Field(right, "Seed", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(rw - 56f, 40f), "random", 32);
            seedField.onEndEdit.AddListener(v => ChangeSettings(s => s.Seed = v));
            y -= 54f;
            testButton = kit.Button(right, "Testing mode: off", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(rw - 56f, 40f), UiKit.ButtonStyle.Ghost, () => ChangeSettings(s => s.TestMode = !s.TestMode), 16);
            testLabel = testButton.GetComponentInChildren<Text>();
            y -= 52f;
            lobbyPreview = kit.Paragraph(Node("Preview", right, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(rw - 56f, 70f)), "", 14, UiKit.Muted, UiKit.Face.Mono, 1.25f);
            y -= 80f;
            shuffleButton = kit.Button(right, "Shuffle roles", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(200f, 44f), UiKit.ButtonStyle.Normal, () => session?.Shuffle(), 17);
            startButton = kit.Button(right, "Start the night", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, y), new Vector2(240f, 44f), UiKit.ButtonStyle.Primary, () => session?.StartMatch(), 19);
            y -= 52f;
            lobbyNote = kit.Label(Node("Note", right, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(rw - 56f, 20f)), "", 14, TextAnchor.MiddleRight, UiKit.Muted, UiKit.Face.Mono, 0f, false);
            y -= 34f;
            kit.FieldLabel(right, "Chat", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), 200f);
            y -= 24f;
            RectTransform log = Node("Chat Log", right, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(rw - 56f, 130f));
            UiKit.Box(log, new Color(0f, 0f, 0f, 0.35f));
            chatLog = kit.Label(UiKit.Fill("Text", log, 8f), "", 14, TextAnchor.LowerLeft, UiKit.Bone, UiKit.Face.Mono, 0f, false);
            chatLog.supportRichText = true;
            chatLog.verticalOverflow = VerticalWrapMode.Truncate;
            chatField = kit.Field(right, "Say", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(rw - 56f, 40f), "Say something...", 140);
            chatField.onEndEdit.AddListener(v =>
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                session?.Chat(v);
                chatField.text = "";
                chatField.ActivateInputField();
            });

            lobbyScreen = screen.gameObject;
            lobbyScreen.SetActive(false);
        }

        void Stepper(RectTransform parent, string label, ref float y, out Text value, out Button minus, out Button plus, System.Action<int> step)
        {
            kit.Label(Node(label, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(300f, 40f)), label, 17, TextAnchor.MiddleLeft, UiKit.Bone, UiKit.Face.Mono, 0f, false);
            minus = kit.Button(parent, "-", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, y), new Vector2(40f, 40f), UiKit.ButtonStyle.Normal, () => step(-1), 20);
            value = kit.Label(Node("Value", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-76f, y), new Vector2(70f, 40f)), "1", 20, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.DisplayBold, 0f, false);
            plus = kit.Button(parent, "+", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, y), new Vector2(40f, 40f), UiKit.ButtonStyle.Normal, () => step(1), 20);
            y -= 52f;
        }

        void ChangeSettings(System.Action<LobbySettings> change)
        {
            if (session == null || session.Lobby == null || !session.CanManage) return;
            LobbySettings s = session.Lobby.Settings;
            var copy = new LobbySettings { Hunters = s.Hunters, Survivors = s.Survivors, Seed = s.Seed, TestMode = s.TestMode, Pace = s.Pace };
            change(copy);
            session.SetSettings(copy.Clamped());
        }

        LobbyRow Row(int i)
        {
            while (lobbyRows.Count <= i)
            {
                int k = lobbyRows.Count;
                var row = new LobbyRow { Root = Node("Row", lobbyList, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -k * 40f), new Vector2(724f, 36f)) };
                UiKit.Box(row.Root, new Color(1f, 1f, 1f, 0.03f));
                row.Name = kit.Label(Node("Name", row.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(320f, 30f)), "", 17, TextAnchor.MiddleLeft, UiKit.Bone, UiKit.Face.Mono, 0f, false);
                row.Name.supportRichText = true;
                row.RoleButton = kit.Button(row.Root, "auto role", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(340f, 0f), new Vector2(150f, 30f), UiKit.ButtonStyle.Ghost, null, 14);
                row.Role = row.RoleButton.GetComponentInChildren<Text>();
                row.Ready = kit.Label(Node("Ready", row.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(504f, 0f), new Vector2(120f, 30f)), "", 14, TextAnchor.MiddleCenter, UiKit.Muted, UiKit.Face.Type, 0.08f, false);
                row.Ping = kit.Label(Node("Ping", row.Root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(90f, 30f)), "", 13, TextAnchor.MiddleRight, UiKit.Muted, UiKit.Face.Mono, 0f, false);
                lobbyRows.Add(row);
            }
            return lobbyRows[i];
        }

        static string RoleText(LobbyPlayer p) => p.Assigned switch
        {
            Assigned.Hunter => "Zach",
            Assigned.Survivor => "survivor",
            Assigned.Spectator => "spectator",
            _ => p.Pref == RolePref.Any ? "wants either" : p.Pref == RolePref.Hunter ? "wants Zach" : "wants survivor",
        };

        void RefreshLobby()
        {
            if (session == null || lobbyScreen == null || !lobbyScreen.activeSelf) return;
            Lobby lobby = session.Lobby;
            lobbyCode.text = session.RoomCode;
            if (lobby == null) return;
            bool manage = session.CanManage;
            int i = 0;
            foreach (LobbyPlayer p in lobby.Players)
            {
                LobbyRow row = Row(i++);
                row.Root.gameObject.SetActive(true);
                bool me = p.Id == session.YourId;
                row.Name.text = (p.Owner ? "<color=#c9b26a>♛</color> " : "") + p.Name + (me ? " <color=#8a877c>(you)</color>" : "") + (p.Owner ? " <color=#8a877c>host</color>" : "");
                row.Role.text = manage ? (p.Assigned == Assigned.Auto ? "auto role" : RoleText(p)) : RoleText(p);
                row.RoleButton.onClick.RemoveAllListeners();
                row.RoleButton.interactable = manage;
                if (manage)
                {
                    int id = p.Id;
                    Assigned next = (Assigned)(((int)p.Assigned + 1) % 4);
                    row.RoleButton.onClick.AddListener(() => session.Assign(id, next));
                }
                row.Ready.text = !p.Connected ? "OFFLINE" : p.Ready ? "READY" : "NOT READY";
                row.Ready.color = !p.Connected ? UiKit.Red : p.Ready ? UiKit.Green : UiKit.Muted;
                row.Ping.text = p.Owner || !p.Connected ? "" : $"{p.Ping} ms";
            }
            for (; i < lobbyRows.Count; i++) lobbyRows[i].Root.gameObject.SetActive(false);

            LobbyPlayer mine = lobby.Get(session.YourId);
            RolePref[] values = { RolePref.Survivor, RolePref.Hunter, RolePref.Any };
            for (int k = 0; k < 3; k++)
                prefButtons[k].GetComponent<Image>().color = mine != null && mine.Pref == values[k] ? UiKit.A(UiKit.Red, 0.55f) : new Color(1f, 1f, 1f, 0.06f);
            readyButton.gameObject.SetActive(!session.IsHost);
            readyButton.GetComponentInChildren<Text>().text = mine != null && mine.Ready ? "Ready ✓" : "Ready";

            LobbySettings s = lobby.Settings;
            huntersValue.text = s.Hunters.ToString();
            survivorsValue.text = s.Survivors.ToString();
            if (!seedField.isFocused) seedField.text = s.Seed;
            seedField.interactable = manage;
            testLabel.text = s.TestMode ? "Testing mode: on · T switches Zach/survivor, infinite items, nobody wins" : "Testing mode: off";
            foreach (Button b in new[] { huntersMinus, huntersPlus, survivorsMinus, survivorsPlus, testButton, shuffleButton }) b.interactable = manage;
            (int h, int sv, int spec) = lobby.Preview();
            ResolvedBalance rb = MatchRules.Resolve(Mathf.Max(1, sv), Mathf.Max(1, h));
            lobbyPreview.text = $"Next match: {h} Zach · {sv} survivors{(spec > 0 ? $" · {spec} spectating" : "")}\n" +
                                $"Auto-balance: {rb.RequiredGenerators} generators, {Mathf.RoundToInt(rb.RepairTime)} s each. The night lasts until every survivor has escaped or is down.";
            string why = session.IsHost ? session.Server.CannotStart() : null;
            startButton.gameObject.SetActive(manage);
            startButton.interactable = session.IsHost ? why == null : manage;
            lobbyNote.text = session.IsHost ? why ?? "Ready." : manage ? "" : "Waiting for the host.";
        }

        void AddChat(string from, string text)
        {
            chatLines.Add(from == null ? $"<color=#8a877c>{Escape(text)}</color>" : $"<b>{Escape(from)}:</b> {Escape(text)}");
            while (chatLines.Count > 7) chatLines.RemoveAt(0);
            if (chatLog != null) chatLog.text = string.Join("\n", chatLines);
        }

        static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");

        /// <summary>Into the lobby (just created or joined, or back from a match).</summary>
        void OpenLobby(NetSession s)
        {
            bool fresh = session != s;
            if (fresh)
            {
                session = s;
                chatLines.Clear();
                s.LobbyChanged += RefreshLobby;
                s.ChatLine += AddChat;
                s.Notice += t => { AddChat(null, t); Notify(t); };
                s.MatchLoading += () =>
                {
                    lobbyOpen = false;
                    lobbyScreen.SetActive(false);
                    if (results != null) results.SetActive(false);
                    shownResult = null;
                    generating.SetActive(true);
                };
                s.LobbyShown += () => OpenLobby(session);
            }
            MainMenuOpen = false;
            mainMenu.SetActive(false);
            SetMenu(false);
            if (results != null) results.SetActive(false);
            shownResult = null;
            lobbyOpen = true;
            lobbyScreen.SetActive(true);
            lobbyError.text = "";
            if (fresh) AddChat(null, s.IsHost ? $"Lobby open at {s.RoomCode}" : $"Joined {s.RoomCode}");
            RefreshLobby();
        }

        /// <summary>Leaves the online session (closing the lobby when hosting) for the title screen.</summary>
        public void LeaveOnline() => ShowMainMenu();

        /// <summary>Watches the session each frame: joining, the host leaving, the match loading.</summary>
        void UpdateOnline()
        {
            if (joining != null)
            {
                NetClient c = joining.Client;
                if (c == null || c.Failed != null)
                {
                    string why = c?.Failed ?? "Couldn't join";
                    joining.Leave();
                    joining = null;
                    ShowLandingError(why, false);
                }
                else if (c.Lobby != null)
                {
                    NetSession s = joining;
                    joining = null;
                    OpenLobby(s);
                }
                return;
            }
            if (session == null) return;
            if (session.Client != null && session.Client.Failed != null)
            {
                string why = session.Client.Failed;
                LeaveOnline();
                ShowLandingError(why, false);
                return;
            }
            if (generating.activeSelf && session.Loaded && session.Phase == NetPhase.Match) generating.SetActive(false);
            if (lobbyOpen && Time.frameCount % 30 == 0) RefreshLobby();
        }
    }
}
