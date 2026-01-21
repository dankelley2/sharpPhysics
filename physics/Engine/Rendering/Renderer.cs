using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using physics.Engine.Rendering.UI;
using SFML.Window;
using System.Numerics;
using physics.Engine.Shaders;
using physics.Engine.Objects;
using physics.Engine.Integration;

namespace physics.Engine.Rendering
{
    public class Renderer
    {
        public RenderWindow Window { get; private set; }
        public View GameView { get; private set; }
        public View UiView { get; private set; }

        private Text debugText;
        private Font debugFont;
        private List<UiElement> _debugUiElements = new List<UiElement>();
        private PhysicsSystem _physicsSystem;

        // Reference to person collider bridge for skeleton rendering
        private PersonColliderBridge? _personColliderBridge;

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
            var textObj = new Text(text, debugFont, size)
            {
                Position = new Vector2f(x, y),
                FillColor = color ?? Color.White
            };
            Window.Draw(textObj);
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

            // Note: Debug UI elements are created but tracked separately.
            // They add themselves to GlobalUiElements via base constructor,
            // but we also track them in _debugUiElements so we can control visibility.

            UiElement roundedRect = new UiRoundedRectangle(new Vector2(140, 80), 5, 32)
            {
                OutlineColor = Color.Red
            };
            roundedRect.Position = new Vector2(30, 30);
            _debugUiElements.Add(roundedRect);

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
            _debugUiElements.Add(viewingNormalsLabel);
            _debugUiElements.Add(viewingNormalsCheckbox);

            // Add Gravity X control
            var gravityXLabelPosition = new Vector2(200, 60);
            var gravityXSliderPosition = new Vector2(200, 80);

            var gravityXLabel = new UiTextLabel("Gravity X", debugFont)
            {
                Position = gravityXLabelPosition,
                CharacterSize = 14
            };
            _debugUiElements.Add(gravityXLabel);

            var gravityXSlider = new UiSlider(gravityXSliderPosition, new Vector2(150, 20), -20f, 20f, _physicsSystem.Gravity.X);
            gravityXSlider.OnValueChanged += (value) =>
            {
                var currentGravity = _physicsSystem.Gravity;
                _physicsSystem.Gravity = new Vector2(value, currentGravity.Y);
            };
            _debugUiElements.Add(gravityXSlider);

            // Add Gravity Y control
            var gravityYLabelPosition = new Vector2(400, 60);
            var gravityYSliderPosition = new Vector2(400, 80);

            var gravityYLabel = new UiTextLabel("Gravity Y", debugFont)
            {
                Position = gravityYLabelPosition,
                CharacterSize = 14
            };
            _debugUiElements.Add(gravityYLabel);

            var gravityYSlider = new UiSlider(gravityYSliderPosition, new Vector2(150, 20), -20f, 20f, _physicsSystem.Gravity.Y);
            gravityYSlider.OnValueChanged += (value) =>
            {
                var currentGravity = _physicsSystem.Gravity;
                _physicsSystem.Gravity = new Vector2(currentGravity.X, value);
            };
            _debugUiElements.Add(gravityYSlider);

            // Add Friction slider and label (200px to the right of the checkbox)
            var frictionLabelPosition = new Vector2(400, 110); // Position above the slider
            var frictionSliderPosition = new Vector2(400, 130); // Same Y as checkbox

            // Create and add friction label
            var frictionLabel = new UiTextLabel("Friction", debugFont)
            {
                Position = frictionLabelPosition,
                CharacterSize = 14
            };
            _debugUiElements.Add(frictionLabel);

            // Create and add friction slider
            var frictionSlider = new UiSlider(frictionSliderPosition, new Vector2(150, 20), 0f, 1f, PhysicsObject.Friction);
            frictionSlider.OnValueChanged += (value) =>
            {
                PhysicsObject.Friction = value;
            };
            _debugUiElements.Add(frictionSlider);

            // Add Simulation Speed control
            var simSpeedLabelPosition = new Vector2(200, 110);
            var simSpeedSliderPosition = new Vector2(200, 130);

            var simSpeedLabel = new UiTextLabel("Simulation Speed", debugFont)
            {
                Position = simSpeedLabelPosition,
                CharacterSize = 14
            };
            _debugUiElements.Add(simSpeedLabel);

