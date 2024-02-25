namespace Conesoft.Blazor.NetdiscoAuth;

public interface IStorage
{
    Task<bool> Exists(string name);
    Task<string> Read(string name);
    Task Remove(string name);
    Task Write(string name, string value);
}