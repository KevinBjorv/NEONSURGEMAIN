using UnityEngine;

public class PlatformSpecificUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject joystickUI;  // Joystick background GameObject
    public GameObject dashButtonUI; // Dash button GameObject

    private void Awake()
    {
#if UNITY_IOS
            // Enable joystick and dash button for iOS
            if (joystickUI) joystickUI.SetActive(true);
            if (dashButtonUI) dashButtonUI.SetActive(true);
#else
        // Disable joystick and dash button for non-iOS platforms
        if (joystickUI) joystickUI.SetActive(false);
        if (dashButtonUI) dashButtonUI.SetActive(false);
#endif
    }
}
