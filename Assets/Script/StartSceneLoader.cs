using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fades from the start scene into gameplay when its button is clicked.</summary>
public sealed class StartSceneLoader : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private Button sceneLoadButton;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField, Min(0f)] private float fadeDuration = 0.65f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.65f;
    [SerializeField, Min(0f)] private float fadeInDelay = 0.1f;
    [SerializeField] private Color fadeColor = Color.black;

    private bool hasTriggered;
    private GameObject transitionOverlay;

    private void Awake()
    {
        ConfigureUiInputModule();

        if (sceneLoadButton == null)
        {
            sceneLoadButton = GetComponentInChildren<Button>(true);
        }
    }

    private void Update()
    {
        if (hasTriggered || sceneLoadButton == null ||
            !sceneLoadButton.isActiveAndEnabled || !sceneLoadButton.interactable)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame &&
            IsInsideButton(mouse.position.ReadValue()))
        {
            LoadNextScene();
            return;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame &&
            IsInsideButton(touchscreen.primaryTouch.position.ReadValue()))
        {
            LoadNextScene();
            return;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonUp(0) && IsInsideButton(Input.mousePosition))
        {
            LoadNextScene();
        }
#endif
    }

    public void LoadNextScene()
    {
        if (hasTriggered)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName) ||
            !Application.CanStreamedLevelBeLoaded(nextSceneName))
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
            .OnComplete(() => LoadScene(transitionGroup));
    }

    private bool IsInsideButton(Vector2 screenPosition)
    {
        RectTransform buttonRect = sceneLoadButton.transform as RectTransform;
        if (buttonRect == null)
        {
            return false;
        }

        Canvas canvas = sceneLoadButton.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPosition, eventCamera);
    }

    private static void ConfigureUiInputModule()
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

#if ENABLE_LEGACY_INPUT_MANAGER
        // Unity 6000 with Active Input Handling = Both is most reliable for a
        // classic uGUI start button when exactly one Standalone module owns UI input.
        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule != null)
        {
            inputSystemModule.enabled = false;
        }

        StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule == null)
        {
            standaloneModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        standaloneModule.enabled = true;
#else
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
#endif
    }

    private void LoadScene(CanvasGroup transitionGroup)
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName);
        if (loadOperation == null)
        {
            hasTriggered = false;
            return;
        }

        GameObject overlayToDestroy = transitionOverlay;
        loadOperation.completed += _ =>
        {
            if (transitionGroup == null)
            {
                return;
            }

            transitionGroup.alpha = 1f;
            transitionGroup.DOFade(0f, fadeInDuration)
                .SetDelay(fadeInDelay)
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