            var simSpeedSlider = new UiSlider(simSpeedSliderPosition, new Vector2(150, 20), 0.1f, 2f, _physicsSystem.TimeScale);
            simSpeedSlider.OnValueChanged += (value) =>
            {
                _physicsSystem.TimeScale = value;
            };
            _debugUiElements.Add(simSpeedSlider);

            // Add Pause/Resume button
            var pauseButtonPosition = new Vector2(450, 30);
            var pauseButton = new UiButton("Pause", debugFont, pauseButtonPosition, new Vector2(70, 20));
            pauseButton.OnClick += (state) => 
            {
                _physicsSystem.IsPaused = !_physicsSystem.IsPaused;
                pauseButton.Text = _physicsSystem.IsPaused ? "Resume" : "Pause";
            };
            _debugUiElements.Add(pauseButton);
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

                // Draw skeleton overlay if available
                DrawSkeletonOverlay();
            }

            /// <summary>
            /// Sets the person collider bridge for skeleton rendering.
            /// </summary>
            public void SetPersonColliderBridge(PersonColliderBridge? bridge)
            {
                _personColliderBridge = bridge;
            }

            /// <summary>
            /// Draws the skeleton overlay showing detected keypoints and connections.
            /// </summary>
            private void DrawSkeletonOverlay()
            {
                if (_personColliderBridge == null) return;

                // Try to get full skeleton first
                var fullSkeleton = _personColliderBridge.GetFullSkeleton();
                if (fullSkeleton != null)
                {
                    DrawFullSkeleton(fullSkeleton.Value.Keypoints, fullSkeleton.Value.Confidences, fullSkeleton.Value.Connections);
                    return;
                }

                // Fallback to simple keypoints if full skeleton not available
                var keypoints = _personColliderBridge.GetLatestKeypoints();
                if (keypoints == null) return;

                var (head, leftHand, rightHand, headConf, leftConf, rightConf) = keypoints.Value;

                // Draw connections (skeleton lines)
                var lineColor = new Color(0, 255, 255, 180); // Cyan with transparency

                // Head to left hand
                if (headConf > 0.5f && leftConf > 0.5f)
                {
                    DrawLine(head, leftHand, lineColor, 2);
                }

                // Head to right hand
                if (headConf > 0.5f && rightConf > 0.5f)
                {
                    DrawLine(head, rightHand, lineColor, 2);
                }

                // Draw keypoint markers
                var circleRadius = 8f;

                // Head (green)
                if (headConf > 0.5f)
                {
                    DrawCircleMarker(head, circleRadius, new Color(0, 255, 0, 200));
                }

                // Left hand (blue)
                if (leftConf > 0.5f)
                {
                    DrawCircleMarker(leftHand, circleRadius, new Color(0, 100, 255, 200));
                }

                // Right hand (red)
                if (rightConf > 0.5f)
                {
                    DrawCircleMarker(rightHand, circleRadius, new Color(255, 100, 0, 200));
                }
            }

            /// <summary>
            /// Draws the full 17-keypoint COCO skeleton.
            /// </summary>
            private void DrawFullSkeleton(Vector2[] keypoints, float[] confidences, (int, int)[] connections)
            {
                const float confidenceThreshold = 0.3f;

                // Define colors for different body parts
                var faceColor = new Color(255, 255, 0, 200);      // Yellow for face
                var torsoColor = new Color(0, 255, 255, 200);     // Cyan for torso
                var leftArmColor = new Color(0, 255, 0, 200);     // Green for left arm
                var rightArmColor = new Color(255, 0, 0, 200);    // Red for right arm
                var leftLegColor = new Color(0, 200, 100, 200);   // Teal for left leg
                var rightLegColor = new Color(200, 100, 0, 200);  // Orange for right leg

                // Draw skeleton connections
                foreach (var (idx1, idx2) in connections)
                {
                    if (idx1 >= keypoints.Length || idx2 >= keypoints.Length)
                        continue;

                    if (confidences[idx1] < confidenceThreshold || confidences[idx2] < confidenceThreshold)
                        continue;

                    var pt1 = keypoints[idx1];
                    var pt2 = keypoints[idx2];

                    // Choose color based on body part
                    Color lineColor = GetConnectionColor(idx1, idx2, torsoColor, leftArmColor, rightArmColor, leftLegColor, rightLegColor, faceColor);

                    DrawLine(pt1, pt2, lineColor, 3);
                }

                // Draw keypoint circles
                for (int i = 0; i < keypoints.Length; i++)
                {
                    if (confidences[i] < confidenceThreshold)
                        continue;

                    var pt = keypoints[i];
                    Color circleColor = GetKeypointColor(i);
                    float radius = GetKeypointRadius(i);

                    DrawCircleMarker(pt, radius, circleColor);
                }
            }

