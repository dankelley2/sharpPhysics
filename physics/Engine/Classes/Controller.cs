using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SFML.Window;

namespace physics.Engine.Classes
{

    public class PlayerController : Controller
    {
        public PlayerController(PhysicsObject obj)
        {
            _obj = obj;
        }

        public override void Update(Keys k)
        {

        }
    }

    public abstract class Controller
    {
        public PhysicsObject _obj;

        public abstract void Update(Keys k);
    }
}
