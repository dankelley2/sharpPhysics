#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SFML.Graphics;

namespace physics.Engine.Rendering;

/// <summary>
/// A simple animated particle for background effects.
/// </summary>
public class BackgroundParticle
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelX { get; set; }
    public float VelY { get; set; }
    public float Size { get; set; }
    public float Alpha { get; set; }
    public float Hue { get; set; }
    public CircleShape Shape { get; set; } = null!;
}

/// <summary>
/// Reusable particle system for animated backgrounds.
/// </summary>
public class ParticleSystem
{
    private readonly List<BackgroundParticle> _particles = new();
    private readonly float _screenWidth;
    private readonly float _screenHeight;
    private readonly Random _random = new();

    public IReadOnlyList<BackgroundParticle> Particles => _particles;

    public ParticleSystem(float screenWidth, float screenHeight, int particleCount = 20)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        for (int i = 0; i < particleCount; i++)
        {
            _particles.Add(CreateParticle());
        }
    }

    private BackgroundParticle CreateParticle()
    {
        float size = _random.NextSingle() * 200f + 5f;
        float alpha = _random.NextSingle() * 0.1f;
        float hue = _random.NextSingle();

        var particle = new BackgroundParticle
        {
            X = _random.NextSingle() * _screenWidth,
            Y = _random.NextSingle() * _screenHeight,
            VelX = (_random.NextSingle() - 0.5f) * 30f,
            VelY = (_random.NextSingle() - 0.5f) * 30f,
            Size = size,
            Alpha = alpha,
            Hue = hue,
            Shape = new CircleShape(size)
            {
                FillColor = HsvToColor(hue, 0.6f, 0.8f, (byte)(alpha * 255))
            }
        };

        return particle;
    }

    public void Update(float deltaTime)
    {
        foreach (var p in _particles)
        {
            p.X += p.VelX * deltaTime;
            p.Y += p.VelY * deltaTime;

            if (p.X < -50) p.X = _screenWidth + 50;
            if (p.X > _screenWidth + 50) p.X = -50;
            if (p.Y < -50) p.Y = _screenHeight + 50;
            if (p.Y > _screenHeight + 50) p.Y = -50;

            p.Hue = (p.Hue + deltaTime * 0.05f) % 1f;
            p.Shape.FillColor = HsvToColor(p.Hue, 0.6f, 0.8f, (byte)(p.Alpha * 255));
        }
    }

    public void Draw(RenderWindow window)
    {
        foreach (var p in _particles)
        {
            p.Shape.Position = new SFML.System.Vector2f(p.X - p.Size, p.Y - p.Size);
            window.Draw(p.Shape);
        }
    }

    public static Color HsvToColor(float h, float s, float v, byte alpha)
    {
        int hi = (int)(h * 6) % 6;
        float f = h * 6 - (int)(h * 6);
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        float r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return new Color((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), alpha);
    }

    public void Dispose()
    {
        foreach (var p in _particles)
        {
            p.Shape.Dispose();
        }
        _particles.Clear();
    }
}
