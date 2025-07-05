using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class RocketCompanion : MonoBehaviour
{
    /* ───────────  Inspector knobs  (unchanged) ─────────── */
    [Header("Passive flight")]
    [Range(0.01f, 1f)] public float followSmoothTime = 0.15f;
    [Range(0f, 0.5f)] public float hoverBobAmp = 0.15f;
    [Range(0.2f, 5f)] public float hoverBobFreq = 1.2f;

    [Header("Attack flight")]
    [Range(0.01f, 0.4f)] public float attackSmoothTime = 0.05f;
    [Range(0.05f, 1f)] public float hitDistance = 0.15f;

    [Header("Targeting & aggression")]
    public float detectionRadius = 6f;
    [Range(1, 10)] public int minTargetsToAttack = 1;
    [Range(1, 10)] public int maxTargetsPerBurst = 3;

    [Header("Cooldown & leash")]
    public float attackCooldownSeconds = 1.2f;
    public float leashDistance = 12f;
    /* ────────────────────────────────────────────────────── */

    enum Mode { Passive, Attacking, Cooldown }

    Rigidbody2D rb;
    RocketCompanionManager mgr;

    Mode mode = Mode.Passive;
    Vector2 velocityRef = Vector2.zero;
    Vector2 followSlot;
    Transform attackTarget;
    int burstCounter = 0;
    float cooldownUntil;
    float bobT;

    /* ───────────  Unity  ─────────── */
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        if (mgr == null) { enabled = false; return; }
        StartCoroutine(TargetScanRoutine());
    }

    void FixedUpdate()
    {
        if (!mgr.IsActive) return;          // freeze when manager disabled

        if (mode == Mode.Attacking) DoAttack();
        else DoPassive();
    }

    /* ───────────  Movement  ─────────── */
    void DoPassive()
    {
        bobT += Time.fixedDeltaTime * hoverBobFreq * Mathf.PI * 2f;
        float bob = Mathf.Sin(bobT) * hoverBobAmp;

        Vector2 goal = followSlot + Vector2.up * bob;
        Vector2 next = Vector2.SmoothDamp(rb.position, goal, ref velocityRef, followSmoothTime);
        rb.MovePosition(next);
    }

    void DoAttack()
    {
        if (attackTarget == null) { EnterPassive(); return; }

        Vector2 next = Vector2.SmoothDamp(rb.position, attackTarget.position,
                                          ref velocityRef, attackSmoothTime);
        rb.MovePosition(next);

        // leash check
        if (Vector2.Distance(rb.position, mgr.PlayerPos) > leashDistance)
        {
            EnterPassive(); return;
        }

        // manual proximity hit (backup for rare cases collider misses)
        if (Vector2.Distance(rb.position, attackTarget.position) < hitDistance)
            RegisterHit(attackTarget.gameObject);
    }

    /* ───────────  Target scanning  ─────────── */
    IEnumerator TargetScanRoutine()
    {
        WaitForSeconds wait = new(0.25f);

        while (true)
        {
            if (mode == Mode.Passive && Time.time >= cooldownUntil && mgr.IsActive)
            {
                Transform best = null;
                int count = 0;
                float bestDist = float.MaxValue;

                foreach (var col in Physics2D.OverlapCircleAll(transform.position, detectionRadius))
                {
                    if (!col.CompareTag(RocketCompanionManager.PassiveSquareTag)) continue;
                    count++;
                    float d = Vector2.Distance(col.transform.position, transform.position);
                    if (d < bestDist) { bestDist = d; best = col.transform; }
                }

                if (count >= minTargetsToAttack && best != null)
                    EnterAttack(best);
            }

            yield return wait;
        }
    }

    /* ───────────  Collisions  ─────────── */
    void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag(RocketCompanionManager.PassiveSquareTag)) return;

        // Always grant points & destroy on impact
        DestroySquareAndGrantPoints(col.gameObject);

        if (mode == Mode.Attacking)
            RegisterHit(col.gameObject);   // continue / finish burst logic
    }

    /* ───────────  Hit & burst processing  ─────────── */
    void RegisterHit(GameObject square)
    {
        // square already destroyed in OnTriggerEnter, but may still exist if
        // this call came from distance‑check; ensure points & destruction
        if (square.activeInHierarchy)
            DestroySquareAndGrantPoints(square);

        burstCounter++;

        if (burstCounter < maxTargetsPerBurst)
        {
            Transform next = FindClosestSquare();
            if (next != null) { attackTarget = next; return; }
        }

        EnterCooldown();
    }

    Transform FindClosestSquare()
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var col in Physics2D.OverlapCircleAll(transform.position, detectionRadius))
        {
            if (!col.CompareTag(RocketCompanionManager.PassiveSquareTag)) continue;
            float d = Vector2.Distance(col.transform.position, transform.position);
            if (d < bestDist) { bestDist = d; best = col.transform; }
        }
        return best;
    }

    /* ───────────  Destruction helper (grants points)  ─────────── */
    void DestroySquareAndGrantPoints(GameObject square)
    {
        if (SpawnManager.Instance != null)
        {
            if (square.CompareTag("NormalSquare"))
            {
                SpawnManager.Instance.DespawnEntity(square);            // points handled inside PassiveSquare/ScoreManager
            }
            else
            {
                // UniqueSquare – award points via dedicated call
                SpawnManager.Instance.DestroyUniqueSquare(square, 0);   // 0 prevents double‑score
            }
        }
        else
        {
            Destroy(square);
        }
    }

    /* ───────────  State helpers  ─────────── */
    void EnterAttack(Transform target)
    {
        mode = Mode.Attacking;
        attackTarget = target;
        burstCounter = 0;
        velocityRef = Vector2.zero;
    }

    void EnterPassive()
    {
        mode = Mode.Passive;
        attackTarget = null;
    }

    void EnterCooldown()
    {
        mode = Mode.Cooldown;
        cooldownUntil = Time.time + attackCooldownSeconds;
        attackTarget = null;
        Invoke(nameof(EndCooldown), attackCooldownSeconds);
    }

    void EndCooldown() => mode = Mode.Passive;

    /* ───────────  Manager hooks  ─────────── */
    public void Initialise(RocketCompanionManager manager) => mgr = manager;
    public void SetFollowSlot(Vector2 slot) => followSlot = slot;
}
