using UnityEngine;

/// <summary>
/// Lightweight relay that sits on the entity GameObject.
/// Animation Events on draw-weapon clips call <see cref="OnWeaponReveal"/> by name,
/// and this component forwards to whichever <see cref="IWeaponHandler"/> is currently active.
/// </summary>
public class WeaponEventRelay : MonoBehaviour
{
    private IWeaponHandler _handler;

    public void Bind(IWeaponHandler handler) => _handler = handler;
    public void Unbind() => _handler = null;

    /// <summary>Called by a Unity Animation Event on the draw-weapon clip.</summary>
    public void OnWeaponReveal() => _handler?.OnWeaponReveal();
}
