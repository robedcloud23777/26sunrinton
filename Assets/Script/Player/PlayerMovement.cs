using UnityEngine;
using UnityEngine.InputSystem;  
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    [Header("Ground Check (바닥 체크)")]
    public Transform groundCheck;       // 캐릭터 발밑에 둘 빈 오브젝트
    public LayerMask groundLayer;       // 바닥으로 인식할 레이어 (예: Ground)
    public float checkRadius = 0.2f;    // 바닥 감지 범위 반지름

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isGrounded;
    private bool shouldJump;
    private bool isFacingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 1. 키보드 입력 받기 (A/D, 좌우 방향키)
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 2. 점프 입력 받기 (스페이스바) - 입력 누락 방지를 위해 Update에서 감지
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            shouldJump = true;
        }

        // 3. 캐릭터 방향 전환 (Flip)
        Flip();
    }

    void FixedUpdate()
    {
        // 4. 실제로 바닥에 닿아있는지 체크
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);

        // 5. 좌우 이동 적용 (물리 속도 변경)
        Move();

        // 6. 점프 적용
        if (shouldJump)
        {
            Jump();
        }
    }

    void Move()
    {
        // Y축 속도(중력 가속도)는 그대로 유지하면서, X축 속도만 입력값에 맞춰 설정합니다.
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        shouldJump = false;
    }

    // 가는 방향에 맞춰 캐릭터 이미지를 좌우로 뒤집는 함수
    void Flip()
    {
        if ((horizontalInput > 0 && !isFacingRight) || (horizontalInput < 0 && isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }

    // 에디터 뷰에서 바닥 체크 범위를 빨간색 원으로 시각화해 줍니다. (디버깅용)
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        }
    }
}
