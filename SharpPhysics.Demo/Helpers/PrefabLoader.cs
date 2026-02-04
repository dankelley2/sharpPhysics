#nullable enable
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpPhysics.Engine.Classes.ObjectTemplates;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Demo.Helpers;

#region Prefab Data Classes

public enum ShapeType
{
    Polygon,
    Circle,
    Rectangle
}

public enum ConstraintType
{
    Weld,
    Axis,
    Spring
}

public class PrefabShape
{
    [JsonPropertyName("type")]
    public ShapeType Type { get; set; }

    // For Polygon
    [JsonPropertyName("points")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vector2[]? Points { get; set; }

    // For Circle
    [JsonPropertyName("center")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Vector2 Center { get; set; }

    [JsonPropertyName("radius")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Radius { get; set; }

    // For Rectangle
    [JsonPropertyName("position")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Vector2 Position { get; set; }

    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Width { get; set; }

    [JsonPropertyName("height")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Height { get; set; }
}

public class PrefabConstraint
{
    [JsonPropertyName("type")]
    public ConstraintType Type { get; set; }

    [JsonPropertyName("shapeIndexA")]
    public int ShapeIndexA { get; set; }

    [JsonPropertyName("shapeIndexB")]
    public int ShapeIndexB { get; set; }

    [JsonPropertyName("anchorA")]
    public Vector2 AnchorA { get; set; }

    [JsonPropertyName("anchorB")]
    public Vector2 AnchorB { get; set; }
}

public class PrefabData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("shapes")]
    public PrefabShape[] Shapes { get; set; } = Array.Empty<PrefabShape>();

    [JsonPropertyName("constraints")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrefabConstraint[]? Constraints { get; set; }
}

#endregion

#region JSON Converter for Vector2

/// <summary>
/// Custom JSON converter for System.Numerics.Vector2 since it doesn't serialize well by default.
/// </summary>
public class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        float x = 0, y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = reader.GetString();
                reader.Read();

                if (propertyName == "x" || propertyName == "X")
                    x = reader.GetSingle();
                else if (propertyName == "y" || propertyName == "Y")
                    y = reader.GetSingle();
            }
        }

        return new Vector2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteEndObject();
    }
}

#endregion

/// <summary>
/// Utility class for loading prefab JSON files and instantiating them as physics objects.
/// </summary>
public class PrefabLoader
{
    private readonly GameEngine _engine;
    private readonly ObjectTemplates _objectTemplates;

    public PrefabLoader(GameEngine engine, ObjectTemplates objectTemplates)
    {
        _engine = engine;
        _objectTemplates = objectTemplates;
    }

