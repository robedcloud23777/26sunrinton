using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class Final : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private CanvasGroup fadeCanvasGroup; // 검은 풀스크린 이미지의 CanvasGroup
    [SerializeField] private float fadeDuration = 0.5f;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;
        hasTriggered = true;

        fadeCanvasGroup.DOFade(1f, fadeDuration)
            .OnComplete(() => SceneManager.LoadScene(nextSceneName));
    }
}