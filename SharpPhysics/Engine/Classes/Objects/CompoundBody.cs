#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Constraints;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Rendering.Shaders;

namespace SharpPhysics.Engine.Objects;

/// <summary>
/// Represents a compound physics body made up of multiple convex shapes
/// that move as a single rigid body. This eliminates the need for internal
/// constraints and provides stable simulation for concave polygons.
/// </summary>
public class CompoundBody : PhysicsObject
{
    /// <summary>
    /// The compound shape containing all child shapes.
    /// </summary>
    public new CompoundShape Shape => (CompoundShape)base.Shape;

    /// <summary>
    /// Gets the number of child shapes in this compound body.
    /// </summary>
    public int ChildCount => Shape.Children.Count;

    public CompoundBody(CompoundShape shape, Vector2 center, float restitution, bool locked, SFMLShader shader, bool canRotate = true)
        : base(shape, center, restitution, locked, shader, 0, canRotate)
    {
        // Mass is calculated from the compound shape's children
        RecalculateMassProperties();
    }

    /// <summary>
    /// Creates a compound body from multiple convex polygon vertices.
    /// Each polygon is converted to a child shape with proper local offset.
    /// </summary>
    public static CompoundBody FromConvexPieces(
        Vector2 worldCenter,
        List<Vector2[]> convexPieces,
        SFMLShader shader,
        bool canRotate = true,
        float restitution = 0.2f)
    {
        var compoundShape = new CompoundShape();

        // Calculate the overall centroid of all pieces combined
        Vector2 overallCentroid = ComputeOverallCentroid(convexPieces);

        foreach (var pieceVertices in convexPieces)
        {
            // Create a polygon shape for this piece
            var polygonShape = new PolygonPhysShape(pieceVertices);

            // Calculate this piece's centroid
            Vector2 pieceCentroid = CollisionHelpers.ComputeCentroid(new List<Vector2>(pieceVertices));

            // Local offset is relative to the compound's center of mass
            Vector2 localOffset = pieceCentroid - overallCentroid;

            // Calculate mass based on area
            float mass = polygonShape.GetArea();

            compoundShape.AddChild(polygonShape, localOffset, 0f, mass);
        }

        return new CompoundBody(compoundShape, worldCenter, restitution, false, shader, canRotate);
    }

    /// <summary>
    /// Creates a compound body from multiple physics objects, placing the compound at the
    /// mass-weighted centroid of all input objects. This preserves the exact world positions
    /// of all child shapes.
    /// If any input object is a CompoundBody, its children are flattened into the new compound.
    /// </summary>
    public static (CompoundBody, List<Object> trashHeap) FromPhysicsObjects(
        List<PhysicsObject> physicsObjects,
        SFMLShader shader,
        bool canRotate = true,
        float restitution = 0.2f)
    {
        Vector2 overallCentroid = ComputeOverallCentroid(physicsObjects);
        return FromPhysicsObjects(overallCentroid, physicsObjects, shader, canRotate, restitution);
    }

