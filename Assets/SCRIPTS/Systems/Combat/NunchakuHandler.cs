using UnityEngine;

public class NunchakuHandler : MonoBehaviour
{
    private CombatHandler _combat;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
    }
}
