
#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// DEBUG ONLY — swaps the character's skinned mesh materials while a combo window is open,
/// then restores them when the window closes or the move ends.
/// Attach this to the same GameObject as CombatHandler.
/// Uses PhantomMaterial from PrefabBankManager as the "window open" indicator.
/// Remove or strip this component before shipping.
/// </summary>
[RequireComponent(typeof(CombatHandler))]
public class CombatComboWindowDebugger : MonoBehaviour
{
    [Tooltip("Override the flash material. Leave empty to use PrefabBankManager.PhantomMaterial.")]
    public Material overrideMaterial;

    private CombatHandler _combat;
    private Animator _animator;
    private SkinnedMeshRenderer[] _renderers;
    private Material[][] _originalMaterials;
    private bool _windowWasOpen;
    private readonly int _hashIsAction = Animator.StringToHash("isAction");

    private void Awake()
    {
        _combat = GetComponent<CombatHandler>();
        _animator = GetComponent<Animator>();

        _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        CacheOriginalMaterials();
    }

    private void CacheOriginalMaterials()
    {
        _originalMaterials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _originalMaterials[i] = _renderers[i].sharedMaterials;
    }

    private void Update()
    {
        // Only visualize when an attack is playing
        if (!_combat.IsAttacking)
        {
            if (_windowWasOpen)
                RestoreMaterials();
            return;
        }

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("ReplaceableAttack"))
        {
            if (_windowWasOpen)
                RestoreMaterials();
            return;
        }

        bool windowIsOpen = !_animator.GetBool(_hashIsAction) && _combat.IsAttacking;

        if (windowIsOpen && !_windowWasOpen)
        {
            ApplyDebugMaterial();
            _windowWasOpen = true;
        }
        else if (!windowIsOpen && _windowWasOpen)
        {
            RestoreMaterials();
        }
    }

    private Material GetFlashMaterial()
    {
        if (overrideMaterial != null)
            return overrideMaterial;

        if (MasterSingleton.Instance != null && MasterSingleton.Instance.PrefabBankManager != null)
            return MasterSingleton.Instance.PrefabBankManager.PhantomMaterial;

        return null;
    }

    private void ApplyDebugMaterial()
    {
        Material flash = GetFlashMaterial();
        if (flash == null)
        {
            Debug.LogWarning("[CombatComboWindowDebugger] No flash material found. Assign one in the Inspector or set PhantomMaterial in PrefabBankManager.");
            return;
        }

        // Re-cache in case the character's materials changed (e.g. weapon swap)
        CacheOriginalMaterials();

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            int slotCount = _renderers[i].sharedMaterials.Length;
            Material[] swapped = new Material[slotCount];
            for (int s = 0; s < slotCount; s++)
                swapped[s] = flash;
            _renderers[i].materials = swapped;
        }

        Debug.Log($"[CombatComboWindowDebugger] Combo window OPEN on {gameObject.name}");
    }

    private void RestoreMaterials()
    {
        _windowWasOpen = false;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].materials = _originalMaterials[i];
        }

        Debug.Log($"[CombatComboWindowDebugger] Combo window CLOSED on {gameObject.name}");
    }

    private void OnDisable()
    {
        if (_windowWasOpen)
            RestoreMaterials();
    }
}
#endif
