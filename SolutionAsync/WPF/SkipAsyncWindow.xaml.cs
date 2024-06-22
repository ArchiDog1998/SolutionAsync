using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using SimpleGrasshopper.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SolutionAsync.WPF
{
    /// <summary>
    /// Interaction logic for SkipAsyncWindow.xaml
    /// </summary>
    public partial class SkipAsyncWindow : Window
    {
        private readonly GH_Canvas _canvas = Instances.ActiveCanvas;


        private readonly List<Guid> _preList;
        private bool _cancel = false;

        private IGH_ActiveObject _targetActiveObj;
        private IGH_ActiveObject TargetActiveObj
        {
            get => _targetActiveObj;
            set
            {
                _targetActiveObj = value;
            }
        }
        public SkipAsyncWindow()
        {
            ObservableCollection<ActiveObjItem> structureLists = new ();
            foreach (Guid guid in Data.NoAsyncObjects)
            {
                structureLists.Add(new ActiveObjItem(guid));
            }

            this.DataContext = structureLists;
            this._preList = Data.NoAsyncObjects;

            InitializeComponent();

            using MemoryStream ms = new ();
            typeof(SolutionAsyncInfo).Assembly.GetBitmap("SolutionAsyncIcon_24.png").Save(ms, System.Drawing.Imaging.ImageFormat.Png);

            BitmapImage ImageIcon = new ();
            ImageIcon.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            ImageIcon.StreamSource = ms;
            ImageIcon.EndInit();
            Icon = ImageIcon;
        }

        private void AddButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _canvas.MouseUp -= _canvas_MouseUp;
            _canvas.MouseLeave -= _canvas_MouseLeave;
            _canvas.MouseMove -= _canvas_MouseMove;
            _canvas.CanvasPostPaintWidgets -= CanvasPostPaintWidgets;

            _canvas.MouseUp += _canvas_MouseUp;
            _canvas.MouseMove += _canvas_MouseMove;
            _canvas.MouseLeave += _canvas_MouseLeave;
            _canvas.CanvasPostPaintWidgets += CanvasPostPaintWidgets;
            _canvas.ModifiersEnabled = false;
            _canvas.Refresh();
        }
        private void finish()
        {
            _canvas.MouseUp -= _canvas_MouseUp;
            _canvas.MouseLeave -= _canvas_MouseLeave;
            _canvas.MouseMove -= _canvas_MouseMove;
            _canvas.CanvasPostPaintWidgets -= CanvasPostPaintWidgets;
            Instances.CursorServer.ResetCursor(_canvas);
            _canvas.ModifiersEnabled = true;
            _canvas.Refresh();
        }

        private void _canvas_MouseLeave(object sender, EventArgs e)
        {
            finish();
            TargetActiveObj = null;
        }

        private void _canvas_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            finish();
            if (TargetActiveObj != null) SaveOne(TargetActiveObj);
            TargetActiveObj = null;
        }



        private void SaveOne(IGH_ActiveObject activeobj)
        {
            ActiveObjItem item = new ActiveObjItem(activeobj);
            ((ObservableCollection<ActiveObjItem>)DataContext).Add(item);
            dataGrid.SelectedIndex = ((ObservableCollection<ActiveObjItem>)DataContext).Count - 1;
        }

        private void _canvas_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Instances.CursorServer.AttachCursor(_canvas, "GH_Target");
            PointF pt = _canvas.Viewport.UnprojectPoint(e.Location);
            GH_RelevantObjectData gH_RelevantObjectData = _canvas.Document.RelevantObjectAtPoint(pt, GH_RelevantObjectFilter.Attributes);
            if (gH_RelevantObjectData != null)
            {
                IGH_DocumentObject obj = gH_RelevantObjectData.Object;
                if (obj == null) return;
                obj = obj.Attributes.GetTopLevel.DocObject;

                IGH_ActiveObject actObj = (IGH_ActiveObject)obj;
                if (actObj == null) return;

                if (actObj == TargetActiveObj) return;
                TargetActiveObj = actObj;
                _canvas.Refresh();
            }
        }

        private void CanvasPostPaintWidgets(GH_Canvas canvas)
        {
            System.Drawing.Drawing2D.Matrix transform = canvas.Graphics.Transform;
            canvas.Graphics.ResetTransform();
            System.Drawing.Rectangle clientRectangle = canvas.ClientRectangle;
            clientRectangle.Inflate(5, 5);
            Region region = new Region(clientRectangle);
            System.Drawing.Rectangle rect = System.Drawing.Rectangle.Empty;
            if (TargetActiveObj != null)
            {
                RectangleF bounds = TargetActiveObj.Attributes.Bounds;
                rect = GH_Convert.ToRectangle(canvas.Viewport.ProjectRectangle(bounds));
                rect.Inflate(2, 2);
                region.Exclude(rect);
            }
            SolidBrush solidBrush = new SolidBrush(System.Drawing.Color.FromArgb(180, System.Drawing.Color.White));
            canvas.Graphics.FillRegion(solidBrush, region);
            solidBrush.Dispose();
            region.Dispose();
            if (TargetActiveObj != null)
            {
                System.Drawing.Color color = System.Drawing.Color.OliveDrab;

                canvas.Graphics.DrawRectangle(new System.Drawing.Pen(color), rect);
                System.Drawing.Pen pen = new System.Drawing.Pen(color, 3f);
                int num = 6;
                int num2 = rect.Left - 4;
                int num3 = rect.Right + 4;
                int num4 = rect.Top - 4;
                int num5 = rect.Bottom + 4;
                canvas.Graphics.DrawLines(pen, new System.Drawing.Point[3]
                {
                    new System.Drawing.Point(num2 + num, num4),
                    new System.Drawing.Point(num2, num4),
                    new System.Drawing.Point(num2, num4 + num)
                });
                canvas.Graphics.DrawLines(pen, new System.Drawing.Point[3]
                {
                    new System.Drawing.Point(num3 - num, num4),
                    new System.Drawing.Point(num3, num4),
                    new System.Drawing.Point(num3, num4 + num)
                });
                canvas.Graphics.DrawLines(pen, new System.Drawing.Point[3]
                {
                    new System.Drawing.Point(num2 + num, num5),
                    new System.Drawing.Point(num2, num5),
                    new System.Drawing.Point(num2, num5 - num)
                });
                canvas.Graphics.DrawLines(pen, new System.Drawing.Point[3]
                {
                    new System.Drawing.Point(num3 - num, num5),
                    new System.Drawing.Point(num3, num5),
                    new System.Drawing.Point(num3, num5 - num)
                });
                pen.Dispose();
            }
            canvas.Graphics.Transform = transform;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            int index = dataGrid.SelectedIndex;
            if (index == -1) return;
            ((ObservableCollection<ActiveObjItem>)DataContext).RemoveAt(index);
            dataGrid.SelectedIndex = Math.Min(index, ((ObservableCollection<ActiveObjItem>)DataContext).Count - 1);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancel = true;
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_cancel)
            {
                Data.NoAsyncObjects = _preList;
            }
            else
            {
                List<Guid> ids = new ();
                foreach (ActiveObjItem item in (ObservableCollection<ActiveObjItem>)DataContext)
                {
                    ids.Add(item.Guid);
                }
                Data.NoAsyncObjects = ids;
            }
            base.OnClosed(e);
        }
    }

    [ValueConversion(typeof(int), typeof(bool))]
    public class GridSelectedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            int grid = (int)value;

            return grid != -1;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    [ValueConversion(typeof(Bitmap), typeof(BitmapImage))]
    public class BitmapConverter : IValueConverter
    {
        public static BitmapImage ToImageSource(Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            BitmapImage image = new BitmapImage();

            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();
            return image;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            System.Drawing.Bitmap picture = (System.Drawing.Bitmap)value;
            return ToImageSource(picture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
