using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ChargeRadialFlasher : MonoBehaviour
{
    [Header("References")]
    public Image radialImage; // The UI Image component with Radial 360 fill

    [Header("Flash Settings")]
    public Color flashColor1 = Color.white;
    public Color flashColor2 = Color.yellow;
    public float flashSpeed = 2f; // How fast it flashes

    private Color originalColor;
    private bool isFlashing = false;
    private Coroutine flashCoroutine;

    void Start()
    {
        if (radialImage == null)
        {
            radialImage = GetComponent<Image>();
        }

        if (radialImage != null)
        {
            originalColor = radialImage.color;
        }
    }

    void Update()
    {
        if (radialImage == null) return;

        // Check if fill amount is at 100%
        if (radialImage.fillAmount >= 1f)
        {
            if (!isFlashing)
            {
               
                StartFlashing();
            }
        }
        else
        {
            if (isFlashing)
            {
                StopFlashing();
            }
        }
    }

    void StartFlashing()
    {
        isFlashing = true;
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }

    void StopFlashing()
    {
        isFlashing = false;
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        radialImage.color = originalColor;
    }

    IEnumerator FlashCoroutine()
    {
        while (isFlashing)
        {
            // Lerp between two colors
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            radialImage.color = Color.Lerp(flashColor1, flashColor2, t);
            yield return null;
        }
    }

    // Optional: Call this if you want to update the original color dynamically
    public void UpdateOriginalColor(Color newColor)
    {
        originalColor = newColor;
        if (!isFlashing)
        {
            radialImage.color = originalColor;
        }
    }
}