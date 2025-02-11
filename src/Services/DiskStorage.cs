namespace Conesoft.Blazor.NetatmoAuth.Services;

public abstract class DiskStorage() : IStorage
{
    public abstract string GeneratePath(string path);

    public async Task Write(string name, string value) => await File.WriteAllTextAsync(GeneratePath(name), value);
    public async Task<string> Read(string name) => await File.ReadAllTextAsync(GeneratePath(name));
    public bool Exists(string name) => File.Exists(GeneratePath(name));
    public void Remove(string name) => File.Delete(GeneratePath(name));
}