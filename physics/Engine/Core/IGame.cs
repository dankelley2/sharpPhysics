#nullable enable
using physics.Engine.Input;
using physics.Engine.Rendering;

namespace physics.Engine.Core;

/// <summary>
/// Interface that games must implement to run on the SharpPhysics engine.
/// Rendering happens in three layers: Background → Physics Objects → Foreground.
/// </summary>
public interface IGame
{
    /// <summary>
    /// Called once when the game starts.
    /// </summary>
    void Initialize(GameEngine engine);

    /// <summary>
    /// Called each frame to update game logic.
    /// </summary>
    void Update(float deltaTime, InputManager input);

    /// <summary>
    /// Optional: Renders behind physics objects (backgrounds, parallax, skyboxes).
    /// Default implementation does nothing.
    /// </summary>
    void RenderBackground(Renderer renderer) { }

    /// <summary>
    /// Renders in front of physics objects (UI, score, overlays).
    /// </summary>
    void Render(Renderer renderer);

    /// <summary>
    /// Called when the game is shutting down.
    /// </summary>
    void Shutdown();
}
