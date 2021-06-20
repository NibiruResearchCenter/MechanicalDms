using System;
using System.Collections.Generic;
using System.Linq;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace MechanicalDms.Operation
{
    public class KaiheilaUserOperation : IDisposable
    {
        private readonly DmsDbContext _db;
        
        public KaiheilaUserOperation()
        {
            _db = new DmsDbContext();
        }

        /// <summary>
        /// 添加或更新开黑啦用户啊
        /// </summary>
        /// <param name="uid">开黑啦用户UID</param>
        /// <param name="username">开黑啦用户名</param>
        /// <param name="identifyNumber">开黑啦用户标识码（用户名后面的那一串）</param>
        /// <param name="roles">角色字符串</param>
        public void AddOrUpdateKaiheilaUser(string uid, string username, string identifyNumber, string roles)
        {
            var user = _db.KaiheilaUsers.FirstOrDefault(x => x.Uid == uid);

            if (user is null)
            {
                var khlUser = new KaiheilaUser()
                {
                    Uid = uid,
                    Username = username,
                    IdentifyNumber = identifyNumber,
                    Roles = roles.Trim()
                };

                _db.KaiheilaUsers.Add(khlUser);
            }
            else
            {
                user.Username = username;
                user.IdentifyNumber = identifyNumber;
                user.Roles = roles.Trim();
                
                _db.KaiheilaUsers.Update(user);
            }
            
            _db.SaveChanges();
        }

        /// <summary>
        /// 删除开黑啦用户
        /// </summary>
        /// <param name="uid">开黑啦用户 UID</param>
        public void DeleteKaiheilaUser(string uid)
        {
            var user = _db.KaiheilaUsers.FirstOrDefault(x => x.Uid == uid);
            if (user is null)
            {
                return;
            }
            _db.KaiheilaUsers.Remove(user);
            _db.SaveChanges();
        }
        
        /// <summary>
        /// 获取开黑啦用户
        /// </summary>
        /// <param name="uid">开黑啦用户 UID</param>
        /// <returns>开黑啦用户实体类</returns>
        public KaiheilaUser GetKaiheilaUser(string uid)
        {
            return _db.KaiheilaUsers
                .Include(x => x.BilibiliUser)
                .Include(x => x.MinecraftPlayer)
                .FirstOrDefault(x => x.Uid == uid);
        }

        /// <summary>
        /// 获取开黑啦用户
        /// </summary>
        /// <param name="uuid">Minecraft UUID</param>
        /// <returns>开黑啦用户实体类</returns>
        public KaiheilaUser GetKaiheilaUserByMinecraftUuid(string uuid)
        {
            return _db.KaiheilaUsers
                .Include(x => x.MinecraftPlayer)
                .Include(x => x.BilibiliUser)
                .FirstOrDefault(x => x.MinecraftPlayer.Uuid == uuid);
        }
        
        /// <summary>
        /// 获取拥有某个角色的开黑啦用户
        /// </summary>
        /// <returns>开黑啦用户实体类列表</returns>
        public IEnumerable<KaiheilaUser> GetKaiheilaUserWithRole(string role)
        {
            return _db.KaiheilaUsers
                .Include(x => x.BilibiliUser)
                .Where(x => x.Roles.Contains(role))
                .ToList();
        }

        /// <summary>
        /// 绑定B站
        /// </summary>
        /// <param name="uid">开黑啦用户 UID</param>
        /// <param name="bilibiliUid">B站账户 UID</param>
        /// <returns>0 - 成功；1 - 开黑啦用户不存在；2 - B站用户不存在；3 - 已绑定</returns>
        public int BindingBilibili(string uid, long bilibiliUid)
        {
            var kaiheilaUser = _db.KaiheilaUsers.FirstOrDefault(x => x.Uid == uid);
            var bilibiliUser = _db.BilibiliUsers.FirstOrDefault(x => x.Uid == bilibiliUid);
            if (kaiheilaUser is null)
            {
                return 1;
            }

            if (bilibiliUser is null)
            {
                return 2;
            }

            if (kaiheilaUser.BilibiliUser is not null)
            {
                return 3;
            }

            kaiheilaUser.BilibiliUser = bilibiliUser;

            _db.KaiheilaUsers.Update(kaiheilaUser);
            _db.SaveChanges();
            return 0;
        }

        /// <summary>
        /// 绑定 MC
        /// </summary>
        /// <param name="uid">开黑啦用户 UID</param>
        /// <param name="uuid">MC UUID</param>
        /// <returns>0 - 成功；1 - 开黑啦用户不存在；2 - MC 玩家不存在；3 - 已绑定</returns>
        public int BindingMinecraft(string uid, string uuid)
        {
            var kaiheilaUser = _db.KaiheilaUsers.FirstOrDefault(x => x.Uid == uid);
            var minecraftPlayer = _db.MinecraftPlayers.FirstOrDefault(x => x.Uuid == uuid);
            if (kaiheilaUser is null)
            {
                return 1;
            }

            if (minecraftPlayer is null)
            {
                return 2;
            }

            if (kaiheilaUser.MinecraftPlayer is not null)
            {
                return 3;
            }

            kaiheilaUser.MinecraftPlayer = minecraftPlayer;

            _db.KaiheilaUsers.Update(kaiheilaUser);
            _db.SaveChanges();
            return 0;
        }

        /// <summary>
        /// 保存更改
        /// </summary>
        /// <param name="user"></param>
        public void UpdateAndSave(KaiheilaUser user)
        {
            _db.Update(user);
            _db.SaveChanges();
        }
        
        public void Dispose()
        {
            _db.Dispose();
        }
    }
}
