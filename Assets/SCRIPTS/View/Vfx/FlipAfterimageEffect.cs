using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// White, semi-transparent ghost trail spawned during the acrobatic flip.
/// Similar to <see cref="AfterimageEffect"/> but with a wider spawn interval
/// and longer lifetime so the images are more spread out.
/// </summary>
public class FlipAfterimageEffect : MonoBehaviour
{
    [Header("Flip Afterimage Settings")]
    public Material afterimageMaterial;
    public Color afterimageColor = new Color(1f, 1f, 1f, 1f);
    public float lifetime = 0.8f;
    public float spawnInterval = 0.08f;
    public float startAlpha = 0.4f;

    [Header("Optimization")]
    public int maxAfterimages = 20;

    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    private float spawnTimer = 0f;
    private List<AfterimageInstance> activeAfterimages = new List<AfterimageInstance>();

    void OnEnable()
    {
        skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        if (afterimageMaterial == null)
        {
            afterimageMaterial = MasterSingleton.Instance.PrefabBankManager.GhostTrailsMat;
        }

        spawnTimer = spawnInterval;
    }

    void Update()
    {
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            SpawnAfterimage();
            spawnTimer = 0f;
        }

        UpdateAfterimages();
    }

    void SpawnAfterimage()
    {
        if (activeAfterimages.Count >= maxAfterimages) return;

        foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
        {
            if (smr == null) continue;

            GameObject afterimageObj = new GameObject("FlipAfterimage");
            afterimageObj.transform.position = smr.transform.position;
            afterimageObj.transform.rotation = smr.transform.rotation;
            afterimageObj.transform.localScale = smr.transform.lossyScale;

            Mesh bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);

            MeshFilter mf = afterimageObj.AddComponent<MeshFilter>();
            mf.mesh = bakedMesh;

            MeshRenderer mr = afterimageObj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Material matInstance = new Material(afterimageMaterial);
            matInstance.SetColor("_Color", afterimageColor);
            matInstance.SetFloat("_Alpha", startAlpha);

            if (smr.sharedMaterial != null && smr.sharedMaterial.HasProperty("_MainTex"))
            {
                matInstance.SetTexture("_MainTex", smr.sharedMaterial.mainTexture);
            }

            mr.material = matInstance;

            AfterimageInstance instance = new AfterimageInstance
            {
                gameObject = afterimageObj,
                mesh = bakedMesh,
                material = matInstance,
                renderer = mr,
                spawnTime = Time.time,
                startAlpha = startAlpha
            };

            activeAfterimages.Add(instance);
        }
    }

    void UpdateAfterimages()
    {
        for (int i = activeAfterimages.Count - 1; i >= 0; i--)
        {
            AfterimageInstance instance = activeAfterimages[i];
            float age = Time.time - instance.spawnTime;

            if (age >= lifetime)
            {
                if (instance.gameObject != null)
                    Destroy(instance.gameObject);
                if (instance.mesh != null)
                    Destroy(instance.mesh);
                if (instance.material != null)
                    Destroy(instance.material);

                activeAfterimages.RemoveAt(i);
            }
            else
            {
                float t = age / lifetime;
                float alpha = Mathf.Lerp(startAlpha, 0f, t);

                if (instance.material != null)
                {
                    instance.material.SetFloat("_Alpha", alpha);
                }
            }
        }
    }

    void OnDisable()
    {
        CleanupAfterimages();
    }

    void OnDestroy()
    {
        CleanupAfterimages();
    }

    void CleanupAfterimages()
    {
        foreach (var instance in activeAfterimages)
        {
            if (instance.gameObject != null) Destroy(instance.gameObject);
            if (instance.mesh != null) Destroy(instance.mesh);
            if (instance.material != null) Destroy(instance.material);
        }
        activeAfterimages.Clear();
    }

    private class AfterimageInstance
    {
        public GameObject gameObject;
        public Mesh mesh;
        public Material material;
        public MeshRenderer renderer;
        public float spawnTime;
        public float startAlpha;
    }
}
