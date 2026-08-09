using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
                    float l = (float)(datum.Local.X * w - left - MarkerWidth);
                    float t = (float)(datum.Local.Y * h - top - MarkerWidth);
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

                UpdateData();
            }
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
            if (MainImage.IsMouseOver)
            {
                if (e.Key == Key.V && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    ClipboardIn();
                }
                else if (e.Key == Key.C && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    ClipboardOut();
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
            Clipboard.SetText(sb.ToString());
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
                    case "tbOpacity":
                        MainImage.Opacity = value;
                        break;
                    case "tbMagnify":
                        ZoomScale = value;
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

                var serializer = new XmlSerializer(typeof(ObservableCollection<Data>));
                ObservableCollection<Data>? tempData = null;

                using (Stream reader = new FileStream(filename, FileMode.Open))
                {
                    var xml = serializer.Deserialize(reader);
                    if (null != xml)
                    {
                        tempData = (ObservableCollection<Data>)xml;
                    }
                }
                if (null != tempData)
                {
                    data.Clear();
                    MainCanvas.Children.Clear();
                    foreach (var datum in tempData)
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