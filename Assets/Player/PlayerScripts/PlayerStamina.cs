using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    public static PlayerStamina Instance { get; private set; }

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    private float currentStamina;

    public float runDrainPerSecond = 10f; // Stamina drained while running
    public float dashCost = 25f;          // Stamina used per dash
    public float regenPerSecond = 5f;     // Stamina regen when not running/dashing

    private bool isRunning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        currentStamina = maxStamina;
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        if (isRunning)
        {
            // Drain stamina if running
            currentStamina -= runDrainPerSecond * delta;
        }
        else
        {
            // Regen stamina if not running
            currentStamina += regenPerSecond * delta;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        // If stamina hits 0 while running, force stop running.
        if (currentStamina <= 0f && isRunning)
        {
            isRunning = false;

            // Inform PlayerMovement to stop running as if Shift was released.
            if (PlayerMovement.Instance != null)
            {
                PlayerMovement.Instance.ForceStopRunning();
            }
        }
    }

    public bool CanDash()
    {
        return currentStamina >= dashCost;
    }

    public bool CanRun()
    {
        return currentStamina > 0f;
    }

    public void Dash()
    {
        if (CanDash())
        {
            currentStamina -= dashCost;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }
    }

    public void SetRunning(bool running)
    {
        isRunning = running;
    }

    public float GetStaminaPercent()
    {
        return currentStamina / maxStamina;
    }

    public void ResetStamina()
    {
        currentStamina = maxStamina;
    }
}
