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
    public class BecasController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Becas
        public ActionResult Index()
        {
            return View(db.Becas.ToList());
        }

        // GET: Becas/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Beca beca = db.Becas.Find(id);
            if (beca == null)
            {
                return HttpNotFound();
            }
            return View(beca);
        }

        // GET: Becas/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Becas/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,NombreBeca,DetallesBeca")] Beca beca)
        {
            if (ModelState.IsValid)
            {
                db.Becas.Add(beca);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(beca);
        }

        // GET: Becas/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Beca beca = db.Becas.Find(id);
            if (beca == null)
            {
                return HttpNotFound();
            }
            return View(beca);
        }

        // POST: Becas/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,NombreBeca,DetallesBeca")] Beca beca)
        {
            if (ModelState.IsValid)
            {
                db.Entry(beca).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(beca);
        }

        // GET: Becas/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Beca beca = db.Becas.Find(id);
            if (beca == null)
            {
                return HttpNotFound();
            }
            return View(beca);
        }

        // POST: Becas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Beca beca = db.Becas.Find(id);
            db.Becas.Remove(beca);
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
