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
using Plataforma_Web.Models.PrimeraEntrevista;
using System.Globalization;
using System.Web.Script.Serialization;

namespace PlataformaWeb.Controllers.BecasTransporte
{
    public class EstudiantesController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Estudiantes
        public ActionResult Index()
        {


            return View(db.Estudiantes.ToList());
        }

        // GET: Estudiantes/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Estudiante estudiante = db.Estudiantes.Find(id);
            if (estudiante == null)
            {
                return HttpNotFound();
            }
            return View(estudiante);
        }

        // GET: Estudiantes/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Estudiantes/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,Nombre,ApellidoP,ApellidoM,Matricula,Sexo,IdTransporte,IdBeca,MontoBeca,IdCarrera,IdGrado,IdGrupo,IdTurno,Direccion")] Estudiante estudiante)
        {
            if (ModelState.IsValid)
            {
                db.Estudiantes.Add(estudiante);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(estudiante);
        }

        // GET: Estudiantes/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Estudiante estudiante = db.Estudiantes.Find(id);
            if (estudiante == null)
            {
                return HttpNotFound();
            }
            return View(estudiante);
        }

        // POST: Estudiantes/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Nombre,ApellidoP,ApellidoM,Matricula,Sexo,IdTransporte,IdBeca,MontoBeca,IdCarrera,IdGrado,IdGrupo,IdTurno,Direccion")] Estudiante estudiante)
        {
            if (ModelState.IsValid)
            {
                db.Entry(estudiante).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(estudiante);
        }

        // GET: Estudiantes/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Estudiante estudiante = db.Estudiantes.Find(id);
            if (estudiante == null)
            {
                return HttpNotFound();
            }
            return View(estudiante);
        }

        // POST: Estudiantes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Estudiante estudiante = db.Estudiantes.Find(id);
            db.Estudiantes.Remove(estudiante);
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

        public ActionResult ListadoAlumnos()
        {
            Usuario tutor = (Usuario)Session["Usuario"];



            return View();
        }


        public static int IdCarrera;

        [CustomAuthorize(Nivel = 2)]
        public ActionResult GruposPorTutor()
        {
            Usuario tutor = (Usuario)Session["Usuario"];
            #region director
            if (tutor != null)
            {
                EstudiantesController.IdCarrera = tutor.IdCarrera;

                if (tutor.IdNivel == 3)
                {
                    var time = DateTime.Now;
                    var paa = 0;
                    if (time.Month == 1 || time.Month == 2 || time.Month == 3 || time.Month == 4)
                    {
                        paa = 1;
                    }
                    else if (time.Month == 5 || time.Month == 6 || time.Month == 7 || time.Month == 8)
                    {
                        paa = 2;
                    }
                    else
                    {
                        paa = 3;
                    }
                    List<TutoriaGrupal> tutoriaas = db.TutoriaGrupals.Where(x => x.IdCarrera == tutor.IdCarrera && x.IdPeriodo == paa && x.Año == DateTime.Now.Year).OrderBy(x => new { x.IdGrado, x.IdGrupo }).ToList();
                    foreach (var item in tutoriaas)
                    {
                        var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                        item.Nomenclatura = x.ToString();
                    }


                    ViewBag.IdCarrea = IdCarrera;
                    ViewBag.Grupos = tutoriaas.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
                    return View();

                }
                #endregion
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdUsuario == tutor.IdUsuario && x.IdPeriodo == pa && x.Año == DateTime.Now.Year).OrderBy(x => new { x.IdGrado, x.IdGrupo }).ToList();
                foreach (var item in tutorias)
                {
                    var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                    item.Nomenclatura = x.ToString();
                }
                ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
                return View();
            }
            ViewBag.Mensaje = "Por favor inicia sesión.";
            return View();
        }


        [CustomAuthorize(Nivel = 2)]
        public ActionResult Grupo(int Id)
        {

            EstudiantesController.IdGrupo = Id;

            if (Id == null || Id == 0)
            {
                return RedirectToAction("Index");
            }


            var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == Id);


            ViewBag.AlumnosConBeca = db.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
            x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.IdBeca > 0).Select(x => x.Matricula).ToList();


            ViewBag.AlumnosConTransporte = db.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
            x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.IdTransporte > 0).Select(x => x.Matricula).ToList();



            grupo.Nomenclatura = grupo.Grado.Nombre.ToString() + grupo.Grupo.Nombre.ToString() + ", " + grupo.Turno.Nombre.ToString() + ", " + grupo.Periodo.Nombre.ToString();

            ViewBag.Grupo = grupo.Nomenclatura;

            List<Estudiante> estudiantesBT = db.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
           x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno).ToList();

            List<DatosPersonales> AlumnosDelGrupo = db.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                             x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.Año == grupo.Año &&
                             x.IdPeriodo == grupo.IdPeriodo).OrderBy(x => x.Nombre).ToList();

            List<SelectListItem> NombreBecas = new List<SelectListItem>();
            List<SelectListItem> nombreTransportes = new List<SelectListItem>();

            foreach (DatosPersonales alumno in AlumnosDelGrupo)
            {
                foreach (Estudiante estudiantee in estudiantesBT)
                {
                    if (estudiantee.Matricula == alumno.Matricula)
                    {
                        int idBeca = estudiantesBT.FirstOrDefault(x => x.Matricula == alumno.Matricula && x.IdCarrera == alumno.IdCarrera).IdBeca;
                        int idTransporte = estudiantesBT.FirstOrDefault(x => x.Matricula == alumno.Matricula && x.IdCarrera == alumno.IdCarrera).IdTransporte;

                        if (idBeca > 0)
                        {
                            SelectListItem beca = new SelectListItem();
                            beca.Value = alumno.Matricula;
                            beca.Text = db.Becas.FirstOrDefault(x => x.Id == idBeca).NombreBeca;
                            NombreBecas.Add(beca);
                        }

                        if (idTransporte > 0)
                        {
                            SelectListItem ruta = new SelectListItem();
                            ruta.Value = alumno.Matricula;
                            ruta.Text = db.Transportes.FirstOrDefault(x => x.Id == idTransporte).Ruta;
                            nombreTransportes.Add(ruta);

                        }
                    }
                }
            }

            ViewBag.Becas = NombreBecas;
            ViewBag.Transportes = nombreTransportes;
            return View(AlumnosDelGrupo);
        }

        private static int IdGrupo;

        [CustomAuthorize(Nivel = 2)]
        public ActionResult AsignarBeca(int id)
        {
            #region periodoActual
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }

            ViewBag.Grupo = (TutoriaGrupal)db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == IdGrupo && x.IdPeriodo == pa && x.Año == DateTime.Now.Year);

            #endregion Grupo
            #region mesesBeca
            List<SelectListItem> MesesBeca = new List<SelectListItem>();
            SelectListItem cantidadMes0 = new SelectListItem();
            cantidadMes0.Value = "0";
            cantidadMes0.Text = "0";
            MesesBeca.Add(cantidadMes0);
            SelectListItem cantidadMes1 = new SelectListItem();
            cantidadMes1.Value = "1";
            cantidadMes1.Text = "1";
            MesesBeca.Add(cantidadMes1);
            SelectListItem cantidadMes2 = new SelectListItem();
            cantidadMes2.Value = "2";
            cantidadMes2.Text = "2";
            MesesBeca.Add(cantidadMes2);
            SelectListItem cantidadMes4 = new SelectListItem();
            cantidadMes4.Value = "4";
            cantidadMes4.Text = "4";
            MesesBeca.Add(cantidadMes4);



            #endregion
            ViewBag.Becas = db.Becas.OrderBy(x => x.NombreBeca).ToList();
            ViewBag.RutasTransporte = db.Transportes.OrderBy(x => x.Ruta).ToList();
            DatosPersonales datosPersonales = db.DatosPersonales.Find(id);
            Estudiante estudiante = db.Estudiantes.FirstOrDefault(x => x.Matricula == datosPersonales.Matricula);

            if (estudiante != null)
            {
                foreach (var item in MesesBeca)
                {
                    if (estudiante.MesesBeca == Convert.ToInt32(item.Value))
                    {
                        item.Selected = true;
                    }
                }
            }

            ViewBag.Alumno = datosPersonales.Matricula + " - " + datosPersonales.Nombre;
            ViewBag.MesesBeca = MesesBeca;
            return View(estudiante);
        }


        [HttpPost]
        public ActionResult AsignarBeca([Bind(Include = "Id,Nombre,ApellidoM,ApellidoP,Matricula,Sexo,IdTransporte,IdCarrera,IdGrado,IdGrupo,IdTurno,Direccion,IdBeca,MontoBeca, DetallesBecaEstudiante,MesesBeca")] Estudiante estudiante)
        {
            ViewBag.Becas = db.Becas.OrderBy(x => x.NombreBeca).ToList();

            #region tutor
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }

            ViewBag.Grupo = (TutoriaGrupal)db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == IdGrupo && x.IdPeriodo == pa && x.Año == DateTime.Now.Year);

            #endregion tutor

            #region mesesBeca
            List<SelectListItem> MesesBeca = new List<SelectListItem>();
            SelectListItem cantidadMes0 = new SelectListItem();
            cantidadMes0.Value = "0";
            cantidadMes0.Text = "0";
            MesesBeca.Add(cantidadMes0);
            SelectListItem cantidadMes1 = new SelectListItem();
            cantidadMes1.Value = "1";
            cantidadMes1.Text = "1";
            MesesBeca.Add(cantidadMes1);
            SelectListItem cantidadMes2 = new SelectListItem();
            cantidadMes2.Value = "2";
            cantidadMes2.Text = "2";
            MesesBeca.Add(cantidadMes2);
            SelectListItem cantidadMes4 = new SelectListItem();
            cantidadMes4.Value = "4";
            cantidadMes4.Text = "4";
            MesesBeca.Add(cantidadMes4);
            #endregion


            ViewBag.MesesBeca = MesesBeca;


            if (estudiante.MesesBeca == 0)
            {
                ViewBag.Mensaje = "Selecciona cada cuantos meses recibe el estudiante la beca.";
                return View(estudiante);
            }

            ViewBag.RutasTransporte = db.Transportes.OrderBy(x => x.Ruta).ToList();
            DatosPersonales Alumno = db.DatosPersonales.Find(estudiante.Id);
            Estudiante existeEstudianteBeca = db.Estudiantes.FirstOrDefault(x => x.Matricula == Alumno.Matricula);
            ViewBag.Alumno = Alumno.Matricula + " - " + Alumno.Nombre;
            if (estudiante.MontoBeca == null || estudiante.IdBeca == 0 || estudiante.MontoBeca == 0)
            {
                return View(estudiante);
            }
            if (Alumno != null)
            {

                if (existeEstudianteBeca == null)
                {
                    Estudiante EstudianteBeca = new Estudiante();
                    EstudianteBeca.Id = 1;
                    EstudianteBeca.Nombre = Alumno.Nombre;
                    EstudianteBeca.ApellidoP = Alumno.Paterno;
                    EstudianteBeca.ApellidoM = Alumno.Materno;
                    EstudianteBeca.Direccion = Alumno.Direccion;
                    EstudianteBeca.IdCarrera = Alumno.IdCarrera;
                    EstudianteBeca.IdGrado = Alumno.IdGrado;
                    EstudianteBeca.IdGrupo = Alumno.IdGrupo;
                    EstudianteBeca.IdTurno = Alumno.IdTurno;
                    EstudianteBeca.Matricula = Alumno.Matricula;
                    EstudianteBeca.Sexo = Alumno.Sexo;
                    EstudianteBeca.IdCiudad = Alumno.IdCiudad;
                    EstudianteBeca.IdColonia = Alumno.IdColonia;
                    EstudianteBeca.Calle = Alumno.Calle;
                    EstudianteBeca.NumeroDireccion = Alumno.NumeroDireccion;
                    EstudianteBeca.periodoActual = Alumno.IdPeriodo;
                    EstudianteBeca.Año = Alumno.Año;
                    EstudianteBeca.Especialidad = Alumno.Especialidad;

                    EstudianteBeca.IdBeca = estudiante.IdBeca;
                    EstudianteBeca.MontoBeca = estudiante.MontoBeca;
                    EstudianteBeca.DetallesBecaEstudiante = estudiante.DetallesBecaEstudiante;
                    EstudianteBeca.MesesBeca = estudiante.MesesBeca;
                    try
                    {
                        db.Estudiantes.Add(EstudianteBeca);
                        db.SaveChanges();
                        //redireccionar a asignacion de beca
                        return RedirectToAction("Grupo", new { Id = IdGrupo });
                    }
                    catch (Exception e)
                    {

                        throw;
                    }

                }
                else
                {
                    using (ModeloPlataforma datab = new ModeloPlataforma())
                    {
                        Estudiante oEstudiante = db.Estudiantes.FirstOrDefault(x => x.Matricula == estudiante.Matricula);
                        estudiante.Id = oEstudiante.Id;

                        estudiante.Nombre = Alumno.Nombre;
                        estudiante.ApellidoP = Alumno.Paterno;
                        estudiante.ApellidoM = Alumno.Materno;
                        estudiante.Direccion = Alumno.Direccion;

                        estudiante.MesesBeca = estudiante.MesesBeca;
                        estudiante.Direccion = Alumno.Direccion;

                        estudiante.IdCarrera = Alumno.IdCarrera;
                        estudiante.IdGrado = Alumno.IdGrado;
                        estudiante.IdGrupo = Alumno.IdGrupo;
                        estudiante.IdTurno = Alumno.IdTurno;
                        estudiante.Matricula = Alumno.Matricula;

                        estudiante.IdCiudad = Alumno.IdCiudad;
                        estudiante.IdColonia = Alumno.IdColonia;
                        estudiante.Calle = Alumno.Calle;
                        estudiante.NumeroDireccion = Alumno.NumeroDireccion;
                        estudiante.IdTransporte = oEstudiante.IdTransporte;
                        estudiante.periodoActual = Alumno.IdPeriodo;
                        estudiante.Año = Alumno.Año;
                        estudiante.Especialidad = Alumno.Especialidad;
                        try
                        {
                            datab.Entry(estudiante).State = EntityState.Modified;
                            datab.SaveChanges();
                            //redireccionar a asignacion de beca
                            return RedirectToAction("Grupo", new { Id = IdGrupo });
                        }
                        catch (Exception e)
                        {

                            throw;
                        }

                    }
                }
            }
            return RedirectToAction("Grupo", new { Id = IdGrupo });
        }

        [CustomAuthorize(Nivel = 2)]
        public ActionResult AsignarTransporte(int id)
        {
            ViewBag.Becas = db.Becas.OrderBy(x => x.NombreBeca).ToList();
            ViewBag.RutasTransporte = db.Transportes.OrderBy(x => x.Ruta).ToList();
            DatosPersonales datosPersonales = db.DatosPersonales.Find(id);
            Estudiante estudiante = db.Estudiantes.FirstOrDefault(x => x.Matricula == datosPersonales.Matricula);


            #region tutor
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }

            ViewBag.Tutor = (TutoriaGrupal)db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == IdGrupo && x.IdPeriodo == pa && x.Año == DateTime.Now.Year);

            #endregion tutor

            ViewBag.Alumno = datosPersonales.Matricula + " - " + datosPersonales.Nombre;

            return View(estudiante);

        }

        [HttpPost]
        public ActionResult AsignarTransporte([Bind(Include = "Id,Nombre,ApellidoM,ApellidoP,Matricula,Sexo,IdTransporte,IdCarrera,IdGrado,IdGrupo,IdTurno,Direccion,IdBeca,MontoBeca")] Estudiante estudiante)
        {

            #region tutor
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }

            ViewBag.Tutor = (TutoriaGrupal)db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == IdGrupo && x.IdPeriodo == pa && x.Año == DateTime.Now.Year);

            #endregion tutor


            ViewBag.Becas = db.Becas.OrderBy(x => x.NombreBeca).ToList();
            ViewBag.RutasTransporte = db.Transportes.OrderBy(x => x.Ruta).ToList();
            DatosPersonales Alumno = db.DatosPersonales.Find(estudiante.Id);
            Estudiante existeEstudianteTransporte = db.Estudiantes.FirstOrDefault(x => x.Matricula == Alumno.Matricula);
            ViewBag.Alumno = Alumno.Matricula + " - " + Alumno.Nombre;

            if (estudiante.IdTransporte < 1)
            {
                return View(estudiante);
            }
            if (Alumno != null)
            {
                //HttpContext.Session.Add("Mensaje", "La funcion Asignar beca funciona.");

                if (existeEstudianteTransporte == null)
                {
                    Estudiante EstudianteTransporte = new Estudiante();
                    EstudianteTransporte.Id = 1;
                    EstudianteTransporte.Nombre = Alumno.Nombre;
                    EstudianteTransporte.ApellidoP = Alumno.Paterno;
                    EstudianteTransporte.ApellidoM = Alumno.Materno;
                    EstudianteTransporte.Direccion = Alumno.Direccion;
                    EstudianteTransporte.IdCarrera = Alumno.IdCarrera;
                    EstudianteTransporte.IdGrado = Alumno.IdGrado;
                    EstudianteTransporte.IdGrupo = Alumno.IdGrupo;
                    EstudianteTransporte.IdTurno = Alumno.IdTurno;
                    EstudianteTransporte.Matricula = Alumno.Matricula;
                    EstudianteTransporte.Sexo = Alumno.Sexo;
                    EstudianteTransporte.IdCiudad = Alumno.IdCiudad;
                    EstudianteTransporte.IdColonia = Alumno.IdColonia;
                    EstudianteTransporte.Calle = Alumno.Calle;
                    EstudianteTransporte.NumeroDireccion = Alumno.NumeroDireccion;
                    EstudianteTransporte.periodoActual = Alumno.IdPeriodo;
                    EstudianteTransporte.Año = Alumno.Año;
                    EstudianteTransporte.Especialidad = Alumno.Especialidad;

                    EstudianteTransporte.IdTransporte = estudiante.IdTransporte;

                    try
                    {
                        db.Estudiantes.Add(EstudianteTransporte);
                        db.SaveChanges();
                        //redireccionar a asignacion de beca
                        return RedirectToAction("Grupo", new { Id = IdGrupo });
                    }
                    catch (Exception e)
                    {

                        throw;
                    }

                }
                else
                {
                    using (ModeloPlataforma datab = new ModeloPlataforma())
                    {
                        Estudiante oEstudiante = db.Estudiantes.FirstOrDefault(x => x.Matricula == estudiante.Matricula);
                        estudiante.Id = oEstudiante.Id;
                        estudiante.Direccion = Alumno.Direccion;
                        estudiante.IdCiudad = Alumno.IdCiudad;
                        estudiante.IdColonia = Alumno.IdColonia;
                        estudiante.Calle = Alumno.Calle;
                        estudiante.NumeroDireccion = Alumno.NumeroDireccion;
                        estudiante.IdGrado = Alumno.IdGrado;
                        estudiante.IdGrupo = Alumno.IdGrupo;
                        estudiante.IdTurno = Alumno.IdTurno;
                        estudiante.IdBeca = oEstudiante.IdBeca;
                        estudiante.MontoBeca = oEstudiante.MontoBeca;
                        estudiante.MesesBeca = oEstudiante.MesesBeca;
                        estudiante.periodoActual = Alumno.IdPeriodo;
                        estudiante.Año = Alumno.Año;
                        estudiante.Especialidad = Alumno.Especialidad;

                        try
                        {
                            datab.Entry(estudiante).State = EntityState.Modified;
                            datab.SaveChanges();
                            //redireccionar a asignacion de beca
                            return RedirectToAction("Grupo", new { Id = IdGrupo });
                        }
                        catch (Exception e)
                        {

                            throw;
                        }

                    }
                }
            }
            return RedirectToAction("Grupo", new { Id = IdGrupo });
        }

        [CustomAuthorize(Nivel = 2)]
        public ActionResult RemoverBeca(int id)
        {
            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                DatosPersonales oAlumno = data.DatosPersonales.Find(id);
                Estudiante oEstudiante = data.Estudiantes.FirstOrDefault(x => x.Matricula == oAlumno.Matricula);

                try
                {
                    oEstudiante.IdBeca = 0;
                    oEstudiante.MontoBeca = 0;
                    oEstudiante.MesesBeca = 0;
                    oEstudiante.DetallesBecaEstudiante = null;
                    data.Entry(oEstudiante).State = EntityState.Modified;
                    data.SaveChanges();
                    return RedirectToAction("Grupo", new { Id = IdGrupo });

                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }

        [CustomAuthorize(Nivel = 2)]
        public ActionResult RemoverTransporte(int id)
        {
            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                DatosPersonales oAlumno = data.DatosPersonales.Find(id);
                Estudiante oEstudiante = data.Estudiantes.FirstOrDefault(x => x.Matricula == oAlumno.Matricula);

                try
                {
                    oEstudiante.IdTransporte = 0;

                    data.Entry(oEstudiante).State = EntityState.Modified;
                    data.SaveChanges();
                    return RedirectToAction("Grupo", new { Id = IdGrupo });

                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }


        [CustomAuthorize(Nivel = 3)]
        public ActionResult Estadisticas()
        {
            Usuario user = (Usuario)Session["Usuario"];

            int Id = user.IdCarrera;

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<Estudiante> estadisticasBeca = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0).ToList();
                List<Estudiante> estadisticasTransporte = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdTransporte > 0).ToList();
                #region Periodo Actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion


                var Turnos = data.TutoriaGrupals.Where(x => x.IdCarrera == user.IdCarrera && x.Año == tiempo.Year && x.IdPeriodo == pa).Select(x => x.IdTurno).Distinct();

                ViewBag.montoTotalPorCuatrimestre = montoTotalPorCuatrimestre(Id);
                int EstudiantesPorCarrera = data.DatosPersonales.Where(x => Turnos.Contains(x.IdTurno)).Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == pa && x.Año == DateTime.Now.Year && x.IdTurno > 0);
                ViewBag.EstudiantesPorCarrera = EstudiantesPorCarrera;
                ViewBag.cantidadAlumnosBeca = estadisticasBeca.Count();
                ViewBag.cantidadAlumnosTransporte = estadisticasTransporte.Count();
                ViewBag.IdCarrea = Id;
                return View();
            }
        }

        #region calculo de becas
        public string montoTotalPorCuatrimestre(int Id)
        {
            int montoTotal = 0;
            CultureInfo ci = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 1 && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 2 && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 4 && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }
            return montoTotal.ToString("C", ci);
        }
        public string montoTotalPorCuatrimestrePorGrupo(List<DatosPersonales> AlumnosPorGrupo, List<string> Matriculas)
        {
            int montoTotal = 0;
            CultureInfo formatoMoneda = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                foreach (DatosPersonales alumno in AlumnosPorGrupo)
                {
                    if (Matriculas.Contains(alumno.Matricula))
                    {
                        int montobeca = data.Estudiantes.FirstOrDefault(x => x.Matricula == alumno.Matricula).MontoBeca;
                        int vecesPorCuatri = data.Estudiantes.FirstOrDefault(x => x.Matricula == alumno.Matricula).MesesBeca;

                        switch (vecesPorCuatri)
                        {
                            case 1:
                                montobeca = montobeca * 4;
                                break;
                            case 2:
                                montobeca = montobeca * 2;
                                break;
                            case 4:
                                montobeca = montobeca * 1;
                                break;
                        }

                        montoTotal = montoTotal + montobeca;
                    }
                }
            }
            return montoTotal.ToString("C", formatoMoneda);
        }
        public string montoTotalPorCuatrimestrePorGrado(int Id, int IdGrado)
        {
            int montoTotal = 0;
            CultureInfo formatoMoneda = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 1 && x.IdGrado == IdGrado && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 2 && x.IdGrado == IdGrado && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 4 && x.IdGrado == IdGrado && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }


            return montoTotal.ToString("C", formatoMoneda);
        }
        public string montoTotalPorCuatrimestrePorEspecialidad(int Id, string Especialidad)
        {
            int montoTotal = 0;
            CultureInfo formatoMoneda = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 1 && x.Especialidad == Especialidad && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 2 && x.Especialidad == Especialidad && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 4 && x.Especialidad == Especialidad && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }


            return montoTotal.ToString("C", formatoMoneda);
        }
        public string cantidadPorTipoDeBeca(int Id, TutoriaGrupal grupo)
        {
            Usuario user = (Usuario)Session["Usuario"];

            int montoTotal = 0;
            CultureInfo ci = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 1 && x.periodoActual == pa && x.Año == tiempo.Year && x.IdGrado == grupo.IdGrado && x.IdGrupo == grupo.IdGrupo).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 2 && x.periodoActual == pa && x.Año == tiempo.Year && x.IdGrado == grupo.IdGrado && x.IdGrupo == grupo.IdGrupo).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 4 && x.periodoActual == pa && x.Año == tiempo.Year && x.IdGrado == grupo.IdGrado && x.IdGrupo == grupo.IdGrupo).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }
            return montoTotal.ToString("C", ci);
        }
        public string cantidadPorTipoDeBeca(int Id, int IdGrado)
        {
            Usuario user = (Usuario)Session["Usuario"];

            int montoTotal = 0;
            CultureInfo ci = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 1 && x.periodoActual == pa && x.Año == tiempo.Year && x.IdGrado == IdGrado ).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 2 && x.periodoActual == pa && x.Año == tiempo.Year && x.IdGrado == IdGrado).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 4 && x.periodoActual == pa && x.Año == tiempo.Year && x.IdGrado == IdGrado).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }
            return montoTotal.ToString("C", ci);
        }
        public string cantidadPorTipoDeBeca(int Id, string Especialidad)
        {
            Usuario user = (Usuario)Session["Usuario"];

            int montoTotal = 0;
            CultureInfo ci = new CultureInfo("es-MX");

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 1 && x.periodoActual == pa && x.Año == tiempo.Year && x.Especialidad == Especialidad).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 2 && x.periodoActual == pa && x.Año == tiempo.Year && x.Especialidad == Especialidad).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdBeca == Id && x.MesesBeca == 4 && x.periodoActual == pa && x.Año == tiempo.Year && x.Especialidad == Especialidad).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }
            return montoTotal.ToString("C", ci);
        }

        #endregion

        #region graficas por grupos

        [CustomAuthorize(Nivel = 3)]
        public ActionResult DetallesBeca()
        {
            Usuario user = (Usuario)Session["Usuario"];

            int id = user.IdCarrera;
            if (user.IdCarrera != id)
            {
                ViewBag.Mensaje = "No se pueden mostrar los datos.";
                ViewBag.idCarrera = IdCarrera;
                return View();
            }


            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<TutoriaGrupal> GruposMatutino = data.TutoriaGrupals.Where(x => x.IdCarrera == id && x.Año == DateTime.Now.Year && x.IdTurno == 1).OrderBy(x => new { x.IdGrado, x.IdGrupo }).ToList();
                List<SelectListItem> listaGruposMatutino = new List<SelectListItem>();
                listaGruposMatutino.Clear();
                foreach (var item in GruposMatutino)
                {
                    string grupo = data.Grupoes.Find(item.IdGrupo).Nombre;
                    string grado = data.Gradoes.Find(item.IdGrado).Nombre;
                    {
                        SelectListItem Grupo = new SelectListItem();
                        Grupo.Value = item.IdTutoriaGrupal.ToString();
                        Grupo.Text = grado + "° " + grupo;
                        listaGruposMatutino.Add(Grupo);
                    }
                }
                List<TutoriaGrupal> GruposVespertino = data.TutoriaGrupals.Where(x => x.IdCarrera == id && x.Año == DateTime.Now.Year && x.IdTurno == 2).OrderBy(x => new { x.IdGrado, x.IdGrupo }).ToList();
                List<SelectListItem> listaGruposVespertino = new List<SelectListItem>();
                listaGruposVespertino.Clear();
                foreach (var item in GruposVespertino)
                {
                    string grupo = data.Grupoes.Find(item.IdGrupo).Nombre;
                    string grado = data.Gradoes.Find(item.IdGrado).Nombre;
                    {
                        SelectListItem Grupo = new SelectListItem();
                        Grupo.Value = item.IdTutoriaGrupal.ToString();
                        Grupo.Text = grado + "° " + grupo;
                        listaGruposVespertino.Add(Grupo);
                    }
                }
                List<TutoriaGrupal> GruposDespresurizado = data.TutoriaGrupals.Where(x => x.IdCarrera == id && x.Año == DateTime.Now.Year && x.IdTurno == 3).OrderBy(x => new { x.IdGrado, x.IdGrupo }).ToList();
                List<SelectListItem> listaGruposDespresurizado = new List<SelectListItem>();
                listaGruposDespresurizado.Clear();
                foreach (var item in GruposDespresurizado)
                {
                    string grupo = data.Grupoes.Find(item.IdGrupo).Nombre;
                    string grado = data.Gradoes.Find(item.IdGrado).Nombre;
                    {
                        SelectListItem Grupo = new SelectListItem();
                        Grupo.Value = item.IdTutoriaGrupal.ToString();
                        Grupo.Text = grado + "° " + grupo;
                        listaGruposDespresurizado.Add(Grupo);
                    }
                }
                ViewBag.montoTotalPorCuatrimestre = montoTotalPorCuatrimestre(id);
                ViewBag.GruposMatutino = listaGruposMatutino;
                ViewBag.GruposVespertino = listaGruposVespertino;
                ViewBag.GruposDespresurizado = listaGruposDespresurizado;
                ViewBag.idCarrera = IdCarrera;
                return View();
            }
        }

        //id para cargar los datos de la grafica por grupo
        private static int idGrupoGrafica = 0;
        public ActionResult _Graficas(int id)
        {
            CultureInfo ci = new CultureInfo("es-MX");
            Usuario user = (Usuario)Session["Usuario"];
            EstudiantesController.idGrupoGrafica = id;
            var idgrupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id).IdGrupo;
            var idgrado = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id).IdGrado;
            string grupoText = db.Grupoes.Find(idgrupo).Nombre;
            string gradoText = db.Gradoes.Find(idgrado).Nombre;
            string gradoygrupo = gradoText + "° " + grupoText;
            HttpContext.Session.Add("Grupo", gradoygrupo);




            if (Session["Grupo"] != null)
            {
                var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id);

                List<DatosPersonales> TotalAlumnosPorGrupo = db.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                                              x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.Año == grupo.Año &&
                                              x.IdPeriodo == grupo.IdPeriodo).OrderBy(x => x.Nombre).ToList();

                List<string> AlumnosConBeca = db.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                     x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.IdBeca > 0).Select(x => x.Matricula).ToList();

                int CantidadAlumnosConBeca = 0;

                int CantidadTotalAlumnosPorGrupo = TotalAlumnosPorGrupo.Count();

                foreach (var alumno in TotalAlumnosPorGrupo)
                {
                    if (AlumnosConBeca.Contains(alumno.Matricula))
                    {
                        CantidadAlumnosConBeca++;
                    }
                }
                var Carrera = db.Carreras.Find(user.IdCarrera).Nombre;
                var AlumnosSinBeca = (CantidadTotalAlumnosPorGrupo - CantidadAlumnosConBeca);

                string monTotalCuatrimestrePorGrupo = montoTotalPorCuatrimestrePorGrupo(TotalAlumnosPorGrupo, AlumnosConBeca);
                //string monTotalCuatrimestre = montoTotalPorCuatrimestre(IdCarrera);


                //HttpContext.Session.Add("MontoTotalPorCuatrimestre", monTotalCuatrimestre);
                HttpContext.Session.Add("MontoTotalPorCuatrimestrePorGrupo", monTotalCuatrimestrePorGrupo);
                HttpContext.Session.Add("GradoDetalles", gradoText);
                HttpContext.Session.Add("GrupoDetalles", grupoText);
                HttpContext.Session.Add("CarreraDetalles", Carrera);
                HttpContext.Session.Add("TotalAlumnos", CantidadTotalAlumnosPorGrupo);
                HttpContext.Session.Add("AlumnosConBeca", CantidadAlumnosConBeca);
                HttpContext.Session.Add("AlumnosSinBeca", AlumnosSinBeca);
                HttpContext.Session.Add("IdGrupoDetalles", id);

                //informacion adicional para las becas
                List<Beca> listaBecas = db.Becas.OrderBy(x => x.NombreBeca).ToList();
                #region periodo actual
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
                #endregion

                List<SelectListItem> listaBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaCantidadAlumnosBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaMontoTotalBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaAlumnosViewBag = new List<SelectListItem>();

                foreach (var beca in listaBecas)
                {
                    BecaHelper item = new BecaHelper();
                    using (ModeloPlataforma data = new ModeloPlataforma())
                    {
                        item.Beca = beca.NombreBeca;

                        SelectListItem newOpcion1 = new SelectListItem();
                        newOpcion1.Value = beca.Id.ToString();
                        newOpcion1.Text = beca.NombreBeca;
                        listaBecasViewBag.Add(newOpcion1);

                        item.CantidadAlumnos = data.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                                                x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.IdBeca == beca.Id).Count();

                        SelectListItem newOpcion2 = new SelectListItem();

                        newOpcion2.Value = beca.Id.ToString();
                        newOpcion2.Text = item.CantidadAlumnos.ToString();

                        listaCantidadAlumnosBecasViewBag.Add(newOpcion2);


                        item.MontoTotalPorBeca = cantidadPorTipoDeBeca(beca.Id, grupo);

                        SelectListItem newOpcion3 = new SelectListItem();
                        newOpcion3.Value = beca.Id.ToString();
                        newOpcion3.Text = item.MontoTotalPorBeca;
                        listaMontoTotalBecasViewBag.Add(newOpcion3);



                        List<Estudiante> oEstudiantes = data.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                                                     x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.IdBeca == beca.Id).ToList();

                        if (oEstudiantes.Count > 0)
                        {
                            foreach (var est in oEstudiantes)
                            {
                                int becaTotal = 0;

                                switch (est.MesesBeca)
                                {
                                    case 1:
                                        {
                                            becaTotal = est.MontoBeca * 4;
                                            break;
                                        }
                                    case 2:
                                        {
                                            becaTotal = est.MontoBeca * 2;
                                            break;
                                        }
                                    case 4:
                                        {
                                            becaTotal = est.MontoBeca * 1;
                                            break;
                                        }
                                }

                                string matriculaNombre = est.Matricula + " - " + est.Nombre + " - Cantidad recibida: " + becaTotal.ToString("C", ci)  + "*";
                                item.ListaAlumnos += matriculaNombre;
                            }
                            item.ListaAlumnos += "Monto total: " + item.MontoTotalPorBeca;
                            SelectListItem newOpcion4 = new SelectListItem();
                            newOpcion4.Value = beca.Id.ToString();
                            newOpcion4.Text = item.ListaAlumnos.ToString();
                            listaAlumnosViewBag.Add(newOpcion4);

                        }
                        else
                        {

                            SelectListItem newOpcion4 = new SelectListItem();
                            newOpcion4.Value = beca.Id.ToString();
                            newOpcion4.Text = "NULL";
                            listaAlumnosViewBag.Add(newOpcion4);
                        }

                    }

                }
                
                HttpContext.Session.Add("ListaBecas", listaBecasViewBag);
                HttpContext.Session.Add("ListaCantidadAlumnos", listaCantidadAlumnosBecasViewBag);
                HttpContext.Session.Add("ListaMontoBecas", listaMontoTotalBecasViewBag);
                HttpContext.Session.Add("ListaAlumnos", listaAlumnosViewBag);


            }
            return RedirectToAction("DetallesBeca", new { id = user.IdCarrera });
        }

        public ContentResult GraficoBeca()
        {
            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                int id = EstudiantesController.idGrupoGrafica;
                string datosGrafica;
                if (id > 0)
                {
                    var grupo = data.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id);
                    EstudiantesController.idGrupoGrafica = 0;
                    var TotalAlumnosPorGrupo = data.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                                  x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.Año == grupo.Año &&
                                  x.IdPeriodo == grupo.IdPeriodo).OrderBy(x => x.Nombre).ToList();
                    var AlumnosConBeca = data.Estudiantes.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
                                         x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.IdBeca > 0).Select(x => x.Matricula).ToList();
                    int cantidadAlumnosConBeca = 0;
                    int cantidadTotalAlumnosPorGrupo = TotalAlumnosPorGrupo.Count();
                    foreach (var alumno in TotalAlumnosPorGrupo)
                    {
                        if (AlumnosConBeca.Contains(alumno.Matricula))
                        {
                            cantidadAlumnosConBeca++;
                        }
                    }
                    var idgrupo = data.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id).IdGrupo;
                    var idgrado = data.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id).IdGrado;

                    string grupoText = data.Grupoes.Find(idgrupo).Nombre;
                    string gradoText = data.Gradoes.Find(idgrado).Nombre;
                    string grupoBeca = gradoText + "° " + grupoText;
                    datosGrafica = "[['Alumnos del grupo " + grupoBeca + "', 'Cantidad']," +
                                            "['Con Beca'," + cantidadAlumnosConBeca + "]," +
                                            "['Sin Beca'," + (cantidadTotalAlumnosPorGrupo - cantidadAlumnosConBeca) + "]" +
                                            "]";
                    return Content(datosGrafica);
                }
                else
                {
                    datosGrafica = "";
                    return Content(datosGrafica);
                }
            }
        }

        #endregion

        #region graficas por grado
        [CustomAuthorize(Nivel = 3)]
        public ActionResult DetallesBecaGrado()
        {
            Usuario user = (Usuario)Session["Usuario"];
            int id = user.IdCarrera;
            List<SelectListItem> Grados = new List<SelectListItem>();

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<int> listaGradosPorCarrera = data.TutoriaGrupals.Where(x => x.IdCarrera == id && x.Año == DateTime.Now.Year && x.IdTurno > 0).OrderBy(x => x.IdGrado).Select(x => x.IdGrado).Distinct().ToList();
                Grados.Clear();
                foreach (var grdo in listaGradosPorCarrera)
                {
                    SelectListItem Grado = new SelectListItem();
                    Grado.Value = grdo.ToString();
                    Grado.Text = grdo + "° Grado";
                    Grados.Add(Grado);
                }

                ViewBag.montoTotalPorCuatrimestre = montoTotalPorCuatrimestre(id);
                ViewBag.Grados = Grados;
                return View();
            }
        }

        //id para cargar los datos de la grafica por grado
        private static int idGradoGrafica = 0;
        public ActionResult _GraficasGrado(int id)
        {
            #region periodo actual
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            #endregion

            Usuario user = (Usuario)Session["Usuario"];
            CultureInfo ci = new CultureInfo("es-MX");
            EstudiantesController.idGradoGrafica = id;
            string grado = id.ToString() + "° Grado";
            HttpContext.Session.Add("Grado", grado);

            if (Session["Grado"] != null)
            {

                List<DatosPersonales> TotalAlumnosPorGrado = db.DatosPersonales.Where(x => x.IdCarrera == user.IdCarrera && x.IdGrado == id && x.IdPeriodo == pa && x.Año == tiempo.Year).OrderBy(x => x.Nombre).ToList();
                List<string> AlumnosConBeca = db.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdGrado == id && x.IdBeca > 0 && x.periodoActual == pa && x.Año == tiempo.Year).Select(x => x.Matricula).ToList();
                int CantidadAlumnosConBeca = 0;
                int CantidadTotalAlumnosPorGrado = TotalAlumnosPorGrado.Count();
                foreach (var alumno in TotalAlumnosPorGrado)
                {
                    if (AlumnosConBeca.Contains(alumno.Matricula))
                    {
                        CantidadAlumnosConBeca++;
                    }
                }
                var Carrera = db.Carreras.Find(user.IdCarrera).Nombre;
                var AlumnosSinBeca = (CantidadTotalAlumnosPorGrado - CantidadAlumnosConBeca);

                string monTotalCuatrimestrePorGrado = montoTotalPorCuatrimestrePorGrado(user.IdCarrera,id);
                //string monTotalCuatrimestre = montoTotalPorCuatrimestre(IdCarrera);



                //informacion adicional para las becas
                List<Beca> listaBecas = db.Becas.OrderBy(x => x.NombreBeca).ToList();
                List<SelectListItem> listaBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaCantidadAlumnosBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaMontoTotalBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaAlumnosViewBag = new List<SelectListItem>();

                foreach (var beca in listaBecas)
                {
                    BecaHelper item = new BecaHelper();
                    using (ModeloPlataforma data = new ModeloPlataforma())
                    {
                        item.Beca = beca.NombreBeca;

                        SelectListItem newOpcion1 = new SelectListItem();
                        newOpcion1.Value = beca.Id.ToString();
                        newOpcion1.Text = beca.NombreBeca;
                        listaBecasViewBag.Add(newOpcion1);

                        item.CantidadAlumnos = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdGrado == id && x.IdBeca == beca.Id && x.periodoActual == pa && x.Año == tiempo.Year).Count();

                        SelectListItem newOpcion2 = new SelectListItem();

                        newOpcion2.Value = beca.Id.ToString();
                        newOpcion2.Text = item.CantidadAlumnos.ToString();

                        listaCantidadAlumnosBecasViewBag.Add(newOpcion2);


                        item.MontoTotalPorBeca = cantidadPorTipoDeBeca(beca.Id, id);

                        SelectListItem newOpcion3 = new SelectListItem();
                        newOpcion3.Value = beca.Id.ToString();
                        newOpcion3.Text = item.MontoTotalPorBeca;
                        listaMontoTotalBecasViewBag.Add(newOpcion3);



                        List<Estudiante> oEstudiantes = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdGrado == id  && x.IdBeca == beca.Id && x.periodoActual == pa && x.Año == tiempo.Year).ToList();

                        if (oEstudiantes.Count > 0)
                        {
                            foreach (var est in oEstudiantes)
                            {
                                int becaTotal = 0;

                                switch (est.MesesBeca)
                                {
                                    case 1:
                                        {
                                            becaTotal = est.MontoBeca * 4;
                                            break;
                                        }
                                    case 2:
                                        {
                                            becaTotal = est.MontoBeca * 2;
                                            break;
                                        }
                                    case 4:
                                        {
                                            becaTotal = est.MontoBeca * 1;
                                            break;
                                        }
                                }

                                string matriculaNombre = est.Matricula + " - " + est.Nombre + " - Cantidad recibida: " + becaTotal.ToString("C", ci) + "*";
                                item.ListaAlumnos += matriculaNombre;
                            }
                            item.ListaAlumnos += "Monto total: " + item.MontoTotalPorBeca;

                            SelectListItem newOpcion4 = new SelectListItem();
                            newOpcion4.Value = beca.Id.ToString();
                            newOpcion4.Text = item.ListaAlumnos.ToString();
                            listaAlumnosViewBag.Add(newOpcion4);

                        }
                        else
                        {

                            SelectListItem newOpcion4 = new SelectListItem();
                            newOpcion4.Value = beca.Id.ToString();
                            newOpcion4.Text = "NULL";
                            listaAlumnosViewBag.Add(newOpcion4);
                        }

                    }

                }

                HttpContext.Session.Add("ListaBecas", listaBecasViewBag);
                HttpContext.Session.Add("ListaCantidadAlumnos", listaCantidadAlumnosBecasViewBag);
                HttpContext.Session.Add("ListaMontoBecas", listaMontoTotalBecasViewBag);
                HttpContext.Session.Add("ListaAlumnos", listaAlumnosViewBag);


                //HttpContext.Session.Add("MontoTotalPorCuatrimestre", monTotalCuatrimestre);
                HttpContext.Session.Add("MontoTotalPorCuatrimestrePorGrado", monTotalCuatrimestrePorGrado);
                HttpContext.Session.Add("CarreraDetalles", Carrera);
                HttpContext.Session.Add("TotalAlumnos", CantidadTotalAlumnosPorGrado);
                HttpContext.Session.Add("AlumnosConBeca", CantidadAlumnosConBeca);
                HttpContext.Session.Add("AlumnosSinBeca", AlumnosSinBeca);
            }
            return RedirectToAction("DetallesBecaGrado");
        }

        public ContentResult GraficoBecaPorGrado()
        {
            Usuario user = (Usuario)Session["Usuario"];

            #region periodo actual
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            #endregion

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                int id = EstudiantesController.idGradoGrafica;
                string datosGrafica;
                if (id > 0)
                {
                    EstudiantesController.idGradoGrafica = 0;
                    var TotalAlumnosPorGrado = data.DatosPersonales.Where(x => x.IdCarrera == user.IdCarrera && x.IdGrado == id &&
                                    x.Año == tiempo.Year && x.IdPeriodo == pa).OrderBy(x => x.Nombre).ToList();
                    var AlumnosConBeca = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.IdGrado == id && x.IdBeca > 0 && x.Año == tiempo.Year && x.periodoActual == pa).Select(x => x.Matricula).ToList();
                    int cantidadAlumnosConBeca = 0;
                    int cantidadTotalAlumnosPorGrado = TotalAlumnosPorGrado.Count();
                    foreach (var alumno in TotalAlumnosPorGrado)
                    {
                        if (AlumnosConBeca.Contains(alumno.Matricula))
                        {
                            cantidadAlumnosConBeca++;
                        }
                    }

                    string gradoText = id.ToString() + "° Grado";
                    datosGrafica = "[['Alumnos por grado " + gradoText + "', 'Cantidad']," +
                                            "['Con Beca'," + cantidadAlumnosConBeca + "]," +
                                            "['Sin Beca'," + (cantidadTotalAlumnosPorGrado - cantidadAlumnosConBeca) + "]" +
                                            "]";
                    return Content(datosGrafica);
                }
                else
                {
                    datosGrafica = "";
                    return Content(datosGrafica);
                }
            }
        }

        #endregion

        #region graficas por especialidad
        [CustomAuthorize(Nivel = 3)]
        public ActionResult DetallesBecaEspecialidad()
        {
            Usuario user = (Usuario)Session["Usuario"];
            int id = user.IdCarrera;
            List<SelectListItem> Especialidades = new List<SelectListItem>();

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<Especialidad> listaEspecialidadesPorCarrera = data.Especialidads.Where(x => x.IdCarrera == id).OrderBy(x => x.Nombre).ToList();
                Especialidades.Clear();
                foreach (var especialidad in listaEspecialidadesPorCarrera)
                {
                    SelectListItem newEspecialidad = new SelectListItem();
                    newEspecialidad.Value = especialidad.Id.ToString();
                    newEspecialidad.Text = especialidad.Nombre;
                    Especialidades.Add(newEspecialidad);
                }

                ViewBag.montoTotalPorCuatrimestre = montoTotalPorCuatrimestre(id);
                ViewBag.Especialidades = Especialidades;
                return View();
            }
        }

        //id para cargar los datos de la grafica por grado
        private static string EspecialidadGrafico = null;
        public ActionResult _GraficasEspecialidad(int id)
        {
            #region periodo actual
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            #endregion

            Usuario user = (Usuario)Session["Usuario"];
            CultureInfo ci = new CultureInfo("es-MX");

            string especialidad = db.Especialidads.Find(id).Nombre;

            EstudiantesController.EspecialidadGrafico = especialidad;

            HttpContext.Session.Add("Especialidad", especialidad);

            if (Session["Especialidad"] != null)
            {

                List<DatosPersonales> TotalAlumnosPorEspecialidad = db.DatosPersonales.Where(x => x.IdCarrera == user.IdCarrera && x.Especialidad == especialidad && x.IdPeriodo == pa && x.Año == tiempo.Year).OrderBy(x => x.Nombre).ToList();
                List<string> AlumnosConBeca = db.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.Especialidad == especialidad && x.IdBeca > 0 && x.periodoActual == pa).Select(x => x.Matricula).ToList();
                int CantidadAlumnosConBeca = 0;
                int CantidadTotalAlumnosPorEspecialidad = TotalAlumnosPorEspecialidad.Count();
                foreach (var alumno in TotalAlumnosPorEspecialidad)
                {
                    if (AlumnosConBeca.Contains(alumno.Matricula))
                    {
                        CantidadAlumnosConBeca++;
                    }
                }
                var Carrera = db.Carreras.Find(user.IdCarrera).Nombre;
                var AlumnosSinBeca = (CantidadTotalAlumnosPorEspecialidad - CantidadAlumnosConBeca);

                string monTotalCuatrimestrePorEspecialidad = montoTotalPorCuatrimestrePorEspecialidad(user.IdCarrera, especialidad);
                //string monTotalCuatrimestre = montoTotalPorCuatrimestre(IdCarrera);


                //HttpContext.Session.Add("MontoTotalPorCuatrimestre", monTotalCuatrimestre);
                HttpContext.Session.Add("MontoTotalPorCuatrimestrePorEspecialidad", monTotalCuatrimestrePorEspecialidad);
                HttpContext.Session.Add("CarreraDetalles", Carrera);
                HttpContext.Session.Add("TotalAlumnos", CantidadTotalAlumnosPorEspecialidad);
                HttpContext.Session.Add("AlumnosConBeca", CantidadAlumnosConBeca);
                HttpContext.Session.Add("AlumnosSinBeca", AlumnosSinBeca);



                //informacion adicional para las becas
                List<Beca> listaBecas = db.Becas.OrderBy(x => x.NombreBeca).ToList();
                List<SelectListItem> listaBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaCantidadAlumnosBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaMontoTotalBecasViewBag = new List<SelectListItem>();
                List<SelectListItem> listaAlumnosViewBag = new List<SelectListItem>();

                foreach (var beca in listaBecas)
                {
                    BecaHelper item = new BecaHelper();
                    using (ModeloPlataforma data = new ModeloPlataforma())
                    {
                        item.Beca = beca.NombreBeca;

                        SelectListItem newOpcion1 = new SelectListItem();
                        newOpcion1.Value = beca.Id.ToString();
                        newOpcion1.Text = beca.NombreBeca;
                        listaBecasViewBag.Add(newOpcion1);

                        item.CantidadAlumnos = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.Especialidad == especialidad && x.IdBeca == beca.Id && x.periodoActual == pa && x.Año == tiempo.Year).Count();

                        SelectListItem newOpcion2 = new SelectListItem();

                        newOpcion2.Value = beca.Id.ToString();
                        newOpcion2.Text = item.CantidadAlumnos.ToString();

                        listaCantidadAlumnosBecasViewBag.Add(newOpcion2);


                        item.MontoTotalPorBeca = cantidadPorTipoDeBeca(beca.Id, especialidad);

                        SelectListItem newOpcion3 = new SelectListItem();
                        newOpcion3.Value = beca.Id.ToString();
                        newOpcion3.Text = item.MontoTotalPorBeca;
                        listaMontoTotalBecasViewBag.Add(newOpcion3);



                        List<Estudiante> oEstudiantes = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.Especialidad == especialidad && x.IdBeca == beca.Id && x.periodoActual == pa && x.Año == tiempo.Year).ToList();

                        if (oEstudiantes.Count > 0)
                        {
                            foreach (var est in oEstudiantes)
                            {
                                int becaTotal = 0;

                                switch (est.MesesBeca)
                                {
                                    case 1:
                                        {
                                            becaTotal = est.MontoBeca * 4;
                                            break;
                                        }
                                    case 2:
                                        {
                                            becaTotal = est.MontoBeca * 2;
                                            break;
                                        }
                                    case 4:
                                        {
                                            becaTotal = est.MontoBeca * 1;
                                            break;
                                        }
                                }

                                string matriculaNombre = est.Matricula + " - " + est.Nombre + " - Cantidad recibida: " + becaTotal.ToString("C", ci) + "*";
                                item.ListaAlumnos += matriculaNombre;
                            }
                            item.ListaAlumnos += "Monto total: " + item.MontoTotalPorBeca;
                            SelectListItem newOpcion4 = new SelectListItem();
                            newOpcion4.Value = beca.Id.ToString();
                            newOpcion4.Text = item.ListaAlumnos.ToString();
                            listaAlumnosViewBag.Add(newOpcion4);

                        }
                        else
                        {

                            SelectListItem newOpcion4 = new SelectListItem();
                            newOpcion4.Value = beca.Id.ToString();
                            newOpcion4.Text = "NULL";
                            listaAlumnosViewBag.Add(newOpcion4);
                        }

                    }

                }

                HttpContext.Session.Add("ListaBecas", listaBecasViewBag);
                HttpContext.Session.Add("ListaCantidadAlumnos", listaCantidadAlumnosBecasViewBag);
                HttpContext.Session.Add("ListaMontoBecas", listaMontoTotalBecasViewBag);
                HttpContext.Session.Add("ListaAlumnos", listaAlumnosViewBag);






            }
            return RedirectToAction("DetallesBecaEspecialidad");
        }

        public ContentResult GraficoBecaPorEspecialidad()
        {
            Usuario user = (Usuario)Session["Usuario"];

            #region periodo actual
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            #endregion

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                string especialidad = EstudiantesController.EspecialidadGrafico;
                string datosGrafica;
                if (!String.IsNullOrEmpty(especialidad))
                {
                    EstudiantesController.EspecialidadGrafico = null;
                    var TotalAlumnosPorEspecialidad = data.DatosPersonales.Where(x => x.IdCarrera == user.IdCarrera && x.Especialidad == especialidad &&
                                    x.Año == tiempo.Year && x.IdPeriodo == pa).OrderBy(x => x.Nombre).ToList();
                    var AlumnosConBeca = data.Estudiantes.Where(x => x.IdCarrera == user.IdCarrera && x.Especialidad == especialidad && x.IdBeca > 0 && x.Año == tiempo.Year && x.periodoActual == pa).Select(x => x.Matricula).ToList();
                    int cantidadAlumnosConBeca = 0;
                    int cantidadTotalAlumnosPorEspecialidad = TotalAlumnosPorEspecialidad.Count();
                    foreach (var alumno in TotalAlumnosPorEspecialidad)
                    {
                        if (AlumnosConBeca.Contains(alumno.Matricula))
                        {
                            cantidadAlumnosConBeca++;
                        }
                    }

                    
                    datosGrafica = "[['Alumnos por especialidad: " + especialidad + "', 'Cantidad']," +
                                            "['Con Beca'," + cantidadAlumnosConBeca + "]," +
                                            "['Sin Beca'," + (cantidadTotalAlumnosPorEspecialidad - cantidadAlumnosConBeca) + "]" +
                                            "]";
                    return Content(datosGrafica);
                }
                else
                {
                    datosGrafica = "";
                    return Content(datosGrafica);
                }
            }
        }

        #endregion



        public JsonResult obtenerDirecciones(int IdCarrera)
        {
            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<Estudiante> alumno = data.Estudiantes.Where(alum => alum.IdCarrera == IdCarrera).ToList();
                return Json(alumno, JsonRequestBehavior.AllowGet);
            }
        }

        [CustomAuthorize(Nivel = 3)]
        public ActionResult MapaTransporte()
        {

            Usuario user = (Usuario)Session["Usuario"];
            int IdCarreraa = user.IdCarrera;
            if (user.IdCarrera != IdCarreraa)
            {
                ViewBag.Mensaje = "No se pueden mostrar los datos.";
                ViewBag.idCarrera = IdCarreraa;
                return View();
            }

            List<Estudiante> alumno = db.Estudiantes.Where(alum => alum.IdCarrera == IdCarreraa && alum.IdTransporte != 0).ToList();
            JavaScriptSerializer js = new JavaScriptSerializer();
            ViewBag.Direcciones = js.Serialize(alumno);

            List<Transporte> rutas = db.Transportes.ToList();

            #region periodo actual
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            #endregion


            #region matutino

            List<int> IdRutasMatutino = db.Estudiantes.Where(x => x.IdCarrera == IdCarreraa && x.IdTransporte != 0 && x.IdTurno == 1).Select(x => x.IdTransporte).Distinct().ToList();
            List<string> RutasMatutino = new List<string>();
            foreach (int Id in IdRutasMatutino)
            {
                RutasMatutino.Add(db.Transportes.Find(Id).Ruta);
            }

            int CantidadAlumnosUsanTransporteM = alumno.Count(x => x.IdTurno == 1);
            int CantidadAlumnosReynosaM = alumno.Where(x => x.IdTurno == 1).Count(x => x.IdCiudad == 1);
            int CantidadAlumnosRBM = alumno.Where(x => x.IdTurno == 1).Count(x => x.IdCiudad == 2);

            List<string> ListaAlumnosReynosaM = new List<string>();
            List<string> ListaAlumnosRBM = new List<string>();

            foreach (Estudiante est in alumno)
            {
                if (est.IdCiudad == 1 && est.IdTurno == 1)
                {
                    string matriculaNombre = est.Matricula + " - " + est.Nombre;
                    ListaAlumnosReynosaM.Add(matriculaNombre);
                }
                else if (est.IdCiudad == 2 && est.IdTurno == 1)
                {
                    string matriculaNombre = est.Matricula + " - " + est.Nombre;
                    ListaAlumnosRBM.Add(matriculaNombre);
                }
            }


            List<SelectListItem> RutasCantidadesMat = new List<SelectListItem>();

            foreach (Transporte ruta in rutas)
            {
                SelectListItem rutaCantidad = new SelectListItem();

                rutaCantidad.Value = db.Estudiantes.Where(x => x.IdTransporte == ruta.Id && x.IdCarrera == IdCarrera && x.IdTurno == 1 && x.periodoActual == pa && x.Año == tiempo.Year).Count().ToString();
                rutaCantidad.Text = ruta.Ruta;
                RutasCantidadesMat.Add(rutaCantidad);
            }


            Estadistica EstadisticasMatutino = new Estadistica();
            EstadisticasMatutino.Rutas = RutasMatutino;
            EstadisticasMatutino.CantidadAlumnosUsanTransporte = CantidadAlumnosUsanTransporteM;
            EstadisticasMatutino.CantidadAlumnosReynosa = CantidadAlumnosReynosaM;
            EstadisticasMatutino.CantidadAlumnosRB = CantidadAlumnosRBM;
            EstadisticasMatutino.ListaAlumnosReynosa = ListaAlumnosReynosaM;
            EstadisticasMatutino.ListaAlumnosRB = ListaAlumnosRBM;
            #endregion


            #region Vespertino

            List<int> IdRutasVespertino = db.Estudiantes.Where(x => x.IdCarrera == IdCarreraa && x.IdTransporte != 0 && x.IdTurno == 2).Select(x => x.IdTransporte).Distinct().ToList();
            List<string> RutasVespertino = new List<string>();
            foreach (int Id in IdRutasVespertino)
            {
                RutasVespertino.Add(db.Transportes.Find(Id).Ruta);
            }

            int CantidadAlumnosUsanTransporteV = alumno.Count(x => x.IdTurno == 2);
            int CantidadAlumnosReynosaV = alumno.Where(x => x.IdTurno == 2).Count(x => x.IdCiudad == 1);
            int CantidadAlumnosRBV = alumno.Where(x => x.IdTurno == 2).Count(x => x.IdCiudad == 2);

            List<string> ListaAlumnosReynosaV = new List<string>();
            List<string> ListaAlumnosRBV = new List<string>();

            foreach (Estudiante est in alumno)
            {
                if (est.IdCiudad == 1 && est.IdTurno == 2)
                {
                    string matriculaNombre = est.Matricula + " - " + est.Nombre;
                    ListaAlumnosReynosaV.Add(matriculaNombre);
                }
                else if (est.IdCiudad == 2 && est.IdTurno == 2)
                {
                    string matriculaNombre = est.Matricula + " - " + est.Nombre;
                    ListaAlumnosRBV.Add(matriculaNombre);
                }
            }


            List<SelectListItem> RutasCantidadesVesp = new List<SelectListItem>();

            foreach (Transporte ruta in rutas)
            {
                SelectListItem rutaCantidad = new SelectListItem();

                int cantidad =  db.Estudiantes.Where(x => x.IdTransporte == ruta.Id && x.IdCarrera == IdCarreraa && x.IdTurno == 2 && x.periodoActual == pa && x.Año == tiempo.Year).Count();
                rutaCantidad.Value = cantidad.ToString();
                rutaCantidad.Text = ruta.Ruta;
                RutasCantidadesVesp.Add(rutaCantidad);
            }


            Estadistica EstadisticasVespertino = new Estadistica();
            EstadisticasVespertino.Rutas = RutasVespertino;
            EstadisticasVespertino.CantidadAlumnosUsanTransporte = CantidadAlumnosUsanTransporteV;
            EstadisticasVespertino.CantidadAlumnosReynosa = CantidadAlumnosReynosaV;
            EstadisticasVespertino.CantidadAlumnosRB = CantidadAlumnosRBV;
            EstadisticasVespertino.ListaAlumnosReynosa = ListaAlumnosReynosaV;
            EstadisticasVespertino.ListaAlumnosRB = ListaAlumnosRBV;
            #endregion


            #region Despresurizado

            List<int> IdRutasDespresurizado = db.Estudiantes.Where(x => x.IdCarrera == IdCarreraa && x.IdTransporte != 0 && x.IdTurno == 3).Select(x => x.IdTransporte).Distinct().ToList();
            List<string> RutasDespresurizado = new List<string>();
            foreach (int Id in IdRutasDespresurizado)
            {
                RutasDespresurizado.Add(db.Transportes.Find(Id).Ruta);
            }

            int CantidadAlumnosUsanTransporteD = alumno.Count(x => x.IdTurno == 3);
            int CantidadAlumnosReynosaD = alumno.Where(x => x.IdTurno == 3).Count(x => x.IdCiudad == 1);
            int CantidadAlumnosRBD = alumno.Where(x => x.IdTurno == 3).Count(x => x.IdCiudad == 2);

            List<string> ListaAlumnosReynosaD = new List<string>();
            List<string> ListaAlumnosRBD = new List<string>();

            foreach (Estudiante est in alumno)
            {
                if (est.IdCiudad == 1 && est.IdTurno == 3)
                {
                    string matriculaNombre = est.Matricula + " - " + est.Nombre;
                    ListaAlumnosReynosaD.Add(matriculaNombre);
                }
                else if (est.IdCiudad == 2 && est.IdTurno == 3)
                {
                    string matriculaNombre = est.Matricula + " - " + est.Nombre;
                    ListaAlumnosRBD.Add(matriculaNombre);
                }
            }

            List<SelectListItem> RutasCantidadesDesp = new List<SelectListItem>();

            foreach (Transporte ruta in rutas)
            {
                SelectListItem rutaCantidad = new SelectListItem();

                rutaCantidad.Value = db.Estudiantes.Where(x => x.IdTransporte == ruta.Id && x.IdCarrera == IdCarrera && x.IdTurno == 3 && x.periodoActual == pa && x.Año == tiempo.Year).Count().ToString();
                rutaCantidad.Text = ruta.Ruta;
                RutasCantidadesDesp.Add(rutaCantidad);
            }


            Estadistica EstadisticasDespresurizado = new Estadistica();
            EstadisticasDespresurizado.Rutas = RutasDespresurizado;
            EstadisticasDespresurizado.CantidadAlumnosUsanTransporte = CantidadAlumnosUsanTransporteD;
            EstadisticasDespresurizado.CantidadAlumnosReynosa = CantidadAlumnosReynosaD;
            EstadisticasDespresurizado.CantidadAlumnosRB = CantidadAlumnosRBD;
            EstadisticasDespresurizado.ListaAlumnosReynosa = ListaAlumnosReynosaD;
            EstadisticasDespresurizado.ListaAlumnosRB = ListaAlumnosRBD;
            #endregion


            ViewBag.RutasCantidadesMat = RutasCantidadesMat;
            ViewBag.RutasCantidadesVesp = RutasCantidadesVesp;
            ViewBag.RutasCantidadesDesp = RutasCantidadesDesp;

            ViewBag.EstadisticasMatutino = EstadisticasMatutino;
            ViewBag.EstadisticasVespertino = EstadisticasVespertino;
            ViewBag.EstadisticasDespresurizado = EstadisticasDespresurizado;
            ViewBag.IdCarrera = IdCarrera;

            return View();
        }

        [CustomAuthorize(Nivel = 3)]
        public ActionResult HistoiralBecasTransporte()
        {

            Usuario user = (Usuario)Session["Usuario"];
            int Id = user.IdCarrera;

            if (user.IdCarrera != Id)
            {
                ViewBag.Mensaje = "No se pueden mostrar los datos.";
                return View();
            }

            List<Historial> listaHistorial = db.Historials.Where(x => x.IdCarrera == Id).OrderByDescending(x => x.Fecha).ToList();

            return View(listaHistorial);
        }

    }
}
