using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A minimal, self-contained QTE verification scene. Add this component to an
/// empty GameObject and press Play. It starts a new random arrow sequence on
/// launch and after each success or failure.
/// </summary>
public sealed class QTEPatternDemoBootstrap : MonoBehaviour
{
    [SerializeField, Min(0f)] private float nextRoundDelay = 0.75f;

    private QTEController qteController;
    private Text patternText;
    private Text statusText;
    private Coroutine nextRoundRoutine;

    private void Start()
    {
        CreateCamera();
        CreateUI();

        qteController = gameObject.AddComponent<QTEController>();
        qteController.OnQTEGenerated += ShowRandomPattern;
        qteController.OnQTEKeyHit += ShowKeyProgress;
        qteController.OnQTESuccess += ShowSuccess;
        qteController.OnQTEFail += ShowFailure;

        StartNextRound();
    }

    private void Update()
    {
        if (!qteController.IsActive && Input.GetKeyDown(KeyCode.Space))
        {
            StartNextRound();
        }
    }

    private void OnDisable()
    {
        if (qteController == null)
        {
            return;
        }

        qteController.OnQTEGenerated -= ShowRandomPattern;
        qteController.OnQTEKeyHit -= ShowKeyProgress;
        qteController.OnQTESuccess -= ShowSuccess;
        qteController.OnQTEFail -= ShowFailure;
    }

    private void StartNextRound()
    {
        if (nextRoundRoutine != null)
        {
            StopCoroutine(nextRoundRoutine);
            nextRoundRoutine = null;
        }

        if (qteController != null && !qteController.IsActive)
        {
            statusText.text = "새 랜덤 패턴을 생성합니다...";
            statusText.color = Color.white;
            qteController.StartQTE();
        }
    }

    private void ShowRandomPattern(IReadOnlyList<KeyCode> keys)
    {
        StringBuilder pattern = new StringBuilder();
        for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            if (pattern.Length > 0)
            {
                pattern.Append("   ");
            }

            pattern.Append(qteController != null && qteController.IsKeyHidden(keyIndex)
                ? "?"
                : ToArrowSymbol(keys[keyIndex]));
        }

        patternText.text = pattern.ToString();
        statusText.text = "표시된 방향키를 순서대로 입력하세요";
        statusText.color = Color.white;
    }

    private void ShowKeyProgress(int keyIndex)
    {
        statusText.text = $"입력 성공: {keyIndex + 1}번째 키";
        statusText.color = new Color(0.35f, 1f, 0.55f);
    }

    private void ShowSuccess()
    {
        statusText.text = "성공! 다음 랜덤 패턴을 준비합니다.";
        statusText.color = new Color(0.35f, 1f, 0.55f);
        QueueNextRound();
    }

    private void ShowFailure()
    {
        statusText.text = "실패! 다음 랜덤 패턴을 준비합니다.";
        statusText.color = new Color(1f, 0.35f, 0.35f);
        QueueNextRound();
    }

    private void QueueNextRound()
    {
        if (nextRoundRoutine != null)
        {
            StopCoroutine(nextRoundRoutine);
        }

        nextRoundRoutine = StartCoroutine(StartNextRoundAfterDelay());
    }

    private IEnumerator StartNextRoundAfterDelay()
    {
        yield return new WaitForSecondsRealtime(nextRoundDelay);
        nextRoundRoutine = null;
        StartNextRound();
    }

    private void CreateCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("QTE Demo Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.backgroundColor = new Color(0.06f, 0.08f, 0.14f);
        camera.clearFlags = CameraClearFlags.SolidColor;
    }

    private void CreateUI()
    {
        GameObject canvasObject = new GameObject(
            "QTE Pattern Demo Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        CreateText(canvas.transform, "Title", "RANDOM QTE PATTERN DEMO", 32, new Vector2(0f, 180f), Color.white);
        patternText = CreateText(canvas.transform, "Pattern", "", 96, Vector2.zero, new Color(1f, 0.9f, 0.3f));
        statusText = CreateText(canvas.transform, "Status", "", 28, new Vector2(0f, -115f), Color.white);
        CreateText(canvas.transform, "Hint", "성공 또는 실패 후 새 패턴이 자동 생성됩니다.  Space: 즉시 다시 시작", 18, new Vector2(0f, -200f), new Color(0.7f, 0.75f, 0.85f));
    }

    private static Text CreateText(Transform parent, string name, string value, int fontSize, Vector2 position, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1000f, 120f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = value;
        return text;
    }

    private static string ToArrowSymbol(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            default: return key.ToString();
        }
    }
}
