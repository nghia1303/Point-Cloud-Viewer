using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;
//using static OpenTK.Graphics.OpenGL.GL;

namespace PointCloudViewer.src
{
    internal class Renderer
    {
        private int vboPosition;
        private int vboColor;
        private int vboModelView;
        private Shader shader;
        private Vector3d[] vects { get; set; }
        private Vector3[] cols { get; set; }
        private Matrix4 modelViewMats { get; set; }
        private Camera camera { get; set; }

        public Renderer(ref Vector3d[] vects, ref Vector3[] cols, Shader shader, Camera camera)
        {
            this.shader = shader;
            this.vects = vects;
            this.cols = cols;
            this.camera = camera;
            //this.modelViewMats = modelViewMats;
        }

        public void Init()
        {
            GL.GenBuffers(1, out vboPosition);
            GL.GenBuffers(1, out vboColor);
            GL.GenBuffers(1, out vboModelView);

            var vertexLocation = shader.GetAttribLocation("aPosition");
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vects.Length * Vector3d.SizeInBytes), vects, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Double, false, 0, 0);

            var texLocation = shader.GetAttribLocation("aColor");
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboColor);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(cols.Length * Vector3.SizeInBytes), cols, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(texLocation, 3, VertexAttribPointerType.Float, true, 0, 0);
        }

        public void Draw(Shader shader, GLControl glControl1)
        {

            GL.EnableVertexAttribArray(shader.GetAttribLocation("aPosition"));
            GL.EnableVertexAttribArray(shader.GetAttribLocation("aColor"));

            shader.SetMatrix4("projection", camera.GetProjectionMatrixOrtho(glControl1.Width, glControl1.Height));
            shader.SetMatrix4("view", camera.GetViewMatrix());
            GL.DrawArrays(PrimitiveType.Points, 0, vects.Length);
        }
        public void DrawPoint(ref Vector3d point)
        {
            //GL.EnableVertexAttribArray(shader.GetAttribLocation("aPosition"));
            //GL.EnableVertexAttribArray(shader.GetAttribLocation("aColor"));

            //shader.SetMatrix4("projection", camera.GetProjectionMatrix());
            //shader.SetMatrix4("view", camera.GetViewMatrix());

            //GL.DrawArrays(PrimitiveType.Points, 0, vects.Length);
            GL.Enable(EnableCap.ProgramPointSize);
            GL.PointSize(30);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(point.X, point.Y, point.Z);
            GL.End();
        }

        //public void ReleaseBuffer()
        //{
        //    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        //    GL.DeleteBuffer(vertexBufferObject);
        //}
    }
}