using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using JSAM;

[RequireComponent(typeof(Button))]
public class ComprehensiveUIButton : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerExitHandler, 
    IPointerDownHandler, 
    IPointerUpHandler, 
    IPointerClickHandler, 
    ISelectHandler, 
    IDeselectHandler, 
    ISubmitHandler
{
    [System.Serializable]
    public class ButtonEvent
    {
        public SoundFileObject sfx;
        public UnityEvent onEvent;
    }

    [Header("Button Events & SFX")]
    [SerializeField] private ButtonEvent onPointerEnter = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerExit = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerDown = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerUp = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerClick = new ButtonEvent();
    [SerializeField] private ButtonEvent onSelect = new ButtonEvent();
    [SerializeField] private ButtonEvent onDeselect = new ButtonEvent();
    [SerializeField] private ButtonEvent onSubmit = new ButtonEvent();

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();       
    }

    private void ExecuteEvent(ButtonEvent buttonEvent)
    {   
        // Play JSAM sound if assigned
        if (buttonEvent.sfx != null)
        {
            if (JSAM.AudioManager.Instance != null)
            {
                JSAM.AudioManager.PlaySound(buttonEvent.sfx);
            }
            else
            {
                Debug.LogWarning("JSAM AudioManager not found in scene. Cannot play sound: " + buttonEvent.sfx.name);
            }
        }
        
        // Always invoke UnityEvent
        buttonEvent.onEvent?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {   
        ExecuteEvent(onPointerEnter);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ExecuteEvent(onPointerExit);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ExecuteEvent(onPointerDown);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ExecuteEvent(onPointerUp);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ExecuteEvent(onPointerClick);
    }

    public void OnSelect(BaseEventData eventData)
    {
        ExecuteEvent(onSelect);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ExecuteEvent(onDeselect);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ExecuteEvent(onSubmit);
    }

    // Public methods to trigger events programmatically if needed
    public void TriggerPointerEnter() => ExecuteEvent(onPointerEnter);
    public void TriggerPointerExit() => ExecuteEvent(onPointerExit);
    public void TriggerDown() => ExecuteEvent(onPointerDown);
    public void TriggerPointerUp() => ExecuteEvent(onPointerUp);
    public void TriggerPointerClick() => ExecuteEvent(onPointerClick);
    public void TriggerSelect() => ExecuteEvent(onSelect);
    public void TriggerDeselect() => ExecuteEvent(onDeselect);
    public void TriggerSubmit() => ExecuteEvent(onSubmit);
}