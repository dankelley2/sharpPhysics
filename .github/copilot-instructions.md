# SharpPhysics Project Instructions

## Project Overview

SharpPhysics is a 2D physics engine built with C# (.NET 9) and SFML.NET for rendering. It includes:

- **physics/** (`SharpPhysics.csproj`) - Core physics engine library
- **SharpPhysics.Demo/** - Demo games showcasing the engine
- **PoseIntegrator.Vision/** - YOLO-based pose detection for body tracking
- **PoseIntegrator.Demo/** - Standalone pose detection demo

## Architecture Principles

### Engine vs Game Separation

The `physics/` project is the **core engine** and should contain only:
- Physics simulation (`PhysicsSystem`, collision detection, constraints)
- Core rendering infrastructure (`Renderer`, primitive drawing)
- Input handling (`InputManager`)
- Game loop (`GameEngine`, `IGame` interface)
- Reusable UI components (`UiManager`, `UiButton`, `UiSlider`, etc.)
- Object templates for creating physics objects

**DO NOT** put game-specific code in the engine:
- Debug UIs specific to one game → put in Demo project
- Visual effects only used by demos → put in Demo project
- Game-specific scene builders → put in Demo project

### Render Layers

Games implement `IGame` with these render methods:
1. `RenderBackground(Renderer)` - Optional, renders behind physics objects
2. `Render(Renderer)` - Required, renders in front of physics objects

The engine calls them in order: Background → Physics Objects → Foreground

### Naming Conventions

- Be honest with names - don't call something a "ParticleSystem" if it's just floating circles
- Use descriptive names: `AnimatedBackground`, `SandboxDebugUI`, `DemoSceneBuilder`
- Prefix demo-specific classes appropriately

## Code Style

### General
- Use `#nullable enable` at the top of all files
- Use file-scoped namespaces (`namespace X;` not `namespace X { }`)
- Minimize comments - code should be self-documenting
- Remove commented-out code blocks

### Fields and Properties
- Private fields: `_camelCase`
- Properties: `PascalCase`
- Constants: `PascalCase` or `UPPER_SNAKE_CASE` for true constants

### Methods
- Keep methods focused and small
- Extract helper methods for clarity
- Group related functionality with `#region` sparingly

## Project Structure

```
physics/Engine/
├── Core/           # GameEngine, IGame interface
├── Rendering/      # Renderer, UI components
├── Input/          # InputManager
├── Objects/        # PhysicsObject
├── Shapes/         # CirclePhysShape, PolygonPhysShape, etc.
├── Shaders/        # SFML shader implementations
├── Constraints/    # Physics constraints (Weld, Axis, Spring)
├── Helpers/        # Math utilities, extensions
└── Classes/        # Templates, collision data

SharpPhysics.Demo/
├── DemoProps/      # Demo-specific components (SandboxDebugUI, DemoSceneBuilder)
├── Helpers/        # Demo utilities (AnimatedBackground, SkeletonRenderer)
├── Integration/    # PersonColliderBridge for pose tracking
├── Designer/       # Prefab designer game
├── Settings/       # GameSettings configuration
└── [Game].cs       # Individual game implementations
```

## Common Patterns

### Creating a New Game

```csharp
public class MyGame : IGame
{
    private GameEngine _engine = null!;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        // Setup code here
    }

    public void Update(float deltaTime, InputManager input) { }
    
    public void RenderBackground(Renderer renderer) { } // Optional
    
    public void Render(Renderer renderer) { }
    
    public void Shutdown() { }
}
```

### Debug UI (Game-Specific)

Debug/development UIs should be in the Demo project, not the engine:

```csharp
// In SharpPhysics.Demo/DemoProps/
public class MyGameDebugUI
{
    private readonly UiManager _uiManager = new();
    
    public void Render(Renderer renderer)
    {
        renderer.Window.SetView(renderer.UiView);
        _uiManager.Draw(renderer.Window);
    }
}
```

### Physics Object Creation

Use `ObjectTemplates` for creating physics objects:

```csharp
var templates = new ObjectTemplates(engine.PhysicsSystem);
var ball = templates.CreateSmallBall(x, y);
var box = templates.CreateBox(position, width, height);
```

### Constraints

```csharp
engine.AddWeldConstraint(bodyA, bodyB, anchorA, anchorB);  // Rigid
engine.AddAxisConstraint(bodyA, bodyB, anchorA, anchorB); // Rotating joint
engine.AddSpringConstraint(bodyA, bodyB);                  // Spring
```

## Dependencies

- **SFML.NET** - Windowing and rendering
- **System.Numerics** - Vector math (use `Vector2` from this, not SFML's)
- **OpenCvSharp4** - Camera capture (PoseIntegrator.Vision)
- **Microsoft.ML.OnnxRuntime** - YOLO inference (PoseIntegrator.Vision)

## Testing

Run `dotnet build` to verify changes compile. The solution includes `SharpPhysics.Tests` for unit tests.
