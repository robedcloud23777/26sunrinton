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
    private static readonly int AttackHash = Animator.StringToHash("attack");
    private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Player_Attack");

    public static CombatManager Instance { get; private set; }

    public enum CombatState
    {
        Idle,
        Intro,
        Negotiating,
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
    [SerializeField, Min(0f)] private float finalEnemyDefeatDelay = 1.25f;
    [SerializeField, Min(0)] private int damageOnQTEFail = 1;

    [Header("Player Health")]
    [Tooltip("Health uses quarter-heart units. 12 units equal three full hearts.")]
    [SerializeField, Min(1)] private int maximumPlayerHealth = 12;
    [SerializeField, Min(0)] private int currentPlayerHealth = 12;

    [Header("Slash Positioning")]
    [Tooltip("Distance kept from the target while the player enters QTE input.")]
    [SerializeField, Min(0.1f)] private float qteStartDistance = 1.25f;
    [Tooltip("Distance travelled past the target on a successful slash.")]
    [SerializeField, Min(0f)] private float slashOvershootDistance = 0.7f;

    [Header("Slash Animation Frames")]
    [Tooltip("One-based first attack frame used by the slash and its afterimages.")]
    [SerializeField, Min(1)] private int slashAfterimageStartFrame = 4;
    [Tooltip("One-based last attack frame used by the slash and its afterimages.")]
    [SerializeField, Min(1)] private int slashAfterimageEndFrame = 6;

    [Header("Intent Negotiation")]
    [SerializeField, Min(0.5f)] private float intentDecisionWindow = 3f;
    [SerializeField, Min(0.1f)] private float highTrustHoldDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float lowTrustHoldDuration = 0.9f;
    [SerializeField, Min(16f)] private float targetSelectionRadius = 90f;
    [SerializeField, Min(0f)] private float highTrustAcceptanceDelay = 0.08f;
    [SerializeField, Min(0f)] private float lowTrustAcceptanceDelay = 0.5f;
    [SerializeField] private IntentSelectionUI intentSelectionUI;

    public event Action<int> OnPlayerDamaged;
    public event Action<int, int> OnPlayerHealthChanged;
    public event Action OnCombatCompleted;
    public CombatState CurrentState => currentState;
    public int CurrentPlayerHealth => currentPlayerHealth;
    public int MaximumPlayerHealth => maximumPlayerHealth;

    private Transform playerTransform;
    private Vector3 safeRollbackPosition;
    private Queue<Enemy> targetQueue = new Queue<Enemy>();
    private Enemy currentTarget;
    private Coroutine activeSequence;
    private Vector3 attackDirection = Vector3.right;
    private bool autoKillCurrentTarget;
    private bool playerWasDamagedThisCombat;
    private bool combatHadTargets;
    private Rigidbody2D playerRigidbody;
    private Animator playerAnimator;
    private AnimationClip playerAttackClip;
    private float savedGravityScale;
    private float savedAnimatorSpeed = 1f;
    private bool isPlayerPhysicsSuspended;
    private bool isFinalPoseHeld;
    private bool isSlashAnimationDriven;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        maximumPlayerHealth = Mathf.Max(1, maximumPlayerHealth);
        currentPlayerHealth = Mathf.Clamp(currentPlayerHealth, 0, maximumPlayerHealth);
        if (intentSelectionUI == null)
        {
            intentSelectionUI = GetComponent<IntentSelectionUI>();
        }

        if (intentSelectionUI == null)
        {
            intentSelectionUI = gameObject.AddComponent<IntentSelectionUI>();
        }
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

        feedbackManager?.EndCombatAfterimages(true);
        feedbackManager?.EndIntentPreviews(true);
        intentSelectionUI?.Hide();
        RestorePlayerPresentation();
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
        combatHadTargets = targetQueue.Count > 0;
        playerWasDamagedThisCombat = false;
        SetPlayerControl(false);
        SuspendPlayerPhysics();
        cameraController?.EnterCombat(playerTransform);

        currentState = CombatState.Intro;
        CinematicModeUI.Instance.EnterCinematicMode();
        activeSequence = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        if (cameraController != null)
        {
            cameraController.FrameCombatants(playerTransform, targetQueue);
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

        cameraController?.FrameCombatants(playerTransform, targetQueue);
        currentState = CombatState.Negotiating;
        activeSequence = StartCoroutine(TargetNegotiationRoutine());
    }

    private IEnumerator TargetNegotiationRoutine()
    {
        RemoveMissingTargets();
        if (targetQueue.Count == 0)
        {
            EndCombat();
            yield break;
        }

        Enemy intendedTarget = targetQueue.Peek();
        currentTarget = intendedTarget;
        IReadOnlyList<Sprite> previewSprites = CaptureIntentPreviewSprites();
        feedbackManager?.BeginCharacterIntentPreview(playerTransform, intendedTarget.AimPoint, previewSprites);

        float trustRatio = TrustManager.Instance != null ? TrustManager.Instance.CurrentTrust / 100f : 0.5f;
        float requiredHoldDuration = Mathf.Lerp(lowTrustHoldDuration, highTrustHoldDuration, trustRatio);
        float idleDecisionTime = 0f;
        float heldTime = 0f;
        Enemy heldTarget = null;
        Enemy selectedTarget = null;

        while (intendedTarget != null && idleDecisionTime < intentDecisionWindow)
        {
            if (!ReferenceEquals(heldTarget, null) && heldTarget == null)
            {
                feedbackManager?.EndPlayerProposalPreview(true);
                intentSelectionUI?.Hide();
                heldTarget = null;
                heldTime = 0f;
            }

            if (heldTarget == null)
            {
                idleDecisionTime += Time.unscaledDeltaTime;
                if (Input.GetMouseButtonDown(0))
                {
                    heldTarget = FindTargetNearPointer();
                    heldTime = 0f;
                    if (heldTarget != null)
                    {
                        feedbackManager?.BeginPlayerProposalPreview(playerTransform, heldTarget.AimPoint, previewSprites);
                    }
                }
            }

            if (heldTarget != null)
            {
                if (Input.GetMouseButton(0))
                {
                    heldTime += Time.unscaledDeltaTime;
                    float holdProgress = requiredHoldDuration > 0f ? heldTime / requiredHoldDuration : 1f;
                    intentSelectionUI?.Show(Input.mousePosition, holdProgress, holdProgress >= 1f);
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (heldTime >= requiredHoldDuration)
                    {
                        selectedTarget = heldTarget;
                    }

                    feedbackManager?.EndPlayerProposalPreview(true);
                    intentSelectionUI?.Hide();
                    heldTarget = null;
                    heldTime = 0f;

                    if (selectedTarget != null)
                    {
                        break;
                    }
                }
            }

            yield return null;
        }

        intentSelectionUI?.Hide();
        if (intendedTarget == null)
        {
            feedbackManager?.EndIntentPreviews(true);
            PrepareNextAttack();
            yield break;
        }

        selectedTarget = selectedTarget != null ? selectedTarget : intendedTarget;
        PrioritizeTarget(selectedTarget);
        currentTarget = targetQueue.Peek();
        feedbackManager?.ShowAcceptedIntent(playerTransform, currentTarget.AimPoint, previewSprites);

        bool playerChangedIntent = currentTarget != intendedTarget;
        float acceptanceDelay = playerChangedIntent
            ? Mathf.Lerp(lowTrustAcceptanceDelay, highTrustAcceptanceDelay, trustRatio)
            : highTrustAcceptanceDelay;
        if (acceptanceDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(acceptanceDelay);
        }

        feedbackManager?.EndIntentPreviews(false);
        currentState = CombatState.Dashing;
        autoKillCurrentTarget = TrustManager.Instance != null && TrustManager.Instance.CheckBonusRoll();
        activeSequence = StartCoroutine(PositionForQTERoutine(currentTarget));
    }

    private Enemy FindTargetNearPointer()
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            return null;
        }

        Vector2 pointerPosition = Input.mousePosition;
        float closestDistance = targetSelectionRadius;
        Enemy closestTarget = null;
        foreach (Enemy target in targetQueue)
        {
            if (target == null)
            {
                continue;
            }

            Vector3 screenPosition = gameplayCamera.WorldToScreenPoint(target.AimPoint.position);
            if (screenPosition.z < 0f)
            {
                continue;
            }

            float distance = Vector2.Distance(pointerPosition, screenPosition);
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    private void PrioritizeTarget(Enemy selectedTarget)
    {
        if (selectedTarget == null || targetQueue.Count == 0 || targetQueue.Peek() == selectedTarget)
        {
            return;
        }

        Queue<Enemy> reorderedTargets = new Queue<Enemy>();
        reorderedTargets.Enqueue(selectedTarget);
        foreach (Enemy target in targetQueue)
        {
            if (target != null && target != selectedTarget)
            {
                reorderedTargets.Enqueue(target);
            }
        }

        targetQueue = reorderedTargets;
    }

    private IReadOnlyList<Sprite> CaptureIntentPreviewSprites()
    {
        List<Sprite> attackSprites = new List<Sprite>();
        SpriteRenderer playerRenderer = playerTransform != null
            ? playerTransform.GetComponentInChildren<SpriteRenderer>()
            : null;
        if (playerRenderer == null || playerAnimator == null || playerAttackClip == null ||
            playerAttackClip.length <= 0f || playerAttackClip.frameRate <= 0f ||
            !playerAnimator.HasState(0, AttackStateHash))
        {
            return attackSprites;
        }

        AnimatorStateInfo previousState = playerAnimator.GetCurrentAnimatorStateInfo(0);
        float previousSpeed = playerAnimator.speed;
        playerAnimator.speed = 0f;

        int startFrame = Mathf.Max(1, slashAfterimageStartFrame);
        int endFrame = Mathf.Max(startFrame, slashAfterimageEndFrame);
        for (int frame = startFrame; frame <= endFrame; frame++)
        {
            float frameTime = (frame - 1) / playerAttackClip.frameRate;
            float normalizedTime = Mathf.Clamp01(frameTime / playerAttackClip.length);
            playerAnimator.Play(AttackStateHash, 0, normalizedTime);
            playerAnimator.Update(0f);
            if (playerRenderer.sprite != null)
            {
                attackSprites.Add(playerRenderer.sprite);
            }
        }

        if (previousState.fullPathHash != 0)
        {
            playerAnimator.Play(previousState.fullPathHash, 0, previousState.normalizedTime);
            playerAnimator.Update(0f);
        }

        playerAnimator.speed = previousSpeed;
        return attackSprites;
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

        if (autoKillCurrentTarget)
        {
            currentState = CombatState.Resolution;
            activeSequence = StartCoroutine(SlashThroughTargetRoutine(currentTarget));
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
        BeginSlashAnimation();
        feedbackManager?.BeginCombatAfterimages(playerTransform);
        feedbackManager?.CapturePlayerAfterimage();
        feedbackManager?.PlaySlashFeedback(targetPosition);

        float elapsed = 0f;
        while (elapsed < slashDuration)
        {
            if (target == null)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            float slashProgress = Mathf.Clamp01(elapsed / slashDuration);
            SampleSlashAnimation(slashProgress);
            playerTransform.position = Vector3.Lerp(start, destination, slashProgress);
            yield return null;
        }

        playerTransform.position = destination;
        ResetPlayerFallState();
        SampleSlashAnimation(1f);
        feedbackManager?.CapturePlayerAfterimage();
        feedbackManager?.EndCombatAfterimages();
        if (target != null)
        {
            targetingSystem.RemoveIndicator(target);
            Destroy(target.gameObject);
        }

        if (targetQueue.Count > 0)
        {
            targetQueue.Dequeue();
        }

        RemoveMissingTargets();
        if (targetQueue.Count == 0)
        {
            // Keep the impact framing and slash landing visible before leaving combat.
            currentState = CombatState.Outro;
            HoldFinalSlashPose();
            if (finalEnemyDefeatDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(finalEnemyDefeatDelay);
            }

            EndCombat();
            yield break;
        }

        ReleaseSlashAnimation();
        PrepareNextAttack();
    }

    private void BeginSlashAnimation()
    {
        if (playerAnimator == null)
        {
            return;
        }

        if (playerAnimator.HasState(0, AttackStateHash))
        {
            playerAnimator.speed = 0f;
            isSlashAnimationDriven = true;
            SampleSlashAnimation(0f);
        }
        else
        {
            playerAnimator.SetTrigger(AttackHash);
            playerAnimator.Update(0f);
        }
    }

    private void SampleSlashAnimation(float slashProgress)
    {
        if (!isSlashAnimationDriven || playerAnimator == null)
        {
            return;
        }

        GetSlashAnimationRange(out float startNormalizedTime, out float endNormalizedTime);
        float normalizedTime = Mathf.Lerp(startNormalizedTime, endNormalizedTime, slashProgress);
        playerAnimator.Play(AttackStateHash, 0, normalizedTime);
        playerAnimator.Update(0f);
    }

    private void GetSlashAnimationRange(out float startNormalizedTime, out float endNormalizedTime)
    {
        if (playerAttackClip == null || playerAttackClip.length <= 0f || playerAttackClip.frameRate <= 0f)
        {
            startNormalizedTime = 0f;
            endNormalizedTime = 1f;
            return;
        }

        int startFrame = Mathf.Max(1, slashAfterimageStartFrame);
        int endFrame = Mathf.Max(startFrame, slashAfterimageEndFrame);
        float startTime = (startFrame - 1) / playerAttackClip.frameRate;
        float endTime = (endFrame - 1) / playerAttackClip.frameRate;
        startNormalizedTime = Mathf.Clamp01(startTime / playerAttackClip.length);
        endNormalizedTime = Mathf.Clamp01(endTime / playerAttackClip.length);
    }

    private void ReleaseSlashAnimation()
    {
        if (isSlashAnimationDriven && playerAnimator != null)
        {
            playerAnimator.speed = savedAnimatorSpeed;
        }

        isSlashAnimationDriven = false;
    }

    private void HandleQTEFail()
    {
        if (currentState != CombatState.QTEActive)
        {
            return;
        }

        currentState = CombatState.Resolution;
        playerWasDamagedThisCombat = true;
        ApplyPlayerDamage(damageOnQTEFail);
        feedbackManager?.PlayFailFeedback(playerTransform.position);

        if (cameraController != null)
        {
            cameraController.FrameCombatants(playerTransform, targetQueue);
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
            Enemy missingTarget = targetQueue.Dequeue();
            targetingSystem.RemoveIndicator(missingTarget);
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
        feedbackManager?.EndCombatAfterimages();
        feedbackManager?.EndIntentPreviews(true);
        intentSelectionUI?.Hide();
        RestorePlayerPresentation();
        SetPlayerControl(true);

        if (combatHadTargets && !playerWasDamagedThisCombat)
        {
            TrustManager.Instance?.ReportCombatNoDamage();
        }

        currentTarget = null;
        combatHadTargets = false;
        currentState = CombatState.Idle;
        OnCombatCompleted?.Invoke();
    }

    private void SuspendPlayerPhysics()
    {
        playerRigidbody = playerTransform != null ? playerTransform.GetComponent<Rigidbody2D>() : null;
        playerMovement = playerTransform != null ? playerTransform.GetComponent<PlayerMovement>() : null;
        playerAnimator = playerTransform != null ? playerTransform.GetComponentInChildren<Animator>() : null;
        playerAttackClip = FindAnimationClip(playerAnimator, "Player_Attack");
        playerMovement?.ResetFallDamageTracking();

        if (playerRigidbody != null)
        {
            savedGravityScale = playerRigidbody.gravityScale;
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.gravityScale = 0f;
            isPlayerPhysicsSuspended = true;
        }

        if (playerAnimator != null)
        {
            savedAnimatorSpeed = playerAnimator.speed;
        }

        isFinalPoseHeld = false;
        isSlashAnimationDriven = false;
    }

    private static AnimationClip FindAnimationClip(Animator animator, string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return null;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }

    private void HoldFinalSlashPose()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        if (playerAnimator != null)
        {
            playerAnimator.speed = 0f;
            isFinalPoseHeld = true;
        }
    }

    private void RestorePlayerPresentation()
    {
        if ((isFinalPoseHeld || isSlashAnimationDriven) && playerAnimator != null)
        {
            playerAnimator.speed = savedAnimatorSpeed;
        }

        if (isPlayerPhysicsSuspended && playerRigidbody != null)
        {
            ResetPlayerFallState();
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.gravityScale = savedGravityScale;
        }

        isFinalPoseHeld = false;
        isSlashAnimationDriven = false;
        isPlayerPhysicsSuspended = false;
        playerRigidbody = null;
        playerMovement = null;
        playerAnimator = null;
        playerAttackClip = null;
    }

    private void ResetPlayerFallState()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        playerMovement?.ResetFallDamageTracking();
    }

    /// <summary>Call this when non-QTE combat damage is applied to the player.</summary>
    public void RegisterPlayerHit(int damage = 1)
    {
        if (damage <= 0)
        {
            return;
        }

        playerWasDamagedThisCombat = true;
        TrustManager.Instance?.ReportNormalHit();
        ApplyPlayerDamage(damage);
    }

    /// <summary>Forwards fall damage without also applying the normal-hit trust penalty.</summary>
    public void RegisterFallDamage(int damage = 1)
    {
        int safeDamage = Mathf.Max(0, damage);
        if (safeDamage == 0)
        {
            return;
        }

        if (currentState != CombatState.Idle)
        {
            playerWasDamagedThisCombat = true;
        }

        ApplyPlayerDamage(safeDamage);
    }

    public void RestorePlayerHealth(int amount)
    {
        if (amount <= 0 || currentPlayerHealth >= maximumPlayerHealth)
        {
            return;
        }

        currentPlayerHealth = Mathf.Min(maximumPlayerHealth, currentPlayerHealth + amount);
        OnPlayerHealthChanged?.Invoke(currentPlayerHealth, maximumPlayerHealth);
    }

    private void ApplyPlayerDamage(int damage)
    {
        int safeDamage = Mathf.Max(0, damage);
        if (safeDamage == 0 || currentPlayerHealth <= 0)
        {
            return;
        }

        int previousHealth = currentPlayerHealth;
        currentPlayerHealth = Mathf.Max(0, currentPlayerHealth - safeDamage);
        int appliedDamage = previousHealth - currentPlayerHealth;
        OnPlayerDamaged?.Invoke(appliedDamage);
        OnPlayerHealthChanged?.Invoke(currentPlayerHealth, maximumPlayerHealth);
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
