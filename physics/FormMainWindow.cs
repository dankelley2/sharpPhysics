﻿using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using physics.Engine;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Helpers;
using physics.Engine.Structs;


namespace physics
{
    public partial class FormMainWindow : Form
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly FastLoop _fastLoop;

        private PhysicsSystem _physicsSystem = new PhysicsSystem();
        private bool MOUSE_PRESSED_LEFT = false;
        private bool MOUSE_PRESSED_RIGHT = false;
        private PointF StartPointF { get; set; }
        private PointF EndPointF { get; set; }

        private bool _grabbing;
        private PointF _mousePos;

        //private FormState formState = new FormState();
        private Graphics gfxBuffer;
        private Bitmap bmpBuffer;

        private long _msFrameTime;
        private long _msLastFrame;
        private long _msPerDrawCycle;
        private long _msThisFrame;
        private long _msPhysicsTime;
        private int _radius = 10;

        Font debugFont = new Font(FontFamily.GenericMonospace, 10);
        SolidBrush debugBrush = new SolidBrush(Color.WhiteSmoke);
        private long _frameTime;

        public FormMainWindow()
        {
            InitializeComponent();
            DoubleBuffered = true;

            bmpBuffer = new Bitmap(Size.Width, Size.Height);
            gfxBuffer = Graphics.FromImage(bmpBuffer);

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            gfxBuffer.CompositingMode = CompositingMode.SourceOver;
            gfxBuffer.CompositingQuality = CompositingQuality.HighSpeed;
            gfxBuffer.InterpolationMode = InterpolationMode.NearestNeighbor;
            gfxBuffer.PixelOffsetMode = PixelOffsetMode.Half;
            gfxBuffer.SmoothingMode = SmoothingMode.HighQuality;

            _fastLoop = new FastLoop(GameLoop);
            _stopwatch.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Render();
            e.Graphics.DrawImage(bmpBuffer, new Point(0, 0));
        }

        private void FormMainWindow_Load(object sender, EventArgs e)
        {
            ObjectTemplates.CreateWall(0, 0, 20, this.Height);
            ObjectTemplates.CreateWall(this.Width - 20, 0, this.Width, this.Height);
            ObjectTemplates.CreateWall(0, 0, this.Width, 20);
            ObjectTemplates.CreateWall(0, this.Height - 20, this.Width, this.Height);

            for (int i = 0; i < 400; i += 20)
            {
                for (int j = 0; j < 200; j += 20)
                {
                    ObjectTemplates.CreateMedBall(i + 200, j + 150);
                }
            }

            ObjectTemplates.CreateAttractor(400, 450);
        }

        private void Render()
        {
            gfxBuffer.Clear(Color.Black);

            gfxBuffer.DrawString("ms physics time: " + _msPhysicsTime, debugFont,
                debugBrush, new PointF(80, 70));
            gfxBuffer.DrawString("ms total draw time: " + _msPerDrawCycle, debugFont, debugBrush,
                new PointF(80, 90));
            gfxBuffer.DrawString("frame rate: " + 1000 / Math.Max(_msFrameTime, 1), debugFont,
                debugBrush, new PointF(80, 110));
            gfxBuffer.DrawString("num objects: " + PhysicsSystem.ListStaticObjects.Count, debugFont,
                debugBrush, new PointF(80, 130));
            if (_grabbing)
            {
                gfxBuffer.DrawLine(new Pen(Color.DarkGreen), _mousePos, _physicsSystem.GetActiveObjectCenter());
            }

            if (MOUSE_PRESSED_LEFT)
            {
                var penArrow = new Pen(Color.Green, 2);
                penArrow.EndCap = LineCap.ArrowAnchor;
                penArrow.StartCap = LineCap.Round;
                gfxBuffer.DrawLine(penArrow, _mousePos, StartPointF);
                gfxBuffer.DrawEllipse(new Pen(Color.DarkBlue), (int)(StartPointF.X - _radius), (int)(StartPointF.Y - _radius), _radius * 2, _radius * 2);
            }

            foreach (var o in PhysicsSystem.ListStaticObjects)
            {
                o.Shader.PreDraw(o, gfxBuffer);
            }
            foreach (var o in PhysicsSystem.ListStaticObjects)
            {
                o.Shader.Draw(o, gfxBuffer);
            }
            foreach (var o in PhysicsSystem.ListStaticObjects)
            {
                o.Shader.PostDraw(o, gfxBuffer);
            }

        }


