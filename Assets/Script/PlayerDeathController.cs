using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ends the game when the player's health reaches zero or the player falls
/// below the map. The end presentation uses unscaled DOTween time so it keeps
/// playing after gameplay has been frozen.
/// </summary>
public sealed class PlayerDeathController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatManager healthSource;
    [SerializeField] private Transform player;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Header("Fall Death")]
    [SerializeField] private float fallDeathY = -12f;

    [Header("End Presentation")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 1f;
    [SerializeField, Min(0.01f)] private float endTextFadeDuration = 0.45f;
    [SerializeField, Min(0f)] private float endTextDelay = 0.45f;
    [SerializeField, Min(0f)] private float endHoldDuration = 1.5f;
    [SerializeField] private string endText = "End";
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private Color endTextColor = Color.white;
    [SerializeField, Min(1)] private int endFontSize = 96;

    private Sequence deathSequence;
    private GameObject deathOverlay;
    private bool isEnding;
    private float timeScaleBeforeDeath = 1f;

    public bool IsEnding => isEnding;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        BindHealthSource();
    }

    private void Start()
    {
        BindHealthSource();
        if (healthSource != null && healthSource.CurrentPlayerHealth <= 0)
        {
            BeginDeath();
        }
    }

    private void Update()
    {
        if (isEnding)
        {
            return;
        }

        if (healthSource == null)
        {
            BindHealthSource();
        }

        // Combat moves the player vertically by script, so only normal
        // exploration physics is allowed to trigger the map fall check.
        bool canCheckMapFall = healthSource == null ||
                               healthSource.CurrentState == CombatManager.CombatState.Idle;
        if (canCheckMapFall && player != null && player.position.y <= fallDeathY)
        {
            BeginDeath();
        }
    }

    /// <summary>Allows a future kill zone or hazard to use the same end sequence.</summary>
    public void BeginDeath()
    {
        BeginEnding(endText);
    }

    /// <summary>Runs the shared fade, message, and quit sequence.</summary>
    public void BeginEnding(string message)
    {
        if (isEnding)
        {
            return;
        }

        isEnding = true;
        if (healthSource != null)
        {
            healthSource.enabled = false;
        }

        LockPlayer();
        CreateDeathOverlay(message, out CanvasGroup overlayGroup, out Text endLabel);

        timeScaleBeforeDeath = Time.timeScale;
        Time.timeScale = 0f;

        deathSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(overlayGroup.DOFade(1f, fadeDuration).SetEase(Ease.InOutSine))
            .Join(endLabel.DOFade(1f, endTextFadeDuration)
                .SetDelay(endTextDelay)
                .SetEase(Ease.OutCubic))
            .AppendInterval(endHoldDuration)
            .OnComplete(QuitGame);
    }

    private void HandlePlayerDied()
    {
        BeginDeath();
    }

    private void ResolveReferences()
    {
        if (healthSource == null)
        {
            healthSource = CombatManager.Instance != null
                ? CombatManager.Instance
                : FindAnyObjectByType<CombatManager>();
        }

        if (playerMovement == null && player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        if (player == null && playerMovement != null)
        {
            player = playerMovement.transform;
        }

        if (playerRigidbody == null && player != null)
        {
            playerRigidbody = player.GetComponent<Rigidbody2D>();
        }
    }

    private void BindHealthSource()
    {
        ResolveReferences();
        if (healthSource == null)
        {
            return;
        }

        healthSource.OnPlayerDied -= HandlePlayerDied;
        healthSource.OnPlayerDied += HandlePlayerDied;
    }

    private void LockPlayer()
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.simulated = false;
        }
    }

    private void CreateDeathOverlay(string message, out CanvasGroup overlayGroup, out Text endLabel)
    {
        deathOverlay = new GameObject(
            "Player Death Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));

        Canvas canvas = deathOverlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = deathOverlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(deathOverlay.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.GetComponent<Image>();
        background.color = fadeColor;
        background.raycastTarget = true;

        GameObject labelObject = new GameObject("End Text", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(deathOverlay.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(1000f, 240f);

        endLabel = labelObject.GetComponent<Text>();
        endLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        endLabel.fontSize = Mathf.Max(1, endFontSize);
        endLabel.fontStyle = FontStyle.Bold;
        endLabel.alignment = TextAnchor.MiddleCenter;
        endLabel.text = string.IsNullOrWhiteSpace(message)
            ? (string.IsNullOrWhiteSpace(endText) ? "End" : endText)
            : message;
        endLabel.color = new Color(endTextColor.r, endTextColor.g, endTextColor.b, 0f);
        endLabel.raycastTarget = false;

        overlayGroup = deathOverlay.GetComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = true;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDisable()
    {
        if (healthSource != null)
        {
            healthSource.OnPlayerDied -= HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        deathSequence?.Kill();
        if (isEnding)
        {
            Time.timeScale = timeScaleBeforeDeath;
        }
    }
}
