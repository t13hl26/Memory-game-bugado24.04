using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  SabotageController.cs
//  Criado para: Memory River
//  O que faz: controla as ações de sabotagem da Doutora
//  Resíduo na fase 3 (São Paulo). Ela aparece no canto da
//  tela e lança distrações visuais no tabuleiro conforme
//  o jogador avança, exatamente como descrito no GDD.
// ============================================================

public class SabotageController : MonoBehaviour
{
    // ---------- Singleton ----------
    public static SabotageController Instance { get; private set; }

    // ---------- Referências visuais da Doutora Resíduo ----------
    [Header("Doutora Resíduo - Visual")]
    [Tooltip("GameObject da Doutora Resíduo no canto da tela")]
    public GameObject doctorResidueObject;

    [Tooltip("Imagem/sprite da Doutora Resíduo")]
    public Image doctorResidueImage;

    [Tooltip("Sprite da Doutora Resíduo no estado normal")]
    public Sprite doctorNormalSprite;

    [Tooltip("Sprite da Doutora Resíduo no estado sabotando (rindo)")]
    public Sprite doctorSabotageSprite;

    [Tooltip("Fala da Doutora que aparece ao sabotar")]
    public TextMeshProUGUI doctorSpeechText;

    [Tooltip("Balão de fala da Doutora (fundo do texto)")]
    public GameObject speechBubble;

    // ---------- Referências do tabuleiro ----------
    [Header("Tabuleiro")]
    [Tooltip("Prefab da carta de distração (lixo boiando)")]
    public GameObject distractionCardPrefab;

    [Tooltip("Onde as cartas de distração serão colocadas (o mesmo container do tabuleiro)")]
    public Transform boardContainer;

    // ---------- Configurações de sabotagem ----------
    [Header("Configurações")]
    [Tooltip("Tempo mínimo entre sabotagens (segundos)")]
    public float minTimeBetweenSabotages = 8f;

    [Tooltip("Tempo máximo entre sabotagens (segundos)")]
    public float maxTimeBetweenSabotages = 15f;

    [Tooltip("Quanto tempo a carta de distração fica na tela")]
    public float distractionDuration = 4f;

    [Tooltip("Quantos pares o jogador precisa acertar para a sabotagem começar")]
    public int pairsBeforeSabotageStarts = 2;

    // ---------- Estado interno ----------
    private bool sabotageActive = false;
    private int pairsFoundSoFar = 0;
    private int maxSabotageCards = 3;
    private List<GameObject> activeDistractions = new List<GameObject>();

