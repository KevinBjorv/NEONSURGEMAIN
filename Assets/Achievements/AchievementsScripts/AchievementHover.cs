using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class AchievementHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Delay before showing tooltip")]
    public float hoverDelay = 2f;

    string _description;
    Coroutine _hoverRoutine;

    /// <summary>
    /// Called by the UI Init above.
    /// </summary>
    public void SetDescription(string desc)
    {
        _description = desc;
    }

    public void OnPointerEnter(PointerEventData _)
    {
        _hoverRoutine = StartCoroutine(HoverRoutine());
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (_hoverRoutine != null) StopCoroutine(_hoverRoutine);
        TooltipManager.Instance.Hide();
    }

    IEnumerator HoverRoutine()
    {
        yield return new WaitForSecondsRealtime(hoverDelay);
        TooltipManager.Instance.Show(_description, Input.mousePosition);
    }
}
