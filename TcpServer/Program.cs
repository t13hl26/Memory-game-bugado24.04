using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpLobbyServer
{
    internal class Program
    {
        private static readonly ConcurrentDictionary<string, ClientConnection> Clients = new ConcurrentDictionary<string, ClientConnection>();
        private static readonly ConcurrentDictionary<string, RoomState> Rooms = new ConcurrentDictionary<string, RoomState>();
        private static readonly Random Random = new Random();
        private static TcpListener _listener;

        private static async Task Main(string[] args)
        {
            int port = 7777;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed))
                port = parsed;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            Console.WriteLine("[TCP] Lobby server started on port " + port);

            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private static async Task HandleClientAsync(TcpClient tcpClient)
        {
            string clientId = Guid.NewGuid().ToString("N");
            Console.WriteLine("[TCP] Client connected: " + clientId);

            NetworkStream stream = tcpClient.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            ClientConnection connection = new ClientConnection
            {
                ClientId = clientId,
                TcpClient = tcpClient,
                Writer = writer
            };

            Clients[clientId] = connection;

            await SendAsync(connection, new LobbyMessage
            {
                type = "welcome",
                playerId = clientId
            });

            try
            {
                while (true)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    LobbyMessage message = LobbyMessage.Deserialize(line);
                    if (message == null)
                        continue;

                    await HandleMessageAsync(connection, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TCP] Client error: " + ex.Message);
            }
            finally
            {
                Clients.TryRemove(clientId, out _);
                HandleDisconnect(connection);
                tcpClient.Close();
                Console.WriteLine("[TCP] Client disconnected: " + clientId);
            }
        }

        private static async Task HandleMessageAsync(ClientConnection connection, LobbyMessage message)
        {
            switch (message.type)
            {
                case "hello":
                    connection.PlayerName = message.playerName ?? "Player";
                    break;
                case "create_room":
                    await HandleCreateRoom(connection);
                    break;
                case "join_room":
                    await HandleJoinRoom(connection, message.roomCode);
                    break;
                case "ready":
                    await HandleReady(connection, message.isReady);
                    break;
                case "leave_room":
                    await HandleLeaveRoom(connection);
                    break;
                case "relay_info":
                    await HandleRelayInfo(connection, message.roomCode, message.relayJoinCode);
                    break;
                default:
                    await SendError(connection, "Mensagem desconhecida.");
                    break;
            }
        }

        private static async Task HandleCreateRoom(ClientConnection connection)
        {
            string roomCode = GenerateRoomCode();
            RoomState room = new RoomState
            {
                RoomCode = roomCode,
                HostId = connection.ClientId,
                Status = "waiting"
            };

            Rooms[roomCode] = room;
            connection.RoomCode = roomCode;

            await SendAsync(connection, new LobbyMessage
            {
                type = "room_created",
                room = ToPublicRoom(room)
            });

            Console.WriteLine("[TCP] Room created: " + roomCode);
        }

        private static async Task HandleJoinRoom(ClientConnection connection, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                await SendError(connection, "Codigo invalido.");
                return;
            }

            roomCode = roomCode.Trim().ToUpperInvariant();

            if (!Rooms.TryGetValue(roomCode, out RoomState room))
            {
                await SendError(connection, "Sala nao encontrada.");
                return;
            }

            if (!string.IsNullOrEmpty(room.GuestId))
            {
                await SendError(connection, "Sala ja esta cheia.");
                return;
            }

            room.GuestId = connection.ClientId;
            room.Status = "ready_check";
            connection.RoomCode = roomCode;

            await SendAsync(connection, new LobbyMessage
            {
                type = "room_joined",
                room = ToPublicRoom(room),
                isHost = false
            });

            await BroadcastRoomState(room);
            Console.WriteLine("[TCP] Guest joined: " + roomCode);
        }

        private static async Task HandleReady(ClientConnection connection, bool isReady)
        {
            if (string.IsNullOrEmpty(connection.RoomCode))
            {
                await SendError(connection, "Voce nao esta em uma sala.");
                return;
            }

            if (!Rooms.TryGetValue(connection.RoomCode, out RoomState room))
            {
                await SendError(connection, "Sala nao encontrada.");
                return;
            }

            if (connection.ClientId == room.HostId)
                room.HostReady = isReady;
            else if (connection.ClientId == room.GuestId)
                room.GuestReady = isReady;

            await BroadcastRoomState(room);

            if (room.HostReady && room.GuestReady)
            {
                room.Status = "starting";
                await BroadcastRoomState(room);
                await Broadcast(room, new LobbyMessage { type = "start_match", room = ToPublicRoom(room) });
            }
        }

        private static async Task HandleRelayInfo(ClientConnection connection, string roomCode, string relayJoinCode)
        {
            if (string.IsNullOrEmpty(roomCode))
                return;

            if (!Rooms.TryGetValue(roomCode, out RoomState room))
                return;

            if (connection.ClientId != room.HostId)
                return;

            room.RelayJoinCode = relayJoinCode ?? "";
            if (string.IsNullOrEmpty(room.GuestId))
                return;

            if (Clients.TryGetValue(room.GuestId, out ClientConnection guest))
            {
                await SendAsync(guest, new LobbyMessage
                {
                    type = "relay_info",
                    relayJoinCode = room.RelayJoinCode,
                    room = ToPublicRoom(room)
                });
            }
        }

        private static async Task HandleLeaveRoom(ClientConnection connection)
        {
            if (string.IsNullOrEmpty(connection.RoomCode))
                return;

            if (!Rooms.TryGetValue(connection.RoomCode, out RoomState room))
                return;

            if (connection.ClientId == room.HostId)
            {
                await NotifyOther(room, connection.ClientId);
                Rooms.TryRemove(room.RoomCode, out _);
                return;
            }

            if (connection.ClientId == room.GuestId)
            {
                room.GuestId = "";
                room.GuestReady = false;
                room.Status = "waiting";
                await NotifyOther(room, connection.ClientId);
                await BroadcastRoomState(room);
            }
        }

        private static void HandleDisconnect(ClientConnection connection)
        {
            if (string.IsNullOrEmpty(connection.RoomCode))
                return;

            if (!Rooms.TryGetValue(connection.RoomCode, out RoomState room))
                return;

            if (connection.ClientId == room.HostId)
            {
                NotifyOther(room, connection.ClientId).GetAwaiter().GetResult();
                Rooms.TryRemove(room.RoomCode, out _);
                return;
            }

            if (connection.ClientId == room.GuestId)
            {
                room.GuestId = "";
                room.GuestReady = false;
                room.Status = "waiting";
                BroadcastRoomState(room).GetAwaiter().GetResult();
                NotifyOther(room, connection.ClientId).GetAwaiter().GetResult();
            }
        }

        private static async Task BroadcastRoomState(RoomState room)
        {
            await Broadcast(room, new LobbyMessage
            {
                type = "room_state",
                room = ToPublicRoom(room)
            });
        }

        private static async Task Broadcast(RoomState room, LobbyMessage message)
        {
            if (!string.IsNullOrEmpty(room.HostId) && Clients.TryGetValue(room.HostId, out ClientConnection host))
                await SendAsync(host, message);

            if (!string.IsNullOrEmpty(room.GuestId) && Clients.TryGetValue(room.GuestId, out ClientConnection guest))
                await SendAsync(guest, message);
        }

        private static async Task NotifyOther(RoomState room, string sourceClientId)
        {
            if (room.HostId != sourceClientId && Clients.TryGetValue(room.HostId, out ClientConnection host))
                await SendAsync(host, new LobbyMessage { type = "player_left", room = ToPublicRoom(room) });

            if (room.GuestId != sourceClientId && Clients.TryGetValue(room.GuestId, out ClientConnection guest))
                await SendAsync(guest, new LobbyMessage { type = "player_left", room = ToPublicRoom(room) });
        }

        private static Task SendError(ClientConnection connection, string error)
        {
            return SendAsync(connection, new LobbyMessage { type = "error", error = error });
        }

        private static Task SendAsync(ClientConnection connection, LobbyMessage message)
        {
            string json = LobbyMessage.Serialize(message);
            return connection.Writer.WriteLineAsync(json);
        }

        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string code;
            do
            {
                char[] buffer = new char[6];
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = chars[Random.Next(chars.Length)];
                code = new string(buffer);
            } while (Rooms.ContainsKey(code));

            return code;
        }

        private static RoomState ToPublicRoom(RoomState room)
        {
            return new RoomState
            {
                RoomCode = room.RoomCode,
                HostId = room.HostId,
                GuestId = room.GuestId,
                HostReady = room.HostReady,
                GuestReady = room.GuestReady,
                Status = room.Status,
                RelayJoinCode = room.RelayJoinCode
            };
        }
    }

    internal class ClientConnection
    {
        public string ClientId { get; set; }
        public string PlayerName { get; set; }
        public string RoomCode { get; set; }
        public TcpClient TcpClient { get; set; }
        public StreamWriter Writer { get; set; }
    }
}
