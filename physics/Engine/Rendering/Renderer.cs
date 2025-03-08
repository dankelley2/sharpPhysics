using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using physics.Engine.Rendering.UI;
using SFML.Window;
using System.Numerics;
using physics.Engine.Shaders;
using physics.Engine.Objects;

namespace physics.Engine.Rendering
{
    public class Renderer
    {
        public RenderWindow Window { get; private set; }
        public View GameView { get; private set; }
        public View UiView { get; private set; }

        private Text debugText;
        private Font debugFont;
        private List<UiElement> _uiElements = new List<UiElement>();
        private PhysicsSystem _physicsSystem;

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

            UiElement roundedRect = new UiRoundedRectangle(new Vector2(140, 80), 5, 32)
            {
                OutlineColor = Color.Red
            };
            roundedRect.Position = new Vector2(30, 30);
            _uiElements.Add(roundedRect);

            // UI Elements for "Enable viewing normals"
            var viewingNormalsLabel = new UiTextLabel("Enable viewing normals", debugFont)
            {
                Position = new Vector2(200, 30),
                CharacterSize = 14 // optional customization
            };
            var viewingNormalsCheckbox = new UiCheckbox(new Vector2(400, 30), new Vector2(20, 20));
            viewingNormalsCheckbox.IsChecked = SFMLPolyShader.DrawNormals;
            viewingNormalsCheckbox.OnClick += (isChecked) =>
            {
                SFMLPolyShader.DrawNormals = isChecked;
            };
            _uiElements.Add(viewingNormalsLabel);
            _uiElements.Add(viewingNormalsCheckbox);
            
            // Add Friction slider and label (200px to the right of the checkbox)
            var frictionLabelPosition = new Vector2(600, 10); // Position above the slider
            var frictionSliderPosition = new Vector2(600, 30); // Same Y as checkbox
            
            // Create and add friction label
            var frictionLabel = new UiTextLabel("Friction", debugFont)
            {
                Position = frictionLabelPosition,
                CharacterSize = 14
            };
            _uiElements.Add(frictionLabel);
            
            // Create and add friction slider
            var frictionSlider = new UiSlider(frictionSliderPosition, new Vector2(150, 20), 0f, 1f, PhysicsObject.Friction);
            frictionSlider.OnValueChanged += (value) =>
            {
                PhysicsObject.Friction = value;
            };
            _uiElements.Add(frictionSlider);
            
            // Add Gravity X control
            var gravityXLabelPosition = new Vector2(200, 60);
            var gravityXSliderPosition = new Vector2(200, 80);
            
            var gravityXLabel = new UiTextLabel("Gravity X", debugFont)
            {
                Position = gravityXLabelPosition,
                CharacterSize = 14
            };
            _uiElements.Add(gravityXLabel);
            
            var gravityXSlider = new UiSlider(gravityXSliderPosition, new Vector2(150, 20), -20f, 20f, _physicsSystem.Gravity.X);
            gravityXSlider.OnValueChanged += (value) =>
            {
                var currentGravity = _physicsSystem.Gravity;
                _physicsSystem.Gravity = new Vector2(value, currentGravity.Y);
            };
            _uiElements.Add(gravityXSlider);
            
            // Add Gravity Y control
            var gravityYLabelPosition = new Vector2(400, 60);
            var gravityYSliderPosition = new Vector2(400, 80);
            
            var gravityYLabel = new UiTextLabel("Gravity Y", debugFont)
            {
                Position = gravityYLabelPosition,
                CharacterSize = 14
            };
            _uiElements.Add(gravityYLabel);
            
            var gravityYSlider = new UiSlider(gravityYSliderPosition, new Vector2(150, 20), -20f, 20f, _physicsSystem.Gravity.Y);
            gravityYSlider.OnValueChanged += (value) =>
            {
                var currentGravity = _physicsSystem.Gravity;
                _physicsSystem.Gravity = new Vector2(currentGravity.X, value);
            };
            _uiElements.Add(gravityYSlider);
            
            // //Add Restitution (bounciness) control
            // var restitutionLabelPosition = new Vector2(600, 60);
            // var restitutionSliderPosition = new Vector2(600, 80);
            
            // var restitutionLabel = new UiTextLabel("Restitution", debugFont)
            // {
            //     Position = restitutionLabelPosition,
            //     CharacterSize = 14
            // };
            // _uiElements.Add(restitutionLabel);
            
