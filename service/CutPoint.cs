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
            // Calculate plane normal using points A, B, and C
            Vector3d edge1 = pointB - pointA;
            Vector3d edge2 = pointC - pointA;
            Vector3d planeNormal = Vector3d.Cross(edge1, edge2).Normalized();

            // Find a Point on the plane (any of the three given points can be used)
            Vector3d planePoint = pointA;

            // Calculate the projection Point using previously defined functions
            return GetProjectionPoint(pointP, planeNormal, planePoint);
        }
        private static Vector3d GetProjectionPoint(Vector3d pointP, Vector3d planeNormal, Vector3d planePoint)
        {
            // Calculate the line direction vector (same as the plane normal)
            Vector3d lineDirection = planeNormal;

            // Calculate the t value
            double t = Vector3d.Dot(planePoint - pointP, planeNormal) / Vector3d.Dot(lineDirection, planeNormal);
            //t = (planePoint - pointP).planeNormal / (planeNormal.planeNormal)

            // Calculate the projection Point
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

                // Kiểm tra nếu điểm trùng với một đỉnh của đa giác
                if (xp == x1 && yp == y1)
                {
                    return true;
                }

                // Kiểm tra nếu điểm nằm trên cạnh của đa giác
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
            // Define the plane normal using the coefficients of the plane equation
            Vector3d edge1 = pointB - pointA;
            Vector3d edge2 = pointC - pointA;
            Vector3d planeNormal = Vector3d.Cross(edge1, edge2).Normalized();

            // Normalize the plane normal
            planeNormal = planeNormal.Normalized();

            // Define the new z' axis as the plane normal
            Vector3d zPrime = planeNormal;

            // Define an arbitrary vector not parallel to z'
            Vector3d arbitraryVector = (Math.Abs(zPrime.X) > Math.Abs(zPrime.Z)) ? new Vector3d(-zPrime.Y, zPrime.X, 0) : new Vector3d(0, -zPrime.Z, zPrime.Y);

            // Define the new x' axis as the cross product of z' and the arbitrary vector, normalized
            Vector3d xPrime = Vector3d.Cross(zPrime, arbitraryVector).Normalized();

            // Define the new y' axis as the cross product of z' and x'
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
