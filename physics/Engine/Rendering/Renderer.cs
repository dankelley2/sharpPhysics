using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using physics.Engine.Rendering.UI;
using SFML.Window;
using System.Numerics;
using physics.Engine.Shaders;
using physics.Engine.Objects;
using physics.Engine.Helpers;
using System.Security.AccessControl;

namespace physics.Engine.Rendering
{
    public class Renderer
    {
        public RenderWindow Window { get; private set; }
        public View GameView { get; private set; }
        public View UiView { get; private set; }

        private Text debugText;
        private Text _reusableText; // Reusable text object for DrawText calls
        private Font debugFont;
        private UiManager _debugUiManager = new UiManager();
        private PhysicsSystem _physicsSystem;

        /// <summary>
        /// Gets the debug UI manager. Games like DemoGame can use this for physics debug controls.
        /// </summary>
        public UiManager DebugUiManager => _debugUiManager;

        /// <summary>
        /// Gets the default font used for UI rendering.
        /// </summary>
        public Font DefaultFont => debugFont;

        /// <summary>
        /// Controls whether the debug UI (sliders, debug text) is shown.
        /// </summary>
        public bool ShowDebugUI { get; set; } = true;

        /// <summary>
        /// Draws text at the specified position (in screen coordinates).
        /// Call this during game's Render method.
        /// </summary>
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

            // Create and set a view covering the whole window.
            GameView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
            UiView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
            Window.SetFramerateLimit(144);

            _physicsSystem = physicsSystem;
            InitializeUi(windowWidth, windowHeight);
        }

        /// <summary>
        /// Initialize UI elements for the window
        /// </summary>
        /// <param name="windowWidth"></param>
        /// <param name="windowHeight"></param>
        private void InitializeUi(uint windowWidth, uint windowHeight)
        {
            // Load the font from the embedded Resources folder.
            // This path is relative to the working directory (usually the output folder).
            debugFont = new Font("Resources/good_timing_bd.otf");
            debugText = new Text("", debugFont, 12)
            {
                FillColor = Color.White,
                Position = new Vector2f(40, 40)
            };
            _reusableText = new Text("", debugFont, 24); // Reusable text for DrawText calls

                // Note: Debug UI elements are managed by _debugUiManager

                UiElement roundedRect = new UiRoundedRectangle(new Vector2(140, 80), 5, 32)
                {
                    OutlineColor = Color.Red
                };
                roundedRect.Position = new Vector2(30, 30);
                _debugUiManager.Add(roundedRect);

                // UI Elements for "Enable viewing normals"
                var viewingNormalsLabel = new UiTextLabel("Contact Normals", debugFont)
                {
                    Position = new Vector2(200, 30),
                    CharacterSize = 14 // optional customization
                };
                var viewingNormalsCheckbox = new UiCheckbox(new Vector2(350, 30), new Vector2(20, 20));
                viewingNormalsCheckbox.IsChecked = SFMLPolyShader.DrawNormals;
                viewingNormalsCheckbox.OnClick += (isChecked) =>
                {
                    SFMLPolyShader.DrawNormals = isChecked;
                };
                _debugUiManager.Add(viewingNormalsLabel);
                _debugUiManager.Add(viewingNormalsCheckbox);

                // Add Gravity X control
                var gravityXLabelPosition = new Vector2(200, 60);
                var gravityXSliderPosition = new Vector2(200, 80);

                var gravityXLabel = new UiTextLabel("Gravity X", debugFont)
                {
                    Position = gravityXLabelPosition,
                    CharacterSize = 14
                };
                _debugUiManager.Add(gravityXLabel);

                var gravityXSlider = new UiSlider(gravityXSliderPosition, new Vector2(150, 20), -20f, 20f, _physicsSystem.Gravity.X);
                gravityXSlider.OnValueChanged += (value) =>
                {
                    var currentGravity = _physicsSystem.Gravity;
                    _physicsSystem.Gravity = new Vector2(value, currentGravity.Y);
                };
                _debugUiManager.Add(gravityXSlider);

                // Add Gravity Y control
                var gravityYLabelPosition = new Vector2(400, 60);
                var gravityYSliderPosition = new Vector2(400, 80);

                var gravityYLabel = new UiTextLabel("Gravity Y", debugFont)
                {
                    Position = gravityYLabelPosition,
                    CharacterSize = 14
                };
                _debugUiManager.Add(gravityYLabel);

                var gravityYSlider = new UiSlider(gravityYSliderPosition, new Vector2(150, 20), -20f, 20f, _physicsSystem.Gravity.Y);
                gravityYSlider.OnValueChanged += (value) =>
                {
                    var currentGravity = _physicsSystem.Gravity;
                    _physicsSystem.Gravity = new Vector2(currentGravity.X, value);
                };
                _debugUiManager.Add(gravityYSlider);

                // Add Friction slider and label (200px to the right of the checkbox)
                var frictionLabelPosition = new Vector2(400, 110); // Position above the slider
                var frictionSliderPosition = new Vector2(400, 130); // Same Y as checkbox

                // Create and add friction label
                var frictionLabel = new UiTextLabel("Friction", debugFont)
                {
                    Position = frictionLabelPosition,
                    CharacterSize = 14
                };
                _debugUiManager.Add(frictionLabel);

                // Create and add friction slider
                var frictionSlider = new UiSlider(frictionSliderPosition, new Vector2(150, 20), 0f, 1f, PhysicsObject.Friction);
                frictionSlider.OnValueChanged += (value) =>
                {
                    PhysicsObject.Friction = value;
                };
                _debugUiManager.Add(frictionSlider);

                // Add Simulation Speed control
                var simSpeedLabelPosition = new Vector2(200, 110);
                var simSpeedSliderPosition = new Vector2(200, 130);

                var simSpeedLabel = new UiTextLabel("Simulation Speed", debugFont)
                {
                    Position = simSpeedLabelPosition,
                    CharacterSize = 14
                };
                _debugUiManager.Add(simSpeedLabel);

                var simSpeedSlider = new UiSlider(simSpeedSliderPosition, new Vector2(150, 20), 0.1f, 2f, _physicsSystem.TimeScale);
                simSpeedSlider.OnValueChanged += (value) =>
                {
                    _physicsSystem.TimeScale = value;
                };
                _debugUiManager.Add(simSpeedSlider);

                // Add Pause/Resume button
                var pauseButtonPosition = new Vector2(450, 30);
                var pauseButton = new UiButton("Pause", debugFont, pauseButtonPosition, new Vector2(70, 20));
                pauseButton.OnClick += (state) => 
                {
                    _physicsSystem.IsPaused = !_physicsSystem.IsPaused;
                    pauseButton.Text = _physicsSystem.IsPaused ? "Resume" : "Pause";
                };
                _debugUiManager.Add(pauseButton);
            }

