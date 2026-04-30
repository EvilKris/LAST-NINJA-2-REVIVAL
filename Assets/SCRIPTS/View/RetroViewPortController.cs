using UnityEngine;

public class RetroViewportController : MonoBehaviour
{
    private Camera _cam;
    private Rect _fullRect = new Rect(0, 0, 1, 1);
    private Rect _retroRect = new Rect(0f, 0.2f, 0.82f, 0.82f);

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    // Call this from your MasterSingleton or other scripts
    public void SetRetroMode(bool isRetro)
    {
        if (_cam == null) return;
        _cam.rect = isRetro ? _retroRect : _fullRect;
    }
}