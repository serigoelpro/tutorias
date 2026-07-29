using System.Web;
using System.Web.Mvc;

namespace PlataformaWeb
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new Plataforma_Web.Models.SoloLecturaDirectorAttribute());
        }
    }
}
