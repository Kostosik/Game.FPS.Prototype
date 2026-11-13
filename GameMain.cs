
using Engine.Camera;
using Engine.Core.Math;
using Engine.Core.Time;
using Engine.Graphics.Rendering;
using Engine.Graphics.Resources;
using Engine.Graphics3D;
using Engine.Graphics3D.Commands;
using Engine.Graphics3D.Lighting;
using Engine.Graphics3D.Rendering;
using Engine.Graphics3D.Resources;
using Engine.Hosting.Desktop;
using Engine.Input;
using Engine.Input.Cursors;
using Engine.Networking.Replication;
using Engine.Runtime;
using Engine.Serialization.SaveLoad.Ecs;
using Engine.Serialization.SaveLoad.Worlds;

namespace GameProject;

internal sealed class GameMain : EngineGame
{
    private readonly ArenaRaycast _arenaRaycast = new();

    private Vector3? _impactMarkerPosition;
    private float _impactMarkerTime;

    private const float WeaponRange = 40.0f;

    private static readonly Material3D ImpactMaterial = new(
        new MaterialColor(1.0f, 0.05f, 0.02f),
        0.5f,
        0.5f);

    private readonly DesktopGameHostContext _context;
    private readonly string _startupScenePath;
    private readonly ArenaCollision _arenaCollision = new();
    private Camera3DController? _cameraController;

    private bool _forceLockedCursor;

    private const float MoveSpeed = 5.0f;


    private const float PlayerRadius = 0.32f;

    // Внутренняя граница стен арены с учётом радиуса игрока.
    private const float ArenaMovementLimit = 5.68f;

    private const float CollisionSubstep = 0.1f;

    private readonly record struct ObstacleBounds(
        float MinX,
        float MaxX,
        float MinZ,
        float MaxZ);

    // Эти границы соответствуют размерам ящиков в OnRender.
    private static readonly ObstacleBounds[] Obstacles =
    [
        new(-3.1f, -1.9f, -2.1f, -0.9f),
    new( 1.6f,  2.8f, -2.9f, -1.7f),
    new(-2.8f, -1.2f,  1.6f,  2.4f),
    new( 2.05f, 2.95f, 0.75f, 1.65f)
    ];


    private IMeshManager? _meshes;
    private Camera3D? _camera;

    private MeshHandle _cubeMesh;
    private ShaderHandle _shader;

    private const string VertexShader = """
        #version 330 core

        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aTexCoord;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vWorldPosition;
        out vec3 vNormal;

        void main()
        {
            vec4 worldPosition = uModel * vec4(aPosition, 1.0);

            vWorldPosition = worldPosition.xyz;
            vNormal = mat3(transpose(inverse(uModel))) * aNormal;

            gl_Position =
                uProjection *
                uView *
                worldPosition;
        }
        """;

    private const string FragmentShader = """
        #version 330 core

        #define MAX_POINT_LIGHTS 4

        in vec3 vNormal;
        in vec3 vWorldPosition;

        uniform vec3 uBaseColor;
        uniform float uAmbientStrength;
        uniform float uDiffuseStrength;

        uniform vec3 uDirectionalLightDirection;
        uniform vec3 uDirectionalLightColor;
        uniform float uDirectionalLightIntensity;

        struct PointLight
        {
            vec3 position;
            vec3 color;
            float intensity;
            float range;
        };

        uniform PointLight uPointLights[MAX_POINT_LIGHTS];

        out vec4 FragColor;

        void main()
        {
            vec3 normal = normalize(vNormal);

            vec3 finalColor =
                uBaseColor * uAmbientStrength;

            vec3 direction =
                normalize(uDirectionalLightDirection);

            float directionalDiffuse = max(
                dot(normal, direction),
                0.0);

            finalColor +=
                uBaseColor *
                uDirectionalLightColor *
                directionalDiffuse *
                uDiffuseStrength *
                uDirectionalLightIntensity;

            for (int i = 0; i < MAX_POINT_LIGHTS; i++)
            {
                PointLight light = uPointLights[i];

                vec3 toLight =
                    light.position - vWorldPosition;

                float distanceToLight = length(toLight);

                if (distanceToLight <= 0.0001 ||
                    distanceToLight >= light.range)
                {
                    continue;
                }

                vec3 lightDirection =
                    toLight / distanceToLight;

                float diffuse = max(
                    dot(normal, lightDirection),
                    0.0);

                float attenuation =
                    1.0 - distanceToLight / light.range;

                attenuation *= attenuation;

                finalColor +=
                    uBaseColor *
                    light.color *
                    diffuse *
                    uDiffuseStrength *
                    light.intensity *
                    attenuation;
            }

            FragColor = vec4(finalColor, 1.0);
        }
        """;

