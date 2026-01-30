#nullable enable
using System.Numerics;
using physics.Engine;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Rendering.UI;
using SFML.Graphics;
using SFML.Window;
using SharpPhysics.Demo.Settings;

namespace SharpPhysics.Demo;

/// <summary>
/// In-game settings menu for configuring camera, detection, and display options.
/// Uses simple text-based UI with keyboard navigation.
/// </summary>
public class SettingsGame : IGame
{
    private GameEngine _engine = null!;
    private GameSettings _settings = null!;
    private UiManager _uiManager = new();
    private readonly List<UiMenuButton> _menuButtons = new();

    // Settings state
    private int _selectedIndex = 0;
    private string _currentCategory = "Camera";
    private readonly string[] _categories = { "Camera", "Detection", "Display" };

    // Editable values (strings for display/editing)
    private readonly List<SettingItem> _currentSettings = new();
    private int _editingIndex = -1;
    private string _editBuffer = "";
    private float _keyRepeatTimer = 0f;
    private const float KEY_REPEAT_DELAY = 0.15f;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _settings = GameSettings.Instance;

        _engine.Renderer.ShowDebugUI = false;
        _engine.PhysicsSystem.Gravity = new Vector2(0, 0);
        _engine.PhysicsSystem.GravityScale = 0;

        // Subscribe to text input events
        _engine.Renderer.Window.TextEntered += OnTextEntered;

