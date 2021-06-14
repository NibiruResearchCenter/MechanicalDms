using System;
using System.Linq;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;

namespace MechanicalDms.Operation
{
    public class BilibiliUserOperation : IDisposable
    {
        private readonly DmsDbContext _db;

        public BilibiliUserOperation()
        {
            _db = new DmsDbContext();
        }

        public void AddOrUpdateBilibiliUser(long uid, string username, int guardLevel, int level)
        {
            var user = _db.BilibiliUsers.FirstOrDefault(x => x.Uid == uid);

            if (user is null)
            {
                var biliUser = new BilibiliUser()
                {
                    Uid = uid,
                    Username = username,
                    Level = level,
                    GuardLevel = guardLevel
                };

                _db.BilibiliUsers.Add(biliUser);
            }
            else
            {
                user.Username = username;
                user.Level = level;
                user.GuardLevel = guardLevel;

                _db.BilibiliUsers.Update(user);
            }

            _db.SaveChanges();
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}