        /// <summary>
        /// Render the game window view and UI elements view
        /// </summary>
        /// <param name="msPhysicsTime"></param>
        /// <param name="msDrawTime"></param>
        /// <param name="msFrameTime"></param>
        public void Render(long msPhysicsTime, long msDrawTime, long msFrameTime)
        {

            // Draw Game View
            DrawGameView();

            DrawUiView(msPhysicsTime, msDrawTime, msFrameTime);

            // Note: Window.Display() is called by GameEngine after game-specific rendering
        }

        /// <summary>
        /// Presents the rendered frame to the screen.
        /// Called by GameEngine after all rendering (engine + game) is complete.
        /// </summary>
        public void Display()
        {
            Window.Display();
        }

        private void DrawGameView()
        {
            // Switch to Game window view
            Window.SetView(GameView);

            // Clear with black color
            Window.Clear(Color.Black);

            // Draw all static objects with their shaders.
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

            // Draw all static objects with their shaders.
            // foreach (var obj in _physicsSystem.Constraints)
            // {
            //     var a = obj.A.Center + PhysMath.RotateVector(obj.AnchorA, obj.A.Angle);
            //     var b = obj.B.Center + PhysMath.RotateVector(obj.AnchorB, obj.B.Angle);
            //     DrawLine(obj.A.Center, a, Color.Yellow, 2f);
            //     DrawLine(obj.B.Center, b, Color.Red, 2f);
            // }
        }

        #region Public Primitive Drawing Methods

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

            var direction = end - start;
            var length = direction.Length();
            var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

            var line = new RectangleShape(new Vector2f(length, thickness))
            {
                Position = new Vector2f(start.X, start.Y),
                FillColor = color,
                Rotation = angle,
                Origin = new Vector2f(0, thickness / 2)
            };

