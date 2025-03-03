﻿using System;
using System.Collections.Generic;
using physics.Engine.Classes;
using physics.Engine.Extensions;
using physics.Engine.Helpers;
using physics.Engine.Objects;
using physics.Engine.Shapes;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine
{
    public static class Collision
    {

        public static bool AABBvsAABB(AABB a, AABB b)
        {
            // Exit with no intersection if found separated along an axis
            if (a.Max.X < b.Min.X || a.Min.X > b.Max.X) return false;
            if (a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y) return false;

            // No separating axis found, therefor there is at least one overlapping axis
            return true;
        }

        public static bool PolygonVsPolygon(ref Manifold m)
        {
            /*
            * 1) Get the shapes (both assumed to be “polygons” here).
            *    - If a shape is BoxPhysShape, you can generate the 4 corners via CollisionHelpers.GetRectangleCorners.
            *    - If a shape is PolygonPhysShape, create a method GetTransformedVertices(PhysicsObject) that returns
            *      all vertices in world space.
            */
            var A = m.A;
            var B = m.B;

            // Safety check: if both objects are locked, no need to resolve collision
            if (A.Locked && B.Locked)
                return false;

            // Collect polygon vertices in *world space*.
            // For example, if your shapes are BoxPhysShape, you can do:
            //   List<Vector2f> polyA = CollisionHelpers.GetRectangleCorners(A);
            //   List<Vector2f> polyB = CollisionHelpers.GetRectangleCorners(B);
            // For a general PolygonPhysShape, you might do polygonShape.GetTransformedVertices(A.Center, A.Angle), etc.
            List<Vector2f> polyA = GetWorldVertices(A);
            List<Vector2f> polyB = GetWorldVertices(B);

            // The overall penetration and normal (to fill into the manifold).
            float minPenetration = float.MaxValue;
            Vector2f bestAxis = new Vector2f();

            /*
            * 2) For SAT, we must:
            *    - Take every edge of polygon A, compute its normal,
            *      project both polygons onto that normal, and check for overlap.
            *    - Repeat for every edge of polygon B.
            *    - If any projection does not overlap, return false (no collision).
            *    - Otherwise, find the minimum overlap of all tested axes. That overlap is our final penetration,
            *      and the corresponding axis is our collision normal.
            */

            // Check edges from A
            for (int i = 0; i < polyA.Count; i++)
            {
                int next = (i + 1) % polyA.Count;
                // Edge = current -> next
                Vector2f edge = polyA[next] - polyA[i];
                // Normal = perpendicular; you can do (-edge.Y, edge.X)
                Vector2f axis = new Vector2f(-edge.Y, edge.X).Normalize();

                // Project both polygons onto 'axis'
                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            // Check edges from B
            for (int i = 0; i < polyB.Count; i++)
            {
                int next = (i + 1) % polyB.Count;
                // Edge = current -> next
                Vector2f edge = polyB[next] - polyB[i];
                // Normal = perpendicular
                Vector2f axis = new Vector2f(-edge.Y, edge.X).Normalize();

                // Project both polygons onto 'axis'
                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            // After you finalize bestAxis and minPenetration, ensure the normal points from A to B.
            Vector2f centerDiff = B.Center - A.Center;
            if (PhysMath.Dot(centerDiff, bestAxis) < 0)
            {
                bestAxis = -bestAxis;
            }

            // If we reach this point, there is a collision.
            m.Normal = bestAxis;
            m.Penetration = minPenetration;

            // Approximate contact point: You can do a midpoint between centers as a fallback:
            m.ContactPoint = (A.Center + B.Center) * 0.5f;

            A.LastContactPoint = m.ContactPoint;
            B.LastContactPoint = m.ContactPoint;

            // If you want a more accurate contact point, you can do:
            //   CollisionHelpers.UpdateContactPoint(ref m);
            // (which uses Sutherland-Hodgman clipping to find the intersection polygon)

            return true;
        }

        /*
        * Example helper to project two polygons onto the given axis and check for overlap.
        * If there is an overlap, we return true; otherwise, false. This also updates the minimum
        * penetration depth and best-axis if the new overlap is smaller.
        */
        private static bool ProjectAndCheckOverlap(
            List<Vector2f> polyA,
            List<Vector2f> polyB,
            Vector2f axis,
            ref float minPenetration,
            ref Vector2f bestAxis)
        {
            // 1) Project polygon A
            (float minA, float maxA) = ProjectPolygon(polyA, axis);
            // 2) Project polygon B
            (float minB, float maxB) = ProjectPolygon(polyB, axis);

            // 3) Check for gap
            if (maxA < minB || maxB < minA)
                return false; // No overlap => no collision

            // 4) Overlap distance = min(maxA, maxB) - max(minA, minB)
            float overlap = Math.Min(maxA, maxB) - Math.Max(minA, minB);

            // Track the smallest overlap (for the final collision normal)
            if (overlap < minPenetration)
            {
                minPenetration = overlap;
                // Ensure the normal points from A to B (optional consistency)
                // You can check the direction by comparing centers or by sign of Dot
                bestAxis = axis;
            }

            return true;
        }

        /*
        * Projects all vertices of a polygon onto 'axis' and returns (min, max) scalar values.
        */
        private static (float min, float max) ProjectPolygon(List<Vector2f> poly, Vector2f axis)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (var p in poly)
            {
                float dot = p.X * axis.X + p.Y * axis.Y; // Dot product
                if (dot < min) min = dot;
                if (dot > max) max = dot;
            }
            return (min, max);
        }

        /*
        * Example helper to retrieve a shape's vertices in world space. 
        * For a BoxPhysShape, you can reuse CollisionHelpers.GetRectangleCorners.
        * For a PolygonPhysShape, you might store a local List<Vector2f> and transform each by center + rotation.
        */
        private static List<Vector2f> GetWorldVertices(PhysicsObject obj)
        {
            return obj.Shape.GetTransformedVertices(obj.Center, obj.Angle);
        }


        public static bool BoxVsBox(ref Manifold m)
        {
            var A = m.A;
            var B = m.B;

            if (A.Locked && B.Locked)
                return false;

            // Compute half extents for each box.
            float a_halfX = A.Shape.GetWidth() / 2f;
            float a_halfY = A.Shape.GetHeight() / 2f;
            float b_halfX = B.Shape.GetWidth() / 2f;
            float b_halfY = B.Shape.GetHeight() / 2f;

            // Compute the local axes for each box.
            // For object A:
            Vector2f A_axis0 = new Vector2f((float)Math.Cos(A.Angle), (float)Math.Sin(A.Angle));
            Vector2f A_axis1 = new Vector2f(-A_axis0.Y, A_axis0.X);
            // For object B:
            Vector2f B_axis0 = new Vector2f((float)Math.Cos(B.Angle), (float)Math.Sin(B.Angle));
            Vector2f B_axis1 = new Vector2f(-B_axis0.Y, B_axis0.X);

            // Compute the rotation matrix R where R[i][j] =PhysMath.Dot(A_axis[i], B_axis[j])
            float R00 = PhysMath.Dot(A_axis0, B_axis0);
            float R01 =PhysMath.Dot(A_axis0, B_axis1);
            float R10 =PhysMath.Dot(A_axis1, B_axis0);
            float R11 =PhysMath.Dot(A_axis1, B_axis1);

            // Compute translation vector t from A's center to B's center and
            // express it in A's local coordinate system.
            Vector2f t = B.Center - A.Center;
            Vector2f tA = new Vector2f(PhysMath.Dot(t, A_axis0),PhysMath.Dot(t, A_axis1));

            float penetration = float.MaxValue;
            Vector2f bestAxis = new Vector2f();

            // Test axis A_axis0.
            {
                float ra = a_halfX;
                float rb = b_halfX * Math.Abs(R00) + b_halfY * Math.Abs(R01);
                float overlap = (ra + rb) - Math.Abs(tA.X);
                if (overlap < 0)
                    return false;
                if (overlap < penetration)
                {
                    penetration = overlap;
                    bestAxis = A_axis0 * (tA.X < 0 ? -1 : 1);
                }
            }

            // Test axis A_axis1.
            {
                float ra = a_halfY;
                float rb = b_halfX * Math.Abs(R10) + b_halfY * Math.Abs(R11);
                float overlap = (ra + rb) - Math.Abs(tA.Y);
                if (overlap < 0)
                    return false;
                if (overlap < penetration)
                {
                    penetration = overlap;
                    bestAxis = A_axis1 * (tA.Y < 0 ? -1 : 1);
                }
            }

            // Compute t expressed in B's coordinate system.
            Vector2f tB = new Vector2f(PhysMath.Dot(t, B_axis0),PhysMath.Dot(t, B_axis1));

            // Test axis B_axis0.
            {
                float ra = a_halfX * Math.Abs(R00) + a_halfY * Math.Abs(R10);
                float rb = b_halfX;
                float overlap = (ra + rb) - Math.Abs(tB.X);
                if (overlap < 0)
                    return false;
                if (overlap < penetration)
                {
                    penetration = overlap;
                    // Flip the axis so it points from A to B.
                    bestAxis = B_axis0 * (tB.X < 0 ? -1 : 1);
                }
            }

            // Test axis B_axis1.
            {
                float ra = a_halfX * Math.Abs(R01) + a_halfY * Math.Abs(R11);
                float rb = b_halfY;
                float overlap = (ra + rb) - Math.Abs(tB.Y);
                if (overlap < 0)
                    return false;
                if (overlap < penetration)
                {
                    penetration = overlap;
                    bestAxis = B_axis1 * (tB.Y < 0 ? -1 : 1);
                }
            }

            // If we get here, there is an intersection.
            m.Normal = bestAxis;
            m.Penetration = penetration;
            // Set a simple collision point as the midpoint between the centers.
            m.ContactPoint = (A.Center + B.Center) * 0.5f;

            m.A.LastContactPoint = m.ContactPoint;
            m.B.LastContactPoint = m.ContactPoint;
            return true;
        }

        public static bool CirclevsCircle(ref Manifold m)
        {
            PhysicsObject A = m.A;
            PhysicsObject B = m.B;

            // Ensure both objects are circles.
            CirclePhysShape circleA = A.Shape as CirclePhysShape;
            CirclePhysShape circleB = B.Shape as CirclePhysShape;
            if (circleA == null || circleB == null)
            {
                throw new ArgumentException("CirclevsCircle requires both objects to have a CircleShape.");
            }

            // Vector from A to B.
            Vector2f n = B.Center - A.Center;

            // Radii of the circles.
            float rA = circleA.Radius;
            float rB = circleB.Radius;
            float radiusSum = rA + rB;

            // Early out if circles are not colliding.
            if (n.LengthSquared() > radiusSum * radiusSum)
            {
                return false;
            }

            // Compute the distance between circle centers.
            float d = n.Length();

            if (d != 0)
            {
                // Penetration is the difference between the sum of the radii and the distance.
                m.Penetration = radiusSum - d;
                // The collision normal is the normalized vector from A to B.
                m.Normal = n / d;

                // Compute contact points on each circle's perimeter along the collision normal.
                Vector2f contactA = A.Center + m.Normal * rA;
                Vector2f contactB = B.Center - m.Normal * rB;
                m.ContactPoint = (contactA + contactB) * 0.5f;

                // Store the contact point for both objects.
                A.LastContactPoint = m.ContactPoint;
                B.LastContactPoint = m.ContactPoint;

                return true;
            }
            else
            {
                // If the circles are at the same position, choose an arbitrary normal and contact point.
                m.Penetration = rA;
                m.Normal = new Vector2f(1, 0);
                m.ContactPoint = A.Center;
                return true;
            }
        }

        public static bool BoxVsCircle(ref Manifold m)
        {
            // m.A is the box and m.B is the circle.
            PhysicsObject boxObj = m.A;
            PhysicsObject circleObj = m.B;

            if (!(boxObj.Shape is BoxPhysShape boxShape))
                throw new ArgumentException("AABBvsCircle requires m.A to have a BoxPhysShape.");
            if (!(circleObj.Shape is CirclePhysShape circleShape))
                throw new ArgumentException("AABBvsCircle requires m.B to have a CirclePhysShape.");

            // Calculate half extents of the box.
            float x_extent = boxShape.Width / 2f;
            float y_extent = boxShape.Height / 2f;

            // Translate the circle center into the box's local space.
            // Step 1: Get vector from box center to circle center.
            Vector2f circleToBox = circleObj.Center - boxObj.Center;
            // Step 2: Rotate that vector by -box.Angle.
            float cos = (float)Math.Cos(-boxObj.Angle);
            float sin = (float)Math.Sin(-boxObj.Angle);
            Vector2f circleLocal = new Vector2f(
                 circleToBox.X * cos - circleToBox.Y * sin,
                 circleToBox.X * sin + circleToBox.Y * cos
            );

            // Find the closest point in the box (in local space) to the circle's center.
            Vector2f closestLocal = new Vector2f(
                 PhysMath.Clamp(-x_extent, x_extent, circleLocal.X),
                 PhysMath.Clamp(-y_extent, y_extent, circleLocal.Y)
            );

            bool inside = false;
            // If the circle's local center is inside the box, then no clamping occurred.
            if (circleLocal.X == closestLocal.X && circleLocal.Y == closestLocal.Y)
            {
                inside = true;
                // Push the closest point to the nearest box edge.
                if (Math.Abs(circleLocal.X) < Math.Abs(circleLocal.Y))
                {
                    closestLocal.X = (circleLocal.X > 0) ? x_extent : -x_extent;
                }
                else
                {
                    closestLocal.Y = (circleLocal.Y > 0) ? y_extent : -y_extent;
                }
            }

            // Compute the difference between the circle's local center and the closest point.
            Vector2f diffLocal = circleLocal - closestLocal;
            float dSquared = diffLocal.LengthSquared();
            float r = circleShape.Radius;

            // If there's no collision (and the circle's center isn't inside), early out.
            if (dSquared > r * r && !inside)
                return false;

            float d = (dSquared > 0) ? (float)Math.Sqrt(dSquared) : 0f;

            Vector2f normalLocal;
            if (inside)
            {
                // When inside, the normal should point from the box toward the circle.
                normalLocal = (-diffLocal).Normalize();
                m.Penetration = r - d;
            }
            else
            {
                normalLocal = diffLocal.Normalize();
                m.Penetration = r - d;
            }

            // Transform the collision normal back into world space using the box's rotation.
            cos = (float)Math.Cos(boxObj.Angle);
            sin = (float)Math.Sin(boxObj.Angle);
            Vector2f normalWorld = new Vector2f(
                 normalLocal.X * cos - normalLocal.Y * sin,
                 normalLocal.X * sin + normalLocal.Y * cos
            );
            m.Normal = normalWorld;

            // Set the contact point on the circle's perimeter along the collision normal.
            m.ContactPoint = circleObj.Center - m.Normal * r;

            // Set the LastContactPoint for both box and circle.
            boxObj.LastContactPoint = m.ContactPoint;
            circleObj.LastContactPoint = m.ContactPoint;

            return true;
        }

        public static void ResolveCollisionRotational(ref Manifold m)
        {
            // Retrieve the two physics objects.
            PhysicsObject A = m.A;
            PhysicsObject B = m.B;

            // For each object, if it's rotational, get its angular velocity and inverse inertia; otherwise, treat as zero.
            float angularVelA = A.CanRotate ? A.AngularVelocity : 0F;
            float iInertiaA =   A.CanRotate ? A.IInertia        : 0F;
            float angularVelB = B.CanRotate ? B.AngularVelocity : 0F;
            float iInertiaB =   B.CanRotate ? B.IInertia        : 0F;

            // Compute vectors from centers to contact point.
            Vector2f rA = m.ContactPoint - A.Center;
            Vector2f rB = m.ContactPoint - B.Center;

            // Compute the relative velocity at the contact point (including any rotational contribution).
            Vector2f vA_contact = A.Velocity + PhysMath.Perpendicular(rA) * angularVelA;
            Vector2f vB_contact = B.Velocity + PhysMath.Perpendicular(rB) * angularVelB;
            Vector2f relativeVelocity = vB_contact - vA_contact;

            float velAlongNormal = Extensions.Extensions.DotProduct(relativeVelocity, m.Normal);
            if (velAlongNormal > 0)
                return;

            float e = Math.Min(A.Restitution, B.Restitution);

            // Compute cross products for the normal.
            float rA_cross_N = PhysMath.Cross(rA, m.Normal);
            float rB_cross_N = PhysMath.Cross(rB, m.Normal);

            // Denominator includes linear inertia plus rotational contributions.
            float invMassSum = A.IMass + B.IMass +
                               (rA_cross_N * rA_cross_N) * iInertiaA +
                               (rB_cross_N * rB_cross_N) * iInertiaB;

            float j = -(1 + e) * velAlongNormal;
            j /= invMassSum;

            Vector2f impulse = m.Normal * j;

            if (!A.Locked)
            {
                A.Velocity -= impulse * A.IMass;
                if (A.CanRotate)
                {
                    A.AngularVelocity -= PhysMath.Cross(rA, impulse) * iInertiaA;
                }
            }
            if (!B.Locked)
            {
                B.Velocity += impulse * B.IMass;
                if (B.CanRotate)
                {
                    B.AngularVelocity += PhysMath.Cross(rB, impulse) * iInertiaB;
                }
            }

            // --- Friction impulse ---
            Vector2f tangent = relativeVelocity - m.Normal * Extensions.Extensions.DotProduct(relativeVelocity, m.Normal);
            if (tangent.LengthSquared() > 0.0001f)
                tangent = tangent.Normalize();
            else
                tangent = new Vector2f(0, 0);

            float jt = -Extensions.Extensions.DotProduct(relativeVelocity, tangent);

            float rA_cross_t = PhysMath.Cross(rA, tangent);
            float rB_cross_t = PhysMath.Cross(rB, tangent);
            float invMassSumFriction = A.IMass + B.IMass +
                                       (rA_cross_t * rA_cross_t) * iInertiaA +
                                       (rB_cross_t * rB_cross_t) * iInertiaB;
            jt /= invMassSumFriction;

            // Clamp friction impulse (Coulomb friction).
            float mu = Math.Max(A.Friction, B.Friction);
            jt = Math.Min(Math.Abs(jt), mu * Math.Abs(j));
            jt = jt * (jt < 0 ? -1 : 1); // restore sign

            Vector2f frictionImpulse = tangent * jt;

            if (!A.Locked)
            {
                A.Velocity += frictionImpulse * A.IMass;
                if (A.CanRotate)
                {
                    A.AngularVelocity += PhysMath.Cross(rA, frictionImpulse) * iInertiaA;
                }
            }
            if (!B.Locked)
            {
                B.Velocity -= frictionImpulse * B.IMass;
                if (B.CanRotate)
                {
                    B.AngularVelocity -= PhysMath.Cross(rB, frictionImpulse) * iInertiaB;
                }
            }
        }


        public static void PositionalCorrection(ref Manifold m)
        {
            var percent = 0.6f; // usually 20% to 80%
            var slop = 0.05f;    // usually 0.01 to 0.1

            // Only correct penetration beyond the slop.
            float penetration = Math.Max(m.Penetration - slop, 0.0f);
            float correctionMagnitude = penetration / (m.A.IMass + m.B.IMass) * percent;
            Vector2f correction = m.Normal * correctionMagnitude;

            if (!m.A.Locked)
            {
                m.A.Move(-correction * m.A.IMass);
            }

            if (!m.B.Locked)
            {
                m.B.Move(correction * m.B.IMass);
            }
        }

        public static void AngularPositionalCorrection(ref Manifold m)
        {
            // Tuning factor for angular correction; adjust as needed.
            const float angularCorrectionPercent = 0.01f;

            // Compute lever arms (r vectors) from each object's center to the contact point.
            Vector2f rA = m.ContactPoint - m.A.Center;
            Vector2f rB = m.ContactPoint - m.B.Center;

            // For object A:
            if (!m.A.Locked && m.A.CanRotate && rA.LengthSquared() > 0.0001f)
            {
                // The farther the contact point is from the center, the smaller the required angular adjustment.
                float angularErrorA = m.Penetration / rA.Length();
                // The sign of the correction is given by the cross product of rA and the collision normal.
                float signA = Math.Sign(PhysMath.Cross(rA, m.Normal));
                // Adjust the angle by a fraction of the error.
                m.A.Angle -= angularCorrectionPercent * angularErrorA * signA;
            }

            // For object B:
            if (!m.B.Locked && m.A.CanRotate && rB.LengthSquared() > 0.0001f)
            {
                float angularErrorB = m.Penetration / rB.Length();
                float signB = Math.Sign(PhysMath.Cross(rB, m.Normal));
                m.B.Angle += angularCorrectionPercent * angularErrorB * signB;
            }
        }
    }
}