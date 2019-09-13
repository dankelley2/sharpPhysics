﻿using System;
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
        private readonly Timer PhysicsTimer = new Timer();
        private readonly Timer RefreshTimer = new Timer();
        private readonly Stopwatch stopWatch = new Stopwatch();

        private PhysicsSystem physicsSystem = new PhysicsSystem();
        private bool DrawLeftMouse = false;
        private bool DrawRightMouse = false;
        private PointF StartPointF { get; set; }
        private PointF EndPointF { get; set; }

        private bool grabbing;
        private PointF MousePos;


        private PointF mouseThen;
        private long msFrameTime;
        private long msLastFrame;
        private long msPerDrawCycle;
        private long msPhysics;
        private long msThisFrame;
        private int Radius = 20;
        private Bitmap poolBall = new Bitmap(physics.Properties.Resources.blackPoolBall_40px);
        private Bitmap poolTable = new Bitmap(physics.Properties.Resources.poolTable);


        public FormMainWindow()
        {
            InitializeComponent();
            poolBall.MakeTransparent();
            RefreshTimer.Enabled = true;
            RefreshTimer.Interval = 1000 / 60;
            RefreshTimer.Tick += RefreshTimer_Tick;

            PhysicsTimer.Enabled = true;
            PhysicsTimer.Interval = 1000 / 100;
            PhysicsTimer.Tick += PhysicsTimer_Tick;
            stopWatch.Start();
        }

        private void PhysicsTimer_Tick(object sender, EventArgs e)
        {
            var msElapsed = stopWatch.ElapsedMilliseconds;

            if (grabbing)
            {
                DragObject(MousePos);
            }

            physicsSystem.UsePointGravity = false;
            physicsSystem.gravityPoint = new Vec2 { X = MousePos.X, Y = MousePos.Y};

        physicsSystem.Tick(stopWatch.ElapsedMilliseconds);
            msPhysics = stopWatch.ElapsedMilliseconds - msElapsed;
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            var msElapsed = stopWatch.ElapsedMilliseconds;
            RefreshScreen();
            msPerDrawCycle = stopWatch.ElapsedMilliseconds - msElapsed;
            msLastFrame = msThisFrame;
            msThisFrame = stopWatch.ElapsedMilliseconds;
            msFrameTime = msThisFrame - msLastFrame;
        }

        private void RefreshScreen()
        {
            pictureBox1.Invalidate();
            //foreach (var o in system.staticObjects)
            //{
            //    panel1.Invalidate(new Region(new RectangleF(o.aabb.Min.X - 20, o.aabb.Min.Y - 20, o.width + 40, o.height + 40)));
            //}
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            e.Graphics.CompositingMode = CompositingMode.SourceOver;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            e.Graphics.DrawString("ms per physics cycle: " + msPhysics, DefaultFont, new SolidBrush(Color.Black),
                new PointF(10, 10));
            e.Graphics.DrawString("ms total draw time: " + msPerDrawCycle, DefaultFont, new SolidBrush(Color.Black),
                new PointF(10, 30));
            e.Graphics.DrawString("frame rate: " + 1000 / Math.Max(msFrameTime, 1), DefaultFont,
                new SolidBrush(Color.Black), new PointF(10, 50));
            e.Graphics.DrawString("num objects: " + physicsSystem.staticObjects.Count, DefaultFont,
                new SolidBrush(Color.Black), new PointF(10, 70));
            if (grabbing)
            {
                e.Graphics.DrawLine(new Pen(Color.DarkGreen), MousePos, physicsSystem.getActiveObjectCenter());
            }

            if (DrawLeftMouse)
            {
                var penArrow = new Pen(Color.Green,2);
                penArrow.EndCap = LineCap.ArrowAnchor;
                penArrow.StartCap = LineCap.Round;
                e.Graphics.DrawLine(penArrow, MousePos, StartPointF);
                e.Graphics.DrawEllipse(new Pen(Color.DarkBlue), (int)(StartPointF.X-Radius), (int)(StartPointF.Y-Radius), Radius*2, Radius*2);
            }

            var penVelocity = new Pen(Color.White, 1);
            penVelocity.EndCap = LineCap.ArrowAnchor;

            foreach (var o in physicsSystem.staticObjects.Where(x => x.ShapeType == PhysicsObject.Type.Circle))
            {
                var origin = o.Aabb.Min;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(100,50,50,50)), origin.X+3, origin.Y+5, o.Width-2, o.Height-2);
            }

            foreach (var o in physicsSystem.staticObjects)
            {
                var origin = o.Aabb.Min;
                switch (o.ShapeType)
                {

                    case PhysicsObject.Type.Box:
                        e.Graphics.FillRectangle(new SolidBrush(Color.DimGray), origin.X, origin.Y, o.Width, o.Height);
                        break;
                    case PhysicsObject.Type.Circle:
                        e.Graphics.DrawLine(penVelocity, o.Center.X, o.Center.Y, o.Center.X + o.Velocity.X * 10, o.Center.Y + o.Velocity.Y * 10);
                        e.Graphics.FillEllipse(new SolidBrush(Color.Aqua), origin.X, origin.Y, o.Width, o.Height);
                        //e.Graphics.DrawImage(poolBall, new PointF(o.Pos.X, o.Pos.Y));
                        break;
                }
            }
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (physicsSystem.ActivateAtPoint(e.Location))
                {
                    grabbing = true;
                    return;
                }

                StartPointF = e.Location;
                DrawLeftMouse = true;
            }

            if (e.Button == MouseButtons.Right)
            {
                if (physicsSystem.ActivateAtPoint(e.Location))
                {
                    physicsSystem.removeActiveObject();
                    return;
                }

                StartPointF = e.Location;
                DrawRightMouse = true;
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            MousePos = e.Location;
            //if (e.Button == MouseButtons.Right)
            //{
            //    if (Math.Abs(e.X - mouseThen.X) > 5 || Math.Abs(e.Y - mouseThen.Y) > 5)
            //    {
            //        mouseThen = e.Location;
            //        CreatePhysicsObject(PhysicsObject.Type.Box, e.Location, Radius, 999999);
            //    }
            //}
        }

        private PointF SnapPointToGrid(PointF p1)
        {
            return p1; //new PointF(((int) p1.X / (Radius*2)) * (Radius * 2), ((int)p1.Y / (Radius * 2)) * (Radius * 2));
        }

        private void CreatePhysicsObject(PhysicsObject.Type type, PointF loc, int size, int mass, bool locked)
        {
            var aabb = new AABB
            {
                Min = new Vec2 { X = loc.X - size, Y = loc.Y - size },
                Max = new Vec2 { X = loc.X + size, Y = loc.Y + size }
            };
            PhysMath.CorrectBoundingBox(ref aabb);
            var obj = new PhysicsObject(aabb, type, .92F, mass , false);
            physicsSystem.staticObjects.Add(obj);
        }
        private void CreatePhysicsObject(PhysicsObject.Type type, PointF start, PointF end, int mass, bool locked)
        {
            var aabb = new AABB
            {
                Min = new Vec2 { X = start.X, Y = start.Y },
                Max = new Vec2 { X = end.X, Y = end.Y }
            };
            PhysMath.CorrectBoundingBox(ref aabb);
            var obj = new PhysicsObject(aabb, type, .75F, 5000, locked);
            physicsSystem.staticObjects.Add(obj);
        }

        private void DragObject(PointF location)
        {
            physicsSystem.MoveActiveTowardsPoint(new Vec2 {X = location.X, Y = location.Y});
        }

        private void panel1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (grabbing)
                {
                    stopDragObject();
                    return;
                }

                if (DrawLeftMouse)
                {
                    EndPointF = e.Location;
                    CreatePhysicsObject(PhysicsObject.Type.Circle, StartPointF, Radius, 500, false);
                    physicsSystem.ActivateAtPoint(StartPointF);
                    Vec2 delta = (new Vec2 {X = EndPointF.X, Y = EndPointF.Y} -
                                 new Vec2 {X = StartPointF.X, Y = StartPointF.Y}) / 10;
                    physicsSystem.AddVelocityToActive(-delta);
                    DrawLeftMouse = false;
                }
            }

            if (e.Button == MouseButtons.Right)
            {
                if (DrawRightMouse)
                {
                    EndPointF = e.Location;
                    CreatePhysicsObject(PhysicsObject.Type.Box, StartPointF, EndPointF, 2000, false);
                }
            }
        }

        private void stopDragObject()
        {
            physicsSystem.ReleaseActiveObject();
            grabbing = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            Size size = new Size(10, 10);
            for (var a = 0; a <= 180; a += 5)
            {
                var x = 200 * Math.Cos(a * Math.PI / 180) + 300;
                var y = 200 * Math.Sin(a * Math.PI / 180) + 300;

                var p = new PointF((float)x, (float)y);

                CreatePhysicsObject(PhysicsObject.Type.Box, p, Radius, 1000, true);
            }


            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(60, 0), new PointF(0, pictureBox1.Height), 5000, true);
            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(pictureBox1.Width, 0), new PointF(pictureBox1.Width-60, pictureBox1.Height), 5000, true);
            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(0, 0), new PointF(pictureBox1.Width, 60), 5000, true);
            CreatePhysicsObject(PhysicsObject.Type.Box, new PointF(0, pictureBox1.Height), new PointF(pictureBox1.Width, pictureBox1.Height-60), 5000, true);

            for (int i = 0; i < 500; i += 40)
            {
                for (int j = 0; j < 200; j += 40)
                {
                    CreatePhysicsObject(PhysicsObject.Type.Circle, new PointF(i + 300, j + 100), Radius, 500, false);
                }
            }

        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                physicsSystem.freezeAll();
            }
        }
        
    }
}