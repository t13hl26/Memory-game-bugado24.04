using System;

namespace TcpLobby
{
    [Serializable]
    public class RoomState
    {
        public string roomCode;
        public string hostId;
        public string guestId;
        public bool hostReady;
        public bool guestReady;
        public string status;
    }
}
