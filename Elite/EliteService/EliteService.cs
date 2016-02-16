/// <author>
/// Shawn Quereshi
/// </author>
namespace EliteService
{
    using System;
    using System.ServiceModel;
    using System.ServiceProcess;

    public partial class EliteService : ServiceBase
    {
        internal static ServiceHost serviceHost = null;

        public EliteService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            if (serviceHost != null)
            {
                serviceHost.Close();
            }

            serviceHost = new ServiceHost(typeof(EliteServiceLibrary.EliteService));
            serviceHost.Open();
        }

        protected override void OnStop()
        {
            if (serviceHost != null)
            {
                serviceHost.Close();
                serviceHost = null;
            }
        }
    }
}
