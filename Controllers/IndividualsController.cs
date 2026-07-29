using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using PlataformaWeb;
using PlataformaWeb.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models;

namespace PlataformaWeb.Controllers
{
    [CustomAuthorize(Nivel = 2)]
    public class IndividualsController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Individuals (lista unificada de seguimientos por alumno)
        // GET: Individuals (lista unificada de seguimientos por alumno)
        public ActionResult Index(int id, string sortBy = "fecha", string dir = "desc", string searchQ = "")
        {
            // Alumno (encabezado)
            var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            ViewBag.Alumno = datos;
            ViewBag.AlumnoNombre = datos?.Nombre ?? "";

            // Calcular el cuatrimestre/grupo/año ACTUALES del alumno
            if (datos != null)
            {
                var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == datos.IdTurno);
                var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == datos.IdCarrera);
                var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == datos.IdGrado);
                var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == datos.IdGrupo);

                string pref = t?.Nombre == "Matutino" ? "M" :
                              t?.Nombre == "Vespertino" ? "I" :
                              t?.Nombre == "Despresurizado" ? "D" : "";
                string grupoActual = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

                var mes = DateTime.Now.Month;
                string cuatriActual;
                if (mes <= 4) cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
                else if (mes <= 8) cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
                else cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

                int anioActual = DateTime.Now.Year;

                // Enviar los datos actuales a la vista
                ViewBag.GrupoActual = grupoActual;
                ViewBag.CuatriActual = cuatriActual;
                ViewBag.AnioActual = anioActual;
            }

            // Hoja más reciente (para "Agregar seguimiento")
            var idIndReciente = db.Individuals
                .Where(i => i.IdPersona == id)
                .OrderByDescending(i => i.Fecha)
                .Select(i => i.IdIndividual)
                .FirstOrDefault();
            ViewBag.IdIndReciente = idIndReciente;

            // Query base
            var q = from s in db.Seguimientoes
                    join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                    where i.IdPersona == id
                    select new SeguimientoPlanoVM
                    {
                        IdSeguimiento = s.IdSeguimiento,
                        IdIndividual = s.IdIndividual,
                        Fecha = s.Fecha,
                        Vulnerabilidad = s.Vulnerabilidad,
                        Problematica = s.Problematica,
                        Accion = s.Accion,
                        Grupo = i.Grupo,               // ej. MTI6C
                        Cuatrimestre = i.Cuatrimestre, // ej. Septiembre - Diciembre
                        Anio = i.Fecha.Year
                    };

            // Filtro de búsqueda (texto libre)
            if (!string.IsNullOrWhiteSpace(searchQ))
            {
                var qLower = searchQ.ToLower();
                q = q.Where(x =>
                    (x.Vulnerabilidad ?? "").ToLower().Contains(qLower) ||
                    (x.Problematica ?? "").ToLower().Contains(qLower) ||
                    (x.Accion ?? "").ToLower().Contains(qLower) ||
                    (x.Grupo ?? "").ToLower().Contains(qLower) ||
                    (x.Cuatrimestre ?? "").ToLower().Contains(qLower) ||
                    x.Anio.ToString().Contains(qLower)
                );
            }

            // Orden dinámico
            bool desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sortBy ?? "fecha").ToLower())
            {
                case "grupo":
                    q = desc
                        ? q.OrderByDescending(x => x.Grupo)
                           .ThenByDescending(x => x.Anio)
                           .ThenByDescending(x => x.Cuatrimestre)
                           .ThenByDescending(x => x.Fecha)
                        : q.OrderBy(x => x.Grupo)
                           .ThenBy(x => x.Anio)
                           .ThenBy(x => x.Cuatrimestre)
                           .ThenBy(x => x.Fecha);
                    break;

                case "cuatri":
                    q = desc
                        ? q.OrderByDescending(x => x.Anio)
                           .ThenByDescending(x => x.Cuatrimestre)
                           .ThenByDescending(x => x.Grupo)
                           .ThenByDescending(x => x.Fecha)
                        : q.OrderBy(x => x.Anio)
                           .ThenBy(x => x.Cuatrimestre)
                           .ThenBy(x => x.Grupo)
                           .ThenBy(x => x.Fecha);
                    break;

                case "fecha":
                default:
                    q = desc
                        ? q.OrderByDescending(x => x.Fecha)
                           .ThenByDescending(x => x.IdSeguimiento)
                        : q.OrderBy(x => x.Fecha)
                           .ThenBy(x => x.IdSeguimiento);
                    break;
            }

            // Mantener selección en la vista
            ViewBag.SortBy = sortBy;
            ViewBag.Dir = desc ? "desc" : "asc";
            ViewBag.SearchQ = searchQ;

            var segs = q.AsNoTracking().ToList();
            return View(segs);
        }



        // ===== Reporte (imprimir todos los seguimientos del alumno dentro del MISMO periodo) =====
        // El parámetro 'id' ahora representa IdSeguimiento (el seguimiento sobre el que el usuario hizo click).
        // El reporte trae todos los seguimientos del alumno cuya fecha cae en el mismo periodo cuatrimestral
        // (1: ene-abr, 2: may-ago, 3: sep-dic) y el mismo año que el seguimiento seleccionado,
        // ordenados del más viejo al más reciente.
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var segPicked = db.Seguimientoes.AsNoTracking().FirstOrDefault(x => x.IdSeguimiento == id);
            if (segPicked == null) return HttpNotFound();

            var individual = db.Individuals.AsNoTracking().FirstOrDefault(x => x.IdIndividual == segPicked.IdIndividual);
            if (individual == null) return HttpNotFound();

            try
            {
                // Determinar el periodo (cuatrimestre) del seguimiento elegido.
                int mes = segPicked.Fecha.Month;
                int anio = segPicked.Fecha.Year;
                int mesIni, mesFin;
                if (mes <= 4) { mesIni = 1; mesFin = 4; }
                else if (mes <= 8) { mesIni = 5; mesFin = 8; }
                else { mesIni = 9; mesFin = 12; }

                int idPersona = individual.IdPersona;

                // Todos los seguimientos del alumno dentro del mismo periodo+año, ordenados ASC por fecha.
                var seguimientosRaw = (from sg in db.Seguimientoes
                                       join ind in db.Individuals on sg.IdIndividual equals ind.IdIndividual
                                       where ind.IdPersona == idPersona
                                          && sg.Fecha.Year == anio
                                          && sg.Fecha.Month >= mesIni
                                          && sg.Fecha.Month <= mesFin
                                       orderby sg.Fecha ascending, sg.IdSeguimiento ascending
                                       select sg)
                                       .AsNoTracking()
                                       .ToList();

                // Etiquetar cada seguimiento como "Body" o "Tail". El reporte usa este campo para
                // formar un grupo de orfandad (KeepTogether) que pega los últimos 2 renglones a la
                // fila de la firma, evitando que la firma quede sola en una hoja vacía.
                int total = seguimientosRaw.Count;
                var s = seguimientosRaw.Select((sg, idx) => new SeguimientoConBloque
                {
                    IdSeguimiento = sg.IdSeguimiento,
                    IdIndividual = sg.IdIndividual,
                    Fecha = sg.Fecha,
                    Vulnerabilidad = sg.Vulnerabilidad,
                    Problematica = sg.Problematica,
                    Accion = sg.Accion,
                    Bloque = (idx >= total - 2) ? "Tail" : "Body"
                }).ToList();

                // Resolver la carrera "histórica" correcta para mostrar en el encabezado del reporte.
                // El campo Carrera puede haber quedado obsoleto si el alumno cambió de TSU a Ingeniería;
                // la columna Especialidad (en datos modernos) ya trae la denominación completa correcta.
                individual.Carrera = ResolveCarreraHistorica(individual);

                var i = new List<Individual> { individual };

                var report1 = new Microsoft.Reporting.WebForms.ReportViewer();
                var rds = new Microsoft.Reporting.WebForms.ReportDataSource
                {
                    Value = i,
                    Name = "IndividualAlumno"
                };
                var rds2 = new Microsoft.Reporting.WebForms.ReportDataSource
                {
                    Value = s,
                    Name = "SeguimientoAlumno"
                };

                report1.LocalReport.EnableExternalImages = true;
                report1.LocalReport.DataSources.Add(rds);
                report1.LocalReport.DataSources.Add(rds2);
                report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/rptSeguimientoTutIndividual.rdlc");
                ViewBag.ReportViewer = report1;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = ex.Message;
            }
            return View(individual);
        }

        // Devuelve la denominación de carrera con el nivel correcto para esa hoja histórica.
        // Prefiere Especialidad cuando ya viene con prefijo de nivel (TSU / Ingeniería / Licenciatura),
        // y cae al campo Carrera legacy cuando Especialidad solo trae la sub-área.
        private static string ResolveCarreraHistorica(Individual ind)
        {
            if (ind == null) return "";

            var esp = (ind.Especialidad ?? "").Trim();
            var espUp = esp.ToUpperInvariant();

            // Caso moderno: Especialidad ya viene con el nivel correcto.
            if (espUp.StartsWith("TSU ") ||
                espUp.StartsWith("INGENIERÍA ") || espUp.StartsWith("INGENIERIA ") ||
                espUp.StartsWith("LICENCIATURA "))
            {
                return esp;
            }

            // Legacy: Especialidad es solo la sub-área. Conservar el Carrera histórico.
            if (!string.IsNullOrWhiteSpace(ind.Carrera))
                return ind.Carrera;

            return esp;
        }

        // --- INICIO DE CAMBIOS ---

        // 1. AÑADE ESTA NUEVA ACCIÓN "GATEWAY"
        public ActionResult PrepararNuevoSeguimiento(int id) // 'id' es IdPersona
        {
            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            if (dp == null) return HttpNotFound();

            // Rol Director = solo lectura: este gateway CREA una hoja (INSERT) al vuelo.
            // Para un Director no se crea nada; se le manda a la vista de seguimientos.
            var _usrDir = Session["Usuario"] as Usuario;
            if (_usrDir != null && _usrDir.EsDirector)
            {
                TempData["ToastInfo"] = "Cuenta de Director: modo solo lectura.";
                return RedirectToAction("Index", new { id });
            }

            // 1. Obtener la hoja (Individual) más reciente de este alumno
            var hojaReciente = db.Individuals
                .Where(i => i.IdPersona == id)
                .OrderByDescending(i => i.Fecha)
                .FirstOrDefault();

            // 2. Calcular cuál SERÍA el grupo y cuatri actual del alumno
            var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == dp.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == dp.IdCarrera);
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == dp.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == dp.IdGrupo);

            string pref = t?.Nombre == "Matutino" ? "M" :
                          t?.Nombre == "Vespertino" ? "I" :
                          t?.Nombre == "Despresurizado" ? "D" : "";
            string grupoActual = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

            var mes = DateTime.Now.Month;
            string cuatriActual;
            if (mes <= 4) cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
            else if (mes <= 8) cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
            else cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

            int anioActual = DateTime.Now.Year;

            // 3. Comparar
            bool crearNueva = false;
            if (hojaReciente == null)
            {
                crearNueva = true; // No tiene ninguna hoja, hay que crear la primera
            }
            else if (hojaReciente.Grupo != grupoActual ||
                     hojaReciente.Cuatrimestre != cuatriActual ||
                     hojaReciente.Fecha.Year != anioActual)
            {
                crearNueva = true; // El grupo, cuatri o año son diferentes
            }

            // 4. Actuar
            if (crearNueva)
            {
                // Crear la nueva hoja usando el helper
                var nuevaHoja = CrearHojaParaAlumno(id); // 'id' es idPersona
                if (nuevaHoja == null) return HttpNotFound();

                TempData["ToastOk"] = $"Hoja nueva creada: {nuevaHoja.Grupo} • {nuevaHoja.Cuatrimestre} {nuevaHoja.Fecha.Year}.";

                // Redirigir a crear un SEGUIMIENTO para esa NUEVA hoja
                return RedirectToAction("Create", "Seguimientoes", new { id = nuevaHoja.IdIndividual });
            }
            else
            {
                // Usar la hoja existente
                // Redirigir a crear un SEGUIMIENTO para la hoja RECIENTE
                return RedirectToAction("Create", "Seguimientoes", new { id = hojaReciente.IdIndividual });
            }
        }

        // 2. AÑADE ESTE MÉTODO HELPER PRIVADO (Lógica movida de las acciones "Create" originales)
        private Individual CrearHojaParaAlumno(int idPersona)
        {
            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);
            if (dp == null) return null;

            var individual = new Individual
            {
                IdPersona = idPersona,
                Fecha = DateTime.Now,
                Nombre = dp.Nombre,
                Matricula = dp.Matricula,
                // Carrera, Especialidad, Area
                Carrera = dp.CarreraNom,
                Especialidad = dp.Especialidad ?? "",
                Area = dp.Area
            };

            // Construcción de nomenclatura de grupo
            var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == dp.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == dp.IdCarrera);
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == dp.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == dp.IdGrupo);

            string pref = t?.Nombre == "Matutino" ? "M" :
                          t?.Nombre == "Vespertino" ? "I" :
                          t?.Nombre == "Despresurizado" ? "D" : "";
            individual.Grupo = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

            // Cuatrimestre por mes
            var mes = DateTime.Now.Month;
            if (mes <= 4) individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
            else if (mes <= 8) individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
            else individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

            // Lógica adicional de las acciones Create (sobreescribir Carrera/Area si es TSU/Ing)
            if (!string.IsNullOrEmpty(individual.Especialidad) &&
                (individual.Especialidad.StartsWith("Ingeniería") || individual.Especialidad.StartsWith("Licenciatura")))
            {
                individual.Carrera = individual.Especialidad;
                individual.Area = "";
            }
            else
            {
                individual.Carrera = "TSU en " + (c?.Nombre ?? "");
                individual.Area = individual.Especialidad ?? "";
            }

            db.Individuals.Add(individual);
            db.SaveChanges();
            return individual;
        }

        // ===== Edit / Delete de Hojas (se conservan) =====

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var individual = db.Individuals.Find(id);
            if (individual == null) return HttpNotFound();

            var idp = individual.IdPersona;
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idp);
            ViewBag.AlumnoNombre = (ViewBag.Alumno as DatosPersonales)?.Nombre ?? "";
            return View(individual);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Individual individual)
        {
            if (individual == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == individual.IdPersona);
            if (dp == null) return HttpNotFound();

            individual.Fecha = DateTime.Now;
            individual.Nombre = dp.Nombre;
            individual.Matricula = dp.Matricula;

            var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == dp.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == dp.IdCarrera);
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == dp.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == dp.IdGrupo);

            string pref = t?.Nombre == "Matutino" ? "M" :
              t?.Nombre == "Vespertino" ? "I" :
              t?.Nombre == "Despresurizado" ? "D" : "";
            // Simplemente quitas {pref} de la línea siguiente:
            individual.Grupo = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

            if (!string.IsNullOrEmpty(individual.Especialidad) &&
                (individual.Especialidad.StartsWith("Ingeniería") || individual.Especialidad.StartsWith("Licenciatura")))
            {
                individual.Carrera = individual.Especialidad;
                individual.Area = "";
            }
            else
            {
                individual.Carrera = "TSU en " + (c?.Nombre ?? "");
                individual.Area = individual.Especialidad ?? "";
            }

            var mes = DateTime.Now.Month;
            if (mes <= 4) individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
            else if (mes <= 8) individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
            else individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

            var idPersona = individual.IdPersona;

            if (ModelState.IsValid)
            {
                db.Entry(individual).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index", new { id = idPersona });
            }

            var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);
            ViewBag.Alumno = alumno;
            ViewBag.AlumnoNombre = alumno?.Nombre ?? "";
            return View(individual);
        }

        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var individual = db.Individuals.Find(id);
            if (individual == null) return HttpNotFound();

            var idp = individual.IdPersona;
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idp);
            ViewBag.AlumnoNombre = (ViewBag.Alumno as DatosPersonales)?.Nombre ?? "";
            return View(individual);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var individual = db.Individuals.Find(id);
            if (individual == null) return HttpNotFound();

            var idp = individual.IdPersona;

            var seg = db.Seguimientoes
                        .Where(x => x.IdIndividual == individual.IdIndividual)
                        .ToList();

            if (seg != null && seg.Count > 0)
            {
                db.Seguimientoes.RemoveRange(seg);
            }

            db.Individuals.Remove(individual);
            db.SaveChanges();
            return RedirectToAction("Index", new { id = idp });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // VM auxiliar para el reporte de seguimiento individual: igual que Seguimiento + un campo Bloque
    // ("Body" o "Tail") usado por la fila de la firma para mantener los últimos 2 renglones juntos.
    public class SeguimientoConBloque
    {
        public int IdSeguimiento { get; set; }
        public int IdIndividual { get; set; }
        public DateTime Fecha { get; set; }
        public string Vulnerabilidad { get; set; }
        public string Problematica { get; set; }
        public string Accion { get; set; }
        public string Bloque { get; set; }
    }
}