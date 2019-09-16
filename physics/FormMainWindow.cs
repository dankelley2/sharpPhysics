using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using physics.Engine;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Structs;


namespace physics
{
    public partial class FormMainWindow : Form
    {
        private readonly Timer _physicsTimer = new Timer();
        private readonly Timer _refreshTimer = new Timer();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private PhysicsSystem _physicsSystem = new PhysicsSystem();
        private bool _drawLeftMouse = false;
        private bool _drawRightMouse = false;
        private PointF StartPointF { get; set; }
        private PointF EndPointF { get; set; }

        private bool _grabbing;
        private PointF _mousePos;


        private PointF _mouseThen;
        private long _msFrameTime;
        private long _msLastFrame;
        private long _msPerDrawCycle;
        private long _msPhysics;
        private long _msThisFrame;
        private int _radius = 10;
        private Bitmap _poolBall = new Bitmap(physics.Properties.Resources.blackPoolBall_30px);
        private Bitmap _poolTable = new Bitmap(physics.Properties.Resources.poolTable);

        Font debugFont = new Font(FontFamily.GenericMonospace, 10);
        SolidBrush debugBrush = new SolidBrush(Color.WhiteSmoke);

        public FormMainWindow()
        {
            InitializeComponent();
            _poolBall.MakeTransparent();
            _refreshTimer.Enabled = true;
            _refreshTimer.Interval = 1000 / 60;
            _refreshTimer.Tick += RefreshTimer_Tick;

            _physicsTimer.Enabled = true;
            _physicsTimer.Interval = 1000 / 100;
            _physicsTimer.Tick += PhysicsTimer_Tick;
            _stopwatch.Start();
        }

