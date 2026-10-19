namespace Veriqo.Api.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string storedName);
    string GetPath(string storedName);
    bool Exists(string storedName);
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "Storage", "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string storedName)
    {
        var path = Path.Combine(_root, storedName);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fs);
        return path;
    }

    public string GetPath(string storedName) => Path.Combine(_root, storedName);

    public bool Exists(string storedName) => File.Exists(Path.Combine(_root, storedName));
}
