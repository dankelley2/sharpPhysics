using System;
using System.Collections.Generic;
using physics.Engine.Classes;
using physics.Engine.Objects;
using physics.Engine.Shapes;
using physics.Engine.Constraints;
using System.Linq;
using System.Numerics;
using physics.Engine.Core;

public static class ConstraintHelpers
{

    public static Constraint AddWeldConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, Vector2 localAnchorA, Vector2 localAnchorB, bool canBreak = false)
    {
        var weldConstraint = new WeldConstraint(objA, objB, localAnchorA, localAnchorB, canBreak);
        engine.PhysicsSystem.Constraints.Add(weldConstraint);
        return weldConstraint;
    }

    public static Constraint AddWeldConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, bool canBreak = false)
    {
        var halfdiff = (objB.Center - objA.Center) / 2f;
        var weldConstraint = new WeldConstraint(objA, objB, halfdiff, -halfdiff, canBreak);
        engine.PhysicsSystem.Constraints.Add(weldConstraint);
        return weldConstraint;
    }

    public static Constraint AddAxisConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB, Vector2 localAnchorA, Vector2 localAnchorB)
    {
        var axisConstraint = new AxisConstraint(objA, objB, localAnchorA, localAnchorB);
        engine.PhysicsSystem.Constraints.Add(axisConstraint);
        return axisConstraint;
    }

    public static Constraint AddAxisConstraint(this GameEngine engine, PhysicsObject objA, PhysicsObject objB)
    {
        var halfdiff = (objB.Center - objA.Center) / 2f;
        var axisConstraint = new AxisConstraint(objA, objB, halfdiff, -halfdiff);
        engine.PhysicsSystem.Constraints.Add(axisConstraint);
        return axisConstraint;
    }
}