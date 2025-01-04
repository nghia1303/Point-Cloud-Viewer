using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PointCloudViewer
{
    public struct ColorPoint
    {
        public Vector3d Point;
        public Vector3h Color;
        public byte Classification;
    }

    internal class PointCloudNode
    {
        #region Member Variable

        private PointCloudColorMapper pointCloudColorMapper { get; set; }
        public const int maxPointPerNode = 50000;
        private List<ColorPoint> data;
        private List<int> dataIndex;
        private PointCloudNode[] childNode;
        private int displayList;
        private Vector3d minCoordinate, maxCoordinate;
        private Vector3d midPoint = Vector3d.Zero;
        private bool OutlineInFrustum;
        private string pointCloudColor;
        private bool isLeaf = false;

        #endregion Member Variable

      //  public PointCloudNode(byte depth, ref List<int> dataIndex, ref List<ColorPoint> data,
      //ref Vector3d minv, ref Vector3d maxv, PointCloudColorMapper pointCloudColorMapper, string colorType)
      //  {
      //      if (dataIndex == null || data == null || dataIndex.Count == 0 || data.Count == 0) return;

      //      if (dataIndex.Count < maxPointPerNode)
      //      {
      //          this.dataIndex = dataIndex;
      //          this.data = data;
      //          this.pointCloudColorMapper = pointCloudColorMapper;
      //          minCoordinate = minv;
      //          maxCoordinate = maxv;
      //          midPoint = (minv + maxv) / 2;
      //          GenerateDisplayList(colorType);
      //      }
      //      else
      //      {
      //          this.dataIndex = null;
      //          childNode = new PointCloudNode[8];
      //          Vector3d split = (minv + maxv) / 2;
      //          List<int>[] childIndex = new List<int>[8];
      //          int[] counts = new int[8];
      //          byte nextDepth = (byte)(depth + 1);
      //          foreach (var v in dataIndex)
      //          {
      //              int index = GetOctantIndex(data[v].Point, split);
      //              counts[index]++;
      //          }
      //          for (int i = 0; i < 8; i++)
      //          {
      //              if (counts[i] > 0)
      //                  childIndex[i] = new List<int>(counts[i]);
      //          }
      //          foreach (var v in dataIndex)
      //          {
      //              int index = GetOctantIndex(data[v].Point, split);
      //              childIndex[index].Add(v);
      //          }
      //          for (int i = 0; i < 8; i++)
      //          {
      //              if (childIndex[i]?.Count > 0)
      //              {
      //                  Vector3d minva = new Vector3d(
      //                      (i & 1) == 0 ? split.X : minv.X,
      //                      (i & 2) == 0 ? split.Y : minv.Y,
      //                      (i & 4) == 0 ? split.Z : minv.Z
      //                  );
      //                  Vector3d maxva = new Vector3d(
      //                      (i & 1) == 0 ? maxv.X : split.X,
      //                      (i & 2) == 0 ? maxv.Y : split.Y,
      //                      (i & 4) == 0 ? maxv.Z : split.Z
      //                  );
      //                  childNode[i] = new PointCloudNode(nextDepth, ref childIndex[i], ref data, ref minva, ref maxva, pointCloudColorMapper, colorType);
      //              }
      //          }
      //      }
      //  }
    //     public PointCloudNode(byte depth, ref List<int> dataIndex, ref List<ColorPoint> data,
    //   ref Vector3d minv, ref Vector3d maxv, PointCloudColorMapper pointCloudColorMapper, string colorType)
    //     {
    //         // Initialize basic node properties
    //         this.data = data;
    //         this.dataIndex = new List<int>();
    //         this.minCoordinate = minv;
    //         this.maxCoordinate = maxv;
    //         this.pointCloudColorMapper = pointCloudColorMapper;
    //         this.pointCloudColor = colorType;

    //         // Initialize child nodes array
    //         childNode = new PointCloudNode[8]; // Octree has 8 children
    //         for (int i = 0; i < 8; i++)
    //         {
    //             childNode[i] = null;
    //         }
    //     }
        private void GenerateDisplayList(string colorType)
        {
            isLeaf = true;
            displayList = GL.GenLists(1);
            GL.NewList(displayList, ListMode.Compile);
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
            GL.DeleteLists(displayList, 1);
            displayList = 0;
            displayList = GL.GenLists(1);
            GL.NewList(displayList, ListMode.Compile);
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
                GL.CallList(displayList);

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
        public void InsertPointToNode(ColorPoint point)
        {
            if (data == null || data.Count < 1)
            {
                return;   
            }
            if (dataIndex.Count < maxPointPerNode)
            {
                data.Add(point);
                dataIndex.Add(data.Count - 1);
                UpdateDisplayList(pointCloudColor);
            }
            else
            {
                Vector3d split = (minCoordinate + maxCoordinate) / 2;
                int index = GetOctantIndex(point.Point, split);
                if (childNode[index] == null)
                {
                    Vector3d minva = new Vector3d(
                        (index & 1) == 0 ? split.X : minCoordinate.X,
                        (index & 2) == 0 ? split.Y : minCoordinate.Y,
                        (index & 4) == 0 ? split.Z : minCoordinate.Z
                    );
                    Vector3d maxva = new Vector3d(
                        (index & 1) == 0 ? maxCoordinate.X : split.X,
                        (index & 2) == 0 ? maxCoordinate.Y : split.Y,
                        (index & 4) == 0 ? maxCoordinate.Z : split.Z
                    );
                    childNode[index] = new PointCloudNode(0, ref dataIndex, ref data, ref minva, ref maxva, pointCloudColorMapper, pointCloudColor);
                }
                childNode[index].InsertPointToNode(point);
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
        public void InsertPoint(ColorPoint point) {
            if (_root == null) return;
            _root.InsertPointToNode(point);
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