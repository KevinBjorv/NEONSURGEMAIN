using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections; // Required for IEnumerator

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    // Assign your PhoneUIManager instance here in the Inspector.
    // If your PhoneUIManager is on a GameObject named "PhoneUIManager_Instance", for example.
    public PhoneUIManager phoneUIManager;

    [Header("Hover/Press Settings")]
    public float interactionScale = 1.1f; // Scale when hovered (desktop) or pressed (mobile)
    public float transitionDuration = 0.2f; // Duration for the transition

    private Vector3 originalScale;
    private bool isHoveringDesktop = false;
    private bool isPressedMobile = false;
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        originalScale = transform.localScale;

        // Attempt to find PhoneUIManager if not assigned in the Inspector.
        // This is a fallback; direct assignment in the Inspector is generally more reliable.
        if (phoneUIManager == null)
        {
            phoneUIManager = FindObjectOfType<PhoneUIManager>();
            if (phoneUIManager == null)
            {
                Debug.LogWarning("PhoneUIManager instance not found or assigned on " + gameObject.name +
                                 ". Platform defines will be used for mobile check as a fallback.");
            }
        }
    }

    private bool IsConsideredMobile()
    {
        if (phoneUIManager != null)
        {
            return phoneUIManager.isMobile;
        }

        // Fallback logic if PhoneUIManager instance is not available
        // This ensures the script still tries to behave reasonably.
#if UNITY_EDITOR
        // In Editor, if PhoneUIManager isn't found, assume desktop for hover effects unless build target suggests otherwise.
        // This part of the fallback might be redundant if your PhoneUIManager.Awake() always runs and is found.
        var activeTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
        return activeTarget == UnityEditor.BuildTarget.Android || activeTarget == UnityEditor.BuildTarget.iOS;
#elif UNITY_ANDROID || UNITY_IOS
            // On these specific mobile platforms, definitely true.
            return true;
#else
            // For other platforms, rely on Unity's general mobile platform check.
            return Application.isMobilePlatform;
#endif
    }

    // --- Desktop Hover Logic ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsConsideredMobile())
        {
            isHoveringDesktop = true;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleToTargetCoroutine(interactionScale));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsConsideredMobile())
        {
            isHoveringDesktop = false;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleToTargetCoroutine(1f)); // Scale back to original
        }
        else // For mobile, if finger drags off while pressed
        {
            if (isPressedMobile)
            {
                isPressedMobile = false; // Reset pressed state
                if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
                scaleCoroutine = StartCoroutine(ScaleToTargetCoroutine(1f)); // Scale back to original
            }
        }
    }

    // --- Mobile Press Logic ---
    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsConsideredMobile())
        {
            isPressedMobile = true;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleToTargetCoroutine(interactionScale));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (IsConsideredMobile())
        {
            if (isPressedMobile) // Only trigger if it was actually pressed down on this button
            {
                isPressedMobile = false;
                if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
                scaleCoroutine = StartCoroutine(ScaleToTargetCoroutine(1f)); // Scale back to original
            }
        }
    }

    private IEnumerator ScaleToTargetCoroutine(float targetScaleFactor)
    {
        float elapsedTime = 0f;
        Vector3 targetScaleVec = originalScale * targetScaleFactor;
        Vector3 currentScale = transform.localScale;

        while (elapsedTime < transitionDuration)
        {
            // Use unscaledDeltaTime for UI animations that should not pause with game time.
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);

            transform.localScale = Vector3.Lerp(currentScale, targetScaleVec, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.localScale = targetScaleVec; // Ensure it's exactly at the target scale
        scaleCoroutine = null; // Mark coroutine as finished
    }

    // Optional: Reset scale if the object is disabled during the effect.
    private void OnDisable()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
        // Only reset if the scale is not already original, to avoid unnecessary changes
        if (transform.localScale != originalScale)
        {
            transform.localScale = originalScale;
        }
        isHoveringDesktop = false;
        isPressedMobile = false;
    }
}