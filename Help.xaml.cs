using System.IO;
using System.Windows;

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