    /// <summary>
    /// Creates a compound body from multiple physics objects at a specified world center.
    /// Note: For exact position preservation, use the overload without worldCenter parameter.
    /// If any input object is a CompoundBody, its children are flattened into the new compound.
    /// External constraints (to objects not being merged) are preserved with recalculated anchors.
    /// </summary>
    public static (CompoundBody, List<Object> trashHeap) FromPhysicsObjects(
        Vector2 worldCenter,
        List<PhysicsObject> physicsObjects,
        SFMLShader shader,
        bool canRotate = true,
        float restitution = 0.2f)
    {
        List<object> trashHeap = new List<object>();

        var compoundShape = new CompoundShape();
        var mergedObjects = new HashSet<PhysicsObject>(physicsObjects);

        // Calculate the overall centroid of all pieces combined
        Vector2 overallCentroid = ComputeOverallCentroid(physicsObjects);

        // Collect constraints to transfer (external constraints only)
        var constraintsToTransfer = new List<(Constraint constraint, PhysicsObject oldObject, bool isObjectA)>();

        foreach (var piece in physicsObjects)
        {
            // Check if this piece is a CompoundBody with children to merge
            if (piece is CompoundBody compoundBody && compoundBody.Shape.Children.Count > 0)
            {
                // Flatten the compound's children into the new compound
                for (int i = 0; i < compoundBody.Shape.Children.Count; i++)
                {
                    var child = compoundBody.Shape.Children[i];
                    var (childWorldCenter, childWorldAngle) = compoundBody.GetChildWorldTransform(i);

                    // Local offset relative to the new compound's center of mass
                    Vector2 localOffset = childWorldCenter - overallCentroid;

                    compoundShape.AddChild(child.Shape, localOffset, childWorldAngle, child.Mass);
                }
            }
            else
            {
                // Regular physics object - add as a single child
                Vector2 localOffset = piece.Center - overallCentroid;
                compoundShape.AddChild(piece.Shape, localOffset, piece.Angle, piece.Mass);
            }

            // Collect external constraints from this piece
            foreach (var constraint in piece.Constraints)
            {
                bool isObjectA = constraint.A == piece;
                var otherObject = isObjectA ? constraint.B : constraint.A;

                // Only transfer if the other object is NOT being merged
                if (!mergedObjects.Contains(otherObject))
                {
                    // Avoid duplicates (constraint might be seen from both sides)
                    if (!constraintsToTransfer.Exists(c => c.constraint == constraint))
                    {
                        constraintsToTransfer.Add((constraint, piece, isObjectA));
                    }
                }
                else
                {
                    // Add to trashheap for removal 
                    trashHeap.Add(constraint);
                }
            }
        }

        var compound = new CompoundBody(compoundShape, worldCenter, restitution, false, shader, canRotate);

        // Transfer external constraints to the new compound body
        foreach (var (constraint, oldObject, isObjectA) in constraintsToTransfer)
        {
            // Get the anchor in the old object's local space
            Vector2 oldAnchorLocal = isObjectA ? constraint.AnchorA : constraint.AnchorB;

            // Convert old anchor to world space
            Vector2 anchorWorld = oldObject.Center + PhysMath.RotateVector(oldAnchorLocal, oldObject.Angle);

            // Convert world anchor to new compound's local space
            Vector2 newAnchorLocal = PhysMath.RotateVector(anchorWorld - compound.Center, -compound.Angle);

            // Calculate the angle difference between old object and new compound
            // This adjusts InitialRelativeAngle to maintain the same world-space constraint behavior
            float angleDelta = oldObject.Angle - compound.Angle;

            // Update the constraint to reference the new compound
            if (isObjectA)
            {
                // Remove old object from constraint tracking
                constraint.A.Constraints.Remove(constraint);
                constraint.A.ConnectedObjects.Remove(constraint.B);
                constraint.B.ConnectedObjects.Remove(constraint.A);

                // Update constraint to use compound
                constraint.A = compound;
                constraint.AnchorA = newAnchorLocal;

                // Adjust InitialRelativeAngle: was (B.Angle - oldA.Angle), now needs (B.Angle - compound.Angle)
                // Since compound.Angle = oldA.Angle - angleDelta, the new relative angle increases by angleDelta
                constraint.InitialRelativeAngle += angleDelta;

                // Add new connections
                compound.Constraints.Add(constraint);
                compound.ConnectedObjects.Add(constraint.B);
                constraint.B.ConnectedObjects.Add(compound);
            }
            else
            {
                // Remove old object from constraint tracking
                constraint.B.Constraints.Remove(constraint);
                constraint.B.ConnectedObjects.Remove(constraint.A);
                constraint.A.ConnectedObjects.Remove(constraint.B);

                // Update constraint to use compound
                constraint.B = compound;
                constraint.AnchorB = newAnchorLocal;

                // Adjust InitialRelativeAngle: was (oldB.Angle - A.Angle), now needs (compound.Angle - A.Angle)
                // Since compound.Angle = oldB.Angle - angleDelta, the new relative angle decreases by angleDelta
                constraint.InitialRelativeAngle -= angleDelta;

                // Add new connections
                compound.Constraints.Add(constraint);
                compound.ConnectedObjects.Add(constraint.A);
                constraint.A.ConnectedObjects.Add(compound);
            }
        }

        // Clean up constraints and connections from merged objects
        foreach (var piece in physicsObjects)
        {
            // Remove internal constraints (between merged objects)
            piece.Constraints.RemoveAll(c => mergedObjects.Contains(c.A) && mergedObjects.Contains(c.B));

            // Clear connected objects that were merged
            piece.ConnectedObjects.RemoveWhere(o => mergedObjects.Contains(o));
        }

        trashHeap.AddRange(physicsObjects);

        return (compound, trashHeap);
    }

