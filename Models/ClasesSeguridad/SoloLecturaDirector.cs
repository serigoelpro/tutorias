using System;
using System.Web.Mvc;
using ProyectoIntegracion.Functionalities;

namespace Plataforma_Web.Models
{
    /// <summary>
    /// Marca una acción (o un controlador completo) cuyos POST son de LECTURA
    /// (cargas AJAX/DataTables, exportaciones a Excel/PDF, navegación) y por
    /// tanto permitidos para el rol Director. Lo consume SoloLecturaDirectorAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class LecturaPermitidaAttribute : Attribute
    {
    }

    /// <summary>
    /// Filtro GLOBAL: para una sesión de Director (EsDirector), bloquea todo
    /// verbo mutante (POST/PUT/PATCH/DELETE) salvo que la acción o el
    /// controlador estén marcados [LecturaPermitida]. Fail-closed: un POST de
    /// lectura no anotado queda bloqueado (seguro; anotarlo al detectarlo).
    /// Las peticiones sin sesión (p. ej. EjecutarCorteProgramado /
    /// EnviarAlertaCierre del Task Scheduler, autenticados por token) pasan.
    /// </summary>
    public class SoloLecturaDirectorAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = filterContext.HttpContext.Session == null
                ? null
                : filterContext.HttpContext.Session["Usuario"] as Usuario;

            if (usuario == null || !usuario.EsDirector)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            string metodo = filterContext.HttpContext.Request.HttpMethod;
            bool esLectura = string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(metodo, "HEAD", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(metodo, "OPTIONS", StringComparison.OrdinalIgnoreCase);

            if (esLectura)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            bool lecturaPermitida =
                filterContext.ActionDescriptor.IsDefined(typeof(LecturaPermitidaAttribute), true) ||
                filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(LecturaPermitidaAttribute), true);

            if (lecturaPermitida)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.HttpContext.Response.StatusCode = 403;
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, ok = false, message = "Cuenta de Director: modo solo lectura." },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                filterContext.Result = new RedirectResult(LinkGenerator.Create("login", "Home/Unauthorized"));
            }
        }
    }
}
