using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Xml;
using System.Xml.Serialization;
using static System.Net.Mime.MediaTypeNames;
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
        public bool AutoMode { get; set; } = false;
        public double AutoTolerance { get; set; } = 35;
        public double AutoSpacing { get; set; } = 20;
        public bool SortMode { get; set; } = true;
        public double ZoomScale { get; set; } = 3;
        public float MarkerWidth { get; set; } = 4;
        public string StyleX { get; set; } = "Linear";
        public string StyleY { get; set; } = "Linear";
        public double MainOpacity { get; set; } = 0.25;
        public SolidColorBrush LineColor { get; set; } = new SolidColorBrush(Colors.Red); 
        Data? movingDatum = null;
        public ObservableCollection<Data> data { get; set; } = new ObservableCollection<Data>();

        public MainWindow()
        {
            InitializeComponent();
            // Set the window as the data context so bindings in XAML can resolve
            this.DataContext = this;
            rcbTypeX.Items.Add("Linear");
            rcbTypeX.Items.Add("Logarithmic");
            rcbTypeY.Items.Add("Linear");
            rcbTypeY.Items.Add("Logarithmic");
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
                Canvas.SetLeft(movingDatum.Marker, pos.X - MarkerWidth);
                Canvas.SetTop(movingDatum.Marker, pos.Y - MarkerWidth);
                movingDatum.Local = new Point(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
            }
            updateSubImage(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
        }

        private Bitmap? createBitmap(Image image)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BitmapMetadata metadata = new BitmapMetadata("png");
                    BitmapFrame frame = BitmapFrame.Create((BitmapSource)image.Source, null, metadata, null);

                    BitmapEncoder enc = new PngBitmapEncoder();
                    enc.Frames.Add(frame);
                    enc.Save(ms);
                    ms.Position = 0;
                    using (var temp = new Bitmap(ms))
                    {
                        // Return a fully independent copy that does not depend on the stream
                        return new Bitmap(temp);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        private void updateSubImage(double x, double y)
        {
            try
            {
                float w = mainBitmap.Width;
                float h = mainBitmap.Height;
                float width = (float)(SubImage.ActualWidth / ZoomScale);
                float height = (float)(SubImage.ActualHeight / ZoomScale);
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
                    float l = (float)(datum.Local.X * w - left + _left - MarkerWidth);
                    float t = (float)(datum.Local.Y * h - top + _top - MarkerWidth);
                    RectangleF rect = new RectangleF(l, t, 2 * MarkerWidth, 2 * MarkerWidth);
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
                MessageBox.Show(ex.Message);
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
                Canvas.SetLeft(ellipse, x - MarkerWidth);
                Canvas.SetTop(ellipse, y - MarkerWidth);
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
                    if (Math.Abs(Canvas.GetLeft(datum.Marker) + MarkerWidth - pos.X) <= MarkerWidth && Math.Abs(Canvas.GetTop(datum.Marker) + MarkerWidth - pos.Y) <= MarkerWidth)
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
                if (AutoMode)
                {
                    AutoDigitize(image, pos);
                }
                else
                {
                    AddDataPoint(image, pos);
                }

                UpdateData();
            }
        }

        private void AutoDigitize(Image image, Point pos)
        {
            if (data.Count < 3)
            {
                AddDataPoint(image, pos);
                return;
            }

            var working = mainBitmap.Clone(new RectangleF(0, 0, mainBitmap.Width, mainBitmap.Height), mainBitmap.PixelFormat);
            int x0 = (int)(pos.X / image.ActualWidth * working.Width);
            int y0 = (int)(pos.Y / image.ActualHeight * working.Height);
            var p0 = working.GetPixel(x0, y0);
            var inv = System.Drawing.Color.FromArgb((p0.R + 128) % 256, (p0.G + 128) % 256, (p0.B + 128) % 256);
            //var hue0 = Hue(p0);

            var x1 = x0;
            var y1 = y0;
            var stack = new Stack<(int, int)>();
            stack.Push((x1, y1));
            working.SetPixel(x1, y1, inv);
            var tol = 3 * AutoTolerance * AutoTolerance;
            List<Point> allData = new List<Point>();
            while (stack.Count > 0)
            {
                (x1, y1) = stack.Pop();
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        var x2 = x1 + i;
                        var y2 = y1 + j;
                        if (x2 < 0 || x2 >= working.Width || y2 < 0 || y2 >= working.Height) continue;
                        var p2 = working.GetPixel(x2, y2);
                        if (p2 == inv) continue;
                        //var diff = Math.Abs(hue0 - Hue(p2));
                        var diff = (p2.R - p0.R) * (p2.R - p0.R) + (p2.G - p0.G) * (p2.G - p0.G) + (p2.B - p0.B) * (p2.B - p0.B);
                        if (diff >= tol) continue;
                        stack.Push((x2, y2));
                        working.SetPixel(x2, y2, inv);
                        allData.Add(new Point(x2 * image.ActualWidth / working.Width, y2 * image.ActualHeight / working.Height));
                    }
                }
            }

            allData = allData.OrderBy(p => p.X).ToList();
            var last = allData[0];
            AddDataPoint(image, last);
            foreach (var point in allData)
            {
                if (point.X - last.X > AutoSpacing)
                {
                    last = point;
                    AddDataPoint(image, last);
                }
            }

            //mainBitmap = working;
        }

        private double Hue(System.Drawing.Color color)
        {
            var R = color.R / 255.0;
            var G = color.G / 255.0;
            var B = color.B / 255.0;
            var min = Math.Min(R, Math.Min(G, B));
            var max = Math.Max(R, Math.Max(G, B));
            var diff = max - min;
            if (diff == 0) return 0;
            var hue = 0.0;
            if (R > G && R > B)
            {
                hue = (G - B) / (max - min);
            }
            else if (G > B && G > R)
            {
                hue = 2 + (B - R) / (max - min);
            }
            else
            {
                hue = 4 + (R - G) / (max - min);
            }
            return hue < 0 ? 360 + 60 * hue : 60 * hue;
        }

        private void AddDataPoint(Image image, Point pos)
        {
            Data newDatum = new Data();
            data.Add(newDatum);

            newDatum.Local = new Point(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
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
            }
            else
            {
                newDatum.Label = "Point";
            }
            AddEllipse(newDatum);
        }

        private void MainImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (null != movingDatum)
            {
                movingDatum = null;
                UpdateData();
            }
        }

        private double Dot(Vector A, Vector B)
        {
            return A.X * B.X + A.Y * B.Y;
        }

        private void UpdateData()
        {
            if (SortMode)
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
            }
            ResultsGrid.ItemsSource = null;
            int i = 1;
            foreach (var datum in data)
            {
                if (StyleX == "Logarithmic")
                    datum.X = Math.Log10(datum.X);
                if (StyleY == "Logarithmic")
                    datum.Y = Math.Log10(datum.Y);
            }
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
            foreach (var datum in data)
            {
                if (StyleX == "Logarithmic")
                    datum.X = Math.Pow(10, datum.X);
                if (StyleY == "Logarithmic")
                    datum.Y = Math.Pow(10, datum.Y);
            }
            ResultsGrid.ItemsSource = data;
            if (data.Count > 0) OriginX.Text = data[0].X.ToString();
            if (data.Count > 0) OriginY.Text = data[0].Y.ToString();
            if (data.Count > 1) XAxis.Text = data[1].X.ToString();
            if (data.Count > 2) YAxis.Text = data[2].Y.ToString();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (MainImage.IsMouseOver || MainCanvas.IsMouseOver)
            {
                if (isCtrl)
                {
                    if (e.Key == Key.V)
                    {
                        ClipboardIn();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.C)
                    {
                        ClipboardOut();
                        e.Handled = true;
                    }
                }
            }
        }

        private void ClipboardIn()
        {
            if (Clipboard.ContainsImage())
            {
                MainImage.Source = Clipboard.GetImage();
                mainBitmap = createBitmap(MainImage);
            }
        }

        private void ClipboardOut()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var datum in data)
            {
                if (datum.Label.StartsWith("Point"))
                {
                    sb.AppendLine(datum.X + "\t" + datum.Y);
                }
            }
            sb.AppendLine();
            Clipboard.Clear();
            Clipboard.SetText(sb.ToString());
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateTextBox(sender);
        }

        private void UpdateTextBox(object sender)
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
                    case "tbOpacity":
                        MainImage.Opacity = value;
                        break;
                    case "tbMagnify":
                        ZoomScale = value;
                        break;
                    case "tbTolerance":
                        AutoTolerance = value;
                        break;
                    case "tbSpacing":
                        AutoSpacing = value;
                        break;
                }
                UpdateData();
            }
        }

        private void Ribbon_Loaded(object sender, RoutedEventArgs e)
        {
            Grid child = (Grid)VisualTreeHelper.GetChild((DependencyObject)sender, 0);
            if (child != null)
            {
                child.RowDefinitions[0].Height = new GridLength(0);
                child.RowDefinitions[1].Height = new GridLength(0);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            data.Clear();
            MainCanvas.Children.Clear();
            UpdateData();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "DigitizePlot"; // Default file name
            dialog.DefaultExt = ".xml"; // Default file extension
            dialog.Filter = "Digitized data (.xml)|*.xml"; // Filter files by extension

            // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                string filename = dialog.FileName;
                List<Data>? tempList = null;

                try
                {
                    var serializer = new XmlSerializer(typeof(List<Data>));
                    using (var fs = new FileStream(filename, FileMode.Open))
                    {
                        tempList = (List<Data>?)serializer.Deserialize(fs);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                if (null != tempList)
                {
                    data.Clear();
                    MainCanvas.Children.Clear();
                    foreach (var datum in tempList)
                    {
                        AddEllipse(datum);
                        data.Add(datum);
                    }
                    UpdateData();
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "DigitizePlot"; // Default file name
            dlg.DefaultExt = ".xml"; // Default file extension
            dlg.Filter = "Digitized data (.xml)|*.xml"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                string filename = dlg.FileName;

                using (Stream writer = new FileStream(filename, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Data>));
                    serializer.Serialize(writer, data);
                }
            }
        }

        private void AddEllipse(Data datum)
        {
            var color = Colors.Red;
            if (datum.Label.StartsWith("Origin"))
            {
                color = Colors.Black;
            }
            else if (datum.Label.StartsWith("X"))
            {
                color = Colors.Green;
            }
            else if (datum.Label.StartsWith("Y"))
            {
                color = Colors.Blue;
            }
            var ellipse = new Ellipse()
            {
                Width = 2 * MarkerWidth,
                Height = 2 * MarkerWidth,
                Fill = new SolidColorBrush(Colors.Transparent),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2
            };
            MainCanvas.Children.Add(ellipse);
            var pos = new Point(datum.Local.X * MainImage.ActualWidth, datum.Local.Y * MainImage.ActualHeight);
            Canvas.SetLeft(ellipse, pos.X - MarkerWidth);
            Canvas.SetTop(ellipse, pos.Y - MarkerWidth);
            datum.Marker = ellipse;
        }

        private void ClipboardIn_Click(object sender, RoutedEventArgs e)
        {
            ClipboardIn();
        }

        private void ClipboardOut_Click(object sender, RoutedEventArgs e)
        {
            ClipboardOut();
        }

        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "Image"; // Default file name
            dialog.DefaultExt = ".png"; // Default file extension
            dialog.Filter = "Image files|*.png;*.bmp;*.jpg|All files|*.*"; // Filter files by extension

            // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                string filename = dialog.FileName;

                var uri = new Uri(filename);
                var bitmap = new BitmapImage(uri);
                MainImage.Source = bitmap;
                mainBitmap = createBitmap(MainImage);
            }
        }

        private void Points_Click(object sender, RoutedEventArgs e)
        {
            while (data.Count > 3)
            {
                MainCanvas.Children.Remove(data[3].Marker);
                data.RemoveAt(3);
            }
            UpdateData();
        }

        private void Type_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            UpdateData();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            Help help = new Help();
            help.Show();
        }

        private void cbSort_Checked(object sender, RoutedEventArgs e)
        {
            UpdateData();
        }

        private void TextBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            UpdateTextBox(sender);
        }
    }

    public class Data
    {
        public bool IsVisible { get; set; } = true;
        [XmlIgnore]
        public Ellipse Marker { get; set; } = new Ellipse();
        public Point Local { get; set; } = new Point(0, 0);
        public string Label { get; set; } = "";
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
    }
}