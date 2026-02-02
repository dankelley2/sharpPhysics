using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SharpPhysics.Engine.Input;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Demo.DemoProps
{
    public class DemoGameCar
    {
        public PhysicsObject Body;
        public PhysicsObject FrontWheel;
        public PhysicsObject RearWheel;
        public PhysicsObject FrontBumper;
        public PhysicsObject RearBumper;

        public bool InstanceControlled { get; }

        private float speed = 5f;
        private bool canChangeDir = true;
        private float directionChangeTimer;
        private float _horizontalInput;

        public DemoGameCar(PhysicsObject body, PhysicsObject frontWheel, PhysicsObject rearWheel, PhysicsObject frontBumper, PhysicsObject rearBumper, bool instanceControlled = false)
        {
            Body = body;
            FrontWheel = frontWheel;
            RearWheel = rearWheel;
            FrontBumper = frontBumper;
            RearBumper = rearBumper;
            InstanceControlled = instanceControlled;

            frontBumper.ContactPointAdded += FrontBumper_ContactPointAdded;
            rearBumper.ContactPointAdded += FrontBumper_ContactPointAdded;

            frontWheel.Friction = 1f;
            rearWheel.Friction = 1f;
        }

        private void FrontBumper_ContactPointAdded(PhysicsObject arg1, (Vector2, Vector2) arg2)
        {
            if (InstanceControlled)
                return;

            if (!canChangeDir)
                return;
            speed = -speed;
            canChangeDir = false;
            directionChangeTimer = 0;
        }

        public void Update(float deltatime, InputManager inputManager)
        {
            if (!InstanceControlled)
            {
                FrontWheel.AngularVelocity = speed;
                RearWheel.AngularVelocity = speed;

                directionChangeTimer += deltatime;
                if (directionChangeTimer >= 1f)
                {
                    canChangeDir = true;
                }
                return;
            }

            // Reset the horizontal input accumulator.
            _horizontalInput = 0f;

            if (inputManager.IsKeyHeld(SFML.Window.Keyboard.Key.Left))
            {
                MoveLeft();
            }
            if (inputManager.IsKeyHeld(SFML.Window.Keyboard.Key.Right))
            {
                MoveRight();
            }

            var angularVelocityf = Math.Clamp((_horizontalInput * speed * 20 * deltatime + FrontWheel.AngularVelocity) , -speed, speed);
            var angularVelocityr = Math.Clamp((_horizontalInput * speed * 20 * deltatime + RearWheel.AngularVelocity) , -speed, speed);

            FrontWheel.AngularVelocity = angularVelocityf;
            RearWheel.AngularVelocity = angularVelocityr;

        }

        private void MoveLeft()
        {
            _horizontalInput -= 1f;
        }

        private void MoveRight()
        {
            _horizontalInput += 1f;
        }
    }
}
