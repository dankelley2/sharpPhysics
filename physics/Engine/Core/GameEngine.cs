#nullable enable
using System;
using System.Diagnostics;
using SFML.System;
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

        private readonly Clock _clock = new Clock();
        private readonly Stopwatch _stopwatch = new Stopwatch();

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
            _inputManager = new InputManager(_renderer.Window, _physicsSystem, _renderer.GameView);
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

            // Process removal queue
            while (_physicsSystem.RemovalQueue.Count > 0)
            {
                var obj = _physicsSystem.RemovalQueue.Dequeue();
                _physicsSystem.ListStaticObjects.Remove(obj);
            }

            // Clear all UI elements
            UiElement.GlobalUiElements.Clear();

            // Reset physics properties
            _physicsSystem.Gravity = new System.Numerics.Vector2(0, 9.8f);
            _physicsSystem.GravityScale = 30f;
            _physicsSystem.TimeScale = 1f;
            _physicsSystem.IsPaused = false;

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

                // Get delta time and cap it
                float deltaTime = _clock.Restart().AsSeconds();
                deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

                // Update input
                _inputManager.Update(deltaTime);

                // Update game logic
                _currentGame.Update(deltaTime, _inputManager.GetKeyState());

                // Physics update
                long physicsStart = _stopwatch.ElapsedMilliseconds;
                _physicsSystem.Tick(deltaTime);
                MsPhysicsTime = _stopwatch.ElapsedMilliseconds - physicsStart;

                // Rendering
                long renderStart = _stopwatch.ElapsedMilliseconds;

                // Engine rendering (physics objects, debug UI)
                _renderer.Render(MsPhysicsTime, MsDrawTime, MsFrameTime);

                // Game-specific rendering (skeleton overlay, score display, etc.)
                _currentGame.Render(_renderer);

                // Present the frame to screen (after all rendering is complete)
                _renderer.Display();

                MsDrawTime = _stopwatch.ElapsedMilliseconds - renderStart;
                MsFrameTime = _stopwatch.ElapsedMilliseconds - frameStartTime;
            }

            // Cleanup
            _currentGame.Shutdown();
        }
    }
}
