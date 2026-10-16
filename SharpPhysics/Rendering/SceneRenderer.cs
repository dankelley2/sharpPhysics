#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Shapes;
using Silk.NET.OpenGL;
using Color = SFML.Graphics.Color;

namespace SharpPhysics.Rendering;

/// <summary>
/// Depth-tested OpenGL renderer for the 3D scene, running on SFML's window context.
/// Draw calls are only valid between <see cref="Begin"/> and <see cref="End"/>.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    // View-axis distances in pixels; a near plane well above zero keeps 24-bit depth precise at pixel scale.
    private const float NearPlane = 10f;
    private const float FarPadding = 5000f;

    // Points from the light toward the scene; -Z faces the default camera.
    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(0.3f, 0.5f, 1f));

    private const string VertexSource = """
        #version 330
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        uniform mat4 uModel;
        uniform mat4 uViewProjection;
        out vec3 vNormal;
        out vec3 vWorldPosition;
        void main()
        {
            vec4 world = uModel * vec4(aPosition, 1.0);
            vWorldPosition = world.xyz;
            vNormal = mat3(uModel) * aNormal;
            gl_Position = uViewProjection * world;
        }
        """;

    private const string FragmentSource = """
        #version 330
        in vec3 vNormal;
        in vec3 vWorldPosition;
        uniform vec4 uColor;
        uniform vec3 uLightDirection;
        uniform vec3 uCameraPosition;
        uniform float uSpecular;
        uniform float uUnlit;
        out vec4 fragColor;
        void main()
        {
            if (uUnlit > 0.5)
            {
                fragColor = uColor;
                return;
            }
            vec3 n = normalize(vNormal);
            vec3 toLight = -uLightDirection;
            vec3 toCamera = normalize(uCameraPosition - vWorldPosition);
            // Faces are drawn double-sided, so light whichever side faces the camera.
            if (dot(n, toCamera) < 0.0) n = -n;
            float diffuse = max(dot(n, toLight), 0.0);
            float specular = pow(max(dot(n, normalize(toLight + toCamera)), 0.0), 32.0) * uSpecular;
            fragColor = vec4(uColor.rgb * (0.45 + 0.55 * diffuse) + vec3(specular), uColor.a);
        }
        """;

    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _modelLocation;
    private readonly int _viewProjectionLocation;
    private readonly int _colorLocation;
    private readonly int _lightDirectionLocation;
    private readonly int _cameraPositionLocation;
    private readonly int _specularLocation;
    private readonly int _unlitLocation;

    private readonly Mesh _sphere;
    private readonly Mesh _cube;
    private readonly Mesh _disc;
    private readonly Dictionary<PolyhedronPhysShape, Mesh> _polyhedronMeshes = new();

    private readonly uint _lineVao;
    private readonly uint _lineVbo;
    private float[] _lineVertices = new float[64 * Mesh.FloatsPerVertex];

    public Camera3D Camera { get; }

    public SceneRenderer(Camera3D camera)
    {
        Camera = camera;
        _gl = GL.GetApi(SFML.Window.Context.GetFunction);

        _program = CreateProgram(VertexSource, FragmentSource);
        _modelLocation = _gl.GetUniformLocation(_program, "uModel");
        _viewProjectionLocation = _gl.GetUniformLocation(_program, "uViewProjection");
        _colorLocation = _gl.GetUniformLocation(_program, "uColor");
        _lightDirectionLocation = _gl.GetUniformLocation(_program, "uLightDirection");
        _cameraPositionLocation = _gl.GetUniformLocation(_program, "uCameraPosition");
        _specularLocation = _gl.GetUniformLocation(_program, "uSpecular");
        _unlitLocation = _gl.GetUniformLocation(_program, "uUnlit");

        _sphere = Mesh.CreateSphere(_gl, 32, 20);
        _cube = Mesh.FromPolyhedron(_gl, new BoxPhysShape(1, 1, 1));
        _disc = Mesh.CreateDisc(_gl, 32);

        _lineVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_lineVao);
        _lineVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
        Mesh.ConfigureVertexLayout(_gl);
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    #region Frame

    /// <summary>
    /// Prepares GL state for a scene pass over the given GameView rectangle.
    /// </summary>
    public void Begin(Vector2 viewCenter, Vector2 viewSize, uint windowWidth, uint windowHeight)
    {
        _gl.Viewport(0, 0, windowWidth, windowHeight);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(true);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _gl.UseProgram(_program);
        float far = Camera.Distance * 2f + FarPadding;
        SetMatrix(_viewProjectionLocation, Camera.GetViewProjection(viewCenter, viewSize, NearPlane, far));
        _gl.Uniform3(_lightDirectionLocation, LightDirection.X, LightDirection.Y, LightDirection.Z);
        var cameraPosition = Camera.Position;
        _gl.Uniform3(_cameraPositionLocation, cameraPosition.X, cameraPosition.Y, cameraPosition.Z);
    }

    /// <summary>
    /// Unbinds scene GL state. The caller must reset SFML's GL state before further SFML drawing.
    /// </summary>
    public void End()
    {
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.UseProgram(0);
        _gl.Disable(EnableCap.DepthTest);
    }

    #endregion

    #region Drawing

    public void DrawShape(IShape shape, Vector3 center, Quaternion orientation, Color color, float specular = 0.25f)
    {
        switch (shape)
        {
            case SpherePhysShape sphere:
                DrawSphere(center, sphere.Radius, color, specular);
                break;

            case BoxPhysShape box:
                DrawBox(center, new Vector3(box.Width, box.Height, box.Depth), orientation, color, specular);
                break;

            case PolyhedronPhysShape poly:
                DrawMesh(GetPolyhedronMesh(poly), Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(center), color, specular);
                break;

            case CompoundShape compound:
                for (int i = 0; i < compound.Children.Count; i++)
                {
                    var (childCenter, childOrientation) = compound.GetChildWorldTransform(i, center, orientation);
                    DrawShape(compound.Children[i].Shape, childCenter, childOrientation, color, specular);
                }
                break;
        }
    }

    public void DrawSphere(Vector3 center, float radius, Color color, float specular = 0.5f)
    {
        DrawMesh(_sphere, Matrix4x4.CreateScale(radius) * Matrix4x4.CreateTranslation(center), color, specular);
    }

    public void DrawBox(Vector3 center, Vector3 size, Quaternion orientation, Color color, float specular = 0.25f)
    {
        var model = Matrix4x4.CreateScale(size) * Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(center);
        DrawMesh(_cube, model, color, specular);
    }

    /// <summary>
    /// Draws a disc whose unrotated face lies in the XY plane facing -Z.
    /// </summary>
    public void DrawDisc(Vector3 center, float radius, Quaternion orientation, Color color)
    {
        var model = Matrix4x4.CreateScale(radius, radius, 1f) * Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(center);
        DrawMesh(_disc, model, color, 0f);
    }

    /// <summary>
    /// Draws unlit, depth-tested line segments from consecutive point pairs.
    /// </summary>
    public void DrawLines(ReadOnlySpan<Vector3> points, Color color)
    {
        if (points.Length < 2)
            return;

        int floatCount = points.Length * Mesh.FloatsPerVertex;
        if (_lineVertices.Length < floatCount)
            _lineVertices = new float[floatCount * 2];

        for (int i = 0; i < points.Length; i++)
        {
            int o = i * Mesh.FloatsPerVertex;
            _lineVertices[o] = points[i].X;
            _lineVertices[o + 1] = points[i].Y;
            _lineVertices[o + 2] = points[i].Z;
            _lineVertices[o + 3] = 0f;
            _lineVertices[o + 4] = 0f;
            _lineVertices[o + 5] = 0f;
        }

        _gl.BindVertexArray(_lineVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<float>(_lineVertices, 0, floatCount), BufferUsageARB.StreamDraw);

        SetMatrix(_modelLocation, Matrix4x4.Identity);
        SetColor(color);
        _gl.Uniform1(_unlitLocation, 1f);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)points.Length);
    }

    public void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        Span<Vector3> points = stackalloc Vector3[] { start, end };
        DrawLines(points, color);
    }

    private void DrawMesh(Mesh mesh, Matrix4x4 model, Color color, float specular)
    {
        SetMatrix(_modelLocation, model);
        SetColor(color);
        _gl.Uniform1(_specularLocation, specular);
        _gl.Uniform1(_unlitLocation, 0f);
        mesh.Draw();
    }

    #endregion

    #region Resources

    private Mesh GetPolyhedronMesh(PolyhedronPhysShape shape)
    {
        if (!_polyhedronMeshes.TryGetValue(shape, out var mesh))
        {
            mesh = Mesh.FromPolyhedron(_gl, shape);
            _polyhedronMeshes[shape] = mesh;
        }
        return mesh;
    }

    /// <summary>
    /// Frees meshes cached for general polyhedron shapes (e.g. when switching scenes).
    /// </summary>
    public void ReleaseShapeMeshes()
    {
        foreach (var mesh in _polyhedronMeshes.Values)
            mesh.Dispose();
        _polyhedronMeshes.Clear();
    }

    private unsafe void SetMatrix(int location, Matrix4x4 matrix)
    {
        // Row-major row-vector matrices read as column-major are already the GLSL column-vector form.
        _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
    }

    private void SetColor(Color color)
    {
        _gl.Uniform4(_colorLocation, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private uint CreateProgram(string vertexSource, string fragmentSource)
    {
        uint vertex = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException($"Scene shader link failed: {_gl.GetProgramInfoLog(program)}");

        _gl.DetachShader(program, vertex);
        _gl.DetachShader(program, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
            throw new InvalidOperationException($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    public void Dispose()
    {
        ReleaseShapeMeshes();
        _sphere.Dispose();
        _cube.Dispose();
        _disc.Dispose();
        _gl.DeleteVertexArray(_lineVao);
        _gl.DeleteBuffer(_lineVbo);
        _gl.DeleteProgram(_program);
        _gl.Dispose();
    }

    #endregion
}
