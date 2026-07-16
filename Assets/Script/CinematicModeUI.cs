using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CinematicModeUI : MonoBehaviour
{
    public static CinematicModeUI Instance { get; private set; }

    [Header("Letterbox")]
    [SerializeField] private RectTransform topLetterbox;
    [SerializeField] private RectTransform bottomLetterbox;
    [SerializeField] private float letterboxHeight = 120f;
    [SerializeField] private float letterboxDuration = 0.6f;

    [Header("Vignette")]
    [SerializeField] private CanvasGroup vignetteGroup;
    [SerializeField] private float vignetteTargetAlpha = 0.6f;

    [Header("Gameplay UI")]
    [SerializeField] private CanvasGroup gameplayUIGroup;

    [Header("Player Status HUD")]
    [SerializeField] private Image[] healthIcons;
    [SerializeField] private Scrollbar trustBar;
    [SerializeField, Range(0f, 1f)] private float emptyHealthIconAlpha = 0.18f;

    private Sequence cinematicSequence;
    private CombatManager healthSource;
    private TrustManager trustSource;

    public bool IsCinematicActive { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BindStatusHUD();
    }

    private void BindStatusHUD()
    {
        healthSource = CombatManager.Instance;
        if (healthSource != null)
        {
            healthSource.OnPlayerHealthChanged -= UpdateHealthHUD;
            healthSource.OnPlayerHealthChanged += UpdateHealthHUD;
            UpdateHealthHUD(healthSource.CurrentPlayerHealth, healthSource.MaximumPlayerHealth);
        }

        trustSource = TrustManager.Instance;
        if (trustSource != null)
        {
            trustSource.OnTrustChanged -= HandleTrustChanged;
            trustSource.OnTrustChanged += HandleTrustChanged;
            UpdateTrustHUD(trustSource.CurrentTrust);
        }

        if (trustBar != null)
        {
            trustBar.interactable = false;
        }
    }

    private void UpdateHealthHUD(int currentHealth, int maximumHealth)
    {
        if (healthIcons == null || healthIcons.Length == 0)
        {
            return;
        }

        float safeMaximumHealth = Mathf.Max(1, maximumHealth);
        float healthPerIcon = safeMaximumHealth / healthIcons.Length;
        float safeCurrentHealth = Mathf.Clamp(currentHealth, 0, maximumHealth);
        for (int index = 0; index < healthIcons.Length; index++)
        {
            Image icon = healthIcons[index];
            if (icon == null)
            {
                continue;
            }

            float iconHealth = safeCurrentHealth - index * healthPerIcon;
            float fillAmount = Mathf.Clamp01(iconHealth / healthPerIcon);
            Color color = icon.color;

            if (fillAmount <= 0f)
            {
                // Keep an empty heart silhouette visible instead of removing it completely.
                icon.type = Image.Type.Simple;
                icon.fillAmount = 1f;
                color.a = emptyHealthIconAlpha;
            }
            else
            {
                icon.type = Image.Type.Filled;
                icon.fillMethod = Image.FillMethod.Horizontal;
                icon.fillOrigin = (int)Image.OriginHorizontal.Left;
                icon.fillClockwise = true;
                icon.fillAmount = fillAmount;
                color.a = 1f;
            }

            icon.color = color;
        }
    }

    private void HandleTrustChanged(float previousTrust, float currentTrust)
    {
        UpdateTrustHUD(currentTrust);
    }

    private void UpdateTrustHUD(float currentTrust)
    {
        if (trustBar == null)
        {
            return;
        }

        float normalizedTrust = Mathf.Clamp01(currentTrust / 100f);
        trustBar.size = normalizedTrust;
        trustBar.value = 0f;
    }

    public void EnterCinematicMode()
    {
        if (IsCinematicActive) return;
        IsCinematicActive = true;

        cinematicSequence?.Kill();

        topLetterbox.sizeDelta = new Vector2(topLetterbox.sizeDelta.x, 0f);
        bottomLetterbox.sizeDelta = new Vector2(bottomLetterbox.sizeDelta.x, 0f);
        vignetteGroup.alpha = 0f;

        cinematicSequence = DOTween.Sequence();

        cinematicSequence.Append(
            DOTween.To(() => topLetterbox.sizeDelta.y,
                       h => topLetterbox.sizeDelta = new Vector2(topLetterbox.sizeDelta.x, h),
                       letterboxHeight, letterboxDuration).SetEase(Ease.OutCubic)
        );
        cinematicSequence.Join(
            DOTween.To(() => bottomLetterbox.sizeDelta.y,
                       h => bottomLetterbox.sizeDelta = new Vector2(bottomLetterbox.sizeDelta.x, h),
                       letterboxHeight, letterboxDuration).SetEase(Ease.OutCubic)
        );
        cinematicSequence.Join(gameplayUIGroup.DOFade(0f, letterboxDuration));
        cinematicSequence.Join(vignetteGroup.DOFade(vignetteTargetAlpha, letterboxDuration));

        cinematicSequence.OnStart(() =>
        {
            gameplayUIGroup.interactable = false;
            gameplayUIGroup.blocksRaycasts = false;
        });

        cinematicSequence.Play();
    }

    public void ExitCinematicMode()
    {
        if (!IsCinematicActive) return;

        cinematicSequence?.Kill();
        cinematicSequence = DOTween.Sequence();

        cinematicSequence.Append(
            DOTween.To(() => topLetterbox.sizeDelta.y,
                       h => topLetterbox.sizeDelta = new Vector2(topLetterbox.sizeDelta.x, h),
                       0f, letterboxDuration).SetEase(Ease.InCubic)
        );
        cinematicSequence.Join(
            DOTween.To(() => bottomLetterbox.sizeDelta.y,
                       h => bottomLetterbox.sizeDelta = new Vector2(bottomLetterbox.sizeDelta.x, h),
                       0f, letterboxDuration).SetEase(Ease.InCubic)
        );
        cinematicSequence.Join(vignetteGroup.DOFade(0f, letterboxDuration));
        cinematicSequence.Join(gameplayUIGroup.DOFade(1f, letterboxDuration));

        cinematicSequence.OnComplete(() =>
        {
            gameplayUIGroup.interactable = true;
            gameplayUIGroup.blocksRaycasts = true;
            IsCinematicActive = false;
        });

        cinematicSequence.Play();
    }

    private void OnDestroy()
    {
        if (healthSource != null)
        {
            healthSource.OnPlayerHealthChanged -= UpdateHealthHUD;
        }

        if (trustSource != null)
        {
            trustSource.OnTrustChanged -= HandleTrustChanged;
        }

        if (Instance == this)
            cinematicSequence?.Kill();
    }
}
