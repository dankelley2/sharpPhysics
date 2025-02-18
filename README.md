# SharpPhysics

A 2D physics engine in C# with SFML.NET rendering. Implements efficient collision detection and resolution algorithms with a focus on performance and accuracy.

## Core Components

### Physics System
The physics system manages object interactions and updates using several key components:

#### Fixed Timestep Implementation
- Uses accumulator pattern for stable physics
- Multiple physics iterations per frame for accuracy
- Configurable FPS and iteration count
- Accumulator clamping to prevent spiral of death

#### Spatial Hashing
- Grid-based spatial partitioning for broad phase
- Cell size optimized for average AABB size
- Efficient pair generation using hash sets
- Handles objects spanning multiple cells

#### Gravity System
- Point-based gravity fields
- Inverse square law falloff
- Distance-based force calculation
- Optimized cutoff radius for performance

### Collision Detection
The engine implements three types of collision detection:

#### AABB vs AABB
- Uses Separating Axis Theorem (SAT)
- Early-out checks for performance
- Minimal axis checks (X and Y only)
- Efficient overlap tests

#### Circle vs Circle
- Squared distance optimization
- Avoids sqrt until necessary
- Special case for coincident circles
- Efficient radius comparison

#### AABB vs Circle
- Voronoi region-based detection
- Efficient point containment
- Special case for internal collisions
- Optimized clamping operations

### Physics Resolution
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
  - Prevents object sinking
  - Mass-weighted correction
  - Configurable correction percentage
  - Default 60% correction factor

### Performance Optimizations
1. **Math Operations**
   - Squared distance comparisons
   - Cached vector calculations
   - Minimal sqrt usage
   - Stack-based structs

2. **Memory Management**
   - Struct-based vectors
   - Reusable collision manifolds
   - Optimized list usage
   - Minimal allocations

3. **Collision Detection**
   - Early-out in broad phase
   - Efficient overlap tests
   - Cached calculations
   - Special case handling

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
