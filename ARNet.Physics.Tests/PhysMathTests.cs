using Microsoft.VisualStudio.TestTools.UnitTesting;
using physics.Engine.Helpers;
using physics.Engine.Structs;
using System.Drawing;

namespace ARNet.Physics.Tests
{
    [TestClass]
    public class PhysMathTests
    {
        [TestMethod]
        public void DotProduct_IdenticalVectors_ReturnsSquaredLength()
        {
            var p = new PointF(3, 4);
            var result = PhysMath.DotProduct(p, p);
            Assert.AreEqual(25m, result);
        }

        [TestMethod]
        public void DotProduct_PerpendicularVectors_ReturnsZero()
        {
            var p1 = new PointF(1, 0);
            var p2 = new PointF(0, 1);
            var result = PhysMath.DotProduct(p1, p2);
            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void CorrectBoundingBox_SwappedMinMax_CorrectlySwaps()
        {
            var aabb = new AABB
            {
                Min = new Vec2(5, 6),
                Max = new Vec2(1, 2)
            };
            PhysMath.CorrectBoundingBox(ref aabb);
            Assert.AreEqual(1, aabb.Min.X);
            Assert.AreEqual(2, aabb.Min.Y);
            Assert.AreEqual(5, aabb.Max.X);
            Assert.AreEqual(6, aabb.Max.Y);
        }

        [TestMethod]
        public void CorrectBoundingBox_AlreadyCorrect_RemainsSame()
        {
            var aabb = new AABB
            {
                Min = new Vec2(1, 2),
                Max = new Vec2(5, 6)
            };
            PhysMath.CorrectBoundingBox(ref aabb);
            Assert.AreEqual(1, aabb.Min.X);
            Assert.AreEqual(2, aabb.Min.Y);
            Assert.AreEqual(5, aabb.Max.X);
            Assert.AreEqual(6, aabb.Max.Y);
        }

        [TestMethod]
        public void Clamp_ValueInRange_ReturnsValue()
        {
            var vector = new Vec2(5, 5);
            var min = new Vec2(0, 0);
            var max = new Vec2(10, 10);
            PhysMath.Clamp(ref vector, min, max);
            Assert.AreEqual(5, vector.X);
            Assert.AreEqual(5, vector.Y);
        }

        [TestMethod]
        public void Clamp_ValueBelowRange_ClampsToMin()
        {
            var vector = new Vec2(-1, -2);
            var min = new Vec2(0, 0);
            var max = new Vec2(10, 10);
            PhysMath.Clamp(ref vector, min, max);
            Assert.AreEqual(0, vector.X);
            Assert.AreEqual(0, vector.Y);
        }

        [TestMethod]
        public void Clamp_ValueAboveRange_ClampsToMax()
        {
            var vector = new Vec2(11, 12);
            var min = new Vec2(0, 0);
            var max = new Vec2(10, 10);
            PhysMath.Clamp(ref vector, min, max);
            Assert.AreEqual(10, vector.X);
            Assert.AreEqual(10, vector.Y);
        }

        [TestMethod]
        public void RoundToZero_BelowCutoff_SetsToZero()
        {
            var vector = new Vec2(0.001f, 0.002f);
            PhysMath.RoundToZero(ref vector, 0.01f);
            Assert.AreEqual(0, vector.X);
            Assert.AreEqual(0, vector.Y);
        }

        [TestMethod]
        public void RoundToZero_AboveCutoff_RemainsSame()
        {
            var vector = new Vec2(0.1f, 0.2f);
            PhysMath.RoundToZero(ref vector, 0.01f);
            Assert.AreEqual(0.1f, vector.X);
            Assert.AreEqual(0.2f, vector.Y);
        }

        [TestMethod]
        public void RadiansToDegrees_ZeroRadians_ReturnsZeroDegrees()
        {
            float radians = 0;
            float degrees = radians.RadiansToDegrees();
            Assert.AreEqual(0, degrees, 0.0001f);
        }

        [TestMethod]
        public void RadiansToDegrees_PiRadians_Returns180Degrees()
        {
            float radians = (float)System.Math.PI;
            float degrees = radians.RadiansToDegrees();
            Assert.AreEqual(180, degrees, 0.0001f);
        }
    }
}
