using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    [Header("Ground Check (바닥 체크)")]
    public Transform groundCheck;
    public LayerMask groundLayer = ~0;
    public float checkRadius = 0.2f;

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
    private bool shouldJump;
    private bool isFacingRight = true;
    private bool isDashing;
    private bool isAttacking;
    private bool isHurt;

    public bool IsGrounded => isGrounded;
    public bool IsBusy => isDashing || isAttacking || isHurt;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

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
    }

    private void Update()
    {
        if (isHurt || isDashing)
        {
            StopHorizontalMovement();
            return;
        }

        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }

        if (isAttacking)
        {
            StopHorizontalMovement();
            return;
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");
        SetAnimatorBool(IsRunningHash, !Mathf.Approximately(horizontalInput, 0f));
        FlipTowardsInput();

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            shouldJump = true;
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
        if (groundCheck != null)
        {
            return Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer) != null;
        }

        if (bodyCollider != null)
        {
            return bodyCollider.IsTouchingLayers(groundLayer);
        }

        return false;
    }

    private void Move()
    {
        float velocityX = IsBusy ? 0f : horizontalInput * moveSpeed;
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
        StopAllCoroutines();
        horizontalInput = 0f;
        shouldJump = false;
        isDashing = false;
        isAttacking = false;
        isHurt = false;
        SetAnimatorBool(IsRunningHash, false);
        SetAnimatorBool(IsDashingHash, false);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        }
    }
}
