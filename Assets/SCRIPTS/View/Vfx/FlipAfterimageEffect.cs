using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns a white, semi-transparent ghost trail during the acrobatic flip.
/// Similar to <see cref="AfterimageEffect"/> but tuned for a wider spread:
/// larger spawn interval and longer lifetime so silhouettes linger further apart.
/// No sound is played on activation (unlike the charged-attack afterimage).
/// </summary>
public class FlipAfterimageEffect : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Tuning
    // -------------------------------------------------------------------------
    [Header("Flip Afterimage Settings")]
    [Tooltip("Tint applied to each ghost silhouette (RGB). Alpha channel is unused — see startAlpha.")]
    public Color afterimageColor = new(0.3f, 0.3f, 0.3f, 0f);

    [Tooltip("Seconds before a ghost silhouette fully fades out and is destroyed.")]
    public float lifetime = 0.1f;

    [Tooltip("Seconds between consecutive ghost spawns. Larger = more spread out.")]
    public float spawnInterval = 0.05f;

    [Tooltip("Initial opacity of each ghost (0 = invisible, 1 = fully opaque).")]
    public float startAlpha = 0.5f;

    [Header("Optimization")]
    [Tooltip("Hard cap on simultaneous ghost meshes to bound memory/draw calls.")]
    public int maxAfterimages = 5;

    // -------------------------------------------------------------------------
    // Cached shader property IDs (avoids per-frame string hashing)
    // -------------------------------------------------------------------------
    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    /// <summary>Template material cloned once on enable; each ghost gets its own copy so alpha fades independently.</summary>
    private Material _baseMaterial;

    /// <summary>All skinned renderers on the character, cached on enable.</summary>
    private SkinnedMeshRenderer[] _skinnedRenderers;

    /// <summary>Accumulator for spawn timing.</summary>
    private float _spawnTimer;

    /// <summary>Cached <c>1 / lifetime</c> to replace per-frame division with multiplication.</summary>
    private float _lifetimeInv;

    /// <summary>Active ghost instances. Pre-allocated to <see cref="maxAfterimages"/> capacity.</summary>
    private readonly List<AfterimageInstance> _activeAfterimages = new List<AfterimageInstance>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        // Clone the shared ghost-trails material and stamp it white.
        // The GhostTrailsShader defaults _MainTex to a built-in white texture,
        // so we intentionally skip setting _MainTex — the silhouette will be a
        // solid white shape tinted by _Color, which is exactly what we want.
        _baseMaterial = new Material(MasterSingleton.Instance.PrefabBankManager.GhostTrailsMat);
        _baseMaterial.SetColor(PropColor, afterimageColor);
        _baseMaterial.SetFloat(PropAlpha, startAlpha);

        _lifetimeInv = 1f / lifetime;
        _spawnTimer = spawnInterval; // Spawn the first ghost immediately
        _activeAfterimages.Capacity = Mathf.Max(_activeAfterimages.Capacity, maxAfterimages);
    }

    private void Update()
    {
        _spawnTimer += Time.deltaTime;

        if (_spawnTimer >= spawnInterval)
        {
            SpawnAfterimage();
            _spawnTimer = 0f;
        }

        FadeAndCullAfterimages();
    }

    private void OnDisable()
    {
        CleanupAll();
    }

    private void OnDestroy()
    {
        CleanupAll();
    }

    // -------------------------------------------------------------------------
    // Spawn
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bakes the current pose of every <see cref="SkinnedMeshRenderer"/> into a
    /// static mesh, wraps it in a new GameObject with the white ghost material,
    /// and registers it for fade-out tracking.
    /// </summary>
    private void SpawnAfterimage()
    {
        if (_activeAfterimages.Count >= maxAfterimages) return;

        float now = Time.time;

        for (int r = 0; r < _skinnedRenderers.Length; r++)
        {
            SkinnedMeshRenderer smr = _skinnedRenderers[r];
            if (smr == null) continue;

            // --- GameObject & transform ---
            GameObject ghost = new GameObject("FlipAfterimage");
            Transform ghostT = ghost.transform;
            Transform smrT = smr.transform;
            ghostT.position = smrT.position;
            ghostT.rotation = smrT.rotation;
            ghostT.localScale = smrT.lossyScale;

            // --- Baked mesh snapshot ---
            Mesh bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);

            ghost.AddComponent<MeshFilter>().mesh = bakedMesh;

            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Each ghost needs its own material instance so alpha fades independently
            Material matInstance = new Material(_baseMaterial);
            mr.material = matInstance;

            _activeAfterimages.Add(new AfterimageInstance
            {
                gameObject = ghost,
                mesh = bakedMesh,
                material = matInstance,
                spawnTime = now
            });
        }
    }

    // -------------------------------------------------------------------------
    // Fade & cull
    // -------------------------------------------------------------------------

    /// <summary>
    /// Iterates all active ghosts in reverse, fading their alpha linearly toward
    /// zero. Expired ghosts are destroyed and removed from the list.
    /// </summary>
    private void FadeAndCullAfterimages()
    {
        float now = Time.time;

        for (int i = _activeAfterimages.Count - 1; i >= 0; i--)
        {
            AfterimageInstance inst = _activeAfterimages[i];
            float age = now - inst.spawnTime;

            if (age >= lifetime)
            {
                // Expired — destroy Unity objects and remove from tracking
                if (inst.gameObject != null) Destroy(inst.gameObject);
                if (inst.mesh != null) Destroy(inst.mesh);
                if (inst.material != null) Destroy(inst.material);

                _activeAfterimages.RemoveAt(i);
            }
            else if (inst.material != null)
            {
                // Fade: lerp alpha from startAlpha → 0 over the lifetime
                float alpha = startAlpha * (1f - age * _lifetimeInv);
                inst.material.SetFloat(PropAlpha, alpha);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Cleanup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Destroys every active ghost and the shared base material.
    /// Called on disable and destroy to prevent leaked meshes/materials.
    /// </summary>
    private void CleanupAll()
    {
        for (int i = 0; i < _activeAfterimages.Count; i++)
        {
            AfterimageInstance inst = _activeAfterimages[i];
            if (inst.gameObject != null) Destroy(inst.gameObject);
            if (inst.mesh != null) Destroy(inst.mesh);
            if (inst.material != null) Destroy(inst.material);
        }
        _activeAfterimages.Clear();

        if (_baseMaterial != null)
        {
            Destroy(_baseMaterial);
            _baseMaterial = null;
        }
    }

    // -------------------------------------------------------------------------
    // Per-ghost data (struct to avoid per-instance heap allocation / GC pressure)
    // -------------------------------------------------------------------------

    private struct AfterimageInstance
    {
        public GameObject gameObject;
        public Mesh mesh;
        public Material material;
        public float spawnTime;
    }
}
