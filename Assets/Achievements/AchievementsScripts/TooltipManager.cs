using UnityEngine;
using TMPro;
using System.Collections;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup canvasGroup;   // on the TooltipPanel
    public RectTransform background;    // TooltipPanel's RectTransform
    public TextMeshProUGUI tooltipText;   // the child TMP

    [Header("Behavior Settings")]
    [Tooltip("How long it takes to fade in/out")]
    public float fadeDuration = 0.2f;
    [Tooltip("Offset from the mouse cursor")]
    public Vector2 screenOffset = new Vector2(12, -12);

    // internal state
    bool isTrackingMouse = false;
    Coroutine fadeRoutine;

    void Awake()
    {
        // singleton
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // start invisible but active
        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (isTrackingMouse)
        {
            // move background to follow the mouse
            Vector2 localPt;
            RectTransform parentRT = background.parent as RectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                (Vector2)Input.mousePosition + screenOffset,
                null, out localPt);

            background.anchoredPosition = localPt;
        }
    }

    /// <summary>
    /// Called by your hover script after delay.
    /// Immediately shows (fades in) and begins tracking.
    /// </summary>
    public void Show(string text, Vector2 screenPos)
    {
        tooltipText.text = text;

        // position immediately
        Vector2 localPt;
        RectTransform parentRT = background.parent as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT,
            screenPos + screenOffset,
            null, out localPt);

        background.anchoredPosition = localPt;

        // begin fade‑in
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(1f));

        isTrackingMouse = true;
    }

    /// <summary>
    /// Called by your hover script on pointer exit.
    /// Starts fade‑out but keeps tracking until invisible.
    /// </summary>
    public void Hide()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(0f));
        // do NOT set isTrackingMouse=false here—
        // we'll turn that off once fade completes
    }

    IEnumerator Fade(float targetAlpha)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // if we just faded out fully, stop tracking
        if (Mathf.Approximately(targetAlpha, 0f))
            isTrackingMouse = false;
    }
}
