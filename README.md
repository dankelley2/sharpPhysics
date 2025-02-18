# SharpPhysics

A 2D physics engine in C# with SFML.NET rendering. The engine implements various collision detection and resolution algorithms, along with a flexible shader system for visualization.

## Core Components

### Physics System
The physics system manages object interactions and updates using several key components:

#### Collision Detection
- **AABB vs AABB**: Uses Separating Axis Theorem (SAT)
  - Checks overlap on X and Y axes independently
  - Calculates penetration depth for collision resolution
  - Determines collision normal based on minimum penetration
  - Optimized early-out checks for performance

- **Circle vs Circle**
  - Fast distance check using squared distances (avoids sqrt)
  - Handles special case of coincident circles
  - Computes penetration and normal for collision response
  - Efficient radius comparison using r²

- **AABB vs Circle**
  - Finds closest point on AABB to circle
  - Handles both external and internal collisions
  - Uses clamping for efficient point containment
  - Special handling for circles inside AABB

#### Physics Resolution
- **Impulse-Based Resolution**
  ```csharp
  // Calculate relative velocity
  var rv = objB.Velocity - objA.Velocity;
  
  // Compute impulse magnitude
  var j = -(1 + restitution) * normalVelocity / (objA.IMass + objB.IMass);
  
  // Apply impulse
  objA.Velocity -= j * objA.IMass * normal;
  objB.Velocity += j * objB.IMass * normal;
  ```

- **Positional Correction**
  - Prevents object sinking using bias factor
  - Mass-weighted correction
  - Configurable correction percentage (60% default)

#### Gravity System
- Point-based gravity fields
- Inverse square law falloff
- Distance-based force calculation
- Optimized for performance with cutoff radius

### Vector Math
The Vec2 structure provides optimized 2D vector operations:
- Basic arithmetic (add, subtract, multiply, divide)
- Dot product calculation
- Length and squared length
- Vector normalization
- Operator overloading for natural syntax

### Performance Optimizations
1. **Broad Phase Collision**
   - O(n²) pair generation optimized with spatial partitioning
   - Early-out checks in narrow phase

2. **Math Optimizations**
   - Squared distance comparisons to avoid sqrt
   - Cached vector calculations
   - Efficient dot product implementation

3. **Memory Management**
   - Struct-based vectors for stack allocation
   - Reusable collision manifolds
   - Optimized list management

## Controls

* Left Mouse Button: Move/launch objects
* Right Mouse Button: Delete objects
* P: Switch to poolball shader
* V: Switch to velocity shader
* W: Switch to "water" shader
* I: Switch to info shader
* G: Create gravity orb
* SPACE: Reset object momentum

## Visualization
The engine uses SFML.NET for hardware-accelerated rendering with a flexible shader system:
- Ball shader with velocity visualization
- Wall shader for static objects
- Debug visualization options
- Performance metrics display

## Screenshots
![Screenshot](https://user-images.githubusercontent.com/21973290/88493117-6d4f5a00-cf7d-11ea-96c7-579df0e7436a.png)

## Demo Video
https://youtu.be/O_O9jhB9bcI

## References
- [Game Physics Engine Development](https://gamedevelopment.tutsplus.com/tutorials/how-to-create-a-custom-2d-physics-engine-the-basics-and-impulse-resolution--gamedev-6331)
- [Real-Time Collision Detection](https://realtimecollisiondetection.net/)
