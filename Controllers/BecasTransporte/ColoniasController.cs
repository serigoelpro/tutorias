using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using PlataformaWeb;
using PlataformaWeb.BecasTransporte.Models;
using Plataforma_Web.Models;

namespace PlataformaWeb.Controllers.BecasTransporte
{
    [CustomAuthorize(Nivel = 99)]
    public class ColoniasController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Colonias
        public ActionResult Index()
        {
            return View(db.Colonias.ToList());
        }

        // GET: Colonias/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Colonia colonia = db.Colonias.Find(id);
            if (colonia == null)
            {
                return HttpNotFound();
            }
            return View(colonia);
        }

        // GET: Colonias/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Colonias/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,Nombre,IdCiudad")] Colonia colonia)
        {
            if (ModelState.IsValid)
            {
                db.Colonias.Add(colonia);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(colonia);
        }

        // GET: Colonias/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Colonia colonia = db.Colonias.Find(id);
            if (colonia == null)
            {
                return HttpNotFound();
            }
            return View(colonia);
        }

        // POST: Colonias/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Nombre,IdCiudad")] Colonia colonia)
        {
            if (ModelState.IsValid)
            {
                db.Entry(colonia).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(colonia);
        }

        // GET: Colonias/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Colonia colonia = db.Colonias.Find(id);
            if (colonia == null)
            {
                return HttpNotFound();
            }
            return View(colonia);
        }

        // POST: Colonias/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Colonia colonia = db.Colonias.Find(id);
            db.Colonias.Remove(colonia);
            db.SaveChanges();
            return RedirectToAction("Index");
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
