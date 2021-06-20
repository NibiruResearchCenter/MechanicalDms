using System;
using System.Collections.Generic;
using System.Linq;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Functions.CronJobs.CheckDatabaseFormatJob
{
    public static class CheckDatabaseFormat
    {
        [Function("CheckDatabaseFormat")]
        public static void Run([TimerTrigger("0 0 0/2 * * *")] MyInfo myTimer, FunctionContext context)
        {
            var logger = context.GetLogger("CheckDatabaseFormat");
            using var db = new DmsDbContext();
            var users = db.KaiheilaUsers.AsNoTracking().ToList();
            var updated = new List<KaiheilaUser>();
            foreach (var user in users)
            {
                var roles = user.Roles.Trim().Split(' ');
                roles = roles.Select(x => x.Trim()).Where(x => x != string.Empty).ToArray();
                var roleString = string.Join(' ', roles);
                if (roleString == user.Roles)
                {
                    continue;
                }
                logger.LogInformation("需要修正的 Row：");
                logger.LogInformation($"OLD：{user.Roles}");
                logger.LogInformation($"NEW：{roleString}");
                user.Roles = roleString;
                updated.Add(user);
            }

            foreach (var user in updated)
            {
                db.KaiheilaUsers.Update(user);
            }
            db.SaveChanges();
            logger.LogInformation($"影响行数：{updated.Count}");
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