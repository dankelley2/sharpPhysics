using System;
using physics.Engine.Classes;
using physics.Engine.Extensions;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine
{
    internal static class Collision
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
            // Setup a couple pointers to each object
            var A = m.A;
            var B = m.B;

            // Vector from A to B
            var n = B.Center - A.Center;


            var abox = A.Aabb;
            var bbox = B.Aabb;

            // Calculate half extents along x axis for each object
            var a_extent = (abox.Max.X - abox.Min.X) / 2;
            var b_extent = (bbox.Max.X - bbox.Min.X) / 2;

            // Calculate overlap on x axis
            var x_overlap = a_extent + b_extent - Math.Abs(n.X);

            // SAT test on x axis
            if (x_overlap > 0)
            {
                // Calculate half extents along y axis for each object
                a_extent = (abox.Max.Y - abox.Min.Y) / 2;
                b_extent = (bbox.Max.Y - bbox.Min.Y) / 2;

                // Calculate overlap on y axis
                var y_overlap = a_extent + b_extent - Math.Abs(n.Y);

                // SAT test on y axis
                if (y_overlap > 0)
                {
                    // Find out which axis is axis of least penetration
                    if (x_overlap < y_overlap)
                    {
                        // Point towards B knowing that n points from A to B
                        if (n.X < 0)
                        {
                            m.Normal = new Vector2f {X = -1, Y = 0};
                        }
                        else
                        {
                            m.Normal = new Vector2f {X = 1, Y = 0};
                        }

                        m.Penetration = x_overlap;
                        return true;
                    }

                    // Point toward B knowing that n points from A to B
                    if (n.Y < 0)
                    {
                        m.Normal = new Vector2f {X = 0, Y = -1};
                    }
                    else
                    {
                        m.Normal = new Vector2f {X = 0, Y = 1};
                    }

                    m.Penetration = y_overlap;
                    return true;
                }
            }

            return false;
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
            // Setup a couple pointers to each object
            //Box Shape
            var box = m.A;

            //CircleShape
            var circle = m.B;

            // Vector from box to circle
            var n = circle.Center - box.Center;

            // Closest point on box to center of circle
            var closest = n;

            // Calculate half extents along each axis
            var x_extent = (box.Aabb.Max.X - box.Aabb.Min.X) / 2;
            var y_extent = (box.Aabb.Max.Y - box.Aabb.Min.Y) / 2;

            // Clamp point to edges of the AABB
            closest.X = Clamp(-x_extent, x_extent, closest.X);
            closest.Y = Clamp(-y_extent, y_extent, closest.Y);


            var inside = false;

            // Circle is inside the AABB, so we need to clamp the circle's center
            // to the closest edge
            if (n == closest)
            {
                inside = true;

                // Find closest axis
                if (Math.Abs(n.X) < Math.Abs(n.Y))
                {
                    // Clamp to closest extent
                    if (closest.X > 0)
                    {
                        closest.X = x_extent;
                    }
                    else
                    {
                        closest.X = -x_extent;
                    }
                }

                // y axis is shorter
                else
                {
                    // Clamp to closest extent
                    if (closest.Y > 0)
                    {
                        closest.Y = y_extent;
                    }
                    else
                    {
                        closest.Y = -y_extent;
                    }
                }
            }

            var normal = n - closest;
            var d = normal.LengthSquared();
            var r = circle.Width/2;

            // Early out of the radius is shorter than distance to closest point and
            // Circle not inside the AABB
            if (d > r * r && !inside)
            {
                return false;
            }

            // Avoided sqrt until we needed
            d = (float) Math.Sqrt(d);

            // Collision normal needs to be flipped to point outside if circle was
            // inside the AABB
            if (inside)
            {
                m.Normal = (-normal).Normalize();
                m.Penetration = r - d;
            }
            else
            {
                //If pushing up at all, go straight up (gravity hack)
                m.Normal = normal.Normalize();
                m.Penetration = r - d;
            }

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

        public static void PositionalCorrection(ref Manifold m)
        {
            // Pushing this all the way will cause the objects to be fully separated.
            // lower amounts will allow for some overlap.
            var percent = .8F; // usually 20% to 80%
            var correction = m.Normal * (percent * (m.Penetration / (m.A.IMass + m.B.IMass)));
            if (!m.A.Locked)
            {
                m.A.Move(-correction * m.A.IMass);
            }

            if (!m.B.Locked)
            {
                m.B.Move(correction * m.B.IMass);
            }
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