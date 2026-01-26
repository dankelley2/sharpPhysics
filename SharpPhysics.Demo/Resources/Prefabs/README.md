# Prefabs Directory

This folder contains JSON prefab files created by the Prefab Designer game.

Each prefab file contains shape definitions that can be loaded and instantiated in other games.

## File Format

```json
{
  "name": "PrefabName",
  "shapes": [
    {
      "type": "Polygon",
      "points": [
        { "X": 0, "Y": 0 },
        { "X": 100, "Y": 0 },
        { "X": 50, "Y": 100 }
      ]
    },
    {
      "type": "Circle",
      "center": { "X": 200, "Y": 200 },
      "radius": 50
    },
    {
      "type": "Rectangle",
      "position": { "X": 300, "Y": 100 },
      "width": 100,
      "height": 60
    }
  ]
}
```
