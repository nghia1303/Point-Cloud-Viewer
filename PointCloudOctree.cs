using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms.Design;

namespace PointCloudViewer
{
    internal struct ColorPoint
    {
        public Vector3d point;
        public Vector3d color;
    }

    internal class PointCloudNode
    {
        #region Member Variable

        public const int maxPointPerNode = 10000;
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

        public PointCloudNode(byte depth, ref List<int> dataIndex, ref List<ColorPoint> data,
    ref Vector3d minv, ref Vector3d maxv)
        {
            if (dataIndex == null || data == null || dataIndex.Count == 0 || data.Count == 0) return;
    
            if (depth >= 4)
            {
                this.dataIndex = dataIndex;
                this.data = data;
                minCoordinate = minv;
                maxCoordinate = maxv;
                midPoint = (minv + maxv) / 2;
                isLeaf = true;
                displayList = GL.GenLists(1);
                GL.NewList(displayList, ListMode.Compile);
                GL.Begin(PrimitiveType.Points);
                for (int i = 0; i < dataIndex.Count; i++)
                {
                    GL.Color3(data[dataIndex[i]].color.X, data[dataIndex[i]].color.Y, data[dataIndex[i]].color.Z);
                    GL.Vertex3(data[dataIndex[i]].point.X, data[dataIndex[i]].point.Y, data[dataIndex[i]].point.Z);
                }
                GL.End();
                GL.EndList();
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
                    int index = GetOctantIndex(data[v].point, split);
                    counts[index]++;
                }
                for (int i = 0; i < 8; i++)
                {
                    if (counts[i] > 0)
                        childIndex[i] = new List<int>(counts[i]);
                }
                foreach (var v in dataIndex)
                {
                    int index = GetOctantIndex(data[v].point, split);
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
                        childNode[i] = new PointCloudNode(nextDepth, ref childIndex[i], ref data, ref minva, ref maxva);
                    }
                }
            }
        }

        private void GenerateDisplayList()
        {
            isLeaf = true;
            displayList = GL.GenLists(1);
            GL.NewList(displayList, ListMode.Compile);
            GL.Begin(PrimitiveType.Points);
            foreach (var index in dataIndex)
            {
                GL.Color3(data[index].color.X, data[index].color.Y, data[index].color.Z);
                GL.Vertex3(data[index].point.X, data[index].point.Y, data[index].point.Z);
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
                    distance = CalculateDistance(data[index].point, farPoint, nearPoint);
                    if (closestPoint.Flag > distance)
                    {
                        closestPoint.Point = data[index].point;
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
        private double CalculateSquare(double lenght1, double lenght2, double lenght3)
        {
            double s;
            double half_circumference = (lenght1 + lenght2 + lenght3) / 2;
            double ss = half_circumference * (half_circumference - lenght1)
                * (half_circumference - lenght2) * (half_circumference - lenght3);
            s = Math.Sqrt(ss);
            return s;
        }

        private double CalculateLength(Vector3d point1, Vector3d point2)
        {
            double x = 100 * (point1.X - point2.X);
            double y = 100 * (point1.Y - point2.Y);
            double z = 100 * (point1.Z - point2.Z);
            double ll = x * x + y * y + z * z;
            double l = Math.Sqrt(ll);
            return l;
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
        private PointCloudNode root;
        private List<int> dataIndex;
        public PointCloudOctree(ref List<ColorPoint> data,
            ref Vector3d minv, ref Vector3d maxv)
        {
            if (data == null)
                return;
            if (data.Count == 0)
                return;
            dataIndex = Enumerable.Range(0, data.Count).ToList();
            root = new PointCloudNode(0, ref dataIndex, ref data, ref minv, ref maxv);
        }
        public void Render(int pointSize, bool showTreeNodeOutline, string pointCloudColor, double[,] frustum)
        {
            if (root != null)
                root.Render(pointSize, showTreeNodeOutline, pointCloudColor, frustum);
        }
        public void FindClosestPoint(double[,] frustum, Vector3d nearPoint, Vector3d farPoint,
            ref Point3DExt closestPoint)
        {
            if (root != null)
                root.FindClosestPoint(frustum, nearPoint, farPoint, ref closestPoint);
        }
        public void Dispose()
        {
            root = null;
            dataIndex = null;
        }
    }
}