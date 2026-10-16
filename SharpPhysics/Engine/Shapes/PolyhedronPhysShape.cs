#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Shapes;

/// <summary>
/// A planar face of a convex polyhedron in local space.
/// </summary>
public readonly struct PolyhedronFace
{
    /// <summary>Outward unit normal.</summary>
    public Vector3 Normal { get; }
    /// <summary>Plane offset such that Dot(Normal, p) == Offset for points on the face.</summary>
    public float Offset { get; }
    /// <summary>Vertex indices, wound counter-clockwise around <see cref="Normal"/>.</summary>
    public int[] Indices { get; }

    public PolyhedronFace(Vector3 normal, float offset, int[] indices)
    {
        Normal = normal;
        Offset = offset;
        Indices = indices;
    }

    public float SignedDistance(Vector3 localPoint) => Vector3.Dot(Normal, localPoint) - Offset;
}

/// <summary>
/// Convex polyhedron formed from the convex hull of a point set.
/// Vertices are re-centered so the local origin is the center of mass.
/// </summary>
public class PolyhedronPhysShape : IShape
{
    private readonly Vector3[] _vertices;
    private readonly PolyhedronFace[] _faces;
    private readonly (int A, int B)[] _edges;
    private readonly Vector3[] _edgeDirections;
    private readonly float _volume;
    private readonly Mat3 _unitInertia;

    private Vector3[]? _cachedTransformedVertices;
    private Vector3 _cachedCenter;
    private Quaternion _cachedOrientation;

    public virtual ShapeTypeEnum ShapeType => ShapeTypeEnum.Polyhedron;

    public IReadOnlyList<Vector3> LocalVertices => _vertices;
    public IReadOnlyList<PolyhedronFace> Faces => _faces;
    public IReadOnlyList<(int A, int B)> Edges => _edges;
    /// <summary>Unique local edge directions (up to sign), used as SAT axes.</summary>
    public IReadOnlyList<Vector3> EdgeDirections => _edgeDirections;
    /// <summary>Distance from the center of mass to the farthest vertex.</summary>
    public float BoundingRadius { get; }

    public PolyhedronPhysShape(IEnumerable<Vector3> points)
    {
        (_vertices, _faces) = BuildHull(points);

        var (volume, centroid) = ComputeVolumeAndCentroid(_vertices, _faces);
        for (int i = 0; i < _vertices.Length; i++)
            _vertices[i] -= centroid;
        for (int i = 0; i < _faces.Length; i++)
        {
            var face = _faces[i];
            _faces[i] = new PolyhedronFace(face.Normal, face.Offset - Vector3.Dot(face.Normal, centroid), face.Indices);
        }

        _volume = volume;
        BoundingRadius = _vertices.Max(v => v.Length());
        _unitInertia = ComputeUnitInertia(_vertices, _faces, volume);
        (_edges, _edgeDirections) = BuildEdges(_vertices, _faces);
    }

    public AABB GetAABB(Vector3 center, Quaternion orientation)
    {
        var vertices = GetTransformedVertices(center, orientation);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        return new AABB { Min = min, Max = max };
    }

    public float GetVolume() => _volume;

    public Mat3 GetInertiaTensor(float mass) => _unitInertia * mass;

    public bool Contains(Vector3 point, Vector3 center, Quaternion orientation)
    {
        return ContainsLocal(PhysMath.InverseRotate(point - center, orientation), 0f);
    }

    /// <summary>
    /// Tests a local-space point against all face planes, inflated by <paramref name="tolerance"/>.
    /// </summary>
    public bool ContainsLocal(Vector3 localPoint, float tolerance)
    {
        foreach (var face in _faces)
        {
            if (face.SignedDistance(localPoint) > tolerance)
                return false;
        }
        return true;
    }

