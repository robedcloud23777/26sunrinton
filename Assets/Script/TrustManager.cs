using System;
using UnityEngine;

/// <summary>Owns the persistent 0-100 trust score and all trust probability rolls.</summary>
[DefaultExecutionOrder(-100)]
public sealed class TrustManager : MonoBehaviour
{
    private static TrustManager instance;

    public static TrustManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<TrustManager>();
                if (instance == null && Application.isPlaying)
                {
                    GameObject managerObject = new GameObject("Trust Manager");
                    instance = managerObject.AddComponent<TrustManager>();
                }
            }

            return instance;
        }
    }

    [Header("Trust")]
    [SerializeField, Range(0f, 100f)] private float currentTrust = 50f;

    [Header("Trust Weights")]
    [SerializeField] private float normalHitPenalty = -2f;
    [SerializeField] private float fallPenalty = -4f;
    [SerializeField] private float qteFailPenalty = -6f;
    [SerializeField] private float qtePerfectReward = 2f;
    [SerializeField] private float noDamageCombatReward = 5f;

    [Header("Difficulty Curve")]
    [SerializeField, Range(0f, 50f)] private float penaltyStartTrust = 25f;
    [SerializeField, Range(0f, 1f)] private float maximumPenaltyProbability = 0.2f;
    [SerializeField, Min(1f)] private float penaltyCurveExponent = 2f;
    [SerializeField, Range(50f, 100f)] private float bonusStartTrust = 65f;
    [SerializeField, Range(0f, 1f)] private float maximumBonusProbability = 0.35f;

    [Header("Safety")]
    [SerializeField, Min(0f)] private float fallPenaltyGracePeriod = 5f;

    public event Action<float, float> OnTrustChanged;

    public float CurrentTrust => currentTrust;
    public float PenaltyProbability
    {
        get
        {
            if (currentTrust > penaltyStartTrust || penaltyStartTrust <= 0f)
            {
                return 0f;
            }

            float lowTrustProgress = Mathf.InverseLerp(penaltyStartTrust, 0f, currentTrust);
            return Mathf.Pow(lowTrustProgress, penaltyCurveExponent) * maximumPenaltyProbability;
        }
    }

    public float BonusProbability
    {
        get
        {
            if (currentTrust < bonusStartTrust || bonusStartTrust >= 100f)
            {
                return 0f;
            }

            float highTrustProgress = Mathf.InverseLerp(bonusStartTrust, 100f, currentTrust);
            return highTrustProgress * maximumBonusProbability;
        }
    }

    private float penaltyBlockedUntil;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        currentTrust = Mathf.Clamp(currentTrust, 0f, 100f);
        penaltyCurveExponent = Mathf.Max(1f, penaltyCurveExponent);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void ModifyTrust(float amount)
    {
        float previousTrust = currentTrust;
        currentTrust = Mathf.Clamp(currentTrust + amount, 0f, 100f);
        if (!Mathf.Approximately(previousTrust, currentTrust))
        {
            OnTrustChanged?.Invoke(previousTrust, currentTrust);
            Debug.Log($"Trust {amount:+0.##;-0.##;0} -> {currentTrust:0.##}");
        }
    }

    public void ReportNormalHit() => ModifyTrust(normalHitPenalty);

    public void ReportFall()
    {
        ModifyTrust(fallPenalty);
        penaltyBlockedUntil = Time.unscaledTime + fallPenaltyGracePeriod;
    }

    public void ReportQTEFail() => ModifyTrust(qteFailPenalty);
    public void ReportQTEPerfect() => ModifyTrust(qtePerfectReward);
    public void ReportCombatNoDamage() => ModifyTrust(noDamageCombatReward);

    public bool CheckPenaltyRoll()
    {
        if (Time.unscaledTime < penaltyBlockedUntil)
        {
            return false;
        }

        return UnityEngine.Random.value < PenaltyProbability;
    }

    public bool CheckBonusRoll()
    {
        return UnityEngine.Random.value < BonusProbability;
    }
}
