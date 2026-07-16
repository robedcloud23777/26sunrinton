using UnityEngine;

public sealed class CombetArenaTrigger : MonoBehaviour
{
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private Transform enemyGroup;

    private bool hasStarted;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStarted || !other.TryGetComponent(out PlayerMovement player))
        {
            return;
        }

        hasStarted = true;
        combatManager.StartCombatSequence(enemyGroup, player.transform);
    }
}
