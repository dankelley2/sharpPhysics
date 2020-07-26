using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Structs;

namespace physics.Engine
{
    public abstract class aShader
    {
        public abstract void PreDraw(PhysicsObject obj, Graphics g);
        public abstract void Draw(PhysicsObject obj, Graphics g);
        public abstract void PostDraw(PhysicsObject obj, Graphics g);
    }

    public class ShaderDefault : aShader
    {
        public static readonly Pen RedPen = new Pen(Color.Red);

        public override void PreDraw(PhysicsObject obj, Graphics g)
        {
        }

        public override void Draw(PhysicsObject obj, Graphics g)
        {
            g.DrawRectangle(RedPen, obj.Aabb.Min.X, obj.Aabb.Min.Y, obj.Aabb.Max.X - obj.Aabb.Min.X, obj.Aabb.Max.Y - obj.Aabb.Min.Y);
        }

        public override void PostDraw(PhysicsObject obj, Graphics g)
        {
        }
    }

    public class ShaderWall : aShader
    {
        public static readonly SolidBrush RedPen = new SolidBrush(Color.Gray);

        public override void PreDraw(PhysicsObject obj, Graphics g)
        {
        }

        public override void Draw(PhysicsObject obj, Graphics g)
        {
            g.FillRectangle(RedPen, obj.Aabb.Min.X, obj.Aabb.Min.Y, obj.Aabb.Max.X - obj.Aabb.Min.X, obj.Aabb.Max.Y - obj.Aabb.Min.Y);
        }

        public override void PostDraw(PhysicsObject obj, Graphics g)
        {
        }
    }

    public class ShaderBall : aShader
    {
        public static readonly SolidBrush BrushLightGray = new SolidBrush(Color.LightGray);
        public static readonly SolidBrush BrushWhite = new SolidBrush(Color.White);
        public static readonly Pen PenVelocity = new Pen(Color.Red) { Color = Color.FromArgb(50, 255, 0, 0), Width = 1, EndCap = LineCap.ArrowAnchor };

        public override void PreDraw(PhysicsObject obj, Graphics g)
        {
            var origin = obj.Aabb.Min;
            g.FillEllipse(new SolidBrush(Color.FromArgb(100, 50, 50, 50)), origin.X + 3, origin.Y + 5, obj.Width - 2, obj.Height - 2);
        }

        public override void Draw(PhysicsObject obj, Graphics g)
        {
            var origin = obj.Aabb.Min;
            g.FillEllipse(BrushLightGray, origin.X, origin.Y, obj.Width, obj.Height);
            g.FillEllipse(BrushWhite, origin.X + obj.Width * .1F, origin.Y + obj.Height * .1F, obj.Width * .6F, obj.Height * .6F);
            g.DrawLine(PenVelocity, obj.Center.X, obj.Center.Y, obj.Center.X + obj.Velocity.X * 10, obj.Center.Y + obj.Velocity.Y * 10);
        }

        public override void PostDraw(PhysicsObject obj, Graphics g)
        {
        }
    }

    public class ShaderBallVelocity : aShader
    {
        public static readonly SolidBrush BrushLightGray = new SolidBrush(Color.LightGray);
        public static readonly SolidBrush BrushWhite = new SolidBrush(Color.White);
        public static readonly Pen PenVelocity = new Pen(Color.Red) { Color = Color.FromArgb(50, 255, 0, 0), Width = 1, EndCap = LineCap.ArrowAnchor };

        public override void PreDraw(PhysicsObject obj, Graphics g)
        {
        }

        public override void Draw(PhysicsObject obj, Graphics g)
        {
            var origin = obj.Aabb.Min;
            int r, gee, b;
            double particleSpeed = 220 - Math.Min((int)obj.Velocity.Length, 220);
            ColorUtil.HsvToRgb(particleSpeed, 1, 1, out r, out gee, out b);
            g.FillEllipse(new SolidBrush(Color.FromArgb(255, r, gee, b)), origin.X, origin.Y, obj.Width, obj.Height);
        }

        public override void PostDraw(PhysicsObject obj, Graphics g)
        {
        }
    }

    public class ShaderWater : aShader
    {
        public static readonly SolidBrush BrushBlue = new SolidBrush(Color.FromArgb(255, 67, 142, 191));
        public static readonly SolidBrush BrushLightBlue = new SolidBrush(Color.FromArgb(200, 67, 142, 191));
        public static readonly Pen PenBlue = new Pen(Color.Blue, 2);

        public static int size = 15;

        private static readonly Matrix Matrix = new Matrix();
        private static float Rotation = 0;
        private static  readonly PointF[] Points = {
            new PointF(-(size/2), -(size/2)),
            new PointF(size/2, -(size/2)),
            new PointF(size /2, size/2),
            new PointF(-(size /2), size/2)
        };

        private static readonly GraphicsPath Path = new GraphicsPath();

        public ShaderWater()
        {
        }

        public override void Draw(PhysicsObject obj, Graphics g)
        {
        }

        public override void PreDraw(PhysicsObject obj, Graphics g)
        {
            //Reset Matrix
            Matrix.Reset();
            //Apply Matrix
            Path.Reset();

            // calculate stretch and rotation
            var stretchFactor = Math.Max(1,obj.Velocity.Length/50);
            Rotation =((float)Math.Atan2(obj.Velocity.Y, obj.Velocity.X)).RadiansToDegrees();

            // Transform identity mtx to position
            Matrix.Translate(obj.Center.X, obj.Center.Y);
            Matrix.Rotate((float)Rotation);
            Matrix.Scale(stretchFactor,1);

            //Apply mtx to path
            Path.AddClosedCurve(Points);
            Path.Transform(Matrix);

            //Draw Path
            if (obj.LastCollision == null)
            {
                g.FillPath(BrushLightBlue, Path);
            }
            else
            {
                g.FillPath(BrushBlue, Path);
            }
        }

        public override void PostDraw(PhysicsObject obj, Graphics g)
        {
        }
    }

    public static class ColorUtil
    {
        public static void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        static int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}