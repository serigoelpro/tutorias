using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models.Materias;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.Materias
{
    public class MisMateriasController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = Session["Usuario"] as Usuario;

            // ✅ Si no hay sesión, redirigir al login
            if (usuario == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new { controller = "Home", action = "Login" })
                );
                return;
            }

            // ✅ SOLO permitir acceso a estudiantes (Nivel 1)
            if (usuario.IdNivel != 1)
            {
                // Guardar mensaje de error en TempData
                TempData["Error"] = "No tienes permisos para acceder a esta sección. Solo estudiantes pueden ver sus materias.";

                // Redirigir al Index de Home
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new { controller = "Home", action = "Index" })
                );
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // GET: MisMaterias - Vista para estudiantes
        public ActionResult Index()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            var lista = new List<MateriaAlumno>();
            string nombreAlumno = "";
            string matricula = "";
            string carrera = "";

            try
            {
                // 1. Obtener el IdPersona del usuario logueado desde la base de datos
                int idPersona = 0;

                // Buscar el IdPersona del usuario en DatosPersonales usando su UserName
                var datosPersona = db.Database.SqlQuery<DatosPersonaSimple>(
                    @"SELECT dp.IdPersona, dp.Nombre, dp.Matricula 
                      FROM DatosPersonales dp 
                      INNER JOIN Usuarios u ON dp.Matricula = u.UserName 
                      WHERE u.IdUsuario = @p0",
                    usuario.IdUsuario).FirstOrDefault();

                if (datosPersona != null)
                {
                    idPersona = datosPersona.IdPersona;
                }
                else
                {
                    // ✅ CORRECCIÓN: Usar int? nullable para manejar casos sin resultados
                    var dpAlt = db.Database.SqlQuery<int?>(
                        @"SELECT TOP 1 IdPersona FROM DatosPersonales 
                          WHERE Matricula = @p0",
                        usuario.UserName).FirstOrDefault();
                    idPersona = dpAlt ?? 0;
                }

                if (idPersona == 0)
                {
                    ViewBag.Error = "No se encontró información del estudiante. Por favor contacta a tu tutor.";
                    ViewBag.AlumnoNombre = "Estudiante";
                    ViewBag.Matricula = "";
                    ViewBag.Carrera = "";
                    ViewBag.IdPersona = 0;
                    ViewBag.IdGrado = 0;
                    ViewBag.TotalReprobadas = 0;
                    ViewBag.TotalExtraordinarios = 0;
                    return View(new List<MateriaAlumno>());
                }

                // 2. Obtener datos del estudiante
                var estudiante = db.Database.SqlQuery<EstudianteDatos>(
                    @"SELECT IdPersona, Nombre, Matricula, IdCarrera, IdGrado, Especialidad 
                      FROM DatosPersonales 
                      WHERE IdPersona = @p0", idPersona).FirstOrDefault();

                if (estudiante == null)
                {
                    ViewBag.Error = "Estudiante no encontrado";
                    ViewBag.AlumnoNombre = "Estudiante";
                    ViewBag.Matricula = "";
                    ViewBag.Carrera = "";
                    ViewBag.IdPersona = idPersona;
                    ViewBag.IdGrado = 0;
                    ViewBag.TotalReprobadas = 0;
                    ViewBag.TotalExtraordinarios = 0;
                    return View(new List<MateriaAlumno>());
                }

                nombreAlumno = estudiante.Nombre;
                matricula = estudiante.Matricula;
                int idCarrera = estudiante.IdCarrera;
                int idGrado = estudiante.IdGrado;

                string especialidadAlumno = estudiante.Especialidad ?? "";

                // 3. Obtener nombre de carrera
                var carreraData = db.Carreras.FirstOrDefault(c => c.IdCarrera == idCarrera);
                carrera = carreraData?.Nombre ?? "Carrera desconocida";

                // 4. ✅ CONSULTA MEJORADA - INCLUIR MATERIAS DEL CUATRIMESTRE ACTUAL
                var materiasCompletas = db.Database.SqlQuery<MateriaCompletaDto>(
                    @"-- ✅ PARTE 1: Materias activas del plan de estudios del cuatrimestre ACTUAL
    SELECT DISTINCT 
        m.IdMateria, m.Nombre, m.IdCarrera, m.IdEspecialidad, m.IdGrado,
        m.IdPlanEstudio, m.NumeroUnidades, m.Activo,
        ma.Calificacion, ISNULL(ma.Estado, 'Pendiente') as Estado,
        ISNULL(ma.Observaciones, '') as Observaciones,
        ISNULL(ma.IntentosExtraordinarios, 0) as IntentosExtraordinarios,
        ma.FechaExamenExtraordinario, ma.FechaInicioArrastre,
        p.Nombre as NombrePlan, p.Año as AñoPlan, p.CalificacionMinima,
        p.PermiteDecimales, p.Descripcion as DescripcionPlan
    FROM Materias m
    INNER JOIN Carreras c ON m.IdCarrera = c.IdCarrera
    INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
    INNER JOIN Gradoes g ON m.IdGrado = g.IdGrado
    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
    LEFT JOIN MateriasAlumno ma ON m.IdMateria = ma.IdMateria AND ma.IdPersona = @p1
    WHERE m.IdCarrera = @p2 
      AND m.IdGrado = @p3 
      AND LTRIM(RTRIM(LOWER(e.Nombre))) = LTRIM(RTRIM(LOWER(@p4)))
      AND m.Activo = 1

    UNION

    -- ✅ PARTE 2: Materias desactivadas que YA tienen registro para este alumno
    SELECT DISTINCT
        m.IdMateria, m.Nombre, m.IdCarrera, m.IdEspecialidad, m.IdGrado,
        m.IdPlanEstudio, m.NumeroUnidades, m.Activo,
        ma.Calificacion, ma.Estado,
        ISNULL(ma.Observaciones, '') as Observaciones,
        ISNULL(ma.IntentosExtraordinarios, 0) as IntentosExtraordinarios,
        ma.FechaExamenExtraordinario, ma.FechaInicioArrastre,
        p.Nombre as NombrePlan, p.Año as AñoPlan, p.CalificacionMinima,
        p.PermiteDecimales, p.Descripcion as DescripcionPlan
    FROM MateriasAlumno ma
    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
    INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
    WHERE ma.IdPersona = @p1
      -- Sin filtro de especialidad: se muestran todos los registros (incluyendo
      -- duplicados con especialidad distinta) para mantener consistencia con
      -- las vistas de tutor y coordinador. El usuario puede borrar duplicados.
      AND m.Activo = 0

    UNION

    -- ✅ PARTE 3: Materias REPROBADAS/EXTRAORDINARIO de cuatrimestres ANTERIORES
    SELECT DISTINCT
        m.IdMateria, m.Nombre, m.IdCarrera, m.IdEspecialidad, m.IdGrado,
        m.IdPlanEstudio, m.NumeroUnidades, m.Activo,
        ma.Calificacion, ma.Estado,
        ISNULL(ma.Observaciones, '') as Observaciones,
        ISNULL(ma.IntentosExtraordinarios, 0) as IntentosExtraordinarios,
        ma.FechaExamenExtraordinario, ma.FechaInicioArrastre,
        p.Nombre as NombrePlan, p.Año as AñoPlan, p.CalificacionMinima,
        p.PermiteDecimales, p.Descripcion as DescripcionPlan
    FROM MateriasAlumno ma
    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
    INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
    WHERE ma.IdPersona = @p1
      AND m.IdCarrera = @p2
      -- Sin filtro de especialidad: se muestran todos los registros de arrastre
      -- (incluyendo duplicados con especialidad distinta) para mantener consistencia
      -- con las vistas de tutor y coordinador. El usuario puede borrar duplicados.
      AND m.IdGrado < @p3
      AND (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')

    ORDER BY m.Activo DESC, m.IdGrado ASC, m.Nombre ASC",
                    idPersona, idPersona, idCarrera, idGrado, especialidadAlumno
                ).ToList();

                // 5. Mapear a objetos MateriaAlumno
                foreach (var materia in materiasCompletas)
                {
                    var materiaAlumno = new MateriaAlumno
                    {
                        IdMateria = materia.IdMateria,
                        IdPersona = idPersona,
                        NombreMateria = materia.Nombre,
                        NombreAlumno = nombreAlumno,
                        Matricula = matricula,
                        Calificacion = materia.Calificacion,
                        Estado = materia.Estado ?? "Pendiente",
                        Observaciones = materia.Observaciones ?? "",
                        IntentosExtraordinarios = materia.IntentosExtraordinarios,
                        FechaExamenExtraordinario = materia.FechaExamenExtraordinario,
                        FechaInicioArrastre = materia.FechaInicioArrastre,
                        IdGrado = materia.IdGrado,
                        MateriaActiva = materia.Activo,
                        NumeroUnidades = materia.NumeroUnidades,
                        IdPlanEstudio = materia.IdPlanEstudio,
                        NombrePlan = materia.NombrePlan ?? "Sin plan",
                        AñoPlan = materia.AñoPlan ?? 0,
                        CalificacionMinimaPlan = materia.CalificacionMinima ?? 7.0m,
                        PermiteDecimales = materia.PermiteDecimales ?? true,
                        DescripcionPlan = materia.DescripcionPlan ?? ""
                    };

                    lista.Add(materiaAlumno);
                }

                // 6. ✅ OBTENER CALIFICACIONES DE UNIDADES (solo si hay materias)
                if (lista.Any())
                {
                    var calificacionesUnidades = db.Database.SqlQuery<CalificacionUnidadInfo>(
                        @"SELECT cu.Id, cu.IdMateriaAlumno, cu.NumeroUnidad, cu.Calificacion,
                               cu.FechaRegistro, cu.FechaActualizacion, ma.IdMateria
                        FROM CalificacionesUnidades cu
                        INNER JOIN MateriasAlumno ma ON cu.IdMateriaAlumno = ma.Id
                        WHERE ma.IdPersona = @p0
                        ORDER BY ma.IdMateria, cu.NumeroUnidad",
                        idPersona
                    ).ToList();

                    // Asignar las calificaciones de unidades a cada materia
                    foreach (var materia in lista)
                    {
                        materia.CalificacionesUnidades = calificacionesUnidades
                            .Where(cu => cu.IdMateria == materia.IdMateria)
                            .Select(cu => new CalificacionUnidad
                            {
                                Id = cu.Id,
                                IdMateriaAlumno = cu.IdMateriaAlumno,
                                NumeroUnidad = cu.NumeroUnidad,
                                Calificacion = cu.Calificacion,
                                FechaRegistro = cu.FechaRegistro,
                                FechaActualizacion = cu.FechaActualizacion
                            })
                            .ToList();
                    }
                }

                // 7. ✅ CALCULAR CONTADORES GLOBALES - MEJORADO con ISNULL
                var contadores = db.Database.SqlQuery<ContadoresSimples>(
                    @"SELECT 
                        ISNULL(SUM(CASE WHEN ma.Estado = 'Reprobada' THEN 1 ELSE 0 END), 0) as Reprobadas,
                        ISNULL(SUM(CASE WHEN ma.Estado = 'Extraordinario' THEN 1 ELSE 0 END), 0) as Extraordinario
                    FROM MateriasAlumno ma
                    WHERE ma.IdPersona = @p0",
                    idPersona
                ).FirstOrDefault();

                int totalReprobadas = contadores?.Reprobadas ?? 0;
                int totalExtraordinarios = contadores?.Extraordinario ?? 0;

                // 8. Pasar datos a la vista
                ViewBag.AlumnoNombre = nombreAlumno;
                ViewBag.Matricula = matricula;
                ViewBag.Carrera = carrera;
                ViewBag.IdPersona = idPersona;
                ViewBag.IdGrado = idGrado;
                ViewBag.TotalReprobadas = totalReprobadas;
                ViewBag.TotalExtraordinarios = totalExtraordinarios;

                return View(lista);
            }
            catch (Exception ex)
            {
                // ✅ MENSAJE MÁS AMIGABLE cuando no hay materias
                string mensajeError = ex.Message.Contains("cast to value type")
                    ? "No tienes materias registradas aún. Por favor contacta a tu tutor para que te asignen tus materias."
                    : "Error al cargar las materias: " + ex.Message;

                ViewBag.Error = mensajeError;
                ViewBag.AlumnoNombre = nombreAlumno;
                ViewBag.Matricula = matricula;
                ViewBag.Carrera = carrera;
                ViewBag.IdPersona = 0;
                ViewBag.IdGrado = 0;
                ViewBag.TotalReprobadas = 0;
                ViewBag.TotalExtraordinarios = 0;
                return View(new List<MateriaAlumno>());
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ===== CLASES DTO LOCALES (Privadas para evitar conflictos) =====

        private class DatosPersonaSimple
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; }
            public string Matricula { get; set; }
        }

        private class EstudianteDatos
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; }
            public string Matricula { get; set; }
            public int IdCarrera { get; set; }
            public int IdGrado { get; set; }
            public string Especialidad { get; set; }
        }

        private class MateriaCompletaDto
        {
            public int IdMateria { get; set; }
            public string Nombre { get; set; }
            public int IdCarrera { get; set; }
            public int IdEspecialidad { get; set; }
            public int IdGrado { get; set; }
            public int IdPlanEstudio { get; set; }
            public int NumeroUnidades { get; set; }
            public bool Activo { get; set; }
            public decimal? Calificacion { get; set; }
            public string Estado { get; set; }
            public string Observaciones { get; set; }
            public int IntentosExtraordinarios { get; set; }
            public DateTime? FechaExamenExtraordinario { get; set; }
            public DateTime? FechaInicioArrastre { get; set; }
            public string NombrePlan { get; set; }
            public int? AñoPlan { get; set; }
            public decimal? CalificacionMinima { get; set; }
            public bool? PermiteDecimales { get; set; }
            public string DescripcionPlan { get; set; }
        }

        private class CalificacionUnidadInfo
        {
            public int Id { get; set; }
            public int IdMateriaAlumno { get; set; }
            public int NumeroUnidad { get; set; }
            public decimal? Calificacion { get; set; }
            public DateTime FechaRegistro { get; set; }
            public DateTime FechaActualizacion { get; set; }
            public int IdMateria { get; set; }
        }

        private class ContadoresSimples
        {
            public int Reprobadas { get; set; }
            public int Extraordinario { get; set; }
        }
    }
}