        private void FormMainWindow_Load(object sender, EventArgs e)
        {

            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(65, 0), new PointF(0, GameCanvas.Height), 1000000, true);
            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(GameCanvas.Width, 0), new PointF(GameCanvas.Width - 65, GameCanvas.Height), 1000000, true);
            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(0, 0), new PointF(GameCanvas.Width, 65), 1000000, true);
            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(0, GameCanvas.Height), new PointF(GameCanvas.Width, GameCanvas.Height - 65), 1000000, true);

            //CreatePhysicsObject(PhysicsObject.Type.Circle, new PointF(300, 100), 40, 200, false);
            for (int i = 0; i < 300; i += 30)
            {
                for (int j = 0; j < 200; j += 30)
                {
                    CreatePhysicsObject(PhysicsObject.Type.Circle, new PointF(i + 100, j + 150), 20, 500, false);
                }
            }

        }

        private void PhysicsTimer_Tick(object sender, EventArgs e)
        {
            var msElapsed = _stopwatch.ElapsedMilliseconds;

            if (_grabbing)
            {
                DragObject(_mousePos);
            }

        _physicsSystem.Tick(_stopwatch.ElapsedMilliseconds);
            _msPhysics = _stopwatch.ElapsedMilliseconds - msElapsed;
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            var msElapsed = _stopwatch.ElapsedMilliseconds;
            InvalidateWindow();
            _msPerDrawCycle = _stopwatch.ElapsedMilliseconds - msElapsed;
            _msLastFrame = _msThisFrame;
            _msThisFrame = _stopwatch.ElapsedMilliseconds;
            _msFrameTime = _msThisFrame - _msLastFrame;
        }

        private void InvalidateWindow()
        {
            GameCanvas.Invalidate();
        }

        private void GameCanvas_DrawGame(object sender, PaintEventArgs e)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            e.Graphics.CompositingMode = CompositingMode.SourceOver;
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            e.Graphics.DrawString("ms per physics cycle: " + _msPhysics, debugFont, debugBrush,
                new PointF(80, 70));
            e.Graphics.DrawString("ms total draw time: " + _msPerDrawCycle, debugFont, debugBrush,
                new PointF(80, 90));
            e.Graphics.DrawString("frame rate: " + 1000 / Math.Max(_msFrameTime, 1), debugFont,
                debugBrush, new PointF(80, 110));
            e.Graphics.DrawString("num objects: " + _physicsSystem.StaticObjects.Count, debugFont,
                debugBrush, new PointF(80, 130));
            if (_grabbing)
            {
                e.Graphics.DrawLine(new Pen(Color.DarkGreen), _mousePos, _physicsSystem.getActiveObjectCenter());
            }

            if (_drawLeftMouse)
            {
                var penArrow = new Pen(Color.Green,2);
                penArrow.EndCap = LineCap.ArrowAnchor;
                penArrow.StartCap = LineCap.Round;
                e.Graphics.DrawLine(penArrow, _mousePos, StartPointF);
                e.Graphics.DrawEllipse(new Pen(Color.DarkBlue), (int)(StartPointF.X-_radius), (int)(StartPointF.Y-_radius), _radius*2, _radius*2);
            }

            var penVelocity = new Pen(Color.Red, 1);
            penVelocity.EndCap = LineCap.ArrowAnchor;

            foreach (var o in _physicsSystem.StaticObjects.Where(x => x.ShapeType == PhysicsObject.Type.Circle))
            {
                var origin = o.Aabb.Min;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(100,50,50,50)), origin.X+3, origin.Y+5, o.Width-2, o.Height-2);
            }

            foreach (var o in _physicsSystem.StaticObjects.Where(a => a.Locked == false))
            {
                var origin = o.Aabb.Min;
                switch (o.ShapeType)
                {

                    case PhysicsObject.Type.Box:
                        e.Graphics.DrawRectangle(new Pen(Color.Maroon), origin.X, origin.Y, o.Width, o.Height);
                        e.Graphics.DrawLine(new Pen(Color.Maroon), origin.X, origin.Y, origin.X + o.Width, origin.Y + o.Height);
                        e.Graphics.DrawLine(new Pen(Color.Maroon), origin.X + o.Width, origin.Y, origin.X, origin.Y + o.Height);
                        break;
                    case PhysicsObject.Type.Circle:
                        e.Graphics.FillEllipse(new SolidBrush(Color.LightGray), origin.X, origin.Y, o.Width, o.Height);
                        e.Graphics.FillEllipse(new SolidBrush(Color.White), origin.X + o.Width * .1F, origin.Y + o.Height * .1F, o.Width*.6F, o.Height*.6F);
                        //e.Graphics.DrawImage(poolBall, new PointF(o.Pos.X, o.Pos.Y));
                        //e.Graphics.DrawLine(penVelocity, o.Center.X, o.Center.Y, o.Center.X + o.Velocity.X * 10, o.Center.Y + o.Velocity.Y * 10);
                        break;
                }
            }
            
        }


        #region EngineBindings
        private void DragObject(PointF location)
        {
            _physicsSystem.MoveActiveTowardsPoint(new Vec2 {X = location.X, Y = location.Y});
        }

        private void stopDragObject()
        {
            _physicsSystem.ReleaseActiveObject();
            _grabbing = false;
        }

        private void CreatePhysicsObject(PhysicsObject.Type type, PointF loc, int size, int mass, bool locked)
        {
            var aabb = new AABB
            {
                Min = new Vec2 { X = loc.X - size, Y = loc.Y - size },
                Max = new Vec2 { X = loc.X + size, Y = loc.Y + size }
            };
            PhysMath.CorrectBoundingBox(ref aabb);
            var obj = new PhysicsObject(aabb, type, .94F, mass, locked);
            _physicsSystem.StaticObjects.Add(obj);
        }

        private void CreatePhysicsObject(PhysicsObject.Type type, PointF start, PointF end, int mass, bool locked)
        {
            var aabb = new AABB
            {
                Min = new Vec2 { X = start.X, Y = start.Y },
                Max = new Vec2 { X = end.X, Y = end.Y }
            };
            PhysMath.CorrectBoundingBox(ref aabb);
            var obj = new PhysicsObject(aabb, type, .9F, 5000, locked);
            _physicsSystem.StaticObjects.Add(obj);
        }
        #endregion


        #region MouseEvents
        private void GameCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_physicsSystem.ActivateAtPoint(e.Location))
                {
                    _grabbing = true;
                    return;
                }

                StartPointF = e.Location;
                _drawLeftMouse = true;
            }

            if (e.Button == MouseButtons.Right)
            {
                if (_physicsSystem.ActivateAtPoint(e.Location))
                {
                    _physicsSystem.removeActiveObject();
                    return;
                }

                StartPointF = e.Location;
                _drawRightMouse = true;
            }
        }

        private void GameCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_grabbing)
                {
                    stopDragObject();
                    return;
                }

                if (_drawLeftMouse)
                {
                    EndPointF = e.Location;
                    CreatePhysicsObject(PhysicsObject.Type.Circle, StartPointF, _radius, 3000, false);
                    _physicsSystem.ActivateAtPoint(StartPointF);
                    Vec2 delta = (new Vec2 { X = EndPointF.X, Y = EndPointF.Y } -
                                 new Vec2 { X = StartPointF.X, Y = StartPointF.Y }) / 10;
                    _physicsSystem.AddVelocityToActive(-delta);
                    _drawLeftMouse = false;

                }
            }

            if (e.Button == MouseButtons.Right)
            {
                if (_drawRightMouse)
                {
                    EndPointF = e.Location;
                    var mass = (EndPointF.X - StartPointF.X * EndPointF.Y - StartPointF.Y) / 10;
                    CreatePhysicsObject(PhysicsObject.Type.Box, StartPointF, EndPointF, (int)mass, false);
                    _drawRightMouse = false;
                }
            }
        }

        private void GameCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            _mousePos = e.Location;
        }
        #endregion


        #region KeyboardEvents
        private void FormMainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                _physicsSystem.freezeAll();
            }
            if (e.KeyCode == Keys.W)
            {
                _physicsSystem.freezeAll();
            }
        }
        #endregion
        
    }
}