using UnityEngine;

public class ThrownWeaponHandler : MonoBehaviour
{
    private CombatHandler _combat;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
    }
}
