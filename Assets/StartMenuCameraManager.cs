using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class StartMenuCameraManager : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCamera;
    public PhoneUIManager phoneUIManager;

    public float lensSizeMobile = 23f;

    private void Start()
    {
        if (phoneUIManager.isMobile) {
            virtualCamera.m_Lens.OrthographicSize = lensSizeMobile; // Set start menu camera lens size when game is launched on mobile
        }
    }
}
