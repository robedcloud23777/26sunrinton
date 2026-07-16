using UnityEngine;

/// <summary>Finishes the game when the player enters this trigger.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class Final : MonoBehaviour
{
    [SerializeField] private string clearMessage = "Clear";

    private bool hasTriggered;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        PlayerDeathController endPresenter = FindAnyObjectByType<PlayerDeathController>();
        if (endPresenter == null)
        {
            GameObject presenterObject = new GameObject("Game End System");
            endPresenter = presenterObject.AddComponent<PlayerDeathController>();
        }

        hasTriggered = true;
        string message = string.IsNullOrWhiteSpace(clearMessage) ? "Clear" : clearMessage;
        endPresenter.BeginEnding(message);
    }
}
