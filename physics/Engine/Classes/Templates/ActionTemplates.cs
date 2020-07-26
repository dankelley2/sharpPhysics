using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using physics.Engine.Structs;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ActionTemplates
    {
        public static void launch(PhysicsSystem physSystem, PhysicsObject physObj, PointF StartPointF, PointF EndPointF)
        {
            physSystem.ActivateAtPoint(StartPointF);
            Vec2 delta = (new Vec2 { X = EndPointF.X, Y = EndPointF.Y } -
                          new Vec2 { X = StartPointF.X, Y = StartPointF.Y });
            physSystem.AddVelocityToActive(-delta);
        }
    }
}
