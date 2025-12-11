using System;

namespace Queueless.Models.Business.Dtos
{
    public class BusinessQueueItemDto
    {
        public int TokenId { get; set; }

        // we convert numeric token to string in controller
        public string TokenNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        // for now not used (no Service table yet), but kept for future
        public string ServiceName { get; set; } = string.Empty;
    }
}
