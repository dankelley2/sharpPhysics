using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using physics.Engine.Shaders;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ObjectTemplates
    {
        private static SFMLShader shaderDefault = new SFMLBallShader();

        private static SFMLShader shaderWall = new SFMLWallShader();

        private static SFMLShader shaderBall = new SFMLBallShader();

        private static SFMLShader shaderBallFast = new SFMLBallShader();

        private static SFMLShader shaderBallVelocity = new SFMLBallVelocityShader();

        private static SFMLShader shaderWater = new SFMLBallVelocityShader();

        private static Random r = new Random();

        public static PhysicsObject CreateSmallBall(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), 5, .6F, false, shaderBallVelocity);
        }

        public static PhysicsObject CreateSizedBall(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), r.Next(5,15), .6F, false, shaderBallVelocity);
        }

        public static PhysicsObject CreateSmallBall_Magnet(float originX, float originY)
        {
            var oPhysicsObject = PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), 5, .6F, false, shaderBallVelocity);
            PhysicsSystem.ListGravityObjects.Add(oPhysicsObject);
            return oPhysicsObject;
        }

        public static PhysicsObject CreateMedBall(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), 10, .6F, false, shaderBallVelocity);
        }

        public static PhysicsObject CreateWater(float originX, float originY)
        {
            return PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), 5, .99F, false, shaderWater);
        }

        public static PhysicsObject CreateAttractor(float originX, float originY)
        {
            var oPhysicsObject = PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), 50, .95F, true, shaderBall);
            PhysicsSystem.ListGravityObjects.Add(oPhysicsObject);
            return oPhysicsObject;
        }

        public static PhysicsObject CreateWall(float minX, float minY, float maxX, float maxY)
        {
            return PhysicsSystem.CreateStaticBox(new Vector2f(minX, minY), new Vector2f(maxX, maxY), true, shaderWall, 1000000);
        }
    }
}
