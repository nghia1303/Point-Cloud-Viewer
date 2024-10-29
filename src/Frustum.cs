using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PointCloudViewer.src
{
    public class BoundingVolume
    {
        public virtual bool IsOnFrustum(ref Frustum frustum, ref Transform transform)
        { return false; }

        public virtual bool IsOnOrForwardPlane(ref Plane plane)
        { return false; }

        public bool IsOnFrustum(ref Frustum camFrustum)
        {
            return (IsOnOrForwardPlane(ref camFrustum.LeftFace) &&
                IsOnOrForwardPlane(ref camFrustum.RightFace) &&
                IsOnOrForwardPlane(ref camFrustum.TopFace) &&
                IsOnOrForwardPlane(ref camFrustum.BottomFace) &&
                IsOnOrForwardPlane(ref camFrustum.NearFace) &&
                IsOnOrForwardPlane(ref camFrustum.FarFace));
        }
    }
    public class AABB : BoundingVolume
    {
        private Vector3 center;
        private Vector3 extents;

        public AABB(Vector3 min, Vector3 max)
        {
            center = (max + min) * 0.5f;
            extents = new Vector3(max.X - center.X, max.Y - center.Y, max.Z - center.Z);
        }

        public AABB(Vector3 inCenter, float iI, float iJ, float iK)
        {
            center = inCenter;
            extents = new Vector3(iI, iJ, iK);
        }

        public Vector3[] GetVertices()
        {
            return new Vector3[]
            {
                new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z - extents.Z),
                new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z - extents.Z),
                new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z - extents.Z),
                new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z - extents.Z),
                new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z + extents.Z),
                new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z + extents.Z),
                new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z + extents.Z),
                new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z + extents.Z),
            };
        }

        public bool IsOnOrForwardPlane(Plane plane)
        {
            float r = extents.X * Math.Abs(plane.Normal.X) + extents.Y * Math.Abs(plane.Normal.Y) + extents.Z * Math.Abs(plane.Normal.Z);
            return -r <= plane.GetSignedDistanceToPlane(center);
        }

        public bool IsOnFrustum(Frustum camFrustum, Transform transform)
        {
            Vector3 globalCenter = Vector3.TransformPerspective(center, transform.GetModelMatrix());

            Vector3 right = transform.GetRight() * extents.X;
            Vector3 up = transform.GetUp() * extents.Y;
            Vector3 forward = transform.GetForward() * extents.Z;

            float newIi = Math.Abs(Vector3.Dot(new Vector3(1f, 0f, 0f), right)) +
                          Math.Abs(Vector3.Dot(new Vector3(1f, 0f, 0f), up)) +
                          Math.Abs(Vector3.Dot(new Vector3(1f, 0f, 0f), forward));

            float newIj = Math.Abs(Vector3.Dot(new Vector3(0f, 1f, 0f), right)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 1f, 0f), up)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 1f, 0f), forward));

            float newIk = Math.Abs(Vector3.Dot(new Vector3(0f, 0f, 1f), right)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 0f, 1f), up)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 0f, 1f), forward));

            AABB globalAABB = new AABB(globalCenter, newIi, newIj, newIk);

            return (globalAABB.IsOnOrForwardPlane(camFrustum.LeftFace) &&
                    globalAABB.IsOnOrForwardPlane(camFrustum.RightFace) &&
                    globalAABB.IsOnOrForwardPlane(camFrustum.TopFace) &&
                    globalAABB.IsOnOrForwardPlane(camFrustum.BottomFace) &&
                    globalAABB.IsOnOrForwardPlane(camFrustum.NearFace) &&
                    globalAABB.IsOnOrForwardPlane(camFrustum.FarFace));
        }
    }
    public struct Plane
    {
        public Vector3 Normal { get; set; }
        public Vector3 P1 { get; set; }
        float Distance { get; set; }

        public Plane(Vector3 normal, Vector3 p1)
        {
            this.Normal = normal;
            this.P1 = p1;
            this.Distance = Vector3.Dot(normal, p1);
        }

        public double GetSignedDistanceToPlane(Vector3 point)
        {
            return Vector3.Dot(Normal, point) - Distance;
        }
    }

    public struct Frustum
    {
        public Plane TopFace;
        public Plane BottomFace;

        public Plane RightFace;
        public Plane LeftFace;

        public Plane FarFace;
        public Plane NearFace;

        public static Frustum CreateFrustumFromCamera(ref Camera cam, float aspect, float fovY, float zNear, float zFar)
        {
            Frustum frustum;

            float halfVSide = zFar * (float)Math.Tan(fovY * 0.5f);
            float halfHSide = halfVSide * aspect;
            Vector3 frontMultFar = zFar * cam.Front;

            frustum.NearFace = new Plane(cam.Position + zNear * cam.Front, cam.Front);
            frustum.FarFace = new Plane(cam.Position + frontMultFar, -cam.Front);

            frustum.RightFace = new Plane(cam.Position, Vector3.Cross(frontMultFar - cam.Right * halfHSide, cam.Up));
            frustum.LeftFace = new Plane(cam.Position, Vector3.Cross(cam.Up, frontMultFar + cam.Right * halfHSide));

            frustum.TopFace = new Plane(cam.Position, Vector3.Cross(cam.Right, frontMultFar - cam.Up * halfVSide));
            frustum.BottomFace = new Plane(cam.Position, Vector3.Cross(frontMultFar + cam.Up * halfVSide, cam.Right));

            DrawFrustumLines(cam, aspect, fovY, zNear, zFar);

            return frustum;
        }

        public static void DrawFrustumLines(Camera cam, float aspect, float fovY, float zNear, float zFar, float normalLength = 1.0f)
        {
            // Calculate frustum corners
            float halfVSide = zFar * (float)Math.Tan(fovY * 0.5f);
            float halfHSide = halfVSide * aspect;

            // Calculate the 8 corners of the frustum
            Vector3 nearCenter = cam.Position + zNear * cam.Front;
            Vector3 farCenter = cam.Position + zFar * cam.Front;

            // Near face corners
            Vector3 ntl = nearCenter + (-cam.Right * halfHSide * (zNear / zFar)) + (cam.Up * halfVSide * (zNear / zFar));
            Vector3 ntr = nearCenter + (cam.Right * halfHSide * (zNear / zFar)) + (cam.Up * halfVSide * (zNear / zFar));
            Vector3 nbl = nearCenter + (-cam.Right * halfHSide * (zNear / zFar)) + (-cam.Up * halfVSide * (zNear / zFar));
            Vector3 nbr = nearCenter + (cam.Right * halfHSide * (zNear / zFar)) + (-cam.Up * halfVSide * (zNear / zFar));

            // Far face corners
            Vector3 ftl = farCenter + (-cam.Right * halfHSide) + (cam.Up * halfVSide);
            Vector3 ftr = farCenter + (cam.Right * halfHSide) + (cam.Up * halfVSide);
            Vector3 fbl = farCenter + (-cam.Right * halfHSide) + (-cam.Up * halfVSide);
            Vector3 fbr = farCenter + (cam.Right * halfHSide) + (-cam.Up * halfVSide);

            // Calculate face normals
            Vector3 nearNormal = cam.Front;  // Points in front direction
            Vector3 farNormal = -cam.Front;  // Points in opposite direction
            Vector3 rightNormal = Vector3.Normalize(Vector3.Cross(farCenter - nearCenter - cam.Right * halfHSide, cam.Up));
            Vector3 leftNormal = -rightNormal;
            Vector3 topNormal = Vector3.Normalize(Vector3.Cross(cam.Right, farCenter - nearCenter - cam.Up * halfVSide));
            Vector3 bottomNormal = -topNormal;

            GL.Begin(PrimitiveType.Lines);

            // Draw frustum edges
            GL.Color3(Color.White);
            // Near face
            DrawLine(ntl, ntr);
            DrawLine(ntr, nbr);
            DrawLine(nbr, nbl);
            DrawLine(nbl, ntl);

            // Far face
            DrawLine(ftl, ftr);
            DrawLine(ftr, fbr);
            DrawLine(fbr, fbl);
            DrawLine(fbl, ftl);

            // Connecting lines
            DrawLine(ntl, ftl);
            DrawLine(ntr, ftr);
            DrawLine(nbr, fbr);
            DrawLine(nbl, fbl);

            GL.End();

            // Draw normals
            GL.Begin(PrimitiveType.Lines);

            // Near face Normal (Yellow)
            GL.Color3(Color.Yellow);
            Vector3 nearCenter2 = (ntl + ntr + nbl + nbr) / 4f;
            DrawNormal(nearCenter2, nearNormal, normalLength);

            // Far face Normal (Orange)
            GL.Color3(Color.Orange);
            Vector3 farCenter2 = (ftl + ftr + fbl + fbr) / 4f;
            DrawNormal(farCenter2, farNormal, normalLength);

            // Right face Normal (Blue)
            GL.Color3(Color.Blue);
            Vector3 rightCenter = (ntr + nbr + ftr + fbr) / 4f;
            DrawNormal(rightCenter, rightNormal, normalLength);

            // Left face Normal (Red)
            GL.Color3(Color.Red);
            Vector3 leftCenter = (ntl + nbl + ftl + fbl) / 4f;
            DrawNormal(leftCenter, leftNormal, normalLength);

            // Top face Normal (Green)
            GL.Color3(Color.Green);
            Vector3 topCenter = (ntl + ntr + ftl + ftr) / 4f;
            DrawNormal(topCenter, topNormal, normalLength);

            // Bottom face Normal (Purple)
            GL.Color3(Color.Purple);
            Vector3 bottomCenter = (nbl + nbr + fbl + fbr) / 4f;
            DrawNormal(bottomCenter, bottomNormal, normalLength);

            GL.End();
        }

        private static void DrawLine(Vector3 start, Vector3 end)
        {
            GL.Vertex3(start);
            GL.Vertex3(end);
        }

        private static void DrawNormal(Vector3 center, Vector3 normal, float length)
        {
            Vector3 end = center + normal * length;
            DrawLine(center, end);

            // Optional: Draw a small arrow head at the end of the Normal
            float arrowSize = length * 0.1f;
            Vector3 right = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY)) * arrowSize;
            Vector3 up = Vector3.Normalize(Vector3.Cross(normal, right)) * arrowSize;

            DrawLine(end, end - normal * arrowSize + right);
            DrawLine(end, end - normal * arrowSize - right);
            DrawLine(end, end - normal * arrowSize + up);
            DrawLine(end, end - normal * arrowSize - up);
        }

        private bool IsNodeOnOrForwardPlane(PointCloudNode treeNode, Plane plane)
        {
            Vector3d extents = treeNode.maxCoordinate - treeNode.midPoint;
            // Compute the projection interval radius of b onto L(t) = b.c + t * p.n
            double r = extents.X * Math.Abs(plane.Normal.X) +
                    extents.Y * Math.Abs(plane.Normal.Y) + extents.Z * Math.Abs(plane.Normal.Z);

            return -r <= plane.GetSignedDistanceToPlane((Vector3)treeNode.midPoint);
        }

        public bool IsOnFrustum(PointCloudNode treeNode, Transform transform)
        {
            Vector3 globalCenter = Vector3.TransformPerspective((Vector3)treeNode.midPoint, transform.GetModelMatrix());

            Vector3 extents = (Vector3)(treeNode.maxCoordinate - treeNode.midPoint);
            Vector3 right = transform.GetRight() * extents.X;
            Vector3 up = transform.GetUp() * extents.Y;
            Vector3 forward = transform.GetForward() * extents.Z;

            float newIi = Math.Abs(Vector3.Dot(new Vector3(1f, 0f, 0f), right)) +
                          Math.Abs(Vector3.Dot(new Vector3(1f, 0f, 0f), up)) +
                          Math.Abs(Vector3.Dot(new Vector3(1f, 0f, 0f), forward));

            float newIj = Math.Abs(Vector3.Dot(new Vector3(0f, 1f, 0f), right)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 1f, 0f), up)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 1f, 0f), forward));

            float newIk = Math.Abs(Vector3.Dot(new Vector3(0f, 0f, 1f), right)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 0f, 1f), up)) +
                          Math.Abs(Vector3.Dot(new Vector3(0f, 0f, 1f), forward));

            return (IsNodeOnOrForwardPlane(treeNode, this.LeftFace) &&
            IsNodeOnOrForwardPlane(treeNode, this.RightFace) &&
            IsNodeOnOrForwardPlane(treeNode, this.TopFace) &&
            IsNodeOnOrForwardPlane(treeNode, this.BottomFace) &&
            IsNodeOnOrForwardPlane(treeNode, this.NearFace) &&
            IsNodeOnOrForwardPlane(treeNode, this.FarFace));
        }
    }
}