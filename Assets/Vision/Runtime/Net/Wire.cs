using System.IO;
using System.Text;
using UnityEngine;
using Vision.Game;

namespace Vision.Net
{
    /// <summary>The kinds of message on the wire (the first byte of each frame).</summary>
    public enum Msg : byte
    {
        // Client to host.
        Hello = 1, RolePref = 2, Ready = 3, Chat = 4, Settings = 5, Assign = 6, Shuffle = 7, Start = 8, Loaded = 9, Input = 10, Command = 11,
        ToLobby = 12, Ping = 13,
        // Host to client.
        Welcome = 40, Lobby = 41, ChatLine = 42, Error = 43, StartMatch = 44, Go = 45, Snapshot = 46, Results = 47, BackToLobby = 48,
        Pong = 49, Kicked = 50,
    }

    /// <summary>The testing and match commands a client asks the host for.</summary>
    public enum Command : byte { MoveSlot = 1, Spectate = 2, ViewReach = 3, SwitchRole = 4, TestFx = 5, Teleport = 6, RespawnNpcs = 7, SpawnDummy = 8, ClearDummies = 9 }

    /// <summary>Helpers for the binary wire format: strings, vectors, and frames of [length][type][payload].</summary>
    public static class Wire
    {
        /// <summary>Bumped whenever the wire format or the rules change: a client must match its host.</summary>
        public const int Version = 1;
        /// <summary>The port a lobby listens on unless told otherwise.</summary>
        public const int DefaultPort = 7777;
        /// <summary>No frame may be larger (guards against garbage on the socket).</summary>
        public const int MaxFrame = 4 * 1024 * 1024;

        public static void Write(BinaryWriter w, Vector2 v)
        {
            w.Write(v.x);
            w.Write(v.y);
        }

        public static Vector2 ReadVector2(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());

        public static void WriteString(BinaryWriter w, string s)
        {
            if (s == null)
            {
                w.Write(-1);
                return;
            }
            byte[] b = Encoding.UTF8.GetBytes(s);
            w.Write(b.Length);
            w.Write(b);
        }

        public static string ReadString(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0) return null;
            if (n > MaxFrame) throw new InvalidDataException("string too long");
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }

        public static void Write(BinaryWriter w, InputCmd c)
        {
            w.Write(c.Seq);
            w.Write((int)c.Buttons);
            Write(w, c.Move);
            w.Write(c.Aim);
            w.Write(c.AimDist);
            w.Write(c.Item);
        }

        public static InputCmd ReadInput(BinaryReader r) => new InputCmd
        {
            Seq = r.ReadUInt32(),
            Buttons = (Btn)r.ReadInt32(),
            Move = Vector2.ClampMagnitude(ReadVector2(r), 1f),
            Aim = r.ReadSingle(),
            AimDist = r.ReadSingle(),
            Item = r.ReadInt32(),
        };

        public static void Write(BinaryWriter w, GameEvent e)
        {
            w.Write((byte)e.Kind);
            w.Write(e.A);
            w.Write(e.B);
            Write(w, e.Pos);
            w.Write(e.F);
            w.Write(e.G);
            WriteString(w, e.Text);
        }

        public static GameEvent ReadEvent(BinaryReader r) => new GameEvent
        {
            Kind = (EventKind)r.ReadByte(),
            A = r.ReadInt32(),
            B = r.ReadInt32(),
            Pos = ReadVector2(r),
            F = r.ReadSingle(),
            G = r.ReadSingle(),
            Text = ReadString(r),
        };

        /// <summary>Starts a message: a writer over a fresh buffer with the type already in it.</summary>
        public static BinaryWriter Begin(Msg type)
        {
            var w = new BinaryWriter(new MemoryStream(256));
            w.Write((byte)type);
            return w;
        }

        public static byte[] End(BinaryWriter w)
        {
            w.Flush();
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        /// <summary>A message with nothing but its type.</summary>
        public static byte[] Bare(Msg type) => new[] { (byte)type };

        /// <summary>Parses "host:port" (or just a host, for the default port). False if it isn't one.</summary>
        public static bool TryParseAddress(string text, out string host, out int port)
        {
            host = null;
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            int colon = text.LastIndexOf(':');
            // An IPv6 address in brackets: [::1]:7777.
            if (text.StartsWith("["))
            {
                int close = text.IndexOf(']');
                if (close < 0) return false;
                host = text.Substring(1, close - 1);
                if (close + 1 < text.Length)
                {
                    if (text[close + 1] != ':' || !int.TryParse(text.Substring(close + 2), out port)) return false;
                }
            }
            else if (colon > 0 && text.IndexOf(':') == colon)
            {
                host = text.Substring(0, colon);
                if (!int.TryParse(text.Substring(colon + 1), out port)) return false;
            }
            else host = text;
            return port > 0 && port < 65536 && host.Length > 0;
        }
    }
}