    /// <summary>
    /// Computes the overall centroid of multiple convex pieces weighted by area.
    /// </summary>
    private static Vector2 ComputeOverallCentroid(List<Vector2[]> convexPieces)
    {
        Vector2 weightedSum = Vector2.Zero;
        float totalArea = 0f;

        foreach (var piece in convexPieces)
        {
            var centroid = CollisionHelpers.ComputeCentroid(new List<Vector2>(piece));
            float area = ComputePolygonArea(piece);
            weightedSum += centroid * area;
            totalArea += area;
        }

        return totalArea > 0 ? weightedSum / totalArea : Vector2.Zero;
    }

    /// <summary>
    /// Computes the overall centroid of multiple physics objects weighted by mass.
    /// Handles CompoundBody objects by including all their children.
    /// </summary>
    private static Vector2 ComputeOverallCentroid(List<PhysicsObject> pieces)
    {
        Vector2 weightedSum = Vector2.Zero;
        float totalMass = 0f;

        foreach (var piece in pieces)
        {
            if (piece is CompoundBody compoundBody && compoundBody.Shape.Children.Count > 0)
            {
                // Include each child's contribution
                for (int i = 0; i < compoundBody.Shape.Children.Count; i++)
                {
                    var child = compoundBody.Shape.Children[i];
                    var (childWorldCenter, _) = compoundBody.GetChildWorldTransform(i);

                    weightedSum += childWorldCenter * child.Mass;
                    totalMass += child.Mass;
                }
            }
            else
            {
                weightedSum += piece.Center * piece.Mass;
                totalMass += piece.Mass;
            }
        }

        return totalMass > 0 ? weightedSum / totalMass : Vector2.Zero;
    }

    /// <summary>
    /// Computes the area of a polygon using the shoelace formula.
    /// </summary>
    private static float ComputePolygonArea(Vector2[] vertices)
    {
        float area = 0f;
        int n = vertices.Length;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += vertices[i].X * vertices[j].Y;
            area -= vertices[j].X * vertices[i].Y;
        }
        return Math.Abs(area) / 2f;
    }

    /// <summary>
    /// Recalculates mass and inertia from child shapes.
    /// </summary>
    private void RecalculateMassProperties()
    {
        float totalMass = 0f;
        float totalInertia = 0f;

        foreach (var child in Shape.Children)
        {
            totalMass += child.Mass;
            // Parallel axis theorem: I = I_cm + m*d^2
            totalInertia += child.Inertia + child.Mass * Vector2.Dot(child.LocalOffset, child.LocalOffset);
        }

        // Use protected setters from base class
        Mass = totalMass;
        IMass = totalMass > 0 ? 1f / totalMass : 0f;
        Inertia = totalInertia;
        IInertia = totalInertia > 0 ? 1f / totalInertia : 0f;
    }

    /// <summary>
    /// Gets the world-space transform for a specific child shape.
    /// </summary>
    public (Vector2 center, float angle) GetChildWorldTransform(int childIndex)
    {
        return Shape.GetChildWorldTransform(childIndex, Center, Angle);
    }

    /// <summary>
    /// Applies a force at a world point, affecting both linear and angular velocity.
    /// </summary>
    public void ApplyForceAtPoint(Vector2 force, Vector2 worldPoint)
    {
        if (Locked)
            return;

        // Linear component
        Velocity += force * IMass;

        // Angular component (torque = r × F)
        Vector2 r = worldPoint - Center;
        float torque = PhysMath.Cross(r, force);
        AngularVelocity += torque * IInertia;
    }

    /// <summary>
    /// Applies an impulse at a world point, affecting both linear and angular velocity.
    /// </summary>
    public void ApplyImpulseAtPoint(Vector2 impulse, Vector2 worldPoint)
    {
        if (Locked)
            return;

        // Linear component
        Velocity += impulse * IMass;

        // Angular component
        Vector2 r = worldPoint - Center;
        float angularImpulse = PhysMath.Cross(r, impulse);
        AngularVelocity += angularImpulse * IInertia;
    }
}

