#nullable enable
using System.Numerics;
using SharpPhysics.Engine;
using SharpPhysics.Engine.Classes.ObjectTemplates;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Demo.DemoProps;

/// <summary>
/// Builds demo scenes with various physics demonstrations.
/// </summary>
public class DemoSceneBuilder
{
    private readonly GameEngine _engine;
    private readonly ObjectTemplates _templates;

    public DemoSceneBuilder(GameEngine engine, ObjectTemplates templates)
    {
        _engine = engine;
        _templates = templates;
    }

    public void CreateWalls(uint worldWidth, uint worldHeight)
    {
        _templates.CreateWall(new Vector2(0, 0), 15, (int)worldHeight);
        _templates.CreateWall(new Vector2((int)worldWidth - 15, 0), 15, (int)worldHeight);
        _templates.CreateWall(new Vector2(0, 0), (int)worldWidth, 15);
        _templates.CreateWall(new Vector2(0, (int)worldHeight - 15), (int)worldWidth, 15);
    }

    public DemoGameCar CreateCar(float carX = 300f, float carY = 500f)
    {
        float bodyWidth = 120f;
        float bodyHeight = 30f;
        float wheelRadius = 20f;
        float wheelInset = 10f;

        var carBody = _templates.CreateBox(new Vector2(carX, carY), (int)bodyWidth, (int)bodyHeight);

        float frontWheelLocalX = bodyWidth / 2f - wheelInset;
        float rearWheelLocalX = -bodyWidth / 2f + wheelInset;
        float frontWheelWorldX = carX + bodyWidth / 2f + frontWheelLocalX;
        float rearWheelWorldX = carX + bodyWidth / 2f + rearWheelLocalX;
        float wheelWorldY = carY + bodyHeight + wheelRadius;

        var frontWheel = _templates.CreateLargeBall(frontWheelWorldX - 10, wheelWorldY - 10);
        var rearWheel = _templates.CreateLargeBall(rearWheelWorldX - 10, wheelWorldY - 10);

        Vector2 frontAttachOnBody = new Vector2(frontWheelLocalX, bodyHeight / 2f + wheelRadius);
        Vector2 rearAttachOnBody = new Vector2(rearWheelLocalX, bodyHeight / 2f + wheelRadius);

        _engine.AddAxisConstraint(carBody, frontWheel, frontAttachOnBody, Vector2.Zero);
        _engine.AddAxisConstraint(carBody, rearWheel, rearAttachOnBody, Vector2.Zero);

        // Spoiler
        float spoilerWidth = 40f;
        float spoilerHeight = 8f;
        float spoilerLocalX = -bodyWidth / 2f + 75f;
        float spoilerLocalY = -bodyHeight / 2f - spoilerHeight / 2f - 2f;
        float spoilerWorldX = carX + bodyWidth + spoilerLocalX - spoilerWidth / 2f;
        float spoilerWorldY = carY + bodyHeight / 2f + spoilerLocalY - spoilerHeight / 2f;
        var spoiler = _templates.CreateBox(new Vector2(spoilerWorldX, spoilerWorldY), (int)spoilerWidth, (int)spoilerHeight);
        spoiler.Angle = 10f;
        _engine.AddWeldConstraint(carBody, spoiler, new Vector2(spoilerLocalX, spoilerLocalY), Vector2.Zero);

        // Bumpers
        float bumperWidth = 10f;
        float bumperHeight = 20f;
        var frontBumper = _templates.CreateBox(new Vector2(carX + bodyWidth, carY + 5f), (int)bumperWidth, (int)bumperHeight);
        _engine.AddWeldConstraint(carBody, frontBumper, new Vector2(bodyWidth / 2f, 0f), new Vector2(-bumperWidth / 2f, 0f));

        var rearBumper = _templates.CreateBox(new Vector2(carX - bumperWidth, carY + 5f), (int)bumperWidth, (int)bumperHeight);
        _engine.AddWeldConstraint(carBody, rearBumper, new Vector2(-bodyWidth / 2f, 0f), new Vector2(bumperWidth / 2f, 0f));

        return new DemoGameCar(carBody, frontWheel, rearWheel, frontBumper, rearBumper, true);
    }

    public void CreateSprocket(Vector2 center, int numBalls = 22, float radius = 80f)
    {
        PhysicsObject? firstBall = null;
        PhysicsObject? prevBall = null;

        for (int i = 0; i < numBalls; i++)
        {
            float angle = i * (2 * MathF.PI / numBalls);
            Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var ball = _templates.CreateMedBall(pos.X - 10, pos.Y - 10);

            if (i == 0) firstBall = ball;
            if (prevBall != null) _engine.AddWeldConstraint(prevBall, ball);
            prevBall = ball;
        }

        if (firstBall != null && prevBall != null)
            _engine.AddWeldConstraint(prevBall, firstBall);
    }

    public void CreateBlanket(Vector2 origin, int countX = 12, int countY = 12, int spacing = 20)
    {
        PhysicsObject[][] grid = new PhysicsObject[countX][];

        for (int x = 0; x < countX; x++)
        {
            grid[x] = new PhysicsObject[countY];
            for (int y = 0; y < countY; y++)
            {
                grid[x][y] = _templates.CreateSmallBall(origin.X + x * spacing, origin.Y + y * spacing);
                if (x > 0) _engine.AddSpringConstraint(grid[x - 1][y], grid[x][y]);
                if (y > 0) _engine.AddSpringConstraint(grid[x][y - 1], grid[x][y]);
            }
        }
    }

    public void CreateChain(Vector2 start, int links = 12, int linkSpacing = 25)
    {
        PhysicsObject? prevObject = null;

        for (int i = 0; i < links; i++)
        {
            var currentObj = _templates.CreateMedBall(start.X + (i * linkSpacing), start.Y);

            if (i == 0)
            {
                var anchor = _templates.CreateBox(new Vector2(start.X - 25, start.Y - 10), 20, 20);
                anchor.Locked = true;
                _engine.AddAxisConstraint(anchor, currentObj);
            }

            if (prevObject != null)
                _engine.AddAxisConstraint(prevObject, currentObj);

            if (i == links - 1)
            {
                var anchor = _templates.CreateBox(new Vector2(start.X + (i * linkSpacing), start.Y - 10), 20, 20);
                anchor.Locked = true;
                _engine.AddAxisConstraint(currentObj, anchor);
            }

            prevObject = currentObj;
        }
    }

    public void CreateBridge(Vector2 start, int segments = 12, int segmentSpacing = 25)
    {
        PhysicsObject? prevObject = null;

        for (int i = 0; i < segments; i++)
        {
            var currentObj = _templates.CreateMedBall(start.X + (i * segmentSpacing), start.Y);
            if (prevObject != null)
                _engine.AddWeldConstraint(prevObject, currentObj);
            prevObject = currentObj;
        }
    }

    public void CreateConcavePolygonDemo(Vector2 position)
    {
        var lShapeVertices = new Vector2[]
        {
            new(0, 0), new(60, 0), new(60, 25),
            new(25, 25), new(25, 60), new(0, 60)
        };
        _templates.CreateConcavePolygon(position, lShapeVertices, canRotate: true, canBreak: true);

        var starVertices = new Vector2[]
        {
            new(25, 0), new(30, 18), new(50, 18), new(35, 30), new(40, 50),
            new(25, 38), new(10, 50), new(15, 30), new(0, 18), new(20, 18)
        };
        _templates.CreateConcavePolygon(position + new Vector2(-100, 0), starVertices, canRotate: true, canBreak: true);
    }
}