            /// <summary>
            /// Gets the color for a skeleton connection based on body part.
            /// </summary>
            private Color GetConnectionColor(int idx1, int idx2, Color torso, Color leftArm, Color rightArm, Color leftLeg, Color rightLeg, Color face)
            {
                // Face connections (0-4)
                if (idx1 <= 4 && idx2 <= 4) return face;

                // Left arm (5, 7, 9)
                if ((idx1 == 5 && idx2 == 7) || (idx1 == 7 && idx2 == 9)) return leftArm;

                // Right arm (6, 8, 10)
                if ((idx1 == 6 && idx2 == 8) || (idx1 == 8 && idx2 == 10)) return rightArm;

                // Left leg (11, 13, 15)
                if ((idx1 == 11 && idx2 == 13) || (idx1 == 13 && idx2 == 15)) return leftLeg;

                // Right leg (12, 14, 16)
                if ((idx1 == 12 && idx2 == 14) || (idx1 == 14 && idx2 == 16)) return rightLeg;

                // Torso (shoulders, hips, shoulder-hip connections)
                return torso;
            }

            /// <summary>
            /// Gets the color for a keypoint based on its index.
            /// </summary>
            private Color GetKeypointColor(int idx)
            {
                return idx switch
                {
                    0 => new Color(255, 255, 0, 255),    // Nose - yellow
                    1 or 2 => new Color(255, 200, 0, 255), // Eyes - orange-yellow
                    3 or 4 => new Color(255, 150, 0, 255), // Ears - orange
                    5 or 7 or 9 => new Color(0, 255, 0, 255),  // Left arm - green
                    6 or 8 or 10 => new Color(255, 0, 0, 255), // Right arm - red
                    11 or 13 or 15 => new Color(0, 200, 100, 255), // Left leg - teal
                    12 or 14 or 16 => new Color(200, 100, 0, 255), // Right leg - orange
                    _ => new Color(255, 255, 255, 255)    // Default - white
                };
            }

            /// <summary>
            /// Gets the radius for a keypoint based on its type.
            /// </summary>
            private float GetKeypointRadius(int idx)
            {
                return idx switch
                {
                    0 => 8f,      // Nose - larger
                    1 or 2 => 5f, // Eyes - smaller
                    3 or 4 => 5f, // Ears - smaller
                    5 or 6 => 7f, // Shoulders - medium
                    7 or 8 => 6f, // Elbows - medium
                    9 or 10 => 8f, // Wrists - larger (hands)
                    11 or 12 => 7f, // Hips - medium
                    13 or 14 => 6f, // Knees - medium
                    15 or 16 => 6f, // Ankles - medium
                    _ => 5f
                };
            }

            /// <summary>
            /// Draws a line between two points.
            /// </summary>
            private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
            {
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
            /// Draws a circle marker at the specified position.
            /// </summary>
            private void DrawCircleMarker(Vector2 position, float radius, Color color)
            {
                var circle = new CircleShape(radius)
                {
                    Position = new Vector2f(position.X - radius, position.Y - radius),
                    FillColor = color,
                    OutlineColor = Color.White,
                                        OutlineThickness = 2
                                    };

                                    Window.Draw(circle);
                                }

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
                                    foreach (var element in _debugUiElements)
                                    {
                                        element.Draw(Window);
                                    }
                                }

                                // Draw all global UI elements (game-created elements)
                                // Skip debug elements when drawing from global list
                                foreach (var element in UiElement.GlobalUiElements)
                                {
                                    if (!_debugUiElements.Contains(element))
                                    {
                                        element.Draw(Window);
                                    }
                                }
                            }
                        }
                    }
