#nullable enable
using System.Numerics;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Objects;
using SharpPhysics.Engine.Player;
using SFML.Graphics;
using SFML.Window;
using SharpPhysics.Engine.Core;
using SharpPhysics.Rendering.Shaders;
using SharpPhysics.Rendering;

namespace SharpPhysics.Demo;

/// <summary>
/// Action-packed single-screen platformer game!
/// Collect coins, avoid enemies, reach the goal!
/// Uses keyboard controls with the PlayerController.
/// </summary>
public class PlatformerGame : IGame
{
    private GameEngine _engine = null!;
    private PhysicsSystem _physics = null!;
    private ObjectTemplates _objectTemplates = null!;
    private PlayerController _playerController = null!;
    private PhysicsObject _player = null!;

    // Game state
    private int _score = 0;
    private int _coinsCollected = 0;
    private int _lives = 3;
    private float _gameTime = 0f;
    private bool _levelComplete = false;
    private bool _gameOver = false;
    private float _messageTimer = 0f;
    private string _message = "";

    // Collectibles and hazards
    private readonly List<PhysicsObject> _coins = new();
    private readonly List<PhysicsObject> _hazards = new();
    private readonly List<(PhysicsObject Enemy, float Direction, float MinX, float MaxX)> _enemies = new();
    private PhysicsObject? _goalFlag = null;

    // Spawn point
    private Vector2 _spawnPoint;

    // Visual effects
    private readonly List<(Vector2 Position, float Timer, string Text, Color Color)> _floatingTexts = new();
    private readonly Random _random = new();

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _physics = engine.PhysicsSystem;
        _objectTemplates = new ObjectTemplates(_physics);

        // Normal gravity for platformer
        _physics.Gravity = new Vector2(0, 9.8f);
        _physics.GravityScale = 35f;

