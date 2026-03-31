using UnityEngine;

public class Detector : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public LayerMask obstructionMask; // layer for your trees / obstacles

    private FadeObstacle _currentObstacle;

    private void LateUpdate()
    {
        if (!cameraTransform) return;

        Vector3 origin = cameraTransform.position;
        Vector3 target = transform.position;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;

        // Raycast from camera to player
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, obstructionMask))
        {
            FadeObstacle fade = hit.collider.GetComponent<FadeObstacle>();

            if (fade != null && fade != _currentObstacle)
            {
                // New obstacle: fade old one in, new one out
                if (_currentObstacle != null)
                    _currentObstacle.FadeIn();

                fade.FadeOut();
                _currentObstacle = fade;
            }
        }
        else
        {
            // Nothing blocking: fade previous obstacle back in
            if (_currentObstacle != null)
            {
                _currentObstacle.FadeIn();
                _currentObstacle = null;
            }
        }
    }
}
