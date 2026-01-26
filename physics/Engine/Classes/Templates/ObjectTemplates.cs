using System;
using System.Collections.Generic;
using physics.Engine.Helpers;
using physics.Engine.Objects;
using physics.Engine.Shaders;
using physics.Engine.Structs;
using System.Numerics;

namespace physics.Engine.Classes.ObjectTemplates
{
    public class ObjectTemplates
    {
        private readonly PhysicsSystem _physicsSystem;

        // Outer dictionary keyed on the shader type.
        // Inner dictionary keyed on the object's diameter (as an int) with a shader instance as the value.
        private Dictionary<Type, Dictionary<int, SFMLShader>> shaderPool =
            new Dictionary<Type, Dictionary<int, SFMLShader>>();

        public ObjectTemplates(PhysicsSystem physicsSystem)
        {
            _physicsSystem = physicsSystem;
        }

        // Generic helper method to get (or create) a shader for a given diameter.
        private T GetShader<T>(int diameter) where T : SFMLShader
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

        public PhysicsObject CreateSmallBall(float originX, float originY)
        {
            // Using a diameter of 5.
            int diameter = 5;
            SFMLShader shader = GetShader<SFMLPolyRainbowShader>(diameter);
            return _physicsSystem.CreateStaticCircle(new Vector2(originX, originY), diameter, 0.8F, false, shader);
        }

        public PhysicsObject CreateSmallBall(float originX, float originY, float mass)
        {
            // Using a diameter of 5.
            int diameter = 5;
            SFMLShader shader = GetShader<SFMLPolyRainbowShader>(diameter);
            return _physicsSystem.CreateStaticCircle(new Vector2(originX, originY), diameter, 0.8F, false, shader);
        }

        public PhysicsObject CreateMedBall(float originX, float originY)
        {
            int diameter = 10;
            SFMLShader shader = GetShader<SFMLPolyRainbowShader>(diameter);
            return _physicsSystem.CreateStaticCircle(new Vector2(originX, originY), diameter, 0.8F, false, shader);
        }

        public PhysicsObject CreateLargeBall(float originX, float originY)
        {
            int diameter = 20;
            SFMLShader shader = GetShader<SFMLPolyShader>(diameter);
            return _physicsSystem.CreateStaticCircle(new Vector2(originX, originY), diameter, 0.8F, false, shader);
        }

        public PhysicsObject CreateAttractor(float originX, float originY)
        {
            int diameter = 50;
            // Use a different shader type for attractors.
            SFMLShader shader = GetShader<SFMLBallShader>(diameter);
            var oPhysicsObject = _physicsSystem.CreateStaticCircle(new Vector2(originX, originY), diameter, 0.95F, true, shader);
            _physicsSystem.ListGravityObjects.Add(oPhysicsObject);
            return oPhysicsObject;
        }

        public PhysicsObject CreateWall(Vector2 origin, int width, int height)
        {
            SFMLShader shader = GetShader<SFMLWallShader>(width);
            Vector2 max = origin + new Vector2(width, height);
            return _physicsSystem.CreateStaticBox(origin, max, true, shader, 1000000);
        }

        public PhysicsObject CreateBox(Vector2 origin, int width, int height)
        {
            SFMLShader shader = GetShader<SFMLPolyShader>(width);
            // Compute mass from dimensions.
            float mass = width * height;
            Vector2 max = origin + new Vector2(width, height);
            return _physicsSystem.CreateStaticBox2(origin, max, false, shader, mass);
        }

        public PhysicsObject CreatePolygonTriangle(Vector2 origin)
        {
            SFMLShader shader = GetShader<SFMLPolyShader>(0);
            var points = new Vector2[]
            {
                new Vector2(25, -25),
                new Vector2(-25, -25),
                new Vector2(0, 12.5f)
            };
            return _physicsSystem.CreatePolygon(origin, points, shader);
        }

        public PhysicsObject CreatePolygonCapsule(Vector2 origin)
        {
            SFMLShader shader = GetShader<SFMLPolyShader>(0);
            return _physicsSystem.CreatePolygon(origin, PolygonShapeHelper.CreateCapsuleVertices(32, 20, 50).ToArray(), shader, canRotate: false);
        }

    }
}
