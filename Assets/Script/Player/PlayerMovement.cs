using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;
    [SerializeField] private PhysicsMaterial2D frictionlessMaterial;

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

    [Header("Trust Rebellion")]
    [SerializeField, Min(1f)] private float rebellionCheckInterval = 10f;
    [SerializeField] private Vector2 confusionDurationRange = new Vector2(3f, 5f);

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

    public bool IsGrounded => isGrounded;
    public Vector2 GroundNormal => groundNormal;
    public bool IsBusy => isDashing || isAttacking || isHurt;
    public bool IsControlsInverted => invertedControlTimer > 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        if (bodyCollider != null && frictionlessMaterial != null)
        {
            bodyCollider.sharedMaterial = frictionlessMaterial;
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
    }

    private void Update()
    {
        UpdateTrustRebellion();

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

    public bool TryTriggerRebellion()
    {
        TrustManager trustManager = TrustManager.Instance;
        if (trustManager == null || !trustManager.CheckPenaltyRoll())
        {
            return false;
        }

        float minDuration = Mathf.Min(confusionDurationRange.x, confusionDurationRange.y);
        float maxDuration = Mathf.Max(confusionDurationRange.x, confusionDurationRange.y);
        invertedControlTimer = UnityEngine.Random.Range(minDuration, maxDuration);
        return true;
    }

    /// <summary>Call this from the fall/respawn system when the player falls.</summary>
    public void RegisterFall()
    {
        invertedControlTimer = 0f;
        rebellionCheckTimer = rebellionCheckInterval;
        TrustManager.Instance?.ReportFall();
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
        if (isHurt)
        {
            return;
        }

        TrustManager.Instance?.ReportNormalHit();
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
        SetAnimatorBool(IsRunningHash, false);
        SetAnimatorBool(IsDashingHash, false);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
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
