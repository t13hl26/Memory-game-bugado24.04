using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public static bool DO_NOT = false;

    [SerializeField] private int _state;
    [SerializeField] private int _cardValue;
    [SerializeField] private bool _initialized = false;
    [SerializeField] private bool _matched = false;
    [SerializeField] private float flipDuration = 0.18f;
    [SerializeField] private float appearDuration = 0.2f;
    [SerializeField] private float disappearDuration = 0.22f;
    [SerializeField] private float appearStartScale = 0.75f;

    private Sprite _cardBack;
    private Sprite _cardFace;
    private GameObject _manager;
    private Image _image;
    private bool _isAnimating;
    private Color _baseColor = Color.white;
    private Coroutine _onlineVisualRoutine;
    private int _lastOnlineVisualState = -1;

    public bool IsAnimating => _isAnimating;

    private void Start()
    {
        _state = 0;
        EnsureReferences();
    }

    public void setupGraphics()
    {
        EnsureReferences();
        _cardBack = _manager.GetComponent<GameManager>().getCardBack();
        _cardFace = _manager.GetComponent<GameManager>().getCardFace(_cardValue);
        ShowFront();
    }

    public void flipCard()
    {
        if (_isAnimating)
            return;

        if (_state == 0)
        {
            _state = 1;
            StartCoroutine(AnimateFlip(_cardFace));
        }
        else
        {
            _state = 0;
            StartCoroutine(AnimateFlip(_cardBack));
        }
    }

    public void ShowFront()
    {
        EnsureReferences();
        _isAnimating = false;
        _state = 1;
        transform.localScale = Vector3.one;
        if (_image != null)
        {
            _image.color = _baseColor;
            _image.sprite = _cardFace;
        }
    }

    public void ShowBack()
    {
        EnsureReferences();
        _isAnimating = false;
        _state = 0;
        transform.localScale = Vector3.one;
        if (_image != null)
        {
            _image.color = _baseColor;
            _image.sprite = _cardBack;
        }
    }

    public void ResetOnlinePresentation(Sprite backSprite)
    {
        EnsureReferences();

        if (_onlineVisualRoutine != null)
        {
            StopCoroutine(_onlineVisualRoutine);
            _onlineVisualRoutine = null;
        }

        _cardBack = backSprite;
        _cardFace = backSprite;
        _matched = false;
        _state = 0;
        _initialized = true;
        _isAnimating = false;
        _lastOnlineVisualState = -1;

        gameObject.SetActive(true);
        transform.localScale = new Vector3(appearStartScale, appearStartScale, 1f);
        SetAlpha(0f);

        if (_image != null)
        {
            _image.sprite = _cardBack;
        }
    }

    public void ApplyOnlineVisual(Sprite faceSprite, Sprite backSprite, int visualState, bool immediate = false)
    {
        EnsureReferences();

        _cardFace = faceSprite != null ? faceSprite : backSprite;
        _cardBack = backSprite;
        _initialized = true;
        _matched = visualState == 2;

        if (_onlineVisualRoutine != null)
        {
            StopCoroutine(_onlineVisualRoutine);
            _onlineVisualRoutine = null;
        }

        int previousState = _lastOnlineVisualState;
        _lastOnlineVisualState = visualState;

        if (immediate)
        {
            ApplyImmediateOnlineState(visualState);
            return;
        }

        if (previousState < 0)
        {
            _onlineVisualRoutine = StartCoroutine(PlayInitialOnlineState(visualState));
            return;
        }

        if (previousState == visualState)
        {
            ApplyImmediateOnlineState(visualState);
            return;
        }

        _onlineVisualRoutine = StartCoroutine(AnimateOnlineStateChange(previousState, visualState));
    }

    public void PrepareForAppear()
    {
        EnsureReferences();
        transform.localScale = new Vector3(appearStartScale, appearStartScale, 1f);
        SetAlpha(0f);
    }

    public IEnumerator PlayAppear(float delay)
    {
        EnsureReferences();

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _isAnimating = true;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(appearStartScale, appearStartScale, 1f);
        Vector3 endScale = Vector3.one;

        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / appearDuration);
            float eased = EaseOutBack(progress);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            SetAlpha(progress);
            yield return null;
        }

        transform.localScale = Vector3.one;
        SetAlpha(1f);
        _isAnimating = false;
    }

    public IEnumerator PlayDisappear()
    {
        EnsureReferences();
        _isAnimating = true;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(0.55f, 0.55f, 1f);

        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / disappearDuration);
            transform.localScale = Vector3.Lerp(startScale, endScale, progress);
            SetAlpha(1f - progress);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        SetAlpha(0f);
        _isAnimating = false;
        gameObject.SetActive(false);
    }

    public int cardValue
    {
        get => _cardValue;
        set { _cardValue = value; }
    }

    public int state
    {
        get => _state;
        set { _state = value; }
    }

    public bool initialized
    {
        get => _initialized;
        set { _initialized = value; }
    }

    public bool matched
    {
        get => _matched;
        set { _matched = value; }
    }

    public void falseCheck()
    {
        StartCoroutine(pause());
    }

    IEnumerator pause()
    {
        yield return new WaitForSeconds(1f);
        _state = 0;
        yield return StartCoroutine(AnimateFlip(_cardBack));
        DO_NOT = false;
    }

    private void EnsureReferences()
    {
        if (_manager == null)
            _manager = GameObject.FindGameObjectWithTag("Manager");

        if (_image == null)
            _image = GetComponent<Image>();

        if (_image != null)
            _baseColor = new Color(_image.color.r, _image.color.g, _image.color.b, 1f);
    }

    private IEnumerator AnimateFlip(Sprite targetSprite)
    {
        EnsureReferences();
        _isAnimating = true;

        Vector3 initialScale = transform.localScale;
        float halfDuration = Mathf.Max(0.01f, flipDuration * 0.5f);
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = new Vector3(Mathf.Lerp(initialScale.x, 0f, progress), initialScale.y, initialScale.z);
            yield return null;
        }

        if (_image != null)
            _image.sprite = targetSprite;

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = new Vector3(Mathf.Lerp(0f, initialScale.x, progress), initialScale.y, initialScale.z);
            yield return null;
        }

        transform.localScale = initialScale;
        _isAnimating = false;
    }

    private IEnumerator PlayInitialOnlineState(int visualState)
    {
        gameObject.SetActive(true);
        transform.localScale = new Vector3(appearStartScale, appearStartScale, 1f);
        SetAlpha(0f);

        if (_image != null)
            _image.sprite = visualState == 0 ? _cardBack : _cardFace;

        yield return StartCoroutine(PlayAppear(0f));

        if (visualState == 2)
        {
            yield return StartCoroutine(PlayDisappear());
        }
        else if (visualState == 1)
        {
            _state = 1;
            ShowFront();
        }
        else
        {
            _state = 0;
            ShowBack();
        }

        _onlineVisualRoutine = null;
    }

    private IEnumerator AnimateOnlineStateChange(int previousState, int visualState)
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.one;
        SetAlpha(1f);

        if (previousState == 0 && visualState == 1)
        {
            _state = 1;
            yield return StartCoroutine(AnimateFlip(_cardFace));
        }
        else if (previousState == 1 && visualState == 0)
        {
            _state = 0;
            yield return StartCoroutine(AnimateFlip(_cardBack));
        }
        else if (visualState == 2)
        {
            if (previousState == 0)
            {
                _state = 1;
                yield return StartCoroutine(AnimateFlip(_cardFace));
            }

            _matched = true;
            yield return StartCoroutine(PlayDisappear());
        }
        else
        {
            ApplyImmediateOnlineState(visualState);
        }

        _onlineVisualRoutine = null;
    }

    private void ApplyImmediateOnlineState(int visualState)
    {
        _isAnimating = false;

        if (visualState == 2)
        {
            _state = 0;
            transform.localScale = Vector3.zero;
            SetAlpha(0f);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        transform.localScale = Vector3.one;
        SetAlpha(1f);

        if (_image == null)
            return;

        if (visualState == 1)
        {
            _state = 1;
            _image.sprite = _cardFace;
        }
        else
        {
            _state = 0;
            _image.sprite = _cardBack;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (_image == null)
            return;

        _image.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, Mathf.Clamp01(alpha));
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
