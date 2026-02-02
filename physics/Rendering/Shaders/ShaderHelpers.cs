using System;

namespace SharpPhysics.Rendering.Shaders {
    public static class ShaderHelpers
    {
        /// <summary>
        /// Converts an HSV color value to RGB.
        /// h (hue) should be in [0,360), s (saturation) and v (value) in [0,1].
        /// </summary>
        public static void HsvToRgb(double h, double s, double v, out int r, out int g, out int b)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            double r1, g1, b1;

            if (h < 60)
            {
                r1 = c; g1 = x; b1 = 0;
            }
            else if (h < 120)
            {
                r1 = x; g1 = c; b1 = 0;
            }
            else if (h < 180)
            {
                r1 = 0; g1 = c; b1 = x;
            }
            else if (h < 240)
            {
                r1 = 0; g1 = x; b1 = c;
            }
            else if (h < 300)
            {
                r1 = x; g1 = 0; b1 = c;
            }
            else
            {
                r1 = c; g1 = 0; b1 = x;
            }

            r = (int)((r1 + m) * 255);
            g = (int)((g1 + m) * 255);
            b = (int)((b1 + m) * 255);
        }
    }
}