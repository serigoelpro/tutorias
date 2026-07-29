using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using PlataformaWeb;
using PlataformaWeb.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models;

namespace PlataformaWeb.Controllers
{
    [CustomAuthorize(Nivel = 2)]
    public class SeguimientoesController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Seguimientoes
        public ActionResult Index(int id)
        {
            var seg = db.Seguimientoes
        .Where(x => x.IdIndividual == id)
        .OrderByDescending(x => x.Fecha)          // <-- más recientes primero
        .ThenByDescending(x => x.IdSeguimiento)   // <-- desempate estable
        .ToList();

            var ind = db.Individuals.FirstOrDefault(x => x.IdIndividual == id);
            ViewBag.idind = ind.IdIndividual;

            var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == ind.IdPersona);
            ViewBag.Alumno = datos;

            return View(seg);
        }
        // GET: Seguimientoes/Details/5
        //public ActionResult Details(int? id)
        //{
        //    if (id == null)
        //    {
        //        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        //    }
        //    Seguimiento seguimiento = db.Seguimientoes.Find(id);
        //    if (seguimiento == null)
        //    {
        //        return HttpNotFound();
        //    }
        //    return View(seguimiento);
        //}
        // GET: Seguimientoes/Create
        // GET: Seguimientoes/Create/{id}   <-- id = IdIndividual
        // GET: Seguimientoes/Create?id=ID_INDIVIDUAL
        // GET: Seguimientoes/Create?id=IdIndividual
        public ActionResult Create(int id)
        {
            var ind = db.Individuals.Find(id);
            if (ind == null) return HttpNotFound();

            // Encabezado
            var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == ind.IdPersona);
            ViewBag.Alumno = alumno;
            ViewBag.AlumnoNombre = alumno?.Nombre ?? "";   // <- SOLO el nombre
            ViewBag.IdPersona = ind.IdPersona;     // <-- lo mandamos a la vista también
            ViewBag.IdIndividual = id;

            // Model para el form
            return View(new Seguimiento
            {
                IdIndividual = id,
                Fecha = DateTime.Now
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Seguimiento model)
        {
            if (ModelState.IsValid)
            {
                model.Fecha = DateTime.Now;
                db.Seguimientoes.Add(model);
                db.SaveChanges();

                int idPersona = db.Individuals
                                  .Where(i => i.IdIndividual == model.IdIndividual)
                                  .Select(i => i.IdPersona)
                                  .FirstOrDefault();

                if (idPersona == 0)
                    int.TryParse(Request["ReturnIdPersona"], out idPersona);

                return RedirectToAction("Index", "Individuals", new { id = idPersona });
            }

            // Si hubo errores, volver a mostrar encabezado correctamente:
            int idPersonaRetry = db.Individuals
                                   .Where(i => i.IdIndividual == model.IdIndividual)
                                   .Select(i => i.IdPersona)
                                   .FirstOrDefault();

            if (idPersonaRetry == 0)
                int.TryParse(Request["ReturnIdPersona"], out idPersonaRetry);

            ViewBag.AlumnoNombre = db.DatosPersonales
                                     .Where(x => x.IdPersona == idPersonaRetry)
                                     .Select(x => x.Nombre)
                                     .FirstOrDefault() ?? "";
            ViewBag.IdPersona = idPersonaRetry;

            return View(model);
        }





        // GET: Seguimientoes/Edit/5
        // GET: Seguimientoes/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var seg = db.Seguimientoes.Find(id);
            if (seg == null) return HttpNotFound();

            // <-- obtener el IdPersona de la hoja (Individual) de este seguimiento
            var idPersona = db.Individuals
                              .Where(i => i.IdIndividual == seg.IdIndividual)
                              .Select(i => i.IdPersona)
                              .FirstOrDefault();

            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);
            ViewBag.IdPersona = idPersona;   // <-- úsalo en la vista para el link Regresar

            return View(seg);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Seguimiento seguimiento)
        {
            if (!ModelState.IsValid)
            {
                // si vuelve a la vista por error, vuelve a setear IdPersona
                var idPersonaErr = db.Individuals
                                     .Where(i => i.IdIndividual == seguimiento.IdIndividual)
                                     .Select(i => i.IdPersona)
                                     .FirstOrDefault();
                ViewBag.IdPersona = idPersonaErr;
                return View(seguimiento);
            }

            db.Entry(seguimiento).State = EntityState.Modified;
            db.SaveChanges();

            // <-- después de guardar, manda al Index de Individuals con el IdPersona correcto
            var idPersona = db.Individuals
                              .Where(i => i.IdIndividual == seguimiento.IdIndividual)
                              .Select(i => i.IdPersona)
                              .FirstOrDefault();

            return RedirectToAction("Index", "Individuals", new { id = idPersona });
        }




        // GET: Seguimientoes/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Seguimiento seguimiento = db.Seguimientoes.Find(id);
            if (seguimiento == null)
            {
                return HttpNotFound();
            }
            Individual ind = db.Individuals.FirstOrDefault(x => x.IdIndividual == seguimiento.IdIndividual);
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == ind.IdPersona);
            ViewBag.idind = ind.IdIndividual;
            return View(seguimiento);
        }

        // POST: Seguimientoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Seguimiento seguimiento = db.Seguimientoes.Find(id);
            if (seguimiento == null) return HttpNotFound();

            // necesitamos el IdPersona para redirigir
            var ind = db.Individuals.FirstOrDefault(x => x.IdIndividual == seguimiento.IdIndividual);
            var idPersona = ind?.IdPersona ?? 0;

            db.Seguimientoes.Remove(seguimiento);
            db.SaveChanges();

            // ⬇⬇⬇ CAMBIAR A ESTA REDIRECCIÓN
            return RedirectToAction("Index", "Individuals", new { id = idPersona });
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}