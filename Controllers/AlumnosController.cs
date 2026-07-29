using Microsoft.Reporting.WebForms;
using Plataforma_Web.Data;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using PlataformaWeb;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity.Validation;

namespace Plataforma_Web.Controllers.Alumnos
{
    public class AlumnosController : Controller
    {
        private GestionUsuariosContext gestionUsuariosDb = new GestionUsuariosContext();
        private TutoriasContext tutoriasDb = new TutoriasContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = Session["Usuario"] as Usuario;

            if (usuario == null || usuario.IdNivel == 1 || usuario.IdNivel == 2)
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // GET: Alumnos - Vista principal
        public ActionResult Index()
        {
            try
            {
                Usuario user = Session["Usuario"] as Usuario;

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Cargar catálogos para la tarjeta de estadísticas
                try
                {
                    var carreras = tutoriasDb.Carreras
                        .AsNoTracking()
                        .ToDictionary(c => c.IdCarrera, c => c.Nombre);

                    ViewBag.UserNivel = user.IdNivel;
                    ViewBag.UserCarrera = user.IdCarrera;
                    ViewBag.UserCarreraNombre = user.IdCarrera > 0 && carreras.ContainsKey(user.IdCarrera)
                        ? carreras[user.IdCarrera]
                        : "Sin asignar";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al cargar catálogos: {ex.Message}");
                    ViewBag.UserNivel = user.IdNivel;
                    ViewBag.UserCarrera = user.IdCarrera;
                    ViewBag.UserCarreraNombre = "Error al cargar";
                }

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar la vista: " + ex.Message;
                System.Diagnostics.Debug.WriteLine($"[Alumnos] Error completo: {ex}");
                return View();
            }
        }

