using SharpPhysics.Demo;
using physics.Engine.Core;

// Start with the Menu game - no console selection needed
try
{
    var game = new MenuGame();
    var engine = new GameEngine(1280, 720, "🎮 SharpPhysics Demo Games", game);
    engine.Run();
}
catch (Exception ex)
{
    Console.WriteLine("Fatal error starting the game engine:");
    Console.WriteLine(ex.Message);
}