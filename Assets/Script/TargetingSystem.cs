using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Finds nearby enemies in a group and manages their optional target indicators.</summary>
public sealed class TargetingSystem : MonoBehaviour
{
    [Header("Indicator")]
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField, Min(0f)] private float indicatorSpawnInterval = 0.08f;

    private readonly Queue<GameObject> activeIndicators = new Queue<GameObject>();

    /// <summary>Configures this component when a demo scene is assembled in code.</summary>
    public void Configure(GameObject prefab, float spawnInterval = 0.08f)
    {
        indicatorPrefab = prefab;
        indicatorSpawnInterval = Mathf.Max(0f, spawnInterval);
    }

    public Queue<Enemy> GetSortedTargets(Transform player, Transform enemyGroup)
    {
        if (player == null || enemyGroup == null)
        {
            return new Queue<Enemy>();
        }

        IEnumerable<Enemy> sortedEnemies = enemyGroup
            .GetComponentsInChildren<Enemy>(false)
            .Where(enemy => enemy != null && enemy.gameObject.activeInHierarchy)
            .OrderBy(enemy => (enemy.AimPoint.position - player.position).sqrMagnitude);

        return new Queue<Enemy>(sortedEnemies);
    }

    public IEnumerator SpawnIndicatorsRoutine(IEnumerable<Enemy> targets)
    {
        ClearIndicators();

        if (targets == null)
        {
            yield break;
        }

        foreach (Enemy target in targets)
        {
            if (target == null || indicatorPrefab == null)
            {
                continue;
            }

            GameObject indicator = Instantiate(indicatorPrefab, target.AimPoint.position, Quaternion.identity, target.transform);
            indicator.SetActive(true);
            activeIndicators.Enqueue(indicator);

            if (indicatorSpawnInterval > 0f)
            {
                yield return new WaitForSecondsRealtime(indicatorSpawnInterval);
            }
        }
    }

    public void RemoveFirstIndicator()
    {
        while (activeIndicators.Count > 0)
        {
            GameObject indicator = activeIndicators.Dequeue();
            if (indicator != null)
            {
                Destroy(indicator);
                return;
            }
        }
    }

    public void ClearIndicators()
    {
        while (activeIndicators.Count > 0)
        {
            GameObject indicator = activeIndicators.Dequeue();
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }
    }
}
