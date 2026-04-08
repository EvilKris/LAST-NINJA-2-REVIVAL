using UnityEngine;

public class SwordFightingHandler : MonoBehaviour
{
    private CombatHandler _combat;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
    }
}
