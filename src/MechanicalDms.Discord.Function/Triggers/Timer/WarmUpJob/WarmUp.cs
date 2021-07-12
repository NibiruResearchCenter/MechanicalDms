using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Discord.Function.Triggers.Timer.WarmUpJob
{
    public static class WarmUp
    {
        [Function("WarmUp")]
        public static void Run([TimerTrigger("0 */15 * * * *")] MyInfo myTimer, FunctionContext context)
        {
            var logger = context.GetLogger("WarmUp");
            logger.LogInformation("Warm up");
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}