// ReSharper disable ConvertIfStatementToReturnStatement
namespace MechanicalDms.Functions
{
    public static class CheckElement
    {
        public static int Get(string roles)
        {
            if (roles.Contains("254775") || roles.Contains("218176") || roles.Contains("218131"))
            {
                return 1;
            }
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
    }
}