    private static readonly SceneLighting3D SceneLighting = new(
        new DirectionalLight3D(
            new Vector3(-0.4f, 0.7f, 0.5f),
            new Vector3(1.0f, 0.95f, 0.85f),
            1.1f),
        new[]
        {
            new PointLight3D(
                new Vector3(-3.0f, 2.5f, 3.0f),
                new Vector3(1.0f, 0.55f, 0.25f),
                2.0f,
                7.0f),

            new PointLight3D(
                new Vector3(3.0f, 2.5f, -3.0f),
                new Vector3(0.25f, 0.45f, 1.0f),
                1.5f,
                6.0f)
        });

    private static readonly Material3D FloorMaterial = new(
        new MaterialColor(0.22f, 0.25f, 0.30f),
        0.35f,
        0.65f);

    private static readonly Material3D CeilingMaterial = new(
        new MaterialColor(0.13f, 0.15f, 0.19f),
        0.35f,
        0.65f);

    private static readonly Material3D WallMaterial = new(
        new MaterialColor(0.32f, 0.36f, 0.43f),
        0.30f,
        0.70f);

    private static readonly Material3D OrangeMaterial = new(
        new MaterialColor(0.85f, 0.30f, 0.12f),
        0.25f,
        0.75f);

    private static readonly Material3D GreenMaterial = new(
        new MaterialColor(0.20f, 0.55f, 0.35f),
        0.25f,
        0.75f);

    public GameMain(
        DesktopGameHostContext context,
        string startupScenePath)
        : base(
            (context ?? throw new ArgumentNullException(nameof(context)))
            .Runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            startupScenePath);

