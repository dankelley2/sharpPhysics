using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;
namespace SharpPhysics.Tests
{
    [TestClass]
    public class Vector2Tests
    {
        [TestMethod]
        public void Length_ZeroVector_ReturnsZero()
        {
            var vec = new Vector2(0, 0);
            Assert.AreEqual(0, vec.Length());
        }

        [TestMethod]
        public void Length_UnitVector_ReturnsOne()
        {
            var vec = new Vector2(1, 0);
            Assert.AreEqual(1, vec.Length());
        }

        [TestMethod]
        public void Normalize_NonZeroVector_ReturnsNormalizedVector()
        {
            var vec = new Vector2(3, 4);
            var normalized = Vector2.Normalize(vec);
            Assert.AreEqual(0.6f, normalized.X, 0.0001f);
            Assert.AreEqual(0.8f, normalized.Y, 0.0001f);
        }

        [TestMethod]
        public void DotProduct_PerpendicularVectors_ReturnsZero()
        {
            var v1 = new Vector2(1, 0);
            var v2 = new Vector2(0, 1);
            Assert.AreEqual(0, Vector2.Dot(v1, v2));
        }

        [TestMethod]
        public void Addition_TwoVectors_ReturnsCorrectSum()
        {
            var v1 = new Vector2(1, 2);
            var v2 = new Vector2(3, 4);
            var sum = v1 + v2;
            Assert.AreEqual(4, sum.X);
            Assert.AreEqual(6, sum.Y);
        }

        [TestMethod]
        public void Subtraction_TwoVectors_ReturnsCorrectDifference()
        {
            var v1 = new Vector2(3, 4);
            var v2 = new Vector2(1, 2);
            var diff = v1 - v2;
            Assert.AreEqual(2, diff.X);
            Assert.AreEqual(2, diff.Y);
        }

        [TestMethod]
        public void Multiplication_VectorByScalar_ReturnsScaledVector()
        {
            var vec = new Vector2(2, 3);
            var scaled = vec * 2;
            Assert.AreEqual(4, scaled.X);
            Assert.AreEqual(6, scaled.Y);
        }

        [TestMethod]
        public void Division_VectorByScalar_ReturnsDividedVector()
        {
            var vec = new Vector2(4, 6);
            var divided = vec / 2;
            Assert.AreEqual(2, divided.X);
            Assert.AreEqual(3, divided.Y);
        }

        [TestMethod]
        public void LengthSquared_Vector_ReturnsSquaredLength()
        {
            var vec = new Vector2(3, 4);
            Assert.AreEqual(25, vec.LengthSquared());
        }

        [TestMethod]
        public void Equality_SameVectors_ReturnsTrue()
        {
            var v1 = new Vector2(1, 2);
            var v2 = new Vector2(1, 2);
            Assert.IsTrue(v1 == v2);
        }
    }
}
