using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EnemyHealthBarScript : MonoBehaviour
{
    [SerializeField] private Image enemyHealthUIImage;
    [SerializeField] private Image enemyHealthUIImageLerp;

    // Duration in seconds for the lerp bar to reach its target fill amount
    [SerializeField] private float lerpDuration = 0.5f;

    // Called with a 0-1 normalizedHealth value whenever the enemy takes damage or heals
    public void UpdateHealthBar(float normalizedHealth)
    {
        // Snap the main bar to the new value immediately
        if (enemyHealthUIImage != null)
            enemyHealthUIImage.fillAmount = normalizedHealth;

        // Tween the lerp bar to the new value at a slower rate
        if (enemyHealthUIImageLerp != null)
        {
            enemyHealthUIImageLerp.DOKill();
            enemyHealthUIImageLerp.DOFillAmount(normalizedHealth, lerpDuration);
        }
    }
}
