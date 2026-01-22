using System.Numerics;

namespace physics.Engine.Input
{
    public record KeyState
    {
        // Keyboard
        public bool Left { get; set; }
        public bool Right { get; set; }
        public bool Up { get; set; }
        public bool Down { get; set; }
        public bool Space { get; set; }
        public bool Escape { get; set; }
        public bool Enter { get; set; }
        public bool Tab { get; set; }
        public bool Backspace { get; set; }

        // Mouse buttons (current frame state)
        public bool LeftMouseDown { get; set; }
        public bool RightMouseDown { get; set; }
        public bool MiddleMouseDown { get; set; }

        // Mouse button pressed this frame (edge detection)
        public bool LeftMousePressed { get; set; }
        public bool RightMousePressed { get; set; }

        // Mouse position in world coordinates
        public Vector2 MousePosition { get; set; }
    }
}