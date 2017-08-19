using OpenTK.Graphics;
using System;

namespace SS14.Shared
{
    public static class ColorUtils
    {
        public static Color4 InterpolateBetween(Color4 endPoint1, Color4 endPoint2, double lambda)
        {
            if (lambda < 0 || lambda > 1)
            {
                throw new ArgumentOutOfRangeException("lambda");
            }
            return new Color4(
                (float)(endPoint1.R * lambda + endPoint2.R * (1 - lambda)),
                (float)(endPoint1.G * lambda + endPoint2.G * (1 - lambda)),
                (float)(endPoint1.B * lambda + endPoint2.B * (1 - lambda)),
                (float)(endPoint1.A * lambda + endPoint2.A * (1 - lambda))
            );
        }

        public static Color4 WithAlpha(this Color4 color, byte a)
        {
            color.A = a;
            return color;
        }

        public static Color4 WithAlpha(this Color4 color, float a)
        {
            color.A = a;
            return color;
        }

        public static Color4 FromHex(string hexColor, Color4? fallback = null)
        {
            if (hexColor[0] == '#')
            {

                if (hexColor.Length == 9)
                {
                    return new Color4(Convert.ToByte(hexColor.Substring(1, 2), 16),
                                      Convert.ToByte(hexColor.Substring(3, 2), 16),
                                      Convert.ToByte(hexColor.Substring(5, 2), 16),
                                      Convert.ToByte(hexColor.Substring(7, 2), 16));
                }
                else if (hexColor.Length == 7)
                {
                    return new Color4(Convert.ToByte(hexColor.Substring(1, 2), 16),
                                      Convert.ToByte(hexColor.Substring(3, 2), 16),
                                      Convert.ToByte(hexColor.Substring(5, 2), 16),
                                      255);
                }
                else if (hexColor.Length == 4)
                {
                    string r = hexColor[1].ToString();
                    string g = hexColor[2].ToString();
                    string b = hexColor[3].ToString();
                    string a = hexColor[4].ToString();

                    return new Color4(Convert.ToByte(r + r, 16),
                                      Convert.ToByte(g + g, 16),
                                      Convert.ToByte(b + b, 16),
                                      Convert.ToByte(a + a, 16));
                }
                else if (hexColor.Length == 3)
                {
                    string r = hexColor[1].ToString();
                    string g = hexColor[2].ToString();
                    string b = hexColor[3].ToString();

                    return new Color4(Convert.ToByte(r + r, 16),
                                      Convert.ToByte(g + g, 16),
                                      Convert.ToByte(b + b, 16),
                                      255);
                }
            }

            if (fallback.HasValue)
                return fallback.Value;
            else
                throw new ArgumentException("Invalid color code.", "hexColor");
        }
    }
}
