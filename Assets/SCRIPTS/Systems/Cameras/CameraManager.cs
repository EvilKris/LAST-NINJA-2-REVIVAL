using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private GameObject TargetGroupCams;
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private float blendWaitTime = 1f;
    [SerializeField] private RetroViewportController retroViewportController;

    public static Action<bool, Transform[]> OnActivateTargetGroupCams;

    private Coroutine clearTargetsCoroutine;
    private bool isPendingActivation;
    private Transform[] pendingTargets;

    private void Start()
    {
        TargetGroupCams.SetActive(false);
        OnActivateTargetGroupCams += ActivateTargetGroupCams;
    }

    private void OnDestroy()
    {
        OnActivateTargetGroupCams -= ActivateTargetGroupCams;
    }

    public void ActivateTargetGroupCams(bool toggle, Transform[] targets)
    {
        if (toggle)
        {
            if (clearTargetsCoroutine != null)
            {
                StopCoroutine(clearTargetsCoroutine);
                clearTargetsCoroutine = null;
            }

            if (!TargetGroupCams.activeSelf)
            {
                isPendingActivation = true;
                pendingTargets = targets;
                StartCoroutine(ActivateAfterClear(targets));
            }
            else
            {
                UpdateTargetGroup(targets);
            }
        }
        else
        {
            isPendingActivation = false;
            TargetGroupCams.SetActive(false);
            
            if (clearTargetsCoroutine != null)
            {
                StopCoroutine(clearTargetsCoroutine);
            }
            clearTargetsCoroutine = StartCoroutine(ClearTargetsDelayed());
        }
    }

    private IEnumerator ActivateAfterClear(Transform[] targets)
    {
        yield return new WaitForSeconds(blendWaitTime);
        
        if (isPendingActivation && pendingTargets == targets)
        {
            UpdateTargetGroup(targets);
            TargetGroupCams.SetActive(true);
            isPendingActivation = false;
        }
    }

    private void UpdateTargetGroup(Transform[] targets)
    {
        if (targets != null && targets.Length > 0)
        {
            targetGroup.Targets.Clear();
            
            foreach (Transform target in targets)
            {
                if (target != null)
                {
                    targetGroup.AddMember(target, 1f, 0.5f);
                }
            }
        }
    }

    private IEnumerator ClearTargetsDelayed()
    {
        yield return new WaitForSeconds(blendWaitTime);
        targetGroup.Targets.Clear();
        clearTargetsCoroutine = null;
    }

    public void SetRetroMode(bool isRetro)
    {
        if (retroViewportController != null)
        {
            retroViewportController.SetRetroMode(isRetro);
        }
    }   
}

