using UnityEngine;

public class NunchakuHandler : MonoBehaviour, IWeaponHandler
{
    private CombatHandler _combat;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
    }

    public void OnWeaponReveal()
    {
    }
}
