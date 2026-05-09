using System;
using System.Text.Json;

namespace TcpLobbyServer
{
    public class LobbyMessage
    {
        public string type { get; set; } = "";
        public string requestId { get; set; }
        public string roomCode { get; set; }
        public string playerId { get; set; }
        public string playerName { get; set; }
        public bool isHost { get; set; }
        public bool isReady { get; set; }
        public string status { get; set; }
        public string error { get; set; }
        public string relayJoinCode { get; set; }
        public RoomState room { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static string Serialize(LobbyMessage message)
        {
            return JsonSerializer.Serialize(message, JsonOptions);
        }

        public static LobbyMessage Deserialize(string json)
        {
            return JsonSerializer.Deserialize<LobbyMessage>(json, JsonOptions);
        }
    }
}
