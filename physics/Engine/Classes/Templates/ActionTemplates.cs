
using physics.Engine.Objects;
using physics.Engine.Shaders;
using System.Numerics;

namespace physics.Engine.Classes.ObjectTemplates
{
    public class ActionTemplates
    {
        private readonly PhysicsSystem _physicsSystem;
        private readonly ObjectTemplates _objectTemplates;

        public ActionTemplates(PhysicsSystem physicsSystem, ObjectTemplates objectTemplates)
        {
            _physicsSystem = physicsSystem;
            _objectTemplates = objectTemplates;
        }

        public void Launch(PhysicsObject physObj, Vector2 StartPointF, Vector2 EndPointF)
        {
            _physicsSystem.ActivateAtPoint(StartPointF);
            Vector2 delta = (new Vector2 { X = EndPointF.X, Y = EndPointF.Y } -
                          new Vector2 { X = StartPointF.X, Y = StartPointF.Y });
            _physicsSystem.AddVelocityToActive(delta * 2);
        }

        public void ChangeShader(SFMLShader shader)
        {
            foreach(PhysicsObject obj in _physicsSystem.GetMoveableObjects())
            {
                obj.Shader = shader;
            }
        }

        public void PopAndMultiply()
        {
            foreach(PhysicsObject obj in _physicsSystem.ListStaticObjects)
            {
                _physicsSystem.ActivateAtPoint(new Vector2(obj.Center.X, obj.Center.Y));
                var velocity = obj.Velocity;
                var origin = obj.Center;
                _physicsSystem.RemoveActiveObject();
                _physicsSystem.SetVelocity(_objectTemplates.CreateSmallBall(origin.X, origin.Y), velocity);
                _physicsSystem.SetVelocity(_objectTemplates.CreateSmallBall(origin.X, origin.Y), velocity);
            }
        }
    }
}
