using System;
using System.Threading.Tasks;
using KaiheilaBot.Core.Services.IServices;
using RestSharp;

namespace MechanicalDms.AccountManager.Helpers
{
    public static class RoleHelper
    {
        public static async Task RevokeRole(string khlUid, string role, IHttpApiRequestService httpApiRequestService)
        { 
            await httpApiRequestService
                .SetResourcePath("guild-role/revoke")
                .SetMethod(Method.POST)
                .AddPostBody("guild_id", Configuration.GuildId)
                .AddPostBody("user_id", khlUid)
                .AddPostBody("role_id", Convert.ToUInt32(role))
                .GetResponse();
        }
        
        public static async Task GrantRole(string khlUid, string role, IHttpApiRequestService httpApiRequestService) 
        { 
            await httpApiRequestService
                .SetResourcePath("guild-role/grant")
                .SetMethod(Method.POST)
                .AddPostBody("guild_id", Configuration.GuildId)
                .AddPostBody("user_id", khlUid)
                .AddPostBody("role_id", Convert.ToUInt32(role))
                .GetResponse();
        }
    }
}