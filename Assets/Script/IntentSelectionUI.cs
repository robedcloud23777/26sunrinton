using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime cursor ring used while the player holds a proposed combat target.</summary>
[DisallowMultipleComponent]
public sealed class IntentSelectionUI : MonoBehaviour
{
    [SerializeField, Min(24f)] private float ringSize = 72f;
    [SerializeField, Range(1f, 20f)] private float ringThickness = 7f;
    [SerializeField] private Color backgroundColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Color proposalColor = new Color(1f, 0.72f, 0.12f, 0.95f);
    [SerializeField] private Color acceptedColor = Color.white;

    private RectTransform ringRoot;
    private Image progressImage;
    private Texture2D ringTexture;
    private Sprite ringSprite;

    private void Awake()
    {
        BuildRuntimeUI();
        Hide();
    }

    public void Show(Vector2 screenPosition, float progress, bool isReady)
    {
        if (ringRoot == null)
        {
            BuildRuntimeUI();
        }

        ringRoot.gameObject.SetActive(true);
        ringRoot.position = screenPosition;
        progressImage.fillAmount = Mathf.Clamp01(progress);
        progressImage.color = isReady ? acceptedColor : proposalColor;
    }

    public void Hide()
    {
        if (ringRoot != null)
        {
            ringRoot.gameObject.SetActive(false);
        }
    }

    private void BuildRuntimeUI()
    {
        if (ringRoot != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Intent Selection Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        GameObject ringObject = new GameObject("Hold Selection Ring", typeof(RectTransform));
        ringObject.transform.SetParent(canvasObject.transform, false);
        ringRoot = ringObject.GetComponent<RectTransform>();
        ringRoot.sizeDelta = Vector2.one * ringSize;

        ringSprite = CreateRingSprite(128, ringThickness / ringSize);
        CreateRingImage("Background", backgroundColor, false);
        progressImage = CreateRingImage("Progress", proposalColor, true);
    }

    private Image CreateRingImage(string objectName, Color color, bool filled)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(ringRoot, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = ringSprite;
        image.color = color;
        image.raycastTarget = false;
        if (filled)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
        }

        return image;
    }

    private Sprite CreateRingSprite(int resolution, float normalizedThickness)
    {
        ringTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        ringTexture.name = "Runtime Intent Ring";
        ringTexture.wrapMode = TextureWrapMode.Clamp;
        ringTexture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[resolution * resolution];
        float center = (resolution - 1) * 0.5f;
        float outerRadius = center - 1f;
        float innerRadius = outerRadius * (1f - Mathf.Clamp(normalizedThickness, 0.05f, 0.45f));

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                byte alpha = distance >= innerRadius && distance <= outerRadius ? (byte)255 : (byte)0;
                pixels[y * resolution + x] = new Color32(255, 255, 255, alpha);
            }
        }

        ringTexture.SetPixels32(pixels);
        ringTexture.Apply(false, true);
        return Sprite.Create(
            ringTexture,
            new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (ringSprite != null)
        {
            Destroy(ringSprite);
        }

        if (ringTexture != null)
        {
            Destroy(ringTexture);
        }
    }
}
