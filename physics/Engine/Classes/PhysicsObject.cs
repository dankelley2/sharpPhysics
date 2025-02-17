using physics.Engine.Structs;
using physics.Engine;
using SFML.System;
using physics.Engine.Classes;
using System;

public class PhysicsObject
{
    public enum Type { Box, Circle }

    public SFMLShader Shader;
    public bool Locked;
    public AABB Aabb;
    public Vector2f Velocity;
    public Vector2f Center;
    public Vector2f Pos;
    public float Width;
    public float Height;
    public float Restitution;
    public float Mass;
    public float IMass;
    public Vector2f LastContactPoint;

    // New rotational properties:
    public float Angle;             // Orientation (in degrees or radians)
    public float AngularVelocity;   // Angular speed
    public float Inertia;           // Moment of inertia
    public float IInertia;          // Inverse of moment of inertia

    public PhysicsObject(AABB boundingBox, Type t, float r, bool locked, SFMLShader shader, float m = 0)
    {
        Velocity = new Vector2f(0, 0);
        Aabb = boundingBox;
        Width = Aabb.Max.X - Aabb.Min.X;
        Height = Aabb.Max.Y - Aabb.Min.Y;
        Pos = new Vector2f(Aabb.Min.X, Aabb.Min.Y);
        Center = new Vector2f(Pos.X + Width / 2, Pos.Y + Height / 2);
        ShapeType = t;
        Restitution = r;
        Mass = (int)m == 0 ? Aabb.Area : m;
        IMass = 1 / Mass;
        Locked = locked;
        Shader = shader;

        // Initialize rotation state
        Angle = 0;
        AngularVelocity = 0;

        // For a circle, moment of inertia I = 0.5 * Mass * radius^2
        if (t == Type.Circle)
        {
            float radius = Width / 2;
            Inertia = 0.5f * Mass * radius * radius;
        }
        else
        {
            // For a box, a common approximation:
            Inertia = (Mass / 12f) * (Width * Width + Height * Height);
        }
        IInertia = (Inertia != 0) ? 1 / Inertia : 0;
    }

    public Type ShapeType { get; set; }
    public Manifold LastCollision { get; internal set; }

    public bool Contains(Vector2f p)
    {
        return Aabb.Max.X > p.X && p.X > Aabb.Min.X &&
               Aabb.Max.Y > p.Y && p.Y > Aabb.Min.Y;
    }

    public void Move(float dt)
    {
        if (Mass >= 1000000)
            return;

        RoundSpeedToZero();

        var p1 = Aabb.Min + (Velocity * dt);
        var p2 = Aabb.Max + (Velocity * dt);
        Aabb = new AABB { Min = p1, Max = p2 };
        Recalculate();
    }

    private void RoundSpeedToZero()
    {
        if (Math.Abs(Velocity.X) + Math.Abs(Velocity.Y) < 0.01F)
        {
            Velocity = new Vector2f(0, 0);
        }
    }

    private void Recalculate()
    {
        Width = Aabb.Max.X - Aabb.Min.X;
        Height = Aabb.Max.Y - Aabb.Min.Y;
        Pos = new Vector2f(Aabb.Min.X, Aabb.Min.Y);
        Center = new Vector2f(Pos.X + Width / 2, Pos.Y + Height / 2);
    }

    public void Move(Vector2f dVector)
    {
        if (Locked)
            return;

        Aabb.Min += dVector;
        Aabb.Max += dVector;
        Recalculate();
    }
    public void UpdateRotation(float dt)
    {
        if (Locked) return;
        Angle += AngularVelocity * dt;

        // Apply angular damping.
        AngularVelocity *= 0.9999f; // dampen by 1% per update
        if (Math.Abs(AngularVelocity) < 0.0001f)
            AngularVelocity = 0;
    }
}
