namespace Conesoft.Blazor.NetdiscoAuth;

public class DiskStorage(Func<string, string> pathGenerator) : IStorage
{
    public async Task Write(string name, string value)
    {
        await File.WriteAllTextAsync(pathGenerator(name), value);
    }
    public async Task<string> Read(string name)
    {
        return await File.ReadAllTextAsync(pathGenerator(name));
    }
    public async Task<bool> Exists(string name)
    {
        return File.Exists(pathGenerator(name));
    }
    public async Task Remove(string name)
    {
        File.Delete(pathGenerator(name));
    }
}