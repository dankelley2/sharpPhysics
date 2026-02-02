#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Classes.ObjectTemplates;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Input;
using SharpPhysics.Engine.Objects;
using SFML.Window;
using SharpPhysics.Demo.Helpers;
using SharpPhysics.Demo.Integration;
using SharpPhysics.Demo.Settings;
using SharpPhysics.Rendering.Shaders;
using SharpPhysics.Engine.Player;
using SharpPhysics.Rendering;

namespace SharpPhysics.Demo;

/// <summary>
/// A fun rain catcher game designed for kids on a projector.
/// Balls rain from the sky and players use their bodies to interact!
/// </summary>
public class RainCatcherGame : IGame
{
    private GameEngine _engine = null!;
    private PhysicsSystem _physics = null!;
    private ObjectTemplates _objectTemplates = null!;
    private PersonColliderBridge? _personColliderBridge;
    private readonly Random _random = new();

    // Game configuration
    private const float SPAWN_INTERVAL = 0.15f;      // Time between ball spawns
    private const float BALL_LIFETIME = 8f;          // Seconds before balls are removed
    private const int MAX_BALLS = 200;               // Maximum balls on screen
    private const float POWERUP_CHANCE = 0.08f;      // 8% chance for power-up ball

    // Game state
    private float _spawnTimer = 0f;
    private int _score = 0;
    private int _combo = 0;
    private float _comboTimer = 0f;
    private float _gameTime = 0f;
    private bool _rainbowMode = false;
    private float _gravityPulseTimer = 0f;
    private bool _isGravityReversed = false;

    // Ball tracking for lifetime management
    private readonly Dictionary<PhysicsObject, BallInfo> _activeBalls = new();

    // Ball types with colors and point values
    private static readonly BallType[] BallTypes =
    [
        new("Red", new SFML.Graphics.Color(255, 80, 80), 10, 8),
        new("Orange", new SFML.Graphics.Color(255, 165, 0), 15, 9),
        new("Yellow", new SFML.Graphics.Color(255, 255, 100), 20, 10),
        new("Green", new SFML.Graphics.Color(100, 255, 100), 25, 11),
        new("Blue", new SFML.Graphics.Color(100, 150, 255), 30, 12),
        new("Purple", new SFML.Graphics.Color(180, 100, 255), 50, 14),
    ];

    // Power-up types
    private static readonly PowerUpType[] PowerUpTypes =
    [
        new("Rainbow", new SFML.Graphics.Color(255, 255, 255), "🌈 RAINBOW MODE!", 5f),
        new("BigBalls", new SFML.Graphics.Color(255, 200, 100), "⭐ BIG BALLS!", 4f),
        new("SlowMo", new SFML.Graphics.Color(100, 200, 255), "🐢 SLOW MOTION!", 3f),
        new("GravityFlip", new SFML.Graphics.Color(255, 100, 200), "🔄 GRAVITY FLIP!", 4f),
        new("ScoreBoost", new SFML.Graphics.Color(255, 215, 0), "💰 2X POINTS!", 5f),
    ];

    private string? _activePowerUp = null;
    private float _powerUpTimer = 0f;
    private int _scoreMultiplier = 1;
    private float _ballSizeMultiplier = 1f;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _physics = engine.PhysicsSystem;
        _objectTemplates = new ObjectTemplates(_physics);

        // Reduce gravity for floaty fun feel
        _physics.Gravity = new Vector2(0, 6f);
        _physics.GravityScale = 25f;

        InitializeWorld(engine.WindowWidth, engine.WindowHeight);

