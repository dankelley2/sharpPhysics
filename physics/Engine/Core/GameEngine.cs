#nullable enable
using System;
using System.Diagnostics;
using SFML.System;
using physics.Engine.Input;
using physics.Engine.Rendering;

namespace physics.Engine.Core
{
    /// <summary>
    /// Main game engine class that manages the game loop and core systems.
    /// Games implement IGame and pass themselves to GameEngine to run.
    /// </summary>
    public class GameEngine
    {
        private readonly PhysicsSystem _physicsSystem;
        private readonly Renderer _renderer;
        private readonly InputManager _inputManager;
        private readonly IGame _game;

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
            _game = game;

            _physicsSystem = new PhysicsSystem();
            _renderer = new Renderer(width, height, title, _physicsSystem);
            _inputManager = new InputManager(_renderer.Window, _physicsSystem, _renderer.GameView);
        }

        /// <summary>
        /// Initializes the game and starts the main game loop.
        /// This method blocks until the window is closed.
        /// </summary>
        public void Run()
        {
            // Initialize the game
            _game.Initialize(this);

            _stopwatch.Start();

            while (_renderer.Window.IsOpen)
            {
                // Handle window events
                _renderer.Window.DispatchEvents();

                long frameStartTime = _stopwatch.ElapsedMilliseconds;

                // Get delta time and cap it
                float deltaTime = _clock.Restart().AsSeconds();
                deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

                // Update input
                _inputManager.Update(deltaTime);

                // Update game logic
                _game.Update(deltaTime, _inputManager.GetKeyState());

                // Physics update
                long physicsStart = _stopwatch.ElapsedMilliseconds;
                _physicsSystem.Tick(deltaTime);
                MsPhysicsTime = _stopwatch.ElapsedMilliseconds - physicsStart;

                // Rendering
                long renderStart = _stopwatch.ElapsedMilliseconds;

                // Engine rendering (physics objects, UI)
                _renderer.Render(MsPhysicsTime, MsDrawTime, MsFrameTime,
                                 _inputManager.IsCreatingBox,
                                 _inputManager.BoxStartPoint,
                                 _inputManager.BoxEndPoint);

                // Game-specific rendering
                _game.Render(_renderer);

                MsDrawTime = _stopwatch.ElapsedMilliseconds - renderStart;
                MsFrameTime = _stopwatch.ElapsedMilliseconds - frameStartTime;
            }

            // Cleanup
            _game.Shutdown();
        }
    }
}
