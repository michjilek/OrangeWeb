using Serilog;

public interface ICustomLogger
{
    ILogger MyLogger { get; }
    bool IsTest { get; }

    string LogAndReturn(string msg, bool error = true);
    void TestLog(string msg);
}