        // POST: GetAlumnos - DataTables Server-Side Processing
        [LecturaPermitida]
        [HttpPost]
        public ActionResult GetAlumnos()
        {
            try
            {
                Usuario user = Session["Usuario"] as Usuario;
                if (user == null)
                {
                    return Json(new { error = "Sesión no válida" }, JsonRequestBehavior.AllowGet);
                }

                // Leer parámetros de DataTables
                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumn = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                gestionUsuariosDb.Database.CommandTimeout = 120;
                tutoriasDb.Database.CommandTimeout = 120;

                // Obtener correos de alumnos habilitados
                var correos = gestionUsuariosDb.Alumnos
                    .Where(a => a.Habilitado == true)
                    .Select(a => a.CorreoElectronico)
                    .ToList();

                // Obtener DatosPersonales en memoria
                var datosPersonalesList = tutoriasDb.DatosPersonales
                    .Where(dp => correos.Contains(dp.Email))
                    .Select(dp => new
                    {
                        dp.Email,
                        dp.Matricula,
                        dp.IdCarrera,
                        dp.IdTurno,
                        dp.IdGrado,
                        dp.IdGrupo,
                        dp.Especialidad
                    })
                    .ToList();

                var datosPersonalesDict = datosPersonalesList
                    .Where(dp => !string.IsNullOrEmpty(dp.Email))
                    .GroupBy(dp => dp.Email.ToLower())
                    .ToDictionary(g => g.Key, g => g.First());

                // Cargar catálogos en memoria
                var carrerasDict = tutoriasDb.Carreras
                    .ToDictionary(c => c.IdCarrera, c => c.Nombre);

                var turnosDict = tutoriasDb.Turnoes
                    .ToDictionary(t => t.IdTurno, t => t.Nombre);

                var gradosDict = tutoriasDb.Gradoes
                    .ToDictionary(g => g.IdGrado, g => g.Nombre);

                var gruposDict = tutoriasDb.Grupoes
                    .ToDictionary(g => g.IdGrupo, g => g.Nombre);

                // Query base
                var alumnosQuery = gestionUsuariosDb.Alumnos
                    .Where(a => a.Habilitado == true)
                    .AsQueryable();

                // Total de registros SIN filtro
                int recordsTotal = alumnosQuery.Count();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(searchValue))
                {
                    alumnosQuery = alumnosQuery.Where(a =>
                        a.Nombre.Contains(searchValue) ||
                        a.ApellidoPaterno.Contains(searchValue) ||
                        a.ApellidoMaterno.Contains(searchValue) ||
                        a.Matricula.Contains(searchValue) ||
                        a.CorreoElectronico.Contains(searchValue));
                }

                // Obtener datos en memoria para aplicar filtro por carrera
                var alumnosList = alumnosQuery.ToList();

                // Filtrar por carrera (si es coordinador)
                if (user.IdNivel == 3)
                {
                    alumnosList = alumnosList.Where(a =>
                    {
                        var emailKey = a.CorreoElectronico?.ToLower();
                        if (!string.IsNullOrEmpty(emailKey) && datosPersonalesDict.ContainsKey(emailKey))
                        {
                            return datosPersonalesDict[emailKey].IdCarrera == user.IdCarrera;
                        }
                        return a.IdCarrera == user.IdCarrera;
                    }).ToList();
                }

                // Total de registros CON filtro
                int recordsFiltered = alumnosList.Count;

                // Aplicar ordenamiento
                switch (sortColumn)
                {
                    case "0": // Nombre Completo
                        alumnosList = sortDirection == "asc"
                            ? alumnosList.OrderBy(a => a.Nombre).ThenBy(a => a.ApellidoPaterno).ToList()
                            : alumnosList.OrderByDescending(a => a.Nombre).ThenByDescending(a => a.ApellidoPaterno).ToList();
                        break;
                    case "1": // Matrícula
                        alumnosList = sortDirection == "asc"
                            ? alumnosList.OrderBy(a => a.Matricula).ToList()
                            : alumnosList.OrderByDescending(a => a.Matricula).ToList();
                        break;
                    default:
                        alumnosList = alumnosList.OrderBy(a => a.Nombre).ThenBy(a => a.ApellidoPaterno).ToList();
                        break;
                }

                // Aplicar paginación
                var data = alumnosList.Skip(skip).Take(pageSize).ToList();

                // Preparar respuesta
                var dataResponse = data.Select(a =>
                {
                    var emailKey = a.CorreoElectronico?.ToLower();
                    var datosPersonales = !string.IsNullOrEmpty(emailKey) && datosPersonalesDict.ContainsKey(emailKey)
                        ? datosPersonalesDict[emailKey]
                        : null;

                    int carreraReal = datosPersonales?.IdCarrera ?? a.IdCarrera;

                    return new
                    {
                        NombreCompleto = $"{a.Nombre} {a.ApellidoPaterno} {a.ApellidoMaterno}".Trim(),
                        Matricula = a.Matricula ?? "Sin matrícula",
                        EspecialidadNombre = !string.IsNullOrEmpty(datosPersonales?.Especialidad)
                            ? datosPersonales.Especialidad
                            : "-",
                        TurnoNombre = datosPersonales != null && datosPersonales.IdTurno > 0 && turnosDict.ContainsKey(datosPersonales.IdTurno)
                            ? turnosDict[datosPersonales.IdTurno]
                            : "-",
                        GradoNombre = datosPersonales != null && datosPersonales.IdGrado > 0 && gradosDict.ContainsKey(datosPersonales.IdGrado)
                            ? gradosDict[datosPersonales.IdGrado]
                            : "-",
                        GrupoNombre = datosPersonales != null && datosPersonales.IdGrupo > 0 && gruposDict.ContainsKey(datosPersonales.IdGrupo)
                            ? gruposDict[datosPersonales.IdGrupo]
                            : "-",
                        Estado = a.Habilitado,
                        CorreoElectronico = a.CorreoElectronico ?? "Sin correo",
                        IdCarrera = carreraReal,
                        IdTurno = datosPersonales?.IdTurno ?? 0,
                        IdGrado = datosPersonales?.IdGrado ?? 0,
                        IdGrupo = datosPersonales?.IdGrupo ?? 0,
                        TieneEspecialidad = !string.IsNullOrEmpty(datosPersonales?.Especialidad)
                    };
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
                System.Diagnostics.Debug.WriteLine($"[GetAlumnos] Error: {ex}");
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gestionUsuariosDb?.Dispose();
                tutoriasDb?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ViewModel para los datos del alumno
    public class AlumnoViewModel
    {
        public int IdAlumno { get; set; }
        public string NombreCompleto { get; set; }
        public string Matricula { get; set; }
        public string CorreoElectronico { get; set; }
        public int IdCarrera { get; set; }
        public int IdTurno { get; set; }
        public int IdGrado { get; set; }
        public int IdGrupo { get; set; }
        public bool Estado { get; set; }
    }

    // Clase auxiliar para datos personales
    public class DatosPersonalesDto
    {
        public string Email { get; set; }
        public string Matricula { get; set; }
        public int IdCarrera { get; set; }
        public int IdTurno { get; set; }
        public int IdGrado { get; set; }
        public int IdGrupo { get; set; }
    }
}