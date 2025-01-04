using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PointCloudViewer
{
    public struct ColorPoint
    {
        public Vector3d Point;
        public Vector3h Color;
        public byte Classification;
        public bool State;
    }

    internal class PointCloudNode
    {
        #region Member Variable

        private PointCloudColorMapper pointCloudColorMapper { get; set; }
        public const int maxPointPerNode = 50000;
        private List<ColorPoint> data;
        private List<int> dataIndex;
        private PointCloudNode[] childNode;
        private int _displayListId;
        private bool _displayListDirty = true;
        private Vector3d minCoordinate, maxCoordinate;
        private Vector3d midPoint = Vector3d.Zero;
        private bool OutlineInFrustum;
        private string pointCloudColor;
        private bool isLeaf = false;
        private readonly object _lockObject = new object();

        #endregion Member Variable

        public PointCloudNode(byte depth, ref List<int> dataIndex, ref List<ColorPoint> data,
      ref Vector3d minv, ref Vector3d maxv, PointCloudColorMapper pointCloudColorMapper, string colorType)
        {
            if (dataIndex == null || data == null || dataIndex.Count == 0 || data.Count == 0) return;

            if (dataIndex.Count < maxPointPerNode)
            {
                this.dataIndex = dataIndex;
                this.data = data;
                this.pointCloudColorMapper = pointCloudColorMapper;
                minCoordinate = minv;
                maxCoordinate = maxv;
                midPoint = (minv + maxv) / 2;
                GenerateDisplayList(colorType);
            }
            else
            {
                this.dataIndex = null;
                childNode = new PointCloudNode[8];
                Vector3d split = (minv + maxv) / 2;
                List<int>[] childIndex = new List<int>[8];
                int[] counts = new int[8];
                byte nextDepth = (byte)(depth + 1);
                foreach (var v in dataIndex)
                {
                    int index = GetOctantIndex(data[v].Point, split);
                    counts[index]++;
                }
                for (int i = 0; i < 8; i++)
                {
                    if (counts[i] > 0)
                        childIndex[i] = new List<int>(counts[i]);
                }
                foreach (var v in dataIndex)
                {
                    int index = GetOctantIndex(data[v].Point, split);
                    childIndex[index].Add(v);
                }
                for (int i = 0; i < 8; i++)
                {
                    if (childIndex[i]?.Count > 0)
                    {
                        Vector3d minva = new Vector3d(
                            (i & 1) == 0 ? split.X : minv.X,
                            (i & 2) == 0 ? split.Y : minv.Y,
                            (i & 4) == 0 ? split.Z : minv.Z
                        );
                        Vector3d maxva = new Vector3d(
                            (i & 1) == 0 ? maxv.X : split.X,
                            (i & 2) == 0 ? maxv.Y : split.Y,
                            (i & 4) == 0 ? maxv.Z : split.Z
                        );
                        childNode[i] = new PointCloudNode(nextDepth, ref childIndex[i], ref data, ref minva, ref maxva, pointCloudColorMapper, colorType);
                    }
                }
            }
        }
        private void GenerateDisplayList(string colorType)
        {
            isLeaf = true;
            _displayListId = GL.GenLists(1);
            GL.NewList(_displayListId, ListMode.Compile);
            GL.Begin(PrimitiveType.Points);
            foreach (var index in dataIndex)
            {
                var color = data[index].Color;
                var point = data[index].Point;
                var colorArray = new double[3];
                var coordArray = new double[3];
                // Apply color ramp
                if (colorType == "rgb")
                {
                    colorArray[0] = color.X;
                    colorArray[1] = color.Y;
                    colorArray[2] = color.Z;
                    color = pointCloudColorMapper.MapColor(ColorMode.RGB, null, colorArray);
                }
                else
                {
                    coordArray[0] = point.X;
                    coordArray[1] = point.Y;
                    coordArray[2] = point.Z;
                }
                if (colorType == "rainbow")
                {
                    color = pointCloudColorMapper.MapColor(ColorMode.Rainbow, coordArray, null);
                }
                else if (colorType == "warm")
                {
                    color = pointCloudColorMapper.MapColor(ColorMode.Warm, coordArray, null);
                }
                else if (colorType == "cold")
                {
                    color = pointCloudColorMapper.MapColor(ColorMode.Cold, coordArray, null);
                }
                GL.Color3(color.X, color.Y, color.Z);
                GL.Vertex3(data[index].Point.X, data[index].Point.Y, data[index].Point.Z);
            }
            GL.End();
            GL.EndList();
        }
        public void UpdatePointsColor(string colorType)
        {
            if (dataIndex != null)
            {
                UpdateDisplayList(colorType);
            }
            if (childNode != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (childNode[i] != null)
                    {
                        childNode[i].UpdatePointsColor(colorType);
                    }
                }
            }
        }
        public void UpdateDisplayList(string colorType)
        {
            _displayListDirty = true;
            GL.DeleteLists(_displayListId, 1);
            _displayListId = 0;
            _displayListId = GL.GenLists(1);
            GL.NewList(_displayListId, ListMode.Compile);
            GL.Begin(PrimitiveType.Points);
            foreach (var index in dataIndex)
            {
                var color = data[index].Color;
                var colorArray = new double[3];
                var coordArray = new double[3];
                if (colorType == "rgb")
                {
                    colorArray[0] = color.X;
                    colorArray[1] = color.Y;
                    colorArray[2] = color.Z;
                    color = pointCloudColorMapper.MapColor(ColorMode.RGB, null, colorArray);
                }
                if (colorType == "rainbow")
                {
                    coordArray[0] = data[index].Point.X;
                    coordArray[1] = data[index].Point.Y;
                    coordArray[2] = data[index].Point.Z;
                    color = pointCloudColorMapper.MapColor(ColorMode.Rainbow, coordArray);
                }
                else if (colorType == "warm")
                {
                    coordArray[0] = data[index].Point.X;
                    coordArray[1] = data[index].Point.Y;
                    coordArray[2] = data[index].Point.Z;
                    color = pointCloudColorMapper.MapColor(ColorMode.Warm, coordArray);
                }
                else if (colorType == "cold")
                {
                    coordArray[0] = data[index].Point.X;
                    coordArray[1] = data[index].Point.Y;
                    coordArray[2] = data[index].Point.Z;
                    color = pointCloudColorMapper.MapColor(ColorMode.Cold, coordArray);
                }
                GL.Color3(color.X, color.Y, color.Z);
                GL.Vertex3(data[index].Point.X, data[index].Point.Y, data[index].Point.Z);
            }
            GL.End();
            GL.EndList();
        }
        private int GetOctantIndex(Vector3d point, Vector3d split)
        {
            int index = 0;
            if (point.Z <= split.Z) index += 4;
            if (point.Y <= split.Y) index += 2;
            if (point.X <= split.X) index += 1;
            return index;
        }
        public void Render(int pointSize, bool showTreeNodeOutline, string pointCloudColor, double[,] frustum)
        {
            OutlineInFrustum = VoxelWithinFrustum(frustum, minCoordinate.X, minCoordinate.Y, minCoordinate.Z,
                maxCoordinate.X, maxCoordinate.Y, maxCoordinate.Z);
            this.pointCloudColor = pointCloudColor;

            if (!OutlineInFrustum && isLeaf)
            {
                return;
            }
            if (dataIndex != null)
            {
                GL.PointSize(pointSize);
                GL.CallList(_displayListId);

                if (showTreeNodeOutline == true)
                {
                    DrawNodeOutline(minCoordinate, maxCoordinate);
                }
            }
            if (childNode != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (childNode[i] != null)
                    {
                        childNode[i].Render(pointSize, showTreeNodeOutline, pointCloudColor, frustum);
                    }
                }
            }
        }
        private bool VoxelWithinFrustum(double[,] ftum, double minx, double miny, double minz,
            double maxx, double maxy, double maxz)
        {
            double x1 = minx, y1 = miny, z1 = minz;
            double x2 = maxx, y2 = maxy, z2 = maxz;
            for (int i = 0; i < 6; i++)
            {
                if ((ftum[i, 0] * x1 + ftum[i, 1] * y1 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y1 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x1 + ftum[i, 1] * y2 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y2 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x1 + ftum[i, 1] * y1 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y1 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x1 + ftum[i, 1] * y2 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y2 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F))
                {
                    return false;
                }
            }
            return true;
        }
        public void FindClosestPoint(double[,] frustum, Vector3d nearPoint, Vector3d farPoint,
            ref Point3DExt closestPoint)
        {
            if (!OutlineInFrustum && isLeaf)
                return;

            if (dataIndex != null)
            {
                double distance;
                foreach (int index in dataIndex)
                {
                    distance = CalculateDistance(data[index].Point, farPoint, nearPoint);
                    if (closestPoint.Flag > distance)
                    {
                        closestPoint.Point = data[index].Point;
                        closestPoint.Flag = distance;
                    }
                }
            }
            if (childNode != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (childNode[i] != null)
                    {
                        childNode[i].FindClosestPoint(frustum, nearPoint, farPoint, ref closestPoint);
                    }
                }
            }
        }
        private double CalculateDistance(Vector3d point, Vector3d lineStart, Vector3d lineEnd)
        {
            // Calculate vectors
            Vector3d line = lineEnd - lineStart;
            Vector3d pointToStart = point - lineStart;

            // Calculate cross product
            Vector3d cross = Vector3d.Cross(pointToStart, line);

            // Distance formula: |cross| / |line|
            return cross.Length / line.Length;
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
        public void FindProjection(List<Vector3d> midPoints, GLControl glControl1, ref List<ColorPoint> points)
        {
            var resultPoints = new List<ColorPoint>();
            //FindProjectionRecursive(midPoints, glControl1, ref points);
            //List<ColorPoint> pointsTemp = new List<ColorPoint>(points.Count);
            //for (int i = 0; i < points.Count; i++)
            //{
            //    if (!toRemove[i])
            //    {
            //        pointsTemp.Add(points[i]);
            //    }
            //}
            //points = pointsTemp;
        }
        private void FindProjectionRecursive(List<Vector3d> polygon, GLControl glControl, ref List<ColorPoint> points)
        {
            if (!OutlineInFrustum && isLeaf)
                return;

            // Process points in current node
            if (dataIndex != null)
            {
                Vector3d pointA = polygon[0];
                Vector3d pointB = polygon[1];
                Vector3d pointC = polygon[2];

                for (int index = 0; index < dataIndex.Count; index++)
                {
                    var point = data[dataIndex[index]];
                    Vector3d projectionPoint = GetProjectionPoint(point.Point, pointA, pointB, pointC);

                    if (TransformCoordinateAxes(pointA, pointB, pointC, projectionPoint, glControl, polygon))
                    {
                        lock (_lockObject)
                        {
                            points.Add(point);
                        }
                    }
                }
            }

            // Process child nodes
            if (childNode != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (childNode[i] != null)
                    {
                        childNode[i].FindProjectionRecursive(polygon, glControl, ref points);
                    }
                }
            }
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
        private void DrawNodeOutline(Vector3d midNodeCoordinate, Vector3d maxNodeCoordinate)
        {
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, midNodeCoordinate.Y, midNodeCoordinate.Z);//(0,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, midNodeCoordinate.Y, midNodeCoordinate.Z);//(1,0,0)

            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, maxNodeCoordinate.Y, midNodeCoordinate.Z);//(0,1,0)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, midNodeCoordinate.Z);//(1,1,0)

            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, midNodeCoordinate.Y, midNodeCoordinate.Z);//(0,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, maxNodeCoordinate.Y, midNodeCoordinate.Z);//(0,1,0)

            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, midNodeCoordinate.Y, midNodeCoordinate.Z);//(1,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, midNodeCoordinate.Z);//(1,1,0)

            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, midNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, midNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,0,1)

            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,1,1)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,1,1)

            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, midNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,1,1)

            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, midNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,1,1)

            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, midNodeCoordinate.Y, midNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, midNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, midNodeCoordinate.Z);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, midNodeCoordinate.Y, midNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, midNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, maxNodeCoordinate.Y, midNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(midNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.End();
        }
    }
    internal class PointCloudOctree : IDisposable
    {
        private PointCloudNode _root;
        private List<int> _dataIndex;
        private Vector3d _min, _max;
        private PointCloudColorMapper _pointCloudColorMapper;
        public PointCloudOctree(ref List<ColorPoint> data,
            ref Vector3d minv, ref Vector3d maxv, PointCloudColorMapper pointCloudColorMapper, string colorType = "RGB")
        {
            if (data == null)
                return;
            if (data.Count == 0)
                return;
            _dataIndex = Enumerable.Range(0, data.Count).ToList();
            _min = minv;
            _max = maxv;
            _pointCloudColorMapper = pointCloudColorMapper;
             _root = new PointCloudNode(0, ref _dataIndex, ref data, ref minv, ref maxv, pointCloudColorMapper, colorType);
        }
        public void FindProjection(List<Vector3d> midpoints, GLControl glControl1, ref List<ColorPoint> points)
        {
            if (_root == null)
                return;
            _root.FindProjection(midpoints, glControl1, ref points);
        }
        public void Render(int pointSize, bool showTreeNodeOutline, string pointCloudColor, double[,] frustum)
        {
            if (_root != null)
                _root.Render(pointSize, showTreeNodeOutline, pointCloudColor, frustum);
        }
        public void UpdateColor(List<ColorPoint> points, string colorType)
        {
            if (_root == null) return;
            _dataIndex = Enumerable.Range(0, points.Count).ToList();
            _root.UpdatePointsColor(colorType);
            GC.Collect();
        }
        public void FindClosestPoint(double[,] frustum, Vector3d nearPoint, Vector3d farPoint,
            ref Point3DExt closestPoint)
        {
            if (_root != null)
                _root.FindClosestPoint(frustum, nearPoint, farPoint, ref closestPoint);
        }
        public void Dispose()
        {
            _root = null;
            _dataIndex = null;
        }
    }
}
}