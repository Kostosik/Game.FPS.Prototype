using Engine.Input;

namespace GameProject;

internal static class FpsInputActions
{
    public static readonly InputAction MoveForward = new("MoveForward");
    public static readonly InputAction MoveBackward = new("MoveBackward");
    public static readonly InputAction MoveLeft = new("MoveLeft");
    public static readonly InputAction MoveRight = new("MoveRight");

    public static InputActionMap CreateMap()
    {
        var map = new InputActionMap();

        map.Bind(MoveForward, new InputBinding("Keyboard.W"));
        map.Bind(MoveBackward, new InputBinding("Keyboard.S"));
        map.Bind(MoveLeft, new InputBinding("Keyboard.A"));
        map.Bind(MoveRight, new InputBinding("Keyboard.D"));

        return map;
    }
}