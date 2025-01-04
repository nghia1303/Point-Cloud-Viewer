using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace PointCloudViewer.service
{
    public class Render
    {
        private List<ColorPoint> _points;
        private string _colorType = "RBB";
        private List<int> _displayLists;
        public Render(List<ColorPoint> points, string colorType)
        {
            _points = points;
            _colorType = colorType;
        }
        public void GenerateDisplayLists()
        {
            if (_points.Count > 10000)
            {
                _displayLists = new List<int>();
                int numLists = _points.Count / 10000;
                for (int i = 0; i < numLists; i++)
                {
                    int displayList = GL.GenLists(1);
                    GL.NewList(displayList, ListMode.Compile);
                    RenderPoints(i * 10000, 10000);
                    GL.EndList();
                    _displayLists.Add(displayList);
                }
                if (_points.Count % 10000 != 0)
                {
                    int displayList = GL.GenLists(1);
                    GL.NewList(displayList, ListMode.Compile);
                    RenderPoints(numLists * 10000, _points.Count % 10000);
                    GL.EndList();
                    _displayLists.Add(displayList);
                }
            }
            else
            {
                int displayList = GL.GenLists(1);
                GL.NewList(displayList, ListMode.Compile);
                RenderPoints(0, _points.Count);
                GL.EndList();
                _displayLists = new List<int> { displayList };
            }
        }
        public void RenderDisplayList()
        {
            foreach (int displayList in _displayLists)
            {
                GL.CallList(displayList);
            }
        }
        private void RenderPoints(int start, int count)
        {
            GL.Begin(PrimitiveType.Points);
            for (int i = start; i < start + count; i++)
            {
                ColorPoint point = _points[i];
                GL.Color3(point.Color.X, point.Color.Y, point.Color.Z);
                GL.Vertex3(point.Point.X, point.Point.Y, point.Point.Z);
            }
            GL.End();
        }
    }
}
