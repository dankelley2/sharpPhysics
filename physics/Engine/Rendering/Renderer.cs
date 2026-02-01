using SFML.Graphics;
using SFML.System;
using System;
using SFML.Window;
using System.Numerics;
using physics.Engine.Helpers;

namespace physics.Engine.Rendering
{
    public class Renderer
    {
        public RenderWindow Window { get; private set; }
        public View GameView { get; private set; }
        public View UiView { get; private set; }

        private Text _reusableText;
        private Font _defaultFont;
        private PhysicsSystem _physicsSystem;
        private Color _ConstraintAColor = new Color(255, 0, 0, 100);
        private Color _ConstraintBColor = new Color(0, 255, 255, 100);

        public Font DefaultFont => _defaultFont;

        public void DrawText(string text, float x, float y, uint size = 24, Color? color = null)
        {
            Window.SetView(UiView);
            _reusableText.DisplayedString = text;
            _reusableText.CharacterSize = size;
            _reusableText.Position = new Vector2f(x, y);
            _reusableText.FillColor = color ?? Color.White;
            Window.Draw(_reusableText);
        }

        public Renderer(uint windowWidth, uint windowHeight, string windowTitle, PhysicsSystem physicsSystem)
        {
            ContextSettings settings = new ContextSettings();
            settings.AntialiasingLevel = 8;
            Window = new RenderWindow(new VideoMode(windowWidth, windowHeight), windowTitle, Styles.Close, settings);
            Window.Closed += (s, e) => Window.Close();

            GameView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
            UiView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
            Window.SetFramerateLimit(144);

            _physicsSystem = physicsSystem;

            _defaultFont = new Font("Resources/good_timing_bd.otf");
            _reusableText = new Text("", _defaultFont, 24);
        }

        #region View Pan/Zoom Methods

        public void PanView(Vector2 delta)
        {
            GameView.Center += new Vector2f(delta.X, delta.Y);
        }

        /// <summary>
        /// Pans the game view based on screen pixel movement.
        /// Converts pixel delta to world coordinates accounting for current zoom.
        /// </summary>
        /// <param name="previousScreenPos">Previous mouse screen position.</param>
        /// <param name="currentScreenPos">Current mouse screen position.</param>
        public void PanViewByScreenDelta(Vector2 previousScreenPos, Vector2 currentScreenPos)
        {
            var prevWorld = Window.MapPixelToCoords(
                new Vector2i((int)previousScreenPos.X, (int)previousScreenPos.Y), GameView);
            var currWorld = Window.MapPixelToCoords(
                new Vector2i((int)currentScreenPos.X, (int)currentScreenPos.Y), GameView);

            Vector2 delta = new Vector2(prevWorld.X - currWorld.X, prevWorld.Y - currWorld.Y);
            PanView(delta);
        }

        /// <summary>
        /// Zooms the game view by a factor. Positive = zoom in, Negative = zoom out.
        /// Optionally focuses on a specific world point (keeps that point stationary during zoom).
        /// </summary>
        /// <param name="zoomDelta">Zoom amount. Positive zooms in, negative zooms out.</param>
        /// <param name="focusScreenPos">Optional screen position to zoom towards (e.g., mouse position).</param>
        public void ZoomView(float zoomDelta, Vector2? focusScreenPos = null)
        {
            // Convert delta to zoom factor (e.g., scroll wheel delta of 1 = 10% zoom)
            float zoomFactor = 1f - (zoomDelta * 0.1f);
            zoomFactor = Math.Clamp(zoomFactor, 0.5f, 2f); // Limit single-frame zoom

            if (focusScreenPos.HasValue)
            {
                // Zoom towards the focus point (keeps that world point under the cursor)
                var worldPosBefore = Window.MapPixelToCoords(
                    new Vector2i((int)focusScreenPos.Value.X, (int)focusScreenPos.Value.Y), GameView);

                GameView.Zoom(zoomFactor);

                var worldPosAfter = Window.MapPixelToCoords(
                    new Vector2i((int)focusScreenPos.Value.X, (int)focusScreenPos.Value.Y), GameView);

                // Adjust center to keep focus point stationary
                Vector2f offset = new Vector2f(
                    worldPosBefore.X - worldPosAfter.X,
                    worldPosBefore.Y - worldPosAfter.Y);
                GameView.Center += offset;
            }
            else
            {
                GameView.Zoom(zoomFactor);
            }
        }

        /// <summary>
        /// Resets the game view to its default state (original size and position).
        /// </summary>
        /// <param name="windowWidth">Window width in pixels.</param>
        /// <param name="windowHeight">Window height in pixels.</param>
        public void ResetView(uint windowWidth, uint windowHeight)
        {
            GameView.Reset(new FloatRect(0, 0, windowWidth, windowHeight));
        }

