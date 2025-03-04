using SFML.System;
using System;
using physics.Engine.Input;
using physics.Engine.Objects;
using System.Runtime.InteropServices.Marshalling;
using System.Collections.Generic;

namespace SharpPhysics.Engine.Player
{
    public class PlayerController
    {
        private float _speed = 150.0f;
        private float _jumpForce = 180.0f;
        private bool _isGrounded => _groundObjects.Count > 0;
        private PhysicsObject _player;

        // Field to store the accumulated horizontal input.
        // Negative values mean leftward input, positive values mean rightward input.
        private float _horizontalInput = 0f;

        private List<PhysicsObject> _groundObjects = new List<PhysicsObject>();

        public PlayerController(PhysicsObject player)
        {
            _player = player;
            _player.Friction = 0.99f;
            _player.Restitution = 0.001f;
            _player.ContactPointAdded += OnContactPointAdded;
            _player.ContactPointRemoved += OnContactPointRemoved;
        }

        private void OnContactPointAdded(PhysicsObject obj, (Vector2f, Vector2f) pointNormal)
        {
            // if normal is pointing down, add to ground objects
            var (_, normal) = pointNormal;
            if (normal.Y > 0.4f)
            {
                _groundObjects.Add(obj);
            }
        }

        private void OnContactPointRemoved(PhysicsObject obj, (Vector2f, Vector2f) pointNormal)
        {
            _ = _groundObjects.Remove(obj);
        }

        /// <summary>
        /// Updates the player controller. Assumes deltaTime is passed in (in seconds).
        /// </summary>
        public void Update(KeyState keyState)
        {
            // Reset the horizontal input accumulator.
            _horizontalInput = 0f;

            if (keyState.Left)
            {
                MoveLeft();
            }
            if (keyState.Right)
            {
                MoveRight();
            }

            // Apply horizontal movement.
            // TODO: resting on platfoms
            // if (_isGrounded)
            //     _player.Velocity = new Vector2f(_player.Velocity.X + _horizontalInput * _speed, _player.Velocity.Y);
            // else 

            _player.Velocity = new Vector2f(_horizontalInput * _speed, _player.Velocity.Y);

            if (keyState.Up)
            {
                Jump();
            }
        }

        private void MoveLeft()
        {
            _horizontalInput -= 1f;
        }

        private void MoveRight()
        {
            _horizontalInput += 1f;
        }

        private void Jump()
        {
            if (_isGrounded)
            {
                _player.Velocity = new Vector2f(_player.Velocity.X, -_jumpForce);
            }
        }

        // Called to simulate landing.
        public void Land()
        {

        }
    }
}