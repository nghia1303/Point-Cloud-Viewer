using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTK.Graphics.OpenGL.GL;

namespace PointCloudViewer.src
{
    public class Shader
    {
        private int handle;
        private bool disposedValue = false;
        public int attributePos { get; set; }
        public int attributeCol { get; set; }
        public int uniformModelView { get; set; }

        public Shader(string vertexPath, string fragmentPath)
        {
            string vertexShaderSource;
            string fragmentShaderSource;
            try
            {
                vertexShaderSource = File.ReadAllText(vertexPath);
                fragmentShaderSource = File.ReadAllText(fragmentPath);

                CompileVertexShader(out int vertexShader, ref vertexShaderSource);
                CompileFragmentShader(out int fragmentShader, ref fragmentShaderSource);

                handle = GL.CreateProgram();
                GL.AttachShader(handle, vertexShader);
                GL.AttachShader(handle, fragmentShader);

                GL.LinkProgram(handle);
                GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out int success);
                if (success == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(handle);
                    Console.WriteLine(infoLog);
                }

                attributePos = GL.GetAttribLocation(handle, "aPosition");
                attributeCol = GL.GetAttribLocation(handle, "aColor");
                uniformModelView = GL.GetUniformLocation(handle, "modelView");
                if (attributePos == -1 || attributeCol == -1 || uniformModelView == -1)
                {
                    Console.WriteLine("Error binding attributes");
                }

                GL.DetachShader(handle, vertexShader);
                GL.DetachShader(handle, fragmentShader);
                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        protected void CompileVertexShader(out int vertexShader, ref string vertexShaderSource)
        {
            vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);

            GL.CompileShader(vertexShader);
            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(vertexShader);
                Console.WriteLine(infoLog);
            }
        }

        protected void CompileFragmentShader(out int fragmentShader, ref string fragmentShaderSource)
        {
            fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);

            GL.CompileShader(fragmentShader);
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(fragmentShader);
                Console.WriteLine(infoLog);
            }
        }

        public void Use()
        {
            GL.UseProgram(handle);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(handle);
                disposedValue = true;
            }
        }

        ~Shader()
        {
            if (disposedValue == false)
            {
                Console.WriteLine("GPU Resource leak! Did you forget to call Dispose()?");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}