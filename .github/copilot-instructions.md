# SharpPhysics Project Instructions

## Project Overview

SharpPhysics is a 3D physics engine built with C# (.NET 9). SFML.NET provides the window, input, text and 2D UI; physics objects are drawn in a depth-tested OpenGL pass (Silk.NET.OpenGL on SFML's context) by `SceneRenderer`, viewed through `Renderer.Camera` (`Camera3D`, a perspective orbit camera). Each `PhysicsObject` has an `ObjectShader` (`SolidShader`, `WireframeShader`, `NoneShader`) that submits draws to the `SceneRenderer`. It includes:

- **SharpPhysics/** (`SharpPhysics.csproj`) - Core physics engine library
- **SharpPhysics3d.Demo/** - Slim 3D test harness (menu + top-down pool table scene)
- **SharpPhysics.Demo/** - Demo games (not yet ported to 3D; does not build)
- **SharpPhysics.Tests/** - Unit tests (not yet ported to 3D; does not build)

## Units and Scale

World units are **pixels**, and objects commonly range from 10 px to 500+ px. Mass defaults to volume (px³), so masses reach 1e8+ and inverse inertia ~1e-12.

- Never use absolute tolerances tuned for meter-scale engines; scale them with body size (e.g. `PolyhedronPhysShape.BoundingRadius`).
- Don't use mass cutoffs to mean "immovable"; use `Locked`.
- Use `PhysMath.Rotate`/`InverseRotate`, not `Vector3.Transform(Vector3, Quaternion)`, which loses precision for tiny vectors.

## Architecture Principles

### Engine vs Game Separation

The `SharpPhysics/` project is the **core engine** and should contain only:
- Physics simulation (`PhysicsSystem`, collision detection, constraints)
- Core rendering infrastructure (`Renderer`, primitive drawing)
- Input handling (`InputManager`)
- Game loop (`GameEngine`, `IGame` interface)
- Reusable UI components (`UiManager`, `UiButton`, `UiSlider`, etc.)

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
SharpPhysics/Engine/
├── Core/           # GameEngine, IGame, PhysicsSystem, Collision
├── Input/          # InputManager
├── Classes/        # PhysicsObject, CompoundBody, Manifold, collision pairs
├── Shapes/         # SpherePhysShape, BoxPhysShape, PolyhedronPhysShape, CompoundShape
├── Constraints/    # Physics constraints (Weld, Axis, Spring)
├── Helpers/        # PhysMath, Mat3, extensions
└── Structs/        # AABB
SharpPhysics/Rendering/
├── Shaders/        # ObjectShader implementations (per-object 3D drawing)
└── UI/             # UI components

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

Construct objects directly and add them to the physics system:

```csharp
var box = new PhysicsObject(new BoxPhysShape(40, 40, 40), new Vector3(x, y, z), restitution: 0.2f,
    locked: false, shader, canRotate: true);
engine.PhysicsSystem.ListStaticObjects.Add(box);
```

### Constraints

```csharp
engine.AddWeldConstraint(bodyA, bodyB, anchorA, anchorB);  // Rigid
engine.AddAxisConstraint(bodyA, bodyB, anchorA, anchorB); // Rotating joint
engine.AddSpringConstraint(bodyA, bodyB);                  // Spring
```

## Dependencies

- **SFML.NET** - Windowing, input, text and 2D UI
- **Silk.NET.OpenGL** - Depth-tested 3D scene rendering on SFML's GL context
- **System.Numerics** - Vector math (`Vector3`, `Quaternion`; `Mat3` in Helpers for inertia tensors)

## Testing

Run `dotnet build` in `SharpPhysics/` to verify engine changes compile. The Demo and Tests projects still target the old 2D API.
