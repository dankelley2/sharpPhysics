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
        public bool MiddleMousePressed;

        /// <summary>
        /// Scroll wheel delta this frame. Positive = scroll up, Negative = scroll down, 0 = no scroll.
        /// </summary>
        public float ScrollWheelDelta;

        /// <summary>
        /// Mouse position in world coordinates (accounts for view panning/zoom).
        /// Use this for game logic, physics interactions, and drawing.
        /// </summary>
        public Vector2 MousePosition;

        /// <summary>
        /// Mouse position in screen/pixel coordinates (raw window position).
        /// Use this for UI hit detection and toolbar interactions.
        /// </summary>
        public Vector2 MouseScreenPosition;
    }
}