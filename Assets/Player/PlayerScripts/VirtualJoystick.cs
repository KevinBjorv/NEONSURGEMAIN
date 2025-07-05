// VirtualJoystick.cs
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to a UI Image that represents the joystick background.
/// The first child (index 0) should be the Joystick "Handle" image.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform bgRect;       // The background image's RectTransform
    private RectTransform handleRect;   // The handle (thumb) image's RectTransform
    private Vector2 inputVector;        // Joystick output in range [-1..1] for both X & Y

    // --- NEW ---
    /// <summary>
    /// Gets the pointerId (fingerId on touch devices) currently interacting with this joystick.
    /// Returns -1 if no finger is actively controlling it.
    /// </summary>
    public int ControllingPointerId { get; private set; } = -1;
    // --- END NEW ---

    // Public getters for horizontal and vertical
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;

    private void Awake()
    {
        // The background is this object's RectTransform
        bgRect = GetComponent<RectTransform>();
        // The handle is assumed to be the first child
        handleRect = transform.GetChild(0).GetComponent<RectTransform>();
        ControllingPointerId = -1; // Ensure reset on awake/enable
    }

    private void OnEnable()
    {
        // Reset state if re-enabled
        ControllingPointerId = -1;
        inputVector = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // --- Store the controlling pointer ID ---
        ControllingPointerId = eventData.pointerId;
        // ---

        // When finger touches, call OnDrag so we can update position immediately
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // --- Ensure the drag event matches the controlling finger ---
        if (eventData.pointerId != ControllingPointerId)
        {
            // Ignore drag events from other fingers touching the joystick area
            return;
        }
        // ---

        Vector2 pos;
        // Convert screen position to local position inside the bgRect
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgRect, eventData.position, eventData.pressEventCamera, out pos))
        {
            // Normalize 0..1, then shift to -1..1
            pos.x = (pos.x / bgRect.sizeDelta.x) * 2f;
            pos.y = (pos.y / bgRect.sizeDelta.y) * 2f;

            inputVector = new Vector2(pos.x, pos.y);
            if (inputVector.magnitude > 1f)
                inputVector = inputVector.normalized;

            // Move the handle to match the inputVector
            handleRect.anchoredPosition = new Vector2(
                inputVector.x * (bgRect.sizeDelta.x / 2f),
                inputVector.y * (bgRect.sizeDelta.y / 2f)
            );
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // --- Only reset if the controlling finger is lifted ---
        if (eventData.pointerId == ControllingPointerId)
        {
            // Reset input when the controlling finger lifts
            inputVector = Vector2.zero;
            handleRect.anchoredPosition = Vector2.zero;
            ControllingPointerId = -1; // Reset the controlling pointer ID
        }
        // ---
        // If a *different* finger lifts off the joystick area, don't reset the input
    }

    // --- Handle cases where the joystick might be disabled mid-interaction ---
    private void OnDisable()
    {
        // If the controlling pointer ID is active when disabled, reset everything
        if (ControllingPointerId != -1)
        {
            inputVector = Vector2.zero;
            handleRect.anchoredPosition = Vector2.zero;
            ControllingPointerId = -1;
        }
    }
    // ---
}