// Services/ICustomerQueueService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Queueless.Models.Queue;

namespace Queueless.Services
{
    public interface ICustomerQueueService
    {
        Task<CustomerTokenDto> JoinQueueAsync(int businessId, int customerUserId);

        Task<List<CustomerTokenDto>> GetActiveTokensAsync(int customerUserId);

        // 🔹 ADD THIS
        Task<CustomerTokenDto?> GetActiveTokenForBusinessAsync(
            int businessId,
            int customerUserId);
    }
}
