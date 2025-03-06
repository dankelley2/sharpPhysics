using physics.Engine.Input;
using physics.Engine.Objects;
using System.Numerics;
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

        private void OnContactPointAdded(PhysicsObject obj, (Vector2, Vector2) pointNormal)
        {
            // if normal is pointing down, add to ground objects
            var (_, normal) = pointNormal;
            if (normal.Y > 0.5f)
            {
                _groundObjects.Add(obj);
                _player.Velocity = new Vector2(_player.Velocity.X, 0);
                return;
            }
        }

        private void OnContactPointRemoved(PhysicsObject obj, (Vector2, Vector2) pointNormal)
        {
            _ = _groundObjects.Remove(obj);
        }

        public void Update(KeyState keyState)
        {
            if (_player.Sleeping)
            {
                _player.Wake();
            }
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
            if (keyState.Up)
            {
                Jump();
            }

            _player.Velocity = new Vector2(_horizontalInput * _speed, _player.Velocity.Y);

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
                _player.Velocity = new Vector2(_player.Velocity.X, -_jumpForce);
            }
        }

        // Called to simulate landing.
        public void Land()
        {

        }
    }
}