        #region EngineBindings
        private void DragObject(PointF location)
        {
            _physicsSystem.HoldActiveAtPoint(new Vec2 { X = location.X, Y = location.Y });
        }

        private void stopDragObject()
        {
            _physicsSystem.SetVelocityOfActive(new Vec2 { X = 0F, Y = 0F });
            _physicsSystem.ReleaseActiveObject();
            _grabbing = false;
        }
        #endregion


        #region MouseEvents
        private void this_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {

                if (_physicsSystem.ActivateAtPoint(e.Location))
                {
                    _grabbing = true;
                    return;
                }

                StartPointF = e.Location;
                MOUSE_PRESSED_LEFT = true;
            }

            if (e.Button == MouseButtons.Right)
            {
                if (_physicsSystem.ActivateAtPoint(e.Location))
                {
                    _physicsSystem.RemoveActiveObject();
                }

                MOUSE_PRESSED_RIGHT = true;
            }
        }

        private void this_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_grabbing)
                {
                    stopDragObject();
                    return;
                }

                if (MOUSE_PRESSED_LEFT)
                {
                    EndPointF = e.Location;

                    ActionTemplates.launch(
                        _physicsSystem,
                        ObjectTemplates.CreateSmallBall(StartPointF.X, StartPointF.Y),
                        StartPointF,
                        EndPointF);

                    MOUSE_PRESSED_LEFT = false;
                }
            }

            if (e.Button == MouseButtons.Right)
            {
                MOUSE_PRESSED_RIGHT = false;
            }
        }

        private void this_MouseMove(object sender, MouseEventArgs e)
        {
            _mousePos = e.Location;

            if (MOUSE_PRESSED_RIGHT)
            {
                if (_physicsSystem.ActivateAtPoint(e.Location))
                {
                    _physicsSystem.RemoveActiveObject();
                }
            }
        }
        #endregion


        #region KeyboardEvents
        private void FormMainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                _physicsSystem.FreezeStaticObjects();
            }

            //Change to info Shader
            if (e.KeyCode == Keys.I)
            {
                ActionTemplates.changeShader(_physicsSystem, new ShaderInfo());
            }

            //Change to PoolBall
            if (e.KeyCode == Keys.P)
            {
                ActionTemplates.changeShader(_physicsSystem, new ShaderBall());
            }

            //Change to Water
            if (e.KeyCode == Keys.W)
            {
                ActionTemplates.changeShader(_physicsSystem, new ShaderWater());
            }

            //Change to Velocity Shader
            if (e.KeyCode == Keys.V)
            {
                ActionTemplates.changeShader(_physicsSystem, new ShaderBallVelocity());
            }

            //Change to Velocity Shader
            if (e.KeyCode == Keys.F)
            {
                ActionTemplates.changeShader(_physicsSystem, new ShaderBallFast());
            }

            //Create Gravity Ball
            if (e.KeyCode == Keys.G)
            {
                ObjectTemplates.CreateAttractor(_mousePos.X, _mousePos.Y);
            }

            //Pop
            if (e.KeyCode == Keys.OemSemicolon)
            {
                ActionTemplates.PopAndMultiply(_physicsSystem);
            }

            //Pop
            if (e.KeyCode == Keys.Escape)
            {
                Exit();
            }
        }

        private void Exit()
        {
            this.Close();
            this.Dispose();
        }
        #endregion



        private void GameLoop(double elapsedTime)
        {
            _msPhysicsTime = _stopwatch.ElapsedMilliseconds;
            RunEngine(elapsedTime);
            _msPhysicsTime = _stopwatch.ElapsedMilliseconds - _msPhysicsTime;

            if (_stopwatch.ElapsedMilliseconds - _frameTime > 1000 / 60)
            {
                StartTimedRender();
            }
        }

        private void StartTimedRender()
        {
            _frameTime = _stopwatch.ElapsedMilliseconds;

            this.Refresh();

            _msPerDrawCycle = _stopwatch.ElapsedMilliseconds - _frameTime;
            _msLastFrame = _msThisFrame;
            _msThisFrame = _stopwatch.ElapsedMilliseconds;
            _msFrameTime = _msThisFrame - _msLastFrame;
        }

        private void RunEngine(double elapsedTime)
        {
            if (_grabbing)
            {
                DragObject(_mousePos);
            }
            _physicsSystem.Tick(elapsedTime);
        }
    }
}