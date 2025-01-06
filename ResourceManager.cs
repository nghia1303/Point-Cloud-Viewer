using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using laszip.net;
using OpenTK;

namespace PointCloudViewer
{
    public class ResourceManager
    {
        public laszip_header LasHeader;
        private ResourceManager _instance;
        public Vector3d MaxDrawCoord { get; set; }
        public Vector3d MinDrawCoord { get; set; }
        public Vector3d MaxRealCoord { get; set; }
        public Vector3d MinRealCoord { get; set; }
        public Vector3d CenterCoord { get; set; }
        public Vector3d ScaleFactor { get; set; }
        public double Scale { get; set; }
        public List<ColorPoint> Points { get; set; }

        public ResourceManager GetInstance()
        {
            if (_instance == null)
                _instance = this;
            return _instance;
        }

        public ResourceManager(ref List<ColorPoint> points)
        {
            Points = points;
            _instance = this;
        }

        public Vector3d GetRealCoordinate(ColorPoint point)
        {
            Vector3d realPointCoord = new Vector3d(
                (point.Point.X * Scale) + CenterCoord.X,
                (point.Point.Y * Scale) + CenterCoord.Y,
                (point.Point.Z * Scale) + CenterCoord.Z
            );
            return realPointCoord;
        }
        public static Vector3d GetRealCoordinateStatic(ColorPoint point, double scale, Vector3d centerCoord)
        {
            Vector3d realPointCoord = new Vector3d(
                (point.Point.X * scale) + centerCoord.X,
                (point.Point.Y * scale) + centerCoord.Y,
                (point.Point.Z * scale) + centerCoord.Z
            );
            return realPointCoord;
        }
    }
}