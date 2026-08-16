using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        public Version? Version { get { return new Version(1, 2); } }

        Bitmap? mainBitmap = null;
        Data? movingDatum = null;
        System.Windows.Shapes.Rectangle? groupRect = null;
        Point groupPos;
        Stack<MemoryStream> undoStack = new Stack<MemoryStream>();
        Stack<MemoryStream> redoStack = new Stack<MemoryStream>();

        public bool AxisGuides { get; set; } = false;
        public bool NearestNeighbour { get; set; } = false;
        public bool AutoMode { get; set; } = false;
        public double AutoTolerance { get; set; } = 35;
        public double AutoSpacing { get; set; } = 20;
        public string AutoSpaceType { get; set; } = "X Distance";
        public int PixelStep { get; set; } = 1;
        public bool SortMode { get; set; } = true;
        public double ZoomScale { get; set; } = 3;
        public float MarkerWidth { get; set; } = 4;
        public string StyleX { get; set; } = "Linear";
        public string StyleY { get; set; } = "Linear";
        public double MainOpacity { get; set; } = 0.25;
        public SolidColorBrush LineColor { get; set; } = new SolidColorBrush(Colors.Red); 
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
            rcbTypeSpacing.Items.Add("X Distance");
            rcbTypeSpacing.Items.Add("Distance");
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();
            if (Properties.Settings.Default.WinState > 0) WindowState = (WindowState)Properties.Settings.Default.WinState;
            if (Properties.Settings.Default.WinTop > 0) Top = Properties.Settings.Default.WinTop;
            if (Properties.Settings.Default.WinLeft > 0) Left = Properties.Settings.Default.WinLeft;
            if (Properties.Settings.Default.WinWidth > 0) Width = Properties.Settings.Default.WinWidth;
            if (Properties.Settings.Default.WinHeight > 0) Height = Properties.Settings.Default.WinHeight;
            MainOpacity = Properties.Settings.Default.Opacity;
            ZoomScale = Properties.Settings.Default.Magnify;
            ZoomScale = Properties.Settings.Default.Magnify;
            SortMode = Properties.Settings.Default.SortMode;
            AutoMode = Properties.Settings.Default.AutoMode;
            AutoTolerance = Properties.Settings.Default.AutoTolerance;
            PixelStep = Properties.Settings.Default.PixelStep;
            AutoSpacing = Properties.Settings.Default.AutoSpacing;
            AutoSpaceType = Properties.Settings.Default.AutoSpaceType;
            AxisGuides = Properties.Settings.Default.AxisGuides;
            StyleX = Properties.Settings.Default.StyleX;
            StyleY = Properties.Settings.Default.StyleX;
            NearestNeighbour = Properties.Settings.Default.NearestNeighbour;

            if (Width < 200) Width = 1200;
            if (Height < 100) Height = 800;
            BitmapScaling();


            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            Action setAlignmentValue = () => {
                if (SystemParameters.MenuDropAlignment && menuDropAlignmentField != null) menuDropAlignmentField.SetValue(null, false);
            };
            setAlignmentValue();
            SystemParameters.StaticPropertyChanged += (sender, e) => { setAlignmentValue(); };

            RoutedEventArgs rea = new RoutedEventArgs();
            var menu = new ContextMenu();
            MainImage.ContextMenu = menu;
            var openImageMenuItem = new MenuItem()
            {
                Header = "Open and load a new image file",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/OpenImage.png")) }
            };
            openImageMenuItem.Click += (object _sender, RoutedEventArgs _e) => { OpenImage_Click(sender, rea); };
            menu.Items.Add(openImageMenuItem);
            var saveMenuItem = new MenuItem()
            {
                Header = "Save current point data",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/Save.png")) }
            };
            saveMenuItem.Click += (object _sender, RoutedEventArgs _e) => { Save_Click(sender, rea); };
            menu.Items.Add(saveMenuItem);
            var openMenuItem = new MenuItem()
            {
                Header = "Restore previously saved point data",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/Open.png")) }
            };
            openMenuItem.Click += (object _sender, RoutedEventArgs _e) => { Open_Click(sender, rea); };
            menu.Items.Add(openMenuItem);
            menu.Items.Add(new Separator());
            var newMenuItem = new MenuItem()
            {
                Header = "Delete all data points, including axis",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/New.png")) }
            };
            newMenuItem.Click += (object _sender, RoutedEventArgs _e) => { Reset_Click(sender, rea); };
            menu.Items.Add(newMenuItem);
            var clearMenuItem = new MenuItem()
            {
                Header = "Delete all data points, excluding axis",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/Clear.png")) }
            };
            clearMenuItem.Click += (object _sender, RoutedEventArgs _e) => { Points_Click(sender, rea); };
            menu.Items.Add(clearMenuItem);
            menu.Items.Add(new Separator());
            var copyMenuItem = new MenuItem()
            {
                Header = "Export digitized data to clipboard (Ctrl C)",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/ClipboardOut.png")) }
            };
            copyMenuItem.Click += (object _sender, RoutedEventArgs _e) => { ClipboardOut(); };
            menu.Items.Add(copyMenuItem);
            var pasteMenuItem = new MenuItem()
            {
                Header = "Paste image from clipboard (Ctrl V)",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/ClipboardIn.png")) }
            };
            pasteMenuItem.Click += (object _sender, RoutedEventArgs _e) => { ClipboardIn(); };
            menu.Items.Add(pasteMenuItem);
            menu.Items.Add(new Separator());
            var undoMenuItem = new MenuItem()
            {
                Header = "Undo last points operation (Ctrl Z)",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/Undo.png")) }
            };
            undoMenuItem.Click += (object _sender, RoutedEventArgs _e) => { Undo(); };
            menu.Items.Add(undoMenuItem);
            var redoMenuItem = new MenuItem()
            {
                Header = "Redo last points undo operation (Ctrl Y)",
                Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Images/Redo.png")) }
            };
            redoMenuItem.Click += (object _sender, RoutedEventArgs _e) => { Redo(); };
            menu.Items.Add(redoMenuItem);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.WinState = WindowState == WindowState.Minimized ? (int)WindowState.Normal : (int)WindowState;
            Properties.Settings.Default.WinTop = Top;
            Properties.Settings.Default.WinLeft = Left;
            Properties.Settings.Default.WinWidth = Width;
            Properties.Settings.Default.WinHeight = Height;
            Properties.Settings.Default.Version = Version?.ToString();
            Properties.Settings.Default.Opacity = MainOpacity;
            Properties.Settings.Default.Magnify = ZoomScale;
            Properties.Settings.Default.SortMode = SortMode;
            Properties.Settings.Default.AutoMode = AutoMode;
            Properties.Settings.Default.AutoTolerance = AutoTolerance;
            Properties.Settings.Default.PixelStep = PixelStep;
            Properties.Settings.Default.AutoSpacing = AutoSpacing;
            Properties.Settings.Default.AutoSpaceType = AutoSpaceType;
            Properties.Settings.Default.AxisGuides = AxisGuides;
            Properties.Settings.Default.StyleX = StyleX;
            Properties.Settings.Default.StyleX = StyleY;
            Properties.Settings.Default.NearestNeighbour = NearestNeighbour;

            Properties.Settings.Default.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainBitmap = createBitmap(MainImage);
            ResultsGrid.ItemsSource = data;
        }

        private void BitmapScaling()
        {
            MainImage.SetValue(RenderOptions.BitmapScalingModeProperty, NearestNeighbour ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            SubImage.SetValue(RenderOptions.BitmapScalingModeProperty, NearestNeighbour ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
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
                UpdateGuides(movingDatum);
            }
            if (null != groupRect)
            {
                var w = pos.X - groupPos.X;
                Canvas.SetLeft(groupRect, w > 0 ? groupPos.X :pos.X);
                groupRect.Width = Math.Abs(w);

                var h = pos.Y - groupPos.Y;
                Canvas.SetTop(groupRect, h > 0 ? groupPos.Y : pos.Y);
                groupRect.Height = Math.Abs(h);
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
                    statusImageSize.Content = "Image size " + (int)image.Source.Width + " x " + (int)image.Source.Height;

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
                MessageBox.Show("createBitmap: " + ex.Message);
                return null;
            }
        }

        private void updateSubImage(double x, double y)
        {
            if (null == mainBitmap) return;

            try
            {
                if (data.Count >= 3)
                {
                    var coord = GetCoords(new Point(x, y));
                    tbCoordX.Text = string.Format("X {0:G6}", coord.X);
                    tbCoordY.Text = string.Format("Y {0:G6}", coord.Y);
                }

                float w = mainBitmap.Width;
                float h = mainBitmap.Height;
                float width = (float)(SubImage.ActualWidth / ZoomScale);
                float height = (float)(SubImage.ActualHeight / ZoomScale);
                float left = (float)(x * w - width / 2);
                float top = (float)(y * h - height / 2);

                var color = mainBitmap.GetPixel((int)(x * w), (int)(y * h));
                Swatch.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));

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
            catch //(Exception ex)
            {
                //MessageBox.Show("updateSubImage: " + ex.Message);
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
            UpdateGuides();
        }

        private void MainImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var image = (Image)sender;
            var pos = e.GetPosition(image);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                undoStack.Push(SerialiseData());

                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    //Group select
                    groupRect = new System.Windows.Shapes.Rectangle()
                    {
                        Fill = new SolidColorBrush(Colors.Transparent),
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = 1,
                        StrokeDashArray = { 5, 5 },
                    };
                    MainCanvas.Children.Add(groupRect);
                    groupPos = pos;
                    UpdateData();
                    return;
                }
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
            if (null == mainBitmap) return;

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
                for (int i = -PixelStep; i <= PixelStep; i++)
                {
                    for (int j = -PixelStep; j <= PixelStep; j++)
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
                if (allData.Count > 100000)
                {
                    break;
                }
            }

            allData = allData.OrderBy(p => p.X).ToList();
            var dir = new Vector(0, 0);
            var last = allData.First();
            AddDataPoint(image, last);
            foreach (var point in allData)
            {
                var sep = point - last;
                sep.Normalize();
                var dot = sep.X * dir.X + sep.Y * dir.Y;
                var dist = (point - last).Length;
                if ((point.X - last.X > AutoSpacing) ||
                    (AutoSpaceType == "Distance" && dist > AutoSpacing &&
                        ((dot > 0.5 && point.X >= last.X) || //Same direction
                        (point.X >= last.X + 1)))) //Direction change
                {
                    dir = (point - last);
                    dir.Normalize();
                    AddDataPoint(image, point);
                    last = point;
                }
            }
            AddDataPoint(image, allData.Last());

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

        private bool AddDataPoint(Image image, Point pos)
        {
            Data newDatum = new Data();
            newDatum.Local = new Point(pos.X / image.ActualWidth, pos.Y / image.ActualHeight);
            //var near = data.Where(x => (x.Local - newDatum.Local).Length < 0.001).Count();
            var near = data.Where(x => x.Label.StartsWith("Point") && 
            Math.Abs(x.Local.X * image.ActualWidth - pos.X) < MarkerWidth &&
            Math.Abs(x.Local.Y * image.ActualHeight - pos.Y) < MarkerWidth).Count();
            if (near > 0) return false;

            data.Add(newDatum);
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
            return true;
        }

        private void MainImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var image = (Image)sender;
            var pos = e.GetPosition(image);

            if (null != movingDatum)
            {
                movingDatum = null;
                UpdateData();
            }
            if (null != groupRect)
            {
                List<Data> toDelete = new List<Data>();
                foreach (var datum in data)
                {
                    if (datum.Label.StartsWith("Point"))
                    {
                        var x = datum.Local.X * MainImage.ActualWidth;
                        var y = datum.Local.Y * MainImage.ActualHeight;
                        if (x > Math.Min(pos.X, groupPos.X) && x < Math.Max(pos.X, groupPos.X) &&
                            y > Math.Min(pos.Y, groupPos.Y) && y < Math.Max(pos.Y, groupPos.Y))
                        {
                            toDelete.Add(datum);
                        }
                    }
                }
                foreach (var datum in toDelete)
                {
                    MainCanvas.Children.Remove(datum.Marker);
                    data.Remove(datum);
                }

                MainCanvas.Children.Remove(groupRect);
                groupRect = null;
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
                if (datum.Label.StartsWith("Point"))
                {
                    var coord = GetCoords(datum.Local);
                    datum.X = coord.X;
                    datum.Y = coord.Y;
                    datum.Label = "Point " + i++;
                }
            }
            ResultsGrid.ItemsSource = data;
            if (data.Count > 0) OriginX.Text = data[0].X.ToString("G6");
            if (data.Count > 0) OriginY.Text = data[0].Y.ToString("G6");
            if (data.Count > 1) XAxis.Text = data[1].X.ToString("G6");
            if (data.Count > 2) YAxis.Text = data[2].Y.ToString("G6");

            var lines = MainCanvas.Children.OfType<Line>().ToList();
            if (lines.Count == 0)
            {
                for (i = 0; i < 2; i++)
                {
                    MainCanvas.Children.Add(new Line()
                    {
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = 1,
                        Visibility = Visibility.Collapsed,
                    });
                    MainCanvas.Children.Add(new Line()
                    {
                        Stroke = new SolidColorBrush(Colors.Green),
                        StrokeThickness = 1,
                        Visibility = Visibility.Collapsed,
                    });
                    MainCanvas.Children.Add(new Line()
                    {
                        Stroke = new SolidColorBrush(Colors.Blue),
                        StrokeThickness = 1,
                        Visibility = Visibility.Collapsed,
                    });
                }
            }
            UpdateGuides();
        }

        private Point GetCoords(Point local)
        {
            for (int i = 0; i < 3; i++)
            {
                var datum = data[i];
                if (StyleX == "Logarithmic")
                    datum.X = Math.Log10(datum.X);
                if (StyleY == "Logarithmic")
                    datum.Y = Math.Log10(datum.Y);
            }
            var P = local - data[0].Local;
            var A = data[1].Local - data[0].Local;
            var B = data[2].Local - data[0].Local;
            var A2 = Dot(A, A);
            var B2 = Dot(B, B);
            var AB = Dot(A, B);
            var PA = Dot(P, A);
            var PB = Dot(P, B);
            var a = (PA - PB * AB / B2) / (A2 - AB * AB / B2);
            var b = (PB - PA * AB / A2) / (B2 - AB * AB / A2);
            var X = data[0].X + a * (data[1].X - data[0].X);
            var Y = data[0].Y + b * (data[2].Y - data[0].Y);
            if (StyleX == "Logarithmic")
                X = Math.Pow(10, X);
            if (StyleY == "Logarithmic")
                Y = Math.Pow(10, Y);
            for (int i = 0; i < 3; i++)
            {
                var datum = data[i];
                if (StyleX == "Logarithmic")
                    datum.X = Math.Pow(10, datum.X);
                if (StyleY == "Logarithmic")
                    datum.Y = Math.Pow(10, datum.Y);
            }
            return new Point(X, Y);
        }

        private void UpdateGuides(Data? _datum = null)
        {
            var lines = MainCanvas.Children.OfType<Line>().ToList();
            var w = MainCanvas.ActualWidth;
            var h = MainCanvas.ActualHeight;
            for (var i = 0; i < Math.Min(3, data.Count); i++)
            {
                var datum = data[i];
                if (null == _datum || _datum == datum)
                {
                    var lineH = lines[i];
                    lineH.X1 = 0;
                    lineH.Y1 = data[i].Local.Y * h;
                    lineH.X2 = w;
                    lineH.Y2 = data[i].Local.Y * h;
                    lineH.Visibility = AxisGuides ? Visibility.Visible : Visibility.Collapsed;

                    var lineV = lines[3 + i];
                    lineV.X1 = data[i].Local.X * w;
                    lineV.Y1 = 0;
                    lineV.X2 = data[i].Local.X * w;
                    lineV.Y2 = h;
                    lineV.Visibility = AxisGuides ? Visibility.Visible : Visibility.Collapsed;
                }
            }
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
                    else if (e.Key == Key.Z)
                    {
                        Undo();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Y)
                    {
                        Redo();
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
                    case "tbPixelStep":
                        PixelStep = (int)value;
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
            undoStack.Push(SerialiseData());
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
                undoStack.Push(SerialiseData());

                // Open document
                string filename = dialog.FileName;
                List<Data>? tempList = null;
                DataSave? dataSave = null;

                try
                {
                    var serializer = new XmlSerializer(typeof(DataSave));
                    using (var fs = new FileStream(filename, FileMode.Open))
                    {
                        dataSave = (DataSave?)serializer.Deserialize(fs);
                    }
                    if (null != dataSave)
                    {
                        //var version = dataSave.SaveVersion;
                        rcbX.SelectedValue = dataSave.StyleX;
                        rcbY.SelectedValue = dataSave.StyleY;
                        tempList = dataSave.data;
                    }
                }
                catch
                {
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
                        MessageBox.Show("Open_Click: " + ex.Message);
                    }
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

                DataSave dataSave = new DataSave(Version, StyleX, StyleY, data.ToList());
                using (Stream writer = new FileStream(filename, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(DataSave));
                    serializer.Serialize(writer, dataSave);
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
            undoStack.Push(SerialiseData());
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

        private void TextBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            UpdateTextBox(sender);
        }

        private void cbGuides_Click(object sender, RoutedEventArgs e)
        {
            UpdateData();
        }

        private void cbNearestNeighbour_Click(object sender, RoutedEventArgs e)
        {
            BitmapScaling();
            UpdateData();
        }

        private void cbSort_Clicked(object sender, RoutedEventArgs e)
        {
            undoStack.Push(SerialiseData());
            UpdateData();
        }

        private void MainImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            statusViewSize.Content = " View size " + (int)e.NewSize.Width + " x " + (int)e.NewSize.Height;
        }

        private MemoryStream SerialiseData()
        {
            var stream = new MemoryStream();
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<Data>));
            serializer.Serialize(stream, data);
            return stream;
        }

        private void DeSerialiseData(MemoryStream stream)
        {
            List<Data>? tempList = null;

            stream.Position = 0;
            var serializer = new XmlSerializer(typeof(List<Data>));
            tempList = (List<Data>?)serializer.Deserialize(stream);

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

        private void Undo()
        {
            if (undoStack.Count == 0) return;
            redoStack.Push(SerialiseData());
            var last = undoStack.Pop();
            DeSerialiseData(last);
        }

        private void Redo()
        {
            if (redoStack.Count == 0) return;
            undoStack.Push(SerialiseData());
            var last = redoStack.Pop();
            DeSerialiseData(last);
        }
    }

    public class DataSave
    {
        public string Version = string.Empty;
        public string StyleX = string.Empty;
        public string StyleY = string.Empty;
        public List<Data> data { get; set; } = new List<Data>();

        public DataSave()
        {
        }

        public DataSave(Version? version, string styleX, string styleY, List<Data> data)
        {
            if (null != version) Version = version.ToString();
            StyleX = styleX;
            StyleY = styleY;
            this.data = data;
        }

        public Version SaveVersion
        {
            get { return new Version(Version); }
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