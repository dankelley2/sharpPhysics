# sharpPhysics
A 2D physics engine implemented in C# using SFML for rendering.

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

## Screenshots
* Arbitrary Polygon physics now supported:

![image](https://github.com/user-attachments/assets/748ab34d-d570-4e6d-9157-5661fad7351d)
![image](https://github.com/user-attachments/assets/228bfc7f-c784-4b9b-90ea-41914142d307)

* Object Sleep States have been implemented (WIP)
![IMG_6923](https://github.com/user-attachments/assets/b48e7793-762c-4768-8609-9f0ab265a45c)

* Older Screenshots
![image](https://github.com/user-attachments/assets/5780a59d-bd7c-469f-8c92-f8b03be4f223)
![image](https://github.com/user-attachments/assets/83f52229-d30a-47b0-9467-e96b4445126b)
![image](https://github.com/user-attachments/assets/f51c6901-9197-45fd-956d-20997dabc3d1)
![image](https://github.com/user-attachments/assets/b32b5011-4df3-4fcf-81ab-707044529b9f)

## Controls
- LMB: Move existing objects or create/launch new ones
- RMB: Delete objects on click / mouse move, Lock objects that are currently being held with LMB
- P: Switch all shaders to the polygon shader
- MMB Scroll: zoom
- MMB Hold: pan screen
- G: Create Gravity orb
- SPACE: Stop all object momentum (momentarily)

## Installation and Setup
1. Clone the repository
2. Open the solution file `PhysicsEngine.sln` in Visual Studio
3. Restore NuGet packages
4. Build and run the project

## Physics Algorithms Explained

### Broad-Phase Collision Detection
The engine uses spatial hashing for efficient broad-phase collision detection. This algorithm divides the world into a grid of cells and assigns objects to cells based on their position. Only objects in the same or adjacent cells are checked for collisions, significantly reducing the number of collision checks needed.

```csharp
// From PhysicsSystem.cs
private void BroadPhase_GeneratePairs()
{
    // Reuse the ListCollisionPairs (clear it first)
    ListCollisionPairs.Clear();

    // Clear reusable structures to avoid allocations
    _spatialHash.Clear();
    _pairSet.Clear();

    float cellSize = SpatialHashCellSize;

    // Populate the spatial hash.
    foreach (var obj in ListStaticObjects)
    {
        // Get min / max extents, divide by cellSize for grid coordinates.
        int minX = (int)Math.Floor(obj.Aabb.Min.X / cellSize);
        int minY = (int)Math.Floor(obj.Aabb.Min.Y / cellSize);
        int maxX = (int)Math.Floor(obj.Aabb.Max.X / cellSize);
        int maxY = (int)Math.Floor(obj.Aabb.Max.Y / cellSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var key = (x, y);
                if (!_spatialHash.TryGetValue(key, out List<PhysicsObject> cellList))
                {
                    cellList = new List<PhysicsObject>();
                    _spatialHash[key] = cellList;
                }
                cellList.Add(obj);
            }
        }
    }

    // Generate collision pairs from the spatial hash
    // ...
}
```

### Narrow-Phase Collision Detection
For narrow-phase collision detection, the engine uses different algorithms depending on the shapes involved:

1. **Circle vs Circle**: Simple distance check between centers
2. **Polygon vs Circle**: Find closest point on polygon to circle center
3. **Polygon vs Polygon**: Separating Axis Theorem (SAT)

The Separating Axis Theorem (SAT) is used for polygon collision detection. It projects polygons onto axes and checks for overlap. If any axis has no overlap, the polygons are not colliding.

```csharp
// From Collision.cs
private static bool ProjectAndCheckOverlap(
    Vector2f[] polyA,
    Vector2f[] polyB,
    Vector2f axis,
    ref float minPenetration,
    ref Vector2f bestAxis)
{
    // 1) Project polygon A
    (float minA, float maxA) = ProjectPolygon(polyA, axis);
    // 2) Project polygon B
    (float minB, float maxB) = ProjectPolygon(polyB, axis);

    // 3) Check for gap
    if (maxA < minB || maxB < minA)
        return false; // No overlap => no collision

    // 4) Overlap distance = min(maxA, maxB) - max(minA, minB)
    float overlap = Math.Min(maxA, maxB) - Math.Max(minA, minB);

    // Track the smallest overlap (for the final collision normal)
    if (overlap < minPenetration)
    {
        minPenetration = overlap;
        bestAxis = axis;
    }

    return true;
}
```

### Collision Resolution
The engine uses impulse-based collision resolution. When a collision is detected, impulses are applied to the objects to resolve the collision. The impulse magnitude depends on the objects' masses, velocities, and restitution (bounciness).

```csharp
// From Collision.cs
public static void ResolveCollisionRotational(ref Manifold m)
{
    // Retrieve the two physics objects.
    PhysicsObject A = m.A;
    PhysicsObject B = m.B;

    // For each object, if it's rotational, get its angular velocity and inverse inertia; otherwise, treat as zero.
    float angularVelA = A.CanRotate ? A.AngularVelocity : 0F;
    float iInertiaA =   A.CanRotate ? A.IInertia        : 0F;
    float angularVelB = B.CanRotate ? B.AngularVelocity : 0F;
    float iInertiaB =   B.CanRotate ? B.IInertia        : 0F;

    // Compute vectors from centers to contact point.
    Vector2f rA = m.ContactPoint - A.Center;
    Vector2f rB = m.ContactPoint - B.Center;

    // Compute the relative velocity at the contact point (including any rotational contribution).
    Vector2f vA_contact = A.Velocity + PhysMath.Perpendicular(rA) * angularVelA;
    Vector2f vB_contact = B.Velocity + PhysMath.Perpendicular(rB) * angularVelB;
    Vector2f relativeVelocity = vB_contact - vA_contact;

    // Calculate impulse and apply to objects
    // ...
}
```

### Object Sleep States
To optimize performance, objects that have been stationary for a while are put to sleep. Sleeping objects don't participate in physics calculations until they're woken up by a collision or user interaction.

```csharp
// From PhysicsObject.cs
public void UpdateSleepState(float dt)
{
    if (Locked)
    {
        sleepTimer = 0f;
        return;
    }

    // Compute displacement as the distance between the current center and previous center.
    float displacement = (Center - _prevCenter).Length();

    // Compare displacement against the threshold and also check angular movement.
    if (displacement < LinearSleepThreshold && Math.Abs(AngularVelocity) < AngularSleepThreshold)
    {
        sleepTimer += dt;
        if (sleepTimer >= SleepTimeThreshold)
        {
            Sleep();
        }
    }
    else
    {
        sleepTimer = 0f;
        if (Sleeping)
            Wake();
    }
}
```

## Project Structure
- **Engine**: Core physics engine components
  - **Classes**: Data structures and object definitions
  - **Constraints**: Joint and constraint implementations
  - **Helpers**: Math and utility functions
  - **Input**: User input handling
  - **Rendering**: SFML-based rendering system
  - **Shapes**: Shape implementations (Circle, Box, Polygon)
  - **Shaders**: SFML shader implementations
  - **Structs**: Basic data structures
- **Tests**: Unit tests for the physics engine

## Dependencies
- [SFML.Net](https://www.sfml-dev.org/download/sfml.net/) - Simple and Fast Multimedia Library for .NET

## Credits and References
Tutorials I worked with to create this:

- This c++ engine tutorial (Up to the rotational calculations)

    https://gamedevelopment.tutsplus.com/tutorials/how-to-create-a-custom-2d-physics-engine-the-basics-and-impulse-resolution--gamedev-6331

- SFML .Net library for drawing
  
    https://www.sfml-dev.org/download/sfml.net/
  
- Ten Minute Physics for Spatial Hashing algorithm

    https://www.youtube.com/watch?v=D2M8jTtKi44
  
- OpenAi 03-mini-high used in newer code for fixing bugs and brainstorming features
