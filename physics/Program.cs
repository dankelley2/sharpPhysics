using physics.Demo;
using physics.Engine.Core;

namespace physics
{
    static class Program
    {
        static void Main()
        {
            var game = new DemoGame();
            var engine = new GameEngine(1280, 720, "SharpPhysics Demo", game);
            engine.Run();
        }
    }
}
