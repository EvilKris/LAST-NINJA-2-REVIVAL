using UnityEngine;

/// <summary>
/// Marker component placed on empty GameObjects around the map.
/// The drowning system finds the nearest one to respawn the player.
/// </summary>
public class RespawnPoint : MonoBehaviour
{
    /// <summary>
    /// Returns the closest <see cref="RespawnPoint"/> to <paramref name="position"/>.
    /// Returns null if none exist in the scene.
    /// </summary>
    public static RespawnPoint FindNearest(Vector3 position)
    {
        RespawnPoint[] points = FindObjectsByType<RespawnPoint>(FindObjectsSortMode.None);
        if (points.Length == 0) return null;

        RespawnPoint nearest = null;
        float bestSqrDist = float.MaxValue;

        foreach (RespawnPoint point in points)
        {
            float sqrDist = (point.transform.position - position).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                nearest = point;
            }
        }

        return nearest;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawIcon(transform.position, "d_Prefab Icon", true);
    }
}