    public Vector3[] GetTransformedVertices(Vector3 center, Quaternion orientation)
    {
        if (_cachedTransformedVertices != null && _cachedCenter == center && _cachedOrientation == orientation)
            return _cachedTransformedVertices;

        var transformed = new Vector3[_vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
            transformed[i] = center + PhysMath.Rotate(_vertices[i], orientation);

        _cachedTransformedVertices = transformed;
        _cachedCenter = center;
        _cachedOrientation = orientation;
        return transformed;
    }

    #region Hull Construction

    // Brute-force hull: every vertex triple whose plane has all points on one side is a face.
    // O(n^4), which is fine for the small vertex counts used by rigid bodies.
    private static (Vector3[] vertices, PolyhedronFace[] faces) BuildHull(IEnumerable<Vector3> input)
    {
        var raw = input.ToList();
        if (raw.Count < 4)
            throw new ArgumentException("A polyhedron requires at least 4 vertices.", nameof(input));

        Vector3 average = Vector3.Zero;
        foreach (var p in raw)
            average += p;
        average /= raw.Count;

        float extent = MathF.Max(raw.Max(p => Vector3.Distance(p, average)), 1e-3f);
        float tolerance = 1e-4f * MathF.Max(extent, 1f);

        var points = new List<Vector3>();
        foreach (var p in raw)
        {
            if (!points.Any(q => Vector3.DistanceSquared(p, q) <= tolerance * tolerance))
                points.Add(p);
        }

        int n = points.Count;
        var planes = new List<(Vector3 normal, float offset)>();
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        for (int k = j + 1; k < n; k++)
        {
            Vector3 normal = Vector3.Cross(points[j] - points[i], points[k] - points[i]);
            float length = normal.Length();
            if (length <= tolerance * extent)
                continue;

            normal /= length;
            float offset = Vector3.Dot(normal, points[i]);

            bool anyAbove = false, anyBelow = false;
            foreach (var p in points)
            {
                float d = Vector3.Dot(normal, p) - offset;
                if (d > tolerance) anyAbove = true;
                else if (d < -tolerance) anyBelow = true;
                if (anyAbove && anyBelow) break;
            }
            if (anyAbove && anyBelow)
                continue;

            if (anyAbove)
            {
                normal = -normal;
                offset = -offset;
            }

            bool duplicate = planes.Any(pl =>
                Vector3.Dot(pl.normal, normal) > 0.9f &&
                MathF.Abs(Vector3.Dot(pl.normal, points[i]) - pl.offset) <= tolerance &&
                MathF.Abs(Vector3.Dot(pl.normal, points[j]) - pl.offset) <= tolerance &&
                MathF.Abs(Vector3.Dot(pl.normal, points[k]) - pl.offset) <= tolerance);
            if (!duplicate)
                planes.Add((normal, offset));
        }

        if (planes.Count < 4)
            throw new ArgumentException("Vertices must span a non-degenerate 3D volume.", nameof(input));

        var faceLoops = new List<List<int>>(planes.Count);
        foreach (var (normal, offset) in planes)
        {
            var onPlane = Enumerable.Range(0, n)
                .Where(idx => MathF.Abs(Vector3.Dot(normal, points[idx]) - offset) <= tolerance)
                .ToList();
            faceLoops.Add(SortCounterClockwise(onPlane, points, normal));
        }

        // Drop interior points and re-index.
        var used = faceLoops.SelectMany(f => f).Distinct().OrderBy(i => i).ToList();
        var remap = new Dictionary<int, int>(used.Count);
        for (int i = 0; i < used.Count; i++)
            remap[used[i]] = i;

        var vertices = used.Select(i => points[i]).ToArray();
        var faces = new PolyhedronFace[planes.Count];
        for (int f = 0; f < planes.Count; f++)
            faces[f] = new PolyhedronFace(planes[f].normal, planes[f].offset, faceLoops[f].Select(i => remap[i]).ToArray());

        return (vertices, faces);
    }

    private static List<int> SortCounterClockwise(List<int> indices, List<Vector3> points, Vector3 normal)
    {
        Vector3 center = Vector3.Zero;
        foreach (var i in indices)
            center += points[i];
        center /= indices.Count;

        Vector3 u = Vector3.Normalize(points[indices[0]] - center);
        Vector3 v = Vector3.Cross(normal, u);

        return indices
            .OrderBy(i =>
            {
                Vector3 d = points[i] - center;
                return MathF.Atan2(Vector3.Dot(d, v), Vector3.Dot(d, u));
            })
            .ToList();
    }

    private static ((int A, int B)[] edges, Vector3[] directions) BuildEdges(Vector3[] vertices, PolyhedronFace[] faces)
    {
        var edges = new HashSet<(int, int)>();
        foreach (var face in faces)
        {
            var idx = face.Indices;
            for (int i = 0; i < idx.Length; i++)
            {
                int a = idx[i], b = idx[(i + 1) % idx.Length];
                edges.Add(a < b ? (a, b) : (b, a));
            }
        }

        var directions = new List<Vector3>();
        foreach (var (a, b) in edges)
        {
            Vector3 dir = Vector3.Normalize(vertices[b] - vertices[a]);
            if (!directions.Any(d => MathF.Abs(Vector3.Dot(d, dir)) > 1f - 1e-5f))
                directions.Add(dir);
        }

        return (edges.ToArray(), directions.ToArray());
    }

    #endregion

    #region Mass Properties

    // Volume and centroid via signed tetrahedra fanned from an interior reference point.
    private static (float volume, Vector3 centroid) ComputeVolumeAndCentroid(Vector3[] vertices, PolyhedronFace[] faces)
    {
        Vector3 reference = Vector3.Zero;
        foreach (var v in vertices)
            reference += v;
        reference /= vertices.Length;

        float volume = 0f;
        Vector3 weighted = Vector3.Zero;
        foreach (var face in faces)
        {
            var idx = face.Indices;
            for (int k = 1; k < idx.Length - 1; k++)
            {
                Vector3 a = vertices[idx[0]] - reference;
                Vector3 b = vertices[idx[k]] - reference;
                Vector3 c = vertices[idx[k + 1]] - reference;
                float tetVolume = Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
                volume += tetVolume;
                weighted += tetVolume * (a + b + c) / 4f;
            }
        }

        return (volume, reference + weighted / volume);
    }

    // Inertia per unit mass about the origin (assumed to be the centroid), using the
    // tetrahedron covariance method (Blow & Binstock).
    private static Mat3 ComputeUnitInertia(Vector3[] vertices, PolyhedronFace[] faces, float volume)
    {
        Mat3 covariance = Mat3.Zero;
        foreach (var face in faces)
        {
            var idx = face.Indices;
            for (int k = 1; k < idx.Length - 1; k++)
            {
                Vector3 a = vertices[idx[0]];
                Vector3 b = vertices[idx[k]];
                Vector3 c = vertices[idx[k + 1]];
                Vector3 s = a + b + c;
                float det = Vector3.Dot(a, Vector3.Cross(b, c));
                covariance += (Mat3.OuterProduct(a, a) + Mat3.OuterProduct(b, b) + Mat3.OuterProduct(c, c) + Mat3.OuterProduct(s, s)) * (det / 120f);
            }
        }

        Mat3 inertia = Mat3.Identity * covariance.Trace - covariance;
        return inertia * (1f / volume);
    }

    #endregion
}
