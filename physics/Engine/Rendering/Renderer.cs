using SFML.Graphics;
using SFML.System;
using System;
using physics.Engine.Classes;
using System.Collections.Generic;
using physics.Engine.Rendering.UI;
using SFML.Window;

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

        public Renderer(uint windowWidth, uint windowHeight, string windowTitle)
        {
            ContextSettings settings = new ContextSettings();
            settings.AntialiasingLevel = 8; 
            Window = new RenderWindow(new VideoMode(windowWidth, windowHeight), windowTitle, Styles.Close, settings);
            Window.Closed += (s, e) => Window.Close();

            // Create and set a view covering the whole window.
            GameView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
            UiView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
            Window.SetFramerateLimit(144);

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

            UiElement roundedRect = new UiRoundedRectangle(new Vector2f(140, 80), 5, 32);
            roundedRect.Position = new Vector2f(30, 30);
            _uiElements.Add(roundedRect);
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
                           bool isCreatingBox, Vector2f boxStartPoint, Vector2f boxEndPoint)
        {

            // Draw Game View
            DrawGameView(isCreatingBox, boxStartPoint, boxEndPoint);

            DrawUiView(msPhysicsTime, msDrawTime, msFrameTime);

            Window.Display();
        }

        private void DrawGameView(bool isCreatingBox, Vector2f boxStartPoint, Vector2f boxEndPoint)
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
