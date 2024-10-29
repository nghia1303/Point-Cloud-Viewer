using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;
//using OpenTK.Graphics.ES20;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using OpenTK.Platform;
using PointCloudViewer.src;

namespace PointCloudViewer
{
    internal struct ColorPoint
    {
        public Vector3d point;
        public Vector3d color;
    }

    public class PointCloudNode
    {
        #region Member Variable

        public const int maxPointPerNode = 50000;
        public Vector3d minCoordinate, maxCoordinate;
        public Vector3d midPoint;
        public List<int> dataIndex;
        private Vector3d[] data;
        private PointCloudNode[] childNode;
        private bool OutlineInFrustum;
        private string pointCloudColor;
        private bool isLeaf = false;
        private UInt16 ID = 0;
        private PointCloudNode parentNode;

        #endregion Member Variable
        private void DrawNodeOutline(Vector3d minNodeCoordinate, Vector3d maxNodeCoordinate)
        {
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, minNodeCoordinate.Y, minNodeCoordinate.Z);//(0,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, minNodeCoordinate.Y, minNodeCoordinate.Z);//(1,0,0)

            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, maxNodeCoordinate.Y, minNodeCoordinate.Z);//(0,1,0)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, minNodeCoordinate.Z);//(1,1,0)

            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, minNodeCoordinate.Y, minNodeCoordinate.Z);//(0,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, maxNodeCoordinate.Y, minNodeCoordinate.Z);//(0,1,0)

            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, minNodeCoordinate.Y, minNodeCoordinate.Z);//(1,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, minNodeCoordinate.Z);//(1,1,0)

            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, minNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, minNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,0,1)

            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,1,1)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,1,1)

            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, minNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(0,1,1)

            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, minNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);//(1,1,1)

            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, minNodeCoordinate.Y, minNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, minNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, maxNodeCoordinate.Y, minNodeCoordinate.Z);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, minNodeCoordinate.Y, minNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(maxNodeCoordinate.X, minNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.End();

            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, maxNodeCoordinate.Y, minNodeCoordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(minNodeCoordinate.X, maxNodeCoordinate.Y, maxNodeCoordinate.Z);
            GL.End();
        }
        public PointCloudNode(ref List<int> dataIndex, ref Vector3d[] data,
            Vector3d minv, Vector3d maxv)
        {
            if (dataIndex == null || data == null || dataIndex.Count == 0 || data.Length == 0) return;

            if (dataIndex.Count <= maxPointPerNode)
            {
                this.dataIndex = dataIndex;
                this.data = data;
                minCoordinate = minv;
                maxCoordinate = maxv;
                midPoint = (minv + maxv) / 2;
                this.isLeaf = true;
            }
            else
            {
                this.dataIndex = null;
                childNode = new PointCloudNode[8];
                List<int>[] childIndex = new List<int>[8];
                Vector3d[] minva = new Vector3d[8];
                Vector3d[] maxva = new Vector3d[8];
                Vector3d split = (minv + maxv) / 2;
                for (int i = 0; i < 8; i++)
                {
                    childIndex[i] = new List<int>();
                    minva[i] = new Vector3d();
                    maxva[i] = new Vector3d();
                }

                foreach (var v in dataIndex)
                {
                    int index = 0;
                    if (data[v].Z <= split.Z) index += 4;
                    if (data[v].Y <= split.Y) index += 2;
                    if (data[v].X <= split.X) index += 1;
                    childIndex[index].Add(v);
                }

                for (int i = 0; i < 8; i++)
                {
                    minva[i].X = (i & 1) == 0 ? split.X : minv.X;
                    minva[i].Y = (i & 2) == 0 ? split.Y : minv.Y;
                    minva[i].Z = (i & 4) == 0 ? split.Z : minv.Z;

                    maxva[i].X = (i & 1) == 0 ? maxv.X : split.X;
                    maxva[i].Y = (i & 2) == 0 ? maxv.Y : split.Y;
                    maxva[i].Z = (i & 4) == 0 ? maxv.Z : split.Z;

                    if (childIndex[i].Count > 0)
                    {
                        childNode[i] = new PointCloudNode(ref childIndex[i], ref data, minva[i], maxva[i]);
                    }
                }
            }
        }
        public void Render(int pointSize, bool showTreeNodeOutline, string pointCloudColor, double[,] frustum)
        {
            if (dataIndex != null)
            {
                DrawNodeOutline(minCoordinate, maxCoordinate);
                if (showTreeNodeOutline == true)
                {

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
        public void IsNodeInsideFrustum(ref Frustum frustum, ref Transform transform)
        {
            // Check if the current node is within the frustum
            bool isInsideFrustum = frustum.IsOnFrustum(this, transform);

            // Draw the outline only for leaf nodes that are inside the frustum
            if (isInsideFrustum && isLeaf)
            {
                DrawNodeOutline(minCoordinate, maxCoordinate);
            }

            // Perform the frustum check on all child nodes regardless of the parent's frustum status
            if (childNode != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (childNode[i] != null)
                    {
                        // Recursively check each child node for visibility
                        childNode[i].IsNodeInsideFrustum(ref frustum, ref transform);
                    }
                }
            }
        }
        public bool VoxelWithinFrustum(double[,] ftum, double minx, double miny, double minz,
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
            //if (!OutlineInFrustum && isLeaf)
            //    return;

            Vector3d line;
            line = farPoint - nearPoint;

            if (dataIndex != null)
            {
                double distance;
                foreach (int index in dataIndex)
                {
                    distance = CalculateDistance(data[index], farPoint, nearPoint);

                    if (closestPoint.flag > distance)
                    {
                        closestPoint.point = data[index];
                        closestPoint.flag = distance;
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
        private double CalculateDistance(Vector3d now_point, Vector3d far_point, Vector3d near_point)
        {
            double now_far_distance = CalculateLength(now_point, far_point);
            double now_near_distance = CalculateLength(now_point, near_point);
            double near_far_distance = CalculateLength(near_point, far_point);
            double triangle_square = CalculateSquare(now_far_distance, now_near_distance, near_far_distance);
            double distance = (2 * triangle_square / 10000) / (near_far_distance / 100);
            return distance;
        }
    }

    internal class PointCloudOctree
    {
        private readonly PointCloudNode root;
        private readonly List<int> dataIndex;
        private Vector3d[] boundingBox;
        private Renderer renderer;
        public PointCloudOctree(ref Vector3d[] data,
            Vector3d minv, Vector3d maxv)
        {
            if (data == null)
                return;
            if (data.Length == 0)
                return;
            dataIndex = Enumerable.Range(0, data.Length).ToList();

            double[] vertices = new double[data.Length * 3];
            for (int i = 0; i < data.Length; i++)
            {
                vertices[i] = data[i].X;
                vertices[i + 1] = data[i].Y;
                vertices[i + 2] = data[i].Z;
            }

            root = new PointCloudNode(ref dataIndex, ref data, minv, maxv);
        }
        public PointCloudOctree(ref Vector3d[] data, Renderer renderer, 
            Vector3d minv, Vector3d maxv)
        {
            if (data == null)
                return;
            if (data.Length == 0)
                return;

            this.renderer = renderer;
            dataIndex = Enumerable.Range(0, data.Length).ToList();
            root = new PointCloudNode(ref dataIndex, ref data, minv, maxv);
        }

        //public void Render(Shader shader, int pointSize, bool showTreeNodeOutline, string pointCloudColor, double[,] frustum)
        //{
        //    if (renderer == null) return;
        //    renderer.Draw(shader);
        //}

        public void FindClosestPoint(double[,] frustum, Vector3d nearPoint, Vector3d farPoint,
            ref Point3DExt closestPoint)
        {
            if (root != null)
                root.FindClosestPoint(frustum, nearPoint, farPoint, ref closestPoint);
        }
        public void IsNodeInsideFrustum(ref Frustum frustum, ref Transform transform)
        {
            if (root != null)
            {
                root.IsNodeInsideFrustum(ref frustum, ref transform);
            }
        }
    }
}