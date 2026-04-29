using System;
using System.Threading.Tasks;

namespace ContosoDashboard.Services
{
    public class NotificationStateService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        
        public event Func<Task>? OnNotificationCountChanged;
        
        public NotificationStateService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }
        
        public async Task NotifyCountChangedAsync()
        {
            if (OnNotificationCountChanged != null)
            {
                await OnNotificationCountChanged.Invoke();
            }
        }
    }
}
