using UnityEngine;

namespace ClusterLab.UseCase
{
    public static class HSV
    {
        public static Color LerpHSV(Color a, Color b, float x)
        {
            var ah = RGB2HSV(a);
            var bh = RGB2HSV(b);
            var lerpValue = new Vector3(
                Mathf.LerpAngle(ah.x, bh.x, x),
                Mathf.Lerp(ah.y, bh.y, x),
                Mathf.Lerp(ah.z, bh.z, x)
            );
            return HSV2RGB(lerpValue);
        }

        static Vector3 RGB2HSV(Color color)
        {
            var cmax = Mathf.Max(color.r, color.g, color.b);
            var cmin = Mathf.Min(color.r, color.g, color.b);
            var delta = cmax - cmin;

            var hue = 0f;
            var saturation = 0f;

            if (Mathf.Approximately(cmax, color.r))
            {
                hue = 60 * (((color.g - color.b) / delta) % 6);
            }
            else if (Mathf.Approximately(cmax, color.g))
            {
                hue = 60 * ((color.b - color.r) / delta + 2);
            }
            else if (Mathf.Approximately(cmax, color.b))
            {
                hue = 60 * ((color.r - color.g) / delta + 4);
            }

            if (cmax > 0)
            {
                saturation = delta / cmax;
            }

            return new Vector3(hue, saturation, cmax);
        }

        static Color HSV2RGB(Vector3 color)
        {
            var hue = color.x;
            var c = color.z * color.y;
            var x = c * (1 - Mathf.Abs((hue / 60) % 2 - 1));
            var m = color.z - c;

            var r = 0f;
            var g = 0f;
            var b = 0f;

            if (hue < 60)
            {
                r = c;
                g = x;
            }
            else if (hue < 120)
            {
                r = x;
                g = c;
            }
            else if (hue < 180)
            {
                g = c;
                b = x;
            }
            else if (hue < 240)
            {
                g = x;
                b = c;
            }
            else if (hue < 300)
            {
                r = x;
                b = c;
            }
            else
            {
                r = c;
                b = x;
            }

            return new Color(r + m, g + m, b + m);
        }
    }
}
