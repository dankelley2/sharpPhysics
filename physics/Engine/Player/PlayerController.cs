using System;
using System.Numerics;
using physics.Engine.Input;
using physics.Engine.Objects;
using SFML.System;

namespace SharpPhysics.Engine.Player
{

    public class PlayerController
    {
        
        private float _speed = 150.0f;
        private float _jumpForce = 150.0f;
        private bool _isGrounded = true;
        private PhysicsObject _player;

        public PlayerController(PhysicsObject player)
        {
            _player = player;
        }

        public void Update(KeyState keyState)
        {
            if (keyState.Left)
            {
                MoveLeft();
            }
            if (keyState.Right)
            {
                MoveRight();
            }
            if (keyState.Up)
            {
                Jump();
            }
        }

        private void MoveLeft()
        {
            _player.Velocity = new Vector2f(-_speed, _player.Velocity.Y);
        }

        private void MoveRight()
        {
            _player.Velocity = new Vector2f(_speed, _player.Velocity.Y);
        }

        private void Jump()
        {
            if (_isGrounded)
            {
                _player.Velocity = new Vector2f(_player.Velocity.X, -_jumpForce);
                //_isGrounded = false;
            }
        }

        // Call this method to simulate landing
        public void Land()
        {
            _isGrounded = true;
        }
    }
}