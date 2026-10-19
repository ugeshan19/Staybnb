namespace Staybnb.Services;

public interface IFileStorage
{
    string? Validate(IFormFile file, bool isDocument);
    Task<string> SaveImageAsync(IFormFile file);
    Task<string> SaveDocumentAsync(IFormFile file);
    string? GetDocumentPath(string storedName);
    void DeleteImage(string? url);
    void DeleteDocument(string? storedName);
}

public class FileStorageService : IFileStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const string ImageUrlPrefix = "/uploads/properties/";

    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly string[] DocExt = { ".pdf", ".jpg", ".jpeg", ".png" };

    private readonly string _imageDir;
    private readonly string _docDir;

    public FileStorageService(IWebHostEnvironment env)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _imageDir = Path.Combine(webRoot, "uploads", "properties");
        _docDir = Path.Combine(env.ContentRootPath, "App_Data", "documents");

        Directory.CreateDirectory(_imageDir);
        Directory.CreateDirectory(_docDir);
    }

    public string? Validate(IFormFile file, bool isDocument)
    {
        if (file == null || file.Length == 0) return "Please choose a file.";
        if (file.Length > MaxBytes) return $"'{file.FileName}' is larger than 5 MB.";

        var allowed = isDocument ? DocExt : ImageExt;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return allowed.Contains(ext)
            ? null
            : $"'{file.FileName}' must be one of: {string.Join(", ", allowed)}.";
    }

    public async Task<string> SaveImageAsync(IFormFile file) => ImageUrlPrefix + await WriteAsync(file, _imageDir);

    public Task<string> SaveDocumentAsync(IFormFile file) => WriteAsync(file, _docDir);

    public string? GetDocumentPath(string storedName)
    {
        var path = Path.Combine(_docDir, Path.GetFileName(storedName));
        return File.Exists(path) ? path : null;
    }

    public void DeleteImage(string? url)
    {
        // seed images live under /uploads/seed and must never be removed
        if (string.IsNullOrEmpty(url) || !url.StartsWith(ImageUrlPrefix)) return;
        TryDelete(Path.Combine(_imageDir, Path.GetFileName(url)));
    }

    public void DeleteDocument(string? storedName)
    {
        if (string.IsNullOrEmpty(storedName)) return;
        TryDelete(Path.Combine(_docDir, Path.GetFileName(storedName)));
    }

    private static async Task<string> WriteAsync(IFormFile file, string directory)
    {
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = new FileStream(Path.Combine(directory, name), FileMode.Create);
        await file.CopyToAsync(stream);
        return name;
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
