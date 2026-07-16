using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class Final : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private Button sceneLoadButton;
    [SerializeField] private CanvasGroup fadeCanvasGroup; // 검은 풀스크린 이미지의 CanvasGroup
    [SerializeField, Min(0f)] private float fadeDuration = 0.65f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.65f;
    [SerializeField, Min(0f)] private float fadeInDelay = 0.1f;
    [SerializeField] private Color fadeColor = Color.black;

    private bool hasTriggered = false;
    private GameObject transitionOverlay;

    private void Awake()
    {
        RestoreUiInputActions();

        if (sceneLoadButton == null)
        {
            sceneLoadButton = GetComponentInChildren<Button>(true);
        }

        if (sceneLoadButton != null)
        {
            sceneLoadButton.onClick.RemoveListener(LoadNextScene);
            sceneLoadButton.onClick.AddListener(LoadNextScene);
        }
    }

    private void OnDestroy()
    {
        if (sceneLoadButton != null)
        {
            sceneLoadButton.onClick.RemoveListener(LoadNextScene);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        LoadNextScene();
    }

    /// <summary>Connect this method to a UI Button OnClick event.</summary>
    public void LoadNextScene()
    {
        if (hasTriggered)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"Scene transition failed: '{nextSceneName}' is not in Build Settings.", this);
            return;
        }

        hasTriggered = true;
        CanvasGroup transitionGroup = GetOrCreateTransitionOverlay();
        transitionGroup.DOKill();
        transitionGroup.alpha = 0f;
        transitionGroup.blocksRaycasts = true;

        transitionGroup.DOFade(1f, fadeDuration)
            .SetUpdate(true)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName);
                if (loadOperation == null)
                {
                    hasTriggered = false;
                    return;
                }

                GameObject overlayToDestroy = transitionOverlay;
                float revealDuration = fadeInDuration;
                float revealDelay = fadeInDelay;
                loadOperation.completed += _ =>
                {
                    if (transitionGroup == null)
                    {
                        return;
                    }

                    transitionGroup.alpha = 1f;
                    transitionGroup.DOFade(0f, revealDuration)
                        .SetDelay(revealDelay)
                        .SetUpdate(true)
                        .SetEase(Ease.OutCubic)
                        .OnComplete(() =>
                        {
                            transitionGroup.blocksRaycasts = false;
                            if (overlayToDestroy != null)
                            {
                                Destroy(overlayToDestroy);
                            }
                        });
                };
            });
    }

    private static void RestoreUiInputActions()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            eventSystem = FindAnyObjectByType<EventSystem>();
        }

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (inputModule.actionsAsset != null &&
            inputModule.point != null &&
            inputModule.leftClick != null)
        {
            return;
        }

        bool wasEnabled = inputModule.enabled;
        inputModule.enabled = false;
        inputModule.AssignDefaultActions();
        inputModule.enabled = wasEnabled;
    }

    private CanvasGroup GetOrCreateTransitionOverlay()
    {
        if (fadeCanvasGroup != null &&
            fadeCanvasGroup.transform.parent == null &&
            fadeCanvasGroup.GetComponent<Canvas>() != null)
        {
            transitionOverlay = fadeCanvasGroup.gameObject;
            DontDestroyOnLoad(transitionOverlay);
            return fadeCanvasGroup;
        }

        transitionOverlay = new GameObject(
            "Persistent Scene Transition",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        DontDestroyOnLoad(transitionOverlay);

        Canvas canvas = transitionOverlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = transitionOverlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject fadeImageObject = new GameObject("Fade Image", typeof(RectTransform), typeof(Image));
        fadeImageObject.transform.SetParent(transitionOverlay.transform, false);
        RectTransform fadeRect = fadeImageObject.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        Image fadeImage = fadeImageObject.GetComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = true;

        fadeCanvasGroup = transitionOverlay.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.interactable = false;
        fadeCanvasGroup.blocksRaycasts = true;
        return fadeCanvasGroup;
    }

}
