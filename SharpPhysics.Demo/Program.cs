using SharpPhysics.Demo;
using physics.Engine.Core;

var game = new DemoGame();
var engine = new GameEngine(1280, 720, "SharpPhysics Demo", game);
engine.Run();
