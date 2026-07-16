using UnityEngine;

/// <summary>
/// Configures a collider as a Maple-style one-way platform: pass from below,
/// land from above. PlayerMovement handles down+jump drop-through.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public sealed class OneWayPlatform : MonoBehaviour
{
    private void Reset()
    {
        ConfigurePlatform();
    }

    private void Awake()
    {
        ConfigurePlatform();
    }

    private void ConfigurePlatform()
    {
        Collider2D platformCollider = GetComponent<Collider2D>();
        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();

        platformCollider.isTrigger = false;
        platformCollider.usedByEffector = true;
        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = 180f;
        effector.rotationalOffset = 0f;

        int platformLayer = LayerMask.NameToLayer("OneWayPlatform");
        if (platformLayer >= 0)
        {
            gameObject.layer = platformLayer;
        }
    }
}
