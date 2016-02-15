/// <author>
/// Shawn Quereshi
/// </author>
namespace EliteService
{
    using System.ServiceProcess;

    /// <summary>
    /// The service.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new EliteService()
            };

            ServiceBase.Run(ServicesToRun);
        }
    }
}
