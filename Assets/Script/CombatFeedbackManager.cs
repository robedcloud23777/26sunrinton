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

    [Header("Intent Negotiation Ghosts")]
    [SerializeField] private Color characterIntentColor = new Color(0.7f, 0.75f, 0.8f, 0.3f);
    [SerializeField] private Color playerProposalColor = new Color(1f, 0.72f, 0.12f, 0.45f);
    [SerializeField] private Color acceptedIntentColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField, Range(2, 10)] private int intentGhostCount = 5;
    [SerializeField, Min(0.02f)] private float intentGhostInterval = 0.08f;
    [SerializeField, Min(0.05f)] private float intentGhostLifetime = 0.5f;
    [SerializeField, Min(0f)] private float intentLoopPause = 0.12f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip dashImpactSound;
    [SerializeField] private AudioClip qteSuccessSound;
    [SerializeField] private AudioClip qteFailSound;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    private readonly List<GameObject> activeAfterimages = new List<GameObject>();
    private readonly List<GameObject> activeCharacterIntentGhosts = new List<GameObject>();
    private readonly List<GameObject> activeProposalGhosts = new List<GameObject>();
    private readonly List<GameObject> activeAcceptedGhosts = new List<GameObject>();
    private SpriteRenderer afterimageSource;
    private Coroutine afterimageTrailRoutine;
    private Coroutine characterIntentRoutine;
    private Coroutine playerProposalRoutine;

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

    public void BeginCharacterIntentPreview(
        Transform player,
        Transform target,
        IReadOnlyList<Sprite> previewSprites)
    {
        EndIntentPreviews(true);
        SpriteRenderer source = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        if (source != null && target != null)
        {
            characterIntentRoutine = StartCoroutine(IntentTrailLoopRoutine(
                source,
                target,
                previewSprites,
                characterIntentColor,
                activeCharacterIntentGhosts));
        }
    }

    public void BeginPlayerProposalPreview(
        Transform player,
        Transform target,
        IReadOnlyList<Sprite> previewSprites)
    {
        EndPlayerProposalPreview(true);
        SpriteRenderer source = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        if (source != null && target != null)
        {
            playerProposalRoutine = StartCoroutine(IntentTrailLoopRoutine(
                source,
                target,
                previewSprites,
                playerProposalColor,
                activeProposalGhosts));
        }
    }

    public void EndPlayerProposalPreview(bool clearImmediately)
    {
        if (playerProposalRoutine != null)
        {
            StopCoroutine(playerProposalRoutine);
            playerProposalRoutine = null;
        }

        if (clearImmediately)
        {
            ClearGhosts(activeProposalGhosts);
        }
    }

    public void ShowAcceptedIntent(
        Transform player,
        Transform target,
        IReadOnlyList<Sprite> previewSprites)
    {
        EndIntentPreviews(true);
        SpriteRenderer source = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        if (source == null || target == null)
        {
            return;
        }

        Vector3 origin = source.transform.position;
        for (int i = 0; i < intentGhostCount; i++)
        {
            float pathProgress = (i + 1f) / (intentGhostCount + 1f);
            Vector3 position = Vector3.Lerp(origin, target.position, pathProgress);
            Sprite attackSprite = GetAttackPreviewSprite(previewSprites, i);
            SpawnIntentGhost(source, attackSprite, position, acceptedIntentColor, activeAcceptedGhosts);
        }
    }

    public void EndIntentPreviews(bool clearImmediately)
    {
        if (characterIntentRoutine != null)
        {
            StopCoroutine(characterIntentRoutine);
            characterIntentRoutine = null;
        }

        if (playerProposalRoutine != null)
        {
            StopCoroutine(playerProposalRoutine);
            playerProposalRoutine = null;
        }

        if (clearImmediately)
        {
            ClearGhosts(activeCharacterIntentGhosts);
            ClearGhosts(activeProposalGhosts);
            ClearGhosts(activeAcceptedGhosts);
        }
    }

    private IEnumerator IntentTrailLoopRoutine(
        SpriteRenderer source,
        Transform target,
        IReadOnlyList<Sprite> previewSprites,
        Color ghostColor,
        List<GameObject> ghostList)
    {
        while (source != null && target != null)
        {
            Vector3 origin = source.transform.position;
            for (int i = 0; i < intentGhostCount; i++)
            {
                if (source == null || target == null)
                {
                    yield break;
                }

                float pathProgress = (i + 1f) / (intentGhostCount + 1f);
                Vector3 position = Vector3.Lerp(origin, target.position, pathProgress);
                Sprite attackSprite = GetAttackPreviewSprite(previewSprites, i);
                SpawnIntentGhost(source, attackSprite, position, ghostColor, ghostList);
                yield return new WaitForSecondsRealtime(intentGhostInterval);
            }

            if (intentLoopPause > 0f)
            {
                yield return new WaitForSecondsRealtime(intentLoopPause);
            }
        }
    }

    private void SpawnIntentGhost(
        SpriteRenderer source,
        Sprite attackSprite,
        Vector3 position,
        Color ghostColor,
        List<GameObject> ghostList)
    {
        if (source == null)
        {
            return;
        }

        if (attackSprite == null)
        {
            return;
        }

        GameObject ghost = new GameObject("Intent Ghost");
        Transform sourceTransform = source.transform;
        ghost.transform.SetPositionAndRotation(position, sourceTransform.rotation);
        ghost.transform.localScale = sourceTransform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        CopySpriteRenderer(source, ghostRenderer, attackSprite);
        ghostRenderer.color = ghostColor;

        ghostList.Add(ghost);
        StartCoroutine(FadeGhostRoutine(ghost, ghostRenderer, intentGhostLifetime, ghostList));
    }

    private static Sprite GetAttackPreviewSprite(IReadOnlyList<Sprite> previewSprites, int ghostIndex)
    {
        if (previewSprites == null || previewSprites.Count == 0)
        {
            return null;
        }

        return previewSprites[ghostIndex % previewSprites.Count];
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
        CopySpriteRenderer(afterimageSource, ghostRenderer, afterimageSource.sprite);

        Color sourceColor = afterimageSource.color;
        ghostRenderer.color = new Color(
            sourceColor.r * afterimageColor.r,
            sourceColor.g * afterimageColor.g,
            sourceColor.b * afterimageColor.b,
            sourceColor.a * afterimageColor.a);

        activeAfterimages.Add(ghost);
        StartCoroutine(FadeGhostRoutine(ghost, ghostRenderer, afterimageLifetime, activeAfterimages));
    }

    private void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer destination, Sprite sprite)
    {
        destination.sprite = sprite;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.sharedMaterial = source.sharedMaterial;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder + afterimageSortingOffset;
        destination.maskInteraction = source.maskInteraction;
    }

    private IEnumerator FadeGhostRoutine(
        GameObject ghost,
        SpriteRenderer ghostRenderer,
        float lifetime,
        List<GameObject> ownerList)
    {
        Color startColor = ghostRenderer.color;
        float elapsed = 0f;

        while (ghost != null && elapsed < lifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            Color fadedColor = startColor;
            fadedColor.a = Mathf.Lerp(startColor.a, 0f, elapsed / lifetime);
            ghostRenderer.color = fadedColor;
            yield return null;
        }

        ownerList.Remove(ghost);
        if (ghost != null)
        {
            Destroy(ghost);
        }
    }

    private void ClearGhosts(List<GameObject> ghosts)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            if (ghosts[i] != null)
            {
                Destroy(ghosts[i]);
            }
        }

        ghosts.Clear();
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
        EndIntentPreviews(true);
    }
}
