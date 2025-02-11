namespace Conesoft.Blazor.NetatmoAuth.Services;

public interface IStorage
{
    bool Exists(string name);
    Task<string> Read(string name);
    void Remove(string name);
    Task Write(string name, string value);
}