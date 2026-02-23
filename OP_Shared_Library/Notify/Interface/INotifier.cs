
namespace OP_Shared_Library.Notify.Interface;
public interface INotifier
{
    void AddClient(INotifyClient client);
    void RemoveClient(INotifyClient client);
    void Notify(INotifyMessage message);
}