            Window.Draw(line);
        }

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

            var circle = new CircleShape(radius)
            {
                Position = new Vector2f(center.X - radius, center.Y - radius),
                FillColor = fillColor,
                OutlineColor = outlineColor ?? Color.White,
                OutlineThickness = outlineThickness
            };

            Window.Draw(circle);
        }

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

            var rect = new RectangleShape(new Vector2f(size.X, size.Y))
            {
                Position = new Vector2f(position.X, position.Y),
                FillColor = fillColor,
                OutlineColor = outlineColor ?? Color.Transparent,
                OutlineThickness = outlineThickness
            };

            Window.Draw(rect);
        }

        /// <summary>
        /// Draws a polygon (connected line segments) in game coordinates.
        /// More efficient than multiple DrawLine calls for connected paths.
        /// </summary>
        /// <param name="points">Array of points defining the polygon vertices.</param>
        /// <param name="color">Color of the lines.</param>
        /// <param name="thickness">Thickness of the lines.</param>
        /// <param name="closed">If true, connects the last point to the first.</param>
        public void DrawPolygon(Vector2[] points, Color color, float thickness = 2f, bool closed = false)
        {
            if (points == null || points.Length < 2) return;

            Window.SetView(GameView);

            int count = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < count; i++)
            {
                var start = points[i];
                var end = points[(i + 1) % points.Length];

                var direction = end - start;
                var length = direction.Length();
                var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

                var line = new RectangleShape(new Vector2f(length, thickness))
                {
                    Position = new Vector2f(start.X, start.Y),
                    FillColor = color,
                    Rotation = angle,
                    Origin = new Vector2f(0, thickness / 2)
                };

                Window.Draw(line);
            }
        }

        /// <summary>
        /// Draws a filled convex polygon in game coordinates.
        /// </summary>
        /// <param name="points">Array of points defining the polygon vertices (must be convex).</param>
        /// <param name="fillColor">Fill color.</param>
        /// <param name="outlineColor">Optional outline color.</param>
        /// <param name="outlineThickness">Outline thickness.</param>
        public void DrawFilledPolygon(Vector2[] points, Color fillColor, Color? outlineColor = null, float outlineThickness = 0f)
        {
            if (points == null || points.Length < 3) return;

            Window.SetView(GameView);

            var convex = new ConvexShape((uint)points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                convex.SetPoint((uint)i, new Vector2f(points[i].X, points[i].Y));
            }

            convex.FillColor = fillColor;
            convex.OutlineColor = outlineColor ?? Color.Transparent;
            convex.OutlineThickness = outlineThickness;

            Window.Draw(convex);
        }

        /// <summary>
        /// Draws multiple line segments efficiently (for skeleton rendering, etc.).
        /// Each pair of points defines a line segment.
        /// </summary>
        /// <param name="segments">Array of line segments as (start, end) tuples.</param>
        /// <param name="color">Color for all lines.</param>
        /// <param name="thickness">Thickness of the lines.</param>
        public void DrawLineSegments((Vector2 Start, Vector2 End)[] segments, Color color, float thickness = 2f)
        {
            if (segments == null || segments.Length == 0) return;

            Window.SetView(GameView);

            foreach (var (start, end) in segments)
            {
                var direction = end - start;
                var length = direction.Length();
                if (length < 0.001f) continue; // Skip zero-length lines

                var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

                var line = new RectangleShape(new Vector2f(length, thickness))
                {
                    Position = new Vector2f(start.X, start.Y),
                    FillColor = color,
                    Rotation = angle,
                    Origin = new Vector2f(0, thickness / 2)
                };

                Window.Draw(line);
            }
        }

        /// <summary>
        /// Draws multiple line segments with individual colors.
        /// </summary>
        /// <param name="segments">Array of line segments with colors.</param>
        /// <param name="thickness">Thickness of the lines.</param>
        public void DrawColoredLineSegments((Vector2 Start, Vector2 End, Color Color)[] segments, float thickness = 2f)
        {
            if (segments == null || segments.Length == 0) return;

            Window.SetView(GameView);

            foreach (var (start, end, color) in segments)
            {
                var direction = end - start;
                var length = direction.Length();
                if (length < 0.001f) continue;

                var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

                var line = new RectangleShape(new Vector2f(length, thickness))
                {
                    Position = new Vector2f(start.X, start.Y),
                    FillColor = color,
                    Rotation = angle,
                    Origin = new Vector2f(0, thickness / 2)
                };

                Window.Draw(line);
            }
        }

        #endregion

                        private void DrawUiView(long msPhysicsTime, long msDrawTime, long msFrameTime)
                        {
                            // Switch to UI window view
                            Window.SetView(UiView);

                            // Draw debug info only if enabled
                            if (ShowDebugUI)
                            {
                                debugText.DisplayedString =
                                    $"ms physics time: {msPhysicsTime}\n" +
                                    $"ms draw time: {msDrawTime}\n" +
                                    $"frame rate: {1000 / Math.Max(msFrameTime, 1)}\n" +
                                    $"num objects: {_physicsSystem.ListStaticObjects.Count}";
                                Window.Draw(debugText);

                                // Draw debug UI elements
                                _debugUiManager.Draw(Window);
                            }
                        }
                    }
                }
