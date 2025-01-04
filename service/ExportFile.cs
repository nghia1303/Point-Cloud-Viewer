using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using laszip.net;
using Newtonsoft.Json;

namespace PointCloudViewer.service
{
    internal class ExportFile
    {
        public static int ExportPoints(List<ColorPoint> points, ResourceManager resourceManager, ProgressBar progressBar)
        {
            if (points == null || points.Count == 0)
            {
                return 0;
            }
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|JSON files (*.json)|*.json|LAS files (*.las)|*.las|All files (*.*)|*.*",
                    FilterIndex = 1,
                    Title = "Lưu tệp",
                    DefaultExt = "txt"
                };

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return 0;
                }
                if (saveFileDialog.FileName == "")
                    return 0;

                string extension = Path.GetExtension(saveFileDialog.FileName).ToLower();

                double minx = points.Min(p => p.Point.X);
                double miny = points.Min(p => p.Point.Y);
                double minz = points.Min(p => p.Point.Z);
                double maxx = points.Max(p => p.Point.X);
                double maxy = points.Max(p => p.Point.Y);
                double maxz = points.Max(p => p.Point.Z);

                if (extension == ".json")
                {
                    ExportPointsToJson(points, resourceManager, saveFileDialog.FileName, minx, miny, minz, maxx, maxy, maxz);
                }
                if (extension == ".las")
                {
                    ExportPointsToLas(points, resourceManager, progressBar, saveFileDialog.FileName);
                }
                if (extension == ".txt") 
                {
                    ExportPointToTxt(points, resourceManager, progressBar, saveFileDialog.FileName, minx, miny, minz, maxx, maxy, maxz);
                }
                return points.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Có lỗi khi xuất tệp: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static void ExportPointToTxt(List<ColorPoint> points, ResourceManager resourceManager, ProgressBar progressBar, string fileName, double minx, double miny, double minz, double maxx, double maxy, double maxz)
        {
            progressBar.Visible = true;
            progressBar.Maximum = 100;
            try
            {
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    // Write header information as commented metadata
                    writer.WriteLine($"# Number of Points: {points.Count}");
                    writer.WriteLine($"# Bounds: X({minx}, {maxx}) Y({miny}, {maxy}) Z({minz}, {maxz})");
                    writer.WriteLine("# Format: X,Y,Z,Red,Green,Blue,Classification");
                    writer.WriteLine("# Units: Coordinates(meters), Colors(0-65535), Classification(0-255)");
                    writer.WriteLine();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var point = points[i];
                        writer.WriteLine(
                            $"{(int)((resourceManager.GetRealCoordinate(point).X - resourceManager.MinRealCoord.X) / resourceManager.ScaleFactor.X):F6}," +
                            $"{(int)((resourceManager.GetRealCoordinate(point).Y - resourceManager.MinRealCoord.Y) / resourceManager.ScaleFactor.Y):F6}," +
                            $"{(int)((resourceManager.GetRealCoordinate(point).Z - resourceManager.MinRealCoord.Z) / resourceManager.ScaleFactor.Z):F6}," +
                            $"{point.Color.X * 255}," +
                            $"{point.Color.Y * 255}," +
                            $"{point.Color.Z * 255}," + 
                            $"{point.Classification}"
                        );
                        progressBar.Value = ((i + 1) * 100) / points.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($" Có lỗi khi xuất tệp TXT: {ex.Message.ToString()}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            progressBar.Visible = false;
            MessageBox.Show("Xuất tệp thành công!");
        }

        private static void ExportPointsToJson(List<ColorPoint> points, ResourceManager resourceManager, string fileName, double minx, double miny, double minz, double maxx, double maxy, double maxz)
        {
            try
            {
                var metaData = new
                {
                    NumberOfPoints = points.Count,
                    Bounds = new { X = new { Min = minx, Max = maxx }, Y = new { Min = miny, Max = maxy }, Z = new { Min = minz, Max = maxz } },
                    Format = "X,Y,Z,Red,Green,Blue,Classification",
                    Units = "Coordinates(meters), Colors(0-255), Classification(0-255)"
                };

                var jsonData = new
                {
                    Metadata = metaData,
                    Points = points.Select(p => new {
                        X = (int)((resourceManager.GetRealCoordinate(p).X - resourceManager.MinRealCoord.X) / resourceManager.ScaleFactor.X),
                        Y = (int)((resourceManager.GetRealCoordinate(p).Y - resourceManager.MinRealCoord.Y) / resourceManager.ScaleFactor.Y),
                        Z = (int)((resourceManager.GetRealCoordinate(p).Z - resourceManager.MinRealCoord.Z) / resourceManager.ScaleFactor.Z), 
                        Red = p.Color.X * 255, 
                        Blue = p.Color.Y * 255, 
                        Green = p.Color.Z * 255, 
                        Classification = p.Classification 
                    })
                };

                using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                using (var streamWriter = new StreamWriter(fileStream))
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                    serializer.Serialize(jsonWriter, jsonData);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($" Có lỗi khi xuất tệp JSON: {ex.Message.ToString()}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }
            MessageBox.Show("Xuất tệp thành công!");
        }

        private static void ExportPointsToLas(List<ColorPoint> points, ResourceManager resourceManager, ProgressBar progressBar, string fileName)
        {
            try
            {
                progressBar.Visible = true;
                laszip_dll laszip = new laszip_dll();
                laszip = laszip_dll.laszip_create();
                DateTime dateTime = DateTime.Now;
                laszip.header.file_source_ID = 0;
                laszip.header.global_encoding = 1;
                laszip.header.project_ID_GUID_data_1 = 0;
                laszip.header.project_ID_GUID_data_2 = 0;
                laszip.header.project_ID_GUID_data_3 = 0;
                laszip.header.version_major = 1;
                laszip.header.version_minor = 2;
                laszip.header.file_creation_day = (ushort)dateTime.DayOfYear;
                laszip.header.file_creation_year = (ushort)dateTime.Year;
                laszip.header.header_size = 227;
                laszip.header.offset_to_point_data = 227;
                laszip.header.number_of_variable_length_records = 0;
                laszip.header.point_data_format = 3;
                laszip.header.point_data_record_length = 34;
                laszip.header.number_of_point_records = (uint)points.Count;
                laszip.header.x_scale_factor = resourceManager.ScaleFactor.X;
                laszip.header.y_scale_factor = resourceManager.ScaleFactor.Y;
                laszip.header.z_scale_factor = resourceManager.ScaleFactor.Z;
                laszip.header.x_offset = resourceManager.MinRealCoord.X;
                laszip.header.y_offset = resourceManager.MinRealCoord.Y;
                laszip.header.z_offset = resourceManager.MinRealCoord.Z;
                laszip.header.max_x = resourceManager.MaxRealCoord.X;
                laszip.header.max_y = resourceManager.MaxRealCoord.Y;
                laszip.header.max_z = resourceManager.MaxRealCoord.Z;
                laszip.header.min_x = resourceManager.MinRealCoord.X;
                laszip.header.min_y = resourceManager.MinRealCoord.Y;
                laszip.header.min_z = resourceManager.MinRealCoord.Z;

                laszip.laszip_open_writer(fileName, false);
                laszip_point point = new laszip_point();
                for (int i = 0; i < points.Count; i++)
                {
                    var p = resourceManager.GetRealCoordinate(points[i]);
                    point.X = (int)((p.X - resourceManager.MinRealCoord.X) / resourceManager.ScaleFactor.X);
                    point.Y = (int)((p.Y - resourceManager.MinRealCoord.Y) / resourceManager.ScaleFactor.Y);
                    point.Z = (int)((p.Z - resourceManager.MinRealCoord.Z) / resourceManager.ScaleFactor.Z);
                    point.intensity = 0;
                    point.return_number = 2;
                    point.number_of_returns_of_given_pulse = 1;
                    point.scan_direction_flag = 1;
                    point.edge_of_flight_line = 0;
                    point.classification = points[i].Classification;
                    point.scan_angle_rank = 0;
                    point.user_data = 0;
                    point.point_source_ID = 1;
                    point.gps_time = 1;
                    point.rgb[0] = (ushort)(points[i].Color.X * 255);
                    point.rgb[1] = (ushort)(points[i].Color.Y * 255);
                    point.rgb[2] = (ushort)(points[i].Color.Z * 255);
                    laszip.laszip_set_point(point);
                    laszip.laszip_write_point();
                    progressBar.Value = (i + 1)  * 100 / points.Count;
                }
                laszip.laszip_close_writer();
                progressBar.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($" Có lỗi khi xuất tệp LAS: {ex.Message.ToString()}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            MessageBox.Show("Xuất tệp thành công!");
        }
    }
}