using Minio.DataModel;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Op_LP.Services;

public class YamlService : IYamlService
{
    #region Dependency Injection
    private readonly IHostEnvironment _env;
    private readonly ICustomLogger _customLogger;
    #endregion

    #region Private Properties
    private string _dataDir = string.Empty;
    private IDeserializer? _deserializer;
    private ISerializer? _serializer;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    #endregion

    #region Public Properties
    public IDeserializer? Deserializer => _deserializer;
    public ISerializer? Serializer => _serializer;
    #endregion

    #region Ctor
    public YamlService(IHostEnvironment env, ICustomLogger customLogger)
    {
          _env = env;
        _customLogger = customLogger;

        FillProperties();

        CreateDeSerializers();
    }
    #endregion

    #region Private Properties
    private void FillProperties()
    {
        // Local YAML directories
        _dataDir = Path.Combine(_env.ContentRootPath, "wwwroot", "data");
        Directory.CreateDirectory(_dataDir);
    }
    private void CreateDeSerializers()
    {
        _deserializer = new DeserializerBuilder()
            //.WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            //.WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }
    #endregion

    #region Public Properties
    // Compose path to the YAML file
    public string EnsureYamlPath(string fileName)
    {
        var yamlPath = Path.Combine(_dataDir, fileName);
        return yamlPath;
    }
    // Read yaml from file and return string file
    public async Task<string> ReadYamlFile(string _yamlPath)
    {
        return await File.ReadAllTextAsync(_yamlPath);
    }
    // Save Dict to Yaml
    public async Task SaveDictToYamlFileAsync(string fileName,
                                              Dictionary<string,string> map,
                                              string yamlPath
                                              )
    {
        EnsureYamlPath(fileName);
        await _ioLock.WaitAsync();
        try
        {
            if (_serializer is null) return;
            var yaml = _serializer.Serialize(map);
            await File.WriteAllTextAsync(yamlPath, yaml);
        }
        finally
        {
            _ioLock.Release();
        }
    }
    public async Task SaveToYamlAsync<T>(List<T> items, string fileName)
    {
        var yaml = SerializeYaml(items);
        var path = Path.Combine(_env.ContentRootPath, "wwwroot", "data", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, yaml, Encoding.UTF8);
    }
    // Serialize Yaml
    public string SerializeYaml<T>(List<T> items)
    {
        try
        {
            // Check serializer
            if (_serializer is null) return string.Empty;

            // Serialize the list of items into a YAML string
            var yaml = _serializer.Serialize(items);

            return yaml;
        }
        catch (Exception ex)
        {
            // Log the error and return an empty string in case of failure
            _customLogger.MyLogger.Error($"YamlService: SerializeYaml: Error serializing YAML: {ex.Message}");
            return string.Empty;
        }
    }
    // Serialization for *.yaml
    public List<T> LoadYamlList<T>(string url, Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream);

            var deserializer = new DeserializerBuilder().Build();

            var items = deserializer.Deserialize<List<T>>(reader);
            return items ?? new List<T>();
        }
        catch (Exception ex)
        {
            _customLogger.MyLogger.Error($"YamlService: LoadYamlList: Error loading YAML: {ex.Message}");
            return new List<T>();
        }
    }
    #endregion
}
