
namespace OP_Shared_Library.Notify.Interface;
public interface INotifyMessage
{
    NotifyMessageType Type { get; }
    object MessageObject { get; }
    object Information { get; }
}

public enum NotifyMessageType
{
    cmd,
    onlineState
}
