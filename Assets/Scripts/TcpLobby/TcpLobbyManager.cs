using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TcpLobby
{
    public class TcpLobbyManager : MonoBehaviour
    {
        public TcpLobbyClient client;
        public RelayMatchController relayController;

        public Text roomCodeText;
        public Text statusText;
        public Button readyButton;
        public Button createRoomButton;
        public Button joinRoomButton;
        public InputField joinCodeInput;

        public bool autoConnectOnStart = true;

        private RoomState _roomState;
        private bool _isHost;
        private bool _isReady;

        public event Action<string> RoomCodeChanged;
        public event Action<string> StatusChanged;

        private void Awake()
        {
            if (client == null)
                client = GetComponent<TcpLobbyClient>();

            if (relayController == null)
                relayController = RelayMatchController.EnsureInstance();

            if (client != null)
            {
                client.Connected += HandleConnected;
                client.Disconnected += HandleDisconnected;
                client.MessageReceived += HandleMessage;
                client.Log += HandleLog;
            }
        }

        private async void Start()
        {
            if (autoConnectOnStart && client != null)
                await client.ConnectAsync();
        }

        private void OnDestroy()
        {
            if (client == null)
                return;

            client.Connected -= HandleConnected;
            client.Disconnected -= HandleDisconnected;
            client.MessageReceived -= HandleMessage;
            client.Log -= HandleLog;
        }

        public async void CreateRoom()
        {
            if (client == null || !client.IsConnected)
            {
                PublishStatus("Servidor TCP nao conectado.");
                return;
            }

            _isHost = true;
            _isReady = false;
            await client.CreateRoomAsync();
            PublishStatus("Criando sala...");
        }

        public async void JoinRoom()
        {
            if (client == null || !client.IsConnected)
            {
                PublishStatus("Servidor TCP nao conectado.");
                return;
            }

            string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
            if (string.IsNullOrEmpty(code))
            {
                PublishStatus("Informe um codigo valido.");
                return;
            }

            _isHost = false;
            _isReady = false;
            await client.JoinRoomAsync(code);
            PublishStatus("Entrando na sala...");
        }

        public async void ToggleReady()
        {
            if (client == null || !client.IsConnected)
                return;

            _isReady = !_isReady;
            await client.SetReadyAsync(_isReady);
            UpdateReadyButton();
        }

        public void LeaveRoom()
        {
            _roomState = null;
            _isReady = false;
            _isHost = false;
            PublishRoomCode("");
            PublishStatus("Sala encerrada.");
        }

        private void HandleConnected()
        {
            PublishStatus("Conectado ao servidor TCP.");
        }

        private void HandleDisconnected()
        {
            PublishStatus("Conexao TCP encerrada.");
            _roomState = null;
            _isReady = false;
            UpdateReadyButton();
        }

        private void HandleMessage(LobbyMessage message)
        {
            switch (message.type)
            {
                case "room_created":
                    _roomState = message.room;
                    _isHost = true;
                    PublishRoomCode(_roomState != null ? _roomState.roomCode : "");
                    PublishStatus("Sala criada. Convide o outro jogador.");
                    break;
                case "room_joined":
                    _roomState = message.room;
                    _isHost = message.isHost;
                    PublishRoomCode(_roomState != null ? _roomState.roomCode : "");
                    PublishStatus("Entrou na sala. Marque pronto.");
                    break;
                case "room_state":
                    _roomState = message.room;
                    PublishRoomCode(_roomState != null ? _roomState.roomCode : "");
                    PublishStatus("Sala atualizada.");
                    break;
                case "start_match":
                    PublishStatus("Ambos prontos. Iniciando partida...");
                    BeginRelayHandshake();
                    break;
                case "relay_info":
                    if (!string.IsNullOrEmpty(message.relayJoinCode))
                    {
                        PublishStatus("Recebido codigo do Relay. Entrando...");
                        _ = relayController.JoinMatchAsync(message.relayJoinCode);
                    }
                    break;
                case "player_left":
                    PublishStatus("O outro jogador saiu da sala.");
                    _roomState = null;
                    _isReady = false;
                    UpdateReadyButton();
                    break;
                case "error":
                    PublishStatus(message.error ?? "Erro desconhecido.");
                    break;
            }
        }

        private async void BeginRelayHandshake()
        {
            if (_isHost)
            {
                bool created = await relayController.CreateMatchAsync();
                if (!created)
                {
                    PublishStatus("Falha ao iniciar Relay.");
                    return;
                }

                string joinCode = relayController.CurrentJoinCode;
                if (!string.IsNullOrEmpty(joinCode) && _roomState != null)
                    await client.SendRelayInfoAsync(_roomState.roomCode, joinCode);
            }
        }

        private void PublishRoomCode(string value)
        {
            if (roomCodeText != null)
            {
                roomCodeText.text = value;
                roomCodeText.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }

            RoomCodeChanged?.Invoke(value);
        }

        private void PublishStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;

            StatusChanged?.Invoke(message);
            Debug.Log("[TcpLobbyManager] " + message);
        }

        private void UpdateReadyButton()
        {
            if (readyButton == null)
                return;

            Text label = readyButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _isReady ? "Pronto!" : "Marcar pronto";
        }

        private void HandleLog(string message)
        {
            Debug.Log("[TcpLobby] " + message);
        }
    }
}
