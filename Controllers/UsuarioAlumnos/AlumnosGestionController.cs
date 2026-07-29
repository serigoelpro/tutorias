using Plataforma_Web.Models;
using Plataforma_Web.Models.UsuarioAlumnos;
using Plataforma_Web.Models.UsuariosAlumnoMaster;
using ProyectoIntegracion.Models.GestionUsuarios;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.UsuarioAlumnos
{
    public class AlumnosGestionController : Controller
    {
        private usuarios_model_db db = new usuarios_model_db();
        private EstadiasDbContext estadiasDb = new EstadiasDbContext();

        private bool ValidarAccesoCoordinador()
        {
            var usuario = Session["Usuario"] as Usuario;
            return usuario != null && (usuario.IdNivel == 3 || usuario.IdNivel == 4);
        }
        private bool ValidarAccesoMaster()
        {
            var usuario = Session["Usuario"] as Usuario;
            return usuario != null && (usuario.IdNivel == 4);
        }

        [LecturaPermitida]
        [HttpPost]
        public ActionResult GetAlumnosGestion()
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { error = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var usuario = Session["Usuario"] as Usuario;

                if (usuario == null)
                {
                    return Json(new { error = "Sesión no válida" }, JsonRequestBehavior.AllowGet);
                }

                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumn = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                db.Database.CommandTimeout = 120;
                estadiasDb.Database.CommandTimeout = 120;

                // ========== MAPEO DE CARRERAS: TUTORIAS → ESTADIAS ==========
                var mapeoCarreras = new Dictionary<int, int>
        {
            { 1, 1 },    // TI → TI
            { 2, 3 },    // Mantenimiento Industrial → Mantenimiento Industrial
            { 3, 4 },    // Mecatrónica → Mecatrónica
            { 4, 5 },    // Administración → Administración
            { 5, 2 },    // Industrial (Procesos) → Procesos Industriales
            { 6, 6 },    // Energías Renovables → Energías Renovables
            { 7, 1003 }, // Logística → Logística
            { 8, 1005 }, // Logística Internacional → Logística Internacional
            { 9, 1004 }, // Aeronáutica → Aeronáutica
            { 10, 1007 }, // Microelectrónica → Microelectrónica
            { 11, 1006 }  // Ciencia de Datos → Ciencia de Datos
        };
                // ===========================================================

                // Cargar catálogo de carreras de Estadias
                var carrerasList = estadiasDb.Carrera
                    .AsNoTracking()
                    .Select(c => new { c.IdArea, c.Area })
                    .ToList();
                var carrerasDictionary = carrerasList.ToDictionary(c => c.IdArea, c => c.Area);

                // Obtener alumnos
                IQueryable<ProyectoIntegracion.Models.GestionUsuarios.Alumno> alumnosQuery = db.Alumnos.AsNoTracking();

                // Aplicar filtro de búsqueda PRIMERO
                if (!string.IsNullOrEmpty(searchValue))
                {
                    alumnosQuery = alumnosQuery.Where(a =>
                        a.Nombre.Contains(searchValue) ||
                        a.ApellidoPaterno.Contains(searchValue) ||
                        a.ApellidoMaterno.Contains(searchValue) ||
                        a.Matricula.Contains(searchValue) ||
                        (a.CorreoElectronico != null && a.CorreoElectronico.Contains(searchValue))
                    );
                }

                int recordsTotal = db.Alumnos.Count();

                // Traer a memoria SOLO los que pasaron el filtro de búsqueda
                var alumnosList = alumnosQuery.ToList();

                // ========== OBTENER DATOS PERSONALES ==========
                Dictionary<string, int> datosPersonalesDict = new Dictionary<string, int>();

                try
                {
                    using (var tutoriasDb = new PlataformaWebDbContext())
                    {
                        tutoriasDb.Database.CommandTimeout = 120;

                        var correos = alumnosList
                            .Where(a => !string.IsNullOrEmpty(a.CorreoElectronico))
                            .Select(a => a.CorreoElectronico)
                            .ToList();

                        if (correos.Any())
                        {
                            var datosPersonalesList = tutoriasDb.DatosPersonales
                                .AsNoTracking()
                                .Where(dp => correos.Contains(dp.Email))
                                .Select(dp => new { dp.Email, dp.IdCarrera })
                                .ToList();

                            datosPersonalesDict = datosPersonalesList
                                .Where(dp => !string.IsNullOrEmpty(dp.Email))
                                .GroupBy(dp => dp.Email.ToLower())
                                .ToDictionary(g => g.Key, g => g.First().IdCarrera);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Si falla DatosPersonales, continuar sin ellos
                    System.Diagnostics.Debug.WriteLine($"Error al obtener DatosPersonales: {ex.Message}");
                }
                // ==============================================

                // Mapear carrera real
                var alumnosConCarrera = alumnosList.Select(a =>
                {
                    var emailKey = a.CorreoElectronico?.ToLower();
                    int idCarreraEstadias = a.IdCarrera;

                    if (!string.IsNullOrEmpty(emailKey) && datosPersonalesDict.ContainsKey(emailKey))
                    {
                        int idCarreraTutorias = datosPersonalesDict[emailKey];
                        if (mapeoCarreras.ContainsKey(idCarreraTutorias))
                        {
                            idCarreraEstadias = mapeoCarreras[idCarreraTutorias];
                        }
                    }

                    return new
                    {
                        Alumno = a,
                        IdCarreraEstadias = idCarreraEstadias
                    };
                }).ToList();

                // FILTRO POR CARRERA (SI ES COORDINADOR)
                if (usuario.IdNivel == 3)
                {
                    int idCarreraCoordinadorEstadias = usuario.IdCarrera;

                    if (mapeoCarreras.ContainsKey(usuario.IdCarrera))
                    {
                        idCarreraCoordinadorEstadias = mapeoCarreras[usuario.IdCarrera];
                    }

                    alumnosConCarrera = alumnosConCarrera
                        .Where(x => x.IdCarreraEstadias == idCarreraCoordinadorEstadias)
                        .ToList();
                }

                int recordsFiltered = alumnosConCarrera.Count;

                // Ordenar
                switch (sortColumn ?? "0")
                {
                    case "0":
                        alumnosConCarrera = sortDirection == "asc"
                            ? alumnosConCarrera.OrderBy(x => x.Alumno.Nombre).ToList()
                            : alumnosConCarrera.OrderByDescending(x => x.Alumno.Nombre).ToList();
                        break;
                    case "1":
                        alumnosConCarrera = sortDirection == "asc"
                            ? alumnosConCarrera.OrderBy(x => x.Alumno.ApellidoPaterno).ToList()
                            : alumnosConCarrera.OrderByDescending(x => x.Alumno.ApellidoPaterno).ToList();
                        break;
                    default:
                        alumnosConCarrera = alumnosConCarrera.OrderBy(x => x.Alumno.Nombre).ToList();
                        break;
                }

                // Paginar
                var data = alumnosConCarrera.Skip(skip).Take(pageSize).ToList();

                // Preparar respuesta
                var dataResponse = data.Select(x => new Plataforma_Web.Models.UsuarioAlumnos.AlumnoGestion
                {
                    IdAlumno = x.Alumno.IdAlumno,
                    Nombre = x.Alumno.Nombre ?? "",
                    ApellidoPaterno = x.Alumno.ApellidoPaterno ?? "",
                    ApellidoMaterno = x.Alumno.ApellidoMaterno ?? "",
                    Matricula = x.Alumno.Matricula ?? "",
                    CorreoElectronico = x.Alumno.CorreoElectronico ?? "",
                    IdCarrera = x.IdCarreraEstadias,
                    CarreraNombre = carrerasDictionary.ContainsKey(x.IdCarreraEstadias)
                        ? carrerasDictionary[x.IdCarreraEstadias]
                        : "Sin carrera asignada"
                }).ToList();

                return Json(new
                {
                    draw = int.Parse(draw),
                    recordsTotal = recordsTotal,
                    recordsFiltered = recordsFiltered,
                    data = dataResponse
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetAlumnoById(int id)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var alumno = db.Alumnos
                    .AsNoTracking()
                    .Where(a => a.IdAlumno == id)
                    .Select(a => new
                    {
                        a.IdAlumno,
                        a.Nombre,
                        a.ApellidoPaterno,
                        a.ApellidoMaterno,
                        a.Matricula,
                        a.CorreoElectronico,
                        a.IdCarrera
                    })
                    .FirstOrDefault();

                if (alumno == null)
                {
                    return Json(new { success = false, message = "Alumno no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var usuario = Session["Usuario"] as Usuario;

                var mapeoCarreras = new Dictionary<int, int>
                {
                    { 1, 1 }, { 2, 3 }, { 3, 4 }, { 4, 5 }, { 5, 2 }, { 6, 6 },
                    { 7, 1003 }, { 8, 1005 }, { 9, 1004 }, { 10, 1007 }, { 11, 1006 }
                };

                int idCarreraEstadias = alumno.IdCarrera;
                string emailKey = alumno.CorreoElectronico?.ToLower();

                if (!string.IsNullOrEmpty(emailKey))
                {
                    try
                    {
                        using (var tutoriasDb = new PlataformaWebDbContext())
                        {
                            var dp = tutoriasDb.DatosPersonales
                                .AsNoTracking()
                                .FirstOrDefault(d => d.Email.ToLower() == emailKey);

                            if (dp != null && mapeoCarreras.ContainsKey(dp.IdCarrera))
                            {
                                idCarreraEstadias = mapeoCarreras[dp.IdCarrera];
                            }
                        }
                    }
                    catch (Exception) { }
                }

                if (usuario.IdNivel == 3)
                {
                    int idCarreraCoordinadorEstadias = usuario.IdCarrera;
                    if (mapeoCarreras.ContainsKey(usuario.IdCarrera))
                    {
                        idCarreraCoordinadorEstadias = mapeoCarreras[usuario.IdCarrera];
                    }

                    if (idCarreraEstadias != idCarreraCoordinadorEstadias)
                    {
                        return Json(new { success = false, message = "No autorizado para ver este alumno" }, JsonRequestBehavior.AllowGet);
                    }
                }

                return Json(new { success = true, alumno = alumno, idCarrera = idCarreraEstadias }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Obtener lista de Carreras de Estadias
        [HttpGet]
        public JsonResult GetEstadiasCarrerasList()
        {
            if (!ValidarAccesoMaster())
            {
                return Json(new { success = false, message = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }
            try
            {
                var carreras = estadiasDb.Carrera
                                 .AsNoTracking()
                                 .Where(c => c.EsMaestria == false && c.CarreraAlumno != null)
                                 .Select(c => new { IdArea = c.IdArea, Nombre = c.Area })
                                 .OrderBy(c => c.Nombre)
                                 .ToList();

                return Json(new { success = true, data = carreras }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Guardar solo los datos académicos (Carrera)
        [HttpPost]
        public JsonResult EditGestionCarreraAjax(int IdAlumno, int IdCarrera)
        {
            if (!ValidarAccesoMaster())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var alumno = db.Alumnos.Find(IdAlumno);
                if (alumno == null)
                {
                    return Json(new { success = false, message = "Alumno no encontrado" });
                }

                alumno.IdCarrera = IdCarrera;
                db.Entry(alumno).State = EntityState.Modified;
                db.SaveChanges();

                try
                {
                    var mapeoInverso = new Dictionary<int, int>
                    {
                        { 1, 1 }, { 3, 2 }, { 4, 3 }, { 5, 4 }, { 2, 5 }, { 6, 6 },
                        { 1003, 7 }, { 1005, 8 }, { 1004, 9 }, { 1007, 10 }, { 1006, 11 }
                    };

                    if (mapeoInverso.ContainsKey(IdCarrera) && !string.IsNullOrEmpty(alumno.CorreoElectronico))
                    {
                        int idCarreraTutorias = mapeoInverso[IdCarrera];
                        string emailKey = alumno.CorreoElectronico.ToLower();

                        using (var tutoriasDb = new PlataformaWebDbContext())
                        {
                            var dp = tutoriasDb.DatosPersonales.FirstOrDefault(d => d.Email.ToLower() == emailKey);
                            if (dp != null)
                            {
                                dp.IdCarrera = idCarreraTutorias;
                                var carreraTutorias = tutoriasDb.Carreras.Find(idCarreraTutorias);
                                if (carreraTutorias != null)
                                {
                                    dp.CarreraNom = carreraTutorias.Nombre;
                                }
                                tutoriasDb.Entry(dp).State = EntityState.Modified;
                                tutoriasDb.SaveChanges();
                            }
                        }
                    }
                }
                catch (Exception exSync)
                {
                    System.Diagnostics.Debug.WriteLine($"Error en sincronización inversa: {exSync.Message}");
                }

                return Json(new { success = true, message = "Carrera actualizada exitosamente (y sincronizada)." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult EditAjax(int IdAlumno, string Nombre, string ApellidoPaterno, string ApellidoMaterno, string Matricula)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var alumno = db.Alumnos.Find(IdAlumno);

                if (alumno != null)
                {
                    if (alumno.Matricula != Matricula)
                    {
                        var existeMatricula = db.Alumnos.Any(a => a.Matricula == Matricula && a.IdAlumno != IdAlumno);
                        if (existeMatricula)
                        {
                            return Json(new { success = false, message = "Esta matrícula ya existe" });
                        }
                    }

                    alumno.Nombre = Nombre;
                    alumno.ApellidoPaterno = ApellidoPaterno;
                    alumno.ApellidoMaterno = ApellidoMaterno;
                    alumno.Matricula = Matricula;

                    db.Entry(alumno).State = EntityState.Modified;
                    db.SaveChanges();

                    return Json(new { success = true, message = "Datos del alumno actualizados exitosamente" });
                }

                return Json(new { success = false, message = "Alumno no encontrado" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public ActionResult Edit(int? id)
        {
            if (!ValidarAccesoCoordinador())
            {
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                return RedirectToAction("Index", "UsuarioAlumnos");
            }

            var alumno = db.Alumnos.Find(id);
            if (alumno == null)
            {
                return RedirectToAction("Index", "UsuarioAlumnos");
            }

            // USA EL NAMESPACE COMPLETO AQUÍ TAMBIÉN
            var alumnoGestion = new Plataforma_Web.Models.UsuarioAlumnos.AlumnoGestion
            {
                IdAlumno = alumno.IdAlumno,
                Nombre = alumno.Nombre ?? "",
                ApellidoPaterno = alumno.ApellidoPaterno ?? "",
                ApellidoMaterno = alumno.ApellidoMaterno ?? "",
                Matricula = alumno.Matricula ?? "",
                CorreoElectronico = alumno.CorreoElectronico ?? "",
                Contrasena = alumno.Contrasena,
                IdCarrera = alumno.IdCarrera,
                Cuatrimestre = alumno.Cuatrimestre,
                RegistradoEstadias = alumno.RegistradoEstadias,
                Habilitado = alumno.Habilitado,
                FechaRegistro = alumno.FechaRegistro,
                FechaSesion = alumno.FechaSesion,
                MicrosoftIdentifier = alumno.MicrosoftIdentifier,
                TokenSesion = alumno.TokenSesion
            };

            return View("~/Views/UsuarioAlumnos/EditGestion.cshtml", alumnoGestion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Plataforma_Web.Models.UsuarioAlumnos.AlumnoGestion alumnoGestion)
        {
            if (!ValidarAccesoCoordinador())
            {
                return RedirectToAction("Index", "Home");
            }

            var alumno = db.Alumnos.Find(alumnoGestion.IdAlumno);
            if (alumno != null)
            {
                if (alumno.Matricula != alumnoGestion.Matricula)
                {
                    var existeMatricula = db.Alumnos.Any(a => a.Matricula == alumnoGestion.Matricula && a.IdAlumno != alumnoGestion.IdAlumno);
                    if (existeMatricula)
                    {
                        ModelState.AddModelError("Matricula", "Esta matrícula ya existe");
                        return View("~/Views/UsuarioAlumnos/EditGestion.cshtml", alumnoGestion);
                    }
                }

                alumno.Nombre = alumnoGestion.Nombre;
                alumno.ApellidoPaterno = alumnoGestion.ApellidoPaterno;
                alumno.ApellidoMaterno = alumnoGestion.ApellidoMaterno;
                alumno.Matricula = alumnoGestion.Matricula;

                db.Entry(alumno).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Mensaje"] = "Datos del alumno actualizados exitosamente";
                return RedirectToAction("Index", "UsuarioAlumnos");
            }

            TempData["Error"] = "No se encontró el alumno";
            return RedirectToAction("Index", "UsuarioAlumnos");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                estadiasDb.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}