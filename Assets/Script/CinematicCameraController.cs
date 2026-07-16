using System.Collections;
using UnityEngine;

/// <summary>Interpolates after the player during exploration and takes over for combat framing.</summary>
public sealed class CinematicCameraController : MonoBehaviour
{
    [Header("Exploration Follow")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0.01f)] private float followSharpness = 8f;
    [SerializeField] private bool followVertical = false; // 상하 추적 여부

    [Header("Combat Framing")]
    [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float impactZoomSize = 3.5f;
    [SerializeField] private Vector3 arenaOffset = new Vector3(0f, 0f, -10f);

    private float initialOrthographicSize;
    private float fixedY; // 고정할 Y값
    private Coroutine activeAnimation;
    private bool isCombatCameraActive;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        initialOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 0f;
        fixedY = transform.position.y; // 시작 위치의 Y를 기준으로 고정
    }

    private void FixedUpdate()
    {
        if (isCombatCameraActive || activeAnimation != null || followTarget == null)
        {
            return;
        }

        Vector3 desiredPosition = followTarget.position + followOffset;

        // 상하 추적을 끈 경우, Y는 고정값 유지
        if (!followVertical)
        {
            desiredPosition.y = fixedY;
        }

        float t = 1f - Mathf.Exp(-followSharpness * Time.fixedUnscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
    }

    /// <summary>Assigns the player that is followed while no combat camera is active.</summary>
    public void SetFollowTarget(Transform player)
    {
        followTarget = player;
    }

    /// <summary>Stops normal player follow so combat framing can take over.</summary>
    public void EnterCombat(Transform player)
    {
        SetFollowTarget(player);
        isCombatCameraActive = true;
    }

    public void MoveToArenaCenter(Vector3 arenaCenter)
    {
        isCombatCameraActive = true;
        AnimateTo(arenaCenter + arenaOffset, initialOrthographicSize);
    }

    public void ZoomOnImpact(Vector3 impactPosition)
    {
        isCombatCameraActive = true;
        AnimateTo(impactPosition + arenaOffset, impactZoomSize);
    }

    /// <summary>Restores the exploration zoom, then resumes interpolated player follow.</summary>
    public void ResetCamera(Transform player)
    {
        SetFollowTarget(player);
        isCombatCameraActive = false;

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }

        // 전투 종료 후 복귀 시, 현재 카메라의 Y를 새 고정값으로 갱신
        fixedY = transform.position.y;

        // Keep the current position so the follow interpolation performs the return movement.
        AnimateTo(transform.position, initialOrthographicSize);
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