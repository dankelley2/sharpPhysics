using SharpPhysics.Demo;
using SharpPhysics.Demo.Settings;
using physics.Engine.Core;

// Load settings
var settings = GameSettings.Instance;

// Start with the Menu game
try
{
    var game = new MenuGame();
    var engine = new GameEngine(
        settings.WindowWidth,
        settings.WindowHeight,
        "🎮 SharpPhysics Demo Games",
        game);
    engine.Run();
}
catch (Exception ex)
{
    Console.WriteLine("Fatal error starting the game engine:");
    Console.WriteLine(ex.Message);
}