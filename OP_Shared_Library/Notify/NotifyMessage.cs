using OP_Shared_Library.Notify.Interface;

namespace OP_Shared_Library.Notify;
public class NotifyMessage : INotifyMessage
{
    #region Public Properties
    public NotifyMessageType Type { get; }

    public object MessageObject { get; }

    public object Information { get; }
    #endregion

    #region Ctor
    public NotifyMessage(NotifyMessageType type, object messageObject = null, object information = null)
    {
        Type = type;
        MessageObject = messageObject;
        Information = information;
    }
    #endregion
}
