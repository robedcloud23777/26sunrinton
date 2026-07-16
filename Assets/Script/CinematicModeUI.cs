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

    private Sequence cinematicSequence;

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
        if (Instance == this)
            cinematicSequence?.Kill();
    }
}