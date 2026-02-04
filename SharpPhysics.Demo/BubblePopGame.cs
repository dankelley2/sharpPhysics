#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Input;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Demo.Helpers;
using SharpPhysics.Demo.Integration;
using SharpPhysics.Demo.Settings;
using SFML.Window;
using SharpPhysics.Rendering.Shaders;
using SharpPhysics.Rendering;

namespace SharpPhysics.Demo;

/// <summary>
/// Bubble Pop game - pop floating bubbles with your hands!
/// Bubbles float up from the bottom and kids pop them by touching.
/// </summary>
public class BubblePopGame : IGame
{
    private GameEngine _engine = null!;
    private PhysicsSystem _physics = null!;
    private PersonColliderBridge? _personColliderBridge;
    private readonly Random _random = new();

    private const float SPAWN_INTERVAL = 0.3f;
    private const int MAX_BUBBLES = 100;
    private const float BUBBLE_LIFETIME = 12f;

    // Game state
    private float _spawnTimer = 0f;
    private int _score = 0;
    private int _popped = 0;
    private int _missed = 0;
    private float _gameTime = 0f;
    private int _streak = 0;
    private int _bestStreak = 0;

    // Bubble tracking
    private readonly Dictionary<PhysicsObject, BubbleInfo> _activeBubbles = new();

    // Bubble sizes with different point values
    private static readonly BubbleType[] BubbleTypes =
    [
        new("Tiny", 6, 50, 0.4f),       // Small = more points, harder to hit
        new("Small", 10, 30, 0.3f),
        new("Medium", 16, 20, 0.2f),
        new("Large", 24, 10, 0.08f),
        new("Giant", 35, 5, 0.02f),      // Giant = easy but few points
    ];

    // Special bubble types
    private const float GOLDEN_BUBBLE_CHANCE = 0.05f;
    private const float CHAIN_BUBBLE_CHANCE = 0.03f;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _physics = engine.PhysicsSystem;

        // Very light upward "gravity" for floating bubbles
        _physics.Gravity = new Vector2(0, -2f);
        _physics.GravityScale = 15f;

        InitializeWorld(engine.WindowWidth, engine.WindowHeight);

