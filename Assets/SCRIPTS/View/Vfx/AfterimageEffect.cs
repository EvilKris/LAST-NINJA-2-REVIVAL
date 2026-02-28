using UnityEngine;
using System.Collections.Generic;

public class AfterimageEffect : MonoBehaviour
{
    [Header("Afterimage Settings")]
    public Material afterimageMaterial;
    public Color afterimageColor = new Color(0f, 0.5f, 1f, 1f); // Blue
    public float lifetime = 0.5f;
    public float spawnInterval = 0.05f;
    public float startAlpha = 0.6f;

    [Header("Optimization")]
    public int maxAfterimages = 15;

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
        else
        {
            Debug.LogError("Afterimage shader not found! Make sure AfterimageShader.shader exists in your project.");
        }
        

        spawnTimer = spawnInterval;

        JSAM.AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.Tier_One_Drive_Strike);
    }

    void Update()
    {
        // Spawn new afterimages
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            SpawnAfterimage();
            spawnTimer = 0f;
        }

        // Update and fade all afterimages
        UpdateAfterimages();
    }

    void SpawnAfterimage()
    {
        if (activeAfterimages.Count >= maxAfterimages) return;

        foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
        {
            if (smr == null) continue;

            GameObject afterimageObj = new GameObject("Afterimage");
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

            // Create a NEW material instance for THIS specific afterimage
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
        // Iterate backwards so we can safely remove items
        for (int i = activeAfterimages.Count - 1; i >= 0; i--)
        {
            AfterimageInstance instance = activeAfterimages[i];
            float age = Time.time - instance.spawnTime;

            if (age >= lifetime)
            {
                // Destroy this afterimage - it's expired
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
                // Fade this afterimage based on its age
                float t = age / lifetime; // 0 to 1
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