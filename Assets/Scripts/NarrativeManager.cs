using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// ============================================================
//  NarrativeManager.cs
//  Criado para: Memory River
//  O que faz: exibe as caixas de diálogo dos personagens
//  (Sato, Júlia, Barão do Descarte) antes e depois de cada
//  fase, simulando as cutscenes descritas no GDD.
//  Funciona como um painel de diálogo que aparece sobre
//  o jogo e avança ao toque/clique.
// ============================================================

[System.Serializable]
public class DialogueLine
{
    [Tooltip("Nome do personagem que está falando")]
    public string characterName;

    [Tooltip("Texto da fala")]
    [TextArea(3, 5)]
    public string text;

    [Tooltip("Sprite do personagem (foto/ilustração)")]
    public Sprite characterSprite;

    [Tooltip("Lado que o personagem aparece: true = esquerda, false = direita")]
    public bool isLeft = true;
}

[System.Serializable]
public class DialogueSequence
{
    [Tooltip("Nome desta sequência para identificar no Inspector")]
    public string sequenceName;

    [Tooltip("Linhas de diálogo desta sequência")]
    public DialogueLine[] lines;
}

public class NarrativeManager : MonoBehaviour
{
    // ---------- Singleton ----------
    public static NarrativeManager Instance { get; private set; }

    // ---------- Referências de UI ----------
    // Esses campos serão preenchidos no Inspector apontando
    // para os elementos visuais que você criar na cena

    [Header("Painel de Diálogo")]
    [Tooltip("O painel inteiro que aparece/desaparece (GameObject com Image de fundo)")]
    public GameObject dialoguePanel;

    [Tooltip("Texto do nome do personagem")]
    public TextMeshProUGUI characterNameText;

    [Tooltip("Texto da fala")]
    public TextMeshProUGUI dialogueText;

    [Tooltip("Imagem do personagem no lado esquerdo")]
    public Image characterImageLeft;

    [Tooltip("Imagem do personagem no lado direito")]
    public Image characterImageRight;

    [Tooltip("Texto do botão de avançar (ex: 'Toque para continuar')")]
    public TextMeshProUGUI continueText;

    // ---------- Sequências de diálogo do jogo ----------
    [Header("Sequências de Diálogo")]

    [Tooltip("Diálogo de abertura do jogo (Júlia chega à Secretaria)")]
    public DialogueSequence introSequence;

    [Tooltip("Diálogo antes da Fase 1 - Salesópolis")]
    public DialogueSequence phase1StartSequence;

    [Tooltip("Diálogo antes da Fase 2 - Zona Rural")]
    public DialogueSequence phase2StartSequence;

    [Tooltip("Diálogo antes da Fase 3 - São Paulo")]
    public DialogueSequence phase3StartSequence;

    [Tooltip("Diálogo de vitória final")]
    public DialogueSequence endingSequence;

    // ---------- Estado interno ----------
    private DialogueLine[] currentLines;
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private bool dialogueActive = false;
    private System.Action onDialogueComplete; // função chamada ao terminar

    // Velocidade de digitação (caracteres por segundo)
    [Header("Configurações")]
    [Tooltip("Velocidade que o texto aparece letra por letra")]
    [Range(10f, 80f)]
    public float typingSpeed = 40f;

