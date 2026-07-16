using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates the combat sequence: approach alignment, QTE input, slash dash,
/// and recovery. Other systems subscribe to its events instead of changing state.
/// </summary>
public sealed class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    public enum CombatState
    {
        Idle,
        Intro,
        Dashing,
        QTEActive,
        Resolution,
        Outro
    }

    [Header("State")]
    [SerializeField] private CombatState currentState = CombatState.Idle;

    [Header("Module References")]
    [SerializeField] private TargetingSystem targetingSystem;
    [SerializeField] private CinematicCameraController cameraController;
    [SerializeField] private QTEController qteController;
    [SerializeField] private CombatFeedbackManager feedbackManager;
    [Tooltip("The player's movement behaviour. It is disabled while combat is active.")]
    [SerializeField] private Behaviour playerMovementController;

    [Header("Sequence Timing")]
    [SerializeField, Min(0f)] private float introDelay = 0.5f;
    [SerializeField, Min(0.01f)] private float positioningDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float slashDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float rollbackDuration = 0.5f;
    [SerializeField, Min(0)] private int damageOnQTEFail = 1;

    [Header("Slash Positioning")]
    [Tooltip("Distance kept from the target while the player enters QTE input.")]
    [SerializeField, Min(0.1f)] private float qteStartDistance = 1.25f;
    [Tooltip("Distance travelled past the target on a successful slash.")]
    [SerializeField, Min(0f)] private float slashOvershootDistance = 0.7f;

    public event Action<int> OnPlayerDamaged;
    public event Action OnCombatCompleted;
    public CombatState CurrentState => currentState;

    private Transform playerTransform;
    private Vector3 safeRollbackPosition;
    private Queue<Enemy> targetQueue = new Queue<Enemy>();
    private Enemy currentTarget;
    private Coroutine activeSequence;
    private Vector3 attackDirection = Vector3.right;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        SubscribeToQTE();
    }

    private void OnDisable()
    {
        UnsubscribeFromQTE();
        qteController?.CancelQTE();

        if (activeSequence != null)
        {
            StopCoroutine(activeSequence);
            activeSequence = null;
        }
    }

    /// <summary>Assigns references for a scene created entirely by code.</summary>
    public void Configure(
        TargetingSystem targeting,
        CinematicCameraController camera,
        QTEController qte,
        CombatFeedbackManager feedback,
        Behaviour playerMovement)
    {
        UnsubscribeFromQTE();
        targetingSystem = targeting;
        cameraController = camera;
        qteController = qte;
        feedbackManager = feedback;
        playerMovementController = playerMovement;
        SubscribeToQTE();
    }

    /// <summary>Call this from an arena trigger after assigning the referenced modules.</summary>
    public void StartCombatSequence(Transform enemyGroup, Transform player)
    {
        if (currentState != CombatState.Idle || enemyGroup == null || player == null ||
            targetingSystem == null || qteController == null)
        {
            return;
        }

        playerTransform = player;
        safeRollbackPosition = player.position;
        targetQueue = targetingSystem.GetSortedTargets(player, enemyGroup);
        SetPlayerControl(false);
        cameraController?.EnterCombat(playerTransform);

        currentState = CombatState.Intro;
        CinematicModeUI.Instance.EnterCinematicMode();
        activeSequence = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        if (cameraController != null)
        {
            cameraController.MoveToArenaCenter(playerTransform.position);
        }

        yield return StartCoroutine(targetingSystem.SpawnIndicatorsRoutine(targetQueue));
        if (introDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(introDelay);
        }

        PrepareNextAttack();
    }

    /// <summary>
    /// Selects the next enemy and moves the player to a cardinal attack line.
    /// QTE input starts only after the player is naturally positioned beside it.
    /// </summary>
    private void PrepareNextAttack()
    {
        RemoveMissingTargets();
        if (targetQueue.Count == 0)
        {
            EndCombat();
            return;
        }

        currentState = CombatState.Dashing;
        currentTarget = targetQueue.Peek();
        activeSequence = StartCoroutine(PositionForQTERoutine(currentTarget));
    }

    private IEnumerator PositionForQTERoutine(Enemy target)
    {
        if (target == null)
        {
            PrepareNextAttack();
            yield break;
        }

        Vector3 start = playerTransform.position;
        Vector3 targetPosition = target.AimPoint.position;
        attackDirection = GetCardinalDirection(start, targetPosition);
        Vector3 destination = targetPosition - attackDirection * qteStartDistance;
        float elapsed = 0f;

        while (elapsed < positioningDuration)
        {
            if (target == null)
            {
                PrepareNextAttack();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / positioningDuration);
            playerTransform.position = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        playerTransform.position = destination;
        if (target == null)
        {
            PrepareNextAttack();
            yield break;
        }

        EnterQTEPhase();
    }

    private void EnterQTEPhase()
    {
        if (currentTarget == null)
        {
            PrepareNextAttack();
            return;
        }

        currentState = CombatState.QTEActive;
        if (cameraController != null)
        {
            cameraController.ZoomOnImpact(currentTarget.AimPoint.position);
        }

        feedbackManager?.PlayImpactFeedback(currentTarget.AimPoint.position);
        qteController.StartQTE();
    }

    private void HandleQTESuccess()
    {
        if (currentState != CombatState.QTEActive)
        {
            return;
        }

        currentState = CombatState.Resolution;
        activeSequence = StartCoroutine(SlashThroughTargetRoutine(currentTarget));
    }

    /// <summary>
    /// A successful QTE turns the prepared attack line into a fast, straight
    /// slash that finishes beyond the enemy. The enemy is removed only after
    /// the player has passed through it.
    /// </summary>
    private IEnumerator SlashThroughTargetRoutine(Enemy target)
    {
        if (target == null)
        {
            PrepareNextAttack();
            yield break;
        }

        Vector3 start = playerTransform.position;
        Vector3 targetPosition = target.AimPoint.position;
        attackDirection = GetCardinalDirection(start, targetPosition);
        Vector3 destination = targetPosition + attackDirection * slashOvershootDistance;
        feedbackManager?.PlaySlashFeedback(targetPosition);

        float elapsed = 0f;
        while (elapsed < slashDuration)
        {
            if (target == null)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            playerTransform.position = Vector3.Lerp(start, destination, elapsed / slashDuration);
            yield return null;
        }

        playerTransform.position = destination;
        if (target != null)
        {
            Destroy(target.gameObject);
        }

        if (targetQueue.Count > 0)
        {
            targetQueue.Dequeue();
            targetingSystem.RemoveFirstIndicator();
        }

        PrepareNextAttack();
    }

    private void HandleQTEFail()
    {
        if (currentState != CombatState.QTEActive)
        {
            return;
        }

        currentState = CombatState.Resolution;
        OnPlayerDamaged?.Invoke(damageOnQTEFail);
        feedbackManager?.PlayFailFeedback(playerTransform.position);

        if (cameraController != null)
        {
            cameraController.MoveToArenaCenter(safeRollbackPosition);
        }

        activeSequence = StartCoroutine(RollbackRoutine());
    }

    private IEnumerator RollbackRoutine()
    {
        Vector3 start = playerTransform.position;
        float elapsed = 0f;

        while (elapsed < rollbackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            playerTransform.position = Vector3.Lerp(start, safeRollbackPosition, elapsed / rollbackDuration);
            yield return null;
        }

        playerTransform.position = safeRollbackPosition;
        PrepareNextAttack();
    }

    private void RemoveMissingTargets()
    {
        while (targetQueue.Count > 0 && targetQueue.Peek() == null)
        {
            targetQueue.Dequeue();
            targetingSystem.RemoveFirstIndicator();
        }
    }

    private static Vector3 GetCardinalDirection(Vector3 from, Vector3 to)
    {
        Vector3 offset = to - from;
        if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y))
        {
            return new Vector3(Mathf.Approximately(offset.x, 0f) ? 1f : Mathf.Sign(offset.x), 0f, 0f);
        }

        return new Vector3(0f, Mathf.Approximately(offset.y, 0f) ? 1f : Mathf.Sign(offset.y), 0f);
    }

    private void EndCombat()
    {
        currentState = CombatState.Outro;
        CinematicModeUI.Instance.ExitCinematicMode();
        targetingSystem.ClearIndicators();
        cameraController?.ResetCamera(playerTransform);
        SetPlayerControl(true);
        currentTarget = null;
        currentState = CombatState.Idle;
        OnCombatCompleted?.Invoke();
    }

    private void SetPlayerControl(bool enabled)
    {
        if (playerMovementController != null)
        {
            playerMovementController.enabled = enabled;
        }
    }

    private void SubscribeToQTE()
    {
        if (qteController == null)
        {
            return;
        }

        qteController.OnQTESuccess -= HandleQTESuccess;
        qteController.OnQTEFail -= HandleQTEFail;
        qteController.OnQTESuccess += HandleQTESuccess;
        qteController.OnQTEFail += HandleQTEFail;
    }

    private void UnsubscribeFromQTE()
    {
        if (qteController == null)
        {
            return;
        }

        qteController.OnQTESuccess -= HandleQTESuccess;
        qteController.OnQTEFail -= HandleQTEFail;
    }
}
