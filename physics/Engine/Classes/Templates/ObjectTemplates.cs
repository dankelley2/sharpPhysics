using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using physics.Engine.Structs;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ObjectTemplates
    {
        private static aShader shaderDefault = new ShaderDefault();

        private static aShader shaderWall = new ShaderWall();

        private static aShader shaderBall = new ShaderBall();

        private static aShader shaderBallFast = new ShaderBallFast();

        private static aShader shaderBallVelocity = new ShaderBallVelocity();

        private static aShader shaderWater = new ShaderWater();

        private static Random r = new Random();

        public static PhysicsObject CreateSmallBall(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vec2(originX, originY), 5, .6F, false, shaderBallVelocity);
        }

        public static PhysicsObject CreateSizedBall(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vec2(originX, originY), r.Next(5,15), .6F, false, shaderBallVelocity);
        }

        public static PhysicsObject CreateSmallBall_Magnet(float originX, float originY)
        {
            var oPhysicsObject = PhysicsSystem.CreateStaticCircle(new Vec2(originX, originY), 5, .6F, false, shaderBallVelocity);
            PhysicsSystem.ListGravityObjects.Add(oPhysicsObject);
            return oPhysicsObject;
        }

        public static PhysicsObject CreateMedBall(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vec2(originX, originY), 10, .6F, false, shaderBallVelocity);
        }

        public static PhysicsObject CreateWater(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vec2(originX, originY), 5, .99F, false, shaderWater);
        }

        public static PhysicsObject CreateAttractor(float originX, float originY)
        {
            var oPhysicsObject = PhysicsSystem.CreateStaticCircle(new Vec2(originX, originY), 50, .95F, true, shaderBall);
            PhysicsSystem.ListGravityObjects.Add(oPhysicsObject);
            return oPhysicsObject;
        }

        public static PhysicsObject CreateWall(float minX, float minY, float maxX, float maxY)
        {
            return PhysicsSystem.CreateStaticBox(new Vec2(minX, minY), new Vec2(maxX, maxY), true, shaderWall, 1000000);
        }
    }
}
