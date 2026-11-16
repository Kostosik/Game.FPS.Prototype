
using Engine.Core.Math;

namespace GameProject;

internal sealed class ArenaRaycast
{
    public bool TryCast(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        out ArenaRaycastHit hit)
    {
        if (!float.IsFinite(maxDistance) || maxDistance <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        }

        if (direction.LengthSquared() <= 0.000001f)
        {
            hit = default;
            return false;
        }

        direction = direction.Normalize();

        ArenaBoxDefinition? nearestCollider = null;
        Vector3 nearestNormal = Vector3.Zero;
        var nearestDistance = maxDistance;

        foreach (var collider in ArenaGeometry.All)
        {
            if (!IntersectAabb(
                    origin,
                    direction,
                    collider,
                    maxDistance,
                    out var distance,
                    out var normal))
            {
                continue;
            }

            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestNormal = normal;
            nearestCollider = collider;
        }

        if (nearestCollider is not { } closest)
        {
            hit = default;
            return false;
        }

        hit = new ArenaRaycastHit(
            closest.Name,
            origin + direction * nearestDistance,
            nearestNormal,
            nearestDistance);

        return true;
    }

    private static bool IntersectAabb(
        Vector3 origin,
        Vector3 direction,
        ArenaBoxDefinition collider,
        float maxDistance,
        out float distance,
        out Vector3 normal)
    {
        var near = float.NegativeInfinity;
        var far = float.PositiveInfinity;

        var nearNormal = Vector3.Zero;
        var farNormal = Vector3.Zero;

        if (!ClipAxis(
                origin.X, direction.X,
                collider.Min.X, collider.Max.X,
                new Vector3(-1, 0, 0),
                new Vector3(1, 0, 0),
                ref near, ref far,
                ref nearNormal, ref farNormal) ||
            !ClipAxis(
                origin.Y, direction.Y,
                collider.Min.Y, collider.Max.Y,
                new Vector3(0, -1, 0),
                new Vector3(0, 1, 0),
                ref near, ref far,
                ref nearNormal, ref farNormal) ||
            !ClipAxis(
                origin.Z, direction.Z,
                collider.Min.Z, collider.Max.Z,
                new Vector3(0, 0, -1),
                new Vector3(0, 0, 1),
                ref near, ref far,
                ref nearNormal, ref farNormal))
        {
            distance = 0.0f;
            normal = Vector3.Zero;
            return false;
        }

        if (near >= 0.0f)
        {
            distance = near;
            normal = nearNormal;
        }
        else
        {
            distance = far;
            normal = farNormal;
        }

        return distance >= 0.0f &&
               distance <= maxDistance;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float min,
        float max,
        Vector3 minNormal,
        Vector3 maxNormal,
        ref float near,
        ref float far,
        ref Vector3 nearNormal,
        ref Vector3 farNormal)
    {
        if (MathF.Abs(direction) < 0.000001f)
        {
            return origin >= min && origin <= max;
        }

        var first = (min - origin) / direction;
        var second = (max - origin) / direction;

        var firstNormal = minNormal;
        var secondNormal = maxNormal;

        if (first > second)
        {
            (first, second) = (second, first);
            (firstNormal, secondNormal) = (secondNormal, firstNormal);
        }

        if (first > near)
        {
            near = first;
            nearNormal = firstNormal;
        }

        if (second < far)
        {
            far = second;
            farNormal = secondNormal;
        }

        return near <= far;
    }
}

internal readonly record struct ArenaRaycastHit(
    string ColliderName,
    Vector3 Point,
    Vector3 Normal,
    float Distance);
