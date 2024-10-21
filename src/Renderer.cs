using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using static OpenTK.Graphics.OpenGL.GL;

namespace PointCloudViewer.src
{
    internal class Renderer
    {
        private int vboPosition;
        private int vboColor;
        private int vboModelView;
        private Vector3d[] vects;
        private Vector3[] cols;
        private Shader shader;
        private Matrix4[] modelViewMats;
        public Renderer(ref Vector3d[] vects, ref Vector3[] cols, ref Matrix4[] modelViewMats, Shader shader)
        {
            this.shader = shader;
            this.vects = vects;
            this.cols = cols;
            this.modelViewMats = modelViewMats;
        }

        public void Init()
        {
            GL.GenBuffers(1, out vboPosition);
            GL.GenBuffers(1, out vboColor);
            GL.GenBuffers(1, out vboModelView);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vects.Length * Vector3d.SizeInBytes), vects, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.attributePos, 3, VertexAttribPointerType.Double, false, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboColor);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(cols.Length * Vector3.SizeInBytes), cols, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shader.attributeCol, 3, VertexAttribPointerType.Float, true, 0, 0);

            GL.UniformMatrix4(shader.uniformModelView, false, ref modelViewMats[0]);
        }

        public void Draw(Shader shader)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.EnableVertexAttribArray(shader.attributePos);
            GL.EnableVertexAttribArray(shader.attributeCol);
            GL.DrawArrays(PrimitiveType.Points, 0, vects.Length);
        }

        //public void ReleaseBuffer()
        //{
        //    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        //    GL.DeleteBuffer(vertexBufferObject);
        //}
    }
}
