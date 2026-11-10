using UnityEngine;

internal sealed class LegacyRaceCheckpointTrigger : MonoBehaviour
{
    internal const float DefaultRadius = 12f;
    [SerializeField] private float radiusMeters = DefaultRadius;

    internal static float GetRadius(RaceDirection point)
    {
        var settings = point.GetComponent<LegacyRaceCheckpointTrigger>();
        if (settings) return settings.radiusMeters;
        var sphere = point.GetComponent<SphereCollider>();
        return sphere ? sphere.radius * Scale(point.transform) : DefaultRadius;
    }

    internal static void Configure(RaceDirection point, float radius)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) radius = DefaultRadius;
        var settings = point.GetComponent<LegacyRaceCheckpointTrigger>();
        if (!settings) settings = point.gameObject.AddComponent<LegacyRaceCheckpointTrigger>();
        settings.radiusMeters = radius;
        var sphere = point.GetComponent<SphereCollider>();
        if (!sphere) sphere = point.gameObject.AddComponent<SphereCollider>();
        sphere.center = point.transform.InverseTransformPoint(point.transform.position + Vector3.up);
        sphere.radius = radius / Scale(point.transform);
        sphere.isTrigger = true;
        sphere.enabled = true;
        foreach (var collider in point.GetComponentsInChildren<Collider>(true))
        {
            if (collider == sphere) continue;
            var owners = collider.GetComponentsInParent<RaceDirection>(true);
            if (owners.Length > 0 && owners[0] == point) collider.enabled = false;
        }
    }

    private static float Scale(Transform transform)
    {
        var scale = transform.lossyScale;
        return Mathf.Max(.0001f, Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}
