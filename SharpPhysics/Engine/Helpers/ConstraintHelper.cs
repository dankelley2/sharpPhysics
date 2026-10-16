#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Constraints;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

public static class ConstraintHelpers
{
    public static Constraint AddWeldConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, Vector3 localAnchorA, Vector3 localAnchorB, bool canBreak = false)
    {
        var weldConstraint = new WeldConstraint(objA, objB, localAnchorA, localAnchorB, canBreak);
        engine.PhysicsSystem.Constraints.Add(weldConstraint);
        return weldConstraint;
    }

    public static Constraint AddWeldConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, bool canBreak = false)
    {
        var (anchorA, anchorB) = MidpointAnchors(objA, objB);
        return engine.AddWeldConstraint(objA, objB, anchorA, anchorB, canBreak);
    }

    public static Constraint AddSpringConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, Vector3 localAnchorA, Vector3 localAnchorB)
    {
        var springConstraint = new SpringConstraint(objA, objB, localAnchorA, localAnchorB);
        engine.PhysicsSystem.Constraints.Add(springConstraint);
        return springConstraint;
    }

    public static Constraint AddSpringConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB)
    {
        var (anchorA, anchorB) = MidpointAnchors(objA, objB);
        return engine.AddSpringConstraint(objA, objB, anchorA, anchorB);
    }

    public static Constraint AddAxisConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, Vector3 localAnchorA, Vector3 localAnchorB)
    {
        var axisConstraint = new AxisConstraint(objA, objB, localAnchorA, localAnchorB);
        engine.PhysicsSystem.Constraints.Add(axisConstraint);
        return axisConstraint;
    }

    public static Constraint AddAxisConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB)
    {
        var (anchorA, anchorB) = MidpointAnchors(objA, objB);
        return engine.AddAxisConstraint(objA, objB, anchorA, anchorB);
    }

    // Local anchors on each body that both sit at the world midpoint between their centers.
    private static (Vector3 anchorA, Vector3 anchorB) MidpointAnchors(PhysicsObject objA, PhysicsObject objB)
    {
        var midpoint = (objA.Center + objB.Center) * 0.5f;
        return (
            PhysMath.InverseRotate(midpoint - objA.Center, objA.Orientation),
            PhysMath.InverseRotate(midpoint - objB.Center, objB.Orientation));
    }
}
