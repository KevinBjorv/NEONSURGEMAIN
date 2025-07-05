using UnityEngine;

public class LockAudioListenerRotation : MonoBehaviour
{
    void LateUpdate()
    {
        // Reset rotation so that it's not affected by the parent's rotation.
        transform.rotation = Quaternion.identity;
    }
}
