namespace physics.Engine.Input
{
    public record KeyState
    {
        public bool Left { get; set; }
        public bool Right { get; set; }
        public bool Up { get; set; }
        public bool Down { get; set; }
        public bool Space { get; set; }
        
    }
}