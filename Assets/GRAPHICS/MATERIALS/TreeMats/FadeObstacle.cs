using System.Collections;
using UnityEngine;

public class FadeObstacle : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadedAlpha = 0.2f;
    public float fadeDuration = 0.25f;

    private Renderer _renderer;
    private Material _material;
    private float _originalAlpha;
    private Coroutine _fadeRoutine;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _material = _renderer.material; // instance material for this object

        Color c = _material.GetColor(BaseColorID);
        _originalAlpha = c.a;
    }

    public void FadeOut()
    {
        StartFade(_originalAlpha * fadedAlpha);
    }

    public void FadeIn()
    {
        StartFade(_originalAlpha);
    }

    private void StartFade(float targetAlpha)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeCoroutine(targetAlpha));
    }

    private IEnumerator FadeCoroutine(float targetAlpha)
    {
        Color c = _material.GetColor(BaseColorID);
        float startAlpha = c.a;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / fadeDuration;
            float a = Mathf.Lerp(startAlpha, targetAlpha, t);
            c.a = a;
            _material.SetColor(BaseColorID, c);
            yield return null;
        }

        c.a = targetAlpha;
        _material.SetColor(BaseColorID, c);
        _fadeRoutine = null;
    }
}
