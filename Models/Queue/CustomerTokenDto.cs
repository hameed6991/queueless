// Models/Queue/CustomerTokenDto.cs
using System;

namespace Queueless.Models.Queue
{
    public class CustomerTokenDto
    {
        public int TokenId { get; set; }
        public int BusinessId { get; set; }
        public int CustomerUserId { get; set; }
        public int TokenNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }

        public int? WaitingAhead { get; set; }
        public int? EstimatedWaitMinutes { get; set; }

        public string BusinessName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Emirate { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
    }
}
