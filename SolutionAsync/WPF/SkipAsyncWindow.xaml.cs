using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using SimpleGrasshopper.Util;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using Point = System.Drawing.Point;

namespace SolutionAsync.WPF;

/// <summary>
///     Interaction logic for SkipAsyncWindow.xaml
/// </summary>
public partial class SkipAsyncWindow : Window
{
    private readonly GH_Canvas _canvas = Instances.ActiveCanvas;


    private readonly List<Guid> _preList;
    private bool _cancel;

    public SkipAsyncWindow()
    {
        ObservableCollection<ActiveObjItem> structureLists = new();
        foreach (var guid in Data.NoAsyncObjects) structureLists.Add(new ActiveObjItem(guid));

        DataContext = structureLists;
        _preList = Data.NoAsyncObjects;

        InitializeComponent();

        using MemoryStream ms = new();
        typeof(SolutionAsyncInfo).Assembly.GetBitmap("SolutionAsyncIcon_24.png").Save(ms, ImageFormat.Png);

        BitmapImage ImageIcon = new();
        ImageIcon.BeginInit();
        ms.Seek(0, SeekOrigin.Begin);
        ImageIcon.StreamSource = ms;
        ImageIcon.EndInit();
        Icon = ImageIcon;
    }

    private IGH_ActiveObject TargetActiveObj { get; set; }

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

    private void _canvas_MouseUp(object sender, MouseEventArgs e)
    {
        finish();
        if (TargetActiveObj != null) SaveOne(TargetActiveObj);
        TargetActiveObj = null;
    }


    private void SaveOne(IGH_ActiveObject activeobj)
    {
        var item = new ActiveObjItem(activeobj);
        ((ObservableCollection<ActiveObjItem>)DataContext).Add(item);
        dataGrid.SelectedIndex = ((ObservableCollection<ActiveObjItem>)DataContext).Count - 1;
    }

    private void _canvas_MouseMove(object sender, MouseEventArgs e)
    {
        Instances.CursorServer.AttachCursor(_canvas, "GH_Target");
        var pt = _canvas.Viewport.UnprojectPoint(e.Location);
        var gH_RelevantObjectData = _canvas.Document.RelevantObjectAtPoint(pt, GH_RelevantObjectFilter.Attributes);
        if (gH_RelevantObjectData != null)
        {
            var obj = gH_RelevantObjectData.Object;
            if (obj == null) return;
            obj = obj.Attributes.GetTopLevel.DocObject;

            var actObj = (IGH_ActiveObject)obj;
            if (actObj == null) return;

            if (actObj == TargetActiveObj) return;
            TargetActiveObj = actObj;
            _canvas.Refresh();
        }
    }

    private void CanvasPostPaintWidgets(GH_Canvas canvas)
    {
        var transform = canvas.Graphics.Transform;
        canvas.Graphics.ResetTransform();
        var clientRectangle = canvas.ClientRectangle;
        clientRectangle.Inflate(5, 5);
        var region = new Region(clientRectangle);
        var rect = Rectangle.Empty;
        if (TargetActiveObj != null)
        {
            var bounds = TargetActiveObj.Attributes.Bounds;
            rect = GH_Convert.ToRectangle(canvas.Viewport.ProjectRectangle(bounds));
            rect.Inflate(2, 2);
            region.Exclude(rect);
        }

        var solidBrush = new SolidBrush(Color.FromArgb(180, Color.White));
        canvas.Graphics.FillRegion(solidBrush, region);
        solidBrush.Dispose();
        region.Dispose();
        if (TargetActiveObj != null)
        {
            var color = Color.OliveDrab;

            canvas.Graphics.DrawRectangle(new Pen(color), rect);
            var pen = new Pen(color, 3f);
            var num = 6;
            var num2 = rect.Left - 4;
            var num3 = rect.Right + 4;
            var num4 = rect.Top - 4;
            var num5 = rect.Bottom + 4;
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new(num2 + num, num4),
                new(num2, num4),
                new(num2, num4 + num)
            });
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new(num3 - num, num4),
                new(num3, num4),
                new(num3, num4 + num)
            });
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new(num2 + num, num5),
                new(num2, num5),
                new(num2, num5 - num)
            });
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new(num3 - num, num5),
                new(num3, num5),
                new(num3, num5 - num)
            });
            pen.Dispose();
        }

        canvas.Graphics.Transform = transform;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var index = dataGrid.SelectedIndex;
        if (index == -1) return;
        ((ObservableCollection<ActiveObjItem>)DataContext).RemoveAt(index);
        dataGrid.SelectedIndex = Math.Min(index, ((ObservableCollection<ActiveObjItem>)DataContext).Count - 1);
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancel = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_cancel)
        {
            Data.NoAsyncObjects = _preList;
        }
        else
        {
            List<Guid> ids = new();
            foreach (var item in (ObservableCollection<ActiveObjItem>)DataContext) ids.Add(item.Guid);
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

        var grid = (int)value;

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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return null;

        var picture = (Bitmap)value;
        return ToImageSource(picture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }

    public static BitmapImage ToImageSource(Bitmap bitmap)
    {
        var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        var image = new BitmapImage();

        image.BeginInit();
        ms.Seek(0, SeekOrigin.Begin);
        image.StreamSource = ms;
        image.EndInit();
        return image;
    }
}