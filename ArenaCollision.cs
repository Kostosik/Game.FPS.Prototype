using Engine.Core.Math;

namespace GameProject;

internal sealed class ArenaCollision
{
    private const float PlayerRadius = 0.32f;
    private const float MaxStepDistance = 0.1f;

    public void Move(
        ref Vector3 position,
        float deltaX,
        float deltaZ)
    {
        var maxDelta = MathF.Max(
            MathF.Abs(deltaX),
            MathF.Abs(deltaZ));

        if (maxDelta < 0.000001f)
        {
            return;
        }

        var stepCount = Math.Max(
            1,
            (int)MathF.Ceiling(
                maxDelta / MaxStepDistance));

        var stepX = deltaX / stepCount;
        var stepZ = deltaZ / stepCount;

        for (var i = 0; i < stepCount; i++)
        {
            TryMoveAxis(ref position, stepX, 0.0f);
            TryMoveAxis(ref position, 0.0f, stepZ);
        }
    }

    private static void TryMoveAxis(
        ref Vector3 position,
        float deltaX,
        float deltaZ)
    {
        var candidateX = position.X + deltaX;
        var candidateZ = position.Z + deltaZ;

        if (IsBlocked(candidateX, candidateZ))
        {
            return;
        }

        position = new Vector3(
            candidateX,
            position.Y,
            candidateZ);
    }

    private static bool IsBlocked(float x, float z)
    {
        var radiusSquared = PlayerRadius * PlayerRadius;

        foreach (var box in ArenaGeometry.All)
        {
            if (!box.BlocksMovement)
            {
                continue;
            }

            var min = box.Min;
            var max = box.Max;

            var closestX = Math.Clamp(
                x,
                min.X,
                max.X);

            var closestZ = Math.Clamp(
                z,
                min.Z,
                max.Z);

            var dx = x - closestX;
            var dz = z - closestZ;

            var distanceSquared =
                dx * dx + dz * dz;

            if (distanceSquared < radiusSquared)
            {
                return true;
            }
        }

        return false;
    }
}