using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using LoadSceneMode = UnityEngine.SceneManagement.LoadSceneMode;

public class RelayMatchController : MonoBehaviour
{
    private const string StateSyncMessage = "memory.state.sync";
    private const string FlipRequestMessage = "memory.flip.request";
    private const string ReturnToMenuMessage = "memory.return.menu";
    private const string GameplayReadyMessage = "memory.gameplay.ready";
    private const string RematchRequestMessage = "memory.rematch.request";

    private static RelayMatchController _instance;

    private NetworkManager _networkManager;
    private UnityTransport _transport;
    private GameManager _gameManager;

    private bool _networkCallbacksRegistered;
    private bool _messageHandlersRegistered;
    private bool _servicesInitialized;
    private bool _manualShutdown;
    private bool _returningToMenu;
    private bool _gameStarted;
    private bool _boardInitialized;
    private bool _previewRoutineStarted;
    private bool _previewRunning;
    private bool _turnLocked;
    private bool _gameOver;
    private bool _hostGameplayReady;
    private bool _guestGameplayReady;

    private int[] _boardValues = Array.Empty<int>();
    private int[] _boardStates = Array.Empty<int>();
    private readonly int[] _scores = new int[2];
    private readonly int[] _comboStreaks = new int[2];
    private int _lastScoreEventSlot = -1;
    private int _lastPointsEarned;
    private int _lastComboValue;
    private int _currentTurnSlot;
    private int _firstSelectionIndex = -1;
    private ulong _hostClientId = ulong.MaxValue;
    private ulong _guestClientId = ulong.MaxValue;

    private bool _hasSnapshot;
    private SnapshotState _latestSnapshot;

    public static RelayMatchController Instance
    {
        get { return EnsureInstance(); }
    }

    public string CurrentJoinCode { get; private set; } = "";
    public string CurrentStatusMessage { get; private set; } = "";
    public bool IsBusy { get; private set; }

    public bool SessionRunning
    {
        get { return _networkManager != null && _networkManager.IsListening; }
    }

