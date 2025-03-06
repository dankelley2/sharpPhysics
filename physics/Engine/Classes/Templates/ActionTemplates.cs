
using physics.Engine.Objects;
using physics.Engine.Shaders;
using System.Numerics;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ActionTemplates
    {
        public static void launch(PhysicsSystem physSystem, PhysicsObject physObj, Vector2 StartPointF, Vector2 EndPointF)
        {
            physSystem.ActivateAtPoint(StartPointF);
            Vector2 delta = (new Vector2 { X = EndPointF.X, Y = EndPointF.Y } -
                          new Vector2 { X = StartPointF.X, Y = StartPointF.Y });
            physSystem.AddVelocityToActive(delta * 2);
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
            foreach(PhysicsObject obj in PhysicsSystem.ListStaticObjects)
            {
                physSystem.ActivateAtPoint(new Vector2(obj.Center.X, obj.Center.Y));
                var velocity = obj.Velocity;
                var origin = obj.Center;
                physSystem.RemoveActiveObject();
                physSystem.SetVelocity(ObjectTemplates.CreateSmallBall(origin.X, origin.Y), velocity);
                physSystem.SetVelocity(ObjectTemplates.CreateSmallBall(origin.X, origin.Y), velocity);
            }
        }
    }
}