    // -------------------------------------------------------
    //  Awake
    // -------------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialoguePanel != null)
        {
            CanvasGroup cg = dialoguePanel.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
            else dialoguePanel.SetActive(true);
        }
    }

     // -------------------------------------------------------
    //  Métodos públicos — chamados por outros scripts
    // -------------------------------------------------------

    // Exibe o diálogo de introdução do jogo
    public void ShowIntro(System.Action onComplete = null)
    {
        ShowSequence(introSequence, onComplete);
    }

    // Exibe o diálogo antes de uma fase (0, 1 ou 2)
    public void ShowPhaseStart(int phaseIndex, System.Action onComplete = null)
    {
        switch (phaseIndex)
        {
            case 0: ShowSequence(phase1StartSequence, onComplete); break;
            case 1: ShowSequence(phase2StartSequence, onComplete); break;
            case 2: ShowSequence(phase3StartSequence, onComplete); break;
            default:
                Debug.LogWarning($"[NarrativeManager] Fase {phaseIndex} não tem diálogo definido.");
                onComplete?.Invoke();
                break;
        }
    }

    // Exibe o diálogo de encerramento do jogo
    public void ShowEnding(System.Action onComplete = null)
    {
        ShowSequence(endingSequence, onComplete);
    }

    // Exibe qualquer sequência passada diretamente
    public void ShowSequence(DialogueSequence sequence, System.Action onComplete = null)
    {
        if (sequence == null || sequence.lines == null || sequence.lines.Length == 0)
        {
            Debug.LogWarning("[NarrativeManager] Sequência vazia ou nula. Pulando.");
            onComplete?.Invoke();
            return;
        }

        onDialogueComplete = onComplete;
        currentLines       = sequence.lines;
        currentLineIndex   = 0;
        dialogueActive     = true;

        dialoguePanel.SetActive(true);
        ShowLine(currentLines[0]);
    }

    // -------------------------------------------------------
    //  Chamado ao tocar/clicar na tela durante o diálogo
    // -------------------------------------------------------
    private void Update()
    {
        if (!dialogueActive) return;

        // Detecta toque ou clique do mouse
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            if (isTyping)
            {
                // Se ainda está digitando, mostra o texto completo imediatamente
                SkipTyping();
            }
            else
            {
                // Avança para a próxima linha
                AdvanceLine();
            }
        }
    }

    // -------------------------------------------------------
    //  Exibe uma linha de diálogo
    // -------------------------------------------------------
    private void ShowLine(DialogueLine line)
    {
        // Nome do personagem
        if (characterNameText != null)
            characterNameText.text = line.characterName;

        // Posiciona a imagem do personagem no lado correto
        if (characterImageLeft  != null) characterImageLeft.gameObject.SetActive(line.isLeft);
        if (characterImageRight != null) characterImageRight.gameObject.SetActive(!line.isLeft);

        Image targetImage = line.isLeft ? characterImageLeft : characterImageRight;
        if (targetImage != null && line.characterSprite != null)
            targetImage.sprite = line.characterSprite;

        // Esconde o "toque para continuar" enquanto digita
        if (continueText != null)
            continueText.gameObject.SetActive(false);

        // Inicia o efeito de digitação
        StopAllCoroutines();
        StartCoroutine(TypeText(line.text));
    }

    // -------------------------------------------------------
    //  Efeito de texto aparecendo letra por letra
    // -------------------------------------------------------
    private IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(1f / typingSpeed);
        }

        isTyping = false;

        // Mostra o botão de continuar
        if (continueText != null)
            continueText.gameObject.SetActive(true);
    }

    // -------------------------------------------------------
    //  Pula a digitação e mostra o texto completo
    // -------------------------------------------------------
    private void SkipTyping()
    {
        StopAllCoroutines();
        isTyping = false;

        if (currentLines != null && currentLineIndex < currentLines.Length)
            dialogueText.text = currentLines[currentLineIndex].text;

        if (continueText != null)
            continueText.gameObject.SetActive(true);
    }

    // -------------------------------------------------------
    //  Avança para a próxima linha ou fecha o diálogo
    // -------------------------------------------------------
    private void AdvanceLine()
    {
        currentLineIndex++;

        if (currentLineIndex < currentLines.Length)
        {
            // Ainda há linhas para mostrar
            ShowLine(currentLines[currentLineIndex]);
        }
        else
        {
            // Acabou o diálogo
            CloseDialogue();
        }
    }

    // -------------------------------------------------------
    //  Fecha o painel e executa o callback
    // -------------------------------------------------------
    private void CloseDialogue()
    {
        dialogueActive = false;
        dialoguePanel.SetActive(false);

        Debug.Log("[NarrativeManager] Diálogo encerrado.");

        // Chama a função que estava esperando o diálogo terminar
        // Por exemplo: iniciar a fase, carregar a próxima cena, etc.
        onDialogueComplete?.Invoke();
        onDialogueComplete = null;
    }

    // -------------------------------------------------------
    //  Atalho: Sato elogia o jogador ao acertar um par
    //  Chamado pelo GameManager a cada par correto
    // -------------------------------------------------------
    public void ShowSatoPraise()
    {
        string[] praises = new string[]
        {
            "Sato:  Boa jogada, Júlia!",
            "Sato:  Isso! Você está de olho nas evidências.",
            "Sato:  Excelente! Continue assim.",
            "Sato:  Muito bem. O rio agradece.",
            "Sato:  Perfeito. Isso é trabalho de fiscal."
        };

        int index = Random.Range(0, praises.Length);

        // Exibe apenas no texto educativo por 2 segundos
        // (sem abrir o painel completo para não interromper o gameplay)
        StartCoroutine(ShowQuickMessage(praises[index]));
    }

    // -------------------------------------------------------
    //  Mensagem rápida sem abrir o painel de diálogo
    // -------------------------------------------------------
    private IEnumerator ShowQuickMessage(string message)
    {
        if (dialogueText == null) yield break;

        // Exibe a mensagem temporariamente no texto do diálogo
        // sem ativar o painel completo
        dialogueText.text = message;
        yield return new WaitForSeconds(2f);
        dialogueText.text = "";
    }
}
