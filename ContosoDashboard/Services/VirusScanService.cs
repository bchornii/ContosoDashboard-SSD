namespace ContosoDashboard.Services
{
    public enum ScanResult
    {
        Clean,
        Malicious,
        Unavailable
    }

    public interface IVirusScanService
    {
        Task<ScanResult> ScanFileAsync(string storedFilePath);
    }

    /// <summary>
    /// Stub implementation that always reports files as clean.
    /// Replace with a real AV integration in production.
    /// </summary>
    public class StubVirusScanService : IVirusScanService
    {
        public Task<ScanResult> ScanFileAsync(string storedFilePath)
        {
            return Task.FromResult(ScanResult.Clean);
        }
    }
}
