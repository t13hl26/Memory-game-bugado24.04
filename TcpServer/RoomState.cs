using System;

namespace TcpLobbyServer
{
    public class RoomState
    {
        public string RoomCode { get; set; } = "";
        public string HostId { get; set; } = "";
        public string GuestId { get; set; } = "";
        public bool HostReady { get; set; }
        public bool GuestReady { get; set; }
        public string Status { get; set; } = "waiting";
        public string RelayJoinCode { get; set; } = "";
    }
}
