using UnityEngine;

public class StaffFightingHandler : MonoBehaviour
{
    private CombatHandler _combat;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
    }
}
