
namespace OP_Shared_Library.Notify.Interface;
public interface INotifyClient
{
    void ReceiveNotification(INotifyMessage notifyMessage);
}
