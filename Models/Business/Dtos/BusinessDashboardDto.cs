using System.Collections.Generic;

namespace Queueless.Models.Business.Dtos
{
    public class BusinessDashboardDto
    {
        public int BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;

        // from BusinessRegistration.AvgTimeMinutes
        public int? AvgWaitMinutes { get; set; }

        // current serving token number (can be null)
        public string? CurrentTokenNumber { get; set; }

        // how many are currently waiting
        public int WaitingCount { get; set; }

        // list of tokens in the waiting queue
        public List<BusinessQueueItemDto> Queue { get; set; } = new();
    }
}
