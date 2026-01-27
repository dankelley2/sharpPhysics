#nullable enable
using physics.Engine.Input;
using physics.Engine.Rendering;

namespace physics.Engine.Core
{
    /// <summary>
    /// Interface that games must implement to run on the SharpPhysics engine.
    /// </summary>
    public interface IGame
    {
        /// <summary>
        /// Called once when the game starts. Use this to set up the game world,
        /// create initial objects, and initialize game-specific systems.
        /// </summary>
        /// <param name="engine">The game engine instance providing access to core systems.</param>
        void Initialize(GameEngine engine);

        /// <summary>
        /// Called each frame to update game logic.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
        /// <param name="input">Input manager for querying keyboard and mouse state.</param>
        void Update(float deltaTime, InputManager input);

        /// <summary>
        /// Called each frame after physics to allow game-specific rendering.
        /// The engine handles rendering physics objects; use this for game-specific overlays.
        /// </summary>
        /// <param name="renderer">The renderer to draw with.</param>
        void Render(Renderer renderer);

        /// <summary>
        /// Called when the game is shutting down. Use this to clean up game-specific resources.
        /// </summary>
        void Shutdown();
    }
}
