using SFML.Graphics;
using SFML.System;
using System;
using physics.Engine.Classes;
using System.Collections.Generic;
using physics.Engine.Rendering.UI;

namespace physics.Engine.Rendering
{
    public class Renderer
    {
        private RenderWindow window;
        private View view;
        private PhysicsSystem physicsSystem;
        private Text debugText;
        private Font debugFont;
        private List<aUiElement> _uiElements = new List<aUiElement>();

        public Renderer(RenderWindow window, View view, PhysicsSystem physicsSystem)
        {
            this.window = window;
            this.view = view;
            this.physicsSystem = physicsSystem;

            // Initialize debug font and text.
            debugFont = new Font(@"C:\Windows\Fonts\arial.ttf"); // Ensure this is a valid font path.
            debugText = new Text("", debugFont, 12)
            {
                FillColor = Color.White,
                Position = new Vector2f(40, 40)
            };

            aUiElement roundedRect = new UiRoundedRectangle(new Vector2f(600, 200), 20, 32);
            _uiElements.Add(roundedRect);
        }

        public void Render(long msPhysicsTime, long msDrawTime, long msFrameTime,
                           bool isCreatingBox, Vector2f boxStartPoint, Vector2f boxEndPoint)
        {
            window.SetView(view);
            window.Clear(Color.Black);

            // Update and draw debug information.
            debugText.DisplayedString =
                $"ms physics time: {msPhysicsTime}\n" +
                $"ms draw time: {msDrawTime}\n" +
                $"frame rate: {1000 / Math.Max(msFrameTime, 1)}\n" +
                $"num objects: {PhysicsSystem.ListStaticObjects.Count}";
            window.Draw(debugText);

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
                window.Draw(previewRect);
            }

            // Draw all static objects with their shaders.
            foreach (var obj in PhysicsSystem.ListStaticObjects)
            {
                var sfmlShader = obj.Shader;
                if (sfmlShader != null)
                {
                    sfmlShader.PreDraw(obj, window);
                    sfmlShader.Draw(obj, window);
                    sfmlShader.PostDraw(obj, window);
                }
            }

            // Draw all UI elements
            for (int i = 0; i < _uiElements.Count; i++)
            {
                _uiElements[i].Draw(window);
            }

            window.Display();
        }
    }
}
