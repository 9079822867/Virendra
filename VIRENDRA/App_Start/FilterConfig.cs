using System.Web;
using System.Web.Mvc;

namespace VIRENDRA
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new VIRENDRA.Infrastructure.RefreshUserBalanceAttribute());
            filters.Add(new VIRENDRA.Infrastructure.KycGateAttribute());
        }
    }
}
