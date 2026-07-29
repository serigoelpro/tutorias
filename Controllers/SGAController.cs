using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb.BecasTransporte.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.SGA
{
    public class SGAController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // Dashboard principal con filtros
        public ActionResult Dashboard(int? idCarrera, int? idEspecialidad)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];
            var model = new DashboardResumenViewModel();
            return View("~/Views/SGA/Dashboard.cshtml", model);
        }

        // Gestin de Tutores
        public ActionResult AsignarAsesores()
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            var tutores = ObtenerTutoresPorCarrera(usuario);
            return View("~/Views/SGA/AsignarAsesores/Index.cshtml", tutores);
        }

        // Estudiantes
        public ActionResult Estudiantes(int? carrera, int? grupo, int? grado)
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            var estudiantes = ObtenerEstudiantesFiltrados(usuario, carrera, grupo, grado);
            ViewBag.Carreras = ObtenerCarreras(usuario);
            ViewBag.Grupos = db.Grupoes.ToList();
            ViewBag.Grados = db.Gradoes.ToList();
            return View("~/Views/SGA/Estudiantes/Index.cshtml", estudiantes);
        }

        // Entrevistas
        public ActionResult Entrevistas()
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            return View("~/Views/SGA/Entrevistas/Index.cshtml");
        }

        // PATs
        public ActionResult PATs()
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            return View("~/Views/SGA/PATs/Index.cshtml");
        }

        // Becas
        public ActionResult Becas()
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            return View("~/Views/SGA/Becas/Index.cshtml");
        }

        // Reportes
        public ActionResult Reportes()
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            return View("~/Views/SGA/Reportar/Index.cshtml");
        }

        // Estadsticas
        public ActionResult Estadisticas()
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            var estadisticas = GenerarEstadisticas(usuario);
            return View("~/Views/SGA/Estadisticas/Index.cshtml", estadisticas);
        }

        // NUEVA FUNCIONALIDAD: Filtrar por Especialidad
        // GET: SGA/Especialidad
        public ActionResult Especialidad()
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            ViewBag.Title = "Filtrar por Especialidad";

            try
            {
                // Lista de especialidades ordenadas alfabticamente
                ViewBag.Especialidades = new SelectList(
                    db.Especialidads.OrderBy(e => e.Nombre)
                                   .Select(e => new { e.Id, e.Nombre }),
                    "Id", "Nombre"
                );

                return View("~/Views/SGA/Entrevista/Especialidad.cshtml");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar las especialidades: " + ex.Message;
                return View("~/Views/SGA/Entrevista/Especialidad.cshtml");
            }
        }

        // POST: SGA/Especialidad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Especialidad(int idEspecialidad)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            ViewBag.Title = "Resultados por Especialidad";

            try
            {
                // Validar que se haya seleccionado una especialidad vlida
                if (idEspecialidad <= 0)
                {
                    ViewBag.Error = "Por favor seleccione una especialidad vlida.";
                    ViewBag.Especialidades = new SelectList(
                        db.Especialidads.OrderBy(e => e.Nombre)
                                       .Select(e => new { e.Id, e.Nombre }),
                        "Id", "Nombre"
                    );
                    return View("~/Views/SGA/Entrevista/Especialidad.cshtml");
                }

                // Obtener el nombre de la especialidad seleccionada
                var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == idEspecialidad);
                if (especialidad == null)
                {
                    ViewBag.Error = "La especialidad seleccionada no existe.";
                    ViewBag.Especialidades = new SelectList(
                        db.Especialidads.OrderBy(e => e.Nombre)
                                       .Select(e => new { e.Id, e.Nombre }),
                        "Id", "Nombre"
                    );
                    return View("~/Views/SGA/Entrevista/Especialidad.cshtml");
                }

                // Filtrar las entrevistas iniciales por especialidad
                var entrevistas = db.EntrevistaInicials
                                   .Where(i => i.Especialidad == especialidad.Nombre)
                                   .OrderBy(i => i.Nombre)
                                   .ToList();

                ViewBag.EspecialidadSeleccionada = especialidad.Nombre;
                ViewBag.TotalResultados = entrevistas.Count;

                if (entrevistas.Count == 0)
                {
                    ViewBag.Mensaje = $"No se encontraron entrevistas para la especialidad '{especialidad.Nombre}'.";
                }

                return View("~/Views/SGA/Entrevista/ResultadosEspecialidad.cshtml", entrevistas);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al procesar la consulta: " + ex.Message;
                ViewBag.Especialidades = new SelectList(
                    db.Especialidads.OrderBy(e => e.Nombre)
                                   .Select(e => new { e.Id, e.Nombre }),
                    "Id", "Nombre"
                );
                return View("~/Views/SGA/Entrevista/Especialidad.cshtml");
            }
        }

        // CRUD para Especialidades (Programas Educativos)
        public ActionResult Especialidades()
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];
            var especialidades = ObtenerEspecialidadesPorUsuario(usuario);
            var carreras = db.Carreras.ToList();
            var dictCarreras = carreras.ToDictionary(c => c.IdCarrera, c => c.Nombre);
            ViewBag.CarrerasDict = dictCarreras;

            ViewBag.Title = "Gestión de Programas Educativos";
            return View("~/Views/SGA/Especialidades/Index.cshtml", especialidades);
        }

        public ActionResult CreateEspecialidad()
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];
            ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
            ViewBag.Title = "Crear Programa Educativo";

            return View("~/Views/SGA/Especialidades/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateEspecialidad(Especialidad especialidad)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];

            try
            {
                if (ModelState.IsValid)
                {
                    // Validar que el usuario tenga permisos para la carrera
                    if (usuario.IdNivel != 4 && especialidad.IdCarrera != usuario.IdCarrera)
                    {
                        ViewBag.Error = "No tiene permisos para crear especialidades en esta carrera.";
                        ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
                        return View("~/Views/SGA/Especialidades/Create.cshtml", especialidad);
                    }

                    // Verificar que no exista una especialidad con el mismo nombre en la misma carrera
                    var existeEspecialidad = db.Especialidads
                        .Any(e => e.Nombre.ToLower() == especialidad.Nombre.ToLower() &&
                                 e.IdCarrera == especialidad.IdCarrera);

                    if (existeEspecialidad)
                    {
                        ViewBag.Error = "Ya existe una especialidad con este nombre en la carrera seleccionada.";
                        ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
                        return View("~/Views/SGA/Especialidades/Create.cshtml", especialidad);
                    }

                    db.Especialidads.Add(especialidad);
                    db.SaveChanges();

                    TempData["Success"] = "Programa educativo creado exitosamente.";
                    return RedirectToAction("Especialidades");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al crear el programa educativo: " + ex.Message;
            }

            ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
            return View("~/Views/SGA/Especialidades/Create.cshtml", especialidad);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditEspecialidad(Especialidad especialidad)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];

            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar permisos
                    if (usuario.IdNivel != 4 && especialidad.IdCarrera != usuario.IdCarrera)
                    {
                        ViewBag.Error = "No tiene permisos para editar este programa educativo.";
                        ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
                        return View("~/Views/SGA/Especialidades/Edit.cshtml", especialidad);
                    }

                    // Verificar que no exista otra especialidad con el mismo nombre en la misma carrera
                    var existeEspecialidad = db.Especialidads
                        .Any(e => e.Nombre.ToLower() == especialidad.Nombre.ToLower() &&
                                 e.IdCarrera == especialidad.IdCarrera &&
                                 e.Id != especialidad.Id);

                    if (existeEspecialidad)
                    {
                        ViewBag.Error = "Ya existe otra especialidad con este nombre en la carrera seleccionada.";
                        ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
                        return View("~/Views/SGA/Especialidades/Edit.cshtml", especialidad);
                    }

                    db.Entry(especialidad).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Programa educativo actualizado exitosamente.";
                    return RedirectToAction("Especialidades");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al actualizar el programa educativo: " + ex.Message;
            }

            ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
            return View("~/Views/SGA/Especialidades/Edit.cshtml", especialidad);
        }

        public ActionResult EditEspecialidad(int? id)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            if (id == null)
                return RedirectToAction("Especialidades");

            var usuario = (Usuario)Session["Usuario"];
            var especialidad = db.Especialidads.Find(id);

            if (especialidad == null)
            {
                TempData["Error"] = "Programa educativo no encontrado.";
                return RedirectToAction("Especialidades");
            }

            // Verificar permisos
            if (usuario.IdNivel != 4 && especialidad.IdCarrera != usuario.IdCarrera)
            {
                TempData["Error"] = "No tiene permisos para editar este programa educativo.";
                return RedirectToAction("Especialidades");
            }

            ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre", especialidad.IdCarrera);
            ViewBag.Title = "Editar Programa Educativo";

            return View("~/Views/SGA/Especialidades/Edit.cshtml", especialidad);
        }

        public ActionResult DeleteEspecialidad(int? id)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            if (id == null)
                return RedirectToAction("Especialidades");

            var usuario = (Usuario)Session["Usuario"];
            var especialidad = db.Especialidads.Find(id);

            if (especialidad == null)
            {
                TempData["Error"] = "Programa educativo no encontrado.";
                return RedirectToAction("Especialidades");
            }

            // Verificar permisos
            if (usuario.IdNivel != 4 && especialidad.IdCarrera != usuario.IdCarrera)
            {
                TempData["Error"] = "No tiene permisos para eliminar este programa educativo.";
                return RedirectToAction("Especialidades");
            }

            // Obtener informacin de la carrera
            var carrera = db.Carreras.Find(especialidad.IdCarrera);
            ViewBag.CarreraNombre = carrera?.Nombre ?? "Carrera no encontrada";
            ViewBag.Title = "Eliminar Programa Educativo";

            return View("~/Views/SGA/Especialidades/Delete.cshtml", especialidad);
        }

        [HttpPost, ActionName("DeleteEspecialidad")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteEspecialidadConfirmed(int id)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];

            try
            {
                var especialidad = db.Especialidads.Find(id);

                if (especialidad == null)
                {
                    TempData["Error"] = "Programa educativo no encontrado.";
                    return RedirectToAction("Especialidades");
                }

                // Verificar permisos
                if (usuario.IdNivel != 4 && especialidad.IdCarrera != usuario.IdCarrera)
                {
                    TempData["Error"] = "No tiene permisos para eliminar este programa educativo.";
                    return RedirectToAction("Especialidades");
                }

                // Verificar si la especialidad est siendo utilizada
                var enUso = db.DatosPersonales.Any(d => d.Especialidad == especialidad.Nombre) ||
                   db.EntrevistaInicials.Any(e => e.Especialidad == especialidad.Nombre) ||
                   db.Estudiantes.Any(e => e.Especialidad == especialidad.Nombre);

                if (enUso)
                {
                    TempData["Error"] = "No se puede eliminar este programa educativo porque est siendo utilizado por estudiantes.";
                    return RedirectToAction("Especialidades");
                }

                db.Especialidads.Remove(especialidad);
                db.SaveChanges();

                TempData["Success"] = "Programa educativo eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar el programa educativo: " + ex.Message;
            }

            return RedirectToAction("Especialidades");
        }

        public ActionResult DetailsEspecialidad(int? id)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            if (id == null)
                return RedirectToAction("Especialidades");

            var usuario = (Usuario)Session["Usuario"];
            var especialidad = db.Especialidads.Find(id);

            if (especialidad == null)
            {
                TempData["Error"] = "Programa educativo no encontrado.";
                return RedirectToAction("Especialidades");
            }

            // Verificar permisos
            if (usuario.IdNivel != 4 && especialidad.IdCarrera != usuario.IdCarrera)
            {
                TempData["Error"] = "No tiene permisos para ver este programa educativo.";
                return RedirectToAction("Especialidades");
            }

            // Obtener informacin de la carrera
            var carrera = db.Carreras.Find(especialidad.IdCarrera);
            ViewBag.CarreraNombre = carrera?.Nombre ?? "Carrera no encontrada";

            // Obtener estadsticas de uso
            var estadisticas = new
            {
                TotalEstudiantes = db.DatosPersonales.Count(d => d.Especialidad == especialidad.Nombre),
                TotalEntrevistas = db.EntrevistaInicials.Count(e => e.Especialidad == especialidad.Nombre),
                TotalEstudiantesActivos = db.Estudiantes.Count(e => e.Especialidad == especialidad.Nombre)
            };

            ViewBag.Estadisticas = estadisticas;
            ViewBag.Title = "Detalles del Programa Educativo";

            return View("~/Views/SGA/Especialidades/Details.cshtml", especialidad);
        }

        // Arrastres - Materias de arrastre
        public ActionResult Arrastres(int? idEspecialidad)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];
            ViewBag.Title = "Materias de Arrastre";

            // Filtros para especialidades
            var especialidades = ObtenerEspecialidadesPorUsuario(usuario);
            ViewBag.Especialidades = new SelectList(especialidades, "Id", "Nombre");
            ViewBag.SelectedEspecialidad = idEspecialidad;

            // Consulta de estudiantes con materias en arrastre
            var query = db.Estudiantes.AsQueryable();

            if (usuario.IdNivel != 4) // No es Master
            {
                query = query.Where(e => e.IdCarrera == usuario.IdCarrera);
            }

            if (idEspecialidad.HasValue)
            {
                var especialidad = db.Especialidads.Find(idEspecialidad.Value);
                if (especialidad != null)
                {
                    query = query.Where(e => e.Especialidad == especialidad.Nombre);
                }
            }

            var estudiantesArrastre = query.ToList();

            return View("~/Views/SGA/Arrastres/Index.cshtml", estudiantesArrastre);
        }

        // Canalización
        public ActionResult Canalizacion(int? cuatrimestre)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];
            ViewBag.Title = "Casos de Canalización";

            // Obtener cuatrimestres disponibles
            var cuatrimestres = db.Bajas.Select(b => b.Cuatrimestre).Distinct().ToList();
            ViewBag.Cuatrimestres = new SelectList(cuatrimestres);
            ViewBag.SelectedCuatrimestre = cuatrimestre;

            // Consulta de casos de canalización
            var query = db.Bajas.AsQueryable();

            if (usuario.IdNivel != 4) // No es Master
            {
                var carreraNombre = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera)?.Nombre;
                if (!string.IsNullOrEmpty(carreraNombre))
                {
                    query = query.Where(b => b.Carrera == carreraNombre);
                }
            }

            if (cuatrimestre.HasValue)
            {
                query = query.Where(b => b.Cuatrimestre == cuatrimestre.Value.ToString());
            }

            var canalizaciones = query.OrderByDescending(b => b.Fecha).ToList();

            return View("~/Views/SGA/Canalizacion/Index.cshtml", canalizaciones);
        }

        // Vulnerabilidad
        public ActionResult Vulnerabilidad(int? idEspecialidad)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];
            ViewBag.Title = "Niveles de Vulnerabilidad";

            // Filtros
            var especialidades = ObtenerEspecialidadesPorUsuario(usuario);
            ViewBag.Especialidades = new SelectList(especialidades, "Id", "Nombre");
            ViewBag.SelectedEspecialidad = idEspecialidad;

            // Consulta de vulnerabilidad desde PATs
            var query = db.PATs.AsQueryable();

            if (usuario.IdNivel == 2) // Tutor
            {
                query = query.Where(p => p.IdTutor == usuario.IdUsuario);
            }
            else if (usuario.IdNivel == 3) // Coordinador
            {
                query = query.Where(p => p.IdCarrera == usuario.IdCarrera);
            }

            if (idEspecialidad.HasValue)
            {
                var especialidad = db.Especialidads.Find(idEspecialidad.Value);
                if (especialidad != null)
                {
                    // Filtrar por especialidad a través de entrevistas
                    var entrevistasEspecialidad = db.EntrevistaInicials
                        .Where(e => e.Especialidad == especialidad.Nombre)
                        .Select(e => e.IdEntrevistaInicial);

                    query = query.Where(p => entrevistasEspecialidad.Contains(p.IdEntrevistaInicial));
                }
            }

            var vulnerabilidades = query.Where(p => p.VunerableEconomico > 0 || p.VunerablePersonal > 0 || p.VunerableAcademico > 0)
                                  .ToList();

            return View("~/Views/SGA/Vulnerabilidad/Index.cshtml", vulnerabilidades);
        }

        // Concentrado de Bajas (Solo Master)
        public ActionResult ConcentradoBajas(int? idCarrera, int? año)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];

            if (usuario.IdNivel != 4) // Solo Master
            {
                TempData["Error"] = "No tiene permisos para acceder a esta sección.";
                return RedirectToAction("Dashboard");
            }

            ViewBag.Title = "Concentrado de Bajas";

            // Filtros
            ViewBag.Carreras = new SelectList(db.Carreras.ToList(), "IdCarrera", "Nombre");
            ViewBag.Años = new SelectList(db.Bajas.Select(b => b.Fecha.Year).Distinct().OrderByDescending(y => y));
            ViewBag.SelectedCarrera = idCarrera;
            ViewBag.SelectedAño = año;

            // Consulta consolidada
            var query = db.Bajas.AsQueryable();

            if (idCarrera.HasValue)
            {
                var carreraNombre = db.Carreras.Find(idCarrera.Value)?.Nombre;
                if (!string.IsNullOrEmpty(carreraNombre))
                {
                    query = query.Where(b => b.Carrera == carreraNombre);
                }
            }

            if (año.HasValue)
            {
                query = query.Where(b => b.Fecha.Year == año.Value);
            }

            var bajas = query.OrderByDescending(b => b.Fecha).ToList();

            return View("~/Views/SGA/ConcentradoBajas/Index.cshtml", bajas);
        }

        // Crear Tutor
        public ActionResult CreateTutor()
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];

            if (usuario.IdNivel < 3) // Solo Coordinador y Master
            {
                TempData["Error"] = "No tiene permisos para crear tutores.";
                return RedirectToAction("Dashboard");
            }

            ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
            ViewBag.Title = "Crear Nuevo Tutor";

            return View("~/Views/SGA/Tutores/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTutor(Usuario nuevoTutor)
        {
            if (Session["Usuario"] == null)
                return RedirectToAction("Login", "Home");

            var usuario = (Usuario)Session["Usuario"];

            if (usuario.IdNivel < 3)
            {
                TempData["Error"] = "No tiene permisos para crear tutores.";
                return RedirectToAction("Dashboard");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Validar permisos de carrera
                    if (usuario.IdNivel != 4 && nuevoTutor.IdCarrera != usuario.IdCarrera)
                    {
                        ViewBag.Error = "No tiene permisos para crear tutores en esta carrera.";
                        ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
                        return View("~/Views/SGA/Tutores/Create.cshtml", nuevoTutor);
                    }

                    nuevoTutor.IdNivel = 2; // Tutor
                    nuevoTutor.Estado = true;
                    nuevoTutor.Tiempo = DateTime.Now;
                    nuevoTutor.Password = Security.Encripta(nuevoTutor.Password);

                    if (usuario.IdNivel != 4) // No es Master
                    {
                        nuevoTutor.IdCarrera = usuario.IdCarrera;
                    }

                    db.Usuarios.Add(nuevoTutor);
                    db.SaveChanges();

                    TempData["Success"] = "Tutor creado exitosamente.";
                    return RedirectToAction("AsignarAsesores");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al crear el tutor: " + ex.Message;
            }

            ViewBag.Carreras = new SelectList(ObtenerCarreras(usuario), "IdCarrera", "Nombre");
            return View("~/Views/SGA/Tutores/Create.cshtml", nuevoTutor);
        }

        // Mtodo auxiliar para obtener especialidades segn el usuario
        private List<Especialidad> ObtenerEspecialidadesPorUsuario(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.Especialidads.ToList();
            }
            else // Coordinador
            {
                return db.Especialidads.Where(e => e.IdCarrera == usuario.IdCarrera).ToList();
            }
        }

        // Mtodos auxiliares existentes
        private int ObtenerTotalEstudiantes(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.DatosPersonales.Count(x => x.Estado);
            }
            else // Coordinador
            {
                return db.DatosPersonales.Count(x => x.IdCarrera == usuario.IdCarrera && x.Estado);
            }
        }

        private int ObtenerTotalTutores(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.Usuarios.Count(x => x.IdNivel == 2 && x.Estado);
            }
            else // Coordinador
            {
                return db.Usuarios.Count(x => x.IdCarrera == usuario.IdCarrera && x.IdNivel == 2 && x.Estado);
            }
        }

        private int ObtenerTotalEntrevistas(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.EntrevistaInicials.Count();
            }
            else // Coordinador
            {
                return db.EntrevistaInicials.Count(x => x.IdCarrera == usuario.IdCarrera);
            }
        }

        private int ObtenerPATHsCompletados(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.PATs.Count(x => x.estado);
            }
            else // Coordinador
            {
                return db.PATs.Count(x => x.IdCarrera == usuario.IdCarrera && x.estado);
            }
        }

        private List<object> ObtenerActividadReciente(Usuario usuario)
        {
            var actividades = new List<object>();
            if (usuario.IdNivel == 4) // Master
            {
                var tutoresRecientes = db.Usuarios
                    .Where(x => x.IdNivel == 2 && x.Estado)
                    .OrderByDescending(x => x.Tiempo)
                    .Take(5)
                    .Select(x => new {
                        Tipo = "Nuevo tutor registrado",
                        Descripcion = x.NombreCompleto,
                        Fecha = x.Tiempo
                    }).ToList();
                actividades.AddRange(tutoresRecientes);
            }
            else // Coordinador
            {
                var tutoresRecientes = db.Usuarios
                    .Where(x => x.IdCarrera == usuario.IdCarrera && x.IdNivel == 2 && x.Estado)
                    .OrderByDescending(x => x.Tiempo)
                    .Take(5)
                    .Select(x => new {
                        Tipo = "Nuevo tutor registrado",
                        Descripcion = x.NombreCompleto,
                        Fecha = x.Tiempo
                    }).ToList();
                actividades.AddRange(tutoresRecientes);
            }
            return actividades.Cast<object>().ToList();
        }

        private List<Usuario> ObtenerTutoresPorCarrera(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.Usuarios.Where(x => x.IdNivel == 2 && x.Estado).ToList();
            }
            else // Coordinador
            {
                return db.Usuarios.Where(x => x.IdCarrera == usuario.IdCarrera && x.IdNivel == 2 && x.Estado).ToList();
            }
        }

        private List<DatosPersonales> ObtenerEstudiantesFiltrados(Usuario usuario, int? carrera, int? grupo, int? grado)
        {
            var query = db.DatosPersonales.Where(x => x.Estado);
            if (usuario.IdNivel != 4) // No es Master
            {
                query = query.Where(x => x.IdCarrera == usuario.IdCarrera);
            }
            if (carrera.HasValue)
            {
                query = query.Where(x => x.IdCarrera == carrera.Value);
            }
            if (grupo.HasValue)
            {
                query = query.Where(x => x.IdGrupo == grupo.Value);
            }
            if (grado.HasValue)
            {
                query = query.Where(x => x.IdGrado == grado.Value);
            }
            return query.ToList();
        }

        private List<Carrera> ObtenerCarreras(Usuario usuario)
        {
            if (usuario.IdNivel == 4) // Master
            {
                return db.Carreras.ToList();
            }
            else // Coordinador
            {
                return db.Carreras.Where(x => x.IdCarrera == usuario.IdCarrera).ToList();
            }
        }

        private object GenerarEstadisticas(Usuario usuario)
        {
            var estadisticas = new
            {
                EntrevistasPorGrado = ObtenerEntrevistasPorGrado(usuario),
                PATHsPorSemana = ObtenerPATHsPorSemana(usuario),
                VulnerabilidadPorTipo = ObtenerVulnerabilidadPorTipo(usuario),
                BajasPorPeriodo = ObtenerBajasPorPeriodo(usuario)
            };
            return estadisticas;
        }

        private object ObtenerEntrevistasPorGrado(Usuario usuario)
        {
            var query = db.EntrevistaInicials.AsQueryable();
            if (usuario.IdNivel != 4)
            {
                query = query.Where(x => x.IdCarrera == usuario.IdCarrera);
            }
            return query.GroupBy(x => x.IdGrado)
                       .Select(g => new { Grado = g.Key, Total = g.Count() })
                       .ToList();
        }

        private object ObtenerPATHsPorSemana(Usuario usuario)
        {
            var query = db.PATs.AsQueryable();
            if (usuario.IdNivel != 4)
            {
                query = query.Where(x => x.IdCarrera == usuario.IdCarrera);
            }
            return query.Where(x => x.Fecha >= DbFunctions.AddDays(DateTime.Now, -30))
                       .GroupBy(x => DbFunctions.DiffDays(x.Fecha, DateTime.Now))
                       .Select(g => new { Semana = g.Key, Total = g.Count() })
                       .ToList();
        }

        private object ObtenerVulnerabilidadPorTipo(Usuario usuario)
        {
            var query = db.EntrevistaInicials.AsQueryable();
            if (usuario.IdNivel != 4)
            {
                query = query.Where(x => x.IdCarrera == usuario.IdCarrera);
            }
            return query.Where(x => x.IdVulnerable == 1) // Vulnerable = S
                       .GroupBy(x => x.EleccionVulnerabilidad)
                       .Select(g => new { Tipo = g.Key, Total = g.Count() })
                       .ToList();
        }

        private object ObtenerBajasPorPeriodo(Usuario usuario)
        {
            var query = db.Bajas.AsQueryable();
            if (usuario.IdNivel != 4)
            {
                // Filtrar por carrera usando el campo Carrera de la tabla Bajas
                var carreraNombre = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera)?.Nombre;
                if (!string.IsNullOrEmpty(carreraNombre))
                {
                    query = query.Where(x => x.Carrera == carreraNombre);
                }
            }
            return query.GroupBy(x => x.Cuatrimestre)
                       .Select(g => new { Cuatrimestre = g.Key, Total = g.Count() })
                       .ToList();
        }

        [HttpPost]
        public ActionResult AgregarTutor(Usuario nuevoTutor)
        {
            if (Session["Usuario"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            var usuario = (Usuario)Session["Usuario"];
            if (ModelState.IsValid)
            {
                nuevoTutor.IdNivel = 2; // Tutor
                nuevoTutor.Estado = true;
                nuevoTutor.Tiempo = DateTime.Now;
                nuevoTutor.Password = Security.Encripta(nuevoTutor.Password);
                if (usuario.IdNivel != 4) // No es Master
                {
                    nuevoTutor.IdCarrera = usuario.IdCarrera;
                }
                db.Usuarios.Add(nuevoTutor);
                db.SaveChanges();
                return RedirectToAction("AsignarAsesores");
            }
            return View("~/Views/SGA/AsignarAsesores/Index.cshtml", ObtenerTutoresPorCarrera(usuario));
        }

        // Mtodo auxiliar para generar estadsticas mejoradas
        private object GenerarEstadisticasAvanzadas(Usuario usuario)
        {
            var estadisticas = new
            {
                // Entrevistas por grado
                EntrevistasPorGrado = ObtenerEntrevistasPorGrado(usuario),

                // PATs finalizados
                PATsFinalizados = ObtenerPATsFinalizados(usuario),

                // Actividades por semana
                ActividadesPorSemana = ObtenerActividadesPorSemana(usuario),

                // Vulnerabilidad por tipo
                VulnerabilidadPorTipo = ObtenerVulnerabilidadDetallada(usuario),

                // Bajas por perodo
                BajasPorPeriodo = ObtenerBajasPorPeriodo(usuario),

                // Comparativas por grupos
                ComparativaGrupos = ObtenerComparativaGrupos(usuario),

                // Transhabilidad
                Transhabilidad = ObtenerTranshabilidad(usuario)
            };

            return estadisticas;
        }

        private object ObtenerPATsFinalizados(Usuario usuario)
        {
            var query = db.PATs.Where(p => p.estado == false); // Finalizados

            if (usuario.IdNivel != 4)
            {
                query = query.Where(p => p.IdCarrera == usuario.IdCarrera);
            }

            return query.GroupBy(p => p.Fecha.Month)
                       .Select(g => new { Mes = g.Key, Total = g.Count() })
                       .ToList();
        }

        private object ObtenerActividadesPorSemana(Usuario usuario)
        {
            var query = db.actividadesSemanals.AsQueryable();

            if (usuario.IdNivel == 2) // Tutor
            {
                var gruposTutor = db.TutoriaGrupals
                    .Where(t => t.IdUsuario == usuario.IdUsuario)
                    .Select(t => t.IdTutoriaGrupal);

                query = query.Where(a => gruposTutor.Contains(a.IdEntrevistaInicial));
            }
            else if (usuario.IdNivel == 3) // Coordinador
            {
                var patsCarrera = db.PATs.Where(p => p.IdCarrera == usuario.IdCarrera)
                                        .Select(p => p.IdEntrevistaInicial);

                query = query.Where(a => patsCarrera.Contains(a.IdEntrevistaInicial));
            }

            return query.GroupBy(a => a.IdSemana)
                       .Select(g => new { Semana = g.Key, Actividades = g.Count() })
                       .ToList();
        }

        private object ObtenerVulnerabilidadDetallada(Usuario usuario)
        {
            var query = db.PATs.AsQueryable();

            if (usuario.IdNivel != 4)
            {
                query = query.Where(p => p.IdCarrera == usuario.IdCarrera);
            }

            return new
            {
                Economica = query.Where(p => p.VunerableEconomico > 0).Count(),
                Personal = query.Where(p => p.VunerablePersonal > 0).Count(),
                Academica = query.Where(p => p.VunerableAcademico > 0).Count()
            };
        }

        private object ObtenerComparativaGrupos(Usuario usuario)
        {
            var query = db.TutoriaGrupals.AsQueryable();

            if (usuario.IdNivel != 4)
            {
                query = query.Where(t => t.IdCarrera == usuario.IdCarrera);
            }

            return query.GroupBy(t => t.IdGrupo)
                       .Select(g => new
                       {
                           Grupo = g.Key,
                           Tutores = g.Count(),
                           // Agregar ms mtricas segn necesidad
                       })
                       .ToList();
        }

        private object ObtenerTranshabilidad(Usuario usuario)
        {
            // Implementar lgica de transhabilidad basada en los datos disponibles
            var query = db.Estudiantes.AsQueryable();

            if (usuario.IdNivel != 4)
            {
                query = query.Where(e => e.IdCarrera == usuario.IdCarrera);
            }

            return query.GroupBy(e => new { e.IdGrado, e.IdGrupo })
                       .Select(g => new
                       {
                           Grado = g.Key.IdGrado,
                           Grupo = g.Key.IdGrupo,
                           Total = g.Count()
                       })
                       .ToList();
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

