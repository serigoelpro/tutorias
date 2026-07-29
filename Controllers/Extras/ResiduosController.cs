using Plataforma_Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.Extras
{
    [CustomAuthorize(Nivel = 99)]
    public class ResiduosController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        // GET: Residuos
        public ActionResult Index()
        {
            var temp = DateTime.Now;
            temp -= TimeSpan.Parse("150.00:00:00");
            return View(db.Usuarios.Where(x => x.Tiempo < temp).ToList());
        }
        public ActionResult Eliminar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Usuario tutoriaGrupal = db.Usuarios.Find(id);
            if (tutoriaGrupal == null)
            {
                return HttpNotFound();
            }
            return View(tutoriaGrupal);
        }
        [HttpPost, ActionName("Elimiar")]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            Usuario usu = db.Usuarios.Find(id);
            db.Usuarios.Remove(usu);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}