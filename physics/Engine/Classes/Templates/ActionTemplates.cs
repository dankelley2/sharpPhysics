using System;
using System.Collections.Generic;
using System.Linq;
using SFML.System;
using physics.Engine.Helpers;
using physics.Engine.Structs;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ActionTemplates
    {
        public static void launch(PhysicsSystem physSystem, PhysicsObject physObj, Vector2f start, Vector2f end)
        {
            physSystem.ActivateAtPoint(start);
            Vec2 delta = (new Vec2 { X = end.X, Y = end.Y } -
                          new Vec2 { X = start.X, Y = start.Y });
            physSystem.AddVelocityToActive(-delta);
        }

        public static void changeShader(PhysicsSystem physSystem, aShader shader)
        {
            foreach(PhysicsObject obj in physSystem.GetMoveableObjects())
            {
                obj.Shader = shader;
            }
        }

        public static void PopAndMultiply(PhysicsSystem physSystem)
        {
            foreach(PhysicsObject obj in physSystem.GetMoveableObjects())
            {
                physSystem.ActivateAtPoint(new Vector2f(obj.Center.X, obj.Center.Y));
                var velocity = obj.Velocity;
                var origin = obj.Center;
                physSystem.RemoveActiveObject();
                physSystem.SetVelocity(ObjectTemplates.CreateSmallBall(origin.X, origin.Y), velocity);
                physSystem.SetVelocity(ObjectTemplates.CreateSmallBall(origin.X, origin.Y), velocity);
            }
        }
    }
}
