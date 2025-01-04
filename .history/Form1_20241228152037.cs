using laszip.net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using PointCloudViewer.service;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PointCloudViewer
{
    internal struct Point3DExt
    {
        public Vector3d Point;
        public double Flag;
    }

    public partial class Form1 : Form
    {
        #region Global Variable

        private bool isOpenFile = false;
        private bool bOpenGLInitial = false;
        private int pointSize = 2;
        private double transX = 0;
        private double transY = 0;
        private double angleX = 0;
        private double angleY = 0;
        private float scaling = 1.0f;
        private Vector3d minv;
        private Vector3d maxv;
        private string primtiveObject = "las";
        private bool showTreeNodeOutline = false;
        private double[,] frustum = new double[6, 4];
        private bool bLeftButtonPushed = false;
        private bool bRightButtonPushed = false;
        private Point leftButtonPosition;
        private Point RightButtonPosition;
        private PointCloudOctree pco;
        private string pointCloudColor = "RGB";
        private int numTwoPoints = -1;
        private float fov = (float)Math.PI / 3.0f;
        private bool perspectiveProjection = false;
        private bool hasLas = false;
        private bool isCutPoint = false;
        private Vector3d mousePos = Vector3d.Zero;
        private List<ColorPoint> points;
        private Point3DExt pivotPoint;
        private List<Vector3d> vertices;
        private ResourceManager resourceManager;
        private PointCloudColorMapper colorMapper;
        private Render render;

        #endregion Global Variable

        #region Window Functions

        public Form1()
        {
            InitializeComponent();
            glControl1.MouseWheel += new MouseEventHandler(glControl1_MouseWheel);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitialGL();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Render(pointSize, showTreeNodeOutline, pointCloudColor);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (bOpenGLInitial)
            {
                SetupViewport();
                Invalidate();
            }
        }

        #endregion Window Functions

        private void InitialGL()
        {
            GL.ShadeModel(ShadingModel.Flat);
            glControl1.VSync = true;
            GL.ClearColor(Color.Black);
            GL.ClearDepth(1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.StencilTest);
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.SampleCoverage);
            GL.Enable(EnableCap.PrimitiveRestartFixedIndex);
            SetupViewport();
            bOpenGLInitial = true;
        }

        private void SetupViewport()
        {
            int w = glControl1.ClientSize.Width;
            int h = glControl1.ClientSize.Height;

            GL.Viewport(0, 0, w, h);

            double aspect = w / (double)h;
            Matrix4 projection;
            {
                float n = scaling;
                float left = -n * 0.5f, right = n * 0.5f, down = (float)(-n * 0.5f / aspect), up = (float)(n * 0.5f / aspect);
                if (w <= h)
                    projection = Matrix4.CreateOrthographicOffCenter(-1, 1, (float)-aspect, (float)aspect, -10, 100);
                else
                    projection = Matrix4.CreateOrthographicOffCenter(left, right, down, up, -10.0f, 10.0f);
            }
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref projection);
        }

        private Matrix4d modelViewMatrix = Matrix4d.Identity;
        private Matrix4d modelMatrix = Matrix4d.Identity;
        private float currentYaw = 0;
        private float currentPitch = 0;
        private float previousYaw = 0;
        private float previousPitch = 0;
        private float offsetYaw = 0;
        private float offsetPitch = 0;

        private float currentTransX = 0;
        private float currentTransY = 0;
        private float previousTransX = 0;
        private float previousTransY = 0;
        private float offsetTransX = 0;
        private float offsetTransY = 0;

        private void Render(int pointSize, bool ShowOctreeOutline, string pointCloudColor)
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.MatrixMode(MatrixMode.Modelview);

            currentYaw = previousYaw + offsetYaw;
            currentPitch = previousPitch + offsetPitch;
            currentTransX = previousTransX + offsetTransX;
            currentTransY = previousTransY + offsetTransY;

            GL.LoadMatrix(ref modelViewMatrix);

            Vector4d transformPivot = Vector4d.Transform(new Vector4d(pivotPoint.Point, 1.0), modelMatrix);
            Vector3d newPivot = new Vector3d(transformPivot.X, transformPivot.Y, transformPivot.Z);

            Matrix4d rotationMatrix = Matrix4d.CreateRotationX(MathHelper.DegreesToRadians(-offsetPitch)) *
                                      Matrix4d.CreateRotationY(MathHelper.DegreesToRadians(offsetYaw));
            Matrix4d translateToPoint = Matrix4d.CreateTranslation(newPivot);
            Matrix4d translateFromPoint = Matrix4d.CreateTranslation(-newPivot);
            Matrix4d transformation = translateFromPoint * rotationMatrix * translateToPoint;

            modelMatrix *= transformation;
            modelMatrix *= Matrix4d.CreateTranslation(offsetTransX, offsetTransY, 0);

            modelViewMatrix = modelMatrix;

            previousPitch = currentPitch;
            previousYaw = currentYaw;
            previousTransX = currentTransX;
            previousTransY = currentTransY;

            CalculateFrustum();
            if (pco != null)
            {
                SetupViewport();
                pco.Render(pointSize, ShowOctreeOutline, pointCloudColor, frustum);
            }
            if (pivotPoint.Point != Vector3d.Zero && pivotPoint.Flag != 10000)
            {
                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.ProgramPointSize);
                GL.PointSize(30);
                GL.Begin(PrimitiveType.Points);
                GL.Color4(Color.FromArgb(255, Color.Red));
                GL.Vertex3(pivotPoint.Point);
                GL.End();
                GL.Enable(EnableCap.DepthTest);
            }
            if (cb_CutPoint.Checked)
            {
                if (vertices == null || vertices.Count < 0) return;
                GL.Disable(EnableCap.DepthTest);
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (i > 0)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(vertices[i]);
                        GL.Vertex3(vertices[i - 1]);
                        GL.End();
                    }
                    if (i < 2 && vertices.Count <= 3)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(vertices[vertices.Count - 1]);
                        GL.Vertex3(mousePos);
                        GL.End();
                    }
                    if (i == 2 && vertices.Count <= 3)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(mousePos);
                        GL.Vertex3(vertices[0]);
                        GL.End();
                    }
                    if (i == 3)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(vertices[i]);
                        GL.Vertex3(vertices[0]);
                        GL.End();
                    }
                }
                GL.Enable(EnableCap.DepthTest);
            }
            glControl1.SwapBuffers();
        }

        public void CalculateFrustum()
        {
            Matrix4 projectionMatrix = new Matrix4();
            GL.GetFloat(GetPName.ProjectionMatrix, out projectionMatrix);
            Matrix4 modelViewMatrix = new Matrix4();
            GL.GetFloat(GetPName.ModelviewMatrix, out modelViewMatrix);

            float[] _clipMatrix = new float[16];
            const int RIGHT = 0, LEFT = 1, BOTTOM = 2, TOP = 3, BACK = 4, FRONT = 5;

            _clipMatrix[0] = (modelViewMatrix.M11 * projectionMatrix.M11)
                + (modelViewMatrix.M12 * projectionMatrix.M21) + (modelViewMatrix.M13 * projectionMatrix.M31)
                + (modelViewMatrix.M14 * projectionMatrix.M41);
            _clipMatrix[1] = (modelViewMatrix.M11 * projectionMatrix.M12)
                + (modelViewMatrix.M12 * projectionMatrix.M22) + (modelViewMatrix.M13 * projectionMatrix.M32)
                + (modelViewMatrix.M14 * projectionMatrix.M42);
            _clipMatrix[2] = (modelViewMatrix.M11 * projectionMatrix.M13)
                + (modelViewMatrix.M12 * projectionMatrix.M23) + (modelViewMatrix.M13 * projectionMatrix.M33)
                + (modelViewMatrix.M14 * projectionMatrix.M43);
            _clipMatrix[3] = (modelViewMatrix.M11 * projectionMatrix.M14)
                + (modelViewMatrix.M12 * projectionMatrix.M24) + (modelViewMatrix.M13 * projectionMatrix.M34)
                + (modelViewMatrix.M14 * projectionMatrix.M44);

            _clipMatrix[4] = (modelViewMatrix.M21 * projectionMatrix.M11)
                + (modelViewMatrix.M22 * projectionMatrix.M21) + (modelViewMatrix.M23 * projectionMatrix.M31)
                + (modelViewMatrix.M24 * projectionMatrix.M41);
            _clipMatrix[5] = (modelViewMatrix.M21 * projectionMatrix.M12)
                + (modelViewMatrix.M22 * projectionMatrix.M22) + (modelViewMatrix.M23 * projectionMatrix.M32)
                + (modelViewMatrix.M24 * projectionMatrix.M42);
            _clipMatrix[6] = (modelViewMatrix.M21 * projectionMatrix.M13)
                + (modelViewMatrix.M22 * projectionMatrix.M23) + (modelViewMatrix.M23 * projectionMatrix.M33)
                + (modelViewMatrix.M24 * projectionMatrix.M43);
            _clipMatrix[7] = (modelViewMatrix.M21 * projectionMatrix.M14)
                + (modelViewMatrix.M22 * projectionMatrix.M24) + (modelViewMatrix.M23 * projectionMatrix.M34)
                + (modelViewMatrix.M24 * projectionMatrix.M44);

            _clipMatrix[8] = (modelViewMatrix.M31 * projectionMatrix.M11)
                + (modelViewMatrix.M32 * projectionMatrix.M21) + (modelViewMatrix.M33 * projectionMatrix.M31)
                + (modelViewMatrix.M34 * projectionMatrix.M41);
            _clipMatrix[9] = (modelViewMatrix.M31 * projectionMatrix.M12)
                + (modelViewMatrix.M32 * projectionMatrix.M22) + (modelViewMatrix.M33 * projectionMatrix.M32)
                + (modelViewMatrix.M34 * projectionMatrix.M42);
            _clipMatrix[10] = (modelViewMatrix.M31 * projectionMatrix.M13)
                + (modelViewMatrix.M32 * projectionMatrix.M23) + (modelViewMatrix.M33 * projectionMatrix.M33)
                + (modelViewMatrix.M34 * projectionMatrix.M43);
            _clipMatrix[11] = (modelViewMatrix.M31 * projectionMatrix.M14)
                + (modelViewMatrix.M32 * projectionMatrix.M24) + (modelViewMatrix.M33 * projectionMatrix.M34)
                + (modelViewMatrix.M34 * projectionMatrix.M44);

            _clipMatrix[12] = (modelViewMatrix.M41 * projectionMatrix.M11)
                + (modelViewMatrix.M42 * projectionMatrix.M21) + (modelViewMatrix.M43 * projectionMatrix.M31)
                + (modelViewMatrix.M44 * projectionMatrix.M41);
            _clipMatrix[13] = (modelViewMatrix.M41 * projectionMatrix.M12)
                + (modelViewMatrix.M42 * projectionMatrix.M22) + (modelViewMatrix.M43 * projectionMatrix.M32)
                + (modelViewMatrix.M44 * projectionMatrix.M42);
            _clipMatrix[14] = (modelViewMatrix.M41 * projectionMatrix.M13)
                + (modelViewMatrix.M42 * projectionMatrix.M23) + (modelViewMatrix.M43 * projectionMatrix.M33)
                + (modelViewMatrix.M44 * projectionMatrix.M43);
            _clipMatrix[15] = (modelViewMatrix.M41 * projectionMatrix.M14)
                + (modelViewMatrix.M42 * projectionMatrix.M24) + (modelViewMatrix.M43 * projectionMatrix.M34)
                + (modelViewMatrix.M44 * projectionMatrix.M44);

            frustum[RIGHT, 0] = _clipMatrix[3] - _clipMatrix[0];
            frustum[RIGHT, 1] = _clipMatrix[7] - _clipMatrix[4];
            frustum[RIGHT, 2] = _clipMatrix[11] - _clipMatrix[8];
            frustum[RIGHT, 3] = _clipMatrix[15] - _clipMatrix[12];
            NormalizePlane(frustum, RIGHT);

            frustum[LEFT, 0] = _clipMatrix[3] + _clipMatrix[0];
            frustum[LEFT, 1] = _clipMatrix[7] + _clipMatrix[4];
            frustum[LEFT, 2] = _clipMatrix[11] + _clipMatrix[8];
            frustum[LEFT, 3] = _clipMatrix[15] + _clipMatrix[12];
            NormalizePlane(frustum, LEFT);

            frustum[BOTTOM, 0] = _clipMatrix[3] + _clipMatrix[1];
            frustum[BOTTOM, 1] = _clipMatrix[7] + _clipMatrix[5];
            frustum[BOTTOM, 2] = _clipMatrix[11] + _clipMatrix[9];
            frustum[BOTTOM, 3] = _clipMatrix[15] + _clipMatrix[13];
            NormalizePlane(frustum, BOTTOM);

            frustum[TOP, 0] = _clipMatrix[3] - _clipMatrix[1];
            frustum[TOP, 1] = _clipMatrix[7] - _clipMatrix[5];
            frustum[TOP, 2] = _clipMatrix[11] - _clipMatrix[9];
            frustum[TOP, 3] = _clipMatrix[15] - _clipMatrix[13];
            NormalizePlane(frustum, TOP);

            frustum[BACK, 0] = _clipMatrix[3] - _clipMatrix[2];
            frustum[BACK, 1] = _clipMatrix[7] - _clipMatrix[6];
            frustum[BACK, 2] = _clipMatrix[11] - _clipMatrix[10];
            frustum[BACK, 3] = _clipMatrix[15] - _clipMatrix[14];
            NormalizePlane(frustum, BACK);

            frustum[FRONT, 0] = _clipMatrix[3] + _clipMatrix[2];
            frustum[FRONT, 1] = _clipMatrix[7] + _clipMatrix[6];
            frustum[FRONT, 2] = _clipMatrix[11] + _clipMatrix[10];
            frustum[FRONT, 3] = _clipMatrix[15] + _clipMatrix[14];
            NormalizePlane(frustum, FRONT);
        }

        private void NormalizePlane(double[,] frustum, int side)
        {
            double magnitude = Math.Sqrt((frustum[side, 0] * frustum[side, 0]) +
           (frustum[side, 1] * frustum[side, 1]) + (frustum[side, 2] * frustum[side, 2]));
            frustum[side, 0] /= magnitude;
            frustum[side, 1] /= magnitude;
            frustum[side, 2] /= magnitude;
            frustum[side, 3] /= magnitude;
        }

        #region Mouse Events

        private void glControl1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bLeftButtonPushed = true;
                leftButtonPosition = e.Location;
            }
            else if (e.Button == MouseButtons.Right)
            {
                bRightButtonPushed = true;
                RightButtonPosition = e.Location;

                if (pco == null)
                    return;

                Point ptClicked = e.Location;

                Vector3d winxyz;
                winxyz.X = ptClicked.X;
                winxyz.Y = ptClicked.Y;
                winxyz.Z = 0.0f;

                Vector3d nearPoint = new Vector3d(0, 0, 0);
                UnProject(winxyz, ref nearPoint);
                winxyz.Z = 1.0f;
                Vector3d farPoint = new Vector3d(0, 0, 0);
                UnProject(winxyz, ref farPoint);

                pivotPoint = new Point3DExt();
                pivotPoint.Flag = 10000;

                pco.FindClosestPoint(frustum, nearPoint, farPoint, ref pivotPoint);
            }
            Invalidate();
        }

        private void glControl1_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            bLeftButtonPushed = false;
            bRightButtonPushed = false;
            offsetXPos = 0;
            offsetYPos = 0;
            offsetYaw = 0;
            offsetPitch = 0;
            offsetTransX = 0;
            offsetTransY = 0;
            pivotPoint = new Point3DExt();
            pivotPoint.Flag = 10000;
        }

        private float offsetXPos = 0;
        private float offsetYPos = 0;
        private float prevXPos = 0;
        private float prevYPos = 0;

        private void glControl1_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (bLeftButtonPushed && !cb_CutPoint.Checked)
            {
                float aspect = (float)glControl1.Width / (float)glControl1.Height;
                float deltaX = (e.Location.X - leftButtonPosition.X) / (float)glControl1.Width;
                float deltaY = (e.Location.Y - leftButtonPosition.Y) / (float)glControl1.Height;
                transX += deltaX * scaling;
                transY -= deltaY * scaling / aspect;
                offsetXPos = e.Location.X - leftButtonPosition.X;
                offsetYPos = e.Location.Y - leftButtonPosition.Y;
                offsetTransX = (float)(transX - previousTransX);
                offsetTransY = (float)(transY - previousTransY);
                leftButtonPosition = e.Location;
            }
            if (bRightButtonPushed && !cb_CutPoint.Checked)
            {
                angleX += (e.Location.X - RightButtonPosition.X) / 10.0;
                angleY += -(e.Location.Y - RightButtonPosition.Y) / 10.0;
                offsetYaw = (float)(angleX - previousYaw);
                offsetPitch = (float)(angleY - previousPitch);

                RightButtonPosition = e.Location;
            }
            if (cb_CutPoint.Checked)
            {
                Vector3d winxyz = new Vector3d();
                winxyz.X = e.Location.X;
                winxyz.Y = e.Location.Y;
                winxyz.Z = 0.3f;
                UnProject(winxyz, ref mousePos);
            }
            Invalidate();
        }

        private void glControl1_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                scaling -= 0.1f;
            }
            else if (e.Delta < 0)
            {
                scaling += 0.1f;
            }

            if (scaling <= 0)
            {
                scaling += 0.1f;
            }
            else if (scaling > 2.0)
            {
                scaling -= 0.1f;
            }
            SetupViewport();
            Invalidate();
        }

        private void glControl1_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (pco == null)
                return;

            if (cb_CutPoint.Checked)
            {
                if (vertices == null)
                {
                    vertices = new List<Vector3d>();
                }
                if (vertices.Count == 4)
                {
                    DialogResult dialogResult = MessageBox.Show("Bạn có chắc chắn với vùng chọn?", "Warning", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        points = CutPoint.FindProjection(vertices, glControl1, points);
                        pco.Dispose();
                        vertices.Clear();
                        pco = new PointCloudOctree(ref points, ref minv, ref maxv, colorMapper);
                        GC.Collect();
                        Invalidate();
                        return;
                    }
                    else
                    {
                        vertices.Clear();
                        Invalidate();
                        return;
                    }
                }
                else if (offsetXPos == 0 && offsetYPos == 0)
                {
                    if (bLeftButtonPushed)
                    {
                        Vector3d winxyz;
                        winxyz.X = e.Location.X;
                        winxyz.Y = e.Location.Y;
                        winxyz.Z = 0.3f;
                        Vector3d vertex = new Vector3d(0, 0, 0);
                        UnProject(winxyz, ref vertex);
                        vertices.Add(vertex);
                    }
                    else
                    {
                        vertices.Clear();
                    }
                }
            }
            Render(pointSize, showTreeNodeOutline, pointCloudColor);
            Invalidate();
        }

        #endregion Mouse Events

        #region Open Las File

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isOpenFile)
            {
                MessageBox.Show("Đang có một tệp LAS được mở!");
                return;
            }
            OpenFileDialog pOpenFileDialog = new OpenFileDialog();
            pOpenFileDialog.Title = "Mở tệp LAS";
            pOpenFileDialog.Filter = "las *.las |*.las";
            pOpenFileDialog.CheckFileExists = true;
            if (pOpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    resourceManager = new ResourceManager(ref points);
                    Stopwatch sw = Stopwatch.StartNew();
                    pco = ReadLas(pOpenFileDialog.FileName);
                    isOpenFile = true;
                    sw.Stop();
                    MessageBox.Show("Thời gian nạp tệp: " + sw.ElapsedMilliseconds + " ms");
                    Invalidate();
                }
                catch (Exception ex)
                {
                    StringBuilder sb = new StringBuilder("Không thể đọc dữ liệu từ tệp LAS. Vui lòng kiểm tra tệp và thử lại.\n");
                    MessageBox.Show(sb.ToString() + ": " + ex.Message.ToString());
                    return;
                }
            }
        }

        private PointCloudOctree ReadLas(string fileName)
        {
            var lazReader = new laszip_dll();
            var compressed = true;
            lazReader.laszip_open_reader(fileName, ref compressed);

            var numberOfPoints = lazReader.header.number_of_point_records;
            progressBar1.Maximum = 100;
            progressBar1.Visible = true;
            double minx = lazReader.header.min_x;
            double miny = lazReader.header.min_y;
            double minz = lazReader.header.min_z;
            double maxx = lazReader.header.max_x;
            double maxy = lazReader.header.max_y;
            double maxz = lazReader.header.max_z;

            double xScaleFactor = lazReader.header.x_scale_factor;
            double yScaleFactor = lazReader.header.y_scale_factor;
            double zScaleFactor = lazReader.header.z_scale_factor;

            resourceManager.ScaleFactor = new Vector3d(xScaleFactor, yScaleFactor, zScaleFactor);

            double centx = (minx + maxx) / 2;
            double centy = (miny + maxy) / 2;
            double centz = (minz + maxz) / 2;

            double scale = Math.Max(Math.Max(maxx - minx, maxy - miny), (maxz - minz));
            resourceManager.Scale = scale;
            byte classification = 0;
            var coordArray = new double[3];
            var colorArray = new double[3];
            ColorPoint colorPoint = new ColorPoint();
            points = new List<ColorPoint>(10000);
            minv.X = (minx - centx) / scale;
            minv.Y = (miny - centy) / scale;
            minv.Z = (minz - centz) / scale;
            maxv.X = (maxx - centx) / scale;
            maxv.Y = (maxy - centy) / scale;
            maxv.Z = (maxz - centz) / scale;
            colorMapper = new PointCloudColorMapper(minv.Z, maxv.Z);

            resourceManager.LasHeader = lazReader.header; // Add this line to set the LASHeader attribute
            PointCloudOctree pco = new PointCloudOctree(ref minv, ref maxv, colorMapper);
            for (int pointIndex = 0; pointIndex < numberOfPoints; pointIndex++)
            {
                lazReader.laszip_read_point();
                lazReader.laszip_get_coordinates(coordArray);
                if (pointCloudColor == "RGB")
                {
                    colorArray[0] = lazReader.point.rgb[0];
                    colorArray[1] = lazReader.point.rgb[1];
                    colorArray[2] = lazReader.point.rgb[2];
                    colorPoint.Color = colorMapper.MapColor(ColorMode.RGB, null, colorArray);
                }
                if (pointCloudColor == "warm")
                {
                    colorPoint.Color = colorMapper.MapColor(ColorMode.Warm, colorArray);
                }
                if (pointCloudColor == "cold")
                {
                    colorPoint.Color = colorMapper.MapColor(ColorMode.Cold, coordArray);
                }
                if (pointCloudColor == "rainbow")
                {
                    colorPoint.Color = colorMapper.MapColor(ColorMode.Rainbow, coordArray);
                }
                colorPoint.Point.X = (coordArray[0] - centx) / scale;
                colorPoint.Point.Y = (coordArray[1] - centy) / scale;
                colorPoint.Point.Z = (coordArray[2] - centz) / scale;
                classification = lazReader.point.classification;
                colorPoint.Classification = classification;
                // points.Add(colorPoint);
                pco.InsertPoint(colorPoint);
                progressBar1.Value = (pointIndex * 100) / (int)numberOfPoints;
            }
            lazReader.laszip_close_reader();

            resourceManager.MinRealCoord = new Vector3d(minx, miny, minz);
            resourceManager.MaxRealCoord = new Vector3d(maxx, maxy, maxz);
            resourceManager.CenterCoord = new Vector3d(centx, centy, centz);

            resourceManager.MinDrawCoord = minv;
            resourceManager.MaxDrawCoord = maxv;

            // PointCloudOctree p = new PointCloudOctree(ref points, ref minv, ref maxv, colorMapper);
            progressBar1.Value = 100;
            progressBar1.Visible = false;
            return p;
        }

        #endregion Open Las File

        private int UnProject(Vector3d win, ref Vector3d obj)
        {
            Matrix4d modelMatrix;
            GL.GetDouble(GetPName.ModelviewMatrix, out modelMatrix);
            Matrix4d projMatrix;
            GL.GetDouble(GetPName.ProjectionMatrix, out projMatrix);
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            return UnProject(win, modelMatrix, projMatrix, viewport, ref obj);
        }

        private int UnProject(Vector3d win, Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport, ref Vector3d obj)
        {
            return like_gluUnProject(win.X, win.Y, win.Z, modelMatrix, projMatrix,
            viewport, ref obj.X, ref obj.Y, ref obj.Z);
        }

        private int like_gluUnProject(double winx, double winy, double winz,
            Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport,
            ref double objx, ref double objy, ref double objz)
        {
            Matrix4d finalMatrix;
            Vector4d _in;
            Vector4d _out;
            finalMatrix = Matrix4d.Mult(modelMatrix, projMatrix);
            finalMatrix.Invert();
            _in.X = winx;
            _in.Y = viewport[3] - winy;
            _in.Z = winz;
            _in.W = 1.0f;
            // Map x and y from window coordinates
            _in.X = (_in.X - viewport[0]) / viewport[2];
            _in.Y = (_in.Y - viewport[1]) / viewport[3];
            // Map to range -1 to 1
            _in.X = _in.X * 2 - 1;
            _in.Y = _in.Y * 2 - 1;
            _in.Z = _in.Z * 2 - 1;
            //__gluMultMatrixVecd(finalMatrix, _in, _out);
            // check if this works:
            _out = Vector4d.Transform(_in, finalMatrix);
            if (_out.W == 0.0)
                return (0);
            _out.X /= _out.W;
            _out.Y /= _out.W;
            _out.Z /= _out.W;
            objx = _out.X;
            objy = _out.Y;
            objz = _out.Z;
            return (1);
        }

        private void screenShotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int[] vdata = new int[4];
            GL.GetInteger(GetPName.Viewport, vdata);
            int w = vdata[2];
            int h = vdata[3];
            if ((w % 4) != 0)
                w = (w / 4 + 1) * 4;
            byte[] imgBuffer = new byte[w * h * 3];
            GL.ReadPixels(0, 0, w, h, OpenTK.Graphics.OpenGL.PixelFormat.Bgr, PixelType.UnsignedByte, imgBuffer);
            FlipHeight(imgBuffer, w, h);
            Bitmap bmp = BytesToImg(imgBuffer, w, h);
            string currentPath = Path.GetDirectoryName(Application.ExecutablePath);
            string folderPath = Path.Combine(currentPath, "Screenshots");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string filePath = Path.Combine(folderPath, $"Screenshot_{DateTime.Now:dd-MM-yyy_HH-mm-ss}.png");
            bmp.Save(filePath, ImageFormat.Png);

            MessageBox.Show($"Screenshot saved to {filePath}");
        }

        private void FlipHeight(byte[] data, int w, int h)
        {
            int wstep = w * 3;
            byte[] temp = new byte[wstep];
            for (int i = 0; i < h / 2; i++)
            {
                Array.Copy(data, wstep * i, temp, 0, wstep);
                Array.Copy(data, wstep * (h - i - 1), data, wstep * i, wstep);
                Array.Copy(temp, 0, data, wstep * (h - i - 1), wstep);
            }
        }

        private Bitmap BytesToImg(byte[] bytes, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
            ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            IntPtr ptr = bd.Scan0;
            int bmpLen = bd.Stride * bd.Height;
            Marshal.Copy(bytes, 0, ptr, bmpLen); //using System.Runtime.InteropServices;
            bmp.UnlockBits(bd);
            return bmp;
        }

        #region Select Color

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                StringBuilder sb = new StringBuilder("Chưa có tệp được nạp. Vui lòng nạp tệp LAS/LAZ trước khi chọn màu sắc.");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (isCutPoint)
            {
                StringBuilder sb = new StringBuilder("Thoát chế độ cắt điểm trước!");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (pco == null) return;
            pointCloudColor = "RGB";
            pco.UpdateColor(points, pointCloudColor);
            Invalidate();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                StringBuilder sb = new StringBuilder("Chưa có tệp được nạp. Vui lòng nạp tệp LAS/LAZ trước khi chọn màu sắc.");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (isCutPoint)
            {
                StringBuilder sb = new StringBuilder("Thoát chế độ cắt điểm trước!");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (pco == null) return;
            pointCloudColor = "rainbow";
            pco.UpdateColor(points, pointCloudColor);
            Invalidate();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                StringBuilder sb = new StringBuilder("Chưa có tệp được nạp. Vui lòng nạp tệp LAS/LAZ trước khi chọn màu sắc.");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (isCutPoint)
            {
                StringBuilder sb = new StringBuilder("Thoát chế độ cắt điểm trước!");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (pco == null) return;
            pointCloudColor = "warm";
            pco.UpdateColor(points, pointCloudColor);
            Invalidate();
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                StringBuilder sb = new StringBuilder("Chưa có tệp được nạp. Vui lòng nạp tệp LAS/LAZ trước khi chọn màu sắc.");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (isCutPoint)
            {
                StringBuilder sb = new StringBuilder("Thoát chế độ cắt điểm trước!");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (pco == null) return;
            pointCloudColor = "lạnh";
            pco.UpdateColor(points, pointCloudColor);
            Invalidate();
        }

        #endregion Select Color

        #region Draw Primitive Shapes

        private void drawSphereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            primtiveObject = "triangle";
        }

        private void drawTriangleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            primtiveObject = "sphere";
        }

        #endregion Draw Primitive Shapes

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (showTreeNodeOutline == false)
                showTreeNodeOutline = true;
            else
                showTreeNodeOutline = false;
            Invalidate();
        }

        private void radioButton4_CheckedChanged_1(object sender, EventArgs e)
        {
            perspectiveProjection = false;
            Invalidate();
        }

        private void radioButton5_CheckedChanged_1(object sender, EventArgs e)
        {
            perspectiveProjection = true;
            Invalidate();
        }

        #region useless

        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            //richTextBox1.Text = "ok";
        }

        #endregion useless

        private void splitContainer1_ClientSizeChanged(object sender, EventArgs e)
        {
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (bOpenGLInitial)
            {
                SetupViewport();
                Invalidate();
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                cb_CutPoint.Checked = false;
                return;
            }
            if (!isCutPoint)
            {
                cb_CutPoint.BackgroundImage = Properties.Resources.scissors_glyph_icon_free_vector___enabled;
                isCutPoint = true;
            }
            else
            {
                cb_CutPoint.BackgroundImage = Properties.Resources.scissors_glyph_icon_free_vector;
                isCutPoint = false;
                vertices = null;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (isCutPoint)
            {
                StringBuilder sb = new StringBuilder("Thoát chế độ cắt điểm trước!");
                MessageBox.Show(sb.ToString());
                return;
            }
            int a = (int)numeric_PointSize.Value;
            pointSize = a;
            Invalidate();
        }

        private void saveFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                StringBuilder sb = new StringBuilder("Chưa có tệp được nạp. Vui lòng nạp tệp LAS/LAZ trước khi lưu.");
                MessageBox.Show(sb.ToString());
                return;
            }
            if (isCutPoint)
            {
                StringBuilder sb = new StringBuilder("Thoát chế độ cắt điểm trước!");
                MessageBox.Show(sb.ToString());
                return;
            }
            ExportFile.ExportPoints(points, resourceManager, progressBar1);
        }

        private void newFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }
        private void infoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!isOpenFile)
            {
                StringBuilder sb = new StringBuilder("Chưa có tệp được nạp. Vui lòng nạp tệp LAS/LAZ trước khi xem thông tin dữ liệu.");
                MessageBox.Show(sb.ToString());
                return;
            }
            try
            {
                var header = resourceManager.LasHeader;
                string systemIdentifier = Encoding.UTF8.GetString(header.system_identifier).Trim('\0');
                string generatingSoftware = Encoding.UTF8.GetString(header.generating_software).Trim('\0');
                Console.WriteLine(systemIdentifier);
                string info = $"File Source ID: {header.file_source_ID}\n" +
                              $"Global Encoding: {header.global_encoding}\n" +
                              $"Project ID GUID: {header.project_ID_GUID_data_1}-{header.project_ID_GUID_data_2}-{header.project_ID_GUID_data_3}\n" +
                              $"Version: {header.version_major}.{header.version_minor}\n" +
                              $"System Identifier: {systemIdentifier}\n" +
                              $"Generating Software: {generatingSoftware}\n" +
                              $"File Creation Day/Year: {header.file_creation_day}/{header.file_creation_year}\n" +
                              $"Header Size: {header.header_size}\n" +
                              $"Offset to Point Data: {header.offset_to_point_data}\n" +
                              $"Number of Variable Length Records: {header.number_of_variable_length_records}\n" +
                              $"Point Data Format: {header.point_data_format}\n" +
                              $"Point Data Record Length: {header.point_data_record_length}\n" +
                              $"Number of Point Records: {header.number_of_point_records}\n" +
                              $"Scale Factors: X({header.x_scale_factor}), Y({header.y_scale_factor}), Z({header.z_scale_factor})\n" +
                              $"Offsets: X({header.x_offset}), Y({header.y_offset}), Z({header.z_offset})\n" +
                              $"Max X: {header.max_x}\nMax Y: {header.max_y}\nMax Z: {header.max_z}\n" +
                              $"Min X: {header.min_x}\nMin Y: {header.min_y}\nMin Z: {header.min_z}\n";

                MessageBox.Show(info, "Thông tin tệp LAS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder("Không thể đọc thông tin từ tệp LAS. Vui lòng kiểm tra tệp và thử lại.\n");
                MessageBox.Show(sb.ToString() + ": " + ex.Message.ToString());
            }
        }
    }
}