        _context = context;
        _startupScenePath = Path.GetFullPath(startupScenePath);
    }

    protected override void ConfigureRuntime()
    {
        if (!File.Exists(_startupScenePath))
        {
            throw new FileNotFoundException(
                "Startup scene was not found.",
                _startupScenePath);
        }

        var serializers = new EcsComponentSerializerRegistry();

        serializers.RegisterEngineComponents();
        serializers.RegisterNetworkComponents();

        var saveLoad = new WorldSaveLoadService(serializers);

        saveLoad.Load(
            _startupScenePath,
            Runtime.World);
    }

    protected override void OnInitialize()
    {
        var graphics = Runtime.Services.Graphics;

        if (graphics is not IMeshManagerProvider meshProvider)
        {
            throw new NotSupportedException(
                "The active graphics backend does not provide 3D mesh management.");
        }

        _meshes = meshProvider.Meshes;

        _camera = new Camera3D
        {
            Position = new Vector3(0.0f, 1.6f, 4.8f),
            Rotation = Quaternion.Identity,
            FieldOfView = MathF.PI / 3.0f,
            NearPlane = 0.1f,
            FarPlane = 100.0f
        };

        _cameraController = new Camera3DController(_camera)
        {
            LookSensitivity = 0.0025f
        };

        if (!_context.Cursor.IsModeSupported(PointerMode.Locked))
        {
            throw new NotSupportedException(
                "Locked cursor mode is not supported.");
        }

        _context.Cursor.SetMode(PointerMode.Locked);

        UpdateCameraViewport(
            _context.WindowWidth,
            _context.WindowHeight);

        graphics.Pipeline.Clear();
        graphics.Pipeline.PostProcessing.Clear();

        graphics.Pipeline.AddPass(
            new RenderPass(
                Name: "World3D",
                Target: RenderTargetHandle.Invalid,
                State: new RenderState(
                    RenderBlendMode.Disabled,
                    DepthTestEnabled: true,
                    RenderCullMode.Back),
                ClearColor: true,
                Layers: new RenderPassLayerRange(
                    RenderLayers.World,
                    RenderLayers.Present - 1))
            {
                ContextData = SceneLighting
            });

        _shader = graphics.Shaders.Create(
            new ShaderDescription(
                VertexShader,
                FragmentShader));

        _cubeMesh = _meshes.Create(
            MeshPrimitives.CreateCube());

        _context.Resized += OnResized;

        Console.WriteLine("FPS prototype initialized.");
        Console.WriteLine("3D arena created.");
    }

    protected override void OnUpdate(TimeSnapshot time)
    {
        var deltaSeconds = (float)Math.Clamp(
            time.Delta.TotalSeconds,
            0.0,
            0.1);

        var input = Runtime.Services.Input;

        var forwardInput =
            input.GetValue(FpsInputActions.MoveForward) -
            input.GetValue(FpsInputActions.MoveBackward);

        var rightInput =
            input.GetValue(FpsInputActions.MoveRight) -
            input.GetValue(FpsInputActions.MoveLeft);

        if (input.IsPressed(FpsInputActions.TogglePointerLock))
        {
            var nextMode =
                _context.Cursor.Mode == PointerMode.Locked
                    ? PointerMode.Normal
                    : PointerMode.Locked;

            if (!_context.Cursor.IsModeSupported(nextMode))
            {
                throw new NotSupportedException(
                    $"Cursor mode '{nextMode}' is not supported.");
            }

            _context.Cursor.SetMode(nextMode);
        }

        UpdateMouseLook();

        var camera = _camera;

        if (camera is null || deltaSeconds <= 0.0f)
        {
            return;
        }

        _impactMarkerTime = MathF.Max(
    0.0f,
    _impactMarkerTime - deltaSeconds);

        if (_context.PointerInput?.IsPressed(InputMouseButton.Left) == true)
        {
            FireHitscan();
        }

        // Движение только по горизонтальной плоскости.
        // Взгляд вверх или вниз не должен менять высоту игрока.
        var forward = new Vector3(
            camera.Forward.X,
            0.0f,
            camera.Forward.Z);

        var right = new Vector3(
            camera.Right.X,
            0.0f,
            camera.Right.Z);

        if (forward.LengthSquared() > 0.000001f)
        {
            forward = forward.Normalize();
        }

        if (right.LengthSquared() > 0.000001f)
        {
            right = right.Normalize();
        }

        var movement =
            forward * forwardInput +
            right * rightInput;

        // Нормализация не позволяет двигаться быстрее по диагонали.
        if (movement.LengthSquared() > 1.0f)
        {
            movement = movement.Normalize();
        }

        MoveWithCollision(
    movement * MoveSpeed * deltaSeconds);
    }

    private void FireHitscan()
    {
        var camera = _camera;

        if (camera is null)
        {
            return;
        }

        if (!_arenaRaycast.TryCast(
                camera.Position,
                camera.Forward,
                WeaponRange,
                out var hit))
        {
            _impactMarkerPosition = null;
            Console.WriteLine("[Weapon] Miss");
            return;
        }

        _impactMarkerPosition =
            hit.Point + hit.Normal * 0.045f;

        _impactMarkerTime = 0.2f;

        Console.WriteLine(
            $"[Weapon] Hit: {hit.ColliderName}, " +
            $"distance: {hit.Distance:F2} m");
    }

    private void MoveWithCollision(Vector3 displacement)
    {
        var largestAxisDistance = MathF.Max(
            MathF.Abs(displacement.X),
            MathF.Abs(displacement.Z));

        // Разбиваем движение на небольшие шаги,
        // чтобы игрок не пролетал сквозь тонкие стены.
        var steps = Math.Max(
            1,
            (int)MathF.Ceiling(
                largestAxisDistance / CollisionSubstep));

        var stepX = displacement.X / steps;
        var stepZ = displacement.Z / steps;

        for (var i = 0; i < steps; i++)
        {
            // Раздельная обработка осей позволяет скользить
            // вдоль стены, когда одна из осей заблокирована.
            TryMoveAxis(stepX, 0.0f);
            TryMoveAxis(0.0f, stepZ);
        }
    }

    private void TryMoveAxis(float deltaX, float deltaZ)
    {
        var camera = _camera;

        if (camera is null)
        {
            return;
        }

        var candidateX = camera.Position.X + deltaX;
        var candidateZ = camera.Position.Z + deltaZ;

        if (IsPlayerPositionBlocked(candidateX, candidateZ))
        {
            return;
        }

        camera.Position = new Vector3(
            candidateX,
            camera.Position.Y,
            candidateZ);
    }

    private bool IsPlayerPositionBlocked(float x, float z)
    {
        // Границы арены.
        if (x < -ArenaMovementLimit ||
            x > ArenaMovementLimit ||
            z < -ArenaMovementLimit ||
            z > ArenaMovementLimit)
        {
            return true;
        }

        var radiusSquared = PlayerRadius * PlayerRadius;

        foreach (var obstacle in Obstacles)
        {
            // Ближайшая к игроку точка прямоугольника.
            var closestX = Math.Clamp(
                x,
                obstacle.MinX,
                obstacle.MaxX);

            var closestZ = Math.Clamp(
                z,
                obstacle.MinZ,
                obstacle.MaxZ);

            var differenceX = x - closestX;
            var differenceZ = z - closestZ;

            var distanceSquared =
                differenceX * differenceX +
                differenceZ * differenceZ;

            if (distanceSquared < radiusSquared)
            {
                return true;
            }
        }

        return false;
    }


    private void UpdateMouseLook()
    {
        var pointer = _context.PointerInput;

        if (pointer is null ||
            _context.Cursor.Mode != PointerMode.Locked)
        {
            return;
        }

        _cameraController?.LookFromMouseDelta(
            pointer.RelativeDelta);
    }

    protected override void OnRender(double interpolationAlpha)
    {
        var camera = _camera;

        if (camera is null ||
            !_cubeMesh.IsValid ||
            !_shader.IsValid)
        {
            return;
        }

        var view = camera.ViewMatrix;
        var projection = camera.ProjectionMatrix;

        // Floor.
        DrawCube(
            new Vector3(0.0f, -0.15f, 0.0f),
            new Vector3(12.4f, 0.3f, 12.4f),
            FloorMaterial,
            view,
            projection);

        // Ceiling.
        DrawCube(
            new Vector3(0.0f, 3.15f, 0.0f),
            new Vector3(12.4f, 0.3f, 12.4f),
            CeilingMaterial,
            view,
            projection);

        // Left and right walls.
        DrawCube(
            new Vector3(-6.15f, 1.5f, 0.0f),
            new Vector3(0.3f, 3.0f, 12.4f),
            WallMaterial,
            view,
            projection);

        DrawCube(
            new Vector3(6.15f, 1.5f, 0.0f),
            new Vector3(0.3f, 3.0f, 12.4f),
            WallMaterial,
            view,
            projection);

        // Front and back walls.
        DrawCube(
            new Vector3(0.0f, 1.5f, -6.15f),
            new Vector3(12.4f, 3.0f, 0.3f),
            WallMaterial,
            view,
            projection);

        DrawCube(
            new Vector3(0.0f, 1.5f, 6.15f),
            new Vector3(12.4f, 3.0f, 0.3f),
            WallMaterial,
            view,
            projection);

        // Arena obstacles.
        DrawCube(
            new Vector3(-2.5f, 0.75f, -1.5f),
            new Vector3(1.2f, 1.5f, 1.2f),
            OrangeMaterial,
            view,
            projection);

        DrawCube(
            new Vector3(2.2f, 0.5f, -2.3f),
            new Vector3(1.2f, 1.0f, 1.2f),
            GreenMaterial,
            view,
            projection);

        DrawCube(
            new Vector3(-2.0f, 0.6f, 2.0f),
            new Vector3(1.6f, 1.2f, 0.8f),
            GreenMaterial,
            view,
            projection);

        DrawCube(
            new Vector3(2.5f, 1.0f, 1.2f),
            new Vector3(0.9f, 2.0f, 0.9f),
            OrangeMaterial,
            view,
            projection);

        if (_impactMarkerTime > 0.0f &&
    _impactMarkerPosition is Vector3 impactPosition)
        {
            DrawCube(
                impactPosition,
                new Vector3(0.09f, 0.09f, 0.09f),
                ImpactMaterial,
                view,
                projection);
        }
    }

    private void DrawCube(
        Vector3 position,
        Vector3 size,
        Material3D material,
        Matrix4x4 view,
        Matrix4x4 projection)
    {
        var model =
            Matrix4x4.CreateScale(size) *
            Matrix4x4.CreateTranslation(position);

        Runtime.Services.Graphics.Submit(
            new DrawMeshCommand(
                _cubeMesh,
                _shader,
                model,
                view,
                projection)
            {
                Material = material
            });
    }

    protected override void OnShutdown()
    {
        _context.Resized -= OnResized;

        var errors = new List<Exception>();

        void TryCleanup(Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (_meshes is not null &&
            _cubeMesh.IsValid)
        {
            TryCleanup(() => _meshes.Destroy(_cubeMesh));
            _cubeMesh = MeshHandle.Invalid;
        }

        if (_shader.IsValid)
        {
            TryCleanup(() =>
                Runtime.Services.Graphics.Shaders.Destroy(_shader));

            _shader = ShaderHandle.Invalid;
        }
        TryCleanup(() =>
    _context.Cursor.SetMode(PointerMode.Normal));
        if (errors.Count > 0)
        {
            throw new AggregateException(
                "3D arena resources could not be fully released.",
                errors);
        }
    }

    private void OnResized(int width, int height)
    {
        UpdateCameraViewport(width, height);
    }

    private void UpdateCameraViewport(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _camera?.SetViewportSize(width, height);
    }
}
