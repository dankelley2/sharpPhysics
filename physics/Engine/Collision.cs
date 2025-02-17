using System;
using physics.Engine.Classes;
using physics.Engine.Extensions;
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

        public static bool AABBvsAABB(ref Manifold m)
        {
            var A = m.A;
            var B = m.B;

            if (A.Locked && B.Locked)
                return false;

            // Compute half extents for each box.
            float a_halfX = A.Width / 2f;
            float a_halfY = A.Height / 2f;
            float b_halfX = B.Width / 2f;
            float b_halfY = B.Height / 2f;

            // Compute the local axes for each box.
            // For object A:
            Vector2f A_axis0 = new Vector2f((float)Math.Cos(A.Angle), (float)Math.Sin(A.Angle));
            Vector2f A_axis1 = new Vector2f(-A_axis0.Y, A_axis0.X);
            // For object B:
            Vector2f B_axis0 = new Vector2f((float)Math.Cos(B.Angle), (float)Math.Sin(B.Angle));
            Vector2f B_axis1 = new Vector2f(-B_axis0.Y, B_axis0.X);

            // Compute the rotation matrix R where R[i][j] = Dot(A_axis[i], B_axis[j])
            float R00 = Dot(A_axis0, B_axis0);
            float R01 = Dot(A_axis0, B_axis1);
            float R10 = Dot(A_axis1, B_axis0);
            float R11 = Dot(A_axis1, B_axis1);

            // Compute translation vector t from A's center to B's center and
            // express it in A's local coordinate system.
            Vector2f t = B.Center - A.Center;
            Vector2f tA = new Vector2f(Dot(t, A_axis0), Dot(t, A_axis1));

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
            Vector2f tB = new Vector2f(Dot(t, B_axis0), Dot(t, B_axis1));

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

        // Helper: Dot product.
        private static float Dot(Vector2f a, Vector2f b)
        {
            return a.X * b.X + a.Y * b.Y;
        }


        public static bool CirclevsCircle(ref Manifold m)
        {
            // Setup a couple pointers to each object
            var A = m.A;
            var B = m.B;

            // Vector from A to B
            var n = B.Center - A.Center;

            // Radii of circles
            float rA = A.Width / 2;
            float rB = B.Width / 2;
            float radiusSum = rA + rB;

            // Early out if circles are not colliding
            if (n.LengthSquared() > radiusSum * radiusSum)
            {
                return false;
            }

            // Compute the distance between circle centers
            float d = n.Length();

            // If the circles are not perfectly overlapping...
            if (d != 0)
            {
                // Penetration is the difference between the sum of the radii and the distance
                m.Penetration = radiusSum - d;
                // The collision normal is the normalized vector from A to B
                m.Normal = n / d;

                // Compute the contact point:
                // For circles, one common method is to take the point on the perimeter of A (along the collision normal)
                // and the point on the perimeter of B (opposite the collision normal), then average them.
                Vector2f contactA = A.Center + m.Normal * rA;
                Vector2f contactB = B.Center - m.Normal * rB;
                m.ContactPoint = (contactA + contactB) * 0.5f;

                // Set the LastContactPoint point for circle and circle
                A.LastContactPoint = m.ContactPoint;
                B.LastContactPoint = m.ContactPoint;

                return true;
            }
            else
            {
                // If the circles are on the same position, choose an arbitrary collision normal and contact point.
                m.Penetration = rA;
                m.Normal = new Vector2f(1, 0);
                m.ContactPoint = A.Center;
                return true;
            }
        }


        public static bool AABBvsCircle(ref Manifold m)
        {
            // m.A is the box and m.B is the circle.
            var box = m.A;
            var circle = m.B;

            // Calculate half extents of the box.
            float x_extent = box.Width / 2f;
            float y_extent = box.Height / 2f;

            // Translate the circle center into the box's local space.
            // Step 1: Get vector from box center to circle center.
            Vector2f circleToBox = circle.Center - box.Center;
            // Step 2: Rotate that vector by -box.Angle.
            float cos = (float)Math.Cos(-box.Angle);
            float sin = (float)Math.Sin(-box.Angle);
            Vector2f circleLocal = new Vector2f(
                 circleToBox.X * cos - circleToBox.Y * sin,
                 circleToBox.X * sin + circleToBox.Y * cos
            );

            // Find the closest point in the box (in local space) to the circle's center.
            Vector2f closestLocal = new Vector2f(
                 Clamp(-x_extent, x_extent, circleLocal.X),
                 Clamp(-y_extent, y_extent, circleLocal.Y)
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
            float r = circle.Width / 2f;

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
            cos = (float)Math.Cos(box.Angle);
            sin = (float)Math.Sin(box.Angle);
            Vector2f normalWorld = new Vector2f(
                 normalLocal.X * cos - normalLocal.Y * sin,
                 normalLocal.X * sin + normalLocal.Y * cos
            );
            m.Normal = normalWorld;

            // Set the contact point on the circle's perimeter along the collision normal.
            m.ContactPoint = circle.Center - m.Normal * r;

            // Set the LastContactPoint point for box and circle
            box.LastContactPoint = m.ContactPoint;
            circle.LastContactPoint = m.ContactPoint;

            return true;
        }


        public static void ResolveCollision(ref Manifold m)
        {
            var rv = m.B.Velocity - m.A.Velocity;

            if (float.IsNaN(m.Normal.X) || float.IsNaN(m.Normal.Y))
            {
                return;
            }

            var velAlongNormal = Extensions.Extensions.DotProduct(rv, m.Normal);

            if (velAlongNormal > 0)
            {
                return;
            }

            var e = Math.Min(m.A.Restitution, m.B.Restitution);

            var j = -(1 + e) * velAlongNormal;
            j = j / (m.A.IMass + m.B.IMass);

            var impulse = m.Normal * j;

            m.A.Velocity = !m.A.Locked ? m.A.Velocity - impulse * m.A.IMass : m.A.Velocity;
            m.B.Velocity = !m.B.Locked ? m.B.Velocity + impulse * m.B.IMass : m.B.Velocity;
        }

        public static void ResolveCollisionRotational(ref Manifold m)
        {
            var A = m.A;
            var B = m.B;

            // Vectors from centers to contact point
            Vector2f rA = m.ContactPoint - A.Center;
            Vector2f rB = m.ContactPoint - B.Center;

            // Compute the relative velocity at contact point:
            // In 2D, the tangential velocity due to rotation can be approximated by:
            // Perp(AngularVelocity) * r  (i.e. a perpendicular vector scaled by angular speed)
            Vector2f vA_contact = A.Velocity + Perpendicular(rA) * A.AngularVelocity;
            Vector2f vB_contact = B.Velocity + Perpendicular(rB) * B.AngularVelocity;
            Vector2f relativeVelocity = vB_contact - vA_contact;

            float velAlongNormal = Extensions.Extensions.DotProduct(relativeVelocity, m.Normal);
            if (velAlongNormal > 0)
                return;

            float e = Math.Min(A.Restitution, B.Restitution);

            // Calculate the scalar cross products (in 2D, treat them as scalars)
            float rA_cross_N = Cross(rA, m.Normal);
            float rB_cross_N = Cross(rB, m.Normal);

            // Compute denominator with rotational inertia terms.
            float invMassSum = A.IMass + B.IMass + (rA_cross_N * rA_cross_N) * A.IInertia + (rB_cross_N * rB_cross_N) * B.IInertia;

            float j = -(1 + e) * velAlongNormal;
            j /= invMassSum;

            Vector2f impulse = m.Normal * j;

            if (!A.Locked)
            {
                A.Velocity -= impulse * A.IMass;
                // Angular impulse is the cross product of rA and impulse
                A.AngularVelocity -= Cross(rA, impulse) * A.IInertia;
            }
            if (!B.Locked)
            {
                B.Velocity += impulse * B.IMass;
                B.AngularVelocity += Cross(rB, impulse) * B.IInertia;
            }
        }


        public static void PositionalCorrection(ref Manifold m)
        {
            var percent = 0.4f; // usually 20% to 80%
            var slop = 0.01f;    // usually 0.01 to 0.1

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
            const float angularCorrectionPercent = 0.05f;

            // Compute lever arms (r vectors) from each object's center to the contact point.
            Vector2f rA = m.ContactPoint - m.A.Center;
            Vector2f rB = m.ContactPoint - m.B.Center;

            // For object A:
            if (!m.A.Locked && rA.LengthSquared() > 0.0001f)
            {
                // The farther the contact point is from the center, the smaller the required angular adjustment.
                float angularErrorA = m.Penetration / rA.Length();
                // The sign of the correction is given by the cross product of rA and the collision normal.
                float signA = Math.Sign(Cross(rA, m.Normal));
                // Adjust the angle by a fraction of the error.
                m.A.Angle -= angularCorrectionPercent * angularErrorA * signA;
            }

            // For object B:
            if (!m.B.Locked && rB.LengthSquared() > 0.0001f)
            {
                float angularErrorB = m.Penetration / rB.Length();
                float signB = Math.Sign(Cross(rB, m.Normal));
                m.B.Angle += angularCorrectionPercent * angularErrorB * signB;
            }
        }

        // Helper: Returns a vector perpendicular to v (i.e., rotated 90 degrees)
        private static Vector2f Perpendicular(Vector2f v)
        {
            return new Vector2f(-v.Y, v.X);
        }

        // Helper: Cross product in 2D (returns a scalar)
        // For vectors a and b, Cross(a, b) = a.X * b.Y - a.Y * b.X
        private static float Cross(Vector2f a, Vector2f b)
        {
            return a.X * b.Y - a.Y * b.X;
        }


        private static float Clamp(float low, float high, float val)
        {
            return Math.Max(low, Math.Min(val, high));
        }
    }
}