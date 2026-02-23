namespace OP_Pages_Library;

public sealed class Globals
{
    #region Static
    private static Globals _instance = null;
    public static readonly object _lock = new object();

    public static Globals Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Globals();
                    }
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Public Properties
    public bool IsOnlineGlobal { get; set; }
    #endregion
}
