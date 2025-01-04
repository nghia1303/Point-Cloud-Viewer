using OpenTK;
using System;

namespace PointCloudViewer
{
    public enum ColorMode
    {
        RGB,
        Rainbow,
        Warm,
        Cold
    }
    internal class PointCloudColorMapper
    {
        private Vector3h Red;
        private Vector3h Orange;
        private Vector3h Yellow;
        private Vector3h Green;
        private Vector3h Cyan;
        private Vector3h Blue;
        private Vector3h Purple;

        private double MinZ;
        private double MaxZ;

        public PointCloudColorMapper(double minZ, double maxZ)
        {
            MinZ = minZ;
            MaxZ = maxZ;

            InitializeColors();
        }

        private void InitializeColors()
        {
            Yellow = new Vector3h(1, 1, 0);
            Red = new Vector3h(1, 0, 0);
            Orange = new Vector3h(1, (Half)0.647, 0);
            Green = new Vector3h(0, 1, 0);
            Cyan = new Vector3h(0, (Half)0.498,   1);
            Blue = new Vector3h(0, 0, 0);
            Purple = new Vector3h((Half)0.545, 0, 1);
        }

        public Vector3h MapColor(ColorMode colorMode, double[] coordArray = null, double[] colorArray = null)
        {
            switch (colorMode)
            {
                case ColorMode.RGB:
                    return MapRGBColor(colorArray);

                case ColorMode.Rainbow:
                    return MapRainbowColor(coordArray);

                case ColorMode.Warm:
                    return MapWarmColor(coordArray);

                case ColorMode.Cold:
                    return MapColdColor(coordArray);

                default:
                    throw new ArgumentException("Invalid color mode");
            }
        }

        private Vector3h MapRGBColor(double[] colorArray)
        {
            return new Vector3h(
                (Half)(colorArray[0] / 255.0),
                (Half)(colorArray[1] / 255.0),
                (Half)(colorArray[2] / 255.0)
            );
        }

        private Vector3h MapRainbowColor(double[] coordArray)
        {
            double z = coordArray[2];
            double zRange = MaxZ - MinZ;

            double[] zSegments = new double[6];
            for (int i = 1; i <= 6; i++)
            {
                zSegments[i - 1] = i * zRange / 6 + MinZ;
            }

            Vector3h[] colors = new Vector3h[]
            {
                    Red, Orange, Yellow,
                    Green, Cyan, Blue, Purple
            };

            for (int i = 0; i < zSegments.Length; i++)
            {
                if (z <= zSegments[i])
                {
                    double start = i == 0 ? MinZ : zSegments[i - 1];
                    double end = zSegments[i];

                    return Interpolate(z, start, end, colors[i], colors[i + 1]);
                }
            }

            return Interpolate(z, zSegments[5], MaxZ, colors[6], colors[6]);
        }

        private Vector3h MapWarmColor(double[] coordArray)
        {
            double z = coordArray[2];
            double zMidpoint = 1 * (MaxZ - MinZ) / 2 + MinZ;

            return z <= zMidpoint
                ? Interpolate(z, MinZ, zMidpoint, Red, Orange)
                : Interpolate(z, zMidpoint, MaxZ, Orange, Yellow);
        }

        private Vector3h MapColdColor(double[] coordArray)
        {
            double z = coordArray[2];
            double zMidpoint = 1 * (MaxZ - MinZ) / 2 + MinZ;

            return z <= zMidpoint
                ? Interpolate(z, MinZ, zMidpoint, Green, Cyan)
            : Interpolate(z, zMidpoint, MaxZ, Cyan, Blue);
        }

        private Vector3h Interpolate(double value, double start, double end, Vector3h startColor, Vector3h endColor)
        {
            double t = (value - start) / (end - start);
            return new Vector3h(
                (Half)(startColor.X + t * (endColor.X - startColor.X)),
                (Half)(startColor.Y + t * (endColor.Y - startColor.Y)),
                (Half)(startColor.Z + t * (endColor.Z - startColor.Z))
            );
        }
    }
}