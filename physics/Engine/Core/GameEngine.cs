#nullable enable
using System;
using System.Diagnostics;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Rendering.UI;
using physics.Engine.Objects;

namespace physics.Engine.Core
{
    /// <summary>
    /// Main game engine class that manages the game loop and core systems.
    /// Games implement IGame and pass themselves to GameEngine to run.
    /// </summary>
    public class GameEngine
    {
        private PhysicsSystem _physicsSystem;
        private readonly Renderer _renderer;
        private InputManager _inputManager;
        private IGame _currentGame;
        private IGame? _pendingGame;

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _lastFrameTicks;

        // Cap maximum delta time to avoid spiral of death.
        private const float MAX_DELTA_TIME = 0.033f;

        // Performance metrics (exposed for games/debugging).
        public long MsFrameTime { get; private set; }
        public long MsDrawTime { get; private set; }
        public long MsPhysicsTime { get; private set; }

        /// <summary>
        /// Provides access to the physics system for creating and managing physics objects.
        /// </summary>
        public PhysicsSystem PhysicsSystem => _physicsSystem;

        /// <summary>
        /// Provides access to the renderer for game-specific rendering needs.
        /// </summary>
        public Renderer Renderer => _renderer;

        /// <summary>
        /// Provides access to the input manager for advanced input handling.
        /// </summary>
        public InputManager InputManager => _inputManager;

        /// <summary>
        /// The width of the game window/world.
        /// </summary>
        public uint WindowWidth { get; }

        /// <summary>
        /// The height of the game window/world.
        /// </summary>
        public uint WindowHeight { get; }

        /// <summary>
        /// Creates a new GameEngine instance with the specified window dimensions.
        /// </summary>
        /// <param name="width">Window width in pixels.</param>
        /// <param name="height">Window height in pixels.</param>
        /// <param name="title">Window title.</param>
        /// <param name="game">The game implementation to run.</param>
        public GameEngine(uint width, uint height, string title, IGame game)
        {
            WindowWidth = width;
            WindowHeight = height;
            _currentGame = game;

            _physicsSystem = new PhysicsSystem();
            _renderer = new Renderer(width, height, title, _physicsSystem);
            _inputManager = new InputManager(_renderer.Window);
        }

        /// <summary>
        /// Switches to a new game. The switch happens at the start of the next frame.
        /// </summary>
        /// <param name="newGame">The new game to switch to.</param>
        public void SwitchGame(IGame newGame)
        {
            _pendingGame = newGame;
        }

        /// <summary>
        /// Performs the actual game switch, cleaning up old game and initializing new one.
        /// </summary>
        private void PerformGameSwitch()
        {
            if (_pendingGame == null) return;

            Console.WriteLine("[GameEngine] Switching games...");

            // Shutdown current game (this should dispose PersonColliderBridge, etc.)
            try
            {
                _currentGame.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameEngine] Error during game shutdown: {ex.Message}");
            }

            // Give time for background threads to terminate (HTTP connections, detection threads)
            System.Threading.Thread.Sleep(300);

            // Clear all physics objects
            foreach (var obj in _physicsSystem.ListStaticObjects.ToArray())
            {
                _physicsSystem.RemovalQueue.Enqueue(obj);
            }
            _physicsSystem.ListGravityObjects.Clear();
            _physicsSystem.Constraints.Clear();

            // Process removal queue
            while (_physicsSystem.RemovalQueue.Count > 0)
            {
                var obj = _physicsSystem.RemovalQueue.Dequeue();
                _physicsSystem.ListStaticObjects.Remove(obj);
            }

            // Reset physics properties
            _physicsSystem.Gravity = new System.Numerics.Vector2(0, 9.8f);
            _physicsSystem.GravityScale = 30f;
            _physicsSystem.TimeScale = 1f;
            _physicsSystem.IsPaused = false;

            // Reset view pan/zoom to default
            _renderer.ResetView(WindowWidth, WindowHeight);

            // Switch to new game
            _currentGame = _pendingGame;
            _pendingGame = null;

            // Initialize new game
            try
            {
                _currentGame.Initialize(this);
                Console.WriteLine("[GameEngine] Game switch complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameEngine] Error initializing new game: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initializes the game and starts the main game loop.
        /// This method blocks until the window is closed.
        /// </summary>
        public void Run()
        {
            // Initialize the game
            _currentGame.Initialize(this);

            _stopwatch.Start();
            _lastFrameTicks = _stopwatch.ElapsedTicks;

            while (_renderer.Window.IsOpen)
            {
                // Check for pending game switch
                if (_pendingGame != null)
                {
                    PerformGameSwitch();
                }

                // Handle window events
                _renderer.Window.DispatchEvents();

                long frameStartTime = _stopwatch.ElapsedMilliseconds;

                // Get delta time using Stopwatch ticks for sub-ms precision
                long currentTicks = _stopwatch.ElapsedTicks;
                float deltaTime = (currentTicks - _lastFrameTicks) / (float)Stopwatch.Frequency;
                _lastFrameTicks = currentTicks;
                deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

                // Update input (set view for coordinate transformation first)
                _inputManager.SetView(_renderer.GameView);
                _inputManager.Update(deltaTime);

                // Update game logic
                _currentGame.Update(deltaTime, _inputManager);

                // End input frame (updates previous state for edge detection)
                _inputManager.EndFrame();

                // Physics update
                long physicsStart = _stopwatch.ElapsedMilliseconds;
                _physicsSystem.Tick(deltaTime);
                MsPhysicsTime = _stopwatch.ElapsedMilliseconds - physicsStart;

                // Rendering pipeline (layered)
                long renderStart = _stopwatch.ElapsedMilliseconds;

                _renderer.BeginFrame();
                _currentGame.RenderBackground(_renderer);
                _renderer.RenderPhysicsObjects();
                _currentGame.Render(_renderer);
                _renderer.Display();

                MsDrawTime = _stopwatch.ElapsedMilliseconds - renderStart;
                MsFrameTime = _stopwatch.ElapsedMilliseconds - frameStartTime;
            }

            // Cleanup
            _currentGame.Shutdown();
        }
    }
}
