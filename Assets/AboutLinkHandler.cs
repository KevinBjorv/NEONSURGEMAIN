using UnityEngine;
using UnityEngine.UI; // Required for Button potentially, and GUIUtility
using TMPro; // Optional: If you want to display a "Copied!" message

public class AboutLinksHandler : MonoBehaviour
{
    [Header("Link URLs")]
    [Tooltip("Full URL for your Itch.io page (e.g., https://yourname.itch.io/yourgame)")]
    public string itchioURL = "https://yourname.itch.io/yourgame"; // Replace with your actual URL

    [Tooltip("Full URL for your YouTube channel/video (e.g., https://www.youtube.com/yourchannel)")]
    public string youtubeURL = "youtube.com/your-channel"; // Replace with your actual URL

    [Tooltip("Full URL for your bug report form (e.g., Google Form, Trello board link)")]
    public string bugReportURL = "https://your-bug-report-link.com"; // Replace with your actual URL

    [Tooltip("Full URL for your Privacy Policy page")]
    public string privacyPolicyURL = "https://your-privacy-policy-link.com"; // Replace with your actual URL

    [Header("Email")]
    [Tooltip("The email address for feedback/enquiries")]
    public string contactEmail = "your.email@example.com"; // Replace with your actual email

    [Header("Optional: Feedback UI")]
    [Tooltip("(Optional) TextMeshPro element to show 'Email Copied!' message")]
    public TextMeshProUGUI copyFeedbackText;
    [Tooltip("How long the 'Copied!' message stays visible (in seconds)")]
    public float copyFeedbackDuration = 1.5f;

    private Coroutine _feedbackCoroutine = null;

    /// <summary>
    /// Opens the Itch.io URL in the default web browser.
    /// Called by the Itch.io Button's OnClick event.
    /// </summary>
    public void OpenItchioLink()
    {
        if (!string.IsNullOrEmpty(itchioURL))
        {
            Application.OpenURL(itchioURL);
            Debug.Log($"Opening URL: {itchioURL}");
        }
        else
        {
            Debug.LogWarning("Itch.io URL is not set in the AboutLinksHandler.");
        }
    }

    /// <summary>
    /// Opens the YouTube URL in the default web browser.
    /// Called by the YouTube Button's OnClick event.
    /// </summary>
    public void OpenYoutubeLink()
    {
        if (!string.IsNullOrEmpty(youtubeURL))
        {
            Application.OpenURL(youtubeURL);
            Debug.Log($"Opening URL: {youtubeURL}");
        }
        else
        {
            Debug.LogWarning("YouTube URL is not set in the AboutLinksHandler.");
        }
    }

    /// <summary>
    /// Opens the Bug Report URL in the default web browser.
    /// Called by the Bug Report Button's OnClick event.
    /// </summary>
    public void OpenBugReportLink()
    {
        if (!string.IsNullOrEmpty(bugReportURL))
        {
            Application.OpenURL(bugReportURL);
            Debug.Log($"Opening URL: {bugReportURL}");
        }
        else
        {
            Debug.LogWarning("Bug Report URL is not set in the AboutLinksHandler.");
        }
    }

    /// <summary>
    /// Opens the Privacy Policy URL in the default web browser.
    /// Called by the Privacy Policy Button's OnClick event.
    /// </summary>
    public void OpenPrivacyPolicyLink()
    {
        if (!string.IsNullOrEmpty(privacyPolicyURL))
        {
            Application.OpenURL(privacyPolicyURL);
            Debug.Log($"Opening URL: {privacyPolicyURL}");
        }
        else
        {
            Debug.LogWarning("Privacy Policy URL is not set in the AboutLinksHandler.");
        }
    }

    /// <summary>
    /// Copies the contact email address to the system clipboard.
    /// Called by the Email Button's OnClick event.
    /// </summary>
    public void HandleEmailClick()
    {
        if (!string.IsNullOrEmpty(contactEmail))
        {
            GUIUtility.systemCopyBuffer = contactEmail;
            Debug.Log($"Copied to clipboard: {contactEmail}");

            // Optional: Show feedback message
            ShowCopyFeedback();
        }
        else
        {
            Debug.LogWarning("Contact Email is not set in the AboutLinksHandler.");
        }
    }

    /// <summary>
    /// Shows a temporary feedback message (if configured).
    /// </summary>
    private void ShowCopyFeedback()
    {
        if (copyFeedbackText != null)
        {
            // Stop any previous feedback fadeout
            if (_feedbackCoroutine != null)
            {
                StopCoroutine(_feedbackCoroutine);
            }
            _feedbackCoroutine = StartCoroutine(DisplayFeedbackMessage());
        }
    }

    /// <summary>
    /// Coroutine to display the feedback message and then hide it.
    /// </summary>
    private System.Collections.IEnumerator DisplayFeedbackMessage()
    {
        if (copyFeedbackText != null)
        {
            copyFeedbackText.text = "Email Copied!";
            copyFeedbackText.gameObject.SetActive(true);

            yield return new WaitForSeconds(copyFeedbackDuration);

            copyFeedbackText.gameObject.SetActive(false);
            _feedbackCoroutine = null; // Reset coroutine tracker
        }
    }

    // Optional: Ensure feedback text is hidden on start
    private void Start()
    {
        if (copyFeedbackText != null)
        {
            copyFeedbackText.gameObject.SetActive(false);
        }
    }
}
