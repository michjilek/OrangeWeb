using Serilog;
using ILogger = Serilog.ILogger;

namespace Op_LP.Services;

public class CustomLogger : ICustomLogger
{
    #region Dependency Injection
    IConfiguration _config;
    #endregion

    #region Public Properties
    public ILogger MyLogger => _myLogger;
    public bool IsTest => _isTest;
    #endregion

    #region Private Properties
    private ILogger _myLogger;
    private bool _isTest;

    #endregion

    #region Ctor
    public CustomLogger(IConfiguration config)
    {
        Log.Logger = new LoggerConfiguration()
                       .Enrich.FromLogContext() // add to log context properties (like thread id, etc.)
                        .WriteTo.File(
                           path: @".\Logs\log-.txt", // log file path with rolling file name pattern
                           rollingInterval: RollingInterval.Day, // roll log file every day
                           retainedFileCountLimit: 30, // keep logs for 30 days
                           shared: true, // allow shared access to log file (for multiple processes)
                           flushToDiskInterval: TimeSpan.FromSeconds(1)) // flush log events to disk every second
                       .CreateLogger();

        _myLogger = Log.Logger;
        _config = config;

        FillProperties();
    }
    #endregion

    #region Private Methods
    private void FillProperties()
    {
        _isTest = _config["AppSettings:Environment"]?.ToLower() == "test" ? true : false;
    }
    #endregion

    #region Public Methods
    public string LogAndReturn(string msg, bool error = true)
    {
        // Log
        if (error)
        {
            _myLogger.Error(msg);
        }
        else
        {
            _myLogger.Information(msg);
        }

        // Return empty string because of method above 
        return "";
    }
    public void TestLog(string msg)
    {
        if(_myLogger != null && _isTest)
        {
            _myLogger.Information($"TEST: {msg}");
        }
    }
    #endregion
}
