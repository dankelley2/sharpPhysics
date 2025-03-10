﻿using System.Numerics;
using SFML.System;
using System;
using System.Diagnostics;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Input;
using physics.Engine.Rendering;
using SharpPhysics.Engine.Player;

namespace physics.Engine
{
    public class GameWindow
    {
        private PhysicsSystem physicsSystem = new PhysicsSystem();
        private Clock clock = new Clock();
        private Stopwatch stopwatch = new Stopwatch();

        // Cap maximum delta time to avoid spiral of death.
        private const float MAX_DELTA_TIME = 0.033f;

        // Performance metrics.
        private long msFrameTime;
        private long msDrawTime;
        private long msPhysicsTime;

        private InputManager inputManager;
        private Renderer renderer;
        private PlayerController playerController;

        public GameWindow(uint width, uint height, string title)
        {
            // Instantiate the renderer and input manager.
            renderer = new Renderer(width, height, title);
            inputManager = new InputManager(renderer.Window, physicsSystem, renderer.GameView);
            InitializeGame(width, height);
            stopwatch.Start();
        }

        private void InitializeGame(uint worldWidth, uint worldHeight)
        {
            // Create walls.
            ObjectTemplates.CreateWall(new Vector2(0, 0), 15, (int)worldHeight);
            ObjectTemplates.CreateWall(new Vector2((int)worldWidth - 15, 0), 15, (int)worldHeight);
            ObjectTemplates.CreateWall(new Vector2(0, 0), (int)worldWidth, 15);
            ObjectTemplates.CreateWall(new Vector2(0, (int)worldHeight - 15), (int)worldWidth, 15);

            //Create a grid of medium balls.
            for (int i = 0; i < 1000; i += 25)
            {
                for (int j = 0; j < 600; j += 25)
                {
                    if (j % 80 == 0)
                        ObjectTemplates.CreateMedBall(i + 210, j + 40);
                    else
                        ObjectTemplates.CreateMedBall(i + 200, j + 40);
                }
            }

            var player = ObjectTemplates.CreatePolygonCapsule(new Vector2(50, 20));

            playerController = new PlayerController(player);
            // Create an attractor.
            //ObjectTemplates.CreateAttractor(400, 450);

            // Create a box with initial velocity and angle.
            // Origin vector (top left)

            // Loop to create boxes
            // for (int i = 0; i < 10; i++)
            // {
            //     var boxOrigin = new Vector2(100, 100 + i * 50);
            //     ObjectTemplates.CreateBox(boxOrigin, 200, 50);
            // }
        }



        public void Run()
        {
            while (renderer.Window.IsOpen)
            {
                // Handle window events
                renderer.Window.DispatchEvents();

                // Log
                long frameStartTime = stopwatch.ElapsedMilliseconds;

                // Log
                long physicsStart = stopwatch.ElapsedMilliseconds;

                // Get delta time and cap it.
                float deltaTime = clock.Restart().AsSeconds();
                deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

                // Update Inputs
                inputManager.Update(deltaTime);

                // Update the player
                playerController.Update(inputManager.GetKeyState());

                // Tick the physics system
                physicsSystem.Tick(deltaTime);

                // Log
                msPhysicsTime = stopwatch.ElapsedMilliseconds - physicsStart;
                long renderStart = stopwatch.ElapsedMilliseconds;

                // Render the current Frame
                renderer.Render(msPhysicsTime, msDrawTime, msFrameTime,
                                  inputManager.IsCreatingBox,
                                  inputManager.BoxStartPoint,
                                  inputManager.BoxEndPoint);
                // Log
                msDrawTime = stopwatch.ElapsedMilliseconds - renderStart;
                msFrameTime = stopwatch.ElapsedMilliseconds - frameStartTime;
            }
        }
    }
}