        // Enable or disable body detection
        if (GameSettings.Instance.PoseTrackingEnabled) {
            InitializePersonDetection(engine.WindowWidth, engine.WindowHeight);
        }

        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("   🫧  BUBBLE POP GAME  🫧");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Pop the bubbles with your hands!");
        Console.WriteLine("Smaller bubbles = More points!");
        Console.WriteLine("🌟 Golden bubbles = BONUS POINTS!");
        Console.WriteLine("═══════════════════════════════════════");
    }

    private void InitializeWorld(uint worldWidth, uint worldHeight)
    {
        // Create side walls only
        var wallShader = new SFMLNoneShader(); // Invisible walls
        _physics.CreateStaticBox(
            new Vector2(-50, 0), new Vector2(0, worldHeight),
            locked: true, wallShader, 1000000);
        _physics.CreateStaticBox(
            new Vector2(worldWidth, 0), new Vector2(worldWidth + 50, worldHeight),
            locked: true, wallShader, 1000000);
    }

    private void InitializePersonDetection(uint worldWidth, uint worldHeight)
    {
        var settings = GameSettings.Instance;

        try
        {
            _personColliderBridge = new PersonColliderBridge(
                physicsSystem: _physics,
                worldWidth: worldWidth,
                worldHeight: worldHeight,
                modelPath: settings.ModelPath,
                flipX: settings.FlipX,
                flipY: settings.FlipY,
                ballRadius: settings.BubblePopBallRadius,
                smoothingFactor: settings.BubblePopSmoothingFactor,
                maxPeople: settings.MaxPeople
            );

            _personColliderBridge.OnError += (s, ex) => { Console.WriteLine($"Person Detection Error: {ex.Message}"); };

            // Start detection using configured camera source
            if (settings.CameraSourceType == "url")
            {
                _personColliderBridge.Start(url: settings.CameraUrl);
            }
            else
            {
                _personColliderBridge.Start(
                    cameraIndex: settings.CameraDeviceIndex,
                    width: settings.CameraDeviceResolutionX,
                    height: settings.CameraDeviceResolutionY,
                    fps: settings.CameraDeviceFps);
            }

            Console.WriteLine("🎮 Body tracking ready - use your hands to pop bubbles!");
            Console.WriteLine($"Camera: {(settings.CameraSourceType == "url" ? settings.CameraUrl : $"Device {settings.CameraDeviceIndex}")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Body tracking not available: {ex.Message}");
            _personColliderBridge = null;
        }
    }

    public void Update(float deltaTime, InputManager inputManager)
    {
        // Check for ESC to return to menu
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Escape))
        {
            _engine.SwitchGame(new MenuGame());
            inputManager.ConsumeKeyPress(Keyboard.Key.Escape);
            return;
        }

        _gameTime += deltaTime;
        _personColliderBridge?.ProcessPendingUpdates();

        // Spawn bubbles
        _spawnTimer += deltaTime;
        float dynamicInterval = SPAWN_INTERVAL * MathF.Max(0.4f, 1f - (_gameTime / 180f));

        if (_spawnTimer >= dynamicInterval && _activeBubbles.Count < MAX_BUBBLES)
        {
            _spawnTimer = 0f;
            SpawnBubble();
        }

        // Update bubbles
        UpdateBubbles();

        // Check for pops
        CheckBubblePops();
    }

    private void SpawnBubble()
    {
        float x = _random.Next(50, (int)_engine.WindowWidth - 50);
        float y = _engine.WindowHeight + 20; // Spawn below screen

        // Weighted random selection for bubble size
        float roll = _random.NextFloat();
        float cumulative = 0f;
        BubbleType selectedType = BubbleTypes[2]; // Default medium

        foreach (var type in BubbleTypes)
        {
            cumulative += type.SpawnWeight;
            if (roll <= cumulative)
            {
                selectedType = type;
                break;
            }
        }

        // Check for special bubbles
        bool isGolden = _random.NextDouble() < GOLDEN_BUBBLE_CHANCE;
        bool isChain = !isGolden && _random.NextDouble() < CHAIN_BUBBLE_CHANCE;

        var shader = new SFMLPolyRainbowShader(); // Bubbles look nice with rainbow shader

        var bubble = _physics.CreateStaticCircle(
            new Vector2(x, y),
            selectedType.Size,
            restitution: 0.3f,
            locked: false,
            shader
        );

        // Gentle upward float with slight horizontal drift
        bubble.Velocity = new Vector2(
            (_random.NextFloat() - 0.5f) * 30f,
            -(_random.NextFloat() * 20f + 30f)
        );

        int points = selectedType.Points;
        if (isGolden) points *= 5;
        if (isChain) points *= 2;

        _activeBubbles[bubble] = new BubbleInfo
        {
            SpawnTime = _gameTime,
            Points = points,
            Size = selectedType.Size,
            IsGolden = isGolden,
            IsChain = isChain
        };
    }

    private void UpdateBubbles()
    {
        var toRemove = new List<PhysicsObject>();

        foreach (var (bubble, info) in _activeBubbles)
        {
            // Add gentle floating motion
            float wobble = MathF.Sin(_gameTime * 3f + info.SpawnTime) * 0.5f;
            bubble.Velocity = new Vector2(
                bubble.Velocity.X * 0.99f + wobble,
                bubble.Velocity.Y
            );

            // Remove if floated off top or too old
            bool offScreen = bubble.Center.Y < -50;
            bool tooOld = _gameTime - info.SpawnTime > BUBBLE_LIFETIME;

            if (offScreen || tooOld)
            {
                toRemove.Add(bubble);
                _missed++;
                _streak = 0; // Reset streak on miss
            }
        }

        foreach (var bubble in toRemove)
        {
            _activeBubbles.Remove(bubble);
            _physics.DestroyObject(bubble);
        }
    }

    private void CheckBubblePops()
    {
        if (_personColliderBridge == null) return;

        var trackingBalls = _personColliderBridge.GetTrackingBalls();
        var poppedBubbles = new List<PhysicsObject>();
        var chainSpawns = new List<Vector2>();

        foreach (var (bubble, info) in _activeBubbles)
        {
            foreach (var tracker in trackingBalls)
            {
                float distance = Vector2.Distance(bubble.Center, tracker.Center);
                float popRadius = info.Size + 25f; // Generous pop radius

                if (distance < popRadius)
                {
                    // POP!
                    _score += info.Points;
                    _popped++;
                    _streak++;
                    if (_streak > _bestStreak) _bestStreak = _streak;

                    // Chain bubbles spawn more bubbles when popped
                    if (info.IsChain)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            chainSpawns.Add(bubble.Center);
                        }
                    }

                    // Console feedback for special pops
                    if (info.IsGolden)
                    {
                        Console.WriteLine($"GOLDEN BUBBLE! +{info.Points}");
                    }
                    else if (info.IsChain)
                    {
                        Console.WriteLine($"CHAIN REACTION!");
                    }

                    poppedBubbles.Add(bubble);
                    break;
                }
            }
        }

        foreach (var bubble in poppedBubbles)
        {
            _activeBubbles.Remove(bubble);
            _physics.DestroyObject(bubble);
        }

        // Spawn chain bubbles
        foreach (var pos in chainSpawns)
        {
            SpawnChainBubble(pos);
        }
    }

    private void SpawnChainBubble(Vector2 position)
    {
        var shader = new SFMLPolyRainbowShader();
        var bubble = _physics.CreateStaticCircle(
            position + new Vector2((_random.NextFloat() - 0.5f) * 30, 0),
            8,
            restitution: 0.3f,
            locked: false,
            shader
        );

        bubble.Velocity = new Vector2(
            (_random.NextFloat() - 0.5f) * 60f,
            -(_random.NextFloat() * 30f + 20f)
        );

        _activeBubbles[bubble] = new BubbleInfo
        {
            SpawnTime = _gameTime,
            Points = 15,
            Size = 8,
            IsGolden = false,
            IsChain = false
        };
    }

    public void Render(Renderer renderer)
    {
        // Draw skeleton overlay
        SkeletonRenderer.DrawSkeleton(renderer, _personColliderBridge);

        // Score display
        renderer.DrawText($"SCORE: {_score:N0}", _engine.WindowWidth - 250, 30, 32,
            SFML.Graphics.Color.White);

        // Streak display
        if (_streak > 2)
        {
            var streakColor = _streak switch
            {
                >= 20 => new SFML.Graphics.Color(255, 50, 255),  // Purple for amazing
                >= 10 => new SFML.Graphics.Color(255, 215, 0),   // Gold
                >= 5 => new SFML.Graphics.Color(100, 255, 255),  // Cyan
                _ => new SFML.Graphics.Color(100, 255, 100)       // Green
            };
            renderer.DrawText($"🔥 {_streak} STREAK!", _engine.WindowWidth - 250, 70, 24, streakColor);
        }

        // Stats (bottom)
        renderer.DrawText($"Popped: {_popped}  |  Missed: {_missed}  |  Best Streak: {_bestStreak}",
            20, _engine.WindowHeight - 40, 16, new SFML.Graphics.Color(150, 150, 150));

        renderer.DrawText($"Bubbles: {_activeBubbles.Count}",
            20, _engine.WindowHeight - 65, 16, new SFML.Graphics.Color(150, 150, 150));

        // Accuracy percentage
        if (_popped + _missed > 0)
        {
            float accuracy = (float)_popped / (_popped + _missed) * 100f;
            var accColor = accuracy switch
            {
                >= 90 => new SFML.Graphics.Color(100, 255, 100),
                >= 70 => new SFML.Graphics.Color(255, 255, 100),
                _ => new SFML.Graphics.Color(255, 100, 100)
            };
            renderer.DrawText($"Accuracy: {accuracy:F1}%", _engine.WindowWidth - 200, _engine.WindowHeight - 40,
                16, accColor);
        }
    }

    public void Shutdown()
    {
        Console.WriteLine($"\n🫧 GAME OVER!");
        Console.WriteLine($"Final Score: {_score:N0}");
        Console.WriteLine($"Bubbles Popped: {_popped}");
        Console.WriteLine($"Best Streak: {_bestStreak}");
        _personColliderBridge?.Dispose();
    }

    private record BubbleType(string Name, int Size, int Points, float SpawnWeight);

    private class BubbleInfo
    {
        public float SpawnTime { get; init; }
        public int Points { get; init; }
        public int Size { get; init; }
        public bool IsGolden { get; init; }
        public bool IsChain { get; init; }
    }
}
