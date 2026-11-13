
using Engine.Core.Math;

namespace GameProject;

internal sealed class ArenaCollision
{
    private const float PlayerRadius = 0.35f;
    private const float MaxStepDistance = 0.08f;

    // Внутренние границы арены: ±6 метров.
    private const float MinPosition = -6.0f + PlayerRadius;
    private const float MaxPosition = 6.0f - PlayerRadius;

    private static readonly ObstacleBounds[] Obstacles =
    {
        // (-2.5, 0.75, -1.5), размер (1.2, 1.5, 1.2)
        new(-3.1f, -2.1f, -1.9f, -0.9f),

        // (2.2, 0.5, -2.3), размер (1.2, 1.0, 1.2)
        new(1.6f, -2.9f, 2.8f, -1.7f),

        // (-2.0, 0.6, 2.0), размер (1.6, 1.2, 0.8)
        new(-2.8f, 1.6f, -1.2f, 2.4f),

        // (2.5, 1.0, 1.2), размер (0.9, 2.0, 0.9)
        new(2.05f, 0.75f, 2.95f, 1.65f)
    };

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

        // Разбиваем перемещение на небольшие шаги,
        // чтобы не проскальзывать сквозь препятствия.
        var stepCount = Math.Max(
            1,
            (int)MathF.Ceiling(
                maxDelta / MaxStepDistance));

        var stepX = deltaX / stepCount;
        var stepZ = deltaZ / stepCount;

        for (var i = 0; i < stepCount; i++)
        {
            // Сначала перемещение по X.
            var nextX = Math.Clamp(
                position.X + stepX,
                MinPosition,
                MaxPosition);

            if (!IntersectsObstacle(nextX, position.Z))
            {
                position.X = nextX;
            }

            // Затем перемещение по Z.
            // Если X заблокирован, Z всё равно может измениться.
            // Так реализуется скольжение вдоль препятствий.
            var nextZ = Math.Clamp(
                position.Z + stepZ,
                MinPosition,
                MaxPosition);

            if (!IntersectsObstacle(position.X, nextZ))
            {
                position.Z = nextZ;
            }
        }
    }

    private static bool IntersectsObstacle(
        float centerX,
        float centerZ)
    {
        foreach (var obstacle in Obstacles)
        {
            // Находим ближайшую точку AABB к центру игрока.
            var closestX = Math.Clamp(
                centerX,
                obstacle.MinX,
                obstacle.MaxX);

            var closestZ = Math.Clamp(
                centerZ,
                obstacle.MinZ,
                obstacle.MaxZ);

            var dx = centerX - closestX;
            var dz = centerZ - closestZ;

            var distanceSquared =
                dx * dx + dz * dz;

            if (distanceSquared <
                PlayerRadius * PlayerRadius)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct ObstacleBounds(
        float MinX,
        float MinZ,
        float MaxX,
        float MaxZ);
}
