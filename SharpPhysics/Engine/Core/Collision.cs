#nullable enable
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SharpPhysics.Engine.Classes;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Core;

public static class Collision
{
    // Units are pixels and bodies range from ~10 px to 500+ px, so tolerances scale with the smaller body.
    private const float ContactToleranceFraction = 0.01f;
    private const float MinContactTolerance = 0.05f;
    // Edge-edge SAT axes must beat face axes by this margin, which keeps resting faces from jittering.
    private const float EdgeAxisBiasFraction = 0.002f;
    private const float MinEdgeAxisBias = 0.01f;

    [InlineArray(Manifold.MaxContactPoints)]
    private struct ContactPointBuffer
    {
        private Vector3 _element;
    }

    private struct Contact
    {
        public Vector3 Normal;
        public float Penetration;
        public ContactPointBuffer Points;
        public int Count;

        public void AddPoint(Vector3 point)
        {
            if (Count < Manifold.MaxContactPoints)
                Points[Count++] = point;
        }
    }

    public static bool AABBvsAABB(AABB a, AABB b)
    {
        if (a.Max.X < b.Min.X || a.Min.X > b.Max.X) return false;
        if (a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y) return false;
        if (a.Max.Z < b.Min.Z || a.Min.Z > b.Max.Z) return false;
        return true;
    }

