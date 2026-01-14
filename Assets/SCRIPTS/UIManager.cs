using DG.Tweening;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField]private GameObject inGameUIOverlay; //the main in-game UI overlay
    void Start()
    {/*
        if(inGameUIOverlay != null)
            inGameUIOverlay.SetActive(true);*/
    }

    public void UICamShake(RectTransform canvasRect, float duration = 0.3f, float strength = 30f, int vibrato = 10)
    {
        // Kill any existing shake to prevent overlapping
        canvasRect.DOKill();

        // Shake it!
        canvasRect.DOShakeAnchorPos(duration, strength, vibrato);
    }
}
