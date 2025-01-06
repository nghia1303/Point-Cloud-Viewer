using OpenTK;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PointCloudViewer.service
{
    internal class CutPoint
    {
        public static Vector3d GetProjectionPoint(Vector3d pointP, Vector3d pointA, Vector3d pointB, Vector3d pointC)
        {
            Vector3d edge1 = pointB - pointA;
            Vector3d edge2 = pointC - pointA;
            Vector3d planeNormal = Vector3d.Cross(edge1, edge2).Normalized();

            Vector3d planePoint = pointA;

            return GetProjectionPoint(pointP, planeNormal, planePoint);
        }
        private static Vector3d GetProjectionPoint(Vector3d pointP, Vector3d planeNormal, Vector3d planePoint)
        {
            Vector3d lineDirection = planeNormal;

            double t = Vector3d.Dot(planePoint - pointP, planeNormal) / Vector3d.Dot(lineDirection, planeNormal);

            Vector3d projectionPoint = pointP + t * lineDirection;

            return projectionPoint;
        }
        private static bool IsPointInsidePolygon(List<PointF> polygon, Vector3d pointP, Vector3d xPrime, Vector3d yPrime, GLControl glControl1)
        {
            PointF point = ConvertVectorToPoint(pointP, xPrime, yPrime, glControl1.Width, glControl1.Height);

            bool inside = false;
            int j = polygon.Count - 1;
            double xp = point.X, yp = point.Y;

            for (int i = 0; i < polygon.Count; i++)
            {
                double x1 = polygon[i].X, y1 = polygon[i].Y;
                double x2 = polygon[j].X, y2 = polygon[j].Y;

                if (xp == x1 && yp == y1)
                {
                    return true;
                }

                if ((y1 > yp) != (y2 > yp))
                {
                    double intersectX = (x2 - x1) * (yp - y1) / (y2 - y1) + x1;
                    if (xp < intersectX)
                    {
                        inside = !inside;
                    }
                }
                j = i;
            }
            return inside;
        }
        private static bool TransformCoordinateAxes(Vector3d pointA, Vector3d pointB, Vector3d pointC, Vector3d projectionPoint, GLControl glControl1, List<Vector3d> midpoints)
        {
            Vector3d edge1 = pointB - pointA;
            Vector3d edge2 = pointC - pointA;
            Vector3d planeNormal = Vector3d.Cross(edge1, edge2).Normalized();

            planeNormal = planeNormal.Normalized();

            Vector3d zPrime = planeNormal;

            Vector3d arbitraryVector = (Math.Abs(zPrime.X) > Math.Abs(zPrime.Z)) ? new Vector3d(-zPrime.Y, zPrime.X, 0) : new Vector3d(0, -zPrime.Z, zPrime.Y);

            Vector3d xPrime = Vector3d.Cross(zPrime, arbitraryVector).Normalized();

            Vector3d yPrime = Vector3d.Cross(zPrime, xPrime);

            List<PointF> pointPolygon = ConvertPolygonSpace2D(midpoints, xPrime, yPrime, glControl1);

            return IsPointInsidePolygon(pointPolygon, projectionPoint, xPrime, yPrime, glControl1);
        }
        public static List<ColorPoint> FindProjection(List<Vector3d> midpoints, GLControl glControl1, List<ColorPoint> points)
        {
            if (points == null || points.Count == 0)
                return null;

            Vector3d pointA = midpoints[0];
            Vector3d pointB = midpoints[1];
            Vector3d pointC = midpoints[2];

            int pointCount = points.Count;            
            bool[] toRemove = ArrayPool<bool>.Shared.Rent(pointCount);
            var rangePartitioner = Partitioner.Create(0, pointCount);

            ConcurrentBag<int> indicesToUpdate = new ConcurrentBag<int>();
            Array.Clear(toRemove, 0, pointCount);
            Parallel.ForEach(rangePartitioner, (range, loopState) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    Vector3d projectionPoint = GetProjectionPoint(points[i].Point, pointA, pointB, pointC);
                    if (TransformCoordinateAxes(pointA, pointB, pointC, projectionPoint, glControl1, midpoints))
                    {
                        toRemove[i] = true;
                    }
                }
            });

            List<ColorPoint> pointsTemp = new List<ColorPoint>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                if (!toRemove[i])
                {
                    pointsTemp.Add(points[i]);
                }
            }
            points = pointsTemp;
            ArrayPool<bool>.Shared.Return(toRemove);
            return points;
        }
        public static PointF ConvertVectorToPoint(Vector3d vector, Vector3d xPrime, Vector3d yPrime, int wWindow, int hWindow, float max2DWidth = 1.0f, float max2DHeight = 1.0f)
        {
            float xScreen = (float)(Vector3d.Dot(vector, xPrime) / max2DWidth * wWindow);
            float yScreen = (float)(Vector3d.Dot(vector, yPrime) / max2DHeight * hWindow);
            return new PointF(xScreen, yScreen);
        }
        public static List<PointF> ConvertPolygonSpace2D(List<Vector3d> midpoints, Vector3d xPrime, Vector3d yPrime, GLControl glControl1)
        {
            int wWindow = glControl1.Width;
            int hWindow = glControl1.Height;
            return midpoints.Select(midpoint => ConvertVectorToPoint(midpoint, xPrime, yPrime, wWindow, hWindow)).ToList();
        }
    }
}
