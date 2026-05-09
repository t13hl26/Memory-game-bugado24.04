using System;
using UnityEngine;

namespace TcpLobby
{
    [Serializable]
    public class LobbyMessage
    {
        public string type;
        public string requestId;
        public string roomCode;
        public string playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;
        public string status;
        public string error;
        public string relayJoinCode;
        public RoomState room;

        public static string Serialize(LobbyMessage message)
        {
            return JsonUtility.ToJson(message);
        }

        public static LobbyMessage Deserialize(string json)
        {
            return JsonUtility.FromJson<LobbyMessage>(json);
        }
    }
}
