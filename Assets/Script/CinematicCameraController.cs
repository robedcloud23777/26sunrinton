using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Interpolates after the player during exploration and takes over for combat framing.</summary>
public sealed class CinematicCameraController : MonoBehaviour
{
    [Header("Exploration Follow")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0.01f)] private float followSharpness = 8f;

    [Header("Combat Framing")]
    [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float impactZoomSize = 3.5f;
    [SerializeField] private Vector3 arenaOffset = new Vector3(0f, 0f, -10f);
    [SerializeField] private Vector2 combatFramingPadding = new Vector2(2.25f, 1.75f);
    [SerializeField, Min(0.01f)] private float minimumCombatSize = 6.5f;

    private float initialOrthographicSize;
    private float fittedCombatSize;
    private Vector3 fittedCombatCenter;
    private Coroutine activeAnimation;
    private bool isCombatCameraActive;
    private bool hasFittedCombatFrame;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        initialOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 0f;
    }

    private void FixedUpdate()
    {
        if (isCombatCameraActive || activeAnimation != null || followTarget == null)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-followSharpness * Time.fixedUnscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, followTarget.position + followOffset, t);
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
        hasFittedCombatFrame = false;
    }

    /// <summary>Frames the player and every living enemy inside the orthographic view.</summary>
    public void FrameCombatants(Transform player, IEnumerable<Enemy> targets)
    {
        if (player == null || targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        Vector3 minimum = player.position;
        Vector3 maximum = player.position;
        if (targets != null)
        {
            foreach (Enemy target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                Vector3 targetPosition = target.AimPoint.position;
                minimum = Vector3.Min(minimum, targetPosition);
                maximum = Vector3.Max(maximum, targetPosition);
            }
        }

        fittedCombatCenter = (minimum + maximum) * 0.5f;
        Vector3 extents = (maximum - minimum) * 0.5f;
        float aspect = Mathf.Max(0.01f, targetCamera.aspect);
        float sizeForHeight = extents.y + Mathf.Max(0f, combatFramingPadding.y);
        float sizeForWidth = (extents.x + Mathf.Max(0f, combatFramingPadding.x)) / aspect;
        fittedCombatSize = Mathf.Max(minimumCombatSize, sizeForHeight, sizeForWidth);
        hasFittedCombatFrame = true;
        isCombatCameraActive = true;
        AnimateTo(fittedCombatCenter + arenaOffset, fittedCombatSize);
    }

    public void MoveToArenaCenter(Vector3 arenaCenter)
    {
        isCombatCameraActive = true;
        AnimateTo(arenaCenter + arenaOffset, initialOrthographicSize);
    }

    public void ZoomOnImpact(Vector3 impactPosition)
    {
        isCombatCameraActive = true;
        if (hasFittedCombatFrame)
        {
            // Never crop another combatant just to emphasize the current impact.
            AnimateTo(fittedCombatCenter + arenaOffset, Mathf.Max(impactZoomSize, fittedCombatSize));
            return;
        }

        AnimateTo(impactPosition + arenaOffset, impactZoomSize);
    }

    /// <summary>Restores the exploration zoom, then resumes interpolated player follow.</summary>
    public void ResetCamera(Transform player)
    {
        SetFollowTarget(player);
        isCombatCameraActive = false;
        hasFittedCombatFrame = false;

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }

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
