using System.Collections;
using UnityEngine;

/// <summary>Small, dependency-free camera animator for combat framing.</summary>
public sealed class CinematicCameraController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float impactZoomSize = 3.5f;
    [SerializeField] private Vector3 arenaOffset = new Vector3(0f, 0f, -10f);

    private Vector3 initialPosition;
    private float initialOrthographicSize;
    private Coroutine activeAnimation;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        initialPosition = transform.position;
        initialOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 0f;
    }

    public void MoveToArenaCenter(Vector3 arenaCenter)
    {
        AnimateTo(arenaCenter + arenaOffset, initialOrthographicSize);
    }

    public void ZoomOnImpact(Vector3 impactPosition)
    {
        AnimateTo(impactPosition + arenaOffset, impactZoomSize);
    }

    public void ResetCamera(Transform player)
    {
        Vector3 destination = player != null ? player.position + arenaOffset : initialPosition;
        AnimateTo(destination, initialOrthographicSize);
    }

    private void AnimateTo(Vector3 destination, float orthographicSize)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(AnimateRoutine(destination, orthographicSize));
    }

    private IEnumerator AnimateRoutine(Vector3 destination, float orthographicSize)
    {
        Vector3 startPosition = transform.position;
        float startSize = targetCamera != null ? targetCamera.orthographicSize : 0f;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPosition, destination, t);

            if (targetCamera != null && targetCamera.orthographic)
            {
                targetCamera.orthographicSize = Mathf.Lerp(startSize, orthographicSize, t);
            }

            yield return null;
        }

        transform.position = destination;
        if (targetCamera != null && targetCamera.orthographic)
        {
            targetCamera.orthographicSize = orthographicSize;
        }

        activeAnimation = null;
    }
}
