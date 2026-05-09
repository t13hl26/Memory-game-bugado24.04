using UnityEngine;

// ============================================================
//  PhaseConfig.cs
//  Criado para: Memory River
//  O que faz: define os dados de cada fase do jogo.
//  Os outros scripts vão ler essas informações para montar
//  o tabuleiro, o timer e as cartas corretas.
// ============================================================

// ---------- Dados de uma carta educativa ----------
// Cada par de cartas tem um nome e uma mensagem curta
// que aparece quando o jogador acerta.
[System.Serializable]
public class CardData
{
    [Tooltip("Nome interno da carta (ex: Capivara)")]
    public string cardName;

    [Tooltip("Sprite que aparece na frente da carta")]
    public Sprite cardSprite;

    [Tooltip("Mensagem educativa exibida ao acertar o par")]
    [TextArea(2, 4)]
    public string educationalMessage;
}

// ---------- Dados de uma fase completa ----------
[System.Serializable]
public class PhaseData
{
    [Header("Identificação")]
    [Tooltip("Nome da fase exibido na tela (ex: Salesópolis)")]
    public string phaseName;

    [Tooltip("Descrição curta exibida antes da fase começar")]
    [TextArea(2, 4)]
    public string phaseDescription;

    [Header("Tabuleiro")]
    [Tooltip("Quantidade de colunas do tabuleiro")]
    public int columns = 4;

    [Tooltip("Quantidade de linhas do tabuleiro")]
    public int rows = 3;

    [Header("Tempo")]
    [Tooltip("Tempo em segundos para completar a fase (90 = 1:30, 30 = fase final)")]
    public float timeLimit = 90f;

    [Header("Visual do Rio")]
    [Tooltip("Cor de fundo que representa o estado do rio nesta fase")]
    public Color riverColor = Color.blue;

    [Tooltip("Intensidade do efeito de lama ao errar (0 = sem lama, 1 = lama total)")]
    [Range(0f, 1f)]
    public float mudIntensity = 0.3f;

    [Header("Cartas desta fase")]
    [Tooltip("Lista de cartas disponíveis para esta fase")]
    public CardData[] availableCards;

    [Header("Sabotagem (apenas fase 3)")]
    [Tooltip("Ativar distrações da Doutora Resíduo nesta fase")]
    public bool enableSabotage = false;

    [Tooltip("Quantas cartas falsas a Doutora pode jogar no tabuleiro")]
    public int maxSabotageCards = 3;
}

// ---------- O ScriptableObject principal ----------
// ScriptableObject é um "arquivo de dados" do Unity.
// Você vai criar um arquivo PhaseConfig no projeto
// e preencher as 3 fases pelo inspetor visual.
[CreateAssetMenu(fileName = "PhaseConfig", menuName = "MemoryRiver/Phase Config")]
public class PhaseConfig : ScriptableObject
{
    [Header("Configuração das Fases")]
    [Tooltip("Preencha exatamente 3 fases: Salesópolis, Zona Rural e São Paulo")]
    public PhaseData[] phases = new PhaseData[3];

    // Retorna os dados de uma fase pelo índice (0, 1 ou 2)
    public PhaseData GetPhase(int index)
    {
        if (index < 0 || index >= phases.Length)
        {
            Debug.LogError($"[PhaseConfig] Fase {index} não existe. O jogo tem {phases.Length} fases.");
            return null;
        }
        return phases[index];
    }

    // Retorna quantas fases existem
    public int TotalPhases => phases.Length;

    // Verifica se um índice é a fase final
    public bool IsFinalPhase(int index) => index == phases.Length - 1;
}
