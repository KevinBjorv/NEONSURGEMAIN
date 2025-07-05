using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Reflection;

public class DeathScreenManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject deathPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI timeSurvivedText;
    public Button mainMenuButton;

    public GameObject playerRoot;

    public bool enableDeath = true;

    private void Awake()
    {
        deathPanel.SetActive(false);
        mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    private void Start() {
        if (PlayerHealth.Instance != null && enableDeath)
            PlayerHealth.Instance.OnDeath.AddListener(ShowDeathScreen);
    }

    private void ShowDeathScreen()
    {
        // stop gameplay
        Time.timeScale = 0f;
        DisableAllCustomScripts();
        UIManager.Instance.gameObject.SetActive(false);

        // populate data
        int score = ScoreManager.Instance?.GetScore() ?? 0;
        float time = UIManager.Instance?.GetCurrentTime() ?? 0f;

        finalScoreText.text = $"Score: {score}";
        timeSurvivedText.text = $"Time: {time:0.00}s";

        // show panel
        deathPanel.SetActive(true);
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    private void DisableAllCustomScripts()
    {
        if (playerRoot == null) return;

        // Get the Assembly where your own scripts live (usually Assembly-CSharp)
        Assembly gameAsm = typeof(PlayerMovement).Assembly;

        // Find every MonoBehaviour under playerRoot
        var allBehaviours = playerRoot.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;               // safety
            var asm = mb.GetType().Assembly;

            // If this component’s type comes from *your* game assembly, disable it
            if (asm == gameAsm)
            {
                mb.enabled = false;
            }
        }
    }
}
