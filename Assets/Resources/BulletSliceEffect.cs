using UnityEngine;

public class BulletSliceEffect : MonoBehaviour
{
    [Tooltip("How long it takes for the slice to fully dissolve (seconds).")]
    public float dissolveDuration = 0.5f;

    [Tooltip("Edge width for the dissolve (should roughly match the bullet thickness).")]
    public float edgeWidth = 0.05f;

    private float timer = 0f;
    private Material mat;
    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        // Assume the material is a unique instance (if not, call Instantiate on sr.material)
        mat = sr.material;
        // Set the edge width property on the material
        mat.SetFloat("_EdgeWidth", edgeWidth);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / dissolveDuration);
        mat.SetFloat("_DissolveProgress", progress);
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
