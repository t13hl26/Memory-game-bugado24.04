using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public string storySceneName = SceneIds.StoryMenu;
    public string multiplayerSceneName = SceneIds.Gameplay;

    public GameObject mainPanel;
    public GameObject createMatchPanel;
    public GameObject joinMatchPanel;

    public Text roomCodeText;
    public Text statusText;
    public InputField joinCodeInput;

    private string _currentRoomCode = "";
    private RelayMatchController _relayController;
    public bool useTcpLobby = false;
    public TcpLobby.TcpLobbyManager tcpLobbyManager;

    private void Awake()
    {
        _relayController = RelayMatchController.EnsureInstance();
        _relayController.RoomCodeChanged += HandleRoomCodeChanged;
        _relayController.StatusChanged += HandleStatusChanged;

        if (tcpLobbyManager == null)
            tcpLobbyManager = FindObjectOfType<TcpLobby.TcpLobbyManager>();

        BindUI();
        ResetTransientUI();
        ForceMainPanelState();
    }

    private void Start()
    {
        ForceMainPanelState();
    }

    private void OnDestroy()
    {
        if (_relayController == null)
            return;

        _relayController.RoomCodeChanged -= HandleRoomCodeChanged;
        _relayController.StatusChanged -= HandleStatusChanged;
    }

    public void StartStoryMode()
    {
        if (_relayController != null)
            _relayController.CancelPendingSessionInMenu();

        GameLaunchConfig.ConfigureStory(1);
        SceneManager.LoadScene(storySceneName);
    }

    public void OpenCreateMatchPanel()
    {
        _currentRoomCode = "";
        SetRoomCodeDisplay("");
        ShowOnly(createMatchPanel);
        SetStatus("Clique em Criar Sala para gerar o codigo.");
    }

    public async void ConfirmCreateMatch()
    {
        if (useTcpLobby && tcpLobbyManager != null)
        {
            tcpLobbyManager.CreateRoom();
            return;
        }

        if (_relayController == null || _relayController.IsBusy)
            return;

        SetStatus("Criando sala...");

        bool created = await _relayController.CreateMatchAsync();
        if (!created)
            return;

        if (!string.IsNullOrEmpty(_relayController.CurrentJoinCode))
        {
            _currentRoomCode = _relayController.CurrentJoinCode;
            SetRoomCodeDisplay(_currentRoomCode);
        }
    }

    public void OpenJoinMatchPanel()
    {
        if (joinCodeInput != null)
            joinCodeInput.text = "";

        ShowOnly(joinMatchPanel);
        SetStatus("Digite o codigo da sala para entrar.");
    }

    public async void ConfirmJoinMatch()
    {
        if (useTcpLobby && tcpLobbyManager != null)
        {
            tcpLobbyManager.JoinRoom();
            return;
        }

        if (_relayController == null || _relayController.IsBusy)
            return;

        string roomCode = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
        if (string.IsNullOrEmpty(roomCode))
        {
            SetStatus("Informe um codigo de partida valido.");
            return;
        }

        SetStatus("Entrando na sala...");
        await _relayController.JoinMatchAsync(roomCode);
    }

    public void ShowMainPanel()
    {
        if (_relayController != null)
            _relayController.CancelPendingSessionInMenu();

        if (useTcpLobby && tcpLobbyManager != null)
            tcpLobbyManager.LeaveRoom();

        ResetTransientUI();
        ShowOnly(mainPanel);
        ApplyInitialStatus();
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    private void ShowOnly(GameObject activePanel)
    {
        if (mainPanel != null)
            mainPanel.SetActive(activePanel == mainPanel);

        if (createMatchPanel != null)
            createMatchPanel.SetActive(activePanel == createMatchPanel);

        if (joinMatchPanel != null)
            joinMatchPanel.SetActive(activePanel == joinMatchPanel);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void BindUI()
    {
        mainPanel = FindObjectByName("MainPanel");
        createMatchPanel = FindObjectByName("CreateMatchPanel");
        joinMatchPanel = FindObjectByName("JoinMatchPanel");
        roomCodeText = FindText("RoomCode");
        statusText = FindText("StatusText");
        joinCodeInput = FindInputField("JoinCodeInput");

        BindButton("Modo HistoriaButton", StartStoryMode);
        BindButton("Criar PartidaButton", OpenCreateMatchPanel);
        BindButton("Encontrar PartidaButton", OpenJoinMatchPanel);
        BindButton("SairButton", ExitGame);
        BindButton("Criar SalaButton", ConfirmCreateMatch);
        BindButton("Encontrar SalaButton", ConfirmJoinMatch);

        if (createMatchPanel != null)
            BindButtonInside(createMatchPanel.transform, "VoltarButton", ShowMainPanel);

        if (joinMatchPanel != null)
            BindButtonInside(joinMatchPanel.transform, "VoltarButton", ShowMainPanel);
    }

    private void ResetTransientUI()
    {
        _currentRoomCode = "";
        SetRoomCodeDisplay("");

        if (joinCodeInput != null)
            joinCodeInput.text = "";
    }

    private void ForceMainPanelState()
    {
        if (mainPanel == null || createMatchPanel == null || joinMatchPanel == null)
            BindUI();

        ResetTransientUI();
        ShowOnly(mainPanel);
        ApplyInitialStatus();
    }

    private void SetRoomCodeDisplay(string value)
    {
        if (roomCodeText == null)
            return;

        roomCodeText.text = value;
        roomCodeText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private void ApplyInitialStatus()
    {
        string pendingStatus = GameLaunchConfig.ConsumePendingMenuStatus();
        if (!string.IsNullOrEmpty(pendingStatus))
        {
            SetStatus(pendingStatus);
            return;
        }

        if (_relayController != null && !string.IsNullOrEmpty(_relayController.CurrentStatusMessage))
        {
            SetStatus(_relayController.CurrentStatusMessage);
            return;
        }

        SetStatus("Escolha um modo de jogo.");
    }

    private void HandleRoomCodeChanged(string roomCode)
    {
        if (createMatchPanel == null || !createMatchPanel.activeSelf)
            return;

        _currentRoomCode = roomCode ?? "";
        SetRoomCodeDisplay(_currentRoomCode);
    }

    private void HandleStatusChanged(string statusMessage)
    {
        if (string.IsNullOrEmpty(statusMessage))
            return;

        SetStatus(statusMessage);
    }

    private void BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButton(objectName);
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void BindButtonInside(Transform scope, string objectName, UnityEngine.Events.UnityAction action)
    {
        if (scope == null)
            return;

        Transform target = FindDeepChild(scope, objectName);
        if (target == null)
            return;

        Button button = target.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private Button FindButton(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private Text FindText(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private InputField FindInputField(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<InputField>() : null;
    }

    private GameObject FindObjectByName(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.gameObject : null;
    }

    private Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
