using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Generates and evaluates a WASD quick-time event. This component owns
/// input evaluation and slow motion only; presentation and combat consequences
/// are provided to subscribers through events.
/// </summary>
public sealed class QTEController : MonoBehaviour
{
    [Header("QTE Settings")]
    [SerializeField, Min(0.01f)] private float timeLimit = 2f;
    [SerializeField, Min(1)] private int minKeys = 3;
    [SerializeField, Min(1)] private int maxKeys = 5;
    [SerializeField, Range(0f, 1f)] private float slowMotionScale = 0.1f;

    public event Action OnQTESuccess;
    public event Action OnQTEFail;
    public event Action OnQTEImmunityUsed;
    public event Action<IReadOnlyList<KeyCode>> OnQTEGenerated;
    public event Action<int> OnQTEKeyHit;

    public bool IsActive => isQTEActive;
    public float RemainingTime => Mathf.Max(0f, timeLimit - elapsedTime);

    private readonly List<KeyCode> requiredKeys = new List<KeyCode>();
    private readonly HashSet<int> hiddenKeyIndices = new HashSet<int>();
    private readonly KeyCode[] possibleKeys =
    {
        KeyCode.W,
        KeyCode.A,
        KeyCode.S,
        KeyCode.D
    };

    private int currentKeyIndex;
    private float elapsedTime;
    private bool isQTEActive;
    private bool hasInputImmunity;
    private float previousTimeScale;
    private float previousFixedDeltaTime;

    private void OnValidate()
    {
        minKeys = Mathf.Max(1, minKeys);
        maxKeys = Mathf.Max(minKeys, maxKeys);
        timeLimit = Mathf.Max(0.01f, timeLimit);
    }

    private void Update()
    {
        if (!isQTEActive)
        {
            return;
        }

        // QTE duration must use real time, not the slowed game time.
        elapsedTime += Time.unscaledDeltaTime;
        if (elapsedTime >= timeLimit)
        {
            Complete(false);
            return;
        }

        EvaluateDirectionInput();
    }

    /// <summary>Starts a fresh QTE. Returns false when another QTE is already active.</summary>
    public bool StartQTE()
    {
        if (isQTEActive)
        {
            return false;
        }

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = previousFixedDeltaTime * slowMotionScale;

        isQTEActive = true;
        hasInputImmunity = TrustManager.Instance != null && TrustManager.Instance.CheckBonusRoll();

        currentKeyIndex = 0;
        elapsedTime = 0f;
        GenerateRandomPattern();
        return true;
    }

    /// <summary>Stops an active QTE without publishing a success or failure result.</summary>
    public void CancelQTE()
    {
        if (!isQTEActive)
        {
            return;
        }

        RestoreTimeScale();
        isQTEActive = false;
    }

    private void OnDisable()
    {
        CancelQTE();
    }

    /// <summary>
    /// Creates a new random WASD sequence and publishes it to the UI.
    /// Call this only when starting a QTE so the displayed sequence and the
    /// sequence evaluated by input always stay identical.
    /// </summary>
    private void GenerateRandomPattern()
    {
        requiredKeys.Clear();
        hiddenKeyIndices.Clear();
        int keyCount = UnityEngine.Random.Range(minKeys, maxKeys + 1);

        for (int i = 0; i < keyCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, possibleKeys.Length);
            requiredKeys.Add(possibleKeys[randomIndex]);
        }

        GenerateHiddenKeyIndices();

        // The event notifies listeners after the complete random pattern exists.
        // The read-only view prevents UI code from changing the answer sequence.
        OnQTEGenerated?.Invoke(requiredKeys.AsReadOnly());
    }

    public bool IsKeyHidden(int keyIndex)
    {
        return hiddenKeyIndices.Contains(keyIndex);
    }

    private void GenerateHiddenKeyIndices()
    {
        TrustManager trustManager = TrustManager.Instance;
        if (requiredKeys.Count == 0 || trustManager == null || !trustManager.CheckPenaltyRoll())
        {
            return;
        }

        int maximumHiddenKeys = Mathf.Min(2, requiredKeys.Count);
        int hiddenKeyCount = UnityEngine.Random.Range(1, maximumHiddenKeys + 1);
        while (hiddenKeyIndices.Count < hiddenKeyCount)
        {
            hiddenKeyIndices.Add(UnityEngine.Random.Range(0, requiredKeys.Count));
        }
    }

    private void EvaluateDirectionInput()
    {
        if (!TryGetPressedDirection(out KeyCode pressedKey))
        {
            return;
        }

        if (pressedKey != requiredKeys[currentKeyIndex])
        {
            if (!hasInputImmunity)
            {
                Complete(false);
                return;
            }

            hasInputImmunity = false;
            OnQTEImmunityUsed?.Invoke();
        }

        AcceptCurrentKey();
    }

    private void AcceptCurrentKey()
    {
        OnQTEKeyHit?.Invoke(currentKeyIndex);
        currentKeyIndex++;

        if (currentKeyIndex >= requiredKeys.Count)
        {
            Complete(true);
        }
    }

    private bool TryGetPressedDirection(out KeyCode pressedKey)
    {
#if ENABLE_INPUT_SYSTEM
        // The project uses the Input System package. Reading Keyboard.current
        // keeps the QTE functional even when the legacy Input Manager is off.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.wasPressedThisFrame)
            {
                pressedKey = KeyCode.W;
                return true;
            }

            if (keyboard.aKey.wasPressedThisFrame)
            {
                pressedKey = KeyCode.A;
                return true;
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                pressedKey = KeyCode.S;
                return true;
            }

            if (keyboard.dKey.wasPressedThisFrame)
            {
                pressedKey = KeyCode.D;
                return true;
            }
        }
#endif

        // Retain support for projects configured for the legacy Input Manager.
#if !ENABLE_INPUT_SYSTEM
        foreach (KeyCode key in possibleKeys)
        {
            if (Input.GetKeyDown(key))
            {
                pressedKey = key;
                return true;
            }
        }
#endif

        pressedKey = KeyCode.None;
        return false;
    }

    private void Complete(bool succeeded)
    {
        RestoreTimeScale();
        isQTEActive = false;

        if (succeeded)
        {
            if (elapsedTime <= timeLimit * 0.5f)
            {
                TrustManager.Instance?.ReportQTEPerfect();
            }

            OnQTESuccess?.Invoke();
        }
        else
        {
            TrustManager.Instance?.ReportQTEFail();
            OnQTEFail?.Invoke();
        }
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
    }
}