    // Falas da Doutora ao sabotar
    private string[] sabotagePhrases = new string[]
    {
        "Haha! Tente se concentrar com isso!",
        "O rio é nosso depósito gratuito!",
        "Você nunca vai achar as evidências!",
        "Confusão é o meu talento!",
        "O Barão agradece a sua distração!"
    };

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
    }

    private void Start()
    {
        // Começa com tudo escondido
        if (doctorResidueObject != null)
            doctorResidueObject.SetActive(false);

        if (speechBubble != null)
            speechBubble.SetActive(false);
    }

    // -------------------------------------------------------
    //  Ativa a sabotagem para esta fase
    //  Chamado pelo GameManager quando a fase 3 começa
    // -------------------------------------------------------
    public void ActivateSabotage(int maxCards)
    {
        sabotageActive   = true;
        maxSabotageCards = maxCards;
        pairsFoundSoFar  = 0;

        // Mostra a Doutora Resíduo entrando na tela
        if (doctorResidueObject != null)
        {
            doctorResidueObject.SetActive(true);
            StartCoroutine(DoctorEntrance());
        }

        Debug.Log("[SabotageController] Sabotagem da Doutora Resíduo ativada!");
    }

    // -------------------------------------------------------
    //  Desativa a sabotagem (fase terminou)
    // -------------------------------------------------------
    public void DeactivateSabotage()
    {
        sabotageActive = false;
        StopAllCoroutines();

        // Remove todas as distrações ativas
        foreach (var card in activeDistractions)
            if (card != null) Destroy(card);

        activeDistractions.Clear();

        if (doctorResidueObject != null)
            doctorResidueObject.SetActive(false);

        Debug.Log("[SabotageController] Sabotagem desativada.");
    }

    // -------------------------------------------------------
    //  Chamado pelo GameManager quando o jogador acerta um par
    //  Quanto mais pares acertados, mais frequente a sabotagem
    // -------------------------------------------------------
    public void OnPairFound(int totalPairsFound)
    {
        pairsFoundSoFar = totalPairsFound;

        // A sabotagem só começa após alguns pares acertados
        if (!sabotageActive || pairsFoundSoFar < pairsBeforeSabotageStarts)
            return;

        // Quanto mais pares, maior a chance de sabotar agora
        float sabotageChance = Mathf.Lerp(0.2f, 0.8f, (float)pairsFoundSoFar / 10f);

        if (Random.value < sabotageChance)
            StartCoroutine(TriggerSabotage());
    }

    // -------------------------------------------------------
    //  Rotina principal de sabotagem
    // -------------------------------------------------------
    private IEnumerator TriggerSabotage()
    {
        // Respeita o limite máximo de distrações simultâneas
        if (activeDistractions.Count >= maxSabotageCards)
            yield break;

        // Espera um tempo aleatório antes de sabotar
        float waitTime = Random.Range(minTimeBetweenSabotages, maxTimeBetweenSabotages);
        yield return new WaitForSeconds(waitTime);

        if (!sabotageActive) yield break;

        // Mostra a Doutora "atacando"
        yield return StartCoroutine(ShowDoctorAttack());

        // Cria a carta de distração no tabuleiro
        SpawnDistractionCard();
    }

    // -------------------------------------------------------
    //  Animação da Doutora aparecendo para sabotar
    // -------------------------------------------------------
    private IEnumerator DoctorEntrance()
    {
        // Troca para sprite normal primeiro
        if (doctorResidueImage != null && doctorNormalSprite != null)
            doctorResidueImage.sprite = doctorNormalSprite;

        // Faz ela "entrar" subindo do canto inferior
        RectTransform rect = doctorResidueObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 startPos = rect.anchoredPosition - new Vector2(0, 200f);
            Vector2 endPos   = rect.anchoredPosition;

            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / duration);
                yield return null;
            }

            rect.anchoredPosition = endPos;
        }

        // Fala uma ameaça de entrada
        ShowSpeechBubble("Preparem-se... eu estou chegando!");
        yield return new WaitForSeconds(2f);
        HideSpeechBubble();
    }

    // -------------------------------------------------------
    //  Animação da Doutora ao executar uma sabotagem
    // -------------------------------------------------------
    private IEnumerator ShowDoctorAttack()
    {
        // Troca para sprite de sabotagem
        if (doctorResidueImage != null && doctorSabotageSprite != null)
            doctorResidueImage.sprite = doctorSabotageSprite;

        // Mostra fala aleatória
        string phrase = sabotagePhrases[Random.Range(0, sabotagePhrases.Length)];
        ShowSpeechBubble(phrase);

        // Pequena animação de "pulo" (escala)
        if (doctorResidueObject != null)
            yield return StartCoroutine(BounceAnimation(doctorResidueObject.transform));

        yield return new WaitForSeconds(1.5f);

        HideSpeechBubble();

        // Volta para sprite normal
        if (doctorResidueImage != null && doctorNormalSprite != null)
            doctorResidueImage.sprite = doctorNormalSprite;
    }

    // -------------------------------------------------------
    //  Cria uma carta de distração visual no tabuleiro
    // -------------------------------------------------------
    private void SpawnDistractionCard()
    {
        if (distractionCardPrefab == null || boardContainer == null)
        {
            Debug.LogWarning("[SabotageController] Prefab ou container de distração não configurado.");
            return;
        }

        // Instancia a carta de distração como filha do tabuleiro
        GameObject distraction = Instantiate(distractionCardPrefab, boardContainer);

        // Posição aleatória dentro do tabuleiro
        RectTransform rt = distraction.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(
                Random.Range(-200f, 200f),
                Random.Range(-150f, 150f)
            );
        }

        activeDistractions.Add(distraction);

        // Remove automaticamente após o tempo definido
        StartCoroutine(RemoveDistractionAfterDelay(distraction, distractionDuration));

        Debug.Log($"[SabotageController] Carta de distração criada. Total ativo: {activeDistractions.Count}");
    }

    // -------------------------------------------------------
    //  Remove a carta de distração após um delay
    // -------------------------------------------------------
    private IEnumerator RemoveDistractionAfterDelay(GameObject card, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (card != null)
        {
            activeDistractions.Remove(card);
            Destroy(card);
        }
    }

    // -------------------------------------------------------
    //  Mostra o balão de fala da Doutora
    // -------------------------------------------------------
    private void ShowSpeechBubble(string text)
    {
        if (speechBubble != null)
            speechBubble.SetActive(true);

        if (doctorSpeechText != null)
            doctorSpeechText.text = text;
    }

    // -------------------------------------------------------
    //  Esconde o balão de fala
    // -------------------------------------------------------
    private void HideSpeechBubble()
    {
        if (speechBubble != null)
            speechBubble.SetActive(false);
    }

    // -------------------------------------------------------
    //  Animação simples de "pulo" em escala
    // -------------------------------------------------------
    private IEnumerator BounceAnimation(Transform target)
    {
        Vector3 originalScale = target.localScale;
        Vector3 bigScale      = originalScale * 1.2f;

        float elapsed  = 0f;
        float duration = 0.15f;

        // Cresce
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(originalScale, bigScale, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;

        // Volta
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(bigScale, originalScale, elapsed / duration);
            yield return null;
        }

        target.localScale = originalScale;
    }

    // -------------------------------------------------------
    //  Retorna quantas distrações estão ativas no momento
    //  Usado pelo GameManager para alertar o jogador
    // -------------------------------------------------------
    public int ActiveDistractionCount => activeDistractions.Count;
}
