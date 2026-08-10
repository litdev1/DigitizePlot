using DigitizePlot.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DigitizePlot
{
    /// <summary>
    /// Interaction logic for Help.xaml
    /// </summary>
    public partial class Help : Window
    {
        public Help()
        {
            InitializeComponent();
            LoadRTB();
        }

        void LoadRTB()
        {
            var stream = new MemoryStream(Properties.Resources.HelpRTF);
            rtb.SelectAll();
            rtb.Selection.Load(stream, DataFormats.Rtf);
        }
    }
}
