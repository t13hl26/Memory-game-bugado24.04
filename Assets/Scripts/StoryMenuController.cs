using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StoryMenuController : MonoBehaviour
{
    public string gameplaySceneName = SceneIds.Gameplay;
    public string mainMenuSceneName = SceneIds.MainMenu;

    [Header("Textos do menu")]
    public Text chapterTitleText;
    public Text chapterDescriptionText;
    public Text progressText;
    public Text statusText;

    [Header("Configuração das fases")]
    public PhaseConfig phaseConfig;

    // Fase selecionada atualmente no menu (0, 1 ou 2)
    private int _selectedPhase = 0;

    // Nomes e descrições de cada fase
    private string[] _phaseNames = { "Capítulo 1 - Salesópolis", "Capítulo 2 - Zona Rural", "Capítulo 3 - São Paulo" };
    private string[] _phaseDescriptions =
    {
        "Restaure a fauna e flora nativa do Rio Tietê!",
        "Identifique e remova os resíduos das margens do rio!",
        "Encontre os filtros e leis ambientais antes que a poluição tome conta!"
    };

    private void Awake()
    {
        BindUI();

        // Abre direto na fase que o jogador está atualmente
        int currentPhase = ProgressionManager.Instance != null ? ProgressionManager.Instance.CurrentPhaseIndex : 0;
        SelectPhase(currentPhase);
    }

    private void Start()
    {
        // Atualiza a fase ao voltar para o menu
        int currentPhase = ProgressionManager.Instance != null ? ProgressionManager.Instance.CurrentPhaseIndex : 0;
        SelectPhase(currentPhase);
    }

    // -------------------------------------------------------
    //  Seleciona uma fase e atualiza a tela
    // -------------------------------------------------------
    public void SelectPhase(int phaseIndex)
    {
        _selectedPhase = phaseIndex;
        RefreshTexts();
    }

    // -------------------------------------------------------
    //  Inicia a fase selecionada
    // -------------------------------------------------------
    public void StartSelectedPhase()
    {
        Debug.Log("[StoryMenu] Botão clicado! Fase selecionada: " + _selectedPhase);

        if (ProgressionManager.Instance != null && !ProgressionManager.Instance.IsPhaseUnlocked(_selectedPhase))
        {
            Debug.Log("[StoryMenu] Fase bloqueada!");
            if (statusText != null)
                statusText.text = "Complete a fase anterior primeiro!";
            return;
        }

        GameLaunchConfig.ConfigureStory(_selectedPhase + 1);

        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.SetCurrentPhase(_selectedPhase);

        Debug.Log("[StoryMenu] Chamando SceneManager.LoadScene: " + gameplaySceneName);
        SceneManager.LoadScene(gameplaySceneName);
        Debug.Log("[StoryMenu] Após LoadScene — isso não deveria aparecer");
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // -------------------------------------------------------
    //  Atualiza os textos na tela conforme a fase selecionada
    // -------------------------------------------------------
    private void RefreshTexts()
    {
        if (chapterTitleText != null)
            chapterTitleText.text = _phaseNames[_selectedPhase];

        if (chapterDescriptionText != null)
            chapterDescriptionText.text = _phaseDescriptions[_selectedPhase];

        // Mostra estrelas da fase selecionada
        if (progressText != null)
        {
            int stars = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetStars(_selectedPhase) : 0;
            int total = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetTotalStars() : 0;
            progressText.text = "Estrelas: " + GetStarString(stars) + "   |   Total: " + total + "/9";
        }

        // Mostra status da fase
        if (statusText != null)
        {
            bool unlocked = ProgressionManager.Instance == null || ProgressionManager.Instance.IsPhaseUnlocked(_selectedPhase);
            statusText.text = unlocked ? "Fase disponível! Clique em Jogar." : "🔒 Complete a fase anterior para desbloquear.";
        }
    }

    // -------------------------------------------------------
    //  Converte número de estrelas em texto visual
    // -------------------------------------------------------
    private string GetStarString(int stars)
    {
        string result = "";
        for (int i = 0; i < 3; i++)
            result += i < stars ? "★" : "☆";
        return result;
    }

    // -------------------------------------------------------
    //  Conecta os botões e textos
    // -------------------------------------------------------
    private void BindUI()
    {
        // Atualiza o texto do botão de jogar
        Debug.Log("[StoryMenu] BindUI chamado. Procurando botão...");
        Button startButton = FindButton("Jogar Capitulo 1Button");
        Debug.Log("[StoryMenu] Botão encontrado no BindUI: " + (startButton != null));

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartSelectedPhase);
            
        }

        // Botão voltar
        Button backButton = FindButton("VoltarButton");
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(BackToMainMenu);
        }

        // Botões de seleção de fase (se existirem)
        Button phase1Button = FindButton("Phase1Button");
        if (phase1Button != null) { phase1Button.onClick.RemoveAllListeners(); phase1Button.onClick.AddListener(() => SelectPhase(0)); }

        Button phase2Button = FindButton("Phase2Button");
        if (phase2Button != null) { phase2Button.onClick.RemoveAllListeners(); phase2Button.onClick.AddListener(() => SelectPhase(1)); }

        Button phase3Button = FindButton("Phase3Button");
        if (phase3Button != null) { phase3Button.onClick.RemoveAllListeners(); phase3Button.onClick.AddListener(() => SelectPhase(2)); }

        // Textos
        if (chapterTitleText == null) chapterTitleText = FindText("ChapterTitle");
        if (chapterDescriptionText == null) chapterDescriptionText = FindText("ChapterDescription");
        if (progressText == null) progressText = FindText("Progress");
        if (statusText == null) statusText = FindText("Status");
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

    private Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent.name == objectName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null) return found;
        }
        return null;
    }
}