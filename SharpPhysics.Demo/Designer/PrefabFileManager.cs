#nullable enable
using System.Numerics;
using System.Text.Json;
using SharpPhysics.Demo.Helpers;

namespace SharpPhysics.Demo.Designer;

/// <summary>
/// Handles saving and loading prefab files for the prefab designer.
/// </summary>
public static class PrefabFileManager
{
    private const string PrefabDirectory = "Resources/Prefabs";

    /// <summary>
    /// Saves a prefab to a JSON file.
    /// </summary>
    /// <param name="shapes">The shapes to save.</param>
    /// <param name="constraints">The constraints to save.</param>
    /// <param name="name">Optional custom name. If null, generates timestamp-based name.</param>
    /// <returns>The path to the saved file, or null if save failed.</returns>
    public static string? SavePrefab(IReadOnlyList<PrefabShape> shapes, IReadOnlyList<PrefabConstraint> constraints, string? name = null)
    {
        if (shapes.Count == 0)
        {
            Console.WriteLine("No shapes to save!");
            return null;
        }

        var prefab = new PrefabData
        {
            Name = name ?? $"Prefab_{DateTime.Now:yyyyMMdd_HHmmss}",
            Shapes = shapes.ToArray(),
            Constraints = constraints.Count > 0 ? constraints.ToArray() : null
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new Vector2JsonConverter() }
        };

        string json = JsonSerializer.Serialize(prefab, options);

        // Ensure Resources/Prefabs directory exists
        Directory.CreateDirectory(PrefabDirectory);

        string filePath = Path.Combine(PrefabDirectory, $"{prefab.Name}.json");
        File.WriteAllText(filePath, json);

        Console.WriteLine($"Saved prefab to: {filePath}");
        Console.WriteLine($"  Shapes: {shapes.Count}, Constraints: {constraints.Count}");

        return filePath;
    }

    /// <summary>
    /// Loads a prefab from a JSON file.
    /// </summary>
    /// <param name="filePath">The path to the prefab file.</param>
    /// <returns>The loaded prefab data, or null if load failed.</returns>
    public static PrefabData? LoadPrefab(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);

            var options = new JsonSerializerOptions
            {
                Converters = { new Vector2JsonConverter() }
            };

            var prefab = JsonSerializer.Deserialize<PrefabData>(json, options);

            if (prefab == null)
            {
                Console.WriteLine("Failed to parse prefab file.");
                return null;
            }

            Console.WriteLine($"Loaded prefab '{prefab.Name}'");
            Console.WriteLine($"  Shapes: {prefab.Shapes.Length}, Constraints: {prefab.Constraints?.Length ?? 0}");

            return prefab;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading prefab: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all available prefab files in the prefab directory.
    /// </summary>
    /// <returns>Array of prefab file paths, or empty array if none found.</returns>
    public static string[] GetAvailablePrefabs()
    {
        if (!Directory.Exists(PrefabDirectory))
        {
            return [];
        }

        return Directory.GetFiles(PrefabDirectory, "*.json");
    }

    /// <summary>
    /// Lists available prefabs to the console and returns the file paths.
    /// </summary>
    /// <returns>Array of available prefab file paths.</returns>
    public static string[] ListAvailablePrefabs()
    {
        var files = GetAvailablePrefabs();

        if (files.Length == 0)
        {
            Console.WriteLine("No prefab files found in Resources/Prefabs/");
            return [];
        }

        Console.WriteLine("Available prefabs:");
        for (int i = 0; i < files.Length; i++)
        {
            Console.WriteLine($"  {i + 1}: {Path.GetFileNameWithoutExtension(files[i])}");
        }

        return files;
    }

    /// <summary>
    /// Gets the most recently modified prefab file.
    /// </summary>
    /// <returns>Path to the most recent prefab, or null if none exist.</returns>
    public static string? GetMostRecentPrefab()
    {
        var files = GetAvailablePrefabs();
        return files.Length > 0 ? files[^1] : null;
    }
}
