using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PointCloudViewer.src
{
    internal class Camera
    {
        public Vector3 cameraPos { get; set; }
        public Vector3 cameraUp { get; set; }
        public Vector3 cameraRight { get; set; } 
        public Vector3 cameraDir { get; set; }
        public Vector3 worldUp { get; set; }
        public Matrix4 modelViewMatrix { get; set; }
        public float zoom { get; set; }
        public float yaw { get; set; }
        public float pitch { get; set; }
        public Camera(Vector3 cameraPos)
        {
            this.cameraPos = cameraPos;
            SetupCamera();
        }
        public void SetupCamera()
        {
            cameraDir = Vector3.Zero;
            cameraUp = Vector3.UnitY;
            cameraRight = Vector3.UnitY;
            modelViewMatrix = Matrix4.LookAt(cameraPos, cameraDir, cameraUp);
        }
        public void UpdateZoom(float yoffset)
        {
            zoom -= (float)yoffset;
            if (zoom < 1.0f)
            {
                zoom = 1.0f;
            }
            if (zoom > 45.0f)
            {
                zoom = 45.0f;
            }
        }
        public void UpdateCameraVectors()
        {
            Vector3 front = new Vector3();
            front.X = (float)Math.Cos(MathHelper.DegreesToRadians(yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(pitch));
            front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(pitch));
            front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(pitch));
            cameraDir = front;
            cameraRight = Vector3.Normalize(Vector3.Cross(cameraDir, cameraUp));
            cameraUp = Vector3.Normalize(Vector3.Cross(cameraRight, cameraDir));
        }
    }
}