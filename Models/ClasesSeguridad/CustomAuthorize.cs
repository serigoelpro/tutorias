using ProyectoIntegracion.Functionalities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Plataforma_Web.Models
{
    public class CustomAuthorize : AuthorizeAttribute
    {
        public string User { get; set; }
        public int Nivel { get; set; }

        private string accion = "Login";
        private string control = "Home";

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.Session["Usuario"] == null)
                return false;
            else
            {
                try
                {
                    Usuario user = httpContext.Session["Usuario"] as Usuario;
                    if (user.IdNivel >= Nivel)
                        return true;
                }
                catch (Exception)
                {
                }
                accion = "Restringido";
                control = "Home";
                return false;
            }
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectResult(LinkGenerator.Create("login", "Home/Unauthorized"));
        }
    }
}