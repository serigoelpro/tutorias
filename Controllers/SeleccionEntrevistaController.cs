using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Plataforma_Web.Models;

namespace PlataformaWeb.Controllers
{
    [CustomAuthorize(Nivel = 99)]
    public class SeleccionEntrevistaController : Controller
    {
        // GET: SeleccionEntrevista
        public ActionResult SeleccionEntrevista()
        {
            return View();
        }
    }
}