using System;
using System.Collections.Generic;
using physics.Engine.Helpers;
using physics.Engine.Objects;
using physics.Engine.Shaders;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine.Classes.ObjectTemplates
{
    public static class ObjectTemplates
    {
        // Outer dictionary keyed on the shader type.
        // Inner dictionary keyed on the object's diameter (as an int) with a shader instance as the value.
        private static Dictionary<Type, Dictionary<int, SFMLShader>> shaderPool =
            new Dictionary<Type, Dictionary<int, SFMLShader>>();

        // Generic helper method to get (or create) a shader for a given diameter.
        private static T GetShader<T>(int diameter) where T : SFMLShader
        {
            Type shaderType = typeof(T);
            if (!shaderPool.ContainsKey(shaderType))
            {
                shaderPool[shaderType] = new Dictionary<int, SFMLShader>();
            }
            var innerDict = shaderPool[shaderType];
            if (!innerDict.ContainsKey(diameter))
            {
                // Try to get a constructor that takes an int parameter.
                var constructor = shaderType.GetConstructor(new Type[] { typeof(int) });
                if (constructor != null)
                {
                    innerDict[diameter] = (SFMLShader)Activator.CreateInstance(shaderType, diameter);
                }
                else
                {
                    // If not available, use the default constructor.
                    innerDict[diameter] = (SFMLShader)Activator.CreateInstance(shaderType);
                }
            }
            return (T)innerDict[diameter];
        }

        public static PhysicsObject CreateSmallBall(float originX, float originY)
        {
            // Using a diameter of 5.
            int diameter = 5;
            SFMLShader shader = GetShader<SFMLBallVelocityShader>(diameter);
            return PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), diameter, 0.6F, false, shader);
        }

        public static PhysicsObject CreateMedBall(float originX, float originY)
        {
            int diameter = 10;
            SFMLShader shader = GetShader<SFMLBallVelocityShader>(diameter);
            return PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), diameter, 0.8F, false, shader);
        }

        public static PhysicsObject CreateAttractor(float originX, float originY)
        {
            int diameter = 50;
            // Use a different shader type for attractors.
            SFMLShader shader = GetShader<SFMLBallShader>(diameter);
            var oPhysicsObject = PhysicsSystem.CreateStaticCircle(new Vector2f(originX, originY), diameter, 0.95F, true, shader);
            PhysicsSystem.ListGravityObjects.Add(oPhysicsObject);
            return oPhysicsObject;
        }

        public static PhysicsObject CreateWall(Vector2f origin, int width, int height)
        {
            SFMLShader shader = GetShader<SFMLWallShader>(width);
            Vector2f max = origin + new Vector2f(width, height);
            return PhysicsSystem.CreateStaticBox(origin, max, true, shader, 1000000);
        }

        public static PhysicsObject CreateBox(Vector2f origin, int width, int height)
        {
            SFMLShader shader = GetShader<SFMLPolyShader>(width);
            // Compute mass from dimensions.
            float mass = width * height;
            Vector2f max = origin + new Vector2f(width, height);
            return PhysicsSystem.CreateStaticBox2(origin, max, false, shader, mass);
        }

        public static PhysicsObject CreatePolygonTriangle(Vector2f origin)
        {
            SFMLShader shader = GetShader<SFMLPolyShader>(0);
            var points = new Vector2f[]
            {
                new Vector2f(25, -25),
                new Vector2f(-25, -25),
                new Vector2f(0, 12.5f)
            };
            return PhysicsSystem.CreatePolygon(origin, points, shader);
        }

        public static PhysicsObject CreatePolygonCapsule(Vector2f origin)
        {
            SFMLShader shader = GetShader<SFMLPolyShader>(0);
            return PhysicsSystem.CreatePolygon(origin, PolygonShapeHelper.CreateCapsuleVertices(32, 20, 50).ToArray(), shader, canRotate: false);
        }

    }
}
