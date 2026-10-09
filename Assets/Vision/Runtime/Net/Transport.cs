using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Vision.Net
{
    /// <summary>
    /// One TCP connection carrying framed messages ([4-byte length][message]). A reader thread and a writer thread move the
    /// bytes; the game drains <see cref="Inbox"/> on its own thread and queues with <see cref="Send"/>, so it never waits
    /// on the network. Nagle is off, so small input and snapshot frames go out at once.
    /// </summary>
    public sealed class Connection
    {
        readonly TcpClient tcp;
        readonly NetworkStream stream;
        readonly BlockingCollection<byte[]> outbox = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        public readonly ConcurrentQueue<byte[]> Inbox = new ConcurrentQueue<byte[]>();
        volatile bool closed;
        public string Remote { get; }
        public bool Closed => closed;
        /// <summary>Why it closed (a read error, the other end leaving), for the player.</summary>
        public string CloseReason { get; private set; }
        public long BytesOut, BytesIn;

        public Connection(TcpClient tcp)
        {
            this.tcp = tcp;
            tcp.NoDelay = true;
            tcp.SendBufferSize = 1 << 18;
            tcp.ReceiveBufferSize = 1 << 18;
            stream = tcp.GetStream();
            Remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
            new Thread(ReadLoop) { IsBackground = true, Name = "Net read " + Remote }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "Net write " + Remote }.Start();
        }

        public void Send(byte[] message)
        {
            if (closed) return;
            try { outbox.Add(message); }
            catch (InvalidOperationException) { }
        }

        void ReadLoop()
        {
            var header = new byte[4];
            try
            {
                while (!closed)
                {
                    if (!ReadExactly(header, 4)) break;
                    int n = BitConverter.ToInt32(header, 0);
                    if (n <= 0 || n > Wire.MaxFrame) throw new InvalidDataException("bad frame length");
                    var body = new byte[n];
                    if (!ReadExactly(body, n)) break;
                    Interlocked.Add(ref BytesIn, n + 4);
                    Inbox.Enqueue(body);
                }
                Close("The connection was closed");
            }
            catch (Exception e)
            {
                Close(e is IOException || e is SocketException || e is ObjectDisposedException ? "The connection was lost" : e.Message);
            }
        }

        bool ReadExactly(byte[] buf, int n)
        {
            int got = 0;
            while (got < n)
            {
                int k = stream.Read(buf, got, n - got);
                if (k <= 0) return false;
                got += k;
            }
            return true;
        }

        void WriteLoop()
        {
            try
            {
                foreach (byte[] m in outbox.GetConsumingEnumerable())
                {
                    if (closed) break;
                    stream.Write(BitConverter.GetBytes(m.Length), 0, 4);
                    stream.Write(m, 0, m.Length);
                    Interlocked.Add(ref BytesOut, m.Length + 4);
                }
            }
            catch (Exception)
            {
                Close("The connection was lost");
            }
        }

        public void Close(string reason = "Closed")
        {
            if (closed) return;
            closed = true;
            CloseReason = reason;
            try { outbox.CompleteAdding(); } catch (Exception) { }
            try { stream.Close(); } catch (Exception) { }
            try { tcp.Close(); } catch (Exception) { }
        }

        /// <summary>Sends what is queued, then closes (a last message before leaving).</summary>
        public void Flush(int timeoutMs = 300)
        {
            int waited = 0;
            while (outbox.Count > 0 && !closed && waited < timeoutMs)
            {
                Thread.Sleep(10);
                waited += 10;
            }
        }

        /// <summary>Connects to a host (blocking up to the timeout; run off the main thread).</summary>
        public static Connection Connect(string host, int port, int timeoutMs = 5000)
        {
            bool v4 = IPAddress.TryParse(host, out IPAddress ip) && ip.AddressFamily == AddressFamily.InterNetwork;
            var tcp = v4 ? new TcpClient(AddressFamily.InterNetwork) : new TcpClient(AddressFamily.InterNetworkV6);
            tcp.NoDelay = true;
            if (!v4) tcp.Client.DualMode = true;
            IAsyncResult ar = tcp.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
            {
                tcp.Close();
                throw new TimeoutException($"No lobby answered at {host}:{port}");
            }
            tcp.EndConnect(ar);
            return new Connection(tcp);
        }
    }

    /// <summary>The host's door: listens on a port (IPv4 and IPv6) and hands each new connection over.</summary>
    public sealed class Listener
    {
        readonly TcpListener tcp;
        public readonly ConcurrentQueue<Connection> Accepted = new ConcurrentQueue<Connection>();
        volatile bool stopped;
        public int Port { get; }

        /// <summary>Listen on this machine only (tests and the smoke run: no firewall prompt, nobody else can join).</summary>
        public static bool LoopbackOnly;

        public Listener(int port)
        {
            if (LoopbackOnly) tcp = new TcpListener(IPAddress.Loopback, port);
            else
            {
                tcp = new TcpListener(IPAddress.IPv6Any, port);
                tcp.Server.DualMode = true;
            }
            tcp.Start();
            Port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            new Thread(AcceptLoop) { IsBackground = true, Name = "Net accept" }.Start();
        }

        void AcceptLoop()
        {
            while (!stopped)
            {
                try { Accepted.Enqueue(new Connection(tcp.AcceptTcpClient())); }
                catch (Exception) { if (stopped) break; }
            }
        }

        public void Stop()
        {
            stopped = true;
            try { tcp.Stop(); } catch (Exception) { }
        }

        /// <summary>This machine's addresses others can join (LAN; over the internet the router must forward the port).</summary>
        public static string[] LocalAddresses()
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) list.Add(a.ToString());
            }
            catch (Exception) { }
            if (list.Count == 0) list.Add("127.0.0.1");
            return list.ToArray();
        }
    }
}
