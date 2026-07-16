using System;
using UnityEngine;

/// <summary>
/// Compatibility component kept temporarily so older scenes do not show a
/// Missing Script. All player animation logic now lives in PlayerMovement.
/// </summary>
[Obsolete("PlayerAnimation has been merged into PlayerMovement.")]
[AddComponentMenu("")]
public sealed class PlayerAnimation : MonoBehaviour
{
}