    public event Action<string> RoomCodeChanged;
    public event Action<string> StatusChanged;
    public event Action<bool> BusyStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureInstance();
    }

    public static RelayMatchController EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        RelayMatchController existing = FindObjectOfType<RelayMatchController>();
        if (existing != null)
        {
            _instance = existing;
            return existing;
        }

        GameObject root = new GameObject("RelayMatchController");
        _instance = root.AddComponent<RelayMatchController>();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureNetworkRuntime();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnregisterMessageHandlers();
        UnregisterNetworkCallbacks();
    }

    public async Task<bool> CreateMatchAsync()
    {
        if (IsBusy)
            return false;

        SetBusy(true);

        try
        {
            await EnsureServicesInitializedAsync();

            if (SessionRunning && _networkManager.IsHost && !_gameStarted && !string.IsNullOrEmpty(CurrentJoinCode))
            {
                PublishRoomCode(CurrentJoinCode);
                PublishStatus("Sala criada. Aguarde o segundo jogador.");
                return true;
            }

            ShutdownNetwork(false);
            ResetSessionState();

            PublishStatus("Criando sala...");
            PublishRoomCode("");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            _transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (!_networkManager.StartHost())
            {
                PublishStatus("Nao foi possivel iniciar a sala.");
                return false;
            }

            RegisterMessageHandlers();

            _hostClientId = _networkManager.LocalClientId;
            CurrentJoinCode = joinCode;
            GameLaunchConfig.ConfigureCreateMatch(joinCode);
            PublishRoomCode(joinCode);
            PublishStatus("Sala criada. Aguarde o segundo jogador.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            PublishStatus("Falha ao criar sala: " + ex.Message);
            ShutdownNetwork(false);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task<bool> JoinMatchAsync(string joinCode)
    {
        if (IsBusy)
            return false;

        if (string.IsNullOrEmpty(joinCode))
        {
            PublishStatus("Informe um codigo de partida valido.");
            return false;
        }

        SetBusy(true);

        try
        {
            await EnsureServicesInitializedAsync();

            ShutdownNetwork(false);
            ResetSessionState();

            PublishStatus("Entrando na sala...");
            PublishRoomCode("");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            _transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!_networkManager.StartClient())
            {
                PublishStatus("Nao foi possivel entrar na sala.");
                return false;
            }

            RegisterMessageHandlers();

            CurrentJoinCode = joinCode.ToUpperInvariant();
            GameLaunchConfig.ConfigureJoinMatch(CurrentJoinCode);
            PublishStatus("Conectado. Aguardando o host iniciar a partida.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            PublishStatus("Falha ao entrar na sala: " + ex.Message);
            ShutdownNetwork(false);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void CancelPendingSessionInMenu()
    {
        if (!SessionRunning)
        {
            CurrentJoinCode = "";
            PublishRoomCode("");
            return;
        }

        ShutdownNetwork(false);
        GameLaunchConfig.ConfigureStory(GameLaunchConfig.StoryChapter);
        PublishRoomCode("");
        PublishStatus("Escolha um modo de jogo.");
    }

    public void RequestFlip(int cardIndex)
    {
        if (!GameLaunchConfig.IsOnlineMode || !SessionRunning)
            return;

        if (_previewRunning || _gameOver)
            return;

        if (_networkManager.IsHost)
        {
            ProcessFlipRequest(_networkManager.LocalClientId, cardIndex);
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp))
        {
            writer.WriteValueSafe(cardIndex);
            _networkManager.CustomMessagingManager.SendNamedMessage(FlipRequestMessage, NetworkManager.ServerClientId, writer);
        }
    }

    public void LeaveMatchAndReturnToMenu(string reason)
    {
        if (_returningToMenu)
            return;

        StartCoroutine(ReturnToMenuRoutine(reason, _networkManager != null && _networkManager.IsHost && _networkManager.IsListening));
    }

    public void RequestRematch()
    {
        if (!GameLaunchConfig.IsOnlineMode || !SessionRunning || _returningToMenu)
            return;

        if (_networkManager.IsHost)
        {
            BeginRematch();
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp))
        {
            _networkManager.CustomMessagingManager.SendNamedMessage(RematchRequestMessage, NetworkManager.ServerClientId, writer);
        }

        PublishStatus("Pedido de revanche enviado ao host.");
    }

    private IEnumerator ReturnToMenuRoutine(string reason, bool notifyRemote, float delayBeforeLeaving = 0.2f)
    {
        _returningToMenu = true;

        if (notifyRemote && _networkManager != null && _networkManager.CustomMessagingManager != null)
        {
            using (FastBufferWriter writer = new FastBufferWriter(260, Allocator.Temp))
            {
                writer.WriteValueSafe(delayBeforeLeaving);
                writer.WriteValueSafe(new FixedString128Bytes(reason ?? ""));
                _networkManager.CustomMessagingManager.SendNamedMessageToAll(ReturnToMenuMessage, writer);
            }
        }

        GameLaunchConfig.ConfigureStory(GameLaunchConfig.StoryChapter);
        GameLaunchConfig.SetPendingMenuStatus(reason);
        PublishRoomCode("");

        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeLeaving));

        ShutdownNetwork(false);
        SceneManager.LoadScene(SceneIds.MainMenu);
        _returningToMenu = false;
    }

    private async Task EnsureServicesInitializedAsync()
    {
        if (!_servicesInitialized && UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        _servicesInitialized = true;
    }

    private void EnsureNetworkRuntime()
    {
        if (_networkManager == null)
            _networkManager = FindObjectOfType<NetworkManager>();

        if (_networkManager == null)
        {
            GameObject networkRoot = new GameObject("RelayNetworkRuntime");
            DontDestroyOnLoad(networkRoot);

            _networkManager = networkRoot.AddComponent<NetworkManager>();
            _transport = networkRoot.AddComponent<UnityTransport>();
            _networkManager.NetworkConfig = new NetworkConfig();
            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.NetworkConfig.EnableSceneManagement = true;
        }
        else
        {
            DontDestroyOnLoad(_networkManager.gameObject);
            _transport = _networkManager.GetComponent<UnityTransport>();
            if (_transport == null)
                _transport = _networkManager.gameObject.AddComponent<UnityTransport>();

            if (_networkManager.NetworkConfig == null)
                _networkManager.NetworkConfig = new NetworkConfig();

            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.NetworkConfig.EnableSceneManagement = true;
        }

        RegisterNetworkCallbacks();
    }

    private void RegisterNetworkCallbacks()
    {
        if (_networkManager == null || _networkCallbacksRegistered)
            return;

        _networkManager.OnClientConnectedCallback += HandleClientConnected;
        _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        _networkManager.OnServerStarted += HandleServerStarted;
        _networkCallbacksRegistered = true;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (_networkManager == null || !_networkCallbacksRegistered)
            return;

        _networkManager.OnClientConnectedCallback -= HandleClientConnected;
        _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        _networkManager.OnServerStarted -= HandleServerStarted;
        _networkCallbacksRegistered = false;
    }

    private void RegisterMessageHandlers()
    {
        if (_networkManager == null || _networkManager.CustomMessagingManager == null || _messageHandlersRegistered)
            return;

        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StateSyncMessage, HandleStateSyncMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(FlipRequestMessage, HandleFlipRequestMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReturnToMenuMessage, HandleReturnToMenuMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(GameplayReadyMessage, HandleGameplayReadyMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RematchRequestMessage, HandleRematchRequestMessage);
        _messageHandlersRegistered = true;
    }

    private void UnregisterMessageHandlers()
    {
        if (_networkManager == null || _networkManager.CustomMessagingManager == null || !_messageHandlersRegistered)
            return;

        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StateSyncMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(FlipRequestMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReturnToMenuMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(GameplayReadyMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RematchRequestMessage);
        _messageHandlersRegistered = false;
    }

    private void HandleServerStarted()
    {
        RegisterMessageHandlers();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (_networkManager == null || !_networkManager.IsListening)
            return;

        if (_networkManager.IsHost)
        {
            if (clientId == _networkManager.LocalClientId)
            {
                _hostClientId = clientId;
                return;
            }

            if (_guestClientId != ulong.MaxValue && _guestClientId != clientId)
            {
                _networkManager.DisconnectClient(clientId);
                return;
            }

            _guestClientId = clientId;
            PublishStatus("Jogador 2 conectado. Iniciando a partida...");

            if (!_gameStarted)
            {
                _gameStarted = true;
                _networkManager.SceneManager.LoadScene(SceneIds.Gameplay, LoadSceneMode.Single);
            }
            else
            {
                TryBeginOrRefreshHostGameplay();
            }
        }
        else if (_networkManager.IsClient && clientId == _networkManager.LocalClientId)
        {
            PublishStatus("Conectado. Aguardando o host iniciar a partida...");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (_manualShutdown)
            return;

        if (_networkManager == null)
            return;

        if (_networkManager.IsHost)
        {
            if (clientId == _guestClientId)
            {
                _guestClientId = ulong.MaxValue;
                _guestGameplayReady = false;
                _turnLocked = false;
                _previewRoutineStarted = false;

                if (SceneManager.GetActiveScene().name == SceneIds.Gameplay)
                {
                    StartCoroutine(ReturnToMenuRoutine("O outro jogador saiu da partida.", false));
                }
                else
                {
                    PublishStatus("O outro jogador saiu da sala.");
                }
            }

            return;
        }

        if (_networkManager.IsClient)
        {
            GameLaunchConfig.ConfigureStory(GameLaunchConfig.StoryChapter);
            GameLaunchConfig.SetPendingMenuStatus("A conexao com a sala foi encerrada.");
            ShutdownNetwork(false);
            SceneManager.LoadScene(SceneIds.MainMenu);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneIds.Gameplay)
        {
            _gameManager = null;
            return;
        }

        if (!GameLaunchConfig.IsOnlineMode)
            return;

        _gameManager = FindObjectOfType<GameManager>();
        if (_gameManager == null)
            return;

        _gameManager.ConfigureOnlineMode();

        NotifyGameplayReady();

        if (_networkManager != null && _networkManager.IsHost)
            TryBeginOrRefreshHostGameplay();

        if (_hasSnapshot)
            ApplySnapshotToGameplay(_latestSnapshot);
    }

    private void TryBeginOrRefreshHostGameplay()
    {
        if (_networkManager == null || !_networkManager.IsHost)
            return;

        if (SceneManager.GetActiveScene().name != SceneIds.Gameplay)
            return;

        if (_gameManager == null)
            _gameManager = FindObjectOfType<GameManager>();

        if (_gameManager == null)
            return;

        _gameManager.ConfigureOnlineMode();
        EnsureBoardInitialized();

        if (!_hostGameplayReady || !HasBothPlayers() || !_guestGameplayReady)
            return;

        if (!_previewRoutineStarted)
        {
            _previewRoutineStarted = true;
            StartCoroutine(HostPreviewRoutine());
            return;
        }

        BroadcastSnapshot(BuildTurnStatusMessage());
    }

    private void EnsureBoardInitialized()
    {
        if (_boardInitialized)
            return;

        int totalCards = _gameManager != null && _gameManager.generatedCardsCount > 1 ? _gameManager.generatedCardsCount : 16;
        if (totalCards % 2 != 0)
            totalCards += 1;

        int faceCount = _gameManager != null && _gameManager.cardFace != null && _gameManager.cardFace.Length > 0 ? _gameManager.cardFace.Length : totalCards / 2;
        List<int> values = new List<int>(totalCards);
        for (int pair = 0; pair < totalCards / 2; pair++)
        {
            int value = (pair % faceCount) + 1;
            values.Add(value);
            values.Add(value);
        }

        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temp;
        }

        _boardValues = values.ToArray();
        _boardStates = new int[_boardValues.Length];
        _scores[0] = 0;
        _scores[1] = 0;
        _currentTurnSlot = 0;
        _firstSelectionIndex = -1;
        _gameOver = false;
        _turnLocked = false;
        _previewRunning = false;
        _boardInitialized = true;
    }

    private IEnumerator HostPreviewRoutine()
    {
        _previewRunning = true;

        for (int i = 0; i < _boardStates.Length; i++)
            _boardStates[i] = 1;

        BroadcastSnapshot("Memorize as cartas.");

        float delay = _gameManager != null ? Mathf.Max(1f, _gameManager.previewSeconds) : 2f;
        yield return new WaitForSeconds(delay);

        for (int i = 0; i < _boardStates.Length; i++)
            _boardStates[i] = 0;

        _previewRunning = false;
        _currentTurnSlot = 0;
        BroadcastSnapshot("Partida iniciada. Jogador 1 comeca.");
    }

    private void HandleFlipRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsHost)
            return;

        int cardIndex;
        reader.ReadValueSafe(out cardIndex);
        ProcessFlipRequest(senderClientId, cardIndex);
    }

    private void ProcessFlipRequest(ulong senderClientId, int cardIndex)
    {
        if (!_boardInitialized || _previewRunning || _turnLocked || _gameOver || !HasBothPlayers())
            return;

        if (cardIndex < 0 || cardIndex >= _boardStates.Length)
            return;

        int senderSlot = GetPlayerSlot(senderClientId);
        if (senderSlot != _currentTurnSlot)
        {
            BroadcastSnapshot("Nao e o turno desse jogador.");
            return;
        }

        if (_boardStates[cardIndex] != 0)
        {
            BroadcastSnapshot("Essa carta ja foi revelada.");
            return;
        }

        _boardStates[cardIndex] = 1;

        if (_firstSelectionIndex < 0)
        {
            _firstSelectionIndex = cardIndex;
            BroadcastSnapshot("Primeira carta revelada.");
            return;
        }

        int firstCardIndex = _firstSelectionIndex;
        _firstSelectionIndex = -1;
        _turnLocked = true;

        BroadcastSnapshot("Comparando cartas...");

        if (_boardValues[firstCardIndex] == _boardValues[cardIndex])
            StartCoroutine(ResolveMatchRoutine(senderSlot, firstCardIndex, cardIndex));
        else
            StartCoroutine(ResolveMismatchRoutine(firstCardIndex, cardIndex));
    }

    private IEnumerator ResolveMatchRoutine(int playerSlot, int firstCardIndex, int secondCardIndex)
    {
        yield return new WaitForSeconds(0.6f);

        _boardStates[firstCardIndex] = 2;
        _boardStates[secondCardIndex] = 2;
        _comboStreaks[playerSlot] += 1;
        int pointsEarned = _comboStreaks[playerSlot];
        _scores[playerSlot] += pointsEarned;
        _lastScoreEventSlot = playerSlot;
        _lastPointsEarned = pointsEarned;
        _lastComboValue = _comboStreaks[playerSlot];
        _turnLocked = false;

        if (AllPairsMatched())
        {
            _gameOver = true;
            BroadcastSnapshot(BuildGameOverMessage());
            yield break;
        }

        BroadcastSnapshot("Par encontrado. Combo x" + _comboStreaks[playerSlot] + "  +" + pointsEarned + " ponto(s).");
    }

    private IEnumerator ResolveMismatchRoutine(int firstCardIndex, int secondCardIndex)
    {
        yield return new WaitForSeconds(0.8f);

        _boardStates[firstCardIndex] = 0;
        _boardStates[secondCardIndex] = 0;
        _comboStreaks[_currentTurnSlot] = 0;
        _lastScoreEventSlot = -1;
        _lastPointsEarned = 0;
        _lastComboValue = 0;
        _currentTurnSlot = _currentTurnSlot == 0 ? 1 : 0;
        _turnLocked = false;

        BroadcastSnapshot("Nao formou par. Vez do outro jogador.");
    }

    private void BroadcastSnapshot(string statusMessage)
    {
        if (_networkManager == null || !_networkManager.IsHost || _networkManager.CustomMessagingManager == null || !_boardInitialized)
            return;

        SnapshotState snapshot = BuildSnapshot(statusMessage);
        _latestSnapshot = snapshot;
        _hasSnapshot = true;

        if (_networkManager.IsHost)
            ApplySnapshotToGameplay(snapshot);

        using (FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp))
        {
            writer.WriteValueSafe(snapshot.CardValues.Length);
            for (int i = 0; i < snapshot.CardValues.Length; i++)
                writer.WriteValueSafe(snapshot.CardValues[i]);

            for (int i = 0; i < snapshot.CardStates.Length; i++)
                writer.WriteValueSafe(snapshot.CardStates[i]);

            writer.WriteValueSafe(snapshot.ScorePlayerOne);
            writer.WriteValueSafe(snapshot.ScorePlayerTwo);
            writer.WriteValueSafe(snapshot.CurrentTurnSlot);
            writer.WriteValueSafe(snapshot.HostClientId);
            writer.WriteValueSafe(snapshot.GuestClientId);
            writer.WriteValueSafe(snapshot.PreviewRunning);
            writer.WriteValueSafe(snapshot.WaitingForOpponent);
            writer.WriteValueSafe(snapshot.GameOver);
            writer.WriteValueSafe(snapshot.WinnerSlot);
            writer.WriteValueSafe(snapshot.LastScoreEventSlot);
            writer.WriteValueSafe(snapshot.LastPointsEarned);
            writer.WriteValueSafe(snapshot.LastComboValue);
            writer.WriteValueSafe(snapshot.StatusMessage);

            _networkManager.CustomMessagingManager.SendNamedMessageToAll(StateSyncMessage, writer);
        }
    }

    private void HandleStateSyncMessage(ulong senderClientId, FastBufferReader reader)
    {
        SnapshotState snapshot = new SnapshotState();
        int count;
        reader.ReadValueSafe(out count);

        snapshot.CardValues = new int[count];
        snapshot.CardStates = new int[count];

        for (int i = 0; i < count; i++)
            reader.ReadValueSafe(out snapshot.CardValues[i]);

        for (int i = 0; i < count; i++)
            reader.ReadValueSafe(out snapshot.CardStates[i]);

        reader.ReadValueSafe(out snapshot.ScorePlayerOne);
        reader.ReadValueSafe(out snapshot.ScorePlayerTwo);
        reader.ReadValueSafe(out snapshot.CurrentTurnSlot);
        reader.ReadValueSafe(out snapshot.HostClientId);
        reader.ReadValueSafe(out snapshot.GuestClientId);
        reader.ReadValueSafe(out snapshot.PreviewRunning);
        reader.ReadValueSafe(out snapshot.WaitingForOpponent);
        reader.ReadValueSafe(out snapshot.GameOver);
        reader.ReadValueSafe(out snapshot.WinnerSlot);
        reader.ReadValueSafe(out snapshot.LastScoreEventSlot);
        reader.ReadValueSafe(out snapshot.LastPointsEarned);
        reader.ReadValueSafe(out snapshot.LastComboValue);
        reader.ReadValueSafe(out snapshot.StatusMessage);

        _latestSnapshot = snapshot;
        _hasSnapshot = true;
        ApplySnapshotToGameplay(snapshot);
    }

    private void HandleReturnToMenuMessage(ulong senderClientId, FastBufferReader reader)
    {
        float delayBeforeLeaving;
        reader.ReadValueSafe(out delayBeforeLeaving);

        FixedString128Bytes reason;
        reader.ReadValueSafe(out reason);
        string message = reason.ToString();

        if (_returningToMenu)
            return;

        StartCoroutine(ReturnToMenuRoutine(message, false, delayBeforeLeaving));
    }

    private void HandleGameplayReadyMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsHost)
            return;

        if (senderClientId == _hostClientId)
            _hostGameplayReady = true;
        else if (senderClientId == _guestClientId)
            _guestGameplayReady = true;

        TryBeginOrRefreshHostGameplay();
    }

    private void HandleRematchRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsHost || !_gameOver)
            return;

        BeginRematch();
    }

    private void ApplySnapshotToGameplay(SnapshotState snapshot)
    {
        if (SceneManager.GetActiveScene().name != SceneIds.Gameplay)
            return;

        if (_gameManager == null)
            _gameManager = FindObjectOfType<GameManager>();

        if (_gameManager == null)
            return;

        _gameManager.ConfigureOnlineMode();

        int localSlot = GetLocalPlayerSlot(snapshot);
        _gameManager.ApplyOnlineSnapshot(
            snapshot.CardValues,
            snapshot.CardStates,
            snapshot.ScorePlayerOne,
            snapshot.ScorePlayerTwo,
            snapshot.CurrentTurnSlot,
            localSlot,
            snapshot.PreviewRunning,
            snapshot.WaitingForOpponent,
            snapshot.GameOver,
            snapshot.WinnerSlot,
            snapshot.LastScoreEventSlot,
            snapshot.LastPointsEarned,
            snapshot.LastComboValue,
            snapshot.StatusMessage.ToString());
    }

    private SnapshotState BuildSnapshot(string statusMessage)
    {
        SnapshotState snapshot = new SnapshotState();
        snapshot.CardValues = (int[])_boardValues.Clone();
        snapshot.CardStates = (int[])_boardStates.Clone();
        snapshot.ScorePlayerOne = _scores[0];
        snapshot.ScorePlayerTwo = _scores[1];
        snapshot.CurrentTurnSlot = _currentTurnSlot;
        snapshot.HostClientId = _hostClientId;
        snapshot.GuestClientId = _guestClientId;
        snapshot.PreviewRunning = _previewRunning || _turnLocked;
        snapshot.WaitingForOpponent = !HasBothPlayers();
        snapshot.GameOver = _gameOver;
        snapshot.WinnerSlot = GetWinnerSlot();
        snapshot.LastScoreEventSlot = _lastScoreEventSlot;
        snapshot.LastPointsEarned = _lastPointsEarned;
        snapshot.LastComboValue = _lastComboValue;
        snapshot.StatusMessage = new FixedString128Bytes(statusMessage ?? "");
        return snapshot;
    }

    private int GetWinnerSlot()
    {
        if (!_gameOver)
            return -2;

        if (_scores[0] == _scores[1])
            return -1;

        return _scores[0] > _scores[1] ? 0 : 1;
    }

    private int GetPlayerSlot(ulong clientId)
    {
        if (clientId == _hostClientId)
            return 0;

        if (clientId == _guestClientId)
            return 1;

        return -1;
    }

    private int GetLocalPlayerSlot(SnapshotState snapshot)
    {
        if (_networkManager == null)
            return -1;

        ulong localClientId = _networkManager.LocalClientId;
        if (localClientId == snapshot.HostClientId)
            return 0;

        if (localClientId == snapshot.GuestClientId)
            return 1;

        return -1;
    }

    private bool HasBothPlayers()
    {
        return _hostClientId != ulong.MaxValue && _guestClientId != ulong.MaxValue;
    }

    private bool AllPairsMatched()
    {
        for (int i = 0; i < _boardStates.Length; i++)
        {
            if (_boardStates[i] != 2)
                return false;
        }

        return true;
    }

    private string BuildTurnStatusMessage()
    {
        return _currentTurnSlot == 0 ? "Vez do Jogador 1." : "Vez do Jogador 2.";
    }

    private string BuildGameOverMessage()
    {
        if (_scores[0] == _scores[1])
            return "Empate. Missao concluida.";

        return _scores[0] > _scores[1] ? "Jogador 1 venceu a partida." : "Jogador 2 venceu a partida.";
    }

    private void BeginRematch()
    {
        if (_networkManager == null || !_networkManager.IsHost || !_networkManager.IsListening)
            return;

        ResetMatchStateKeepPlayers();
        PublishStatus("Reiniciando a partida...");
        _networkManager.SceneManager.LoadScene(SceneIds.Gameplay, LoadSceneMode.Single);
    }

    private void ResetMatchStateKeepPlayers()
    {
        _boardInitialized = false;
        _previewRoutineStarted = false;
        _previewRunning = false;
        _turnLocked = false;
        _gameOver = false;
        _firstSelectionIndex = -1;
        _scores[0] = 0;
        _scores[1] = 0;
        _comboStreaks[0] = 0;
        _comboStreaks[1] = 0;
        _lastScoreEventSlot = -1;
        _lastPointsEarned = 0;
        _lastComboValue = 0;
        _currentTurnSlot = 0;
        _boardValues = Array.Empty<int>();
        _boardStates = Array.Empty<int>();
        _hostGameplayReady = false;
        _guestGameplayReady = false;
        _hasSnapshot = false;
        _latestSnapshot = new SnapshotState();
    }

    private void ResetSessionState()
    {
        _gameStarted = false;
        _boardInitialized = false;
        _previewRoutineStarted = false;
        _previewRunning = false;
        _turnLocked = false;
        _gameOver = false;
        _firstSelectionIndex = -1;
        _hostClientId = ulong.MaxValue;
        _guestClientId = ulong.MaxValue;
        _scores[0] = 0;
        _scores[1] = 0;
        _comboStreaks[0] = 0;
        _comboStreaks[1] = 0;
        _lastScoreEventSlot = -1;
        _lastPointsEarned = 0;
        _lastComboValue = 0;
        _currentTurnSlot = 0;
        _boardValues = Array.Empty<int>();
        _boardStates = Array.Empty<int>();
        _hostGameplayReady = false;
        _guestGameplayReady = false;
        _hasSnapshot = false;
        _latestSnapshot = new SnapshotState();
    }

    private void ShutdownNetwork(bool keepStatusMessage)
    {
        UnregisterMessageHandlers();

        if (_networkManager != null && _networkManager.IsListening)
        {
            _manualShutdown = true;
            _networkManager.Shutdown();
            _manualShutdown = false;
        }

        ResetSessionState();

        if (!keepStatusMessage)
            PublishStatus("");

        CurrentJoinCode = "";
        PublishRoomCode("");
    }

    private void PublishStatus(string message)
    {
        CurrentStatusMessage = message ?? "";
        StatusChanged?.Invoke(CurrentStatusMessage);
    }

    private void PublishRoomCode(string roomCode)
    {
        CurrentJoinCode = roomCode ?? "";
        RoomCodeChanged?.Invoke(CurrentJoinCode);
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        BusyStateChanged?.Invoke(IsBusy);
    }

    private void NotifyGameplayReady()
    {
        if (_networkManager == null || !_networkManager.IsListening)
            return;

        if (_networkManager.IsHost)
        {
            _hostGameplayReady = true;
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
        {
            _networkManager.CustomMessagingManager.SendNamedMessage(GameplayReadyMessage, NetworkManager.ServerClientId, writer);
        }
    }

    private struct SnapshotState
    {
        public int[] CardValues;
        public int[] CardStates;
        public int ScorePlayerOne;
        public int ScorePlayerTwo;
        public int CurrentTurnSlot;
        public ulong HostClientId;
        public ulong GuestClientId;
        public bool PreviewRunning;
        public bool WaitingForOpponent;
        public bool GameOver;
        public int WinnerSlot;
        public int LastScoreEventSlot;
        public int LastPointsEarned;
        public int LastComboValue;
        public FixedString128Bytes StatusMessage;
    }
}
