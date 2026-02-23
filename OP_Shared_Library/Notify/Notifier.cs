using OP_Shared_Library.Notify.Interface;

namespace OP_Shared_Library.Notify;
public class Notifier : INotifier
{
    #region Interface Implementation
    public void AddClient(INotifyClient client)
    {
        _clients.Add(client);
    }

    public void Notify(INotifyMessage message)
    {
        foreach (INotifyClient client in _clients)
        {
            client.ReceiveNotification(message);
        }
    }

    public void RemoveClient(INotifyClient client)
    {
        _clients.Remove(client);
    }
    #endregion

    #region Private Properties
    private List<INotifyClient> _clients = new List<INotifyClient>();
    #endregion
}
