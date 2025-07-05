using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class PhoneUIManager : MonoBehaviour
{
    [Header("References")]
    public GameObject movementJoystick;
    public GameObject dashButton;
    public GameObject aimingJoystick;

    [Header("UI Elements to move")]
    public RectTransform weaponIconTransform;

    [Header("Buttons to remove")]
    public GameObject settingsButton;
    public GameObject quitButton;
    public RectTransform startMenuButtons;
    public float startMenuButtonsOffsetY = -55f;

[HideInInspector]
public bool isMobile = Application.isMobilePlatform;
    void Awake()
    {
        #if UNITY_EDITOR
        // In the Editor, simulate mobile if the Build Target is Android or iOS
        var activeTarget = EditorUserBuildSettings.activeBuildTarget;
        bool simulateMobile = activeTarget == BuildTarget.Android
                              || activeTarget == BuildTarget.iOS;
        isMobile = simulateMobile;
#else
            // On real device/runtime, use Unity's built-in check
            isMobile = Application.isMobilePlatform;
#endif
        ConfigureStartMenu();
        moveRelevantUIElements();

    }

    private void Start()
    {
        ConfigureUI();
    }

    /// Enable or disable phone UI based on the detected platform.
    private void ConfigureUI()
    {
        if (movementJoystick == null || dashButton == null || aimingJoystick == null) return;

        if (isMobile)
        {
            // Show all phone UI elements
            movementJoystick.SetActive(true);
            dashButton.SetActive(true);
            aimingJoystick.SetActive(true);
            Debug.Log("Mobile platform detected");
        }
        else
        {
            movementJoystick.SetActive(false);
            dashButton.SetActive(false);
            aimingJoystick.SetActive(false);
        }
    }

    private void moveRelevantUIElements()
    {
        Vector2 newWeaponIconPos = weaponIconTransform.anchoredPosition;
        newWeaponIconPos.x = -1022f;
        weaponIconTransform.anchoredPosition = newWeaponIconPos;
    }

    private void ConfigureStartMenu()
    {
        if (isMobile) { 
            Destroy(settingsButton);
            Destroy(quitButton);

            Vector2 newStartMenuButtonsPos = startMenuButtons.anchoredPosition;
            newStartMenuButtonsPos.y = startMenuButtonsOffsetY;
            startMenuButtons.anchoredPosition = newStartMenuButtonsPos;
        }
    }
}
