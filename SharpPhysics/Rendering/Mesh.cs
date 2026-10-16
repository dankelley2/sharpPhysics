#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Shapes;
using Silk.NET.OpenGL;

namespace SharpPhysics.Rendering;

/// <summary>
/// Indexed triangle mesh on the GPU with interleaved position + normal vertices.
/// </summary>
public sealed class Mesh : IDisposable
{
    internal const int FloatsPerVertex = 6;

    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private readonly uint _indexCount;

    internal unsafe Mesh(GL gl, ReadOnlySpan<float> vertices, ReadOnlySpan<uint> indices)
    {
        _gl = gl;
        _indexCount = (uint)indices.Length;

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);

        _ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);

        ConfigureVertexLayout(gl);

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    internal static unsafe void ConfigureVertexLayout(GL gl)
    {
        uint stride = FloatsPerVertex * sizeof(float);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
    }

    internal unsafe void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }

    /// <summary>Unit-radius sphere with smooth normals.</summary>
    internal static Mesh CreateSphere(GL gl, int slices, int stacks)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int stack = 0; stack <= stacks; stack++)
        {
            float phi = MathF.PI * stack / stacks;
            for (int slice = 0; slice <= slices; slice++)
            {
                float theta = 2f * MathF.PI * slice / slices;
                var p = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Sin(phi) * MathF.Sin(theta), MathF.Cos(phi));
                AddVertex(vertices, p, p);
            }
        }

        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                uint a = (uint)(stack * (slices + 1) + slice);
                uint b = a + (uint)slices + 1;
                indices.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }

        return new Mesh(gl, vertices.ToArray(), indices.ToArray());
    }

    /// <summary>Unit-radius disc in the XY plane facing -Z.</summary>
    internal static Mesh CreateDisc(GL gl, int segments)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();
        var normal = -Vector3.UnitZ;

        AddVertex(vertices, Vector3.Zero, normal);
        for (int i = 0; i <= segments; i++)
        {
            float t = 2f * MathF.PI * i / segments;
            AddVertex(vertices, new Vector3(MathF.Cos(t), MathF.Sin(t), 0f), normal);
        }

        for (uint i = 1; i <= segments; i++)
            indices.AddRange(new[] { 0u, i, i + 1 });

        return new Mesh(gl, vertices.ToArray(), indices.ToArray());
    }

    /// <summary>Flat-shaded mesh in the shape's local space (vertices are not shared between faces).</summary>
    internal static Mesh FromPolyhedron(GL gl, PolyhedronPhysShape shape)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();
        var local = shape.LocalVertices;

        foreach (var face in shape.Faces)
        {
            uint first = (uint)(vertices.Count / FloatsPerVertex);
            foreach (int index in face.Indices)
                AddVertex(vertices, local[index], face.Normal);

            for (uint k = 1; k < face.Indices.Length - 1; k++)
                indices.AddRange(new[] { first, first + k, first + k + 1 });
        }

        return new Mesh(gl, vertices.ToArray(), indices.ToArray());
    }

    private static void AddVertex(List<float> vertices, Vector3 position, Vector3 normal)
    {
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        vertices.Add(normal.X);
        vertices.Add(normal.Y);
        vertices.Add(normal.Z);
    }
}