    /// <summary>
    /// Loads a prefab from a JSON file and instantiates it at the specified world position.
    /// </summary>
    /// <param name="filePath">Path to the prefab JSON file.</param>
    /// <param name="worldPosition">World position to instantiate the prefab at.</param>
    /// <returns>A PrefabInstance containing all created physics objects and constraints.</returns>
    public PrefabInstance? LoadPrefab(string filePath, Vector2 worldPosition)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            return LoadPrefabFromJson(json, worldPosition);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading prefab from file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads a prefab from JSON string and instantiates it at the specified world position.
    /// </summary>
    public PrefabInstance? LoadPrefabFromJson(string json, Vector2 worldPosition)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new Vector2JsonConverter() }
            };

            var prefabData = JsonSerializer.Deserialize<PrefabData>(json, options);
            if (prefabData == null || prefabData.Shapes.Length == 0)
            {
                Console.WriteLine("Prefab data is empty or invalid.");
                return null;
            }

            return InstantiatePrefab(prefabData, worldPosition);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing prefab JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Instantiates a prefab at the specified world position.
    /// </summary>
    private PrefabInstance InstantiatePrefab(PrefabData prefabData, Vector2 worldPosition)
    {
        var instance = new PrefabInstance { Name = prefabData.Name };

        // Calculate the bounding box of all shapes to find the prefab center
        Vector2 prefabMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 prefabMax = new Vector2(float.MinValue, float.MinValue);

        foreach (var shape in prefabData.Shapes)
        {
            GetShapeBounds(shape, out var shapeMin, out var shapeMax);
            prefabMin = Vector2.Min(prefabMin, shapeMin);
            prefabMax = Vector2.Max(prefabMax, shapeMax);
        }

        Vector2 prefabCenter = (prefabMin + prefabMax) / 2f;
        Vector2 offset = worldPosition - prefabCenter;

        // Map shape index -> all physics objects for that shape
        // Compound bodies (concave polygons) have multiple parts
        var shapeObjectsMap = new List<List<PhysicsObject>>();

        // Create physics objects for each shape
        foreach (var shape in prefabData.Shapes)
        {
            var physicsObjects = CreatePhysicsObjects(shape, offset);
            shapeObjectsMap.Add(physicsObjects);

            // Add all objects to the instance (for tracking/cleanup)
            foreach (var obj in physicsObjects)
            {
                instance.Objects.Add(obj);
            }
        }

        // Create constraints between objects
        if (prefabData.Constraints != null)
        {
            foreach (var constraint in prefabData.Constraints)
            {
                if (constraint.ShapeIndexA < shapeObjectsMap.Count &&
                    constraint.ShapeIndexB < shapeObjectsMap.Count)
                {
                    var objectsA = shapeObjectsMap[constraint.ShapeIndexA];
                    var objectsB = shapeObjectsMap[constraint.ShapeIndexB];

                    if (objectsA.Count == 0 || objectsB.Count == 0)
                        continue;

                    if (constraint.Type == ConstraintType.Weld)
                    {
                        // Weld: both anchors point to the same world position
                        Vector2 worldAnchor = constraint.AnchorA + offset;

                        var objA = FindClosestObject(objectsA, worldAnchor);
                        var objB = FindClosestObject(objectsB, worldAnchor);

                        // Convert world offset to LOCAL space by un-rotating by object's current angle
                        Vector2 worldOffsetA = worldAnchor - objA.Center;
                        Vector2 worldOffsetB = worldAnchor - objB.Center;
                        Vector2 localAnchorA = PhysMath.RotateVector(worldOffsetA, -objA.Angle);
                        Vector2 localAnchorB = PhysMath.RotateVector(worldOffsetB, -objB.Angle);
                        _engine.AddWeldConstraint(objA, objB, localAnchorA, localAnchorB);
                    }
                    else if (constraint.Type == ConstraintType.Axis)
                    {
                        // Axis: Both anchors point to the same pivot point in world space
                        Vector2 pivotWorld = constraint.AnchorA + offset;

                        var objA = FindClosestObject(objectsA, pivotWorld);
                        var objB = FindClosestObject(objectsB, pivotWorld);

                        // Convert world offset to LOCAL space by un-rotating by object's current angle
                        Vector2 worldOffsetA = pivotWorld - objA.Center;
                        Vector2 worldOffsetB = pivotWorld - objB.Center;
                        Vector2 localAnchorA = PhysMath.RotateVector(worldOffsetA, -objA.Angle);
                        Vector2 localAnchorB = PhysMath.RotateVector(worldOffsetB, -objB.Angle);

                        Console.WriteLine($"  Axis constraint: Shape[{constraint.ShapeIndexA}] -> Shape[{constraint.ShapeIndexB}]");
                        Console.WriteLine($"    Pivot: {pivotWorld}");
                        Console.WriteLine($"    ObjA center: {objA.Center}, localAnchor: {localAnchorA}, Locked: {objA.Locked}");
                        Console.WriteLine($"    ObjB center: {objB.Center}, localAnchor: {localAnchorB}, Locked: {objB.Locked}");

                        _engine.AddAxisConstraint(objA, objB, localAnchorA, localAnchorB);
                    }
                    else if (constraint.Type == ConstraintType.Spring)
                    {
                        // Spring: Each anchor is on its respective shape
                        Vector2 worldAnchorA = constraint.AnchorA + offset;
                        Vector2 worldAnchorB = constraint.AnchorB + offset;

                        var objA = FindClosestObject(objectsA, worldAnchorA);
                        var objB = FindClosestObject(objectsB, worldAnchorB);

                        // Convert world anchors to local space for each object
                        Vector2 worldOffsetA = worldAnchorA - objA.Center;
                        Vector2 worldOffsetB = worldAnchorB - objB.Center;
                        Vector2 localAnchorA = PhysMath.RotateVector(worldOffsetA, -objA.Angle);
                        Vector2 localAnchorB = PhysMath.RotateVector(worldOffsetB, -objB.Angle);

                        Console.WriteLine($"  Spring constraint: Shape[{constraint.ShapeIndexA}] -> Shape[{constraint.ShapeIndexB}]");
                        Console.WriteLine($"    AnchorA: {worldAnchorA}, AnchorB: {worldAnchorB}");
                        Console.WriteLine($"    ObjA center: {objA.Center}, localAnchor: {localAnchorA}");
                        Console.WriteLine($"    ObjB center: {objB.Center}, localAnchor: {localAnchorB}");

                        _engine.AddSpringConstraint(objA, objB, localAnchorA, localAnchorB);
                    }
                }
            }
        }

        Console.WriteLine($"Instantiated prefab '{prefabData.Name}' at {worldPosition}");
        Console.WriteLine($"  Objects: {instance.Objects.Count}, Constraints: {prefabData.Constraints?.Length ?? 0}");

        return instance;
    }

    /// <summary>
    /// Finds the physics object whose center is closest to the given world point.
    /// </summary>
    private static PhysicsObject FindClosestObject(List<PhysicsObject> objects, Vector2 worldPoint)
    {
        PhysicsObject closest = objects[0];
        float minDistSq = Vector2.DistanceSquared(closest.Center, worldPoint);

        for (int i = 1; i < objects.Count; i++)
        {
            float distSq = Vector2.DistanceSquared(objects[i].Center, worldPoint);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = objects[i];
            }
        }

        return closest;
    }

    /// <summary>
    /// Creates physics objects for a shape. Returns a list since compound bodies have multiple parts.
    /// </summary>
    private List<PhysicsObject> CreatePhysicsObjects(PrefabShape shape, Vector2 offset)
    {
        var result = new List<PhysicsObject>();

        switch (shape.Type)
        {
            case ShapeType.Polygon:
                if (shape.Points == null || shape.Points.Length < 3)
                    return result;

                // Calculate centroid
                Vector2 centroid = Vector2.Zero;
                for (int i = 0; i < shape.Points.Length; i++)
                {
                    centroid += shape.Points[i];
                }
                centroid /= shape.Points.Length;

                // Calculate local vertices relative to centroid
                var localVertices = new Vector2[shape.Points.Length];
                for (int i = 0; i < shape.Points.Length; i++)
                {
                    localVertices[i] = shape.Points[i] - centroid;
                }

                // Create at world position (centroid + offset) using CreateConcavePolygon
                // CompoundBody now derives from PhysicsObject, so it IS the physics object
                var compoundBody = _objectTemplates.CreateConcavePolygon(
                    centroid + offset,
                    localVertices,
                    canRotate: true);

                // CompoundBody is now a single PhysicsObject
                result.Add(compoundBody);
                break;

            case ShapeType.Circle:
                var circlePos = shape.Center + new Vector2(shape.Radius, shape.Radius) + offset;
                // Create circle at top-left corner position using the saved radius
                var circle = _objectTemplates.CreateCircle(circlePos.X - shape.Radius, circlePos.Y - shape.Radius, shape.Radius);
                if (circle != null)
                    result.Add(circle);
                break;

            case ShapeType.Rectangle:
                var rectPos = shape.Position + offset;
                var box = _objectTemplates.CreateBox(rectPos, (int)shape.Width, (int)shape.Height);
                if (box != null)
                    result.Add(box);
                break;
        }

        return result;
    }

    private void GetShapeBounds(PrefabShape shape, out Vector2 min, out Vector2 max)
    {
        switch (shape.Type)
        {
            case ShapeType.Polygon:
                if (shape.Points == null || shape.Points.Length == 0)
                {
                    min = Vector2.Zero;
                    max = Vector2.Zero;
                    return;
                }

                min = shape.Points[0];
                max = shape.Points[0];
                foreach (var point in shape.Points)
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                return;

            case ShapeType.Circle:
                min = shape.Center - new Vector2(shape.Radius, shape.Radius);
                max = shape.Center + new Vector2(shape.Radius, shape.Radius);
                return;

            case ShapeType.Rectangle:
                min = shape.Position;
                max = shape.Position + new Vector2(shape.Width, shape.Height);
                return;

            default:
                min = Vector2.Zero;
                max = Vector2.Zero;
                return;
        }
    }

    /// <summary>
    /// Gets a list of available prefab files in the Resources/Prefabs directory.
    /// </summary>
    public static string[] GetAvailablePrefabs()
    {
        string prefabDir = Path.Combine("Resources", "Prefabs");
        if (!Directory.Exists(prefabDir))
            return Array.Empty<string>();

        return Directory.GetFiles(prefabDir, "*.json");
    }
}

/// <summary>
/// Represents an instantiated prefab with all its physics objects and constraints.
/// </summary>
public class PrefabInstance
{
    public string Name { get; set; } = "";
    public List<PhysicsObject> Objects { get; } = new();
}
