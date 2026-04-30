using UnityEngine;

/// <summary>
/// A simple ordered list of Transform waypoints placed in the scene.
/// Drop this on an empty GameObject, then assign child Transforms as the points.
/// Referenced by <see cref="PatrolWaypointsAction"/> via the Behavior Graph Blackboard.
/// </summary>
public class WaypointPath : MonoBehaviour
{
    [Tooltip("Ordered list of patrol positions. The actor visits them in sequence and then loops.")]
    public Transform[] points = System.Array.Empty<Transform>();

    /// <summary>Number of waypoints in the path.</summary>
    public int Count => points.Length;

    /// <summary>Returns the waypoint Transform at <paramref name="index"/> (wraps around).</summary>
    public Transform Get(int index) => points[index % points.Length];

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (points == null || points.Length < 2) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            Gizmos.DrawSphere(points[i].position, 0.15f);

            Transform next = points[(i + 1) % points.Length];
            if (next != null)
                Gizmos.DrawLine(points[i].position, next.position);
        }
    }
#endif
}
