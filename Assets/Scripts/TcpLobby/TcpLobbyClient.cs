using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TcpLobby
{
    public class TcpLobbyClient : MonoBehaviour
    {
        public string serverIp = "127.0.0.1";
        public int serverPort = 7777;
        public string playerName = "Player";

        public bool IsConnected { get; private set; }
        public string PlayerId { get; private set; } = "";

        public event Action Connected;
        public event Action Disconnected;
        public event Action<LobbyMessage> MessageReceived;
        public event Action<string> Log;

        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out Action action))
                action?.Invoke();
        }

        public async Task<bool> ConnectAsync()
        {
            if (IsConnected)
                return true;

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(serverIp, serverPort);

                NetworkStream stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ReadLoopAsync(_cts.Token));

                IsConnected = true;
                EnqueueMainThread(() => Connected?.Invoke());

                await SendAsync(new LobbyMessage
                {
                    type = "hello",
                    playerName = playerName
                });

                LogInfo("TCP connected to lobby server.");
                return true;
            }
            catch (Exception ex)
            {
                LogInfo("TCP connect failed: " + ex.Message);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected)
                return;

            try
            {
                await SendAsync(new LobbyMessage { type = "leave_room" });
            }
            catch
            {
                // ignore on shutdown
            }

            Cleanup();
        }

        public Task SendAsync(LobbyMessage message)
        {
            if (_writer == null)
                return Task.CompletedTask;

            string json = LobbyMessage.Serialize(message);
            return _writer.WriteLineAsync(json);
        }

        public Task CreateRoomAsync()
        {
            return SendAsync(new LobbyMessage { type = "create_room" });
        }

        public Task JoinRoomAsync(string roomCode)
        {
            return SendAsync(new LobbyMessage { type = "join_room", roomCode = roomCode });
        }

        public Task SetReadyAsync(bool isReady)
        {
            return SendAsync(new LobbyMessage { type = "ready", isReady = isReady });
        }

        public Task SendRelayInfoAsync(string roomCode, string relayJoinCode)
        {
            return SendAsync(new LobbyMessage
            {
                type = "relay_info",
                roomCode = roomCode,
                relayJoinCode = relayJoinCode
            });
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string line = await _reader.ReadLineAsync();
                    if (line == null)
                        break;

                    LobbyMessage message = LobbyMessage.Deserialize(line);
                    if (message == null)
                        continue;

                    if (message.type == "welcome" && !string.IsNullOrEmpty(message.playerId))
                        PlayerId = message.playerId;

                    EnqueueMainThread(() => MessageReceived?.Invoke(message));
                }
            }
            catch (Exception ex)
            {
                LogInfo("TCP read loop stopped: " + ex.Message);
            }
            finally
            {
                EnqueueMainThread(() => Disconnected?.Invoke());
                Cleanup();
            }
        }

        private void Cleanup()
        {
            IsConnected = false;
            _cts?.Cancel();
            _cts = null;

            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }

            _writer = null;
            _reader = null;
            _client = null;
        }

        private void EnqueueMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private void LogInfo(string message)
        {
            EnqueueMainThread(() => Log?.Invoke(message));
            Debug.Log("[TcpLobbyClient] " + message);
        }
    }
}