        CreateCategoryButtons();
        LoadCategorySettings();

        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("         SETTINGS");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Use Up/Down to navigate, Enter to edit, ESC to save and return.");
        Console.WriteLine("═══════════════════════════════════════");
    }

    private void CreateCategoryButtons()
    {
        var font = _engine.Renderer.DefaultFont;
        float centerX = _engine.WindowWidth / 2f;
        float tabY = 80f;
        float tabWidth = 180f;
        float tabSpacing = 10f;
        float tabStartX = centerX - ((_categories.Length * tabWidth + (_categories.Length - 1) * tabSpacing) / 2f);

        for (int i = 0; i < _categories.Length; i++)
        {
            var category = _categories[i];
            var isActive = category == _currentCategory;
            var button = new UiMenuButton(
                category,
                "",
                font,
                new Vector2(tabStartX + i * (tabWidth + tabSpacing), tabY),
                new Vector2(tabWidth, 40f),
                baseColor: isActive ? new Color(80, 80, 120) : new Color(50, 50, 70),
                hoverColor: new Color(100, 100, 140),
                borderColor: isActive ? new Color(150, 150, 255) : new Color(80, 80, 100)
            );
            var cat = category;
            button.OnClick += _ => SwitchCategory(cat);
            _menuButtons.Add(button);
            _uiManager.Add(button);
        }
    }

    private void SwitchCategory(string category)
    {
        _currentCategory = category;
        _selectedIndex = 0;
        _editingIndex = -1;

        // Clear and rebuild buttons
        foreach (var btn in _menuButtons)
        {
            _uiManager.Remove(btn);
        }
        _menuButtons.Clear();

        CreateCategoryButtons();
        LoadCategorySettings();
    }

    private void LoadCategorySettings()
    {
        _currentSettings.Clear();

        switch (_currentCategory)
        {
            case "Camera":
                _currentSettings.Add(new SettingItem("Enable Body Tracking", _settings.PoseTrackingEnabled.ToString(),
                    v => { if (bool.TryParse(v, out var i)) _settings.PoseTrackingEnabled = i; }, "true or false"));
                _currentSettings.Add(new SettingItem("Source Type", _settings.CameraSourceType, 
                    v => _settings.CameraSourceType = v, "url or device"));
                _currentSettings.Add(new SettingItem("Stream URL", _settings.CameraUrl, 
                    v => _settings.CameraUrl = v, "MJPEG stream URL"));
                _currentSettings.Add(new SettingItem("Device Index", _settings.CameraDeviceIndex.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.CameraDeviceIndex = i; }, "0, 1, 2..."));
                _currentSettings.Add(new SettingItem("Device Resolution X", _settings.CameraDeviceResolutionX.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.CameraDeviceResolutionX = i; }, "requested width (pixels)"));
                _currentSettings.Add(new SettingItem("Device Resolution Y", _settings.CameraDeviceResolutionY.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.CameraDeviceResolutionY = i; }, "requested height (pixels)"));
                _currentSettings.Add(new SettingItem("Device FPS", _settings.CameraDeviceFps.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.CameraDeviceFps = i; }, "local camera only"));
                break;

            case "Detection":
                _currentSettings.Add(new SettingItem("Model Path", _settings.ModelPath, 
                    v => _settings.ModelPath = v, "path to ONNX model"));
                _currentSettings.Add(new SettingItem("Use GPU", _settings.UseGpu.ToString(), 
                    v => _settings.UseGpu = v.ToLower() == "true", "true/false"));
                _currentSettings.Add(new SettingItem("Mirror Mode (FlipX)", _settings.FlipX.ToString(), 
                    v => _settings.FlipX = v.ToLower() == "true", "true/false"));
                _currentSettings.Add(new SettingItem("Max People", _settings.MaxPeople.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.MaxPeople = i; }, "1-10"));
                _currentSettings.Add(new SettingItem("Confidence", _settings.ConfidenceThreshold.ToString("F2"), 
                    v => { if (float.TryParse(v, out var f)) _settings.ConfidenceThreshold = Math.Clamp(f, 0.1f, 1f); }, "0.1-1.0"));
                _currentSettings.Add(new SettingItem("Sandbox Ball Radius", _settings.SandboxBallRadius.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.SandboxBallRadius = i; }, "pixels"));
                _currentSettings.Add(new SettingItem("BubblePop Ball Radius", _settings.BubblePopBallRadius.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.BubblePopBallRadius = i; }, "pixels"));
                _currentSettings.Add(new SettingItem("RainCatcher Ball Radius", _settings.RainCatcherBallRadius.ToString(), 
                    v => { if (int.TryParse(v, out var i)) _settings.RainCatcherBallRadius = i; }, "pixels"));
                break;

            case "Display":
                _currentSettings.Add(new SettingItem("Window Width", _settings.WindowWidth.ToString(), 
                    v => { if (uint.TryParse(v, out var i)) _settings.WindowWidth = i; }, "requires restart"));
                _currentSettings.Add(new SettingItem("Window Height", _settings.WindowHeight.ToString(), 
                    v => { if (uint.TryParse(v, out var i)) _settings.WindowHeight = i; }, "requires restart"));
                _currentSettings.Add(new SettingItem("FPS Limit", _settings.FramerateLimit.ToString(), 
                    v => { if (uint.TryParse(v, out var i)) _settings.FramerateLimit = i; }, "0=unlimited"));
                _currentSettings.Add(new SettingItem("Antialiasing", _settings.AntialiasingLevel.ToString(), 
                    v => { if (uint.TryParse(v, out var i)) _settings.AntialiasingLevel = i; }, "0,2,4,8"));
                break;
        }
    }

    private void OnTextEntered(object? sender, TextEventArgs e)
    {
        if (_editingIndex < 0) return;

        char c = (char)e.Unicode[0];

        // Handle backspace
        if (c == '\b')
        {
            if (_editBuffer.Length > 0)
                _editBuffer = _editBuffer[..^1];
            return;
        }

        // Handle enter (confirm edit)
        if (c == '\r' || c == '\n')
        {
            ConfirmEdit();
            // Consume Enter so it doesn't immediately re-enter edit mode
            _engine.InputManager.ConsumeKeyPress(Keyboard.Key.Enter);
            return;
        }

        // ESC is handled in Update() for proper edge detection
        // (skip ESC handling here to avoid double-processing)
        if (c == 27) // ESC
        {
            return;
        }

        // Add printable characters
        if (!char.IsControl(c))
        {
            _editBuffer += c;
        }
    }

    private void ConfirmEdit()
    {
        if (_editingIndex >= 0 && _editingIndex < _currentSettings.Count)
        {
            var setting = _currentSettings[_editingIndex];
            setting.OnChange(_editBuffer);
            setting.Value = _editBuffer;
        }
        _editingIndex = -1;
    }

    private void SaveAndReturn()
    {
        if (_editingIndex >= 0)
        {
            ConfirmEdit();
        }
        _settings.Save();

        // Unsubscribe from text events
        _engine.Renderer.Window.TextEntered -= OnTextEntered;

        _engine.SwitchGame(new MenuGame());
    }

    public void Update(float deltaTime, InputManager inputManager)
    {
        _keyRepeatTimer -= deltaTime;

        // ESC behavior: First press exits editing, second press exits settings
        // Using edge-detected EscapePressed from InputManager
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Escape))
        {
            inputManager.ConsumeKeyPress(Keyboard.Key.Escape);
            if (_editingIndex >= 0)
            {
                // Currently editing - exit editing mode
                _editingIndex = -1;
            }
            else
            {
                // Not editing - save and return to menu
                SaveAndReturn();
            }
            return;
        }

        // Handle UI clicks (category buttons)
        if (inputManager.IsMousePressed(Mouse.Button.Left))
        {
            if (_uiManager.HandleClick(inputManager.MousePosition))
            {
                // UI handled the click
                return;
            }
        }

        // Handle mouse click on settings rows
        if (inputManager.IsMousePressed(Mouse.Button.Left) && _editingIndex < 0)
        {
            float startY = 145f;
            float rowHeight = 38f;
            float labelX = 120f;

            for (int i = 0; i < _currentSettings.Count; i++)
            {
                float y = startY + i * rowHeight;
                // Check if click is within this row
                if (inputManager.MousePosition.X >= labelX && 
                    inputManager.MousePosition.X <= _engine.WindowWidth - 100f &&
                    inputManager.MousePosition.Y >= y && 
                    inputManager.MousePosition.Y <= y + rowHeight)
                {
                    _selectedIndex = i;
                    // Single click starts editing
                    _editingIndex = i;
                    _editBuffer = _currentSettings[i].Value;
                    break;
                }
            }
        }

        // Navigation when not editing
        if (_editingIndex < 0 && _keyRepeatTimer <= 0)
        {
            if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Up) && _selectedIndex > 0)
            {
                inputManager.ConsumeKeyPress(Keyboard.Key.Up);
                _selectedIndex--;
                _keyRepeatTimer = KEY_REPEAT_DELAY;
            }
            if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Down) && _selectedIndex < _currentSettings.Count - 1)
            {
                inputManager.ConsumeKeyPress(Keyboard.Key.Down);
                _selectedIndex++;
                _keyRepeatTimer = KEY_REPEAT_DELAY;
            }
            // Use edge-detected EnterPressed from InputManager
            if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Space) || inputManager.IsKeyPressedBuffered(Keyboard.Key.Enter))
            {
                inputManager.ConsumeKeyPress(Keyboard.Key.Space);
                inputManager.ConsumeKeyPress(Keyboard.Key.Enter);
                // Start editing
                _editingIndex = _selectedIndex;
                _editBuffer = _currentSettings[_selectedIndex].Value;
                _keyRepeatTimer = KEY_REPEAT_DELAY * 2;
            }
        }

        // Update button hover states using KeyState mouse position
        var mouseVec = inputManager.MousePosition;

        foreach (var button in _menuButtons)
        {
            button.SetHovered(button.ContainsPoint(mouseVec));
        }
    }

    public void Render(Renderer renderer)
    {
        // Title
        renderer.DrawText("SETTINGS", _engine.WindowWidth / 2f - 100f, 20f, 36, new Color(200, 200, 255));

        // Instructions
        string instructions = _editingIndex >= 0 
            ? "Type value, press ENTER to confirm, ESC to cancel"
            : "Up/Down Navigate  |  SPACE Edit  |  ESC Save & Return";
        renderer.DrawText(instructions, _engine.WindowWidth / 2f - 220f, _engine.WindowHeight - 35f, 14, new Color(150, 150, 180));

        // Settings list
        float startY = 145f;
        float rowHeight = 38f;
        float labelX = 120f;
        float valueX = 420f;
        float hintX = 750f;

        for (int i = 0; i < _currentSettings.Count; i++)
        {
            float y = startY + i * rowHeight;
            var setting = _currentSettings[i];
            bool isSelected = i == _selectedIndex;
            bool isEditing = i == _editingIndex;

            // Selection indicator
            if (isSelected)
            {
                renderer.DrawText(">", labelX - 25f, y, 18, new Color(100, 200, 255));
            }

            // Label
            var labelColor = isSelected ? new Color(255, 255, 255) : new Color(180, 180, 180);
            renderer.DrawText(setting.Name + ":", labelX, y, 16, labelColor);

            // Value (with edit indicator)
            string displayValue = isEditing ? _editBuffer + "_" : setting.Value;
            var valueColor = isEditing 
                ? new Color(255, 255, 100) 
                : (isSelected ? new Color(100, 200, 255) : new Color(150, 200, 150));
            renderer.DrawText($"[{displayValue}]", valueX, y, 16, valueColor);

            // Hint
            renderer.DrawText(setting.Hint, hintX, y, 12, new Color(100, 100, 120));
        }

            // Category-specific hints
            float hintY = startY + _currentSettings.Count * rowHeight + 35f;
            switch (_currentCategory)
            {
                case "Camera":
                    renderer.DrawText("Common URL formats:", labelX, hintY, 14, new Color(180, 180, 200));
                    renderer.DrawText("- MJPEG Stream: http://<ip>:8080", labelX + 20f, hintY + 22f, 12, new Color(140, 140, 160));
                    renderer.DrawText("- ESP32-CAM: http://<ip>:81/stream", labelX + 20f, hintY + 40f, 12, new Color(140, 140, 160));
                    break;
                case "Detection":
                    renderer.DrawText("Larger ball radius = easier tracking, smaller = more precise", labelX, hintY, 14, new Color(180, 180, 200));
                    break;
                        case "Display":
                                renderer.DrawText("WARNING: Display changes require application restart", labelX, hintY, 14, new Color(255, 200, 100));
                                break;
                        }

                    // Draw UI elements (category buttons)
                    renderer.Window.SetView(renderer.UiView);
                    _uiManager.Draw(renderer.Window);
                    }

                public void Shutdown()
                {
                    // Clear UI
                    _uiManager.Clear();

                    // Ensure text event is unsubscribed
                    try
                    {
                        _engine.Renderer.Window.TextEntered -= OnTextEntered;
                    }
                    catch { }
                }

    private class SettingItem
    {
        public string Name { get; }
        public string Value { get; set; }
        public Action<string> OnChange { get; }
        public string Hint { get; }

        public SettingItem(string name, string value, Action<string> onChange, string hint = "")
        {
            Name = name;
            Value = value;
            OnChange = onChange;
            Hint = hint;
        }
    }
}