        // Enable or disable body detection
        if (GameSettings.Instance.PoseTrackingEnabled) {
            InitializePersonDetection(engine.WindowWidth, engine.WindowHeight);
        }

        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("   🌧️  RAIN CATCHER GAME  🌧️");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Use your body to catch the falling balls!");
        Console.WriteLine("Different colors = Different points!");
        Console.WriteLine("Catch power-ups for special effects!");
        Console.WriteLine("═══════════════════════════════════════");
    }

    private void InitializeWorld(uint worldWidth, uint worldHeight)
    {
        // Create walls (sides only - no floor so balls fall through)
        _objectTemplates.CreateWall(new Vector2(0, 0), 15, (int)worldHeight);
        _objectTemplates.CreateWall(new Vector2((int)worldWidth - 15, 0), 15, (int)worldHeight);

        // Top wall to spawn balls from
        _objectTemplates.CreateWall(new Vector2(0, 0), (int)worldWidth, 15);

        // Create some fun bouncy platforms at different heights
        CreatePlatform(worldWidth * 0.2f, worldHeight * 0.7f, 150, 20);
        CreatePlatform(worldWidth * 0.7f, worldHeight * 0.7f, 150, 20);
        CreatePlatform(worldWidth * 0.45f, worldHeight * 0.5f, 200, 20);

        // Add some angled ramps for extra fun
        CreateAngledRamp(worldWidth * 0.1f, worldHeight * 0.4f, 120, 15, 0.3f);
        CreateAngledRamp(worldWidth * 0.8f, worldHeight * 0.4f, 120, 15, -0.3f);
    }

    private void CreatePlatform(float x, float y, int width, int height)
    {
        var platform = _physics.CreateStaticBox(
            new Vector2(x, y),
            new Vector2(x + width, y + height),
            locked: true,
            new SFMLWallShader(),
            mass: 1000000
        );
        platform.Restitution = 0.6f; // Bouncy!
    }

    private void CreateAngledRamp(float x, float y, int width, int height, float angle)
    {
        // Create a simple angled platform using a box (rotation would need polygon)
        var ramp = _physics.CreateStaticBox(
            new Vector2(x, y),
            new Vector2(x + width, y + height),
            locked: true,
            new SFMLWallShader(),
            mass: 1000000
        );
        ramp.Angle = angle;
        ramp.Restitution = 0.7f;
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
                ballRadius: settings.RainCatcherBallRadius,
                smoothingFactor: settings.RainCatcherSmoothingFactor,
                maxPeople: settings.MaxPeople
            );

            _personColliderBridge.OnError += (s, ex) =>
            {
                Console.WriteLine($"Person Detection Error: {ex.Message}");
            };

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

            Console.WriteLine("🎮 Body tracking initialized!");
            Console.WriteLine($"Camera: {(settings.CameraSourceType == "url" ? settings.CameraUrl : $"Device {settings.CameraDeviceIndex}")}");
            Console.WriteLine("👋 Wave your hands to catch balls!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Body tracking not available: {ex.Message}");
            Console.WriteLine("Game will run without body tracking.");
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

        // Process person detection
        _personColliderBridge?.ProcessPendingUpdates();

        // Update timers
        UpdateTimers(deltaTime);

        // Spawn new balls
        UpdateBallSpawning(deltaTime);

        // Update and cleanup balls
        UpdateBalls(deltaTime);

        // Check for scoring (balls hitting player)
        CheckScoring();
    }

    private void UpdateTimers(float deltaTime)
    {
        // Combo timer
        if (_combo > 0)
        {
            _comboTimer -= deltaTime;
            if (_comboTimer <= 0)
            {
                _combo = 0;
            }
        }

        // Power-up timer
        if (_activePowerUp != null)
        {
            _powerUpTimer -= deltaTime;
            if (_powerUpTimer <= 0)
            {
                DeactivatePowerUp();
            }
        }

        // Gravity pulse effect
        _gravityPulseTimer += deltaTime;
        if (_isGravityReversed)
        {
            // Subtle gravity variation for fun
            float pulse = MathF.Sin(_gravityPulseTimer * 2f) * 0.2f;
            _physics.Gravity = new Vector2(0, -6f * (1f + pulse));
        }
    }

    private void UpdateBallSpawning(float deltaTime)
    {
        _spawnTimer += deltaTime;

        // Dynamic spawn rate - gets faster as game progresses
        float dynamicInterval = SPAWN_INTERVAL * MathF.Max(0.5f, 1f - (_gameTime / 120f));

        if (_spawnTimer >= dynamicInterval && _activeBalls.Count < MAX_BALLS)
        {
            _spawnTimer = 0f;
            SpawnBall();
        }
    }

    private void SpawnBall()
    {
        float x = _random.Next(50, (int)_engine.WindowWidth - 50);
        float y = _isGravityReversed ? _engine.WindowHeight - 30 : 30;

        // Determine if this is a power-up
        bool isPowerUp = _random.NextDouble() < POWERUP_CHANCE;

        PhysicsObject ball;
        BallInfo info;

        if (isPowerUp)
        {
            // Spawn power-up ball
            var powerUpType = PowerUpTypes[_random.Next(PowerUpTypes.Length)];
            int size = (int)(18 * _ballSizeMultiplier);

            var shader = new SFMLBallShader();
            ball = _physics.CreateStaticCircle(
                new Vector2(x, y),
                size,
                restitution: 0.85f,
                locked: false,
                shader
            );

            info = new BallInfo
            {
                SpawnTime = _gameTime,
                IsPowerUp = true,
                PowerUpName = powerUpType.Name,
                Points = 100,
                BaseColor = powerUpType.Color
            };
        }
        else
        {
            // Spawn regular ball
            var ballType = BallTypes[_random.Next(BallTypes.Length)];
            int size = (int)(ballType.Size * _ballSizeMultiplier);

            SFMLShader shader = _rainbowMode
                ? new SFMLPolyRainbowShader()
                : new SFMLBallShader();

            ball = _physics.CreateStaticCircle(
                new Vector2(x, y),
                size,
                restitution: 0.75f,
                locked: false,
                shader
            );

            info = new BallInfo
            {
                SpawnTime = _gameTime,
                IsPowerUp = false,
                Points = ballType.Points,
                BaseColor = ballType.Color
            };
        }

        // Add slight random horizontal velocity for variety
        ball.Velocity = new Vector2(
            (_random.NextFloat() - 0.5f) * 50f,
            _isGravityReversed ? -30f : 30f
        );

        _activeBalls[ball] = info;
    }

    private void UpdateBalls(float deltaTime)
    {
        var ballsToRemove = new List<PhysicsObject>();

        foreach (var (ball, info) in _activeBalls)
        {
            float age = _gameTime - info.SpawnTime;

            // Remove old balls or balls that fell off screen
            bool tooOld = age > BALL_LIFETIME;
            bool offScreen = _isGravityReversed
                ? ball.Center.Y < -50
                : ball.Center.Y > _engine.WindowHeight + 50;

            if (tooOld || offScreen)
            {
                ballsToRemove.Add(ball);
            }
        }

        foreach (var ball in ballsToRemove)
        {
            _activeBalls.Remove(ball);
            _physics.RemovalQueue.Enqueue(ball);
        }
    }

    private void CheckScoring()
    {
        if (_personColliderBridge == null) return;

        var trackingBalls = _personColliderBridge.GetTrackingBalls();
        var scoredBalls = new List<PhysicsObject>();

        foreach (var (ball, info) in _activeBalls)
        {
            foreach (var tracker in trackingBalls)
            {
                float distance = Vector2.Distance(ball.Center, tracker.Center);
                float catchRadius = 50f; // Generous catch radius for kids

                if (distance < catchRadius)
                {
                    if (info.IsPowerUp && info.PowerUpName != null)
                    {
                        ActivatePowerUp(info.PowerUpName);
                    }

                    // Score points
                    int points = info.Points * _scoreMultiplier;
                    _combo++;
                    _comboTimer = 2f;

                    // Combo bonus
                    if (_combo > 1)
                    {
                        points = (int)(points * (1f + _combo * 0.1f));
                    }

                    _score += points;

                    // Visual feedback - console for now
                    if (_combo > 3)
                    {
                        Console.WriteLine($"🔥 {_combo}x COMBO! +{points} (Total: {_score})");
                    }

                    scoredBalls.Add(ball);
                    break;
                }
            }
        }

        foreach (var ball in scoredBalls)
        {
            _activeBalls.Remove(ball);
            _physics.RemovalQueue.Enqueue(ball);
        }
    }

    private void ActivatePowerUp(string powerUpName)
    {
        var powerUp = PowerUpTypes.FirstOrDefault(p => p.Name == powerUpName);
        if (powerUp == null) return;

        Console.WriteLine($"⚡ {powerUp.Message}");

        _activePowerUp = powerUpName;
        _powerUpTimer = powerUp.Duration;

        switch (powerUpName)
        {
            case "Rainbow":
                _rainbowMode = true;
                break;
            case "BigBalls":
                _ballSizeMultiplier = 1.8f;
                break;
            case "SlowMo":
                _physics.TimeScale = 0.5f;
                break;
            case "GravityFlip":
                _isGravityReversed = !_isGravityReversed;
                _physics.Gravity = new Vector2(0, _isGravityReversed ? -6f : 6f);
                break;
            case "ScoreBoost":
                _scoreMultiplier = 2;
                break;
        }
    }

    private void DeactivatePowerUp()
    {
        if (_activePowerUp == null) return;

        Console.WriteLine($"Power-up ended: {_activePowerUp}");

        switch (_activePowerUp)
        {
            case "Rainbow":
                _rainbowMode = false;
                break;
            case "BigBalls":
                _ballSizeMultiplier = 1f;
                break;
            case "SlowMo":
                _physics.TimeScale = 1f;
                break;
            case "GravityFlip":
                _isGravityReversed = false;
                _physics.Gravity = new Vector2(0, 6f);
                break;
            case "ScoreBoost":
                _scoreMultiplier = 1;
                break;
        }

        _activePowerUp = null;
    }

    public void Render(Renderer renderer)
    {
        // Draw skeleton overlay
        SkeletonRenderer.DrawSkeleton(renderer, _personColliderBridge);

        // Draw score (top right)
        string scoreText = $"SCORE: {_score:N0}";
        renderer.DrawText(scoreText, _engine.WindowWidth - 250, 30, 32, SFML.Graphics.Color.White);

        // Draw combo if active
        if (_combo > 1)
        {
            var comboColor = _combo switch
            {
                >= 10 => new SFML.Graphics.Color(255, 50, 50),   // Red for high combo
                >= 5 => new SFML.Graphics.Color(255, 200, 50),   // Gold
                _ => new SFML.Graphics.Color(100, 255, 100)       // Green
            };
            renderer.DrawText($"{_combo}x COMBO!", _engine.WindowWidth - 250, 70, 28, comboColor);
        }

        // Draw active power-up
        if (_activePowerUp != null)
        {
            var powerUp = PowerUpTypes.FirstOrDefault(p => p.Name == _activePowerUp);
            if (powerUp != null)
            {
                string powerUpText = $"⚡ {powerUp.Message}";
                renderer.DrawText(powerUpText, _engine.WindowWidth / 2 - 150, 30, 28, powerUp.Color);

                // Power-up timer bar
                float barWidth = 200f * (_powerUpTimer / powerUp.Duration);
                renderer.DrawText($"[{"".PadRight((int)(barWidth / 10), '█')}]",
                    _engine.WindowWidth / 2 - 100, 65, 18, powerUp.Color);
            }
        }

        // Draw ball count (bottom left for debug)
        renderer.DrawText($"Balls: {_activeBalls.Count}/{MAX_BALLS}", 20, _engine.WindowHeight - 40, 16,
            new SFML.Graphics.Color(150, 150, 150));

        // Draw game time
        int minutes = (int)(_gameTime / 60);
        int seconds = (int)(_gameTime % 60);
        renderer.DrawText($"Time: {minutes:D2}:{seconds:D2}", 20, _engine.WindowHeight - 65, 16,
            new SFML.Graphics.Color(150, 150, 150));
    }

    public void Shutdown()
    {
        Console.WriteLine($"\n🎮 GAME OVER! Final Score: {_score}");
        _personColliderBridge?.Dispose();
    }

    // Helper records
    private record BallType(string Name, SFML.Graphics.Color Color, int Points, int Size);
    private record PowerUpType(string Name, SFML.Graphics.Color Color, string Message, float Duration);

    private class BallInfo
    {
        public float SpawnTime { get; init; }
        public bool IsPowerUp { get; init; }
        public string? PowerUpName { get; init; }
        public int Points { get; init; }
        public SFML.Graphics.Color BaseColor { get; init; }
    }
}

// Extension method for Random
public static class RandomExtensions
{
    public static float NextFloat(this Random random) => (float)random.NextDouble();
}
