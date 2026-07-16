    using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop this one component on an empty GameObject, press Play, and it creates a
/// playable QTE combat example with a player, three enemies, camera, UI and VFX.
/// It needs the other CombatQTE scripts in this folder but no scene setup or assets.
/// </summary>
public sealed class CombatQTEDemoBootstrap : MonoBehaviour
{
    [Header("Demo")]
    [SerializeField] private bool beginCombatAutomatically = true;
    [SerializeField, Min(0f)] private float combatStartDelay = 1f;

    private readonly Color backgroundColor = new Color(0.055f, 0.075f, 0.13f);
    private readonly Color groundColor = new Color(0.13f, 0.17f, 0.25f);

    private CombatManager combatManager;
    private Transform player;
    private Transform enemyGroup;

    private void Start()
    {
        Camera camera = CreateCamera();
        CreateBackdrop();

        player = CreatePlayer();
        enemyGroup = new GameObject("Demo Enemies").transform;
        CreateEnemy("Enemy 1", new Vector3(-2f, -1.5f, 0f), new Color(0.95f, 0.3f, 0.35f));
        CreateEnemy("Enemy 2", new Vector3(1.2f, -0.2f, 0f), new Color(0.95f, 0.55f, 0.25f));
        CreateEnemy("Enemy 3", new Vector3(4.5f, -1.2f, 0f), new Color(0.76f, 0.35f, 0.95f));

        QTEController qteController = gameObject.AddComponent<QTEController>();
        TargetingSystem targetingSystem = gameObject.AddComponent<TargetingSystem>();
        CombatFeedbackManager feedbackManager = gameObject.AddComponent<CombatFeedbackManager>();
        CinematicCameraController cameraController = camera.GetComponent<CinematicCameraController>();
        if (cameraController == null)
        {
            cameraController = camera.gameObject.AddComponent<CinematicCameraController>();
        }

        cameraController.SetFollowTarget(player);

        targetingSystem.Configure(CreateIndicatorPrefab());
        feedbackManager.Configure(
            CreateEffectPrefab("Slash Effect Prefab", new Color(1f, 0.95f, 0.35f), new Vector3(1.6f, 0.25f, 1f)),
            CreateEffectPrefab("Fail Effect Prefab", new Color(1f, 0.25f, 0.25f), new Vector3(0.9f, 0.9f, 1f)));
        CreateQTECanvas(qteController);

        combatManager = gameObject.AddComponent<CombatManager>();
        DemoPlayerMovement movement = player.gameObject.AddComponent<DemoPlayerMovement>();
        combatManager.Configure(targetingSystem, cameraController, qteController, feedbackManager, movement);

        CreateHelpText();
        if (beginCombatAutomatically)
        {
            StartCoroutine(BeginCombatAfterDelay());
        }
    }

    private IEnumerator BeginCombatAfterDelay()
    {
        yield return new WaitForSecondsRealtime(combatStartDelay);
        combatManager.StartCombatSequence(enemyGroup, player);
    }

    private Camera CreateCamera()
    {
        Camera existing = Camera.main;
        if (existing != null)
        {
            existing.orthographic = true;
            existing.orthographicSize = 5.5f;
            existing.backgroundColor = backgroundColor;
            existing.transform.position = new Vector3(0f, 0f, -10f);
            return existing;
        }

        GameObject cameraObject = new GameObject("Demo Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5.5f;
        camera.backgroundColor = backgroundColor;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    private void CreateBackdrop()
    {
        GameObject ground = CreateSpriteObject("Ground", groundColor, new Vector3(0f, -3.2f, 1f), new Vector3(20f, 1f, 1f));
        ground.GetComponent<SpriteRenderer>().sortingOrder = -2;
    }

    private Transform CreatePlayer()
    {
        GameObject playerObject = CreateSpriteObject("Player", new Color(0.25f, 0.8f, 1f), new Vector3(-5.7f, -1.5f, 0f), Vector3.one);
        playerObject.AddComponent<CircleCollider2D>();
        CreateWorldLabel("PLAYER", playerObject.transform, Color.white);
        return playerObject.transform;
    }

    private void CreateEnemy(string enemyName, Vector3 position, Color color)
    {
        GameObject enemyObject = CreateSpriteObject(enemyName, color, position, new Vector3(1.15f, 1.15f, 1f));
        enemyObject.transform.SetParent(enemyGroup);
        enemyObject.AddComponent<CircleCollider2D>();
        enemyObject.AddComponent<Enemy>();
        CreateWorldLabel("ENEMY", enemyObject.transform, new Color(1f, 0.9f, 0.9f));
    }

    private void CreateQTECanvas(QTEController qteController)
    {
        GameObject canvasObject = new GameObject("QTE Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GameObject containerObject = new GameObject("QTE Arrow Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerObject.transform.SetParent(canvasObject.transform, false);
        RectTransform containerRect = containerObject.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -130f);
        containerRect.sizeDelta = new Vector2(560f, 100f);
        HorizontalLayoutGroup layout = containerObject.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        QTEUIManager uiManager = canvasObject.AddComponent<QTEUIManager>();
        uiManager.Configure(qteController, containerObject.transform, CreateArrowPrefab());
    }

    private GameObject CreateArrowPrefab()
    {
        GameObject arrow = new GameObject("Arrow UI Prefab", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        arrow.transform.SetParent(transform, false);
        RectTransform rect = arrow.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(78f, 78f);
        LayoutElement layoutElement = arrow.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 78f;
        layoutElement.preferredHeight = 78f;
        arrow.GetComponent<Image>().color = Color.gray;

        GameObject labelObject = new GameObject("Arrow Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(arrow.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 58;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.08f, 0.1f, 0.15f);
        label.raycastTarget = false;

        arrow.SetActive(false);
        return arrow;
    }

    private GameObject CreateIndicatorPrefab()
    {
        GameObject indicator = CreateSpriteObject("Target Indicator Prefab", new Color(1f, 0.95f, 0.25f), new Vector3(0f, 1.1f, 0f), new Vector3(0.4f, 0.4f, 1f));
        indicator.transform.SetParent(transform, false);
        indicator.GetComponent<SpriteRenderer>().sortingOrder = 4;
        indicator.SetActive(false);
        return indicator;
    }

    private GameObject CreateEffectPrefab(string name, Color color, Vector3 scale)
    {
        GameObject effect = CreateSpriteObject(name, color, Vector3.zero, scale);
        effect.transform.SetParent(transform, false);
        effect.GetComponent<SpriteRenderer>().sortingOrder = 5;
        effect.SetActive(false);
        return effect;
    }

    private void CreateHelpText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject textObject = new GameObject("Help Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -55f);
        rect.sizeDelta = new Vector2(850f, 60f);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "QTE DEMO  •  화면 중앙의 W/A/S/D 순서대로 입력하세요";
    }

    private static GameObject CreateSpriteObject(string objectName, Color color, Vector3 position, Vector3 scale)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        GameObject gameObject = new GameObject(objectName, typeof(SpriteRenderer));
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<SpriteRenderer>().sprite = sprite;
        return gameObject;
    }

    private static void CreateWorldLabel(string label, Transform parent, Color color)
    {
        GameObject labelObject = new GameObject(label + " Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.text = label;
        text.characterSize = 0.18f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
    }
}

/// <summary>Simple WASD controller used only by the generated demo player.</summary>
public sealed class DemoPlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 4f;

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(horizontal, vertical, 0f).normalized;
        transform.position += input * speed * Time.deltaTime;
    }
}
