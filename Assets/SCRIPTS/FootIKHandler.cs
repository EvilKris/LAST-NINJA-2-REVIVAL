using UnityEngine;


/*
 * To fix the foot sliding (ashi no suberi - 足の滑り) without needing to manually animate every diagonal, we can use Foot IK (Inverse Kinematics). This forces the feet to lock to the ground based on the animation's "contact" points, significantly improving the look of blended movements.

1. Enable IK on the Clinch Layer
Before the code can work, the Animator must be told to calculate IK for your specific state:

Go to your Animator window and select the Layers tab.

Click the cog icon on the layer where your Clinch Blend Tree lives (e.g., your Base Layer or a dedicated Clinch Layer).

Check the box for IK Pass. This tells Unity to call the OnAnimatorIK() function every frame while this layer is active.

2. The Foot IK Script
You can add this logic to your MovementComponent or a dedicated FootIKHandler. It uses the raycasting method to find the ground and "snap" the feet down during blended walk cycles.
*/

public class FootIKHandler : MonoBehaviour
{
    private Animator _animator;

    [Range(0, 1)] public float footWeight = 1.0f; // Adjust this to blend the effect
    public LayerMask groundLayer;
    public float footOffset = 0.1f;

    void Awake() => _animator = GetComponent<Animator>();

    // This is called automatically by Unity if "IK Pass" is checked
    void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;

        // Set Weights
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, footWeight);
        _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, footWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, footWeight);

        // Process Left Foot
        ApplyFootIK(AvatarIKGoal.LeftFoot);
        // Process Right Foot
        ApplyFootIK(AvatarIKGoal.RightFoot);
    }

    void ApplyFootIK(AvatarIKGoal foot)
    {
        RaycastHit hit;
        Vector3 footPos = _animator.GetIKPosition(foot);

        // Raycast down from above the foot to find the floor
        if (Physics.Raycast(footPos + Vector3.up, Vector3.down, out hit, 2f, groundLayer))
        {
            Vector3 targetPos = hit.point;
            targetPos.y += footOffset;
            _animator.SetIKPosition(foot, targetPos);

            // Align foot rotation with the ground slope
            _animator.SetIKRotation(foot, Quaternion.LookRotation(transform.forward, hit.normal));
        }
    }
}