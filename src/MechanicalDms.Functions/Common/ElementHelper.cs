// ReSharper disable ConvertIfStatementToReturnStatement
namespace MechanicalDms.Functions.Common
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
            if (roles.Contains("785014197213724683"))
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
                "gold" => 1,
                "herba" => 2,
                "aqua" => 3,
                "flame" => 4,
                "earth" => 5,
                _ => 0
            };
        }
        public static string GetElementString(int element)
        {
            return element switch
            {
                2 => "Herba",
                3 => "Aqua",
                4 => "Flame",
                5 => "Earth",
                _ => ""
            };
        }
        public static string GetGuardString(bool isGuard)
        {
            return isGuard switch
            {
                true => "Gold",
                false => ""
            };
        }
        
    }
}