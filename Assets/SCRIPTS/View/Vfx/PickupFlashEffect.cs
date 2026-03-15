using UnityEngine;

/// <summary>
/// Attach to any <see cref="WorldItem"/> prefab to make it periodically flash solid white,
/// making small pickups easy to spot in the world.
///
/// The component is self-contained: it creates its own white URP Unlit material instance
/// at runtime, caches the originals from every child Renderer on Awake, and swaps
/// between them on a configurable interval/duration cycle.
/// </summary>
public class PickupFlashEffect : MonoBehaviour
{
    [Tooltip("Seconds between the start of each flash.")]
    [SerializeField] private float flashInterval = 1.2f;

    [Tooltip("How long the white flash lasts each cycle.")]
    [SerializeField] private float flashDuration = 0.1f;

    // Cached renderer data so we never call GetComponentsInChildren in Update
    private struct RendererEntry
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] flashMaterials;
    }

    private RendererEntry[] _entries;
    private float _timer;
    private bool _isFlashing;

    private void Awake()
    {
        Material whiteMat = CreateWhiteMaterial();
        CacheRenderers(whiteMat);
    }

    private void OnDestroy()
    {
        // Clean up the per-renderer flash material instances we own
        if (_entries == null) return;
        foreach (var entry in _entries)
        {
            if (entry.flashMaterials == null) continue;
            foreach (var mat in entry.flashMaterials)
            {
                if (mat != null) Destroy(mat);
            }
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (!_isFlashing && _timer >= flashInterval)
        {
            ApplyFlash();
            _isFlashing = true;
            _timer = 0f;
        }
        else if (_isFlashing && _timer >= flashDuration)
        {
            RestoreOriginals();
            _isFlashing = false;
            _timer = 0f;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void CacheRenderers(Material whiteMat)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        _entries = new RendererEntry[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Material[] originals = r.sharedMaterials;

            // One white material instance per slot so restoring is a simple array swap
            Material[] flash = new Material[originals.Length];
            for (int s = 0; s < originals.Length; s++)
                flash[s] = whiteMat;

            _entries[i] = new RendererEntry
            {
                renderer = r,
                originalMaterials = originals,
                flashMaterials = flash
            };
        }
    }

    private void ApplyFlash()
    {
        foreach (var entry in _entries)
        {
            if (entry.renderer != null)
                entry.renderer.materials = entry.flashMaterials;
        }
    }

    private void RestoreOriginals()
    {
        foreach (var entry in _entries)
        {
            if (entry.renderer != null)
                entry.renderer.sharedMaterials = entry.originalMaterials;
        }
    }

    /// <summary>
    /// Creates a single shared white URP Unlit material instance.
    /// All flash slots point to the same instance — they only need to be white,
    /// so sharing is safe and avoids unnecessary allocations.
    /// </summary>
    private static Material CreateWhiteMaterial()
    {
        // URP built-in unlit shader — always present in a URP project
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
        {
            // Fallback for any edge case (shouldn't happen in a URP project)
            unlit = Shader.Find("Unlit/Color");
        }

        var mat = new Material(unlit);
        mat.name = "PickupFlash_White";

        // Set base colour to solid white regardless of which property name the shader uses
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", Color.white);

        return mat;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        flashInterval = Mathf.Max(0.1f, flashInterval);
        flashDuration = Mathf.Clamp(flashDuration, 0.02f, flashInterval - 0.02f);
    }
#endif
}
