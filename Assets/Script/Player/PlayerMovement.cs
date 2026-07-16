using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;
    [SerializeField] private PhysicsMaterial2D frictionlessMaterial;

    [Header("Wall Contact (벽 비비기 방지)")]
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float wallCheckHeightRatio = 0.75f;
    [Tooltip("Normals with a Y component below this value are treated as vertical walls, not slopes.")]
    [SerializeField, Range(0f, 0.5f)] private float maximumWallNormalY = 0.2f;

    [Header("Ground Check (바닥 체크)")]
    public Transform groundCheck;
    public LayerMask groundLayer = ~0;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.45f, 0.12f);
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.1f;
    [SerializeField, Range(0f, 89f)] private float maximumGroundAngle = 60f;

    [Header("One-Way Platform (Maple Style)")]
    [SerializeField] private LayerMask oneWayPlatformLayer = 1 << 7;
    [SerializeField, Min(0.05f)] private float dropThroughDuration = 0.35f;
    [SerializeField, Min(0f)] private float dropDownSpeed = 2f;

    [Header("Fall Damage (낙하 데미지)")]
    [SerializeField] private bool enableFallDamage = true;
    [Tooltip("Damage starts when the vertical fall distance reaches this value.")]
    [SerializeField, Min(0.1f)] private float minimumFallDistance = 5f;
    [Tooltip("Each additional distance step adds one damage.")]
    [SerializeField, Min(0.1f)] private float additionalDamageDistance = 3f;
    [SerializeField, Min(1)] private int baseFallDamage = 1;
    [SerializeField, Min(1)] private int maximumFallDamage = 5;

    [Header("Trust Rebellion")]
    [SerializeField, Min(1f)] private float rebellionCheckInterval = 10f;
    [SerializeField] private Vector2 confusionDurationRange = new Vector2(3f, 5f);

    [Header("Trust Rebellion Screen Effect")]
    [SerializeField] private Sprite rebellionBorderSprite;
    [SerializeField, Range(0f, 1f)] private float rebellionBorderMaximumAlpha = 0.8f;
    [SerializeField, Min(0f)] private float rebellionBorderFadeInDuration = 0.65f;
    [SerializeField, Min(0f)] private float rebellionBorderFadeOutDuration = 0.45f;
    [SerializeField, Min(0f)] private float rebellionBorderPulseSpeed = 1.8f;
    [SerializeField] private int rebellionBorderSortingOrder = 500;

    [Header("Trust Rebellion Debug")]
    [Tooltip("Press this key in Play Mode to force the inverted-control effect regardless of trust.")]
    [SerializeField] private KeyCode rebellionTestKey = KeyCode.O;

    [Header("Animation References")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation Timing")]
    [SerializeField, Min(0f)] private float attackDuration = 0.35f;
    [SerializeField, Min(0f)] private float dashDuration = 0.25f;
    [SerializeField, Min(0f)] private float hurtDuration = 0.4f;
    [SerializeField] private Color hurtColor = new Color(1f, 0.5f, 0.5f, 1f);

    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsDashingHash = Animator.StringToHash("isDashing");
    private static readonly int AttackHash = Animator.StringToHash("attack");
    private static readonly int HurtHash = Animator.StringToHash("hurt");

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Color originalSpriteColor = Color.white;
    private float horizontalInput;
    private bool isGrounded;
    private Vector2 groundNormal = Vector2.up;
    private bool shouldJump;
    private bool isFacingRight = true;
    private bool isDashing;
    private bool isAttacking;
    private bool isHurt;
    private float rebellionCheckTimer;
    private float invertedControlTimer;
    private Collider2D ignoredPlatform;
    private Coroutine dropThroughRoutine;
    private bool isTrackingFall;
    private float fallPeakY;
    private GameObject rebellionEffectCanvas;
    private CanvasGroup rebellionEffectGroup;
    private RectTransform rebellionEffectRect;
    private float rebellionEffectVisibility;

    public bool IsGrounded => isGrounded;
    public Vector2 GroundNormal => groundNormal;
    public bool IsBusy => isDashing || isAttacking || isHurt;
    public bool IsControlsInverted => invertedControlTimer > 0f;
    public event Action<int> OnFallDamaged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        if (bodyCollider != null && frictionlessMaterial != null)
        {
            bodyCollider.sharedMaterial = frictionlessMaterial;
            rb.sharedMaterial = frictionlessMaterial;
        }

        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalSpriteColor = spriteRenderer.color;
        }

        isFacingRight = transform.localScale.x >= 0f;
        rebellionCheckTimer = rebellionCheckInterval;
        CreateRebellionScreenEffect();
    }

    private void Update()
    {
        if (Input.GetKeyDown(rebellionTestKey))
        {
            ForceTriggerRebellion();
        }

        UpdateTrustRebellion();
        UpdateRebellionScreenEffect();

        if (isHurt || isDashing)
        {
            StopHorizontalMovement();
            return;
        }

        

        if (isAttacking)
        {
            StopHorizontalMovement();
            return;
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (IsControlsInverted)
        {
            horizontalInput *= -1f;
        }

        SetAnimatorBool(IsRunningHash, !Mathf.Approximately(horizontalInput, 0f));
        FlipTowardsInput();

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            bool wantsToDrop = Input.GetAxisRaw("Vertical") < -0.5f;
            if (!wantsToDrop || !TryDropThroughPlatform())
            {
                shouldJump = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            StartCoroutine(DashRoutine());
        }

        // Temporary test input. Gameplay damage can call PlayHurt() directly.
        if (Input.GetKeyDown(KeyCode.G))
        {
            PlayHurt();
        }
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        SetAnimatorBool(IsGroundedHash, isGrounded);
        UpdateFallDamageTracking();

        Move();
        if (shouldJump && !IsBusy)
        {
            Jump();
        }
    }

    private bool CheckGrounded()
    {
        int groundedLayers = groundLayer.value | oneWayPlatformLayer.value;
        Vector2 checkOrigin;
        if (groundCheck != null)
        {
            checkOrigin = groundCheck.position;
        }
        else if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            checkOrigin = new Vector2(
                bounds.center.x,
                bounds.min.y + groundCheckSize.y * 0.5f + 0.02f);
        }
        else
        {
            groundNormal = Vector2.up;
            return false;
        }

        Vector2 safeCheckSize = new Vector2(
            Mathf.Max(0.01f, groundCheckSize.x),
            Mathf.Max(0.01f, groundCheckSize.y));
        RaycastHit2D hit = Physics2D.BoxCast(
            checkOrigin,
            safeCheckSize,
            0f,
            Vector2.down,
            groundCheckDistance,
            groundedLayers);

        if (hit.collider == null || hit.collider == bodyCollider || hit.collider == ignoredPlatform)
        {
            groundNormal = Vector2.up;
            return false;
        }

        bool isOneWayPlatform = ((1 << hit.collider.gameObject.layer) & oneWayPlatformLayer.value) != 0;
        if (isOneWayPlatform && rb.linearVelocity.y > 0.1f)
        {
            groundNormal = Vector2.up;
            return false;
        }

        Vector2 hitNormal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : Vector2.up;
        float groundAngle = Vector2.Angle(hitNormal, Vector2.up);
        if (groundAngle > maximumGroundAngle)
        {
            groundNormal = Vector2.up;
            return false;
        }

        groundNormal = hitNormal;
        return true;
    }

    private void Move()
    {
        float velocityX = IsBusy ? 0f : horizontalInput * moveSpeed;
        if (IsMovingIntoWall(velocityX))
        {
            // Stop feeding velocity into the wall. Vertical velocity is left
            // untouched so gravity keeps pulling the player down normally.
            velocityX = 0f;
        }

        // Move along the surface tangent so the player follows a slope instead
        // of repeatedly losing contact with it. Zero velocity while idle also
        // prevents the rigidbody from slowly drifting down a valid slope.
        if (isGrounded && rb.linearVelocity.y <= 0.5f)
        {
            if (Mathf.Approximately(velocityX, 0f))
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 slopeTangent = new Vector2(groundNormal.y, -groundNormal.x).normalized;
            rb.linearVelocity = slopeTangent * velocityX;
            return;
        }

        rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
    }

    private bool IsMovingIntoWall(float velocityX)
    {
        if (bodyCollider == null || Mathf.Approximately(velocityX, 0f))
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 direction = velocityX > 0f ? Vector2.right : Vector2.left;
        Vector2 castSize = new Vector2(
            Mathf.Max(0.01f, bounds.size.x * 0.9f),
            Mathf.Max(0.01f, bounds.size.y * wallCheckHeightRatio));
        RaycastHit2D hit = Physics2D.BoxCast(
            bounds.center,
            castSize,
            0f,
            direction,
            wallCheckDistance,
            groundLayer);

        if (hit.collider == null || hit.collider == bodyCollider || hit.collider.isTrigger)
        {
            return false;
        }

        return Mathf.Abs(hit.normal.x) > 0.5f &&
               Mathf.Abs(hit.normal.y) <= maximumWallNormalY;
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        shouldJump = false;
        isGrounded = false;
        SetAnimatorBool(IsGroundedHash, false);
    }

    private void StopHorizontalMovement()
    {
        horizontalInput = 0f;
        shouldJump = false;
        SetAnimatorBool(IsRunningHash, false);
    }

    private void FlipTowardsInput()
    {
        if ((horizontalInput > 0f && !isFacingRight) || (horizontalInput < 0f && isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1f;
            transform.localScale = scale;
        }
    }

    private void UpdateTrustRebellion()
    {
        if (invertedControlTimer > 0f)
        {
            invertedControlTimer = Mathf.Max(0f, invertedControlTimer - Time.unscaledDeltaTime);
            return;
        }

        rebellionCheckTimer -= Time.unscaledDeltaTime;
        if (rebellionCheckTimer > 0f)
        {
            return;
        }

        rebellionCheckTimer = rebellionCheckInterval;
        TryTriggerRebellion();
    }

    private void CreateRebellionScreenEffect()
    {
        if (rebellionBorderSprite == null || rebellionEffectCanvas != null)
        {
            return;
        }

        rebellionEffectCanvas = new GameObject(
            "Trust Rebellion Screen Effect",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        Canvas canvas = rebellionEffectCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = rebellionBorderSortingOrder;

        CanvasScaler scaler = rebellionEffectCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rebellionEffectGroup = rebellionEffectCanvas.GetComponent<CanvasGroup>();
        rebellionEffectGroup.alpha = 0f;
        rebellionEffectGroup.interactable = false;
        rebellionEffectGroup.blocksRaycasts = false;

        GameObject borderObject = new GameObject("Rebellion Border", typeof(RectTransform), typeof(Image));
        borderObject.transform.SetParent(rebellionEffectCanvas.transform, false);
        rebellionEffectRect = borderObject.GetComponent<RectTransform>();
        rebellionEffectRect.anchorMin = Vector2.zero;
        rebellionEffectRect.anchorMax = Vector2.one;
        rebellionEffectRect.offsetMin = Vector2.zero;
        rebellionEffectRect.offsetMax = Vector2.zero;

        Image borderImage = borderObject.GetComponent<Image>();
        borderImage.sprite = rebellionBorderSprite;
        borderImage.color = Color.white;
        borderImage.preserveAspect = false;
        borderImage.raycastTarget = false;
    }

    private void UpdateRebellionScreenEffect()
    {
        if (rebellionEffectGroup == null)
        {
            return;
        }

        bool shouldBeVisible = IsControlsInverted;
        float targetVisibility = shouldBeVisible ? 1f : 0f;
        float fadeDuration = shouldBeVisible
            ? rebellionBorderFadeInDuration
            : rebellionBorderFadeOutDuration;
        rebellionEffectVisibility = fadeDuration <= 0f
            ? targetVisibility
            : Mathf.MoveTowards(
                rebellionEffectVisibility,
                targetVisibility,
                Time.unscaledDeltaTime / fadeDuration);

        float pulse = 1f;
        if (shouldBeVisible && rebellionBorderPulseSpeed > 0f)
        {
            float wave = (Mathf.Sin(Time.unscaledTime * rebellionBorderPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            pulse = Mathf.Lerp(0.72f, 1f, wave);
        }

        rebellionEffectGroup.alpha = rebellionEffectVisibility * rebellionBorderMaximumAlpha * pulse;
        if (rebellionEffectRect != null)
        {
            rebellionEffectRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.015f, pulse);
        }
    }

    public bool TryTriggerRebellion()
    {
        TrustManager trustManager = TrustManager.Instance;
        if (trustManager == null || !trustManager.CheckPenaltyRoll())
        {
            return false;
        }

        ForceTriggerRebellion();
        return true;
    }

    /// <summary>Immediately starts the rebellion effect without a trust probability roll.</summary>
    public void ForceTriggerRebellion()
    {
        float minDuration = Mathf.Min(confusionDurationRange.x, confusionDurationRange.y);
        float maxDuration = Mathf.Max(confusionDurationRange.x, confusionDurationRange.y);
        invertedControlTimer = UnityEngine.Random.Range(minDuration, maxDuration);
        rebellionCheckTimer = rebellionCheckInterval;
    }

    /// <summary>Call this from the fall/respawn system when the player falls.</summary>
    public void RegisterFall()
    {
        invertedControlTimer = 0f;
        rebellionCheckTimer = rebellionCheckInterval;
        TrustManager.Instance?.ReportFall();
    }

    /// <summary>
    /// Clears accumulated falling distance. CombatManager calls this whenever
    /// a scripted QTE movement starts or ends so a downward slash is never
    /// interpreted as a physics fall.
    /// </summary>
    public void ResetFallDamageTracking()
    {
        isTrackingFall = false;
        fallPeakY = GetFeetY();
    }

    private void UpdateFallDamageTracking()
    {
        if (!enableFallDamage)
        {
            ResetFallDamageTracking();
            return;
        }

        float feetY = GetFeetY();
        if (!isGrounded)
        {
            if (!isTrackingFall)
            {
                isTrackingFall = true;
                fallPeakY = feetY;
            }
            else
            {
                // Following the highest point means jumping upward does not
                // count as falling until the player actually starts descending.
                fallPeakY = Mathf.Max(fallPeakY, feetY);
            }

            return;
        }

        if (!isTrackingFall)
        {
            fallPeakY = feetY;
            return;
        }

        float fallDistance = Mathf.Max(0f, fallPeakY - feetY);
        ResetFallDamageTracking();
        if (fallDistance < minimumFallDistance)
        {
            return;
        }

        int extraDamage = Mathf.FloorToInt(
            (fallDistance - minimumFallDistance) / additionalDamageDistance);
        int damage = Mathf.Clamp(baseFallDamage + extraDamage, 1, maximumFallDamage);
        ApplyFallDamage(damage);
    }

    private float GetFeetY()
    {
        return bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;
    }

    private void ApplyFallDamage(int damage)
    {
        RegisterFall();
        OnFallDamaged?.Invoke(damage);
        CombatManager.Instance?.RegisterFallDamage(damage);
        PlayHurt(false);
    }

    private bool TryDropThroughPlatform()
    {
        if (bodyCollider == null || dropThroughRoutine != null)
        {
            return false;
        }

        Vector2 rayOrigin = groundCheck != null
            ? (Vector2)groundCheck.position + Vector2.up * 0.05f
            : new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y + 0.05f);
        float rayDistance = Mathf.Max(0.2f, groundCheckDistance + groundCheckSize.y + 0.1f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, oneWayPlatformLayer);
        if (hit.collider == null ||
            (hit.collider.GetComponent<OneWayPlatform>() == null &&
             hit.collider.GetComponent<PlatformEffector2D>() == null))
        {
            return false;
        }

        dropThroughRoutine = StartCoroutine(DropThroughPlatformRoutine(hit.collider));
        return true;
    }

    private IEnumerator DropThroughPlatformRoutine(Collider2D platformCollider)
    {
        ignoredPlatform = platformCollider;
        Physics2D.IgnoreCollision(bodyCollider, ignoredPlatform, true);
        isGrounded = false;
        shouldJump = false;
        SetAnimatorBool(IsGroundedHash, false);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, -dropDownSpeed));

        yield return new WaitForSecondsRealtime(dropThroughDuration);

        RestorePlatformCollision();
        dropThroughRoutine = null;
    }

    private void RestorePlatformCollision()
    {
        if (bodyCollider != null && ignoredPlatform != null)
        {
            Physics2D.IgnoreCollision(bodyCollider, ignoredPlatform, false);
        }

        ignoredPlatform = null;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        StopHorizontalMovement();
        SetAnimatorTrigger(AttackHash);

        yield return new WaitForSeconds(attackDuration);
        isAttacking = false;
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        StopHorizontalMovement();
        SetAnimatorBool(IsDashingHash, true);

        yield return new WaitForSeconds(dashDuration);

        SetAnimatorBool(IsDashingHash, false);
        isDashing = false;
    }

    public void PlayHurt()
    {
        PlayHurt(true);
    }

    private void PlayHurt(bool reportNormalHit)
    {
        if (isHurt)
        {
            return;
        }

        if (reportNormalHit)
        {
            TrustManager.Instance?.ReportNormalHit();
        }

        StartCoroutine(HurtRoutine());
    }

    private IEnumerator HurtRoutine()
    {
        isHurt = true;
        StopHorizontalMovement();
        SetAnimatorTrigger(HurtHash);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = hurtColor;
        }

        yield return new WaitForSeconds(hurtDuration);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
        }

        isHurt = false;
    }

    private void SetAnimatorBool(int parameter, bool value)
    {
        if (anim != null)
        {
            anim.SetBool(parameter, value);
        }
    }

    private void SetAnimatorTrigger(int parameter)
    {
        if (anim != null)
        {
            anim.SetTrigger(parameter);
        }
    }

    private void OnDisable()
    {
        RestorePlatformCollision();
        StopAllCoroutines();
        horizontalInput = 0f;
        shouldJump = false;
        isDashing = false;
        isAttacking = false;
        isHurt = false;
        dropThroughRoutine = null;
        ResetFallDamageTracking();
        rebellionEffectVisibility = 0f;
        if (rebellionEffectGroup != null)
        {
            rebellionEffectGroup.alpha = 0f;
        }

        SetAnimatorBool(IsRunningHash, false);
        SetAnimatorBool(IsDashingHash, false);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
        }
    }

    private void OnDestroy()
    {
        if (rebellionEffectCanvas != null)
        {
            Destroy(rebellionEffectCanvas);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Vector3 checkPosition = groundCheck.position;
        Vector3 castEndPosition = checkPosition + Vector3.down * groundCheckDistance;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(checkPosition, groundCheckSize);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(castEndPosition, groundCheckSize);
    }
}
