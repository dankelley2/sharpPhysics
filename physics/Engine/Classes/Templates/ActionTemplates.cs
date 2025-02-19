
using physics.Engine.Objects;
using physics.Engine.Shaders;
using SFML.System;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ActionTemplates
    {
        public static void launch(PhysicsSystem physSystem, PhysicsObject physObj, Vector2f StartPointF, Vector2f EndPointF)
        {
            physSystem.ActivateAtPoint(StartPointF);
            Vector2f delta = (new Vector2f { X = EndPointF.X, Y = EndPointF.Y } -
                          new Vector2f { X = StartPointF.X, Y = StartPointF.Y });
            physSystem.AddVelocityToActive(-delta);
        }

        public static void changeShader(PhysicsSystem physSystem, SFMLShader shader)
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
