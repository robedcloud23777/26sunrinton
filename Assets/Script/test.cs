using UnityEngine;
using UnityEngine.InputSystem;

public class test : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CinematicModeUI.Instance.EnterCinematicMode();
        }
    }
}