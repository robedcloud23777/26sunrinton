using UnityEngine;

/// <summary>Minimal target component used by TargetingSystem and CombatManager.</summary>
public sealed class Enemy : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;

    private Animator anim;

    // 문자열 비교 성능 낭비를 방지하기 위한 해시 값 캐싱 (유니티 최적화 표준)
    private static readonly int AttackTriggerHash = Animator.StringToHash("attack");

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;

    private void Awake()
    {
        // 스스로에게서 Animator를 찾고, 없다면 자식 오브젝트(Visual)에서도 찾습니다.
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }
    }

    /// <summary>
    /// 에너미가 공격할 때 CombatManager나 AI 스크립트에서 이 함수를 호출하면 애니메이션이 실행됩니다.
    /// </summary>
    public void PlayAttackAnimation()
    {
        if (anim != null)
        {
            anim.SetTrigger(AttackTriggerHash);
        }
        else
        {
            Debug.LogWarning($"[Enemy] {gameObject.name}에 Animator 컴포넌트가 존재하지 않습니다!");
        }
    }
}