using UnityEngine;

/// <summary>Plays optional visual and audio feedback without changing combat state.</summary>
public sealed class CombatFeedbackManager : MonoBehaviour
{
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("VFX")]
    [SerializeField] private GameObject slashEffectPrefab;
    [SerializeField] private GameObject failEffectPrefab;
    [SerializeField, Min(0f)] private float effectLifetime = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip dashImpactSound;
    [SerializeField] private AudioClip qteSuccessSound;
    [SerializeField] private AudioClip qteFailSound;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

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
}
