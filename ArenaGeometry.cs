using Engine.Core.Math;

namespace GameProject;

internal enum ArenaSurface
{
    Floor,
    Ceiling,
    Wall,
    Orange,
    Green
}

internal readonly record struct ArenaBoxDefinition(
    string Name,
    Vector3 Center,
    Vector3 Size,
    ArenaSurface Surface,
    bool BlocksMovement)
{
    public Vector3 Min => Center - Size * 0.5f;

    public Vector3 Max => Center + Size * 0.5f;
}

internal static class ArenaGeometry
{
    private static readonly ArenaBoxDefinition[] Boxes =
    [
        // Floor and ceiling.
        new(
            "Floor",
            new Vector3(0.0f, -0.15f, 0.0f),
            new Vector3(12.4f, 0.3f, 12.4f),
            ArenaSurface.Floor,
            false),

        new(
            "Ceiling",
            new Vector3(0.0f, 3.15f, 0.0f),
            new Vector3(12.4f, 0.3f, 12.4f),
            ArenaSurface.Ceiling,
            false),

        // Walls.
        new(
            "Left wall",
            new Vector3(-6.15f, 1.5f, 0.0f),
            new Vector3(0.3f, 3.0f, 12.4f),
            ArenaSurface.Wall,
            true),

        new(
            "Right wall",
            new Vector3(6.15f, 1.5f, 0.0f),
            new Vector3(0.3f, 3.0f, 12.4f),
            ArenaSurface.Wall,
            true),

        new(
            "Back wall",
            new Vector3(0.0f, 1.5f, -6.15f),
            new Vector3(12.4f, 3.0f, 0.3f),
            ArenaSurface.Wall,
            true),

        new(
            "Front wall",
            new Vector3(0.0f, 1.5f, 6.15f),
            new Vector3(12.4f, 3.0f, 0.3f),
            ArenaSurface.Wall,
            true),

        // Obstacles.
        new(
            "Obstacle 1",
            new Vector3(-2.5f, 0.75f, -1.5f),
            new Vector3(1.2f, 1.5f, 1.2f),
            ArenaSurface.Orange,
            true),

        new(
            "Obstacle 2",
            new Vector3(2.2f, 0.5f, -2.3f),
            new Vector3(1.2f, 1.0f, 1.2f),
            ArenaSurface.Green,
            true),

        new(
            "Obstacle 3",
            new Vector3(-2.0f, 0.6f, 2.0f),
            new Vector3(1.6f, 1.2f, 0.8f),
            ArenaSurface.Green,
            true),

        new(
            "Obstacle 4",
            new Vector3(2.5f, 1.0f, 1.2f),
            new Vector3(0.9f, 2.0f, 0.9f),
            ArenaSurface.Orange,
            true)
    ];

    public static ReadOnlySpan<ArenaBoxDefinition> All => Boxes;
}