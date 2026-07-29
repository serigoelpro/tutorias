using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Plataforma_Web.Models;
using PlataformaWeb;

namespace Plataforma_Web.Controllers
{
    //Habilitamos el acceso al controlador de usuarios solo al administrador
    [CustomAuthorize(Nivel = 4)]
    public class UsuariosController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Usuarios
        public ActionResult Index()
        {
            var usuarios = db.Usuarios.Include(u => u.Nivel);
            ViewBag.Carreras = db.Carreras;
            List<Usuario> us = new List<Usuario>();
            foreach (Usuario usu in usuarios.Where(x => x.IdNivel == 3 || x.IdNivel == 4).ToList())
            {
                usu.Password = Security.Desencripta(usu.Password);
                us.Add(usu);
            }
            return View(us);
        }

        // GET: Usuarios/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Usuario usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }
            usuario.Password = Security.Desencripta(usuario.Password);
            return View(usuario);
        }

        // GET: Usuarios/Create
        public ActionResult Create()
        {
            ViewBag.IdNivel = new SelectList(db.Nivels, "IdNivel", "Descripcion");
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            return View();
        }

        // POST: Usuarios/Create
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que desea enlazarse. Para obtener 
        // más información vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                usuario.Password = Security.Encripta(usuario.Password);
                db.Usuarios.Add(usuario);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.IdNivel = new SelectList(db.Nivels, "IdNivel", "Descripcion", usuario.IdNivel);
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            return View(usuario);
        }

        // GET: Usuarios/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Usuario usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }
            ViewBag.IdNivel = new SelectList(db.Nivels, "IdNivel", "Descripcion", usuario.IdNivel);
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            usuario.Password = Security.Desencripta(usuario.Password);
            return View(usuario);
        }

        // POST: Usuarios/Edit/5
        // Para protegerse de ataques de publicación excesiva, habilite las propiedades específicas a las que desea enlazarse. Para obtener 
        // más información vea https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                usuario.Password = Security.Encripta(usuario.Password);
                usuario.Tiempo = DateTime.Now;
                db.Entry(usuario).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.IdNivel = new SelectList(db.Nivels, "IdNivel", "Descripcion", usuario.IdNivel);
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            return View(usuario);
        }

        // GET: Usuarios/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Usuario usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }
            return View(usuario);
        }

        // POST: Usuarios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Usuario usuario = db.Usuarios.Find(id);
            db.Usuarios.Remove(usuario);
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
