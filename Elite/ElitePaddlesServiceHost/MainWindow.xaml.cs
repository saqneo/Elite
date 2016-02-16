using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ElitePaddlesServiceHost
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ServiceHost serviceHost;

        public MainWindow()
        {
            InitializeComponent();
            serviceHost = new ServiceHost(typeof(EliteServiceLibrary.EliteService));
            serviceHost.Open();

            //TODO: Create Icon so app can hide in notification area
            //var minimizedIcon = new NotifyIcon();
            //minimizedIcon.DoubleClick +=
            //    delegate (object sender, EventArgs args)
            //    {
            //        this.Show();
            //        this.WindowState = WindowState.Normal;
            //    };
        }

        protected override void OnStateChanged(EventArgs e)
        {
            //TODO: Make app hide to notification area
            //if (WindowState == System.Windows.WindowState.Minimized)
            //    this.Hide();

            base.OnStateChanged(e);
        }
    }
}
