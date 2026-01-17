using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;


public class PauseManagerFunctions : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference m_PauseAction;
    [Header("Canvas")]
    [SerializeField] private Canvas m_PauseCanvas;


    [Header("Events")]
    [SerializeField] private UnityEvent startEvent;
    [SerializeField] private UnityEvent m_OnPause;
    [SerializeField] private UnityEvent m_OnResume;

    private bool m_IsPaused;

    public bool IsPaused => m_IsPaused;

    private void Awake()
    {
        if (m_PauseCanvas == null)
        { 
            m_PauseCanvas = GetComponent<Canvas>();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        startEvent?.Invoke();

        if (m_PauseAction != null)
        {
            m_PauseAction.action.performed += OnPausePerformed;
            m_PauseAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (m_PauseAction != null)
        {
            m_PauseAction.action.performed -= OnPausePerformed;
            m_PauseAction.action.Disable();
        }
    }


    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        // Ignore pause if interaction is happening
        // if (MasterSingleton.Instance.gameData.IsInteractionHappening)
        //     return;

        if (m_IsPaused)
            Resume();
        else
            Pause();

        // MasterSingleton.Instance.gameData.GamePaused = m_IsPaused;
    }

    public void Pause()
    {
        if (m_IsPaused || m_PauseCanvas == null) return;

        m_PauseCanvas.transform.SetAsLastSibling();
        m_PauseCanvas.gameObject.SetActive(true);

        m_IsPaused = true;
        Time.timeScale = 0f;
        m_OnPause?.Invoke();
    }

    public void Resume()
    {
        if (!m_IsPaused || m_PauseCanvas == null) return;

        m_PauseCanvas.gameObject.SetActive(false);

        m_IsPaused = false;
        Time.timeScale = 1f;
        m_OnResume?.Invoke();
    }
    public void QuitGame() //called via event
    {
#if UNITY_EDITOR
        // Stop play mode in the editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit the built application
        Application.Quit();
#endif
    }

}
