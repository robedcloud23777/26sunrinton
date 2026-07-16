using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders QTE keycaps and reacts to QTEController events only.</summary>
public sealed class QTEUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QTEController qteController;
    [SerializeField] private Transform qteContainer;
    [SerializeField] private GameObject arrowPrefab;

    [Header("Feedback")]
    [SerializeField] private Color waitingColor = Color.gray;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;
    [SerializeField, Min(0f)] private float failDisplayDuration = 0.3f;
    [SerializeField, Min(0f)] private float popDuration = 0.15f;
    [SerializeField, Min(0f)] private float shakeDistance = 20f;

    private readonly List<Image> activeArrows = new List<Image>();
    private Coroutine clearAfterFailure;

    private void OnEnable()
    {
        SubscribeToQTE(qteController);
    }

    private void OnDisable()
    {
        UnsubscribeFromQTE(qteController);
        ClearUI();
    }

    public void SubscribeToQTE(QTEController qte)
    {
        if (qte == null)
        {
            return;
        }

        qte.OnQTEGenerated -= DisplayQTEKeys;
        qte.OnQTEKeyHit -= UpdateKeyColor;
        qte.OnQTEFail -= ShowFailure;
        qte.OnQTESuccess -= ClearUI;

        qte.OnQTEGenerated += DisplayQTEKeys;
        qte.OnQTEKeyHit += UpdateKeyColor;
        qte.OnQTEFail += ShowFailure;
        qte.OnQTESuccess += ClearUI;
    }

    /// <summary>Configures the manager for a scene assembled at runtime.</summary>
    public void Configure(QTEController qte, Transform container, GameObject prefab)
    {
        UnsubscribeFromQTE(qteController);
        qteController = qte;
        qteContainer = container;
        arrowPrefab = prefab;
        SubscribeToQTE(qteController);
    }

    public void UnsubscribeFromQTE(QTEController qte)
    {
        if (qte == null)
        {
            return;
        }

        qte.OnQTEGenerated -= DisplayQTEKeys;
        qte.OnQTEKeyHit -= UpdateKeyColor;
        qte.OnQTEFail -= ShowFailure;
        qte.OnQTESuccess -= ClearUI;
    }

    private void DisplayQTEKeys(IReadOnlyList<KeyCode> keys)
    {
        ClearUI();
        if (keys == null || qteContainer == null || arrowPrefab == null)
        {
            return;
        }

        for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            KeyCode key = keys[keyIndex];
            bool isHidden = qteController != null && qteController.IsKeyHidden(keyIndex);
            GameObject arrowObject = Instantiate(arrowPrefab, qteContainer);
            arrowObject.SetActive(true);
            Image arrowImage = arrowObject.GetComponent<Image>();
            if (arrowImage == null)
            {
                Destroy(arrowObject);
                continue;
            }

            arrowImage.color = waitingColor;

            Text arrowLabel = arrowObject.GetComponentInChildren<Text>();
            if (arrowLabel != null)
            {
                arrowLabel.text = isHidden ? "?" : GetLabelForKey(key);
            }

            activeArrows.Add(arrowImage);
            StartCoroutine(PopIn(arrowObject.transform));
        }
    }

    private void UpdateKeyColor(int keyIndex)
    {
        if (keyIndex >= 0 && keyIndex < activeArrows.Count && activeArrows[keyIndex] != null)
        {
            Image arrow = activeArrows[keyIndex];
            arrow.color = successColor;
            StartCoroutine(Pulse(arrow.transform));
        }
    }

    private void ShowFailure()
    {
        foreach (Image arrow in activeArrows)
        {
            if (arrow != null)
            {
                arrow.color = failColor;
            }
        }

        if (clearAfterFailure != null)
        {
            StopCoroutine(clearAfterFailure);
        }

        clearAfterFailure = StartCoroutine(FailFeedbackRoutine());
    }

    private IEnumerator PopIn(Transform arrow)
    {
        if (arrow == null)
        {
            yield break;
        }

        arrow.localScale = Vector3.zero;
        if (popDuration <= 0f)
        {
            arrow.localScale = Vector3.one;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < popDuration && arrow != null)
        {
            elapsed += Time.unscaledDeltaTime;
            arrow.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, elapsed / popDuration);
            yield return null;
        }

        if (arrow != null)
        {
            arrow.localScale = Vector3.one;
        }
    }

    private IEnumerator Pulse(Transform arrow)
    {
        if (arrow == null)
        {
            yield break;
        }

        Vector3 originalScale = Vector3.one;
        arrow.localScale = originalScale * 1.2f;
        yield return new WaitForSecondsRealtime(0.08f);
        if (arrow != null)
        {
            arrow.localScale = originalScale;
        }
    }

    private IEnumerator FailFeedbackRoutine()
    {
        Vector3 originalPosition = qteContainer != null ? qteContainer.localPosition : Vector3.zero;
        float elapsed = 0f;
        while (elapsed < failDisplayDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (qteContainer != null)
            {
                float x = Mathf.Sin(elapsed * 140f) * shakeDistance;
                qteContainer.localPosition = originalPosition + new Vector3(x, 0f, 0f);
            }

            yield return null;
        }

        if (qteContainer != null)
        {
            qteContainer.localPosition = originalPosition;
        }

        ClearUI();
        clearAfterFailure = null;
    }

    private void ClearUI()
    {
        if (clearAfterFailure != null)
        {
            StopCoroutine(clearAfterFailure);
            clearAfterFailure = null;
        }

        foreach (Image arrow in activeArrows)
        {
            if (arrow != null)
            {
                Destroy(arrow.gameObject);
            }
        }

        activeArrows.Clear();
    }

    private static string GetLabelForKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.W:
                return "W";
            case KeyCode.A:
                return "A";
            case KeyCode.S:
                return "S";
            case KeyCode.D:
                return "D";
            default:
                return string.Empty;
        }
    }
}
