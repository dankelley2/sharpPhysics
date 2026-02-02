#nullable enable
using System;
using System.Collections.Generic;
using SFML.Graphics;

namespace SharpPhysics.Demo.Helpers;

/// <summary>
/// Simple animated background with floating colored circles.
/// A decorative screen effect for menus and demos.
/// </summary>
public class AnimatedBackground : IDisposable
{
    private readonly List<FloatingCircle> _circles = new();
    private readonly float _screenWidth;
    private readonly float _screenHeight;
    private readonly Random _random = new();

    public AnimatedBackground(float screenWidth, float screenHeight, int circleCount = 20)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        for (int i = 0; i < circleCount; i++)
            _circles.Add(CreateCircle());
    }

    private FloatingCircle CreateCircle()
    {
        float size = _random.NextSingle() * 200f + 5f;
        float alpha = _random.NextSingle() * 0.3f;
        float hue = _random.NextSingle();

        return new FloatingCircle
        {
            X = _random.NextSingle() * _screenWidth,
            Y = _random.NextSingle() * _screenHeight,
            VelX = (_random.NextSingle() - 0.5f) * 50f,
            VelY = (_random.NextSingle() - 0.5f) * 50f,
            Size = size,
            Alpha = alpha,
            Hue = hue,
            Shape = new CircleShape(size) { FillColor = ColorUtils.HsvToColor(hue, 0.6f, 0.8f, (byte)(alpha * 255)) }
        };
    }

    public void Update(float deltaTime)
    {
        foreach (var c in _circles)
        {
            c.X += c.VelX * deltaTime;
            c.Y += c.VelY * deltaTime;

            if (c.X < -50)
            {
                c.VelX = -c.VelX;
                // impulse to avoid sticking to the edge
                c.X += 1f;
            }
            if (c.X > _screenWidth + 50)
            {
                c.VelX = -c.VelX;
                c.X -= 1f;
            }
            if (c.Y < -50)
            {
                c.VelY = -c.VelY;
                c.Y += 1f;
            }
            if (c.Y > _screenHeight + 50)
            {
                c.VelY = -c.VelY;
                c.Y -= 1f;
            }

            c.Hue = (c.Hue + deltaTime * 0.05f) % 1f;
            c.Shape.FillColor = ColorUtils.HsvToColor(c.Hue, 0.6f, 0.8f, (byte)(c.Alpha * 255));
        }
    }

    public void Draw(RenderWindow window)
    {
        foreach (var c in _circles)
        {
            c.Shape.Position = new SFML.System.Vector2f(c.X - c.Size, c.Y - c.Size);
            window.Draw(c.Shape);
        }
    }

    public void Dispose()
    {
        foreach (var c in _circles)
            c.Shape.Dispose();
        _circles.Clear();
    }

    private class FloatingCircle
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
}

/// <summary>
/// Color conversion utilities.
/// </summary>
public static class ColorUtils
{
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
}
