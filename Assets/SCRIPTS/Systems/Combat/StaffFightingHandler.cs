using UnityEngine;

public class StaffFightingHandler : MonoBehaviour, IWeaponHandler
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
