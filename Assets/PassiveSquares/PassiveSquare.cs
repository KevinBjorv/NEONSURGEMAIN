// PassiveSquare.cs
using UnityEngine;

public class PassiveSquare : MonoBehaviour
{
    private int pointValue;
    private bool damagesPlayer;
    private int damageAmount;

    [HideInInspector]
    public SpawnManager spawnManager;

    private bool hasGrantedPoints = false;

    // --- NEW: Variables for MaterialPropertyBlock ---
    public Renderer CachedRenderer { get; private set; } // Public getter for cached renderer
    private MaterialPropertyBlock propBlock;
    // Cache Shader Property IDs for performance
    
    private static readonly int ColorID = Shader.PropertyToID("_Color"); // Main color property (Common names: _Color, _BaseColor) - CHECK YOUR SHADER
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor"); // Emission color property
                                                                                         // ---
    public Color BaseColor { get; private set; } // Public getter for base color

    public int PointValue => pointValue;

    // --- NEW: Initialize propBlock in Awake ---
    void Awake()
    {
        // Cache the Renderer
        CachedRenderer = GetComponent<Renderer>();
        if (CachedRenderer == null)
        {
            Debug.LogError("PassiveSquare missing Renderer component!", this);
        }
        // Create the block once
        propBlock = new MaterialPropertyBlock();
    }
    // ---

    // --- MODIFIED: Initialize method ---
    public void Initialize(SquareType squareType)
    {
        pointValue = squareType.pointValue;
        damagesPlayer = squareType.damagesPlayerOnContact;
        damageAmount = squareType.contactDamage;

        // --- Store base color ---
        BaseColor = squareType.color;
        // ---

        // Apply initial color using MaterialPropertyBlock
        if (CachedRenderer != null)
        {
            CachedRenderer.GetPropertyBlock(propBlock); // Start fresh or get defaults
            propBlock.SetColor(ColorID, BaseColor);          // Set initial main color
            propBlock.SetColor(EmissionColorID, BaseColor);  // Set initial emission color (adjust if needed)
            CachedRenderer.SetPropertyBlock(propBlock);
        }

        // Register with pulse manager
        if (EmissionPulseManager.Instance != null) // Check if instance exists
        {
            EmissionPulseManager.Instance.RegisterSquare(this);
        }
        hasGrantedPoints = false;
    }



    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasGrantedPoints) return;

        // Player contact
        if (other.CompareTag("Player"))
        {
            if (damagesPlayer && PlayerHealth.Instance != null)
                PlayerHealth.Instance.TakeDamage(damageAmount);

            GrantPointsAndDestroy();
        }
        // Player bullet
        else if (other.CompareTag("Bullet"))
        {
            GrantPointsAndDestroy();
        }
        // Enemy bullet (no points)
        else if (other.CompareTag("enemyBullet"))
        {
            hasGrantedPoints = true;
            DestroySquare();
        }
        // Rocket companion impact
        else if (other.GetComponent<RocketCompanion>() != null)
        {
            GrantPointsAndDestroy();
        }
    }

    public void ForceDestroyByRocket()
    {
        if (hasGrantedPoints) return;
        GrantPointsAndDestroy();
    }

    private void GrantPointsAndDestroy()
    {
        ScoreManager.Instance.AddPoints(pointValue);
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.ReportProgress("1", 1);
            // Removed debug log for cleaner output
        }

        hasGrantedPoints = true;
        DestroySquare();
    }

    private void DestroySquare()
    {
        if (spawnManager != null)
            spawnManager.DespawnEntity(gameObject);
        else
            Destroy(gameObject); // Fallback if pool manager isn't set
    }

    private void OnDisable()
    {
        if (EmissionPulseManager.Instance != null)
        {
            EmissionPulseManager.Instance.UnregisterSquare(this);
        }
    }
}