using System;
using System.Numerics;
using SharpPhysics.Engine.Classes;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Core
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
            * 1) Get the shapes (both assumed to be �polygons� here).
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
            //   List<Vector2> polyA = CollisionHelpers.GetRectangleCorners(A);
            //   List<Vector2> polyB = CollisionHelpers.GetRectangleCorners(B);
            // For a general PolygonPhysShape, you might do polygonShape.GetTransformedVertices(A.Center, A.Angle), etc.
            Vector2[] polyA = GetWorldVertices(A);
            Vector2[] polyB = GetWorldVertices(B);

            // The overall penetration and normal (to fill into the manifold).
            float minPenetration = float.MaxValue;
            Vector2 bestAxis = new Vector2();

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
            for (int i = 0; i < polyA.Length; i++)
            {
                int next = (i + 1) % polyA.Length;
                // Edge = current -> next
                Vector2 edge = polyA[next] - polyA[i];
                // Normal = perpendicular; you can do (-edge.Y, edge.X)
                Vector2 axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));

                // Project both polygons onto 'axis'
                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            // Check edges from B
            for (int i = 0; i < polyB.Length; i++)
            {
                int next = (i + 1) % polyB.Length;
                // Edge = current -> next
                Vector2 edge = polyB[next] - polyB[i];
                // Normal = perpendicular
                Vector2 axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));

                // Project both polygons onto 'axis'
                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            // After you finalize bestAxis and minPenetration, ensure the normal points from A to B.
            Vector2 centerDiff = B.Center - A.Center;
            if (Vector2.Dot(centerDiff, bestAxis) < 0)
            {
                bestAxis = -bestAxis;
            }

            // If we reach this point, there is a collision.
            m.Normal = bestAxis;
            m.Penetration = minPenetration;

            // Approximate contact point: You can do a midpoint between centers as a fallback:
            m.ContactPoint = (A.Center + B.Center) * 0.5f;

            // Update to accurate contact point after confirmed collision
            CollisionHelpers.UpdateContactPoint(ref m);

            return true;
        }

        /*
        * Example helper to project two polygons onto the given axis and check for overlap.
        * If there is an overlap, we return true; otherwise, false. This also updates the minimum
        * penetration depth and best-axis if the new overlap is smaller.
        */
        private static bool ProjectAndCheckOverlap(
            Vector2[] polyA,
            Vector2[] polyB,
            Vector2 axis,
            ref float minPenetration,
            ref Vector2 bestAxis)
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
        private static (float min, float max) ProjectPolygon(Vector2[] poly, Vector2 axis)
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
        * For a PolygonPhysShape, you might store a local List<Vector2> and transform each by center + rotation.
        */
        private static Vector2[] GetWorldVertices(PhysicsObject obj)
        {
            return obj.Shape.GetTransformedVertices(obj.Center, obj.Angle);
        }

        public static bool CirclevsCircle(ref Manifold m)
        {
            PhysicsObject A = m.A;
            PhysicsObject B = m.B;

            // Ensure both objects are circles.
            CirclePhysShape circleA = (CirclePhysShape)A.Shape;
            CirclePhysShape circleB = (CirclePhysShape)B.Shape;

            if (circleA == null || circleB == null)
            {
                throw new ArgumentException("CirclevsCircle requires both objects to have a CircleShape.");
            }

            // Vector from A to B.
            Vector2 n = B.Center - A.Center;

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
                Vector2 contactA = A.Center + m.Normal * rA;
                Vector2 contactB = B.Center - m.Normal * rB;
                m.ContactPoint = (contactA + contactB) * 0.5f;

                return true;
            }
            else
            {
                // If the circles are at the same position, choose an arbitrary normal and contact point.
                m.Penetration = rA;
                m.Normal = new Vector2(1, 0);
                m.ContactPoint = A.Center;
                return true;
            }
        }

        public static bool PolygonVsCircle(ref Manifold m)
        {
            // m.A is assumed to be the polygon; m.B must be the circle.
            PhysicsObject polyObj = m.A;
            PhysicsObject circleObj = m.B;

            // Cast to the correct shape types.
            var circleShape = (CirclePhysShape)circleObj.Shape;

            // Get polygon vertices in world space.
            Vector2[] poly = GetWorldVertices(polyObj);
            Vector2 circleCenter = circleObj.Center;
            float radius = circleShape.Radius;

            // Find the closest point on the polygon boundary to the circle center
            float minDistSq = float.MaxValue;
            Vector2 closestPoint = Vector2.Zero;
            int closestEdgeIndex = 0;

            // Check all edges
            for (int i = 0; i < poly.Length; i++)
            {
                int next = (i + 1) % poly.Length;
                Vector2 cp = ClosestPointOnSegment(poly[i], poly[next], circleCenter);
                float distSq = Vector2.DistanceSquared(cp, circleCenter);

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closestPoint = cp;
                    closestEdgeIndex = i;
                }
            }

            float dist = (float)Math.Sqrt(minDistSq);

            // Check if circle center is inside the polygon using winding number / crossing test
            bool circleInsidePolygon = IsPointInsidePolygon(circleCenter, poly);

            if (circleInsidePolygon)
            {
                // Circle center is inside - always a collision
                // Normal points from polygon toward circle (outward from polygon's perspective)
                if (dist < 0.0001f)
                {
                    // Circle center is exactly on an edge - use edge normal
                    int next = (closestEdgeIndex + 1) % poly.Length;
                    Vector2 edge = poly[next] - poly[closestEdgeIndex];
                    m.Normal = -Vector2.Normalize(new Vector2(-edge.Y, edge.X));
                }
                else
                {
                    // Normal points from closest point toward circle center
                    m.Normal = -(circleCenter - closestPoint) / dist;
                }
                // Penetration is radius plus distance to edge
                m.Penetration = radius + dist;
                m.ContactPoint = closestPoint;
                return true;
            }
            else
            {
                // Circle center is outside - only collide if distance < radius
                if (dist > radius)
                    return false;

                if (dist < 0.0001f)
                {
                    // Circle center is exactly on the boundary
                    int next = (closestEdgeIndex + 1) % poly.Length;
                    Vector2 edge = poly[next] - poly[closestEdgeIndex];
                    m.Normal = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
                    m.Penetration = radius;
                }
                else
                {
                    // Normal points from closest point toward circle center (pushing circle out)
                    m.Normal = (circleCenter - closestPoint) / dist;
                    m.Penetration = radius - dist;
                }
                m.ContactPoint = closestPoint;
                return true;
            }
        }

        /// <summary>
        /// Determines if a point is inside a polygon using the ray casting (crossing number) algorithm.
        /// </summary>
        private static bool IsPointInsidePolygon(Vector2 point, Vector2[] poly)
        {
            int crossings = 0;
            int n = poly.Length;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 vi = poly[i];
                Vector2 vj = poly[j];

                // Check if the edge crosses the horizontal ray from point going right
                if ((vi.Y <= point.Y && vj.Y > point.Y) || (vj.Y <= point.Y && vi.Y > point.Y))
                {
                    // Compute x coordinate of intersection
                    float t = (point.Y - vi.Y) / (vj.Y - vi.Y);
                    float xIntersect = vi.X + t * (vj.X - vi.X);

                    if (point.X < xIntersect)
                        crossings++;
                }
            }

            // Point is inside if crossing count is odd
            return (crossings % 2) == 1;
        }

        /// <summary>
        /// Helper: Returns the point on the segment [a, b] that is closest to point p.
        /// </summary>
        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / ab.LengthSquared();
            t = Math.Max(0, Math.Min(1, t));
            return a + ab * t;
        }

        public static void ResolveCollisionRotational(ref Manifold m)
        {
            // Retrieve the two physics objects.
            PhysicsObject A = m.A;
            PhysicsObject B = m.B;

            // For each object, if it's rotational, get its angular velocity and inverse inertia; otherwise, treat as zero.
            float angularVelA = A.CanRotate ? A.AngularVelocity : 0F;
            float iInertiaA = A.CanRotate ? A.IInertia : 0F;
            float angularVelB = B.CanRotate ? B.AngularVelocity : 0F;
            float iInertiaB = B.CanRotate ? B.IInertia : 0F;

            // Compute vectors from centers to contact point.
            Vector2 rA = m.ContactPoint - A.Center;
            Vector2 rB = m.ContactPoint - B.Center;

            // Compute the relative velocity at the contact point (including any rotational contribution).
            Vector2 vA_contact = A.Velocity + PhysMath.Perpendicular(rA) * angularVelA;
            Vector2 vB_contact = B.Velocity + PhysMath.Perpendicular(rB) * angularVelB;
            Vector2 relativeVelocity = vB_contact - vA_contact;

            float velAlongNormal = Vector2.Dot(relativeVelocity, m.Normal);
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

            Vector2 impulse = m.Normal * j;

            if (!A.Locked && !A.Sleeping)
            {
                A.Velocity -= impulse * A.IMass;
                if (A.CanRotate)
                {
                    A.AngularVelocity -= PhysMath.Cross(rA, impulse) * iInertiaA;
                }
            }
            if (!B.Locked && !B.Sleeping)
            {
                B.Velocity += impulse * B.IMass;
                if (B.CanRotate)
                {
                    B.AngularVelocity += PhysMath.Cross(rB, impulse) * iInertiaB;
                }
            }

            // --- Friction impulse ---
            Vector2 tangent = relativeVelocity - m.Normal * Vector2.Dot(relativeVelocity, m.Normal);
            if (tangent.LengthSquared() > 0.0001f)
                tangent = Vector2.Normalize(tangent);
            else
                tangent = new Vector2(0, 0);

            float jt = -Vector2.Dot(relativeVelocity, tangent);

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

            Vector2 frictionImpulse = tangent * jt;

            if (!A.Locked && !A.Sleeping)
            {
                A.Velocity += frictionImpulse * A.IMass;
                if (A.CanRotate)
                {
                    A.AngularVelocity += PhysMath.Cross(rA, frictionImpulse) * iInertiaA;
                }
            }
            if (!B.Locked && !B.Sleeping)
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
            Vector2 correction = m.Normal * correctionMagnitude;

            if (!m.A.Locked && !m.A.Sleeping)
            {
                m.A.Move(-correction * m.A.IMass);
            }

            if (!m.B.Locked && !m.B.Sleeping)
            {
                m.B.Move(correction * m.B.IMass);
            }
        }

        public static void AngularPositionalCorrection(ref Manifold m)
        {
            // Tuning factor for angular correction; adjust as needed.
            const float angularCorrectionPercent = 0.01f;

            // Compute lever arms (r vectors) from each object's center to the contact point.
            Vector2 rA = m.ContactPoint - m.A.Center;
            Vector2 rB = m.ContactPoint - m.B.Center;

            // For object A:
            if (!m.A.Locked && !m.A.Sleeping && m.A.CanRotate && rA.LengthSquared() > 0.0001f)
            {
                // The farther the contact point is from the center, the smaller the required angular adjustment.
                float angularErrorA = m.Penetration / rA.Length();
                // The sign of the correction is given by the cross product of rA and the collision normal.
                float signA = Math.Sign(PhysMath.Cross(rA, m.Normal));
                // Adjust the angle by a fraction of the error.
                m.A.Angle -= angularCorrectionPercent * angularErrorA * signA;
            }

            // For object B:
            if (!m.B.Locked && !m.B.Sleeping && m.B.CanRotate && rB.LengthSquared() > 0.0001f)
            {
                float angularErrorB = m.Penetration / rB.Length();
                float signB = Math.Sign(PhysMath.Cross(rB, m.Normal));
                m.B.Angle += angularCorrectionPercent * angularErrorB * signB;
            }
        }

        /// <summary>
        /// Tests collision between a compound body and another physics object.
        /// Tests each child shape of the compound against the other object.
        /// Returns the deepest penetration found.
        /// </summary>
        public static bool CompoundVsOther(ref Manifold m, CompoundShape compoundShape, bool compoundIsA)
        {
            var compound = compoundIsA ? m.A : m.B;
            var other = compoundIsA ? m.B : m.A;

            bool anyCollision = false;
            float deepestPenetration = 0f;
            Vector2 bestNormal = Vector2.Zero;
            Vector2 bestContactPoint = Vector2.Zero;

            // Track the colliding child's vertices for accurate contact point calculation
            Vector2[]? bestChildPolyA = null;
            Vector2[]? bestChildPolyB = null;
            Vector2 bestChildCenterA = Vector2.Zero;
            Vector2 bestChildCenterB = Vector2.Zero;

            // Handle compound vs compound case
            if (other.Shape.ShapeType == ShapeTypeEnum.Compound)
            {
                var otherCompound = (CompoundShape)other.Shape;

                // Test each child of this compound against each child of the other compound
                for (int i = 0; i < compoundShape.Children.Count; i++)
                {
                    var childA = compoundShape.Children[i];
                    var (childACenter, childAAngle) = compoundShape.GetChildWorldTransform(i, compound.Center, compound.Angle);

                    for (int j = 0; j < otherCompound.Children.Count; j++)
                    {
                        var childB = otherCompound.Children[j];
                        var (childBCenter, childBAngle) = otherCompound.GetChildWorldTransform(j, other.Center, other.Angle);

                        Manifold childManifold = new Manifold();
                        bool childCollision = TestChildVsChild(
                            childA.Shape, childACenter, childAAngle,
                            childB.Shape, childBCenter, childBAngle,
                            ref childManifold);

                        if (childCollision && childManifold.Penetration > deepestPenetration)
                        {
                            anyCollision = true;
                            deepestPenetration = childManifold.Penetration;
                            bestNormal = childManifold.Normal;
                            bestContactPoint = childManifold.ContactPoint;

                            // Store child vertices for accurate contact point
                            bestChildPolyA = childA.Shape.GetTransformedVertices(childACenter, childAAngle);
                            bestChildPolyB = childB.Shape.GetTransformedVertices(childBCenter, childBAngle);
                            bestChildCenterA = childACenter;
                            bestChildCenterB = childBCenter;
                        }
                    }
                }

                if (anyCollision)
                {
                    m.Penetration = deepestPenetration;
                    m.Normal = compoundIsA ? bestNormal : -bestNormal;

                    // Compute accurate contact point using the actual colliding child's vertices
                    if (bestChildPolyA != null && bestChildPolyB != null)
                    {
                        m.ContactPoint = CollisionHelpers.ComputeContactPoint(
                            bestChildPolyA, bestChildPolyB, bestChildCenterA, bestChildCenterB);
                    }
                    else
                    {
                        m.ContactPoint = bestContactPoint;
                    }
                }

                return anyCollision;
            }

            // Test each child shape against the other (non-compound) object
            for (int i = 0; i < compoundShape.Children.Count; i++)
            {
                ChildShape child = compoundShape.Children[i];
                var (childCenter, childAngle) = compoundShape.GetChildWorldTransform(i, compound.Center, compound.Angle);

                // skip on no aabb collision
                if (!AABBvsAABB(child.Shape.GetAABB(childCenter, childAngle), other.Aabb))
                    continue;

                Manifold childManifold = new Manifold();
                bool childCollision = false;
                if (child.Shape.ShapeType == ShapeTypeEnum.Circle)
                {
                    if (other.Shape.ShapeType == ShapeTypeEnum.Circle)
                    {
                        childCollision = TestChildCircleVsCircle(child.Shape, childCenter, other, ref childManifold);
                    }
                    else if (other.Shape.ShapeType == ShapeTypeEnum.Box || other.Shape.ShapeType == ShapeTypeEnum.Polygon)
                    {
                        childCollision = TestChildCircleVsPolygon(child.Shape, childCenter, other, ref childManifold);
                    }
                }
                else if (child.Shape.ShapeType == ShapeTypeEnum.Box || child.Shape.ShapeType == ShapeTypeEnum.Polygon)
                {
                    if (other.Shape.ShapeType == ShapeTypeEnum.Circle)
                    {
                        childCollision = TestChildPolygonVsCircle(child.Shape, childCenter, childAngle, other, ref childManifold);
                    }
                    else if (other.Shape.ShapeType == ShapeTypeEnum.Box || other.Shape.ShapeType == ShapeTypeEnum.Polygon)
                    {
                        childCollision = TestChildPolygonVsPolygon(child.Shape, childCenter, childAngle, other, ref childManifold);
                    }
                }

                if (childCollision && childManifold.Penetration > deepestPenetration)
                {
                    anyCollision = true;
                    deepestPenetration = childManifold.Penetration;
                    bestNormal = childManifold.Normal;
                    bestContactPoint = childManifold.ContactPoint;

                    // Store child vertices for accurate contact point (polygon cases only)
                    if (child.Shape.ShapeType == ShapeTypeEnum.Box || child.Shape.ShapeType == ShapeTypeEnum.Polygon)
                    {
                        bestChildPolyA = child.Shape.GetTransformedVertices(childCenter, childAngle);
                        bestChildCenterA = childCenter;
                    }
                    else
                    {
                        bestChildPolyA = null;
                    }

                    if (other.Shape.ShapeType == ShapeTypeEnum.Box || other.Shape.ShapeType == ShapeTypeEnum.Polygon)
                    {
                        bestChildPolyB = other.Shape.GetTransformedVertices(other.Center, other.Angle);
                        bestChildCenterB = other.Center;
                    }
                    else
                    {
                        bestChildPolyB = null;
                    }
                }
            }

            if (anyCollision)
            {
                m.Penetration = deepestPenetration;
                m.Normal = compoundIsA ? bestNormal : -bestNormal;

                // Compute accurate contact point using the actual colliding child's vertices
                if (bestChildPolyA != null && bestChildPolyB != null)
                {
                    m.ContactPoint = CollisionHelpers.ComputeContactPoint(
                        bestChildPolyA, bestChildPolyB, bestChildCenterA, bestChildCenterB);
                }
                else
                {
                    // For circle collisions, use the contact point from the child test
                    m.ContactPoint = bestContactPoint;
                }
            }

            return anyCollision;
        }

        /// <summary>
        /// Tests collision between two child shapes (for compound vs compound).
        /// </summary>
        private static bool TestChildVsChild(
            IShape shapeA, Vector2 centerA, float angleA,
            IShape shapeB, Vector2 centerB, float angleB,
            ref Manifold m)
        {
            if (shapeA.ShapeType == ShapeTypeEnum.Circle && shapeB.ShapeType == ShapeTypeEnum.Circle)
            {
                var circleA = (CirclePhysShape)shapeA;
                var circleB = (CirclePhysShape)shapeB;

                Vector2 n = centerB - centerA;
                float rSum = circleA.Radius + circleB.Radius;

                if (n.LengthSquared() > rSum * rSum)
                    return false;

                float d = n.Length();
                if (d != 0)
                {
                    m.Penetration = rSum - d;
                    m.Normal = n / d;
                    m.ContactPoint = centerA + m.Normal * circleA.Radius;
                }
                else
                {
                    m.Penetration = circleA.Radius;
                    m.Normal = new Vector2(1, 0);
                    m.ContactPoint = centerA;
                }
                return true;
            }

            if (shapeA.ShapeType == ShapeTypeEnum.Circle &&
                (shapeB.ShapeType == ShapeTypeEnum.Box || shapeB.ShapeType == ShapeTypeEnum.Polygon))
            {
                return TestChildCircleVsChildPolygon(shapeA, centerA, shapeB, centerB, angleB, ref m);
            }

            if ((shapeA.ShapeType == ShapeTypeEnum.Box || shapeA.ShapeType == ShapeTypeEnum.Polygon) &&
                shapeB.ShapeType == ShapeTypeEnum.Circle)
            {
                bool result = TestChildCircleVsChildPolygon(shapeB, centerB, shapeA, centerA, angleA, ref m);
                if (result)
                    m.Normal = -m.Normal;
                return result;
            }

            if ((shapeA.ShapeType == ShapeTypeEnum.Box || shapeA.ShapeType == ShapeTypeEnum.Polygon) &&
                (shapeB.ShapeType == ShapeTypeEnum.Box || shapeB.ShapeType == ShapeTypeEnum.Polygon))
            {
                return TestChildPolygonVsChildPolygon(shapeA, centerA, angleA, shapeB, centerB, angleB, ref m);
            }

            return false;
        }

        private static bool TestChildCircleVsChildPolygon(
            IShape circleShape, Vector2 circleCenter,
            IShape polyShape, Vector2 polyCenter, float polyAngle,
            ref Manifold m)
        {
            var circle = (CirclePhysShape)circleShape;
            Vector2[] poly = polyShape.GetTransformedVertices(polyCenter, polyAngle);
            float radius = circle.Radius;

            float minDistSq = float.MaxValue;
            Vector2 closestPoint = Vector2.Zero;

            for (int i = 0; i < poly.Length; i++)
            {
                int next = (i + 1) % poly.Length;
                Vector2 cp = ClosestPointOnSegment(poly[i], poly[next], circleCenter);
                float distSq = Vector2.DistanceSquared(cp, circleCenter);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closestPoint = cp;
                }
            }

            float dist = (float)Math.Sqrt(minDistSq);
            bool inside = IsPointInsidePolygon(circleCenter, poly);

            if (inside)
            {
                m.Penetration = radius + dist;
                m.Normal = dist > 0.0001f ? (circleCenter - closestPoint) / dist : new Vector2(1, 0);
                m.ContactPoint = closestPoint;
                return true;
            }
            else if (dist <= radius)
            {
                m.Penetration = radius - dist;
                m.Normal = dist > 0.0001f ? -(circleCenter - closestPoint) / dist : new Vector2(1, 0);
                m.ContactPoint = closestPoint;
                return true;
            }

            return false;
        }

        private static bool TestChildPolygonVsChildPolygon(
            IShape shapeA, Vector2 centerA, float angleA,
            IShape shapeB, Vector2 centerB, float angleB,
            ref Manifold m)
        {
            Vector2[] polyA = shapeA.GetTransformedVertices(centerA, angleA);
            Vector2[] polyB = shapeB.GetTransformedVertices(centerB, angleB);

            float minPenetration = float.MaxValue;
            Vector2 bestAxis = Vector2.Zero;

            for (int i = 0; i < polyA.Length; i++)
            {
                int next = (i + 1) % polyA.Length;
                Vector2 edge = polyA[next] - polyA[i];
                Vector2 axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));

                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            for (int i = 0; i < polyB.Length; i++)
            {
                int next = (i + 1) % polyB.Length;
                Vector2 edge = polyB[next] - polyB[i];
                Vector2 axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));

                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            Vector2 centerDiff = centerB - centerA;
            if (Vector2.Dot(centerDiff, bestAxis) < 0)
                bestAxis = -bestAxis;

            m.Normal = bestAxis;
            m.Penetration = minPenetration;
            m.ContactPoint = ComputePolygonContactPoint(polyA, polyB, bestAxis);

            return true;
        }

        private static Vector2 ComputePolygonContactPoint(Vector2[] polyA, Vector2[] polyB, Vector2 normal)
        {
            // Find the support point on polyA along the normal direction (furthest in normal direction)
            float maxProjA = float.MinValue;
            Vector2 supportA = Vector2.Zero;
            foreach (var v in polyA)
            {
                float proj = Vector2.Dot(v, normal);
                if (proj > maxProjA)
                {
                    maxProjA = proj;
                    supportA = v;
                }
            }

            // Find the support point on polyB along the negative normal (closest in normal direction)
            float minProjB = float.MaxValue;
            Vector2 supportB = Vector2.Zero;
            foreach (var v in polyB)
            {
                float proj = Vector2.Dot(v, normal);
                if (proj < minProjB)
                {
                    minProjB = proj;
                    supportB = v;
                }
            }

            // Use the midpoint of the support points as contact
            return (supportA + supportB) * 0.5f;
        }

        private static bool TestChildCircleVsCircle(IShape childShape, Vector2 childCenter, PhysicsObject other, ref Manifold m)
        {
            var childCircle = (CirclePhysShape)childShape;
            var otherCircle = (CirclePhysShape)other.Shape;

            Vector2 n = other.Center - childCenter;
            float rSum = childCircle.Radius + otherCircle.Radius;

            if (n.LengthSquared() > rSum * rSum)
                return false;

            float d = n.Length();
            if (d != 0)
            {
                m.Penetration = rSum - d;
                m.Normal = n / d;
                m.ContactPoint = childCenter + m.Normal * childCircle.Radius;
            }
            else
            {
                m.Penetration = childCircle.Radius;
                m.Normal = new Vector2(1, 0);
                m.ContactPoint = childCenter;
            }
            return true;
        }

        private static bool TestChildCircleVsPolygon(IShape childShape, Vector2 childCenter, PhysicsObject polyObj, ref Manifold m)
        {
            var circle = (CirclePhysShape)childShape;
            Vector2[] poly = polyObj.Shape.GetTransformedVertices(polyObj.Center, polyObj.Angle);
            float radius = circle.Radius;

            // Find closest point on polygon to circle center
            float minDistSq = float.MaxValue;
            Vector2 closestPoint = Vector2.Zero;

            for (int i = 0; i < poly.Length; i++)
            {
                int next = (i + 1) % poly.Length;
                Vector2 cp = ClosestPointOnSegment(poly[i], poly[next], childCenter);
                float distSq = Vector2.DistanceSquared(cp, childCenter);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closestPoint = cp;
                }
            }

            float dist = (float)Math.Sqrt(minDistSq);
            bool inside = IsPointInsidePolygon(childCenter, poly);

            if (inside)
            {
                m.Penetration = radius + dist;
                m.Normal = dist > 0.0001f ? (childCenter - closestPoint) / dist : new Vector2(1, 0);
                m.ContactPoint = closestPoint;
                return true;
            }
            else if (dist <= radius)
            {
                m.Penetration = radius - dist;
                m.Normal = dist > 0.0001f ? -(childCenter - closestPoint) / dist : new Vector2(1, 0);
                m.ContactPoint = closestPoint;
                return true;
            }

            return false;
        }

        private static bool TestChildPolygonVsCircle(IShape childShape, Vector2 childCenter, float childAngle, PhysicsObject circleObj, ref Manifold m)
        {
            var circle = (CirclePhysShape)circleObj.Shape;
            Vector2[] poly = childShape.GetTransformedVertices(childCenter, childAngle);
            Vector2 circleCenter = circleObj.Center;
            float radius = circle.Radius;

            // Find closest point on polygon to circle center
            float minDistSq = float.MaxValue;
            Vector2 closestPoint = Vector2.Zero;

            for (int i = 0; i < poly.Length; i++)
            {
                int next = (i + 1) % poly.Length;
                Vector2 cp = ClosestPointOnSegment(poly[i], poly[next], circleCenter);
                float distSq = Vector2.DistanceSquared(cp, circleCenter);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closestPoint = cp;
                }
            }

            float dist = (float)Math.Sqrt(minDistSq);
            bool inside = IsPointInsidePolygon(circleCenter, poly);

            if (inside)
            {
                m.Penetration = radius + dist;
                m.Normal = dist > 0.0001f ? (circleCenter - closestPoint) / dist : new Vector2(1, 0);
                m.ContactPoint = closestPoint;
                return true;
            }
            else if (dist <= radius)
            {
                m.Penetration = radius - dist;
                m.Normal = dist > 0.0001f ? (circleCenter - closestPoint) / dist : new Vector2(1, 0);
                m.ContactPoint = closestPoint;
                return true;
            }

            return false;
        }

        private static bool TestChildPolygonVsPolygon(IShape childShape, Vector2 childCenter, float childAngle, PhysicsObject other, ref Manifold m)
        {
            Vector2[] polyA = childShape.GetTransformedVertices(childCenter, childAngle);
            Vector2[] polyB = other.Shape.GetTransformedVertices(other.Center, other.Angle);

            float minPenetration = float.MaxValue;
            Vector2 bestAxis = Vector2.Zero;

            // SAT: Check edges from polyA
            for (int i = 0; i < polyA.Length; i++)
            {
                int next = (i + 1) % polyA.Length;
                Vector2 edge = polyA[next] - polyA[i];
                Vector2 axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));

                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            // SAT: Check edges from polyB
            for (int i = 0; i < polyB.Length; i++)
            {
                int next = (i + 1) % polyB.Length;
                Vector2 edge = polyB[next] - polyB[i];
                Vector2 axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));

                if (!ProjectAndCheckOverlap(polyA, polyB, axis, ref minPenetration, ref bestAxis))
                    return false;
            }

            // Ensure normal points from child to other
            Vector2 centerDiff = other.Center - childCenter;
            if (Vector2.Dot(centerDiff, bestAxis) < 0)
                bestAxis = -bestAxis;

            m.Normal = bestAxis;
            m.Penetration = minPenetration;
            m.ContactPoint = ComputePolygonContactPoint(polyA, polyB, bestAxis);

            return true;
        }
    }
}