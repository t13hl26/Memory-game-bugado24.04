using UnityEngine;

// ============================================================
//  ProgressionManager.cs
//  Criado para: Memory River
//  O que faz: guarda o progresso do jogador (qual fase está,
//  quantas estrelas ganhou em cada fase) e salva tudo no
//  dispositivo para não perder ao fechar o jogo.
//  É um Singleton: existe uma única instância em todo o jogo
//  e não é destruída ao trocar de cena.
// ============================================================

public class ProgressionManager : MonoBehaviour
{
    // ---------- Singleton ----------
    // Permite acessar de qualquer script com:
    // ProgressionManager.Instance.MetodoOuVariavel
    public static ProgressionManager Instance { get; private set; }

    // ---------- Estado do jogador ----------
    // Índice da fase atual (0 = Salesópolis, 1 = Zona Rural, 2 = São Paulo)
    public int CurrentPhaseIndex { get; private set; } = 0;

    public void SetCurrentPhase(int index)
    {
        CurrentPhaseIndex = index;
    }

    // Estrelas ganhas em cada fase (índice 0, 1, 2)
    // Valor: 0 = não jogou, 1 a 3 = estrelas conquistadas
    private int[] starsPerPhase = new int[3];

    // Quantas fases foram desbloqueadas (começa com 1)
    public int UnlockedPhases { get; private set; } = 1;

    // Chaves usadas para salvar no PlayerPrefs (sistema de save do Unity)
    private const string KEY_CURRENT_PHASE   = "CurrentPhase";
    private const string KEY_UNLOCKED_PHASES = "UnlockedPhases";
    private const string KEY_STARS_PREFIX    = "Stars_Phase_";

    // -------------------------------------------------------
    //  Awake: executado antes de qualquer Start
    // -------------------------------------------------------
    private void Awake()
    {
        // Garante que só existe uma instância
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // não destrói ao trocar de cena

        LoadProgress(); // carrega o save assim que o jogo abre
    }

    // -------------------------------------------------------
    //  Salvar progresso no dispositivo
    // -------------------------------------------------------
    public void SaveProgress()
    {
        PlayerPrefs.SetInt(KEY_CURRENT_PHASE,   CurrentPhaseIndex);
        PlayerPrefs.SetInt(KEY_UNLOCKED_PHASES, UnlockedPhases);

        for (int i = 0; i < starsPerPhase.Length; i++)
            PlayerPrefs.SetInt(KEY_STARS_PREFIX + i, starsPerPhase[i]);

        PlayerPrefs.Save();
        Debug.Log("[ProgressionManager] Progresso salvo.");
    }

    // -------------------------------------------------------
    //  Carregar progresso salvo
    // -------------------------------------------------------
    private void LoadProgress()
    {
        CurrentPhaseIndex = PlayerPrefs.GetInt(KEY_CURRENT_PHASE,   0);
        UnlockedPhases    = PlayerPrefs.GetInt(KEY_UNLOCKED_PHASES, 1);

        for (int i = 0; i < starsPerPhase.Length; i++)
            starsPerPhase[i] = PlayerPrefs.GetInt(KEY_STARS_PREFIX + i, 0);

        Debug.Log($"[ProgressionManager] Progresso carregado. Fase atual: {CurrentPhaseIndex}, Fases desbloqueadas: {UnlockedPhases}");
    }

    // -------------------------------------------------------
    //  Chamado pelo GameManager ao terminar uma fase
    //  pairsFound  = pares que o jogador acertou
    //  totalPairs  = total de pares do tabuleiro
    //  timeLeft    = segundos restantes quando terminou
    // -------------------------------------------------------
    public void CompletePhase(int phaseIndex, int pairsFound, int totalPairs, float timeLeft)
    {
        // Calcula estrelas (sempre pelo menos 1, máximo 3)
        int stars = CalculateStars(pairsFound, totalPairs, timeLeft);

        // Só atualiza se for melhor que o recorde anterior
        if (stars > starsPerPhase[phaseIndex])
        {
            starsPerPhase[phaseIndex] = stars;
            Debug.Log($"[ProgressionManager] Fase {phaseIndex} concluída com {stars} estrela(s)! Novo recorde.");
        }
        else
        {
            Debug.Log($"[ProgressionManager] Fase {phaseIndex} concluída com {stars} estrela(s). Recorde anterior mantido ({starsPerPhase[phaseIndex]}).");
        }

        // Desbloqueia a próxima fase se ainda não foi desbloqueada
        int nextPhase = phaseIndex + 1;
        if (nextPhase < 3 && nextPhase >= UnlockedPhases)
        {
            UnlockedPhases = nextPhase + 1;
            Debug.Log($"[ProgressionManager] Fase {nextPhase} desbloqueada!");
        }

        // Avança a fase atual
        if (phaseIndex == CurrentPhaseIndex && CurrentPhaseIndex < 2)
            CurrentPhaseIndex = nextPhase;

        SaveProgress();
    }

    // -------------------------------------------------------
    //  Lógica de cálculo de estrelas
    //  3 estrelas: acertou tudo com tempo sobrando
    //  2 estrelas: acertou tudo ou quase, pouco tempo
    //  1 estrela:  completou a fase (sempre garantida)
    // -------------------------------------------------------
    private int CalculateStars(int pairsFound, int totalPairs, float timeLeft)
    {
        float completionRate = (float)pairsFound / totalPairs; // 0.0 a 1.0

        if (completionRate >= 1f && timeLeft > 30f) return 3;
        if (completionRate >= 0.75f)                return 2;
        return 1; // sempre garante pelo menos 1 estrela
    }

    // -------------------------------------------------------
    //  Getters úteis para outros scripts
    // -------------------------------------------------------

    // Retorna as estrelas de uma fase específica
    public int GetStars(int phaseIndex)
    {
        if (phaseIndex < 0 || phaseIndex >= starsPerPhase.Length) return 0;
        return starsPerPhase[phaseIndex];
    }

    // Retorna o total de estrelas acumuladas
    public int GetTotalStars()
    {
        int total = 0;
        foreach (int s in starsPerPhase) total += s;
        return total;
    }

    // Verifica se uma fase está disponível para jogar
    public bool IsPhaseUnlocked(int phaseIndex)
    {
        return phaseIndex < UnlockedPhases;
    }

    // Verifica se o jogador completou o jogo inteiro
    public bool IsGameCompleted()
    {
        return starsPerPhase[2] > 0; // fase 3 (São Paulo) foi concluída
    }

    // -------------------------------------------------------
    //  Apagar save (útil para testes — Menu > Reset)
    // -------------------------------------------------------
    public void ResetProgress()
    {
        CurrentPhaseIndex = 0;
        UnlockedPhases    = 1;
        starsPerPhase     = new int[3];
        PlayerPrefs.DeleteAll();
        Debug.Log("[ProgressionManager] Progresso resetado.");
    }
}
