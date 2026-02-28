using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TextMeshProColourTransitions : MonoBehaviour
{
    [SerializeField] private Color[] colors = new Color[] { Color.red, Color.blue, Color.green };
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private bool enableBlink = true;
    [SerializeField] private float blinkInterval = 1f;
    [SerializeField] private float blinkDuration = 0.1f;

    private TextMeshProUGUI _textMesh;
    private int _currentIndex = 0;
    private int _nextIndex = 1;
    private float _transitionTimer = 0f;
    private float _blinkTimer = 0f;
    private bool _isBlinking = false;
    private Color _colorBeforeBlink;

    void Start()
    {
        _textMesh = GetComponent<TextMeshProUGUI>();
        
        if (colors.Length > 0)
        {
            _textMesh.color = colors[0];
        }
    }

    void Update()
    {
        if (colors.Length < 2) return;

        // Handle blinking
        if (enableBlink)
        {
            _blinkTimer += Time.deltaTime;

            if (!_isBlinking && _blinkTimer >= blinkInterval)
            {
                // Start blink
                _isBlinking = true;
                _colorBeforeBlink = _textMesh.color;
                _textMesh.color = Color.black;
                _blinkTimer = 0f;
            }
            else if (_isBlinking && _blinkTimer >= blinkDuration)
            {
                // End blink
                _isBlinking = false;
                _textMesh.color = _colorBeforeBlink;
                _blinkTimer = 0f;
            }
        }

        // Don't update color transition while blinking
        if (_isBlinking) return;

        // Color transition logic
        _transitionTimer += Time.deltaTime;
        float t = _transitionTimer / transitionDuration;

        _textMesh.color = Color.Lerp(colors[_currentIndex], colors[_nextIndex], t);

        if (_transitionTimer >= transitionDuration)
        {
            _transitionTimer = 0f;
            _currentIndex = _nextIndex;
            _nextIndex = (_nextIndex + 1) % colors.Length;
        }
    }
}
