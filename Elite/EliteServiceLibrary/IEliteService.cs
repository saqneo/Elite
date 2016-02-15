/// <author>
/// Shawn Quereshi
/// </author>
namespace EliteServiceLibrary
{
    using System.ServiceModel;

    [ServiceContract]
    public interface IEliteService
    {
        [OperationContract]
        void SendKeyDown(ushort virtualKey);

        [OperationContract]
        void SendKeyUp(ushort virtualKey);
    }
}
