using System;
using System.Linq;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;

namespace MechanicalDms.Operation
{
    public class MinecraftPlayerOperation : IDisposable
    {
        private readonly DmsDbContext _db;

        public MinecraftPlayerOperation()
        {
            _db = new DmsDbContext();
        }

        public void AddOrUpdateMinecraftUser(string uuid, string playerName)
        {
            var player = _db.MinecraftPlayers.FirstOrDefault(x => x.Uuid == uuid);

            if (player is null)
            {
                var minecraftPlayer = new MinecraftPlayer()
                {
                    Uuid = uuid,
                    PlayerName = playerName
                };

                _db.MinecraftPlayers.Add(minecraftPlayer);
            }
            else
            {
                player.PlayerName = playerName;

                _db.MinecraftPlayers.Update(player);
            }

            _db.SaveChanges();
        }
        
        public void UpdateAndSave(MinecraftPlayer player)
        {
            _db.Update(player);
            _db.SaveChanges();
        }

        public MinecraftPlayer GetMinecraftPlayer(string uuid)
        {
            return _db.MinecraftPlayers.FirstOrDefault(x => x.Uuid == uuid);
        }
        
        public void Dispose()
        {
            _db.Dispose();
        }
    }
}
