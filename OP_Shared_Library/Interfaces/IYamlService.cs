
using YamlDotNet.Serialization;

public interface IYamlService
{
    IDeserializer Deserializer { get; }
    ISerializer Serializer { get; }

    string EnsureYamlPath(string fileName);
    List<T> LoadYamlList<T>(string url, Stream stream);
    Task<string> ReadYamlFile(string _yamlPath);
    Task SaveDictToYamlFileAsync(string fileName, Dictionary<string, string> map, string yamlPath);
    Task SaveToYamlAsync<T>(List<T> items, string fileName);
    string SerializeYaml<T>(List<T> items);
}