        CreateLevel();
        SpawnPlayer();

        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("   🎮  PLATFORMER DEMO  🎮");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Controls:");
        Console.WriteLine("  ← → : Move left/right");
        Console.WriteLine("  ↑   : Jump");
        Console.WriteLine("  ↓   : Ground slam");
        Console.WriteLine("");
        Console.WriteLine("Collect coins, avoid enemies, reach the flag!");
        Console.WriteLine("═══════════════════════════════════════");
    }

    private void CreateLevel()
    {
        uint w = _engine.WindowWidth;
        uint h = _engine.WindowHeight;

        // Walls (boundaries)
        _objectTemplates.CreateWall(new Vector2(0, 0), 15, (int)h);           // Left wall
        _objectTemplates.CreateWall(new Vector2(w - 15, 0), 15, (int)h);      // Right wall
        _objectTemplates.CreateWall(new Vector2(0, 0), (int)w, 15);           // Ceiling

        // Ground floor
        CreatePlatform(0, h - 40, w, 40);

        // Platform layout - creates an interesting level
        // Lower platforms
        CreatePlatform(100, h - 150, 200, 20);
        CreatePlatform(400, h - 150, 250, 20);
        CreatePlatform(750, h - 150, 200, 20);
        CreatePlatform(1050, h - 150, 180, 20);

        // Middle platforms
        CreatePlatform(50, h - 280, 150, 20);
        CreatePlatform(280, h - 300, 180, 20);
        CreatePlatform(550, h - 280, 200, 20);
        CreatePlatform(850, h - 320, 150, 20);
        CreatePlatform(1080, h - 280, 150, 20);

        // Upper platforms
        CreatePlatform(150, h - 420, 180, 20);
        CreatePlatform(420, h - 450, 200, 20);
        CreatePlatform(720, h - 430, 180, 20);
        CreatePlatform(980, h - 470, 200, 20);

        // Top platforms (near goal)
        CreatePlatform(250, h - 560, 150, 20);
        CreatePlatform(500, h - 580, 200, 20);
        CreatePlatform(800, h - 560, 150, 20);

        // Goal platform
        CreatePlatform(1050, h - 620, 180, 25);

        // Spawn point (bottom left area)
        _spawnPoint = new Vector2(150, h - 200);

        // Add coins throughout the level
        AddCoin(200, h - 200);
        AddCoin(500, h - 200);
        AddCoin(850, h - 200);
        AddCoin(1100, h - 200);

        AddCoin(120, h - 330);
        AddCoin(350, h - 350);
        AddCoin(650, h - 330);
        AddCoin(920, h - 370);
        AddCoin(1150, h - 330);

        AddCoin(230, h - 470);
        AddCoin(510, h - 500);
        AddCoin(800, h - 480);
        AddCoin(1070, h - 520);

        AddCoin(320, h - 610);
        AddCoin(590, h - 630);
        AddCoin(870, h - 610);

        // Add enemies (moving hazards)
        AddEnemy(300, h - 170, 250, 550);
        AddEnemy(600, h - 300, 560, 740);
        AddEnemy(900, h - 450, 730, 890);
        AddEnemy(350, h - 600, 260, 390);

        // Add static hazards (spikes)
        AddHazard(180, h - 60, 80, 20);
        AddHazard(700, h - 60, 100, 20);
        AddHazard(950, h - 60, 80, 20);

        // Goal flag at top right
        CreateGoalFlag(1130, h - 680);
    }

    private void CreatePlatform(float x, float y, float width, float height)
    {
        var shader = new SFMLWallShader();
        var platform = _physics.CreateStaticBox(
            new Vector2(x, y),
            new Vector2(x + width, y + height),
            locked: true,
            shader,
            mass: 1000000
        );
        platform.Restitution = 0.1f;
    }

    private void AddCoin(float x, float y)
    {
        var shader = new SFMLPolyRainbowShader();
        var coin = _physics.CreateStaticCircle(
            new Vector2(x, y),
            12,
            restitution: 0.5f,
            locked: true,
            shader
        );
        // Coins use distance-based collection, not physics collision
        _coins.Add(coin);
    }

    private void AddEnemy(float x, float y, float minX, float maxX)
    {
        var shader = new SFMLBallShader();
        var enemy = _physics.CreateStaticCircle(
            new Vector2(x, y),
            18,
            restitution: 0.8f,
            locked: true,
            shader
        );
        // Enemies use distance-based collision detection
        float direction = _random.NextSingle() > 0.5f ? 1f : -1f;
        _enemies.Add((enemy, direction, minX, maxX));
    }

    private void AddHazard(float x, float y, float width, float height)
    {
        var shader = new SFMLPolyShader();
        var hazard = _physics.CreateStaticBox(
            new Vector2(x, y),
            new Vector2(x + width, y + height),
            locked: true,
            shader,
            mass: 1000000
        );
        hazard.Restitution = 0.1f;
        _hazards.Add(hazard);
    }

    private void CreateGoalFlag(float x, float y)
    {
        var shader = new SFMLPolyRainbowShader();
        _goalFlag = _physics.CreateStaticCircle(
            new Vector2(x, y),
            25,
            restitution: 0.5f,
            locked: true,
            shader
        );
        // Goal uses distance-based detection, not physics collision
    }

    private void SpawnPlayer()
    {
        if (_player != null)
        {
            _physics.RemovalQueue.Enqueue(_player);
        }

        var shader = new SFMLPolyShader();
        _player = _physics.CreatePolygon(
            _spawnPoint,
            CreateCapsuleVertices(16, 15, 35),
            shader,
            canRotate: false
        );
        _player.Restitution = 0.0f;

        _playerController = new PlayerController(_player);
    }

    private Vector2[] CreateCapsuleVertices(int segments, float radius, float height)
    {
        var vertices = new List<Vector2>();
        float halfHeight = height / 2f;

        // Top semicircle
        for (int i = 0; i <= segments / 2; i++)
        {
            float angle = MathF.PI * i / (segments / 2);
            float x = radius * MathF.Cos(angle);
            float y = -halfHeight - radius * MathF.Sin(angle);
            vertices.Add(new Vector2(x, y));
        }

        // Bottom semicircle
        for (int i = 0; i <= segments / 2; i++)
        {
            float angle = MathF.PI + MathF.PI * i / (segments / 2);
            float x = radius * MathF.Cos(angle);
            float y = halfHeight - radius * MathF.Sin(angle);
            vertices.Add(new Vector2(x, y));
        }

        return vertices.ToArray();
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

        if (_gameOver || _levelComplete)
        {
            _messageTimer -= deltaTime;
            if (_messageTimer <= 0 && inputManager.IsKeyPressedBuffered(Keyboard.Key.Space))
            {
                if (_gameOver)
                {
                    // Restart game
                    _gameOver = false;
                    _lives = 3;
                    _score = 0;
                    _coinsCollected = 0;
                    _engine.SwitchGame(new PlatformerGame());
                }
                else
                {
                    // Return to menu on win
                    _engine.SwitchGame(new MenuGame());
                }
                inputManager.ConsumeKeyPress(Keyboard.Key.Space);
            }
            return;
        }

        _gameTime += deltaTime;

        // Update player
        _playerController.Update(inputManager);

        // Update enemies (patrol movement)
        UpdateEnemies(deltaTime);

        // Check collisions
        CheckCoinCollection();
        CheckHazardCollision();
        CheckEnemyCollision();
        CheckGoalReached();
        CheckFallDeath();

        // Update floating texts
        UpdateFloatingTexts(deltaTime);
    }

    private void UpdateEnemies(float deltaTime)
    {
        float enemySpeed = 80f;

        for (int i = 0; i < _enemies.Count; i++)
        {
            var (enemy, direction, minX, maxX) = _enemies[i];
            
            float newX = enemy.Center.X + direction * enemySpeed * deltaTime;
            
            // Reverse direction at boundaries
            if (newX <= minX || newX >= maxX)
            {
                direction = -direction;
                newX = Math.Clamp(newX, minX, maxX);
            }

            // Move enemy
            float deltaX = newX - enemy.Center.X;
            enemy.Move(new Vector2(deltaX, 0));
            
            _enemies[i] = (enemy, direction, minX, maxX);
        }
    }

    private void CheckCoinCollection()
    {
        var playerCenter = _player.Center;
        float collectRadius = 35f;

        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            var coin = _coins[i];
            float distance = Vector2.Distance(playerCenter, coin.Center);
            
            if (distance < collectRadius)
            {
                // Collect coin!
                _physics.RemovalQueue.Enqueue(coin);
                _coins.RemoveAt(i);
                
                _coinsCollected++;
                int points = 100;
                _score += points;
                
                AddFloatingText(coin.Center, $"+{points}", new Color(255, 215, 0));
                Console.WriteLine($"🪙 Coin collected! Score: {_score}");
            }
        }
    }

    private void CheckHazardCollision()
    {
        var playerCenter = _player.Center;
        float hitRadius = 25f;

        foreach (var hazard in _hazards)
        {
            // Simple AABB check for hazards
            var hazardCenter = hazard.Center;
            float distance = Vector2.Distance(playerCenter, hazardCenter);
            
            if (distance < hitRadius + 40)
            {
                PlayerDied("Spike!");
                return;
            }
        }
    }

    private void CheckEnemyCollision()
    {
        var playerCenter = _player.Center;
        float hitRadius = 30f;

        foreach (var (enemy, _, _, _) in _enemies)
        {
            float distance = Vector2.Distance(playerCenter, enemy.Center);
            
            if (distance < hitRadius + 18)
            {
                // Check if player is above enemy (stomping)
                if (playerCenter.Y < enemy.Center.Y - 10 && _player.Velocity.Y > 0)
                {
                    // Stomp the enemy for points!
                    _score += 200;
                    AddFloatingText(enemy.Center, "+200 STOMP!", new Color(255, 100, 100));
                    _player.Velocity = new Vector2(_player.Velocity.X, -200); // Bounce
                    Console.WriteLine("👟 Enemy stomped!");
                }
                else
                {
                    PlayerDied("Enemy!");
                    return;
                }
            }
        }
    }

    private void CheckGoalReached()
    {
        if (_goalFlag == null) return;

        var playerCenter = _player.Center;
        float distance = Vector2.Distance(playerCenter, _goalFlag.Center);

        if (distance < 50f)
        {
            // Level complete!
            _levelComplete = true;
            int timeBonus = Math.Max(0, 5000 - (int)(_gameTime * 100));
            int coinBonus = _coinsCollected * 50;
            _score += timeBonus + coinBonus;

            _message = $"LEVEL COMPLETE!\n\nCoins: {_coinsCollected}\nTime Bonus: {timeBonus}\nTotal Score: {_score}\n\nPress SPACE to continue";
            _messageTimer = 1f;

            Console.WriteLine($"🏁 Level Complete! Final Score: {_score}");
        }
    }

    private void CheckFallDeath()
    {
        if (_player.Center.Y > _engine.WindowHeight + 100)
        {
            PlayerDied("Fell!");
        }
    }

    private void PlayerDied(string cause)
    {
        _lives--;
        AddFloatingText(_player.Center, $"-1 LIFE ({cause})", new Color(255, 50, 50));
        Console.WriteLine($"💀 Player died: {cause} (Lives remaining: {_lives})");

        if (_lives <= 0)
        {
            _gameOver = true;
            _message = $"GAME OVER\n\nFinal Score: {_score}\nCoins: {_coinsCollected}\n\nPress SPACE to restart";
            _messageTimer = 1f;
        }
        else
        {
            // Respawn
            _player.Move(_spawnPoint - _player.Center);
            _player.Velocity = Vector2.Zero;
        }
    }

    private void AddFloatingText(Vector2 position, string text, Color color)
    {
        _floatingTexts.Add((position, 1.5f, text, color));
    }

    private void UpdateFloatingTexts(float deltaTime)
    {
        for (int i = _floatingTexts.Count - 1; i >= 0; i--)
        {
            var (pos, timer, text, color) = _floatingTexts[i];
            timer -= deltaTime;
            pos.Y -= 50 * deltaTime; // Float upward

            if (timer <= 0)
            {
                _floatingTexts.RemoveAt(i);
            }
            else
            {
                _floatingTexts[i] = (pos, timer, text, color);
            }
        }
    }

    public void Render(Renderer renderer)
    {
        // Draw coins with glow effect (they spin via rainbow shader)
        foreach (var coin in _coins)
        {
            renderer.DrawCircle(coin.Center, 14, new Color(255, 215, 0, 100), new Color(255, 255, 100), 2);
        }

        // Draw enemies with menacing glow
        foreach (var (enemy, _, _, _) in _enemies)
        {
            renderer.DrawCircle(enemy.Center, 22, new Color(255, 0, 0, 80), new Color(255, 50, 50), 3);
        }

        // Draw hazards (spikes indicator)
        foreach (var hazard in _hazards)
        {
            renderer.DrawRectangle(
                hazard.Center - new Vector2(45, 15),
                new Vector2(90, 30),
                new Color(255, 50, 50, 100),
                new Color(255, 0, 0),
                2
            );
        }

        // Draw goal flag with pulsing effect
        if (_goalFlag != null)
        {
            float pulse = MathF.Sin(_gameTime * 4) * 0.3f + 0.7f;
            byte alpha = (byte)(pulse * 255);
            renderer.DrawCircle(_goalFlag.Center, 30, new Color(0, 255, 100, alpha), new Color(255, 255, 255), 3);
        }

        // Draw floating texts
        foreach (var (pos, timer, text, color) in _floatingTexts)
        {
            byte alpha = (byte)(255 * Math.Min(1, timer));
            var fadeColor = new Color(color.R, color.G, color.B, alpha);
            renderer.DrawText(text, pos.X - 30, pos.Y, 18, fadeColor);
        }

        // Draw HUD
        DrawHUD(renderer);

        // Draw game over / level complete message
        if (_gameOver || _levelComplete)
        {
            DrawCenterMessage(renderer);
        }
    }

    private void DrawHUD(Renderer renderer)
    {
        // Score (top left)
        renderer.DrawText($"SCORE: {_score}", 30, 30, 28, Color.White);

        // Coins collected
        renderer.DrawText($"Coins: {_coinsCollected}/{_coins.Count + _coinsCollected}", 30, 65, 22, new Color(255, 215, 0));

        // Lives (top left under coins)
        string livesText = "Lives: " + string.Concat(Enumerable.Repeat("* ", _lives)) + string.Concat(Enumerable.Repeat("- ", 3 - _lives));
        renderer.DrawText(livesText, 30, 95, 22, new Color(255, 100, 100));

        // Time
        int minutes = (int)(_gameTime / 60);
        int seconds = (int)(_gameTime % 60);
        renderer.DrawText($"TIME: {minutes:D2}:{seconds:D2}", _engine.WindowWidth - 180, 30, 22, new Color(200, 200, 200));

        // Controls hint
        renderer.DrawText("Arrow Keys: Move/Jump/Slam  |  ESC: Menu", 
            _engine.WindowWidth / 2 - 180, _engine.WindowHeight - 30, 14, new Color(150, 150, 150));
    }

    private void DrawCenterMessage(Renderer renderer)
    {
        // Semi-transparent overlay
        renderer.DrawRectangle(
            new Vector2(0, 0),
            new Vector2(_engine.WindowWidth, _engine.WindowHeight),
            new Color(0, 0, 0, 180)
        );

        // Message box
        float boxWidth = 400;
        float boxHeight = 250;
        float boxX = (_engine.WindowWidth - boxWidth) / 2;
        float boxY = (_engine.WindowHeight - boxHeight) / 2;

        renderer.DrawRectangle(
            new Vector2(boxX, boxY),
            new Vector2(boxWidth, boxHeight),
            new Color(40, 40, 60),
            _levelComplete ? new Color(100, 255, 100) : new Color(255, 100, 100),
            4
        );

        // Title
        string title = _levelComplete ? "🏆 VICTORY! 🏆" : "💀 GAME OVER 💀";
        var titleColor = _levelComplete ? new Color(100, 255, 100) : new Color(255, 100, 100);
        renderer.DrawText(title, boxX + 80, boxY + 20, 32, titleColor);

        // Stats
        renderer.DrawText($"Score: {_score}", boxX + 50, boxY + 80, 22, Color.White);
        renderer.DrawText($"Coins: {_coinsCollected}", boxX + 50, boxY + 110, 22, new Color(255, 215, 0));
        
        if (_levelComplete)
        {
            int timeBonus = Math.Max(0, 5000 - (int)(_gameTime * 100));
            renderer.DrawText($"Time Bonus: +{timeBonus}", boxX + 50, boxY + 140, 22, new Color(150, 255, 150));
        }

        // Instructions
        if (_messageTimer <= 0)
        {
            string instruction = _levelComplete ? "Press SPACE for Menu" : "Press SPACE to Restart";
            renderer.DrawText(instruction, boxX + 70, boxY + 200, 18, new Color(200, 200, 200));
        }
    }

    public void Shutdown()
    {
        _coins.Clear();
        _hazards.Clear();
        _enemies.Clear();
        Console.WriteLine($"🎮 Platformer ended. Final Score: {_score}");
    }
}