            // var restitutionSlider = new UiSlider(restitutionSliderPosition, new Vector2(150, 20), 0f, 1f, PhysicsObject.Restitution);
            // restitutionSlider.OnValueChanged += (value) =>
            // {
            //     PhysicsObject.Restitution = value;
            // };
            // _uiElements.Add(restitutionSlider);
            
            // Add Simulation Speed control
            var simSpeedLabelPosition = new Vector2(200, 110);
            var simSpeedSliderPosition = new Vector2(200, 130);
            
            var simSpeedLabel = new UiTextLabel("Simulation Speed", debugFont)
            {
                Position = simSpeedLabelPosition,
                CharacterSize = 14
            };
            _uiElements.Add(simSpeedLabel);
            
            var simSpeedSlider = new UiSlider(simSpeedSliderPosition, new Vector2(150, 20), 0.1f, 2f, _physicsSystem.TimeScale);
            simSpeedSlider.OnValueChanged += (value) =>
            {
                _physicsSystem.TimeScale = value;
            };
            _uiElements.Add(simSpeedSlider);
            
            // Add Show Velocities Toggle
            var showVelocitiesLabel = new UiTextLabel("Show Velocities", debugFont)
            {
                Position = new Vector2(400, 110),
                CharacterSize = 14
            };
            
            // Add Pause/Resume button
            var pauseButtonPosition = new Vector2(600, 130);
            var pauseButton = new UiButton("Pause", debugFont, pauseButtonPosition, new Vector2(80, 30));
            pauseButton.OnClick += (state) => 
            {
                _physicsSystem.IsPaused = !_physicsSystem.IsPaused;
                pauseButton.Text = _physicsSystem.IsPaused ? "Resume" : "Pause";
            };
            _uiElements.Add(pauseButton);
        }

        /// <summary>
        /// Render the game window view and UI elements view
        /// </summary>
        /// <param name="msPhysicsTime"></param>
        /// <param name="msDrawTime"></param>
        /// <param name="msFrameTime"></param>
        /// <param name="isCreatingBox"></param>
        /// <param name="boxStartPoint"></param>
        /// <param name="boxEndPoint"></param>
        public void Render(long msPhysicsTime, long msDrawTime, long msFrameTime,
                           bool isCreatingBox, Vector2 boxStartPoint, Vector2 boxEndPoint)
        {

            // Draw Game View
            DrawGameView(isCreatingBox, boxStartPoint, boxEndPoint);

            DrawUiView(msPhysicsTime, msDrawTime, msFrameTime);

            Window.Display();
        }

        private void DrawGameView(bool isCreatingBox, Vector2 boxStartPoint, Vector2 boxEndPoint)
        {
            // Switch to Game window view
            Window.SetView(GameView);

            // Clear with black color
            Window.Clear(Color.Black);

            // Draw preview rectangle when creating a box.
            if (isCreatingBox)
            {
                float minX = Math.Min(boxStartPoint.X, boxEndPoint.X);
                float minY = Math.Min(boxStartPoint.Y, boxEndPoint.Y);
                float width = Math.Abs(boxEndPoint.X - boxStartPoint.X);
                float height = Math.Abs(boxEndPoint.Y - boxStartPoint.Y);
                RectangleShape previewRect = new RectangleShape(new Vector2f(width, height))
                {
                    Position = new Vector2f(minX, minY),
                    FillColor = new Color(0, 0, 0, 0),
                    OutlineColor = Color.Red,
                    OutlineThickness = 2
                };
                Window.Draw(previewRect);
            }

            // Draw all static objects with their shaders.
            foreach (var obj in PhysicsSystem.ListStaticObjects)
            {
                var sfmlShader = obj.Shader;
                if (sfmlShader != null)
                {
                    sfmlShader.PreDraw(obj, Window);
                    sfmlShader.Draw(obj, Window);
                    sfmlShader.PostDraw(obj, Window);
                }
            }
        }

        private void DrawUiView(long msPhysicsTime, long msDrawTime, long msFrameTime)
        {
            // Switch to UI window view
            Window.SetView(UiView);

            // Update and draw debug information.
            debugText.DisplayedString =
                $"ms physics time: {msPhysicsTime}\n" +
                $"ms draw time: {msDrawTime}\n" +
                $"frame rate: {1000 / Math.Max(msFrameTime, 1)}\n" +
                $"num objects: {PhysicsSystem.ListStaticObjects.Count}";
            Window.Draw(debugText);

            // Draw all UI elements
            for (int i = 0; i < _uiElements.Count; i++)
            {
                _uiElements[i].Draw(Window);
            }
        }
    }
}