    /// <summary>
    /// Narrow phase for <c>m.A</c> vs <c>m.B</c>. On hit, fills the normal (A to B), penetration and contact point.
    /// </summary>
    public static bool Detect(Manifold m)
    {
        if (m.A.Locked && m.B.Locked)
            return false;

        if (!TestShapes(m.A.Shape, m.A.Center, m.A.Orientation, m.B.Shape, m.B.Center, m.B.Orientation, out var contact))
            return false;

        m.Normal = contact.Normal;
        m.Penetration = contact.Penetration;
        m.ContactCount = contact.Count;

        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < contact.Count; i++)
        {
            m.ContactPoints[i] = contact.Points[i];
            sum += contact.Points[i];
        }
        m.ContactPoint = sum / contact.Count;
        return true;
    }

    #region Narrow Phase

    private static bool TestShapes(
        IShape a, Vector3 centerA, Quaternion orientA,
        IShape b, Vector3 centerB, Quaternion orientB,
        out Contact contact)
    {
        if (a is CompoundShape compoundA)
            return TestCompound(compoundA, centerA, orientA, b, centerB, orientB, out contact);

        if (b is CompoundShape compoundB)
        {
            bool hit = TestCompound(compoundB, centerB, orientB, a, centerA, orientA, out contact);
            contact.Normal = -contact.Normal;
            return hit;
        }

        switch (a, b)
        {
            case (SpherePhysShape sphereA, SpherePhysShape sphereB):
                return SphereVsSphere(centerA, sphereA.Radius, centerB, sphereB.Radius, out contact);

            case (PolyhedronPhysShape polyA, SpherePhysShape sphereB):
                return PolyhedronVsSphere(polyA, centerA, orientA, centerB, sphereB.Radius, out contact);

            case (SpherePhysShape sphereA, PolyhedronPhysShape polyB):
            {
                bool hit = PolyhedronVsSphere(polyB, centerB, orientB, centerA, sphereA.Radius, out contact);
                contact.Normal = -contact.Normal;
                return hit;
            }

            case (PolyhedronPhysShape polyA, PolyhedronPhysShape polyB):
                return PolyhedronVsPolyhedron(polyA, centerA, orientA, polyB, centerB, orientB, out contact);

            default:
                contact = default;
                return false;
        }
    }

    /// <summary>
    /// Tests every child of the compound against the other shape and keeps the deepest contact.
    /// The resulting normal points from the compound toward the other shape.
    /// </summary>
    private static bool TestCompound(
        CompoundShape compound, Vector3 center, Quaternion orientation,
        IShape other, Vector3 otherCenter, Quaternion otherOrientation,
        out Contact deepest)
    {
        deepest = default;
        bool any = false;
        AABB otherAabb = other.GetAABB(otherCenter, otherOrientation);

        for (int i = 0; i < compound.Children.Count; i++)
        {
            var childShape = compound.Children[i].Shape;
            var (childCenter, childOrientation) = compound.GetChildWorldTransform(i, center, orientation);

            if (!AABBvsAABB(childShape.GetAABB(childCenter, childOrientation), otherAabb))
                continue;

            if (TestShapes(childShape, childCenter, childOrientation, other, otherCenter, otherOrientation, out var contact) &&
                (!any || contact.Penetration > deepest.Penetration))
            {
                deepest = contact;
                any = true;
            }
        }

        return any;
    }

    private static bool SphereVsSphere(Vector3 centerA, float radiusA, Vector3 centerB, float radiusB, out Contact contact)
    {
        contact = default;
        Vector3 n = centerB - centerA;
        float radiusSum = radiusA + radiusB;
        float distSq = n.LengthSquared();

        if (distSq > radiusSum * radiusSum)
            return false;

        float d = MathF.Sqrt(distSq);
        contact.Normal = d > 1e-6f ? n / d : Vector3.UnitX;
        contact.Penetration = radiusSum - d;

        Vector3 surfaceA = centerA + contact.Normal * radiusA;
        Vector3 surfaceB = centerB - contact.Normal * radiusB;
        contact.AddPoint((surfaceA + surfaceB) * 0.5f);
        return true;
    }

    /// <summary>
    /// Normal points from the polyhedron toward the sphere.
    /// </summary>
    private static bool PolyhedronVsSphere(
        PolyhedronPhysShape poly, Vector3 polyCenter, Quaternion polyOrientation,
        Vector3 sphereCenter, float radius,
        out Contact contact)
    {
        contact = default;
        Vector3 local = PhysMath.InverseRotate(sphereCenter - polyCenter, polyOrientation);

        var faces = poly.Faces;
        float maxSeparation = float.MinValue;
        int bestFace = 0;
        for (int f = 0; f < faces.Count; f++)
        {
            float s = faces[f].SignedDistance(local);
            if (s > maxSeparation)
            {
                maxSeparation = s;
                bestFace = f;
            }
        }

        if (maxSeparation > radius)
            return false;

        Vector3 localNormal;
        Vector3 localPoint;
        float penetration;

        if (maxSeparation <= 0f)
        {
            // Center is inside: push out through the nearest face.
            localNormal = faces[bestFace].Normal;
            localPoint = local - localNormal * maxSeparation;
            penetration = radius - maxSeparation;
        }
        else
        {
            localPoint = ClosestPointOnSurface(poly, local);
            Vector3 delta = local - localPoint;
            float dist = delta.Length();
            if (dist > radius)
                return false;

            localNormal = dist > 1e-6f ? delta / dist : faces[bestFace].Normal;
            penetration = radius - dist;
        }

        contact.Normal = PhysMath.Rotate(localNormal, polyOrientation);
        contact.Penetration = penetration;
        contact.AddPoint(polyCenter + PhysMath.Rotate(localPoint, polyOrientation));
        return true;
    }

    private static Vector3 ClosestPointOnSurface(PolyhedronPhysShape poly, Vector3 localPoint)
    {
        float bestDistSq = float.MaxValue;
        Vector3 best = localPoint;

        foreach (var face in poly.Faces)
        {
            if (face.SignedDistance(localPoint) <= 0f)
                continue;

            Vector3 candidate = ClosestPointOnFace(poly, face, localPoint);
            float distSq = Vector3.DistanceSquared(candidate, localPoint);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = candidate;
            }
        }

        return best;
    }

    private static Vector3 ClosestPointOnFace(PolyhedronPhysShape poly, PolyhedronFace face, Vector3 p)
    {
        var vertices = poly.LocalVertices;
        var idx = face.Indices;
        Vector3 projected = p - face.Normal * face.SignedDistance(p);

        bool inside = true;
        for (int i = 0; i < idx.Length; i++)
        {
            Vector3 a = vertices[idx[i]];
            Vector3 b = vertices[idx[(i + 1) % idx.Length]];
            if (Vector3.Dot(Vector3.Cross(b - a, projected - a), face.Normal) < 0f)
            {
                inside = false;
                break;
            }
        }

        if (inside)
            return projected;

        float bestDistSq = float.MaxValue;
        Vector3 best = projected;
        for (int i = 0; i < idx.Length; i++)
        {
            Vector3 candidate = PhysMath.ClosestPointOnSegment(vertices[idx[i]], vertices[idx[(i + 1) % idx.Length]], p);
            float distSq = Vector3.DistanceSquared(candidate, p);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = candidate;
            }
        }
        return best;
    }

    /// <summary>
    /// Separating axis test over face normals of both shapes and cross products of their edge directions.
    /// </summary>
    private static bool PolyhedronVsPolyhedron(
        PolyhedronPhysShape a, Vector3 centerA, Quaternion orientA,
        PolyhedronPhysShape b, Vector3 centerB, Quaternion orientB,
        out Contact contact)
    {
        contact = default;
        Vector3[] vertsA = a.GetTransformedVertices(centerA, orientA);
        Vector3[] vertsB = b.GetTransformedVertices(centerB, orientB);

        float scale = MathF.Min(a.BoundingRadius, b.BoundingRadius);
        float edgeAxisBias = MathF.Max(MinEdgeAxisBias, EdgeAxisBiasFraction * scale);
        float contactTolerance = MathF.Max(MinContactTolerance, ContactToleranceFraction * scale);

        float minPenetration = float.MaxValue;
        Vector3 bestAxis = Vector3.Zero;

        foreach (var face in a.Faces)
        {
            if (!TestAxis(vertsA, vertsB, PhysMath.Rotate(face.Normal, orientA), 0f, ref minPenetration, ref bestAxis))
                return false;
        }

        foreach (var face in b.Faces)
        {
            if (!TestAxis(vertsA, vertsB, PhysMath.Rotate(face.Normal, orientB), 0f, ref minPenetration, ref bestAxis))
                return false;
        }

        foreach (var edgeA in a.EdgeDirections)
        {
            Vector3 worldEdgeA = PhysMath.Rotate(edgeA, orientA);
            foreach (var edgeB in b.EdgeDirections)
            {
                Vector3 axis = Vector3.Cross(worldEdgeA, PhysMath.Rotate(edgeB, orientB));
                float lengthSq = axis.LengthSquared();
                if (lengthSq < 1e-6f)
                    continue;

                if (!TestAxis(vertsA, vertsB, axis / MathF.Sqrt(lengthSq), edgeAxisBias, ref minPenetration, ref bestAxis))
                    return false;
            }
        }

        if (Vector3.Dot(centerB - centerA, bestAxis) < 0)
            bestAxis = -bestAxis;

        contact.Normal = bestAxis;
        contact.Penetration = minPenetration;
        AddContactPoints(ref contact, contactTolerance, a, centerA, orientA, vertsA, b, centerB, orientB, vertsB);
        return true;
    }

    private static bool TestAxis(Vector3[] vertsA, Vector3[] vertsB, Vector3 axis, float bias, ref float minPenetration, ref Vector3 bestAxis)
    {
        var (minA, maxA) = Project(vertsA, axis);
        var (minB, maxB) = Project(vertsB, axis);

        if (maxA < minB || maxB < minA)
            return false;

        float overlap = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);
        if (overlap + bias < minPenetration)
        {
            minPenetration = overlap;
            bestAxis = axis;
        }
        return true;
    }

    private static (float min, float max) Project(Vector3[] vertices, Vector3 axis)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (var v in vertices)
        {
            float d = Vector3.Dot(v, axis);
            if (d < min) min = d;
            if (d > max) max = d;
        }
        return (min, max);
    }

    /// <summary>
    /// Uses the vertices of each shape that lie inside the other as contact points. Falls back to the
    /// midpoint of the two closest features (e.g. edge-edge contact) when no vertices are inside.
    /// </summary>
    private static void AddContactPoints(
        ref Contact contact, float contactTolerance,
        PolyhedronPhysShape a, Vector3 centerA, Quaternion orientA, Vector3[] vertsA,
        PolyhedronPhysShape b, Vector3 centerB, Quaternion orientB, Vector3[] vertsB)
    {
        float tolerance = contact.Penetration + contactTolerance;

        foreach (var v in vertsA)
        {
            if (b.ContainsLocal(PhysMath.InverseRotate(v - centerB, orientB), tolerance))
                contact.AddPoint(v);
        }

        foreach (var v in vertsB)
        {
            if (a.ContainsLocal(PhysMath.InverseRotate(v - centerA, orientA), tolerance))
                contact.AddPoint(v);
        }

        if (contact.Count == 0)
        {
            contact.AddPoint((SupportFeatureCentroid(vertsA, contact.Normal, contactTolerance) +
                              SupportFeatureCentroid(vertsB, -contact.Normal, contactTolerance)) * 0.5f);
        }
    }

    private static Vector3 SupportFeatureCentroid(Vector3[] vertices, Vector3 direction, float tolerance)
    {
        float max = float.MinValue;
        foreach (var v in vertices)
            max = MathF.Max(max, Vector3.Dot(v, direction));

        Vector3 sum = Vector3.Zero;
        int count = 0;
        foreach (var v in vertices)
        {
            if (Vector3.Dot(v, direction) >= max - tolerance)
            {
                sum += v;
                count++;
            }
        }
        return sum / count;
    }

    #endregion

    #region Resolution

    public static void ResolveCollision(Manifold m)
    {
        // Every point is solved against the same pre-impulse velocities so no point is favored by order.
        int count = m.ContactCount;
        Span<Vector3> impulses = stackalloc Vector3[Manifold.MaxContactPoints];
        for (int i = 0; i < count; i++)
            impulses[i] = ComputeContactImpulse(m.A, m.B, m.Normal, m.ContactPoints[i]) / count;

        for (int i = 0; i < count; i++)
        {
            Vector3 point = m.ContactPoints[i];
            m.A.ApplyImpulse(-impulses[i], point - m.A.Center);
            m.B.ApplyImpulse(impulses[i], point - m.B.Center);
        }
    }

    /// <summary>
    /// Combined normal and Coulomb friction impulse applied to B at the contact point (A receives the opposite).
    /// </summary>
    private static Vector3 ComputeContactImpulse(PhysicsObject a, PhysicsObject b, Vector3 n, Vector3 contactPoint)
    {
        Vector3 rA = contactPoint - a.Center;
        Vector3 rB = contactPoint - b.Center;
        Vector3 relativeVelocity = b.GetVelocityAtPoint(contactPoint) - a.GetVelocityAtPoint(contactPoint);

        float velAlongNormal = Vector3.Dot(relativeVelocity, n);
        if (velAlongNormal > 0)
            return Vector3.Zero;

        float invMassSum = a.InverseMassAlong(rA, n) + b.InverseMassAlong(rB, n);
        if (invMassSum <= 0f)
            return Vector3.Zero;

        float e = Math.Min(a.Restitution, b.Restitution);
        float j = -(1 + e) * velAlongNormal / invMassSum;
        Vector3 impulse = n * j;

        Vector3 tangent = relativeVelocity - n * velAlongNormal;
        if (tangent.LengthSquared() <= 0.0001f)
            return impulse;
        tangent = Vector3.Normalize(tangent);

        float invMassSumFriction = a.InverseMassAlong(rA, tangent) + b.InverseMassAlong(rB, tangent);
        if (invMassSumFriction <= 0f)
            return impulse;

        float mu = Math.Max(a.Friction, b.Friction);
        float jt = Math.Min(Vector3.Dot(relativeVelocity, tangent) / invMassSumFriction, mu * j);
        return impulse - tangent * jt;
    }

    public static void PositionalCorrection(Manifold m)
    {
        const float percent = 0.6f;
        const float slop = 0.05f;

        float invMassA = m.A.EffectiveIMass;
        float invMassB = m.B.EffectiveIMass;
        float invMassSum = invMassA + invMassB;
        if (invMassSum <= 0f)
            return;

        float penetration = Math.Max(m.Penetration - slop, 0.0f);
        Vector3 correction = m.Normal * (penetration / invMassSum * percent);

        m.A.Move(-correction * invMassA);
        m.B.Move(correction * invMassB);
    }

    public static void AngularPositionalCorrection(Manifold m)
    {
        const float angularCorrectionPercent = 0.01f;

        ApplyAngularCorrection(m.A, m.ContactPoint - m.A.Center, m.Normal, -angularCorrectionPercent * m.Penetration);
        ApplyAngularCorrection(m.B, m.ContactPoint - m.B.Center, m.Normal, angularCorrectionPercent * m.Penetration);
    }

    private static void ApplyAngularCorrection(PhysicsObject obj, Vector3 r, Vector3 normal, float scaledPenetration)
    {
        float rLengthSq = r.LengthSquared();
        if (obj.Locked || !obj.CanRotate || rLengthSq <= 0.0001f)
            return;

        Vector3 axis = Vector3.Cross(r, normal);
        float axisLengthSq = axis.LengthSquared();
        if (axisLengthSq < 1e-8f)
            return;

        // Farther contacts need less rotation to resolve the same penetration.
        float angle = scaledPenetration / MathF.Sqrt(rLengthSq);
        obj.RotateBy(Quaternion.CreateFromAxisAngle(axis / MathF.Sqrt(axisLengthSq), angle));
    }

    #endregion
}
