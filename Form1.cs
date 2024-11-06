using laszip.net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using PointCloudViewer.service;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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

        private bool bOpenGLInitial = false;
        private int pointSize = 1;
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
        private string pointCloudColor = "rainbow";
        private bool calculateTwoPointsDistance = false;
        private List<Point3DExt> twoPoints = new List<Point3DExt>(2);
        private int numTwoPoints = -1;
        private float fov = (float)Math.PI / 3.0f;
        private bool perspectiveProjection = false;
        private bool hasLas = false;
        private bool isCutPoint = false;
        private Vector3d mousePos = Vector3d.Zero;
        private List<ColorPoint> points;
        private Point3DExt pivotPoint;
        private List<Vector3d> edge;
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

        #region Draw

        private void DrawTriangle()
        {
            GL.Begin(PrimitiveType.Triangles);
            GL.Color4(Color4.Yellow);
            GL.Vertex3(0, 0, 0);
            GL.Color4(Color4.Red);
            GL.Vertex3(0.9, 0, 0);
            GL.Color4(Color4.Green);
            GL.Vertex3(0.9, 0.9, 0);
            GL.End();
        }

        private void DrawSphere()
        {
            const double radius = 0.5;
            const int step = 5;
            int xWidth = 360 / step + 1;
            int zHeight = 180 / step + 1;
            int halfZHeight = (zHeight - 1) / 2;
            int v = 0;
            double xx, yy, zz;

            GL.PointSize(pointSize);
            GL.Begin(PrimitiveType.Points);
            GL.Color4(Color4.Yellow);
            for (int z = -halfZHeight; z <= halfZHeight; z++)
            {
                var d = 0;
                for (int x = 0; x < xWidth; x++)
                {
                    xx = radius * Math.Cos(x * step * Math.PI / 180)
                        * Math.Cos(z * step * Math.PI / 180.0);
                    zz = radius * Math.Sin(x * step * Math.PI / 180)
                        * Math.Cos(z * step * Math.PI / 180.0);
                    yy = radius * Math.Sin(z * step * Math.PI / 180);
                    GL.Vertex3(xx, yy, zz);
                }
            }
            GL.End();
        }

        #endregion Draw

        private void InitialGL()
        {
            GL.ShadeModel(ShadingModel.Smooth);
            GL.ClearColor(0.0f, 0.2f, 0.2f, 0.2f);
            GL.ClearDepth(1.0f);
            GL.Enable(EnableCap.DepthTest);
            SetupViewport();
            bOpenGLInitial = true;
        }

        private void SetupViewport()
        {
            int w = glControl1.ClientSize.Width;
            int h = glControl1.ClientSize.Height;

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            double aspect;

            if (perspectiveProjection)
            {
                aspect = w / (double)h;
                GL.Viewport(0, 0, w, h);
                like_gluPerspective(fov, aspect, -10, 100);
            }
            else
            {
                float n = scaling;
                aspect = (w >= h) ? (1.0 * w / h) : (1.0 * h / w);
                float left = -n * 0.5f, right = n * 0.5f, down = (float)(-n * 0.5f / aspect), up = (float)(n * 0.5f / aspect);
                if (w <= h)
                    GL.Ortho(-1, 1, -aspect, aspect, -10, 100);
                else
                    GL.Ortho(left, right, down, up, -10.0f, 10.0f);
                GL.Viewport(0, 0, w, h);
            }
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
            if (isCutPoint)
            {
                if (edge == null || edge.Count < 0) return;
                GL.Disable(EnableCap.DepthTest);
                for (int i = 0; i < edge.Count; i++)
                {
                    if (i > 0)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(edge[i]);
                        GL.Vertex3(edge[i - 1]);
                        GL.End();
                    }
                    if (i < 2 && edge.Count <= 3)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(edge[edge.Count - 1]);
                        GL.Vertex3(mousePos);
                        GL.End();
                    }
                    if (i == 2 && edge.Count <= 3)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(mousePos);
                        GL.Vertex3(edge[0]);
                        GL.End();
                    }
                    if (i == 3)
                    {
                        GL.Begin(PrimitiveType.Lines);
                        GL.Color3(Color.Yellow);
                        GL.Vertex3(edge[i]);
                        GL.Vertex3(edge[0]);
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
        }

        private float offsetXPos = 0;
        private float offsetYPos = 0;
        private float prevXPos = 0;
        private float prevYPos = 0;

        private void glControl1_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (bLeftButtonPushed)
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
            if (bRightButtonPushed)
            {
                angleX += (e.Location.X - RightButtonPosition.X) / 10.0;
                angleY += -(e.Location.Y - RightButtonPosition.Y) / 10.0;
                offsetYaw = (float)(angleX - previousYaw);
                offsetPitch = (float)(angleY - previousPitch);

                RightButtonPosition = e.Location;
            }
            if (isCutPoint)
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

            if (isCutPoint && bLeftButtonPushed)
            {
                if (edge.Count == 4)
                {
                    DialogResult dialogResult = MessageBox.Show("Bạn có chắc chắn với vùng chọn?", "Warning", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        points = CutPoint.FindProjection(edge, glControl1, points);
                        pco.Dispose();
                        edge.Clear();
                        pco = new PointCloudOctree(ref points, ref minv, ref maxv);
                        GC.Collect();
                        Invalidate();
                        return;
                    }
                    else
                    {
                        edge.Clear();
                        Invalidate();
                        return;
                    }
                }
                else if (offsetXPos == 0 && offsetYPos == 0)
                {
                    Vector3d winxyz;
                    winxyz.X = e.Location.X;
                    winxyz.Y = e.Location.Y;
                    winxyz.Z = 0.3f;
                    Vector3d vertex = new Vector3d(0, 0, 0);
                    UnProject(winxyz, ref vertex);
                    edge.Add(vertex);
                }
            }
            Render(pointSize, showTreeNodeOutline, pointCloudColor);
            Invalidate();
        }

        private void glControl1_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
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

            Point3DExt closePoint = new Point3DExt();
            closePoint.Flag = 10000;

            if (pco != null)
            {
                pco.FindClosestPoint(frustum, nearPoint, farPoint, ref closePoint);
                if (!calculateTwoPointsDistance)
                    MessageBox.Show("Selected point coordinate：\nX：" + closePoint.Point.X
                        + "\nY：" + closePoint.Point.Y + "\nZ：" + closePoint.Point.Z);
                else
                {
                    if (numTwoPoints > 0)
                    {
                        numTwoPoints = 0;
                        twoPoints.Clear();
                    }
                    else
                        numTwoPoints += 1;

                    twoPoints.Add(closePoint);
                    if (numTwoPoints == 1)
                    {
                        double pointsDistance = calculateDistance(twoPoints[0], twoPoints[1]);
                        MessageBox.Show("2 selected coordinates：\n"
                            + coordinate2string(twoPoints[0].Point) + "\n"
                            + coordinate2string(twoPoints[1].Point) + "\n"
                            + "Distance between 2 selected points：\n"
                            + Convert.ToString(pointsDistance));
                    }
                }
                Render(pointSize, showTreeNodeOutline, pointCloudColor);
                Invalidate();
                glControl1.Invalidate();
            }
        }

        #endregion Mouse Events

        #region Open Las File

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog pOpenFileDialog = new OpenFileDialog();
            pOpenFileDialog.Title = "Open LAS file";
            pOpenFileDialog.Filter = "las *.las |*.las";
            pOpenFileDialog.CheckFileExists = true;
            if (pOpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                pco = ReadLas(pOpenFileDialog.FileName);
                Invalidate();
            }
        }

        // private PointCloudOctree ReadLas(string fileName)
        // {
        //     var lazReader = new laszip_dll();
        //     var compressed = true;
        //     lazReader.laszip_open_reader(fileName, ref compressed);
        //
        //     var numberOfPoints = lazReader.header.number_of_point_records;
        //
        //     double minx = lazReader.header.min_x;
        //     double miny = lazReader.header.min_y;
        //     double minz = lazReader.header.min_z;
        //     double maxx = lazReader.header.max_x;
        //     double maxy = lazReader.header.max_y;
        //     double maxz = lazReader.header.max_z;
        //
        //     double centx = (minx + maxx) / 2;
        //     double centy = (miny + maxy) / 2;
        //     double centz = (minz + maxz) / 2;
        //
        //     double scale = Math.Max(Math.Max(maxx - minx, maxy - miny), (maxz - minz));
        //     byte classification = 0;
        //     var coordArray = new double[3];
        //     ColorPoint colorPoint = new ColorPoint();
        //     //ColorPoint[] points = new ColorPoint[numberOfPoints];
        //     // points = new List<ColorPoint>((int)numberOfPoints);
        //     points = new List<ColorPoint>(10000);
        //     Vector3d crYellow = new Vector3d(1, 1, 0);
        //     for (int pointIndex = 0; pointIndex < numberOfPoints; pointIndex++)
        //     {
        //         lazReader.laszip_read_point();
        //         lazReader.laszip_get_coordinates(coordArray);
        //
        //         Vector3d crRed = new Vector3d(1, 0, 0);
        //         Vector3d crOrange = new Vector3d(1, 0.647, 0);
        //         Vector3d crGreen = new Vector3d(0, 1, 0);
        //         Vector3d crCyan = new Vector3d(0, 0.498, 1);
        //         Vector3d crBlue = new Vector3d(0, 0, 1);
        //         Vector3d crPurple = new Vector3d(0.545, 0, 1);
        //
        //         if (pointCloudColor == "RGB")
        //         {
        //             colorPoint.color.X = lazReader.point.rgb[0] / 255.0;
        //             colorPoint.color.Y = lazReader.point.rgb[1] / 255.0;
        //             colorPoint.color.Z = lazReader.point.rgb[2] / 255.0;
        //         }
        //         if (pointCloudColor == "rainbow")
        //         {
        //             double z_1_6 = 1 * (maxz - minz) / 6 + minz;
        //             double z_2_6 = 2 * (maxz - minz) / 6 + minz;
        //             double z_3_6 = 3 * (maxz - minz) / 6 + minz;
        //             double z_4_6 = 4 * (maxz - minz) / 6 + minz;
        //             double z_5_6 = 5 * (maxz - minz) / 6 + minz;
        //
        //             if (coordArray[2] <= z_1_6)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - minz) / (z_1_6 - minz) * (crOrange.X - crRed.X) + crRed.X;
        //                 colorPoint.color.Y = (coordArray[2] - minz) / (z_1_6 - minz) * (crOrange.Y - crRed.Y) + crRed.Y;
        //                 colorPoint.color.Z = (coordArray[2] - minz) / (z_1_6 - minz) * (crOrange.Z - crRed.Z) + crRed.Z;
        //             }
        //             else if (coordArray[2] <= z_2_6)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (crYellow.X - crOrange.X) + crOrange.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (crYellow.Y - crOrange.Y) + crOrange.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (crYellow.Z - crOrange.Z) + crOrange.Z;
        //             }
        //             else if (coordArray[2] <= z_3_6)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (crGreen.X - crYellow.X) + crYellow.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (crGreen.Y - crYellow.Y) + crYellow.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (crGreen.Z - crYellow.Z) + crYellow.Z;
        //             }
        //             else if (coordArray[2] <= z_4_6)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (crCyan.X - crGreen.X) + crGreen.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (crCyan.Y - crGreen.Y) + crGreen.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (crCyan.Z - crGreen.Z) + crGreen.Z;
        //             }
        //             else if (coordArray[2] <= z_5_6)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (crBlue.X - crCyan.X) + crCyan.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (crBlue.Y - crCyan.Y) + crCyan.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (crBlue.Z - crCyan.Z) + crCyan.Z;
        //             }
        //             else
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (crPurple.X - crBlue.X) + crBlue.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (crPurple.Y - crBlue.Y) + crBlue.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (crPurple.Z - crBlue.Z) + crBlue.Z;
        //             }
        //         }
        //         else if (pointCloudColor == "warm")
        //         {
        //             double z_1_2 = 1 * (maxz - minz) / 2 + minz;
        //
        //             if (coordArray[2] <= z_1_2)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - minz) / (z_1_2 - minz) * (crOrange.X - crRed.X) + crRed.X;
        //                 colorPoint.color.Y = (coordArray[2] - minz) / (z_1_2 - minz) * (crOrange.Y - crRed.Y) + crRed.Y;
        //                 colorPoint.color.Z = (coordArray[2] - minz) / (z_1_2 - minz) * (crOrange.Z - crRed.Z) + crRed.Z;
        //             }
        //             else
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crYellow.X - crOrange.X) + crOrange.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crYellow.Y - crOrange.Y) + crOrange.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crYellow.Z - crOrange.Z) + crOrange.Z;
        //             }
        //         }
        //         else if (pointCloudColor == "cold")
        //         {
        //             double z_1_2 = 1 * (maxz - minz) / 2 + minz;
        //
        //             if (coordArray[2] <= z_1_2)
        //             {
        //                 colorPoint.color.X = (coordArray[2] - minz) / (z_1_2 - minz) * (crCyan.X - crGreen.X) + crGreen.X;
        //                 colorPoint.color.Y = (coordArray[2] - minz) / (z_1_2 - minz) * (crCyan.Y - crGreen.Y) + crGreen.Y;
        //                 colorPoint.color.Z = (coordArray[2] - minz) / (z_1_2 - minz) * (crCyan.Z - crGreen.Z) + crGreen.Z;
        //             }
        //             else
        //             {
        //                 colorPoint.color.X = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crBlue.X - crCyan.X) + crCyan.X;
        //                 colorPoint.color.Y = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crBlue.Y - crCyan.Y) + crCyan.Y;
        //                 colorPoint.color.Z = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crBlue.Z - crCyan.Z) + crCyan.Z;
        //             }
        //         }
        //
        //         colorPoint.point.X = (coordArray[0] - centx) / scale;
        //         colorPoint.point.Y = (coordArray[1] - centy) / scale;
        //         colorPoint.point.Z = (coordArray[2] - centz) / scale;
        //         classification = lazReader.point.classification;
        //
        //         points.Add(colorPoint);
        //     }
        //     lazReader.laszip_close_reader();
        //
        //     minv.X = (minx - centx) / scale;
        //     minv.Y = (miny - centy) / scale;
        //     minv.Z = (minz - centz) / scale;
        //
        //     maxv.X = (maxx - centx) / scale;
        //     maxv.Y = (maxy - centy) / scale;
        //     maxv.Z = (maxz - centz) / scale;
        //
        //     PointCloudOctree p = new PointCloudOctree(ref points, ref minv, ref maxv);
        //     return p;
        // }
        private PointCloudOctree ReadLas(string fileName)
        {
            var lazReader = new laszip_dll();
            var compressed = true;
            lazReader.laszip_open_reader(fileName, ref compressed);
        
            var numberOfPoints = lazReader.header.number_of_point_records;
        
            double minx = lazReader.header.min_x;
            double miny = lazReader.header.min_y;
            double minz = lazReader.header.min_z;
            double maxx = lazReader.header.max_x;
            double maxy = lazReader.header.max_y;
            double maxz = lazReader.header.max_z;
        
            double centx = (minx + maxx) / 2;
            double centy = (miny + maxy) / 2;
            double centz = (minz + maxz) / 2;
        
            double scale = Math.Max(Math.Max(maxx - minx, maxy - miny), (maxz - minz));
            byte classification = 0;
            var coordArray = new double[3];
            ColorPoint colorPoint = new ColorPoint();
            points = new List<ColorPoint>(10000);
            Vector3d crYellow = new Vector3d(1, 1, 0);
            for (int pointIndex = 0; pointIndex < numberOfPoints; pointIndex++)
            {
                lazReader.laszip_read_point();
                lazReader.laszip_get_coordinates(coordArray);
        
                Vector3d crRed = new Vector3d(1, 0, 0);
                Vector3d crOrange = new Vector3d(1, 0.647, 0);
                Vector3d crGreen = new Vector3d(0, 1, 0);
                Vector3d crCyan = new Vector3d(0, 0.498, 1);
                Vector3d crBlue = new Vector3d(0, 0, 1);
                Vector3d crPurple = new Vector3d(0.545, 0, 1);
        
                if (pointCloudColor == "RGB")
                {
                    colorPoint.color.X = lazReader.point.rgb[0] / 255.0;
                    colorPoint.color.Y = lazReader.point.rgb[1] / 255.0;
                    colorPoint.color.Z = lazReader.point.rgb[2] / 255.0;
                }
                if (pointCloudColor == "rainbow")
                {
                    double z_1_6 = 1 * (maxz - minz) / 6 + minz;
                    double z_2_6 = 2 * (maxz - minz) / 6 + minz;
                    double z_3_6 = 3 * (maxz - minz) / 6 + minz;
                    double z_4_6 = 4 * (maxz - minz) / 6 + minz;
                    double z_5_6 = 5 * (maxz - minz) / 6 + minz;
        
                    if (coordArray[2] <= z_1_6)
                    {
                        colorPoint.color.X = (coordArray[2] - minz) / (z_1_6 - minz) * (crOrange.X - crRed.X) + crRed.X;
                        colorPoint.color.Y = (coordArray[2] - minz) / (z_1_6 - minz) * (crOrange.Y - crRed.Y) + crRed.Y;
                        colorPoint.color.Z = (coordArray[2] - minz) / (z_1_6 - minz) * (crOrange.Z - crRed.Z) + crRed.Z;
                    }
                    else if (coordArray[2] <= z_2_6)
                    {
                        colorPoint.color.X = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (crYellow.X - crOrange.X) + crOrange.X;
                        colorPoint.color.Y = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (crYellow.Y - crOrange.Y) + crOrange.Y;
                        colorPoint.color.Z = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (crYellow.Z - crOrange.Z) + crOrange.Z;
                    }
                    else if (coordArray[2] <= z_3_6)
                    {
                        colorPoint.color.X = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (crGreen.X - crYellow.X) + crYellow.X;
                        colorPoint.color.Y = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (crGreen.Y - crYellow.Y) + crYellow.Y;
                        colorPoint.color.Z = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (crGreen.Z - crYellow.Z) + crYellow.Z;
                    }
                    else if (coordArray[2] <= z_4_6)
                    {
                        colorPoint.color.X = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (crCyan.X - crGreen.X) + crGreen.X;
                        colorPoint.color.Y = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (crCyan.Y - crGreen.Y) + crGreen.Y;
                        colorPoint.color.Z = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (crCyan.Z - crGreen.Z) + crGreen.Z;
                    }
                    else if (coordArray[2] <= z_5_6)
                    {
                        colorPoint.color.X = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (crBlue.X - crCyan.X) + crCyan.X;
                        colorPoint.color.Y = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (crBlue.Y - crCyan.Y) + crCyan.Y;
                        colorPoint.color.Z = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (crBlue.Z - crCyan.Z) + crCyan.Z;
                    }
                    else
                    {
                        colorPoint.color.X = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (crPurple.X - crBlue.X) + crBlue.X;
                        colorPoint.color.Y = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (crPurple.Y - crBlue.Y) + crBlue.Y;
                        colorPoint.color.Z = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (crPurple.Z - crBlue.Z) + crBlue.Z;
                    }
                }
                else if (pointCloudColor == "warm")
                {
                    double z_1_2 = 1 * (maxz - minz) / 2 + minz;
        
                    if (coordArray[2] <= z_1_2)
                    {
                        colorPoint.color.X = (coordArray[2] - minz) / (z_1_2 - minz) * (crOrange.X - crRed.X) + crRed.X;
                        colorPoint.color.Y = (coordArray[2] - minz) / (z_1_2 - minz) * (crOrange.Y - crRed.Y) + crRed.Y;
                        colorPoint.color.Z = (coordArray[2] - minz) / (z_1_2 - minz) * (crOrange.Z - crRed.Z) + crRed.Z;
                    }
                    else
                    {
                        colorPoint.color.X = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crYellow.X - crOrange.X) + crOrange.X;
                        colorPoint.color.Y = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crYellow.Y - crOrange.Y) + crOrange.Y;
                        colorPoint.color.Z = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crYellow.Z - crOrange.Z) + crOrange.Z;
                    }
                }
                else if (pointCloudColor == "cold")
                {
                    double z_1_2 = 1 * (maxz - minz) / 2 + minz;
        
                    if (coordArray[2] <= z_1_2)
                    {
                        colorPoint.color.X = (coordArray[2] - minz) / (z_1_2 - minz) * (crCyan.X - crGreen.X) + crGreen.X;
                        colorPoint.color.Y = (coordArray[2] - minz) / (z_1_2 - minz) * (crCyan.Y - crGreen.Y) + crGreen.Y;
                        colorPoint.color.Z = (coordArray[2] - minz) / (z_1_2 - minz) * (crCyan.Z - crGreen.Z) + crGreen.Z;
                    }
                    else
                    {
                        colorPoint.color.X = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crBlue.X - crCyan.X) + crCyan.X;
                        colorPoint.color.Y = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crBlue.Y - crCyan.Y) + crCyan.Y;
                        colorPoint.color.Z = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (crBlue.Z - crCyan.Z) + crCyan.Z;
                    }
                }
        
                colorPoint.point.X = (coordArray[0] - centx) / scale;
                colorPoint.point.Y = (coordArray[1] - centy) / scale;
                colorPoint.point.Z = (coordArray[2] - centz) / scale;
                classification = lazReader.point.classification;
        
                points.Add(colorPoint);
            }
            lazReader.laszip_close_reader();
        
            minv.X = (minx - centx) / scale;
            minv.Y = (miny - centy) / scale;
            minv.Z = (minz - centz) / scale;
        
            maxv.X = (maxx - centx) / scale;
            maxv.Y = (maxy - centy) / scale;
            maxv.Z = (maxz - centz) / scale;
        
            PointCloudOctree p = new PointCloudOctree(ref points, ref minv, ref maxv);
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
            GL.ReadPixels(0, 0, w, h, OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
           PixelType.UnsignedByte, imgBuffer);
            FlipHeight(imgBuffer, w, h);
            Bitmap bmp = BytesToImg(imgBuffer, w, h);
            bmp.Save("D:\\opentk.bmp");
            MessageBox.Show("Screen shot taken！");
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
            pointCloudColor = "RGB";
            Invalidate();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            pointCloudColor = "rainbow";
            Invalidate();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            pointCloudColor = "warm";
            Invalidate();
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            pointCloudColor = "cold";
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

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            calculateTwoPointsDistance = !calculateTwoPointsDistance;
            //if (calculateTwoPointsDistance)
            //    calculateTwoPointsDistance = false;
            //else
            //    calculateTwoPointsDistance = true;
        }

        private string coordinate2string(Vector3d v)
        {
            string coordinates = "(" + Convert.ToString(v.X) + "," + Convert.ToString(v.Y) + ","
                + Convert.ToString(v.Z) + ")";
            return coordinates;
        }

        private double calculateDistance(Point3DExt point1, Point3DExt point2)
        {
            double x = point1.Point.X - point2.Point.X;
            double y = point1.Point.Y - point2.Point.Y;
            double z = point1.Point.Z - point2.Point.Z;

            double dd = x * x + y * y + z * z;
            double d = Math.Sqrt(dd);

            return d;
        }

        private void instructionToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Hide();
            Form1 form1 = new Form1();
            form1.Show();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        public void like_gluPerspective(double fovy, double aspect, double near, double far)
        {
            const double DEG2RAD = 3.14159265 / 180.0;
            double tangent = Math.Tan(fovy / 2 * DEG2RAD);
            double height = near * tangent;
            double width = height * aspect;
            GL.Frustum(-width, width, -height, height, -100, 100);
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

        private void label1_Click(object sender, EventArgs e)
        {
            //label1.Text = "a";
        }

        private void splitContainer2_Panel2_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            pointSize += 1;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void checkBox3_CheckedChanged_1(object sender, EventArgs e)
        {
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = false;
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
            if (!isCutPoint)
            {
                //glControl1.MouseMove -= glControl1_MouseMove;
                lbl_Mode.Text = "Mode: Cut point cloud";
                isCutPoint = true;
                edge = new List<Vector3d>();
                btn_OnOffMode.Visible = true;
            }
            else
            {
                //glControl1.MouseMove += glControl1_MouseMove;
                lbl_Mode.Text = "";
                isCutPoint = false;
                edge = null;
                btn_OnOffMode.Visible = false;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            int a = (int)numeric_PointSize.Value;
            pointSize = a;
            Invalidate();
        }

        private void btn_OnOffMode_Click(object sender, EventArgs e)
        {
            if (isCutPoint)
            {
                //glControl1.MouseMove += glControl1_MouseMove;
                lbl_Mode.Text = "";
                isCutPoint = false;
                edge = null;
                btn_OnOffMode.Visible = false;
            }
        }
    }
}