// ReSharper disable ConvertIfStatementToReturnStatement
namespace MechanicalDms.Common
{
    public static class ElementHelper
    {
        public static int GetElementFromKaiheila(string roles)
        {
            if (roles.Contains("218132"))
            {
                return 2;
            }
            if (roles.Contains("218129"))
            {
                return 3;
            }
            if (roles.Contains("218133"))
            {
                return 4;
            }
            if (roles.Contains("218135"))
            {
                return 5;
            }
            return 0;
        }
        public static int GetElementFromDiscord(string roles)
        {
            if (roles.Contains("860617905216553010"))
            {
                return 2;
            }
            if (roles.Contains("860618024767455252"))
            {
                return 3;
            }
            if (roles.Contains("860618316813041735"))
            {
                return 4;
            }
            if (roles.Contains("860618475965644800"))
            {
                return 5;
            }
            return 0;
        }
        public static ulong GetElementRoleForDiscord(int element)
        {
            return element switch
            {
                2 => 860617905216553010,
                3 => 860618024767455252,
                4 => 860618316813041735,
                5 => 860618475965644800,
                _ => 0
            };
        }
        public static bool IsGuardFromKaiheila(string roles)
        {
            if (roles.Contains("254775") || roles.Contains("218176") || roles.Contains("218131"))
            {
                return true;
            }
            return false;
        }
        public static bool IsGuardFromDiscord(string roles)
        {
            if (roles.Contains("785014197213724683") || 
                roles.Contains("863018501077860352"))
            {
                return true;
            }
            return false;
        }
        public static int GetElementFromString(string element)
        {
            var e = element.ToLower().Trim();
            return e switch
            {
                "energy" => 1,
                "wind" => 2,
                "aqua" => 3,
                "fire" => 4,
                "earth" => 5,
                _ => 0
            };
        }
        public static string GetElementString(int element)
        {
            return element switch
            {
                2 => "Wind",
                3 => "Aqua",
                4 => "Fire",
                5 => "Earth",
                _ => ""
            };
        }
        public static string GetGuardString(bool isGuard)
        {
            return isGuard switch
            {
                true => "Energy",
                false => ""
            };
        }
        
    }
}