# SharpPhysics Game Development Guide

A quick reference for creating games with the SharpPhysics engine.

## Creating a New Game

### 1. Implement the `IGame` Interface

```csharp
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Input;
using SharpPhysics.Engine.Rendering;

public class MyGame : IGame
{
    private GameEngine _engine = null!;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        // Setup your game here
    }

    public void Update(float deltaTime, KeyState keyState)
    {
        // Game logic here (called every frame)
    }

    public void Render(Renderer renderer)
    {
        // Custom drawing here (after physics objects are drawn)
    }

    public void Shutdown()
    {
        // Cleanup resources here
    }
}
```

### 2. Add to Menu (Optional)

In `MenuGame.cs`, add your game to the factory dictionary and create a button:

```csharp
private readonly Dictionary<string, Func<IGame>> _gameFactories = new()
{
    ["MyGame"] = () => new MyGame(),
    // ... other games
};
```

---

## Quick Reference

### GameEngine - Core Access
```csharp
_engine.PhysicsSystem    // Physics world
_engine.Renderer         // Drawing system
_engine.WindowWidth      // Screen width
_engine.WindowHeight     // Screen height
_engine.SwitchGame(new OtherGame())  // Change games
```

### PhysicsSystem - Creating Objects
```csharp
// Static circle (ball)
var ball = _physics.CreateStaticCircle(
    position: new Vector2(100, 100),
    radius: 20,
    restitution: 0.8f,      // Bounciness (0-1)
    locked: false,          // true = immovable
    shader: new SFMLBallShader()
);

// Static box (platform/wall)
var box = _physics.CreateStaticBox(
    min: new Vector2(0, 500),
    max: new Vector2(200, 520),
    locked: true,
    shader: new SFMLWallShader(),
    mass: 1000000
);

// Polygon (custom shape)
var poly = _physics.CreatePolygon(
    origin: new Vector2(300, 200),
    vertices: new[] { new Vector2(-20,-20), new Vector2(20,-20), new Vector2(0,20) },
    shader: new SFMLPolyShader(),
    canRotate: false
);
```

### PhysicsObject - Manipulation
```csharp
obj.Center              // Current position
obj.Velocity            // Current velocity
obj.Move(delta)         // Move by offset
obj.Locked = true       // Make immovable
obj.Restitution = 0.5f  // Set bounciness
obj.Sleeping            // Is object at rest?
obj.Wake()              // Wake sleeping object
```

### Renderer - Drawing
```csharp
renderer.DrawText("Score: 100", x, y, fontSize, Color.White);
renderer.DrawLine(start, end, color, thickness);
renderer.DrawCircle(center, radius, fillColor, outlineColor, outlineThickness);
renderer.DrawRectangle(position, size, fillColor, outlineColor, outlineThickness);
renderer.DrawPolygon(points, color, thickness, closed: true);
```

### KeyState - Input
```csharp
keyState.Left    // Arrow left
keyState.Right   // Arrow right
keyState.Up      // Arrow up
keyState.Down    // Arrow down
keyState.Space   // Spacebar
keyState.Escape  // ESC key
```

### ObjectTemplates - Preset Objects
```csharp
var templates = new ObjectTemplates(_physics);
templates.CreateWall(origin, width, height);
templates.CreateSmallBall(x, y);
templates.CreateMedBall(x, y);
templates.CreateBox(origin, width, height);
```

---

## Common Patterns

### Return to Menu on ESC
```csharp
public void Update(float deltaTime, KeyState keyState)
{
    if (keyState.Escape)
    {
        _engine.SwitchGame(new MenuGame());
        return;
    }
    // ... rest of update
}
```

### Player Controller
```csharp
var playerShape = _physics.CreatePolygon(spawnPoint, vertices, shader, canRotate: false);
var controller = new PlayerController(playerShape);

// In Update:
controller.Update(keyState);  // Handles left/right/jump/slam
```

### Distance-Based Collision
```csharp
float distance = Vector2.Distance(player.Center, target.Center);
if (distance < collisionRadius)
{
    // Collision detected!
}
```

### Removing Objects
```csharp
_physics.RemovalQueue.Enqueue(objectToRemove);
// Object removed on next physics tick
```

---

## Caveats & Gotchas

| Issue | Solution |
|-------|----------|
| **Object not moving** | Check if `Locked = true` or object is `Sleeping` |
| **Object falls through floor** | Increase mass of floor, decrease `GravityScale` |
| **Jittery movement** | Use smoothing/lerp, check `deltaTime` usage |
| **Render not showing** | Ensure drawing in `Render()`, not `Update()` |
| **Game switch crashes** | Always `Dispose()` resources in `Shutdown()` |
| **Physics too fast/slow** | Adjust `_physics.GravityScale` (default: 30) |
| **UI showing in game** | Set `_engine.Renderer.ShowDebugUI = false` |

### Physics Settings
```csharp
_physics.Gravity = new Vector2(0, 9.8f);  // Direction
_physics.GravityScale = 30f;               // Multiplier
_physics.TimeScale = 1f;                   // Slow-mo: < 1
_physics.IsPaused = true;                  // Freeze physics
```

---

## Available Shaders

| Shader | Use Case |
|--------|----------|
| `SFMLBallShader` | Simple solid balls |
| `SFMLPolyShader` | Solid color polygons |
| `SFMLPolyRainbowShader` | Animated rainbow color |
| `SFMLWallShader` | Walls/platforms |
| `SFMLBallVelocityShader` | Color based on speed |

---

## Project Structure

```
SharpPhysics.Demo/
├── Program.cs              # Entry point
├── MenuGame.cs             # Main menu
├── [YourGame].cs           # Your game here!
├── Integration/
│   └── PersonColliderBridge.cs  # Body tracking (optional)
└── Helpers/
    └── SkeletonRenderer.cs      # Skeleton drawing (optional)
```

---

## Example: Minimal Game

```csharp
public class MinimalGame : IGame
{
    private GameEngine _engine = null!;
    private PhysicsObject _ball = null!;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        var physics = engine.PhysicsSystem;
        
        // Create floor
        physics.CreateStaticBox(
            new Vector2(0, 550), new Vector2(800, 600),
            true, new SFMLWallShader(), 999999);
        
        // Create bouncy ball
        _ball = physics.CreateStaticCircle(
            new Vector2(400, 100), 30, 0.9f, false,
            new SFMLBallShader());
    }

    public void Update(float deltaTime, KeyState keyState)
    {
        if (keyState.Escape) _engine.SwitchGame(new MenuGame());
        if (keyState.Space) _ball.Velocity = new Vector2(0, -300);
    }

    public void Render(Renderer renderer)
    {
        renderer.DrawText("Press SPACE to bounce!", 250, 30, 24, Color.White);
    }

    public void Shutdown() { }
}
```
