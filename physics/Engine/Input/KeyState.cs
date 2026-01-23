using System.Numerics;

namespace physics.Engine.Input
{
    public struct KeyState
    {
        // Keyboard - held state (true while key is down)
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool Space;
        public bool Escape;
        public bool Enter;
        public bool Tab;
        public bool Backspace;

        // Keyboard - pressed state (true only on the frame the key was pressed)
        public bool LeftPressed;
        public bool RightPressed;
        public bool UpPressed;
        public bool DownPressed;
        public bool SpacePressed;
        public bool EscapePressed;
        public bool EnterPressed;
        public bool TabPressed;
        public bool BackspacePressed;

        // Mouse buttons (current frame state)
        public bool LeftMouseDown;
        public bool RightMouseDown;
        public bool MiddleMouseDown;

        // Mouse button pressed this frame (edge detection)
        public bool LeftMousePressed;
        public bool RightMousePressed;

        // Mouse position in world coordinates
        public Vector2 MousePosition;

        /// <summary>
        /// Copies all values from another KeyState.
        /// </summary>
        public void CopyFrom(in KeyState other)
        {
            Left = other.Left;
            Right = other.Right;
            Up = other.Up;
            Down = other.Down;
            Space = other.Space;
            Escape = other.Escape;
            Enter = other.Enter;
            Tab = other.Tab;
            Backspace = other.Backspace;
            LeftPressed = other.LeftPressed;
            RightPressed = other.RightPressed;
            UpPressed = other.UpPressed;
            DownPressed = other.DownPressed;
            SpacePressed = other.SpacePressed;
            EscapePressed = other.EscapePressed;
            EnterPressed = other.EnterPressed;
            TabPressed = other.TabPressed;
            BackspacePressed = other.BackspacePressed;
            LeftMouseDown = other.LeftMouseDown;
            RightMouseDown = other.RightMouseDown;
            MiddleMouseDown = other.MiddleMouseDown;
            LeftMousePressed = other.LeftMousePressed;
            RightMousePressed = other.RightMousePressed;
            MousePosition = other.MousePosition;
        }
    }
}