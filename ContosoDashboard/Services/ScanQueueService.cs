namespace ContosoDashboard.Services
{
    public interface IScanQueueService
    {
        Task EnqueueAsync(int documentId);
    }

    /// <summary>
    /// No-op implementation. Replace with a real queue (e.g., Azure Service Bus) in production.
    /// </summary>
    public class NoOpScanQueueService : IScanQueueService
    {
        public Task EnqueueAsync(int documentId)
        {
            return Task.CompletedTask;
        }
    }
}
