using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Controls.Ribbon;
using Image = System.Windows.Controls.Image;
using Pen = System.Drawing.Pen;
using Point = System.Windows.Point;

namespace DigitizePlot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Bitmap mainBitmap;
        double zoomScale = 3;
        float markerWidth = 4;
        public double MainOpacity { get; set; } = 0.25;
        public SolidColorBrush LineColor { get; set; } = new SolidColorBrush(Colors.Red); 
        Data? movingDatum = null;
        public ObservableCollection<Data> data { get; set; } = new ObservableCollection<Data>();

        public MainWindow()
        {
            InitializeComponent();
            // Set the window as the data context so bindings in XAML can resolve
            this.DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainBitmap = createBitmap(MainImage);
            ResultsGrid.ItemsSource = data;
        }

        private void MainImage_Drop(object sender, DragEventArgs e)
        {
            var image = (Image)sender;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ImageSourceConverter converter = new ImageSourceConverter();
            foreach (string file in files)
            {
                if (converter.IsValid(file))
                {
                    var uri = new Uri(file);
                    var bitmap = new BitmapImage(uri);
                    image.Source = bitmap;
                    mainBitmap = createBitmap(image);
                    break;
                }
            }
        }

        private void MainImage_MouseMove(object sender, MouseEventArgs e)
        {
            var image = (Image)sender;
            var pos = e.GetPosition(image);
            if (null != movingDatum)
            {
                Canvas.SetLeft(movingDatum.Marker, pos.X - markerWidth);
                Canvas.SetTop(movingDatum.Marker, pos.Y - markerWidth);
                movingDatum.Local = new Point(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
            }
            updateSubImage(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
        }

        private Bitmap createBitmap(Image image)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BitmapEncoder enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create((BitmapSource)image.Source));
                    enc.Save(ms);
                    return new Bitmap(ms);
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void updateSubImage(double x, double y)
        {
            try
            {
                float w = mainBitmap.Width;
                float h = mainBitmap.Height;
                float width = (float)(SubImage.ActualWidth / zoomScale);
                float height = (float)(SubImage.ActualHeight / zoomScale);
                float left = (float)(x * w - width / 2);
                float top = (float)(y * h - height / 2);

                Bitmap image = new Bitmap((int)width, (int)height, mainBitmap.PixelFormat);
                var g = Graphics.FromImage(image);
                g.FillRectangle(new SolidBrush(System.Drawing.Color.Black), 0, 0, width, height);

                var _width = width;
                var _height = height;
                var _left = Math.Max(0, -left);
                var _top = Math.Max(0, -top);
                if (left < 0)
                {
                    _width += left;
                    left = 0;
                }
                if (left > w - width)
                {
                    _width = w - left;
                }
                if (top < 0)
                {
                    _height += top;
                    top = 0;
                }
                if (top > h - height)
                {
                    _height = h - top;
                }
                RectangleF cloneRect = new RectangleF(left, top, _width, _height);
                Bitmap crop = mainBitmap.Clone(cloneRect, mainBitmap.PixelFormat);

                g.DrawImage(crop, _left, _top, _width, _height);
                foreach (var datum in data)
                {
                    var pen = new Pen(new SolidBrush(System.Drawing.Color.Red), width = 1);
                    if (datum.Label.StartsWith("Origin"))
                    {
                        pen.Color = System.Drawing.Color.Black;
                    }
                    else if (datum.Label.StartsWith("X"))
                    {
                        pen.Color = System.Drawing.Color.Green;
                    }
                    if (datum.Label.StartsWith("Y"))
                    {
                        pen.Color = System.Drawing.Color.Blue;
                    }
                    float l = (float)(datum.Local.X * w - left - markerWidth);
                    float t = (float)(datum.Local.Y * h - top - markerWidth);
                    RectangleF rect = new RectangleF(l, t, 2 * markerWidth, 2 * markerWidth);
                    g.DrawEllipse(pen, rect);
                }

                BitmapImage bi = new BitmapImage();
                MemoryStream ms = new MemoryStream();
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.EndInit();
                SubImage.Source = bi;
            }
            catch (Exception ex)
            {
            }
        }

        private void SubImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var w = e.NewSize.Width;
            var h = e.NewSize.Height;
            HLine.X1 = 0;
            HLine.Y1 = h / 2;
            HLine.X2 = w;
            HLine.Y2 = h / 2;
            VLine.X1 = w / 2;
            VLine.Y1 = 0;
            VLine.X2 = w / 2;
            VLine.Y2 = h;
            foreach (var item in data)
            {
                var ellipse = item.Marker;
                var local = item.Local;
                var x = local.X * MainImage.ActualWidth;
                var y = local.Y * MainImage.ActualHeight;
                Canvas.SetLeft(ellipse, x - markerWidth);
                Canvas.SetTop(ellipse, y - markerWidth);
            }
        }

        private void MainImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var image = (Image)sender;
            var pos = e.GetPosition(image);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                foreach (var datum in data)
                {
                    if (Math.Abs(Canvas.GetLeft(datum.Marker) + markerWidth - pos.X) <= markerWidth && Math.Abs(Canvas.GetTop(datum.Marker) + markerWidth - pos.Y) <= markerWidth)
                    {
                        if (!datum.Label.StartsWith("Point") || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            //Move data point
                            movingDatum = datum;
                        }
                        else
                        {
                            //Delete data point
                            MainCanvas.Children.Remove(datum.Marker);
                            data.Remove(datum);
                        }
                        UpdateData();
                        return;
                    }
                }

                //Add data point
                Data newDatum = new Data();
                data.Add(newDatum);

                newDatum.Local = new Point(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
                var color = Colors.Red;
                if (data.Count == 1)
                {
                    newDatum.IsVisible = false;
                    newDatum.Label = "Origin";

                    double X = 0;
                    double Y = 0;
                    if (OriginX.Text != "") double.TryParse(OriginX.Text, out X);
                    if (OriginY.Text != "") double.TryParse(OriginY.Text, out Y);
                    newDatum.X = X;
                    newDatum.Y = Y;
                    color = Colors.Black;
                }
                else if (data.Count == 2)
                {
                    newDatum.IsVisible = false;
                    newDatum.Label = "X Axis";
                    double X = 1;
                    double Y = 0;
                    if (XAxis.Text != "") double.TryParse(XAxis.Text, out X);
                    if (OriginY.Text != "") double.TryParse(OriginY.Text, out Y);
                    newDatum.X = X;
                    newDatum.Y = Y;
                    color = Colors.Green;
                }
                else if (data.Count == 3)
                {
                    newDatum.IsVisible = false;
                    newDatum.Label = "Y Axis";
                    double X = 0;
                    double Y = 1;
                    if (OriginX.Text != "") double.TryParse(OriginX.Text, out X);
                    if (YAxis.Text != "") double.TryParse(YAxis.Text, out Y);
                    newDatum.X = X;
                    newDatum.Y = Y;
                    color = Colors.Blue;
                }
                else
                {
                    newDatum.Label = "Point";
                }

                var ellipse = new Ellipse()
                {
                    Width = 2 * markerWidth,
                    Height = 2 * markerWidth,
                    Fill = new SolidColorBrush(Colors.Transparent),
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 2
                };
                MainCanvas.Children.Add(ellipse);
                Canvas.SetLeft(ellipse, pos.X - markerWidth);
                Canvas.SetTop(ellipse, pos.Y - markerWidth);

                newDatum.Marker = ellipse;
                UpdateData();
            }
        }

        private void MainImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (null != movingDatum)
            {
                movingDatum = null;
            }
        }

        private double Dot(Vector A, Vector B)
        {
            return A.X * B.X + A.Y * B.Y;
        }

        private void UpdateData()
        {
            var points = data.Where(x => x.Label.StartsWith("Point")).ToList().OrderBy(x => x.Local.X).ToList();
            while (data.Count > 3)
            {
                data.RemoveAt(3);
            }
            foreach (var point in points)
            {
                data.Add(point);
            }
            ResultsGrid.ItemsSource = null;
            int i = 1;
            foreach (var datum in data)
            {
                if (datum.Label.StartsWith("Point"))
                {
                    var P = datum.Local - data[0].Local;
                    var A = data[1].Local - data[0].Local;
                    var B = data[2].Local - data[0].Local;
                    var A2 = Dot(A, A);
                    var B2 = Dot(B, B);
                    var AB = Dot(A, B);
                    var PA = Dot(P, A);
                    var PB = Dot(P, B);
                    var a = (PA - PB * AB / B2) / (A2 - AB * AB / B2);
                    var b = (PB - PA * AB / A2) / (B2 - AB * AB / A2);
                    datum.X = data[0].X + a * (data[1].X - data[0].X);
                    datum.Y = data[0].Y + b * (data[2].Y - data[0].Y);
                    datum.Label = "Point " + i++;
                }
            }
            ResultsGrid.ItemsSource = data;
            if (data.Count > 0) OriginX.Text = data[0].X.ToString();
            if (data.Count > 0) OriginY.Text = data[0].Y.ToString();
            if (data.Count > 1) XAxis.Text = data[1].X.ToString();
            if (data.Count > 2) YAxis.Text = data[2].Y.ToString();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (MainImage.IsMouseOver)
            {
                if (e.Key == Key.V && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    if (Clipboard.ContainsImage())
                    {
                        MainImage.Source = Clipboard.GetImage();
                        mainBitmap = createBitmap(MainImage);
                    }
                }
                else if (e.Key == Key.C && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var datum in data)
                    {
                        if (datum.Label.StartsWith("Point"))
                        {
                            sb.AppendLine(datum.X + "\t" + datum.Y);
                        }
                    }
                    Clipboard.SetText(sb.ToString());
                }
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            double value = 0;
            if (double.TryParse(tb.Text, out value))
            {
                switch (tb.Name)
                {
                    case "OriginX":
                        if (data.Count > 0) data[0].X = value;
                        break;
                    case "OriginY":
                        if (data.Count > 0) data[0].Y = value;
                        break;
                    case "XAxis":
                        if (data.Count > 1) data[1].X = value;
                        break;
                    case "YAxis":
                        if (data.Count > 2) data[2].Y = value;
                        break;
                }
                UpdateData();
            }
        }

        private void Ribbon_Loaded(object sender, RoutedEventArgs e)
        {
            Grid child = VisualTreeHelper.GetChild((DependencyObject)sender, 0) as Grid;
            if (child != null)
            {
                child.RowDefinitions[0].Height = new GridLength(0);
                child.RowDefinitions[1].Height = new GridLength(0);
            }
        }

        private void fileNew_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public class Data
    {
        public bool IsVisible { get; set; } = true;
        public Ellipse Marker { get; set; } = new Ellipse();
        public Point Local { get; set; } = new Point(0, 0);
        public string Label { get; set; } = "";
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
    }
}