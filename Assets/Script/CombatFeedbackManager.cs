using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Plays optional visual and audio feedback without changing combat state.</summary>
public sealed class CombatFeedbackManager : MonoBehaviour
{
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("VFX")]
    [SerializeField] private GameObject slashEffectPrefab;
    [SerializeField] private GameObject failEffectPrefab;
    [SerializeField, Min(0f)] private float effectLifetime = 1f;

    [Header("Player Afterimages")]
    [SerializeField, Min(0.01f)] private float afterimageSpawnInterval = 0.04f;
    [SerializeField, Min(0.05f)] private float afterimageLifetime = 0.8f;
    [SerializeField, Min(0f)] private float afterimageMinDistance = 0.04f;
    [SerializeField] private Color afterimageColor = new Color(0.45f, 0.85f, 1f, 0.38f);
    [SerializeField] private int afterimageSortingOffset = -1;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip dashImpactSound;
    [SerializeField] private AudioClip qteSuccessSound;
    [SerializeField] private AudioClip qteFailSound;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    private readonly List<GameObject> activeAfterimages = new List<GameObject>();
    private SpriteRenderer afterimageSource;
    private Coroutine afterimageTrailRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>Assigns effect prefabs when an example scene is created at runtime.</summary>
    public void Configure(GameObject slashPrefab, GameObject failurePrefab)
    {
        slashEffectPrefab = slashPrefab;
        failEffectPrefab = failurePrefab;
    }

    public void PlayImpactFeedback(Vector3 hitPosition)
    {
        PlaySoundWithRandomPitch(dashImpactSound);
    }

    public void PlaySlashFeedback(Vector3 hitPosition)
    {
        SpawnEffect(slashEffectPrefab, hitPosition);
        PlaySoundWithRandomPitch(qteSuccessSound);
    }

    public void PlayFailFeedback(Vector3 playerPosition)
    {
        SpawnEffect(failEffectPrefab, playerPosition);
        PlaySoundWithRandomPitch(qteFailSound);
    }

    /// <summary>Captures the player's current animation frame immediately.</summary>
    public void CapturePlayerAfterimage()
    {
        SpawnAfterimage();
    }

    /// <summary>Continuously leaves fading copies while the slash attack is moving.</summary>
    public void BeginCombatAfterimages(Transform player)
    {
        EndCombatAfterimages(false);
        afterimageSource = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        if (afterimageSource != null)
        {
            afterimageTrailRoutine = StartCoroutine(AfterimageTrailRoutine());
        }
    }

    /// <summary>Stops new ghosts. Existing ghosts can finish fading naturally.</summary>
    public void EndCombatAfterimages(bool clearImmediately = false)
    {
        if (afterimageTrailRoutine != null)
        {
            StopCoroutine(afterimageTrailRoutine);
            afterimageTrailRoutine = null;
        }

        afterimageSource = null;
        if (!clearImmediately)
        {
            return;
        }

        for (int i = activeAfterimages.Count - 1; i >= 0; i--)
        {
            if (activeAfterimages[i] != null)
            {
                Destroy(activeAfterimages[i]);
            }
        }

        activeAfterimages.Clear();
    }

    private IEnumerator AfterimageTrailRoutine()
    {
        Vector3 previousSpawnPosition = afterimageSource.transform.position;
        float spawnTimer = 0f;

        while (afterimageSource != null)
        {
            spawnTimer += Time.unscaledDeltaTime;
            Vector3 currentPosition = afterimageSource.transform.position;
            if (spawnTimer >= afterimageSpawnInterval &&
                Vector3.Distance(previousSpawnPosition, currentPosition) >= afterimageMinDistance)
            {
                SpawnAfterimage();
                previousSpawnPosition = currentPosition;
                spawnTimer = 0f;
            }

            yield return null;
        }

        afterimageTrailRoutine = null;
    }

    private void SpawnAfterimage()
    {
        if (afterimageSource == null || afterimageSource.sprite == null || !afterimageSource.enabled)
        {
            return;
        }

        GameObject ghost = new GameObject("Combat Afterimage");
        Transform sourceTransform = afterimageSource.transform;
        ghost.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        ghost.transform.localScale = sourceTransform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = afterimageSource.sprite;
        ghostRenderer.flipX = afterimageSource.flipX;
        ghostRenderer.flipY = afterimageSource.flipY;
        ghostRenderer.drawMode = afterimageSource.drawMode;
        ghostRenderer.size = afterimageSource.size;
        ghostRenderer.sharedMaterial = afterimageSource.sharedMaterial;
        ghostRenderer.sortingLayerID = afterimageSource.sortingLayerID;
        ghostRenderer.sortingOrder = afterimageSource.sortingOrder + afterimageSortingOffset;
        ghostRenderer.maskInteraction = afterimageSource.maskInteraction;

        Color sourceColor = afterimageSource.color;
        ghostRenderer.color = new Color(
            sourceColor.r * afterimageColor.r,
            sourceColor.g * afterimageColor.g,
            sourceColor.b * afterimageColor.b,
            sourceColor.a * afterimageColor.a);

        activeAfterimages.Add(ghost);
        StartCoroutine(FadeAfterimageRoutine(ghost, ghostRenderer));
    }

    private IEnumerator FadeAfterimageRoutine(GameObject ghost, SpriteRenderer ghostRenderer)
    {
        Color startColor = ghostRenderer.color;
        float elapsed = 0f;

        while (ghost != null && elapsed < afterimageLifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            Color fadedColor = startColor;
            fadedColor.a = Mathf.Lerp(startColor.a, 0f, elapsed / afterimageLifetime);
            ghostRenderer.color = fadedColor;
            yield return null;
        }

        activeAfterimages.Remove(ghost);
        if (ghost != null)
        {
            Destroy(ghost);
        }
    }

    private void SpawnEffect(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject effect = Instantiate(prefab, position, Quaternion.identity);
        effect.SetActive(true);
        Destroy(effect, effectLifetime);
    }

    private void PlaySoundWithRandomPitch(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }

        float minPitch = Mathf.Min(pitchRange.x, pitchRange.y);
        float maxPitch = Mathf.Max(pitchRange.x, pitchRange.y);
        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip);
    }

    private void OnDisable()
    {
        EndCombatAfterimages(true);
    }
}
