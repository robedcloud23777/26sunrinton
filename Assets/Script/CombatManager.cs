using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates the combat sequence: introduction, dash, QTE resolution, and
/// recovery.  Other systems subscribe to its events instead of changing state.
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
    [SerializeField, Min(0.01f)] private float dashDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float rollbackDuration = 0.5f;
    [SerializeField, Min(0)] private int damageOnQTEFail = 1;

    public event Action<int> OnPlayerDamaged;
    public event Action OnCombatCompleted;
    public CombatState CurrentState => currentState;

    private Transform playerTransform;
    private Vector3 safeRollbackPosition;
    private Queue<Enemy> targetQueue = new Queue<Enemy>();
    private Enemy currentTarget;
    private Coroutine activeSequence;

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

        currentState = CombatState.Intro;
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

        ExecuteNextDash();
    }

    private void ExecuteNextDash()
    {
        RemoveMissingTargets();
        if (targetQueue.Count == 0)
        {
            EndCombat();
            return;
        }

        currentState = CombatState.Dashing;
        currentTarget = targetQueue.Peek();
        activeSequence = StartCoroutine(DashRoutine(currentTarget));
    }

    private IEnumerator DashRoutine(Enemy target)
    {
        if (target == null)
        {
            ExecuteNextDash();
            yield break;
        }

        Vector3 start = playerTransform.position;
        Vector3 destination = target.AimPoint.position;
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            if (target == null)
            {
                ExecuteNextDash();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            playerTransform.position = Vector3.Lerp(start, destination, elapsed / dashDuration);
            yield return null;
        }

        playerTransform.position = destination;
        if (target == null)
        {
            ExecuteNextDash();
            yield break;
        }

        EnterQTEPhase();
    }

    private void EnterQTEPhase()
    {
        if (currentTarget == null)
        {
            ExecuteNextDash();
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
        if (currentTarget != null)
        {
            feedbackManager?.PlaySlashFeedback(currentTarget.AimPoint.position);
            Destroy(currentTarget.gameObject);
        }

        if (targetQueue.Count > 0)
        {
            targetQueue.Dequeue();
            targetingSystem.RemoveFirstIndicator();
        }

        ExecuteNextDash();
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
        ExecuteNextDash();
    }

    private void RemoveMissingTargets()
    {
        while (targetQueue.Count > 0 && targetQueue.Peek() == null)
        {
            targetQueue.Dequeue();
            targetingSystem.RemoveFirstIndicator();
        }
    }

    private void EndCombat()
    {
        currentState = CombatState.Outro;
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
