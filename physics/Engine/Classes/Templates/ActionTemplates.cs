
using physics.Engine.Objects;
using physics.Engine.Shaders;
using System.Numerics;

namespace physics.Engine.Classes.ObjectTemplates
{
    public class ActionTemplates
    {
        private readonly PhysicsSystem _physicsSystem;

        public ActionTemplates(PhysicsSystem physicsSystem)
        {
            _physicsSystem = physicsSystem;
        }

        public void Launch(PhysicsObject physObj, Vector2 StartPointF, Vector2 EndPointF)
        {
            _physicsSystem.ActivateAtPoint(StartPointF);
            Vector2 delta = (new Vector2 { X = EndPointF.X, Y = EndPointF.Y } -
                          new Vector2 { X = StartPointF.X, Y = StartPointF.Y });
            _physicsSystem.AddVelocityToActive(delta * 2);
        }
    }
}
