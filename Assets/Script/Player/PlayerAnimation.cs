using System.Collections;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator anim;
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = true;

    // 상태 제어 변수
    private bool isDashing = false;
    private bool isAttacking = false;
    private bool isHurt = false;
    bool isMoving;
    void Start()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 피격 중이거나 대시 중일 때는 다른 입력을 막아 꼬임을 방지합니다.
        if (isHurt || isDashing) return;

        // 1. 공격 (마우스 좌클릭)
        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }

        // 공격 중일 때는 이동 입력을 차단합니다.
        if (isAttacking) return;

        // 2. 좌우 이동 (A, D)
        float horizontal = Input.GetAxisRaw("Horizontal");
        isMoving = horizontal != 0;
        Debug.Log(isMoving);
        anim.SetBool("isRunning", isMoving);

        if (horizontal > 0 && !isFacingRight) Flip();
        else if (horizontal < 0 && isFacingRight) Flip();

        // 3. 점프 (Space 누르고 있으면 공중 상태, 떼면 착지)
        if (Input.GetButton("Jump"))
        {
            anim.SetBool("isGrounded", false);
        }
        else
        {
            anim.SetBool("isGrounded", true);
        }

        // 4. 대시 (Left Shift)
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            StartCoroutine(DashRoutine());
        }

        // 5. 피격 (G 키 - 기존 마우스 클릭과 충돌 방지를 위해 키 변경!)
        if (Input.GetKeyDown(KeyCode.G))
        {
            StartCoroutine(HurtRoutine());
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    // --- 공격 코루틴 ---
    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        anim.SetBool("isRunning", false); // 공격 시작 시 달리기 애니메이션 강제 OFF
        anim.SetTrigger("attack");

        // 공격 애니메이션이 재생될 시간만큼 대기 (클립 길이에 따라 0.2s~0.4s 사이로 조절하세요)
        yield return new WaitForSeconds(0.35f);

        isAttacking = false;
    }

    // --- 대시 코루틴 ---
    private IEnumerator DashRoutine()
    {
        isDashing = true;
        anim.SetBool("isDashing", true);

        yield return new WaitForSeconds(0.25f); // 대시 포즈 유지 시간

        anim.SetBool("isDashing", false);
        isDashing = false;
    }

    // --- 피격 코루틴 ---
    private IEnumerator HurtRoutine()
    {
        isHurt = true;
        anim.SetTrigger("hurt");

        // 피격 시 빨갛게 깜빡임
        spriteRenderer.color = new Color(1f, 0.5f, 0.5f, 1f);

        yield return new WaitForSeconds(0.4f); // 피격 모션 유지 시간

        spriteRenderer.color = Color.white;
        isHurt = false;
    }
}