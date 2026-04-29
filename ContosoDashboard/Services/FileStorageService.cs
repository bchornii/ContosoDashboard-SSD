using Microsoft.AspNetCore.Components.Forms;

namespace ContosoDashboard.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IBrowserFile file, int userId, int? projectId);
        Task<Stream> ReadFileAsync(string storedFilePath);
        Task DeleteFileAsync(string storedFilePath);
    }

    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LocalFileStorageService> _logger;
        private const long MaxAllowedSize = 50 * 1024 * 1024; // 50 MB

        public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        private string GetUploadsRoot() =>
            Path.Combine(_env.ContentRootPath, "AppData", "uploads");

        public async Task<string> SaveFileAsync(IBrowserFile file, int userId, int? projectId)
        {
            var uploadsRoot = GetUploadsRoot();
            var subFolder = projectId.HasValue
                ? Path.Combine(userId.ToString(), projectId.Value.ToString())
                : userId.ToString();

            var targetDir = Path.GetFullPath(Path.Combine(uploadsRoot, subFolder));

            // Path-traversal guard
            var rootFull = Path.GetFullPath(uploadsRoot);
            if (!targetDir.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !targetDir.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid file storage path.");
            }

            Directory.CreateDirectory(targetDir);

            var ext = Path.GetExtension(file.Name);
            var storedName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(targetDir, storedName);

            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await using var source = file.OpenReadStream(MaxAllowedSize);
            await source.CopyToAsync(fs);

            // Return relative path from uploads root
            return Path.GetRelativePath(uploadsRoot, fullPath)
                       .Replace('\\', '/');
        }

        public async Task<Stream> ReadFileAsync(string storedFilePath)
        {
            var uploadsRoot = GetUploadsRoot();
            var fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, storedFilePath));

            // Path-traversal guard
            var rootFull = Path.GetFullPath(uploadsRoot);
            if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid file path.");
            }

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Stored file not found.", storedFilePath);

            // Return MemoryStream so caller doesn't hold FileStream open
            var ms = new MemoryStream();
            await using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await fs.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }

        public Task DeleteFileAsync(string storedFilePath)
        {
            var uploadsRoot = GetUploadsRoot();
            var fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, storedFilePath));

            var rootFull = Path.GetFullPath(uploadsRoot);
            if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid file path.");

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }
    }
}
