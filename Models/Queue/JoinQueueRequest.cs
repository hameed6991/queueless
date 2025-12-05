// Models/Queue/JoinQueueRequest.cs
namespace Queueless.Models.Queue
{
    public class JoinQueueRequest
    {
        public int BusinessId { get; set; }
        public int CustomerUserId { get; set; } // from mobile (SharedPreferences userId)
    }
}
