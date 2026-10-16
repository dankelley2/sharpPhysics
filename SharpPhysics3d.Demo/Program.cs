using SharpPhysics.Engine.Core;
using SharpPhysics3d.Demo;

var engine = new GameEngine(1280, 720, "SharpPhysics 3D Demo", new MenuGame());
// Only breaks when a debugger is attached; stutters are always logged to the console.
engine.Profiler.BreakOnStutter = true;
engine.Run();
