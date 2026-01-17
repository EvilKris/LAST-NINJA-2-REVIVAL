using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ComprehensiveUIButton : Button
{
    public enum AudioPlaybackMode
    {
        Default,        // No audio handling - just execute events
        AudioSource     // Use AudioSource for direct playback
    }

    [System.Serializable]
    public class ButtonEvent
    {
        public AudioClip sfx;
        public UnityEvent onEvent;
    }

    [Header("Audio Playback Method")]
    [SerializeField] private AudioPlaybackMode audioMode = AudioPlaybackMode.Default;

    [Header("Button Events & SFX")]
    [SerializeField] private ButtonEvent onPointerEnter = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerExit = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerDown = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerUp = new ButtonEvent();
    [SerializeField] private ButtonEvent onPointerClick = new ButtonEvent();
    [SerializeField] private ButtonEvent onSelect = new ButtonEvent();
    [SerializeField] private ButtonEvent onDeselect = new ButtonEvent();
    [SerializeField] private ButtonEvent onSubmit = new ButtonEvent();

    [Header("AudioSource Settings (AudioSource mode only)")]
    [SerializeField] private bool respectInteractableState = true;

    private AudioSource audioSource;

    protected override void Awake()
    {
        base.Awake();

        if (audioMode == AudioPlaybackMode.AudioSource)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnValidate()
    {
        if (audioMode == AudioPlaybackMode.AudioSource)
        {
            // Add AudioSource if it doesn't exist
            if (GetComponent<AudioSource>() == null)
            {
                AudioSource newAudioSource = gameObject.AddComponent<AudioSource>();
                newAudioSource.playOnAwake = false;
                newAudioSource.spatialBlend = 0f; // 2D sound for UI
            }
        }
        else if (audioMode == AudioPlaybackMode.Default)
        {
            // Remove AudioSource if it exists
            AudioSource existingAudioSource = GetComponent<AudioSource>();
            if (existingAudioSource != null)
            {
                DestroyImmediate(existingAudioSource);
            }
        }
    }

    private void ExecuteEvent(ButtonEvent buttonEvent)
    {
        if (respectInteractableState && !interactable)
            return;

        // Only play audio in AudioSource mode
        if (audioMode == AudioPlaybackMode.AudioSource)
        {
            if (buttonEvent.sfx != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonEvent.sfx);
            }
        }

        // Always invoke UnityEvent
        buttonEvent.onEvent?.Invoke();
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        ExecuteEvent(onPointerEnter);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        ExecuteEvent(onPointerExit);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        ExecuteEvent(onPointerDown);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        ExecuteEvent(onPointerUp);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        base.OnPointerClick(eventData);
        ExecuteEvent(onPointerClick);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        ExecuteEvent(onSelect);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        ExecuteEvent(onDeselect);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        base.OnSubmit(eventData);
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