        #endregion

        public void BeginFrame()
        {
            Window.SetView(GameView);
            Window.Clear(Color.Black);
        }

        /// <summary>
        /// Renders physics objects and constraints.
        /// Called after game background rendering.
        /// </summary>
        public void RenderPhysicsObjects()
        {
            Window.SetView(GameView);

            foreach (var obj in _physicsSystem.ListStaticObjects)
            {
                var sfmlShader = obj.Shader;
                if (sfmlShader != null)
                {
                    sfmlShader.PreDraw(obj, Window);
                    sfmlShader.Draw(obj, Window);
                    sfmlShader.PostDraw(obj, Window);
                }
            }

            foreach (var obj in _physicsSystem.Constraints)
            {
                var a = obj.A.Center + PhysMath.RotateVector(obj.AnchorA, obj.A.Angle);
                var b = obj.B.Center + PhysMath.RotateVector(obj.AnchorB, obj.B.Angle);
                DrawLine(obj.A.Center, a, _ConstraintAColor, 2f);
                DrawLine(obj.B.Center, b, _ConstraintBColor, 2f);
            }
        }

        public void Display()
        {
            Window.Display();
        }

        #region Public Primitive Drawing Methods


        private readonly VertexArray _lineRenderer = new VertexArray(PrimitiveType.Lines, 2);
        /// <summary>
        /// Draws a line between two points in game coordinates.
        /// </summary>
        /// <param name="start">Start point of the line.</param>
        /// <param name="end">End point of the line.</param>
        /// <param name="color">Color of the line.</param>
        /// <param name="thickness">Thickness of the line in pixels.</param>
        public void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 2f)
        {
            Window.SetView(GameView);

            _lineRenderer[0] = new Vertex(new Vector2f(start.X, start.Y), color);
            _lineRenderer[1] = new Vertex(new Vector2f(end.X, end.Y), color);

            Window.Draw(_lineRenderer);
        }


        private readonly CircleShape _circleShapeRenderer = new CircleShape();
        /// <summary>
        /// Draws a circle at the specified position in game coordinates.
        /// </summary>
        /// <param name="center">Center position of the circle.</param>
        /// <param name="radius">Radius of the circle.</param>
        /// <param name="fillColor">Fill color of the circle.</param>
        /// <param name="outlineColor">Optional outline color. If null, uses white.</param>
        /// <param name="outlineThickness">Thickness of the outline. Default 2.</param>
        public void DrawCircle(Vector2 center, float radius, Color fillColor, Color? outlineColor = null, float outlineThickness = 2f)
        {
            Window.SetView(GameView);

            _circleShapeRenderer.Radius = radius;
            _circleShapeRenderer.Position = new Vector2f(center.X - radius, center.Y - radius);
            _circleShapeRenderer.FillColor = fillColor;
            _circleShapeRenderer.OutlineColor = outlineColor ?? Color.White;
            _circleShapeRenderer.OutlineThickness = outlineThickness;

            Window.Draw(_circleShapeRenderer);
        }

        private readonly RectangleShape _rectangleShapeRenderer = new RectangleShape();
        /// <summary>
        /// Draws a filled rectangle in game coordinates.
        /// </summary>
        /// <param name="position">Top-left corner position.</param>
        /// <param name="size">Size of the rectangle (width, height).</param>
        /// <param name="fillColor">Fill color.</param>
        /// <param name="outlineColor">Optional outline color.</param>
        /// <param name="outlineThickness">Outline thickness.</param>
        public void DrawRectangle(Vector2 position, Vector2 size, Color fillColor, Color? outlineColor = null, float outlineThickness = 0f)
        {
            Window.SetView(GameView);

            //var rect = new RectangleShape(new Vector2f(size.X, size.Y))
            //{
            //    Position = new Vector2f(position.X, position.Y),
            //    FillColor = fillColor,
            //    OutlineColor = outlineColor ?? Color.Transparent,
            //    OutlineThickness = outlineThickness
            //};

            _rectangleShapeRenderer.Size = new Vector2f(size.X, size.Y);
            _rectangleShapeRenderer.Position = new Vector2f(position.X, position.Y);
            _rectangleShapeRenderer.FillColor = fillColor;
            _rectangleShapeRenderer.OutlineColor = outlineColor ?? Color.Transparent;
            _rectangleShapeRenderer.OutlineThickness = outlineThickness;

            Window.Draw(_rectangleShapeRenderer);
        }
        #endregion
    }
}
