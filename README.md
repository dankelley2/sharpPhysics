# SharpPhysics

A WIP 2D physics engine implemented in C# (.NET 9) using SFML for rendering. Project includes Demo implementaitons.
<img width="1201" height="574" alt="image" src="https://github.com/user-attachments/assets/a1ca9196-9ef5-454c-9d01-1787532e1695" />
<img width="1272" height="746" alt="image" src="https://github.com/user-attachments/assets/1a759955-dd96-45a4-b1b1-da69ce689272" />



## Recent Updates (2026)
- Refactored to separate game engine from implementation - the physics engine is now a standalone library
- Added constraint system with WeldConstraint and AxisConstraint
- Added demo games showcasing engine capabilities
- Integrated with [ProjectorSegmentation](https://github.com/dankelley2/ProjectorSegmentation) for body tracking via MJPEG stream or webcam
- Demo video: [https://youtu.be/Lo-g24-rv4k](https://youtu.be/aCmTZrhkQ3E?si=JVf6uVizv2wlRzBA)

## Features
- **Arbitrary Polygon Physics**: Support for complex polygon shapes beyond simple circles and boxes
- **Object Sleep States**: Performance optimization that puts inactive objects to sleep
- **Spatial Hashing**: Efficient broad-phase collision detection algorithm
- **Multiple Collision Types**: Circle-Circle, Circle-Polygon, and Polygon-Polygon collision detection
- **Rotational Physics**: Support for rotational dynamics and angular momentum
- **Constraint System**: Joints and connections between objects (WeldConstraint, AxisConstraint)
- **Friction and Restitution**: Configurable material properties for objects
- **SFML Rendering**: Hardware-accelerated rendering with shader support
- **UI System**: Basic UI elements for debugging and information display
- **Game Engine Architecture**: Clean separation between physics engine and game implementations via `IGame` interface

## Screenshots

*Older screenshots from development - UI and features may have changed*

Arbitrary Polygon physics:

![image](https://github.com/user-attachments/assets/748ab34d-d570-4e6d-9157-5661fad7351d)
![image](https://github.com/user-attachments/assets/228bfc7f-c784-4b9b-90ea-41914142d307)

Object Sleep States (WIP):

![IMG_6923](https://github.com/user-attachments/assets/b48e7793-762c-4768-8609-9f0ab265a45c)

Earlier development screenshots:

![image](https://github.com/user-attachments/assets/5780a59d-bd7c-469f-8c92-f8b03be4f223)
![image](https://github.com/user-attachments/assets/83f52229-d30a-47b0-9467-e96b4445126b)
![image](https://github.com/user-attachments/assets/f51c6901-9197-45fd-956d-20997dabc3d1)
![image](https://github.com/user-attachments/assets/b32b5011-4df3-4fcf-81ab-707044529b9f)

## Installation and Setup
1. Clone the repository
2. Open the solution file in Visual Studio
3. Restore NuGet packages
4. Build and run `SharpPhysics.Demo` project

## Project Structure

```
sharpPhysics/
├── physics/                          # Core Physics Engine Library (SharpPhysics.Engine)
│   └── Engine/
│       ├── Classes/                  # Data structures and object definitions
│       │   ├── Objects/              # PhysicsObject and related classes
│       │   └── Templates/            # ObjectTemplates, ActionTemplates
│       ├── Constraints/              # WeldConstraint, AxisConstraint implementations
│       ├── Core/                     # GameEngine, IGame interface
│       ├── Helpers/                  # Math utilities (PhysMath, CollisionHelpers, etc.)
│       ├── Input/                    # InputManager, KeyState
│       ├── Player/                   # PlayerController
│       ├── Rendering/                # SFML-based renderer
│       │   └── UI/                   # UI elements (buttons, sliders, etc.)
│       ├── Shaders/                  # SFML shader implementations
│       ├── Shapes/                   # Circle, Box, Polygon shape definitions
│       └── Structs/                  # AABB and other basic structures
│
├── SharpPhysics.Demo/                # Demo Application
│   ├── DemoGame.cs                   # Physics sandbox demo
│   ├── MenuGame.cs                   # Main menu
│   ├── BubblePopGame.cs              # Bubble pop mini-game
│   ├── PlatformerGame.cs             # Platformer demo
│   ├── RainCatcherGame.cs            # Rain catcher mini-game
│   ├── SettingsGame.cs               # Settings screen
│   ├── DemoProps/                    # Demo-specific props (e.g., DemoGameCar)
│   ├── Helpers/                      # Demo helpers (SkeletonRenderer)
│   ├── Integration/                  # Person detection bridge
│   ├── Settings/                     # Game settings
│   └── Resources/                    # Fonts, images, etc.
│
└── SharpPhysics.Tests/               # Unit Tests
    ├── CollisionTests.cs
    ├── CollisionHelperTests.cs
    └── Vec2Tests.cs
```

## Physics Algorithms

### Broad-Phase Collision Detection
The engine uses **spatial hashing** for efficient broad-phase collision detection. This algorithm divides the world into a grid of cells and assigns objects to cells based on their position. Only objects in the same or adjacent cells are checked for collisions.

### Narrow-Phase Collision Detection
For narrow-phase collision detection, the engine uses different algorithms depending on the shapes involved:

1. **Circle vs Circle**: Simple distance check between centers
2. **Polygon vs Circle**: Find closest point on polygon to circle center
3. **Polygon vs Polygon**: Separating Axis Theorem (SAT)

### Collision Resolution
The engine uses **impulse-based collision resolution**. When a collision is detected, impulses are applied to the objects to resolve the collision. The impulse magnitude depends on the objects' masses, velocities, and restitution (bounciness).

### Object Sleep States
To optimize performance, objects that have been stationary for a while are put to sleep. Sleeping objects don't participate in physics calculations until they're woken up by a collision or user interaction.

## Creating Your Own Game

Implement the `IGame` interface and register it with the `GameEngine`:

```csharp
public class MyGame : IGame
{
    public void Initialize(GameEngine engine) { /* Setup your game */ }
    public void Update(float deltaTime, KeyState keyState) { /* Game logic */ }
    public void Render(Renderer renderer) { /* Custom rendering */ }
    public void Shutdown() { /* Cleanup */ }
}
```

## Dependencies
- [SFML.Net](https://www.sfml-dev.org/download/sfml.net/) - Simple and Fast Multimedia Library for .NET
- [ProjectorSegmentation](https://github.com/dankelley2/ProjectorSegmentation) (optional) - Body tracking for interactive demos

## Credits and References

- [Custom 2D Physics Engine Tutorial](https://gamedevelopment.tutsplus.com/tutorials/how-to-create-a-custom-2d-physics-engine-the-basics-and-impulse-resolution--gamedev-6331) - Barebones C++ engine tutorial (up to rotational calculations)
- [SFML.Net](https://www.sfml-dev.org/download/sfml.net/) - Rendering library
- [SFML UI Implementation](https://youtu.be/3CWsy4kP6wU?si=Qf0wqzH-_7erObrL) - UI system reference
- [Ten Minute Physics](https://www.youtube.com/watch?v=D2M8jTtKi44) - Spatial Hashing algorithm
- OpenAI models used for bug fixing and brainstorming features
