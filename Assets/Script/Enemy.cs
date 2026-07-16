using UnityEngine;

/// <summary>Minimal target component used by TargetingSystem and CombatManager.</summary>
public sealed class Enemy : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;

    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
}
