using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using physics.Engine.Classes;
using physics.Engine.Structs;

namespace physics.Engine.Helpers
{
    static class PhysMath
    {

        public static decimal DotProduct(PointF pa, PointF pb)
        {
            decimal[] a = { (decimal)pa.X, (decimal)pa.Y };
            decimal[] b = { (decimal)pb.X, (decimal)pb.Y };
            return a.Zip(b, (x, y) => x * y).Sum();
        }

        public static void CorrectBoundingBox(ref AABB aabb)
        {
            PointF p1 = new PointF(Math.Min(aabb.Min.X, aabb.Max.X), Math.Min(aabb.Min.Y, aabb.Max.Y));
            PointF p2 = new PointF(Math.Max(aabb.Min.X, aabb.Max.X), Math.Max(aabb.Min.Y, aabb.Max.Y));
            aabb.Min = new Vec2 { X = p1.X, Y = p1.Y };
            aabb.Max = new Vec2 { X = p2.X, Y = p2.Y };
        }
        public static void CorrectBoundingPoints(ref PointF p1, ref PointF p2)
        {
            PointF new_p1 = new PointF(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y));
            PointF new_p2 = new PointF(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));

            p1 = new_p1;
            p2 = new_p2;
        }

    }

}
