using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.codec.wmf;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Controllers.Materias;
using PlataformaWeb.Models.Materias;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using iTextFont = iTextSharp.text.Font;
using iTextParagraph = iTextSharp.text.Paragraph;

namespace PlataformaWeb.Controllers.Materias
{
    public class MateriasAlumnoController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Response.ContentType = "text/html; charset=utf-8";
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            base.OnActionExecuting(filterContext);
        }

        // ====================================================================
        // ✅ MÉTODO HELPER DE SEGURIDAD (NUEVO)
        // ====================================================================
        /// <summary>
        /// Verifica si el usuario actual tiene permisos para ver al alumno solicitado.
        /// Nivel 4 (Master): Ve a todos.
        /// Nivel 3 (Coordinador): Ve alumnos de su carrera.
        /// Nivel 2 (Tutor): Ve alumnos de sus grupos asignados.
        /// </summary>
        private bool UsuarioPuedeVerAlumno(Usuario usuario, int idPersona)
        {
            if (usuario == null) return false;

            // 1. Obtener los datos del alumno (necesitamos sus IDs de grupo/carrera)
            var alumno = db.DatosPersonales.Find(idPersona);
            if (alumno == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Seguridad Index] Alumno {idPersona} no encontrado en DB.");
                return false; // Alumno no existe
            }

            switch (usuario.IdNivel)
            {
                case 4: // Master
                    System.Diagnostics.Debug.WriteLine($"[Seguridad Index] Acceso MASTER (Nivel 4) a Alumno {idPersona}. Acceso concedido.");
                    return true;

                case 3: // Coordinador
                    bool accesoCoord = alumno.IdCarrera == usuario.IdCarrera;
                    System.Diagnostics.Debug.WriteLine($"[Seguridad Index] Acceso COORDINADOR (Nivel 3) a Alumno {idPersona}. " +
                                                       $"Alumno Carrera: {alumno.IdCarrera}, Coord Carrera: {usuario.IdCarrera}. Acceso: {accesoCoord}");
                    return accesoCoord;

                case 2: // Tutor
                    // Verificar si este alumno pertenece a alguno de los grupos asignados al tutor
                    // Comparamos los campos clave del alumno con los grupos del tutor
                    bool esDeMiGrupo = db.TutoriaGrupals.Any(tg =>
                        tg.IdCarrera == alumno.IdCarrera &&
                        tg.IdGrado == alumno.IdGrado &&
                        tg.IdGrupo == alumno.IdGrupo &&
                        tg.IdTurno == alumno.IdTurno &&
                        tg.IdPeriodo == alumno.IdPeriodo &&
                        tg.Año == alumno.Año &&
                        tg.IdUsuario == usuario.IdUsuario
                    );

                    System.Diagnostics.Debug.WriteLine($"[Seguridad Index] Acceso TUTOR (Nivel 2) a Alumno {idPersona}. " +
                                                       $"¿Pertenece a un grupo del tutor ({usuario.IdUsuario})? {esDeMiGrupo}");
                    return esDeMiGrupo;

                default: // Nivel < 2 o Nivel > 4
                    System.Diagnostics.Debug.WriteLine($"[Seguridad Index] Nivel {usuario.IdNivel} no autorizado.");
                    return false;
            }
        }


        // ====================================================================
        // ✅ MÉTODO INDEX (ACTUALIZADO CON SEGURIDAD)
        // ====================================================================
        public ActionResult Index(int id)
        {
            // 1. VALIDACIONES DE SEGURIDAD (Se mantienen igual)
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null) return RedirectToAction("Login", "Home");
            if (usuario.IdNivel < 2 || usuario.IdNivel > 4) return RedirectToAction("Index", "Home");
            if (!UsuarioPuedeVerAlumno(usuario, id)) return RedirectToAction("ArrastrePorGrupo", "ArrastreGrupo");

            // Rol Director = solo lectura: esta vista GET auto-inicializa filas (INSERT) si faltan.
            // Para un Director se omiten esos INSERT; la vista se renderiza igual (estados por defecto en memoria).
            bool esDirector = usuario.EsDirector;

            var lista = new List<MateriaAlumno>();
            string nombreAlumno = "";
            string matricula = "";
            string carrera = "";

            try
            {
                // 1. Obtener datos del estudiante
                var estudiante = db.Database.SqlQuery<EstudianteDto>(
                    @"SELECT IdPersona, Nombre, Matricula, IdCarrera, IdGrado, Especialidad 
              FROM DatosPersonales 
              WHERE IdPersona = @p0", id).FirstOrDefault();

                if (estudiante == null) return View(new List<MateriaAlumno>());

                nombreAlumno = estudiante.Nombre;
                matricula = estudiante.Matricula;
                int idCarrera = estudiante.IdCarrera;
                int idGrado = estudiante.IdGrado;
                string especialidadTextoAlumno = estudiante.Especialidad ?? ""; // Texto backup

                // =================================================================================
                // 🔴 CORRECCIÓN CLAVE: OBTENER EL ID REAL DE LA ESPECIALIDAD DESDE EL GRUPO
                // =================================================================================
                // En lugar de confiar en el texto del alumno, buscamos la configuración de su grupo
                // para ver qué especialidad tiene asignada REALMENTE.
                int? idEspecialidadDetectada = db.Database.SqlQuery<int?>(@"
            SELECT TOP 1 tg.IdEspecialidad
            FROM DatosPersonales dp
            INNER JOIN TutoriaGrupals tg ON 
                dp.IdGrupo = tg.IdGrupo AND 
                dp.IdCarrera = tg.IdCarrera AND 
                dp.IdGrado = tg.IdGrado AND
                dp.IdTurno = tg.IdTurno AND
                dp.IdPeriodo = tg.IdPeriodo AND
                dp.Año = tg.Año
            WHERE dp.IdPersona = @p0", id).FirstOrDefault();

                // Si no se encuentra en el grupo, usamos 0 para que no rompa el SQL
                int idEspecialidadFiltro = idEspecialidadDetectada ?? 0;

                System.Diagnostics.Debug.WriteLine($"🔍 DEBUG: Alumno {nombreAlumno} - ID Especialidad Detectada: {idEspecialidadFiltro}");

                // 2. Obtener nombre de carrera
                var carreraData = db.Carreras.FirstOrDefault(c => c.IdCarrera == idCarrera);
                carrera = carreraData?.Nombre ?? "Carrera desconocida";

                // 3. CONSULTA DE MATERIAS MEJORADA (Usa ID O Texto)
                var materiasCompletas = db.Database.SqlQuery<MateriaCompletaConPlanDto>(@"
    -- ✅ PARTE 1: Materias activas (Filtro ESTRICTO por especialidad)
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
    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
    INNER JOIN Gradoes g ON m.IdGrado = g.IdGrado
    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
    LEFT JOIN MateriasAlumno ma ON m.IdMateria = ma.IdMateria AND ma.IdPersona = @p0
    WHERE m.IdCarrera = @p1 
      AND m.IdGrado = @p2 
      AND m.Activo = 1
      AND (
          -- 🎯 CASO 1: La materia coincide EXACTAMENTE con la especialidad del grupo
          (@p3 > 0 AND m.IdEspecialidad = @p3)
          OR
          -- 🎯 CASO 2: La materia es Tronco Común (NULL o 0)
          (@p3 > 0 AND (m.IdEspecialidad IS NULL OR m.IdEspecialidad = 0))
          OR
          -- 🎯 CASO 3: El grupo NO tiene especialidad (fallback: usar todas las materias del grado)
          (@p3 = 0)
      )

    UNION

    -- ✅ PARTE 2: Materias desactivadas que YA tienen registro (Histórico)
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
    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
    WHERE ma.IdPersona = @p0 
      AND m.Activo = 0

    UNION

    -- ✅ PARTE 3: Materias REPROBADAS de cuatrimestres ANTERIORES (Arrastre)
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
    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
    WHERE ma.IdPersona = @p0
      AND m.IdCarrera = @p1
      AND m.IdGrado < @p2
      AND (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')

    ORDER BY m.Activo DESC, m.IdGrado ASC, m.Nombre ASC
", id, idCarrera, idGrado, idEspecialidadFiltro).ToList();

                // 4. Convertir a objetos MateriaAlumno (Lógica existente)
                foreach (var materia in materiasCompletas)
                {
                    if (materia.Activo && string.IsNullOrEmpty(materia.Estado))
                    {
                        // Auto-inicializar estado pendiente si es nueva
                        try
                        {
                            if (!esDirector)
                            {
                                db.Database.ExecuteSqlCommand(
                                    "INSERT INTO MateriasAlumno (IdMateria, IdPersona, Estado) VALUES (@p0, @p1, 'Pendiente')",
                                    materia.IdMateria, id);
                            }
                            materia.Estado = "Pendiente";
                        }
                        catch { }
                    }

                    string estadoDepurado = materia.Estado ?? "Pendiente";

                    lista.Add(new MateriaAlumno
                    {
                        IdMateria = materia.IdMateria,
                        IdPersona = id,
                        NombreMateria = materia.Nombre,
                        IdGrado = materia.IdGrado,
                        Calificacion = materia.Calificacion,
                        Estado = estadoDepurado,
                        Observaciones = materia.Observaciones ?? "",
                        IntentosExtraordinarios = materia.IntentosExtraordinarios,
                        FechaExamenExtraordinario = materia.FechaExamenExtraordinario,
                        FechaInicioArrastre = materia.FechaInicioArrastre,
                        NombreAlumno = nombreAlumno,
                        Matricula = matricula,
                        MateriaActiva = materia.Activo,
                        EstadoMateria = materia.Activo ? "Activa" : "Desactivada",
                        IdPlanEstudio = materia.IdPlanEstudio,
                        NombrePlan = materia.NombrePlan ?? "Sin plan",
                        AñoPlan = materia.AñoPlan ?? 0,
                        CalificacionMinimaPlan = materia.CalificacionMinima ?? 7.0m,
                        PermiteDecimales = materia.PermiteDecimales ?? true,
                        DescripcionPlan = materia.DescripcionPlan ?? "",
                        NumeroUnidades = materia.NumeroUnidades
                    });
                }

                // 5. Cargar calificaciones por unidad (Lógica existente mantenida)
                foreach (var materia in lista)
                {
                    var idMateriaAlumno = db.Database.SqlQuery<int?>("SELECT Id FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1", materia.IdMateria, id).FirstOrDefault();
                    if (idMateriaAlumno.HasValue)
                    {
                        var unidades = db.Database.SqlQuery<CalificacionUnidad>(
                            "SELECT Id, IdMateriaAlumno, NumeroUnidad, Calificacion, FechaRegistro, FechaActualizacion FROM CalificacionesUnidades WHERE IdMateriaAlumno = @p0 ORDER BY NumeroUnidad",
                            idMateriaAlumno.Value).ToList();
                        materia.CalificacionesUnidades = unidades;

                        // Crear slots vacíos si faltan
                        if (unidades.Count < materia.NumeroUnidades)
                        {
                            for (int i = 1; i <= materia.NumeroUnidades; i++)
                            {
                                if (!unidades.Any(u => u.NumeroUnidad == i))
                                {
                                    if (!esDirector)
                                    {
                                        try { db.Database.ExecuteSqlCommand("INSERT INTO CalificacionesUnidades (IdMateriaAlumno, NumeroUnidad, Calificacion) VALUES (@p0, @p1, NULL)", idMateriaAlumno.Value, i); } catch { }
                                    }
                                    materia.CalificacionesUnidades.Add(new CalificacionUnidad { IdMateriaAlumno = idMateriaAlumno.Value, NumeroUnidad = i, Calificacion = null });
                                }
                            }
                        }
                    }
                }

                // 6. Contadores y ViewBags (Lógica existente)
                int totalReprobadas = lista.Count(m => m.Estado == "Reprobada");
                int totalExtraordinarios = lista.Count(m => m.Estado == "Extraordinario");

                ViewBag.TotalReprobadas = totalReprobadas;
                ViewBag.TotalExtraordinarios = totalExtraordinarios;
                ViewBag.TieneMateriasDesactivadas = lista.Any(m => !m.MateriaActiva);
                ViewBag.MateriasDesactivadas = lista.Count(m => !m.MateriaActiva);
                ViewBag.MateriasActivas = lista.Count(m => m.MateriaActiva);
                ViewBag.TotalMaterias = lista.Count;

                // Json para JS
                var planesParaJS = lista.ToDictionary(m => m.IdMateria.ToString(), m => new {
                    idMateria = m.IdMateria,
                    idPlanEstudio = m.IdPlanEstudio,
                    nombrePlan = m.NombrePlan,
                    calificacionMinima = m.CalificacionMinimaPlan,
                    permiteDecimales = m.PermiteDecimales,
                    numeroUnidades = m.NumeroUnidades
                });
                ViewBag.PlanesJson = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(planesParaJS);

                ViewBag.AlumnoNombre = nombreAlumno;
                ViewBag.Matricula = matricula;
                ViewBag.Carrera = carrera;
                ViewBag.IdPersona = id;
                ViewBag.IdGrado = idGrado;
                ViewBag.UsuarioNivel = usuario?.IdNivel ?? 0;

                return View(lista);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar materias: " + ex.Message + (ex.InnerException != null ? " - " + ex.InnerException.Message : "");
                System.Diagnostics.Debug.WriteLine($"❌ ERROR FATAL en Index: {ex.Message}");
                return View(new List<MateriaAlumno>());
            }
        }

        // MÉTODO PARA GUARDAR CALIFICACIÓN DE UNA UNIDAD ESPECÍFICA
        [HttpPost]
        public JsonResult GuardarCalificacionUnidad(int idMateria, int idPersona, int numeroUnidad, decimal? calificacion)
        {
            try
            {
                // 1. Obtener o crear el Id de MateriasAlumno
                var idMateriaAlumno = db.Database.SqlQuery<int?>(
                    @"SELECT Id FROM MateriasAlumno 
            WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona).FirstOrDefault();

                // ✅ SI NO EXISTE, CREAR EL REGISTRO
                if (!idMateriaAlumno.HasValue)
                {
                    try
                    {
                        db.Database.ExecuteSqlCommand(
                            @"INSERT INTO MateriasAlumno (IdMateria, IdPersona, Estado, Calificacion, FechaRegistro, FechaActualizacion) 
                    VALUES (@p0, @p1, 'Pendiente', NULL, GETDATE(), GETDATE())",
                            idMateria, idPersona);

                        idMateriaAlumno = db.Database.SqlQuery<int?>(
                            @"SELECT Id FROM MateriasAlumno 
                    WHERE IdMateria = @p0 AND IdPersona = @p1",
                            idMateria, idPersona).FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "Error al crear registro de materia: " + ex.Message });
                    }
                }

                if (!idMateriaAlumno.HasValue)
                {
                    return Json(new { success = false, message = "No se pudo crear el registro de materia" });
                }

                // ✅ VALIDAR QUE LA MATERIA ESTÉ EN ESTADO PENDIENTE
                var estadoMateria = db.Database.SqlQuery<string>(
                    @"SELECT Estado FROM MateriasAlumno WHERE Id = @p0",
                    idMateriaAlumno.Value).FirstOrDefault();

                if (estadoMateria != "Pendiente")
                {
                    return Json(new
                    {
                        success = false,
                        message = $"No se puede modificar. La materia ya fue confirmada con estado: {estadoMateria}"
                    });
                }

                // 2. Validar calificación
                if (calificacion.HasValue && (calificacion < 0 || calificacion > 10))
                {
                    return Json(new { success = false, message = "La calificación debe estar entre 0 y 10" });
                }

                // 3. Obtener información del plan
                var informacionMateria = db.Database.SqlQuery<MateriaConPlanDto>(@"
          SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdPlanEstudio, m.NumeroUnidades,
                 ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan, 
                 ISNULL(p.Año, 2024) as AñoPlan, 
                 ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima, 
                 ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
          FROM Materias m
          LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
          WHERE m.IdMateria = @p0", idMateria).FirstOrDefault();

                if (informacionMateria == null)
                {
                    return Json(new { success = false, message = "Materia no encontrada" });
                }

                // 4. Aplicar reglas del plan a la calificación de la unidad
                decimal? calificacionAjustada = calificacion;
                if (calificacion.HasValue)
                {
                    if (!(informacionMateria.PermiteDecimales ?? true))
                    {
                        // Plan 2020: Redondeo especial
                        if (calificacion.Value < 8.0m)
                            calificacionAjustada = Math.Floor(calificacion.Value);
                        else
                            calificacionAjustada = Math.Round(calificacion.Value, 0);
                    }
                    else
                    {
                        // Plan 2024: Mantener decimales
                        calificacionAjustada = Math.Round(calificacion.Value, 2);
                    }
                }

                // 5. Guardar o actualizar la calificación de la unidad
                var existe = db.Database.SqlQuery<int>(
                    @"SELECT COUNT(*) FROM CalificacionesUnidades 
            WHERE IdMateriaAlumno = @p0 AND NumeroUnidad = @p1",
                    idMateriaAlumno.Value, numeroUnidad).FirstOrDefault();

                if (existe > 0)
                {
                    db.Database.ExecuteSqlCommand(
                        @"UPDATE CalificacionesUnidades 
                SET Calificacion = @p0, FechaActualizacion = GETDATE()
                WHERE IdMateriaAlumno = @p1 AND NumeroUnidad = @p2",
                        calificacionAjustada, idMateriaAlumno.Value, numeroUnidad);
                }
                else
                {
                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO CalificacionesUnidades 
                (IdMateriaAlumno, NumeroUnidad, Calificacion, FechaRegistro, FechaActualizacion)
                VALUES (@p0, @p1, @p2, GETDATE(), GETDATE())",
                        idMateriaAlumno.Value, numeroUnidad, calificacionAjustada);
                }

                // 6. Obtener TODAS las unidades para calcular
                var todasLasUnidades = db.Database.SqlQuery<decimal?>(
                    @"SELECT Calificacion FROM CalificacionesUnidades 
            WHERE IdMateriaAlumno = @p0 
            ORDER BY NumeroUnidad",
                    idMateriaAlumno.Value).ToList();

                // ✅ CALCULAR VARIABLES NECESARIAS
                int unidadesCalificadas = todasLasUnidades.Count(c => c.HasValue);
                decimal porcentajeAvance = Math.Round((decimal)unidadesCalificadas / informacionMateria.NumeroUnidades * 100, 1);

                decimal? calificacionFinalPreview = null;
                bool todasCalificadas = unidadesCalificadas == informacionMateria.NumeroUnidades;

                if (todasCalificadas)
                {
                    // Calcular promedio
                    decimal promedio = todasLasUnidades.Average(c => c.Value);

                    // ✅ NUEVA LÓGICA: Validar según las reglas específicas de cada plan
                    bool cumpleRequisitos = true;
                    string mensajeValidacion = "";

                    if (!(informacionMateria.PermiteDecimales ?? true))
                    {
                        // ══════════════════════════════════════════════════════════════
                        // PLAN 2020: TODAS LAS UNIDADES DEBEN SER ≥ 8
                        // ══════════════════════════════════════════════════════════════
                        var unidadesReprobadas = todasLasUnidades.Where(c => c.HasValue && c.Value < 8.0m).ToList();

                        if (unidadesReprobadas.Any())
                        {
                            cumpleRequisitos = false;
                            mensajeValidacion = $"No acreditada";

                            // 🔴 CRÍTICO: FORZAR CALIFICACIÓN REPROBATORIA
                            // Si hay unidades < 8, la calificación final es 6 (o el promedio si es menor)
                            if (promedio < 8.0m)
                                calificacionFinalPreview = Math.Floor(promedio);
                            else
                                calificacionFinalPreview = Math.Floor(promedio); // Forzar hacia abajo aunque sea ≥8

                            // ASEGURAR que sea reprobatoria (< 8)
                            if (calificacionFinalPreview >= 8.0m)
                                calificacionFinalPreview = 7.0m; // Forzar a 7 para que sea reprobatoria
                        }
                        else
                        {
                            // Todas las unidades ≥ 8, calcular normalmente
                            if (promedio < 8.0m)
                                calificacionFinalPreview = Math.Floor(promedio);
                            else
                                calificacionFinalPreview = Math.Round(promedio, 0);
                        }
                    }
                    else
                    {
                        // ══════════════════════════════════════════════════════════════
                        // PLAN 2024: SOLO IMPORTA EL PROMEDIO FINAL ≥ 7
                        // ══════════════════════════════════════════════════════════════
                        calificacionFinalPreview = Math.Round(promedio, 2);

                        if (calificacionFinalPreview.Value < (informacionMateria.CalificacionMinima ?? 7.0m))
                        {
                            cumpleRequisitos = false;
                            mensajeValidacion = $"No acreditada";
                        }
                    }

                    // ✅ Determinar estado sugerido basado en si cumple requisitos
                    string estadoSugerido = cumpleRequisitos ? "Acreditada" : "Extraordinario";

                    // 7. Retornar resultado SIN actualizar MateriasAlumno
                    return Json(new
                    {
                        success = true,
                        message = todasCalificadas
                            ? (cumpleRequisitos
                                ? $"✅ Todas las unidades calificadas. Promedio: {calificacionFinalPreview:0.00} - APROBADA"
                                : $"⚠️ Todas las unidades calificadas. Promedio: {calificacionFinalPreview:0.00} - {mensajeValidacion}")
                            : $"✅ Unidad {numeroUnidad} guardada ({porcentajeAvance}% completado)",
                        calificacionFinalPreview = calificacionFinalPreview,
                        todasCalificadas = todasCalificadas,
                        unidadesCalificadas = unidadesCalificadas,
                        totalUnidades = informacionMateria.NumeroUnidades,
                        porcentajeAvance = porcentajeAvance,
                        cumpleRequisitos = cumpleRequisitos,
                        estadoSugerido = estadoSugerido,
                        mensajeValidacion = mensajeValidacion,
                        puedeConfirmar = todasCalificadas
                    });
                }
                else
                {
                    // Si no están todas calificadas
                    return Json(new
                    {
                        success = true,
                        message = $"✅ Unidad {numeroUnidad} guardada ({porcentajeAvance}% completado)",
                        calificacionFinalPreview = calificacionFinalPreview,
                        todasCalificadas = false,
                        unidadesCalificadas = unidadesCalificadas,
                        totalUnidades = informacionMateria.NumeroUnidades,
                        porcentajeAvance = porcentajeAvance,
                        puedeConfirmar = false
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        [HttpPost]
        public JsonResult ConfirmarCalificacionesMateria(int idMateria, int idPersona)
        {
            try
            {
                // ✅ VALIDAR QUE LA MATERIA NO HAYA SIDO CONFIRMADA PREVIAMENTE
                var estadoActual = db.Database.SqlQuery<string>(
                    @"SELECT Estado FROM MateriasAlumno 
              WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona).FirstOrDefault();

                if (estadoActual != null && estadoActual != "Pendiente")
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Esta materia ya fue confirmada anteriormente con estado: {estadoActual}. No se puede volver a confirmar."
                    });
                }

                // 1. Obtener el Id de MateriasAlumno
                var idMateriaAlumno = db.Database.SqlQuery<int?>(
                    @"SELECT Id FROM MateriasAlumno 
              WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona).FirstOrDefault();

                if (!idMateriaAlumno.HasValue)
                {
                    return Json(new { success = false, message = "Registro de materia no encontrado" });
                }

                // 2. Verificar que todas las unidades estén calificadas
                var informacionMateria = db.Database.SqlQuery<MateriaConPlanDto>(@"
            SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdPlanEstudio, m.NumeroUnidades,
                   ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan, 
                   ISNULL(p.Año, 2024) as AñoPlan, 
                   ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima, 
                   ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
            FROM Materias m
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdMateria = @p0", idMateria).FirstOrDefault();

                if (informacionMateria == null)
                {
                    return Json(new { success = false, message = "Materia no encontrada" });
                }

                // 3. Obtener todas las calificaciones de unidades
                var unidades = db.Database.SqlQuery<decimal?>(
                    @"SELECT Calificacion FROM CalificacionesUnidades 
              WHERE IdMateriaAlumno = @p0 
              ORDER BY NumeroUnidad",
                    idMateriaAlumno.Value).ToList();

                // 4. Verificar que todas estén calificadas
                bool todasCalificadas = unidades.Count == informacionMateria.NumeroUnidades &&
                                        unidades.All(c => c.HasValue);

                if (!todasCalificadas)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Debe calificar todas las unidades antes de confirmar. Faltan {informacionMateria.NumeroUnidades - unidades.Count(u => u.HasValue)} unidad(es)"
                    });
                }

                // 5. Calcular promedio ORIGINAL
                decimal promedioOriginal = unidades.Average(c => c.Value);

                // ============================================================================
                // 🔴 NUEVA LÓGICA CORREGIDA: Validar reglas según el plan
                // ============================================================================
                decimal calificacionFinal;
                string estadoFinal;
                string observaciones = "";
                bool cumpleRequisitos = true;

                if (!(informacionMateria.PermiteDecimales ?? true))
                {
                    // ══════════════════════════════════════════════════════════════
                    // PLAN 2020: TODAS LAS UNIDADES DEBEN SER ≥ 8
                    // ══════════════════════════════════════════════════════════════
                    var unidadesReprobadas = unidades.Where(c => c.HasValue && c.Value < 8.0m).ToList();

                    if (unidadesReprobadas.Any())
                    {
                        cumpleRequisitos = false;
                        observaciones = "";

                        // 🔴 CRÍTICO: FORZAR CALIFICACIÓN REPROBATORIA
                        if (promedioOriginal < 8.0m)
                            calificacionFinal = Math.Floor(promedioOriginal);
                        else
                            calificacionFinal = Math.Floor(promedioOriginal);

                        // ASEGURAR que sea reprobatoria (< 8)
                        if (calificacionFinal >= 8.0m)
                            calificacionFinal = 7.0m;

                        estadoFinal = "Extraordinario";
                    }
                    else
                    {
                        // Todas las unidades ≥ 8, calcular normalmente
                        if (promedioOriginal < 8.0m)
                            calificacionFinal = Math.Floor(promedioOriginal);
                        else
                            calificacionFinal = Math.Round(promedioOriginal, 0);

                        estadoFinal = calificacionFinal >= informacionMateria.CalificacionMinima ?
                                     "Acreditada" : "Extraordinario";
                        observaciones = "Calificación final por unidades";
                    }
                }
                else
                {
                    // ══════════════════════════════════════════════════════════════
                    // PLAN 2024: SOLO IMPORTA EL PROMEDIO FINAL ≥ 7
                    // ══════════════════════════════════════════════════════════════
                    calificacionFinal = Math.Round(promedioOriginal, 2);

                    if (calificacionFinal < (informacionMateria.CalificacionMinima ?? 7.0m))
                    {
                        cumpleRequisitos = false;
                        estadoFinal = "Extraordinario";
                        observaciones = "";
                    }
                    else
                    {
                        estadoFinal = "Acreditada";
                        observaciones = "Calificación final por unidades";
                    }
                }

                int intentosFinales = estadoFinal == "Acreditada" ? 0 : 1;

                // ============================================================================
                // ✅ NUEVO: GUARDAR EN HistorialIntentosMateria
                // ============================================================================
                try
                {
                    // Verificar si ya existe el intento 1 (Ordinario)
                    var existeIntento = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM HistorialIntentosMateria 
                          WHERE IdMateriaAlumno = @p0 AND NumeroIntento = 1",
                        idMateriaAlumno.Value).FirstOrDefault();

                    if (existeIntento == 0)
                    {
                        // Insertar el intento ordinario en el historial
                        db.Database.ExecuteSqlCommand(@"
                            INSERT INTO HistorialIntentosMateria 
                            (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                             CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                            VALUES (@p0, @p1, @p2, @p3, @p4, @p5, GETDATE(), @p6)",
                            idMateriaAlumno.Value,
                            1, // NumeroIntento
                            "Ordinario", // TipoIntento
                            promedioOriginal, // Calificacion original
                            calificacionFinal, // CalificacionAjustada
                            cumpleRequisitos ? 1 : 0, // EsAprobatoria
                            observaciones // Observaciones
                        );
                    }
                }
                catch (Exception exHistorial)
                {
                    // Log del error pero no detener el proceso
                    System.Diagnostics.Debug.WriteLine($"Error al guardar historial: {exHistorial.Message}");
                }

                // 8. Actualizar MateriasAlumno con la calificación final
                db.Database.ExecuteSqlCommand(
                    @"UPDATE MateriasAlumno 
              SET Calificacion = @p0, 
                  Estado = @p1, 
                  IntentosExtraordinarios = @p2,
                  Observaciones = @p3,
                  FechaActualizacion = GETDATE()
              WHERE IdMateria = @p4 AND IdPersona = @p5",
                    calificacionFinal, estadoFinal, intentosFinales, observaciones, idMateria, idPersona);

                // 9. Verificar límites
                System.Threading.Thread.Sleep(200);

                var conteoPost = db.Database.SqlQuery<ContadorSimple>(
                    @"SELECT 
                COUNT(CASE WHEN Estado = 'Reprobada' THEN 1 END) as Reprobadas,
                COUNT(CASE WHEN Estado = 'Extraordinario' THEN 1 END) as Extraordinario
              FROM MateriasAlumno 
              WHERE IdPersona = @p0", idPersona).FirstOrDefault();

                bool excedioExtraordinarios = conteoPost.Extraordinario >= 4;
                bool excedioArrastre = conteoPost.Reprobadas >= 4;
                bool dadoDeBaja = excedioArrastre || excedioExtraordinarios;

                return Json(new
                {
                    success = true,
                    message = cumpleRequisitos
                        ? $" Materia confirmada: {calificacionFinal:0.00} - {estadoFinal}"
                        : $" Materia confirmada: {calificacionFinal:0.00} - {estadoFinal}",
                    calificacionFinal = calificacionFinal,
                    estado = estadoFinal,
                    intentos = intentosFinales,
                    observaciones = observaciones,
                    cumpleRequisitos = cumpleRequisitos,
                    alumnoReprobado = dadoDeBaja,
                    conteoActual = new
                    {
                        reprobadas = conteoPost.Reprobadas,
                        extraordinarios = conteoPost.Extraordinario
                    },
                    requiereRecarga = true
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }



        // ✅ ELIMINAR REGISTRO DE MATERIA-ALUMNO (con cascada)
        [HttpPost]
        public JsonResult EliminarMateriaAlumno(int idMateria, int idPersona)
        {
            try
            {
                // 1. Obtener el Id interno de MateriasAlumno
                var idMA = db.Database.SqlQuery<int?>(
                    "SELECT Id FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona).FirstOrDefault();

                if (!idMA.HasValue)
                    return Json(new { success = false, message = "Registro no encontrado." });

                // 2. Eliminar registros hijos en cascada
                db.Database.ExecuteSqlCommand(
                    "DELETE FROM CalificacionesUnidades WHERE IdMateriaAlumno = @p0", idMA.Value);

                db.Database.ExecuteSqlCommand(
                    "DELETE FROM HistorialIntentosMateria WHERE IdMateriaAlumno = @p0", idMA.Value);

                // 3. Eliminar el registro principal
                db.Database.ExecuteSqlCommand(
                    "DELETE FROM MateriasAlumno WHERE Id = @p0", idMA.Value);

                System.Diagnostics.Debug.WriteLine(
                    $"[EliminarMateriaAlumno] Eliminado IdMateria={idMateria} IdPersona={idPersona} IdMA={idMA.Value}");

                return Json(new { success = true, message = "Registro eliminado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }

        // ✅ MÉTODO ACTUALIZAR MATERIA CORREGIDO
        [HttpPost]
        public JsonResult ActualizarMateria(int idMateria, int idPersona, decimal? calificacion, string estado,
            int intentosExtraordinarios = 0, DateTime? fechaExamen = null, string observaciones = "",
            DateTime? fechaInicioArrastre = null)
        {
            try
            {
                // Obtener información del plan
                var informacionMateria = db.Database.SqlQuery<MateriaConPlanDto>(@"
            SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdPlanEstudio,
                   ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan, 
                   ISNULL(p.Año, 2024) as AñoPlan, 
                   ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima, 
                   ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
            FROM Materias m
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdMateria = @p0", idMateria).FirstOrDefault();

                if (informacionMateria == null)
                {
                    return Json(new { success = false, message = "Materia no encontrada" });
                }

                // Validar calificación según plan
                if (calificacion.HasValue)
                {
                    var resultadoValidacion = ValidarCalificacionSegunPlan(
                        calificacion,
                        estado,
                        informacionMateria.CalificacionMinima ?? 7.0m,
                        informacionMateria.PermiteDecimales ?? true,
                        informacionMateria.NombrePlan ?? "Plan Estándar"
                    );

                    if (!resultadoValidacion.EsValida)
                    {
                        return Json(new { success = false, message = resultadoValidacion.MensajeError });
                    }

                    calificacion = resultadoValidacion.CalificacionAjustada;
                    estado = resultadoValidacion.EstadoFinal;
                }

                // Calculo centralizado de intentos - NO confiar en el valor del frontend
                intentosExtraordinarios = CalcularNuevosIntentos(idMateria, idPersona, estado);

                if (estado == "Extraordinario" && !fechaExamen.HasValue)
                    fechaExamen = DateTime.Now;
                else if (estado == "Reprobada" && !fechaInicioArrastre.HasValue)
                    fechaInicioArrastre = DateTime.Now;
                else if (estado == "Acreditada")
                    fechaInicioArrastre = null;

                // Validar limite maximo de intentos
                if (intentosExtraordinarios > 3)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Se agotaron los intentos permitidos. La materia esta REPROBADA DEFINITIVA."
                    });
                }

                // ✅✅✅ CRÍTICO: NO AGREGAR OBSERVACIONES AUTOMÁTICAS
                // Solo usar las observaciones que el usuario escribió manualmente
                // Si observaciones es null o vacío, mantener las observaciones existentes
                string observacionesFinales = observaciones;

                // Si no se proporcionaron observaciones, obtener las existentes de la BD
                if (string.IsNullOrWhiteSpace(observaciones))
                {
                    var observacionesExistentes = db.Database.SqlQuery<string>(
                        @"SELECT ISNULL(Observaciones, '') FROM MateriasAlumno 
                  WHERE IdMateria = @p0 AND IdPersona = @p1",
                        idMateria, idPersona).FirstOrDefault();

                    observacionesFinales = observacionesExistentes ?? "";
                }

                // Guardar usando procedimiento almacenado
                var resultado = db.Database.SqlQuery<ProcedimientoResultado>(
                    @"EXEC sp_GuardarMateriaAlumno 
                @IdMateria = @p0, 
                @IdPersona = @p1, 
                @Calificacion = @p2, 
                @Estado = @p3,
                @IntentosExtraordinarios = @p4,
                @FechaExamenExtraordinario = @p5,
                @FechaInicioArrastre = @p6,
                @Observaciones = @p7",
                    idMateria, idPersona, calificacion, estado,
                    intentosExtraordinarios, fechaExamen, fechaInicioArrastre, observacionesFinales
                ).FirstOrDefault();

                if (resultado == null || resultado.Success != 1)
                {
                    return Json(new
                    {
                        success = false,
                        message = resultado?.Mensaje ?? "Error al guardar la materia"
                    });
                }

                var conteoPost = db.Database.SqlQuery<ContadorSimple>(
                    @"SELECT 
                COUNT(CASE WHEN Estado = 'Reprobada' THEN 1 END) as Reprobadas,
                COUNT(CASE WHEN Estado = 'Extraordinario' THEN 1 END) as Extraordinario
              FROM MateriasAlumno 
              WHERE IdPersona = @p0", idPersona).FirstOrDefault();

                bool excedioExtraordinarios = conteoPost.Extraordinario >= 4;
                bool excedioArrastre = conteoPost.Reprobadas >= 4;
                bool dadoDeBaja = excedioArrastre || excedioExtraordinarios;

                // ✅ MENSAJE SEGÚN INTENTO (SIN MENCIONAR OBSERVACIONES)
                string mensajeIntento = "";
                if (estado == "Reprobada")
                {
                    if (intentosExtraordinarios == 2)
                        mensajeIntento = " (1er intento en arrastre - queda 1 más)";
                    else if (intentosExtraordinarios == 3)
                        mensajeIntento = " (2do y ÚLTIMO intento en arrastre)";
                    else if (intentosExtraordinarios > 3)
                        mensajeIntento = " (REPROBADA DEFINITIVA)";
                }

                return Json(new
                {
                    success = true,
                    message = dadoDeBaja ?
                        "Materia guardada. El sistema detectó exceso de límites automáticamente." :
                        $"Materia actualizada correctamente según {informacionMateria.NombrePlan}{mensajeIntento}",

                    planAplicado = new
                    {
                        nombre = informacionMateria.NombrePlan,
                        año = informacionMateria.AñoPlan,
                        calificacionMinima = informacionMateria.CalificacionMinima,
                        permiteDecimales = informacionMateria.PermiteDecimales,
                        calificacionFinal = calificacion,
                        estadoFinal = estado,
                        intentosActuales = intentosExtraordinarios,
                        intentosRestantes = Math.Max(0, 3 - intentosExtraordinarios)
                    },

                    alumnoReprobado = dadoDeBaja,
                    conteoActual = new
                    {
                        reprobadas = conteoPost.Reprobadas,
                        extraordinarios = conteoPost.Extraordinario
                    },

                    requiereRecarga = dadoDeBaja || estado == "Acreditada",
                    fechaArrastre = fechaInicioArrastre?.ToString("yyyy-MM-dd"),
                    fechaExamen = fechaExamen?.ToString("yyyy-MM-dd"),
                    observacionesGuardadas = observacionesFinales  // ✅ Devolver las que realmente se guardaron
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar: " + ex.Message });
            }
        }

        // Calcula el valor correcto de IntentosExtraordinarios desde la BD, sin confiar en el frontend.
        // Centraliza la logica para evitar inconsistencias entre los distintos flujos de escritura.
        private int CalcularNuevosIntentos(int idMateria, int idPersona, string nuevoEstado)
        {
            // Leer el valor actual directamente de la BD
            var intentosActuales = db.Database.SqlQuery<int>(
                "SELECT ISNULL(IntentosExtraordinarios, 0) FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                idMateria, idPersona).FirstOrDefault();

            int intentosCalculados;

            switch (nuevoEstado)
            {
                case "Extraordinario":
                    intentosCalculados = 1;
                    break;
                case "Reprobada":
                    // Piso de 2: indica que la materia esta en fase de arrastre
                    // NO incrementa -- el valor es un indicador de fase, no un contador
                    intentosCalculados = Math.Max(intentosActuales, 2);
                    break;
                case "Acreditada":
                    intentosCalculados = 0;
                    break;
                default:
                    intentosCalculados = intentosActuales;
                    break;
            }

            // Validacion cruzada con HistorialIntentosMateria (solo para registros con historial)
            try
            {
                var intentosHistorial = db.Database.SqlQuery<int>(
                    @"SELECT COUNT(*) FROM HistorialIntentosMateria 
                      WHERE IdMateriaAlumno IN (
                          SELECT Id FROM MateriasAlumno 
                          WHERE IdMateria = @p0 AND IdPersona = @p1
                      )",
                    idMateria, idPersona).FirstOrDefault();

                // Solo alertar si HAY registros en el historial (datos nuevos, post-diciembre 2025)
                if (intentosHistorial > 0 && intentosHistorial != intentosActuales)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ALERTA DISCREPANCIA: Materia {idMateria}, Persona {idPersona}. " +
                        $"IntentosExtraordinarios en BD: {intentosActuales}, Registros en Historial: {intentosHistorial}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en validacion cruzada de historial: {ex.Message}");
            }

            return intentosCalculados;
        }

        // Registra un intento en HistorialIntentosMateria de forma centralizada.
        // Se usa desde cualquier flujo que modifique IntentosExtraordinarios.
        private void RegistrarEnHistorial(int idMateria, int idPersona, int numeroIntento,
            string tipoIntento, decimal? calificacion, decimal? calificacionAjustada,
            bool esAprobatoria, string observaciones)
        {
            try
            {
                // Obtener el Id de MateriasAlumno
                var idMateriaAlumno = db.Database.SqlQuery<int?>(
                    "SELECT Id FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona).FirstOrDefault();

                if (idMateriaAlumno.HasValue)
                {
                    db.Database.ExecuteSqlCommand(@"
                        INSERT INTO HistorialIntentosMateria 
                        (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                         CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                        VALUES (@p0, @p1, @p2, @p3, @p4, @p5, GETDATE(), @p6)",
                        idMateriaAlumno.Value,
                        numeroIntento,
                        tipoIntento,
                        calificacion ?? 0,
                        calificacionAjustada ?? 0,
                        esAprobatoria ? 1 : 0,
                        observaciones ?? ""
                    );
                }
            }
            catch (Exception ex)
            {
                // Log pero no detener el proceso principal
                System.Diagnostics.Debug.WriteLine($"Error al registrar historial: {ex.Message}");
            }
        }

        // VALIDAR CALIFICACION SEGUN PLAN DE ESTUDIO
        private ResultadoValidacionPlan ValidarCalificacionSegunPlan(
            decimal? calificacion,
            string estado,
            decimal calificacionMinima,
            bool permiteDecimales,
            string nombrePlan)
        {
            var resultado = new ResultadoValidacionPlan();

            if (!calificacion.HasValue)
            {
                resultado.CalificacionAjustada = null;
                resultado.EstadoFinal = "Pendiente";
                resultado.EsValida = true;
                resultado.MensajeValidacion = "Sin calificación";
                return resultado;
            }

            decimal calif = calificacion.Value;

            // Validar rango básico
            if (calif < 0 || calif > 10)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La calificación debe estar entre 0 y 10";
                return resultado;
            }

            // ✅ APLICAR REDONDEO SEGÚN EL PLAN
            if (!permiteDecimales)
            {
                // PLAN 2020: REDONDEO ESPECIAL
                if (calif < 8.0m)
                {
                    // Del 7.9 para abajo: redondear hacia ABAJO (floor)
                    resultado.CalificacionAjustada = Math.Floor(calif);
                    if (Math.Abs(calif - resultado.CalificacionAjustada.Value) > 0.001m)
                    {
                        resultado.MensajeValidacion = $"Calificación redondeada hacia abajo de {calif} a {resultado.CalificacionAjustada} ({nombrePlan})";
                    }
                }
                else
                {
                    // Del 8.0 para arriba: redondear NORMAL
                    resultado.CalificacionAjustada = Math.Round(calif, 0);
                    if (Math.Abs(calif - resultado.CalificacionAjustada.Value) > 0.001m)
                    {
                        resultado.MensajeValidacion = $"Calificación redondeada de {calif} a {resultado.CalificacionAjustada} ({nombrePlan})";
                    }
                }
            }
            else
            {
                // Plan 2024: Mantener decimales (hasta 2 decimales)
                resultado.CalificacionAjustada = Math.Round(calif, 2);
                if (Math.Abs(calif - resultado.CalificacionAjustada.Value) > 0.001m)
                {
                    resultado.MensajeValidacion = $"Calificación redondeada de {calif:0.00} a {resultado.CalificacionAjustada:0.00} ({nombrePlan})";
                }
                else
                {
                    resultado.MensajeValidacion = $"Calificación procesada: {resultado.CalificacionAjustada:0.00} ({nombrePlan})";
                }
            }

            // ✅ DETERMINAR ESTADO - LA CLAVE ESTÁ AQUÍ
            // 🔴 CORRECCIÓN CRÍTICA: Para Plan 2020 la calificación mínima debe ser 8.0 siempre
            decimal calificacionMinimaReal = calificacionMinima;
            if (!permiteDecimales) // Plan 2020
            {
                calificacionMinimaReal = 8.0m; // FORZAR 8.0 para Plan 2020
            }

            if (resultado.CalificacionAjustada >= calificacionMinimaReal)
            {
                resultado.EstadoFinal = "Acreditada";
                resultado.MensajeValidacion += $" - APROBADO (mínima: {calificacionMinimaReal})";
            }
            else
            {
                // ✅ LÓGICA CORREGIDA PARA EXTRAORDINARIO Y ARRASTRE
                if (estado == "Extraordinario")
                {
                    // Si ya está en extraordinario y reprueba → pasa a ARRASTRE
                    resultado.EstadoFinal = "Reprobada"; // Arrastre
                    resultado.MensajeValidacion += $" - REPROBADO en extraordinario, pasa a ARRASTRE (mínima: {calificacionMinimaReal})";
                }
                else if (estado == "Reprobada")
                {
                    // Si ya está en arrastre → se mantiene en arrastre
                    resultado.EstadoFinal = "Reprobada";
                    resultado.MensajeValidacion += $" - Sigue en ARRASTRE (mínima: {calificacionMinimaReal})";
                }
                else
                {
                    // Primera vez que reprueba → va a extraordinario
                    resultado.EstadoFinal = "Extraordinario";
                    resultado.MensajeValidacion += $" - NO ACREDITADO, pasa a extraordinario (mínima: {calificacionMinimaReal})";
                }
            }

            resultado.EsValida = true;
            return resultado;
        }

        // ✅ MÉTODO PARA OBTENER INFORMACIÓN DEL PLAN DE UNA MATERIA
        [HttpGet]
        public JsonResult ObtenerInfoPlanMateria(int idMateria)
        {
            try
            {
                var informacion = db.Database.SqlQuery<MateriaConPlanDto>(@"
            SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdPlanEstudio,
                   p.Nombre as NombrePlan, p.Año as AñoPlan, 
                   p.CalificacionMinima, p.PermiteDecimales, p.Descripcion as DescripcionPlan
            FROM Materias m
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdMateria = @p0", idMateria).FirstOrDefault();

                if (informacion == null)
                {
                    return Json(new { success = false, message = "Materia no encontrada" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    plan = new
                    {
                        id = informacion.IdPlanEstudio,
                        nombre = informacion.NombrePlan ?? "Sin plan",
                        año = informacion.AñoPlan ?? 0,
                        calificacionMinima = informacion.CalificacionMinima ?? 7.0m,
                        permiteDecimales = informacion.PermiteDecimales ?? true,
                        descripcion = informacion.DescripcionPlan ?? "",
                        tipoCalificacion = (informacion.PermiteDecimales ?? true) ? "Permite decimales" : "Solo enteros (redondea)",
                        requisitoAprobacion = $"{informacion.CalificacionMinima ?? 7.0m} puntos mínimos"
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ✅ MÉTODO CORREGIDO: GuardarTodasMaterias en MateriasAlumnoController.cs
        [HttpPost]
        public JsonResult GuardarTodasMaterias()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIANDO GUARDADO DE MATERIAS CON PLANES (VERSIÓN CORREGIDA JSON) ===");

                // ✅ LEER DATOS JSON DEL CUERPO DE LA PETICIÓN - MÉTODO CORREGIDO
                string jsonString;
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonString = reader.ReadToEnd();
                }

                if (string.IsNullOrEmpty(jsonString))
                {
                    System.Diagnostics.Debug.WriteLine("❌ No se recibieron datos JSON");
                    return Json(new { success = false, message = "No se recibieron datos para guardar" });
                }

                System.Diagnostics.Debug.WriteLine($"📨 JSON recibido: {jsonString}");

                // ✅ DESERIALIZAR JSON USANDO JavaScriptSerializer
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                List<MateriaGuardar> materias;

                try
                {
                    materias = serializer.Deserialize<List<MateriaGuardar>>(jsonString);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error deserializando JSON: {ex.Message}");
                    return Json(new { success = false, message = "Error en el formato de datos recibidos: " + ex.Message });
                }

                if (materias == null || materias.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("❌ No se pudieron deserializar las materias o la lista está vacía");
                    return Json(new { success = false, message = "No se recibieron materias válidas para guardar" });
                }

                System.Diagnostics.Debug.WriteLine($"📊 Materias deserializadas: {materias.Count}");
                foreach (var m in materias)
                {
                    System.Diagnostics.Debug.WriteLine($"   - Materia {m.IdMateria}: {m.Calificacion} → {m.Estado}");
                }

                int materiasActualizadas = 0;
                var errores = new List<string>();
                int idPersona = materias.First().IdPersona;

                // ✅ PROCESAR CADA MATERIA CON VALIDACIÓN DE PLAN
                foreach (var materia in materias)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"🔄 Procesando materia {materia.IdMateria}...");

                        // ✅ 1. OBTENER INFORMACIÓN DEL PLAN DE LA MATERIA
                        var infoMateria = db.Database.SqlQuery<MateriaConPlanDto>(@"
                    SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdPlanEstudio,
                           ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan, 
                           ISNULL(p.Año, 2024) as AñoPlan, 
                           ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima, 
                           ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
                    FROM Materias m
                    LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
                    WHERE m.IdMateria = @p0", materia.IdMateria).FirstOrDefault();

                        if (infoMateria == null)
                        {
                            errores.Add($"Materia {materia.IdMateria} no encontrada");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"📋 Plan detectado: {infoMateria.NombrePlan} - Mín: {infoMateria.CalificacionMinima}, Decimales: {infoMateria.PermiteDecimales}");

                        // 2. VALIDAR CALIFICACION y calcular intentos de forma centralizada
                        if (materia.Calificacion.HasValue)
                        {
                            var calificacion = materia.Calificacion.Value;
                            var calificacionMinima = infoMateria.CalificacionMinima ?? 7.0m;

                            // Verificar que el estado sea consistente con la calificacion y el plan
                            bool deberiaSerAprobada = calificacion >= calificacionMinima;

                            if (deberiaSerAprobada && materia.Estado != "Acreditada")
                            {
                                System.Diagnostics.Debug.WriteLine($"Corrigiendo estado: {calificacion} >= {calificacionMinima}, cambiando a Acreditada");
                                materia.Estado = "Acreditada";
                            }
                            else if (!deberiaSerAprobada && materia.Estado == "Acreditada")
                            {
                                System.Diagnostics.Debug.WriteLine($"Corrigiendo estado: {calificacion} < {calificacionMinima}, cambiando a Extraordinario");
                                materia.Estado = "Extraordinario";
                            }
                        }

                        // Calcular intentos desde BD (NO confiar en el valor del frontend)
                        materia.IntentosExtraordinarios = CalcularNuevosIntentos(
                            materia.IdMateria, materia.IdPersona, materia.Estado ?? "Pendiente");

                        // 3. GUARDAR EN BASE DE DATOS
                        var existe = db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                            materia.IdMateria, materia.IdPersona
                        ).First();

                        if (existe > 0)
                        {
                            // Actualizar registro existente
                            db.Database.ExecuteSqlCommand(
                                @"UPDATE MateriasAlumno 
                          SET Calificacion = @p0, Estado = @p1, 
                              IntentosExtraordinarios = @p2,
                              FechaInicioArrastre = @p3,
                              Observaciones = @p4,
                              FechaActualizacion = GETDATE()
                          WHERE IdMateria = @p5 AND IdPersona = @p6",
                                materia.Calificacion, materia.Estado ?? "Pendiente",
                                materia.IntentosExtraordinarios, materia.FechaInicioArrastre,
                                materia.Observaciones ?? "", materia.IdMateria, materia.IdPersona
                            );
                            System.Diagnostics.Debug.WriteLine($"Actualizado: Materia {materia.IdMateria} -> {materia.Estado} ({materia.Calificacion})");
                        }
                        else
                        {
                            // Crear nuevo registro
                            db.Database.ExecuteSqlCommand(
                                @"INSERT INTO MateriasAlumno 
                          (IdMateria, IdPersona, Calificacion, Estado, IntentosExtraordinarios, FechaInicioArrastre, Observaciones)
                          VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                                materia.IdMateria, materia.IdPersona, materia.Calificacion, materia.Estado ?? "Pendiente",
                                materia.IntentosExtraordinarios, materia.FechaInicioArrastre, materia.Observaciones ?? ""
                            );
                            System.Diagnostics.Debug.WriteLine($"Creado: Materia {materia.IdMateria} -> {materia.Estado} ({materia.Calificacion})");
                        }

                        // Registrar en historial (este flujo no lo hacia antes)
                        RegistrarEnHistorial(materia.IdMateria, materia.IdPersona,
                            materia.IntentosExtraordinarios,
                            materia.Estado == "Extraordinario" ? "Extraordinario" : "Ordinario",
                            materia.Calificacion, materia.Calificacion,
                            materia.Estado == "Acreditada", materia.Observaciones ?? "");

                        materiasActualizadas++;
                    }
                    catch (Exception ex)
                    {
                        var mensajeError = $"Error en materia {materia.IdMateria}: {ex.Message}";
                        errores.Add(mensajeError);
                        System.Diagnostics.Debug.WriteLine($"❌ {mensajeError}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"📊 Resultado: {materiasActualizadas} actualizadas, {errores.Count} errores");

                // ✅ 4. VERIFICACIÓN POSTERIOR DE LÍMITES
                System.Threading.Thread.Sleep(300); // Esperar que se reflejen los cambios

                var conteoFinal = db.Database.SqlQuery<ContadorMateriasDto>(
                    @"SELECT 
                COUNT(CASE WHEN Estado = 'Reprobada' THEN 1 END) as Reprobadas,
                COUNT(CASE WHEN Estado = 'Extraordinario' THEN 1 END) as Extraordinario
              FROM MateriasAlumno 
              WHERE IdPersona = @p0", idPersona).FirstOrDefault();

                bool excedioArrastre = (conteoFinal?.Reprobadas ?? 0) >= 4;
                bool excedioExtraordinarios = (conteoFinal?.Extraordinario ?? 0) >= 4;
                bool dadoDeBaja = excedioArrastre || excedioExtraordinarios;

                // ✅ 5. GENERAR RESPUESTA
                string mensaje;
                if (errores.Count > 0)
                {
                    mensaje = $"Se procesaron {materiasActualizadas} materias, pero hubo errores: {string.Join("; ", errores)}";
                    return Json(new
                    {
                        success = false,
                        message = mensaje,
                        materiasActualizadas = materiasActualizadas,
                        errores = errores
                    });
                }

                if (dadoDeBaja)
                {
                    var motivoBaja = "";
                    if (excedioArrastre && excedioExtraordinarios)
                    {
                        motivoBaja = $"Excedió límites de arrastre ({conteoFinal.Reprobadas}/3) y extraordinario ({conteoFinal.Extraordinario}/3)";
                    }
                    else if (excedioArrastre)
                    {
                        motivoBaja = $"Excedió límite de arrastre ({conteoFinal.Reprobadas}/3)";
                    }
                    else if (excedioExtraordinarios)
                    {
                        motivoBaja = $"Excedió límite de extraordinario ({conteoFinal.Extraordinario}/3)";
                    }

                    mensaje = $"Se guardaron {materiasActualizadas} materias. El sistema detectó exceso de límites automáticamente.";

                    System.Diagnostics.Debug.WriteLine($"🚨 BAJA DETECTADA: {motivoBaja}");

                    return Json(new
                    {
                        success = true,
                        message = mensaje,
                        alumnoReprobado = true,
                        motivoReprobacion = motivoBaja,
                        limitesFinales = new
                        {
                            materiasReprobadas = conteoFinal.Reprobadas,
                            materiasExtraordinario = conteoFinal.Extraordinario
                        },
                        requiereRecarga = true
                    });
                }

                mensaje = $"Se actualizaron {materiasActualizadas} materias correctamente con lógica de planes de estudio";
                System.Diagnostics.Debug.WriteLine($"✅ ÉXITO: {mensaje}");

                return Json(new
                {
                    success = true,
                    message = mensaje,
                    alumnoReprobado = false,
                    limitesFinales = new
                    {
                        materiasReprobadas = conteoFinal?.Reprobadas ?? 0,
                        materiasExtraordinario = conteoFinal?.Extraordinario ?? 0
                    },
                    advertencias = new
                    {
                        enElLimiteArrastre = (conteoFinal?.Reprobadas ?? 0) == 3,
                        enElLimiteExtraordinarios = (conteoFinal?.Extraordinario ?? 0) == 3
                    },
                    requiereRecarga = false
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR GENERAL: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ STACK TRACE: {ex.StackTrace}");

                return Json(new
                {
                    success = false,
                    message = "Error general al guardar: " + ex.Message,
                    detalleError = ex.StackTrace
                });
            }
        }

        // ✅ CLASE DTO AUXILIAR PARA CONTEO (mantener esta definición si no existe)
        public class ContadorMateriasDto
        {
            public int Reprobadas { get; set; }
            public int Extraordinario { get; set; }
        }

        [HttpGet]
        public JsonResult ObtenerHistorialMateria(int idPersona, int idMateria)
        {
            try
            {
                var historial = db.Database.SqlQuery<IntentoMateriaDto>(
                    @"EXEC sp_ObtenerHistorialMateriaAlumno @p0, @p1",
                    idPersona, idMateria
                ).ToList();

                if (historial == null || !historial.Any())
                {
                    return Json(new
                    {
                        success = true,
                        intentos = new List<object>(),
                        mensaje = "No hay intentos registrados para esta materia"
                    }, JsonRequestBehavior.AllowGet);
                }

                var intentos = historial.Select(h => new
                {
                    intento = h.Intento,
                    tipoIntento = h.TipoIntento,
                    calificacionOriginal = h.CalificacionOriginal,
                    calificacionAjustada = h.CalificacionAjustada,
                    esAprobatoria = h.EsAprobatoria,
                    fechaRegistro = h.FechaRegistro.ToString("dd/MM/yyyy HH:mm"),
                    estado = h.Estado,
                    observaciones = h.Observaciones ?? "",
                    labelClass = h.EsAprobatoria ? "label-success" : "label-danger",
                    iconoResultado = h.EsAprobatoria ? "glyphicon-ok-circle" : "glyphicon-remove-circle",
                    textoResultado = h.EsAprobatoria ? "APROBADO" : "REPROBADO",
                    calificacionMinimaPlan = h.CalificacionMinimaPlan,
                    permiteDecimales = h.PermiteDecimales
                }).ToList();

                return Json(new
                {
                    success = true,
                    intentos = intentos,
                    totalIntentos = intentos.Count,
                    fueAprobada = intentos.Any(i => i.esAprobatoria),
                    formaAprobacion = intentos.FirstOrDefault(i => i.esAprobatoria)?.tipoIntento ?? "No aprobada"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    mensaje = "Error al obtener historial: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================================================
        // CLASE DTO PARA EL HISTORIAL
        // ============================================================

        public class IntentoMateriaDto
        {
            public int Intento { get; set; }
            public string TipoIntento { get; set; }
            public decimal CalificacionOriginal { get; set; }
            public decimal CalificacionAjustada { get; set; }
            public bool EsAprobatoria { get; set; }
            public DateTime FechaRegistro { get; set; }
            public string Estado { get; set; }
            public string Observaciones { get; set; }
            public decimal CalificacionMinimaPlan { get; set; }
            public bool PermiteDecimales { get; set; }
        }

        // MÉTODO PARA ACTUALIZAR OBSERVACIONES ESPECÍFICAMENTE

        [HttpPost]
        public JsonResult ActualizarObservaciones(int idMateria, int idPersona, string observaciones)
        {
            try
            {
                db.Database.ExecuteSqlCommand(
                    @"UPDATE MateriasAlumno 
                      SET Observaciones = @p0, FechaActualizacion = GETDATE()
                      WHERE IdMateria = @p1 AND IdPersona = @p2",
                    observaciones ?? "", idMateria, idPersona
                );

                return Json(new
                {
                    success = true,
                    message = "Observaciones actualizadas correctamente"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al actualizar observaciones: " + ex.Message
                });
            }
        }

        // MÉTODO PARA OBTENER FECHAS CALCULADAS

        [HttpPost]
        public JsonResult ObtenerFechasCalculadas(DateTime? fechaInicio = null)
        {
            try
            {
                var fechaInicioFinal = fechaInicio ?? DateTime.Now;
                var fechaLimite = fechaInicioFinal.AddMonths(8); // 8 meses para límite de arrastre
                var diasRestantes = Math.Max(0, (fechaLimite - DateTime.Now).Days);

                return Json(new
                {
                    success = true,
                    fechaInicio = fechaInicioFinal.ToString("yyyy-MM-dd"),
                    fechaLimite = fechaLimite.ToString("yyyy-MM-dd"),
                    fechaLimiteDisplay = fechaLimite.ToString("dd/MM/yyyy"),
                    diasRestantes = diasRestantes,
                    estadoTiempo = diasRestantes > 60 ? "En tiempo" :
                                   diasRestantes > 30 ? "En riesgo" :
                                   diasRestantes > 0 ? "Crítico" : "Fuera de tiempo"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al calcular fechas: " + ex.Message
                });
            }
        }

        [HttpPost]
        public ActionResult EditarCalificacionHistorialMaster(
    int idPersona,
    int idMateria,
    int numeroIntento,
    decimal nuevaCalificacion,
    string observaciones = "")
        {
            try
            {
                // ✅ VERIFICAR SESIÓN DE USUARIO
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "Sesión expirada. Por favor, inicie sesión nuevamente."
                    });
                }

                // ✅ VERIFICAR QUE SEA USUARIO MASTER (Nivel 4)
                if (usuario.IdNivel != 4)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "⛔ Acceso denegado. Solo el usuario Master puede editar calificaciones del historial."
                    });
                }

                // ✅ VALIDAR PARÁMETROS
                if (idPersona <= 0 || idMateria <= 0 || numeroIntento <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "Parámetros inválidos"
                    });
                }

                if (nuevaCalificacion < 0 || nuevaCalificacion > 10)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "La calificación debe estar entre 0 y 10"
                    });
                }

                // ✅ EJECUTAR STORED PROCEDURE
                var resultado = db.Database.SqlQuery<ResultadoEdicionMaster>(
                    @"EXEC sp_EditarCalificacionHistorialMaster 
                @IdPersona = @p0,
                @IdMateria = @p1,
                @NumeroIntento = @p2,
                @NuevaCalificacion = @p3,
                @Observaciones = @p4",
                    idPersona,
                    idMateria,
                    numeroIntento,
                    nuevaCalificacion,
                    observaciones
                ).FirstOrDefault();

                if (resultado == null)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "No se pudo ejecutar la operación"
                    });
                }

                if (resultado.Success == 0)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = resultado.Mensaje
                    });
                }

                // ✅ RECALCULAR PROMEDIO DEL ALUMNO
                try
                {
                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_CalcularYGuardarPromedioAlumno @IdPersona = @p0",
                        idPersona
                    );
                }
                catch (Exception exPromedio)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al recalcular promedio: {exPromedio.Message}");
                    // No detener la operación si falla el cálculo del promedio
                }

                // ✅ RETORNAR ÉXITO
                return Json(new
                {
                    success = true,
                    mensaje = resultado.Mensaje,
                    numeroIntento = resultado.NumeroIntento,
                    calificacionAjustada = resultado.CalificacionAjustada,
                    esAprobatoria = resultado.EsAprobatoria,
                    nuevoEstado = resultado.NuevoEstado
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en EditarCalificacionHistorialMaster: {ex.Message}");
                return Json(new
                {
                    success = false,
                    mensaje = "Error al procesar la solicitud: " + ex.Message
                });
            }
        }

        // ====================================================================
        // AGREGAR ESTA CLASE DTO AL FINAL DEL ARCHIVO (región DTOs)
        // ====================================================================
        public class ResultadoEdicionMaster
        {
            public int Success { get; set; }
            public string Mensaje { get; set; }
            public int NumeroIntento { get; set; }
            public decimal CalificacionAjustada { get; set; }
            public bool EsAprobatoria { get; set; }
            public string NuevoEstado { get; set; }
        }



        [HttpPost]
        public JsonResult ActualizarFechaInicioArrastre(int idMateria, int idPersona, DateTime nuevaFechaInicio)
        {
            // PASO 1: SEGURIDAD (Se mantiene igual)
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null || usuario.IdNivel != 4) // Nivel 4 = Master
            {
                return Json(new { success = false, message = "Acceso denegado. Solo un Master puede cambiar esta fecha." });
            }

            try
            {
                // ====================================================================
                // ✅ INICIO DE LA CORRECCIÓN
                // ====================================================================

                // PASO 2: EJECUTAR UNA ACTUALIZACIÓN SQL DIRECTA Y SIMPLE
                // Esto evita la lógica de negocio compleja del SP y
                // los problemas de seguimiento de entidades de EF.
                int rowsAffected = db.Database.ExecuteSqlCommand(
                    @"UPDATE MateriasAlumno 
              SET FechaInicioArrastre = @p0, FechaActualizacion = GETDATE()
              WHERE IdMateria = @p1 AND IdPersona = @p2 AND Estado = 'Reprobada'",
                    nuevaFechaInicio,
                    idMateria,
                    idPersona
                );

                if (rowsAffected == 0)
                {
                    // O la materia no se encontró, o no estaba en estado 'Reprobada'
                    return Json(new { success = false, message = "No se actualizó la materia. Verifique que esté en estado 'Reprobada'." });
                }

                // PASO 3: OBTENER LOS INTENTOS PARA LA LÓGICA DE VISUALIZACIÓN
                // Necesitamos esto para la respuesta JSON (para saber si es 'Reprobada Definitiva')
                var intentos = db.Database.SqlQuery<int>(
                    "SELECT IntentosExtraordinarios FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona
                ).FirstOrDefault();

                // ====================================================================
                // ✅ FIN DE LA CORRECCIÓN
                // ====================================================================

                // PASO 4: RECALCULAR Y DEVOLVER NUEVOS DATOS
                DateTime fechaLimite = nuevaFechaInicio.AddMonths(8);
                int diasRestantes = (fechaLimite - DateTime.Now).Days;

                // Usamos el valor 'intentos' que acabamos de consultar
                bool esReprobadaDefinitiva = intentos >= 3;

                string estadoTiempo;
                string estadoLabel;
                string icono;

                if (esReprobadaDefinitiva)
                {
                    estadoTiempo = "SIN TIEMPO";
                    estadoLabel = "label-danger";
                    icono = "glyphicon-ban-circle";
                }
                else if (diasRestantes <= 0)
                {
                    estadoTiempo = $"VENCIDO ({Math.Abs(diasRestantes)} días)";
                    estadoLabel = "label-danger";
                    icono = "glyphicon-remove";
                }
                else if (diasRestantes <= 30)
                {
                    estadoTiempo = $"CRÍTICO: {diasRestantes} días";
                    estadoLabel = "label-danger";
                    icono = "glyphicon-fire";
                }
                else if (diasRestantes <= 60)
                {
                    estadoTiempo = $"ALERTA: {diasRestantes} días";
                    estadoLabel = "label-warning";
                    icono = "glyphicon-warning-sign";
                }
                else
                {
                    estadoTiempo = $"{diasRestantes} días restantes";
                    estadoLabel = "label-info";
                    icono = "glyphicon-time";
                }

                return Json(new
                {
                    success = true,
                    nuevaFechaLimiteDisplay = fechaLimite.ToString("dd/MM/yyyy"),
                    nuevoEstadoTiempo = estadoTiempo,
                    nuevoEstadoLabel = estadoLabel,
                    nuevoIcono = icono
                });
            }
            catch (Exception ex)
            {
                // Este catch capturará el error de SQL si la restricción CHECK falla
                System.Diagnostics.Debug.WriteLine($"❌ Error en ActualizarFechaInicioArrastre (SQL Directo): {ex.Message}");
                // Mostrar la InnerException si existe
                string innerExceptionMessage = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Error al ejecutar SQL: " + innerExceptionMessage });
            }
        }

        // MÉTODO: VISTA DE ARRASTRE POR GRUPO 

        public ActionResult ArrastrePorGrupo(int? id, int? idCarrera, int? idGrado, int? idPeriodo, int? año)
        {
            try
            {
                // PASO CRÍTICO: Obtener usuario y cargar dropdown
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return RedirectToAction("Login", "Home");
                }

                // El parámetro 'id' es el IdTutoriaGrupal
                int? idTutoriaGrupal = id;

                // Obtener información del grupo desde TutoriaGrupal si se pasa id
                TutoriaGrupal grupoSeleccionado = null;
                if (idTutoriaGrupal.HasValue && idTutoriaGrupal.Value > 0)
                {
                    grupoSeleccionado = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == idTutoriaGrupal.Value);
                    if (grupoSeleccionado != null)
                    {
                        // Usar los datos del grupo seleccionado
                        idCarrera = grupoSeleccionado.IdCarrera;
                        idGrado = grupoSeleccionado.IdGrado;
                        idPeriodo = grupoSeleccionado.IdPeriodo;
                        año = grupoSeleccionado.Año;
                    }
                }

                // Pasar el idTutoriaGrupal para marcarlo como seleccionado
                CargarDropdownGrupos(usuario, idTutoriaGrupal);

                // Usar valores por defecto si no se proporcionan
                int carreraId = idCarrera ?? usuario.IdCarrera;
                int gradoId = idGrado ?? 1;
                int periodoActual = idPeriodo ?? ObtenerPeriodoActual();
                int añoActual = año ?? DateTime.Now.Year;

                // Si tenemos grupo seleccionado, usar sus datos reales
                if (grupoSeleccionado != null)
                {
                    carreraId = grupoSeleccionado.IdCarrera;
                    gradoId = grupoSeleccionado.IdGrado;
                    periodoActual = grupoSeleccionado.IdPeriodo;
                    añoActual = grupoSeleccionado.Año;
                }

                // OBTENER INFORMACIÓN DEL GRUPO
                var idGrupoFiltro = (grupoSeleccionado != null) ? grupoSeleccionado.IdGrupo : gradoId;

                var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == idGrupoFiltro);
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId);
                var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == gradoId);

                // OBTENER ESPECIALIDAD
                string nombreEspecialidad = "";
                try
                {
                    var especialidadId = db.Database.SqlQuery<int?>(
                        @"SELECT TOP 1 m.IdEspecialidad 
                  FROM Materias m 
                  WHERE m.IdCarrera = @p0 AND m.IdGrado = @p1",
                        carreraId, gradoId).FirstOrDefault();

                    if (especialidadId.HasValue)
                    {
                        var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == especialidadId.Value);
                        nombreEspecialidad = especialidad?.Nombre ?? "";
                    }
                }
                catch
                {
                    nombreEspecialidad = "";
                }

                // ASIGNAR A VIEWBAG INCLUYENDO ESPECIALIDAD
                ViewBag.NombreGrupo = grupo?.Nombre ?? "Grupo " + (grupoSeleccionado?.IdGrupo ?? gradoId);
                ViewBag.NombreCarrera = carrera?.Nombre ?? "Carrera " + carreraId;
                ViewBag.NombreGrado = grado?.Nombre ?? "Grado " + gradoId;
                ViewBag.NombreEspecialidad = nombreEspecialidad;
                ViewBag.Año = añoActual;

                // Usar los IDs reales del grupo seleccionado
                ViewBag.IdGrupo = grupoSeleccionado?.IdGrupo ?? gradoId;
                ViewBag.IdCarrera = carreraId;
                ViewBag.IdGrado = gradoId;
                ViewBag.IdPeriodo = periodoActual;
                ViewBag.IdTutoriaGrupal = idTutoriaGrupal ?? 1;

                // SIN FILTRO m.Activo = 1
                var materiasArrastreRaw = new List<ArrastreRawDto>();

                if (grupoSeleccionado != null)
                {
                    materiasArrastreRaw = db.Database.SqlQuery<ArrastreRawDto>(@"
    SELECT DISTINCT
        dp.IdPersona,
        ma.IdMateria,
        dp.Matricula,
        dp.Nombre AS NombreAlumno,
        m.Nombre AS NombreMateria,
        ISNULL(ma.IntentosExtraordinarios, 0) AS IntentosExtraordinarios,
        ma.FechaInicioArrastre,
        ISNULL(ma.Observaciones, '') AS Observaciones,
        g.Nombre AS NombreGrado,
        gr.Nombre AS NombreGrupo,
        m.IdGrado AS CuatrimestreMateria,
        ma.Estado,  -- ✅ AGREGAR
        m.Activo as MateriaEstaActiva,
        CASE WHEN m.Activo = 1 THEN 'Materia Activa' ELSE 'Materia Desactivada' END as EstadoMateria
    FROM DatosPersonales dp
    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
    INNER JOIN Gradoes g ON dp.IdGrado = g.IdGrado
    INNER JOIN Grupoes gr ON dp.IdGrupo = gr.IdGrupo
    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
    WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
      AND dp.IdGrupo = @p0
      AND dp.IdCarrera = @p1
      AND dp.IdGrado = @p2
      AND dp.IdTurno = @p3
      AND dp.IdPeriodo = @p4
      AND dp.Año = @p5
    ORDER BY m.Activo DESC, dp.Nombre ASC",
                        grupoSeleccionado.IdGrupo,
                        grupoSeleccionado.IdCarrera,
                        grupoSeleccionado.IdGrado,
                        grupoSeleccionado.IdTurno,
                        grupoSeleccionado.IdPeriodo,
                        grupoSeleccionado.Año).ToList();
                }

                // PROCESAR DATOS CON MAPEO
                var materiasArrastre = new List<ArrastreGrupoDto>();

                foreach (var item in materiasArrastreRaw)
                {
                    // Calcular días en arrastre
                    int diasEnArrastre = 0;
                    DateTime? fechaLimite = null;
                    int diasRestantes = 0;
                    string estadoTiempo = "Pendiente";

                    if (item.FechaInicioArrastre.HasValue)
                    {
                        diasEnArrastre = (DateTime.Now - item.FechaInicioArrastre.Value).Days;
                        fechaLimite = item.FechaInicioArrastre.Value.AddMonths(8);
                        diasRestantes = (fechaLimite.Value - DateTime.Now).Days;

                        if (diasRestantes <= 0)
                            estadoTiempo = "Fuera de Tiempo";
                        else if (diasRestantes <= 30)
                            estadoTiempo = "Crítico";
                        else if (diasRestantes <= 60)
                            estadoTiempo = "En Riesgo";
                        else
                            estadoTiempo = "En Tiempo";
                    }

                    // CLASIFICACIÓN POR CRITICIDAD SEGÚN CUATRIMESTRE
                    int nivelCriticidad;
                    string clasificacionVisual;
                    string descripcionCriticidad;
                    string cuatrimestreTexto;

                    int cuatrimestre = item.CuatrimestreMateria ?? 1;

                    switch (cuatrimestre)
                    {
                        case 1:
                            nivelCriticidad = 1;
                            clasificacionVisual = "danger";
                            descripcionCriticidad = "Crítica Alta";
                            cuatrimestreTexto = "1er Cuatrimestre";
                            break;
                        case 2:
                            nivelCriticidad = 2;
                            clasificacionVisual = "danger";
                            descripcionCriticidad = "Crítica Alta";
                            cuatrimestreTexto = "2do Cuatrimestre";
                            break;
                        case 3:
                            nivelCriticidad = 3;
                            clasificacionVisual = "warning";
                            descripcionCriticidad = "Crítica Media";
                            cuatrimestreTexto = "3er Cuatrimestre";
                            break;
                        default:
                            nivelCriticidad = 4;
                            clasificacionVisual = "info";
                            descripcionCriticidad = "Crítica Baja";
                            cuatrimestreTexto = cuatrimestre + "° Cuatrimestre";
                            break;
                    }

                    // ISTA CON MAPEO
                    materiasArrastre.Add(new ArrastreGrupoDto
                    {
                        IdPersona = item.IdPersona,
                        IdMateria = item.IdMateria,
                        Matricula = item.Matricula ?? "",
                        NombreAlumno = item.NombreAlumno ?? "Sin nombre",
                        GradoGrupo = (item.NombreGrado ?? "Grado") + " - " + (item.NombreGrupo ?? "Grupo"),
                        MateriaArrastre = item.NombreMateria ?? "Sin materia",
                        CuatrimestreMateria = cuatrimestre,
                        CuatrimestreTexto = cuatrimestreTexto,
                        IntentosExtraordinarios = item.IntentosExtraordinarios,
                        FechaInicioArrastre = item.FechaInicioArrastre,
                        Observaciones = item.Observaciones ?? "",

                        MateriaEstaActiva = item.MateriaEstaActiva,
                        EstadoMateria = item.EstadoMateria ?? "Materia Activa",

                        NivelCriticidad = nivelCriticidad,
                        ClasificacionVisual = clasificacionVisual,
                        DescripcionCriticidad = descripcionCriticidad,
                        DiasEnArrastre = diasEnArrastre,
                        FechaLimiteArrastre = fechaLimite,
                        DiasRestantes = diasRestantes,
                        EstadoTiempo = estadoTiempo,
                        OrdenPrioridad = nivelCriticidad * 1000 + Math.Abs((item.NombreAlumno ?? "").GetHashCode())
                    });
                }

                // ORDENAR: PRIMERO MATERIAS ACTIVAS, LUEGO POR CRITICIDAD
                var materiasOrdenadas = new List<ArrastreGrupoDto>();
                var grupos = new Dictionary<string, List<ArrastreGrupoDto>>();

                // Agrupar por estado de materia y criticidad
                foreach (var materia in materiasArrastre)
                {
                    string clave = $"{(materia.MateriaEstaActiva ? "activa" : "desactivada")}_{materia.NivelCriticidad}";
                    if (!grupos.ContainsKey(clave))
                    {
                        grupos[clave] = new List<ArrastreGrupoDto>();
                    }
                    grupos[clave].Add(materia);
                }

                // Ordenar: primero activas, luego desactivadas, ambas por criticidad
                var ordenClaves = new List<string>();
                for (int nivel = 1; nivel <= 5; nivel++)
                {
                    ordenClaves.Add($"activa_{nivel}");
                }
                for (int nivel = 1; nivel <= 5; nivel++)
                {
                    ordenClaves.Add($"desactivada_{nivel}");
                }

                foreach (var clave in ordenClaves)
                {
                    if (grupos.ContainsKey(clave))
                    {
                        var grupoOrdenado = grupos[clave].OrderBy(m => m.NombreAlumno).ToList();
                        materiasOrdenadas.AddRange(grupoOrdenado);
                    }
                }

                materiasArrastre = materiasOrdenadas;

                // CALCULAR RESUMEN ESTADÍSTICO
                var resumen = new ResumenArrastreDto();

                if (materiasArrastre.Count > 0)
                {
                    // Contar alumnos únicos
                    var alumnosUnicos = new List<int>();
                    foreach (var materia in materiasArrastre)
                    {
                        if (!alumnosUnicos.Contains(materia.IdPersona))
                        {
                            alumnosUnicos.Add(materia.IdPersona);
                        }
                    }
                    resumen.TotalAlumnosConArrastre = alumnosUnicos.Count;
                    resumen.TotalMateriasEnArrastre = materiasArrastre.Count;

                    // CONTAR POR ESTADO DE MATERIA
                    int materiasActivas = 0, materiasDesactivadas = 0;
                    int criticas1er = 0, altas2do = 0, medias3er = 0, recientes4to = 0;
                    int fueraTiempo = 0, criticasTiempo = 0, enRiesgo = 0;
                    double totalIntentos = 0, totalDias = 0;
                    int materiasConDias = 0;

                    foreach (var materia in materiasArrastre)
                    {
                        // Contar por estado de materia
                        if (materia.MateriaEstaActiva) materiasActivas++;
                        else materiasDesactivadas++;

                        // Contar por cuatrimestre
                        if (materia.CuatrimestreMateria == 1) criticas1er++;
                        else if (materia.CuatrimestreMateria == 2) altas2do++;
                        else if (materia.CuatrimestreMateria == 3) medias3er++;
                        else if (materia.CuatrimestreMateria >= 4) recientes4to++;

                        // Contar por tiempo
                        if (materia.EstadoTiempo == "Fuera de Tiempo") fueraTiempo++;
                        else if (materia.EstadoTiempo == "Crítico") criticasTiempo++;
                        else if (materia.EstadoTiempo == "En Riesgo") enRiesgo++;

                        // Sumar para promedios
                        totalIntentos += materia.IntentosExtraordinarios;
                        if (materia.DiasEnArrastre > 0)
                        {
                            totalDias += materia.DiasEnArrastre;
                            materiasConDias++;
                        }
                    }

                    resumen.MateriasActivasEnArrastre = materiasActivas;
                    resumen.MateriasDesactivadasEnArrastre = materiasDesactivadas;

                    resumen.MateriasCriticas_1er = criticas1er;
                    resumen.MateriasAltas_2do = altas2do;
                    resumen.MateriasMedias_3er = medias3er;
                    resumen.MateriasRecientes_4to_mas = recientes4to;
                    resumen.MateriasFueraDeTiempo = fueraTiempo;
                    resumen.MateriasCriticasTiempo = criticasTiempo;
                    resumen.MateriasEnRiesgo = enRiesgo;

                    // Promedios
                    if (materiasArrastre.Count > 0)
                    {
                        resumen.PromedioIntentos = totalIntentos / materiasArrastre.Count;
                        resumen.PromedioDiasEnArrastre = materiasConDias > 0 ? totalDias / materiasConDias : 0;
                    }
                }

                var alumnosCriticos = new List<ArrastreGrupoDto>();
                var alumnosMedios = new List<ArrastreGrupoDto>();
                var alumnosRecientes = new List<ArrastreGrupoDto>();

                foreach (var materia in materiasArrastre)
                {
                    if (materia.NivelCriticidad <= 2) alumnosCriticos.Add(materia);
                    else if (materia.NivelCriticidad == 3) alumnosMedios.Add(materia);
                    else if (materia.NivelCriticidad >= 4) alumnosRecientes.Add(materia);
                }

                ViewBag.AlumnosCriticos = alumnosCriticos;
                ViewBag.AlumnosMedios = alumnosMedios;
                ViewBag.AlumnosRecientes = alumnosRecientes;
                ViewBag.ResumenArrastre = resumen;

                ViewBag.HayDatos = materiasArrastre.Count > 0;
                ViewBag.FechaConsulta = DateTime.Now;

                return View(materiasArrastre);
            }
            catch (Exception ex)
            {
                // MANEJO DE ERRORES CON DROPDOWN SIEMPRE DISPONIBLE
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario != null)
                {
                    try
                    {
                        CargarDropdownGrupos(usuario, id);
                    }
                    catch
                    {
                        ViewBag.GruposDropdown = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Error al cargar grupos" }
                };
                        ViewData["selectGrupo"] = ViewBag.GruposDropdown;
                    }
                }
                else
                {
                    ViewBag.GruposDropdown = new List<SelectListItem>();
                    ViewData["selectGrupo"] = ViewBag.GruposDropdown;
                }

                ViewBag.Error = "Error al consultar materias de arrastre: " + ex.Message;
                ViewBag.NombreGrupo = "Error";
                ViewBag.NombreCarrera = "Error";
                ViewBag.NombreGrado = "Error";
                ViewBag.NombreEspecialidad = "";
                ViewBag.HayDatos = false;
                ViewBag.IdTutoriaGrupal = id ?? 1;

                System.Diagnostics.Debug.WriteLine($"Error en ArrastrePorGrupo: {ex.Message}");
                return View(new List<ArrastreGrupoDto>());
            }
        }

        // MÉTODO PARA EXPORTAR ARRASTRE A EXCEL
        public ActionResult ExportarArrastreExcel(int? idGrupo, int? idCarrera, int? idGrado, bool? soloCriticos = false)
        {
            try
            {
                // Validar usuario
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Usuario no encontrado en sesión");
                    return RedirectToAction("Login", "Account");
                }

                // Parámetros
                int grupoId = idGrupo ?? 1;
                int carreraId = idCarrera ?? usuario.IdCarrera;
                int gradoId = idGrado ?? 1;

                System.Diagnostics.Debug.WriteLine($"✅ EXPORTACIÓN CORREGIDA - Grupo={grupoId}, Carrera={carreraId}, Grado={gradoId}");

                // ✅ CONSULTA CORREGIDA - INCLUYE ESTADO PARA DIFERENCIAR ARRASTRE DE EXTRAORDINARIO
                var materiasArrastreRaw = db.Database.SqlQuery<ArrastreRawDto>(@"
            SELECT 
                dp.IdPersona,
                ma.IdMateria,
                dp.Matricula,
                dp.Nombre AS NombreAlumno,
                m.Nombre AS NombreMateria,
                ISNULL(ma.IntentosExtraordinarios, 0) AS IntentosExtraordinarios,
                ma.FechaInicioArrastre,
                ma.FechaExamenExtraordinario,
                ISNULL(ma.Observaciones, '') AS Observaciones,
                g.Nombre AS NombreGrado,
                gr.Nombre AS NombreGrupo,
                m.IdGrado AS CuatrimestreMateria,
                ma.Estado,
                m.Activo as MateriaEstaActiva,
                CASE WHEN m.Activo = 1 THEN 'Materia Activa' ELSE 'Materia Desactivada' END as EstadoMateria
            FROM DatosPersonales dp
            INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
            INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
            INNER JOIN Gradoes g ON dp.IdGrado = g.IdGrado
            INNER JOIN Grupoes gr ON dp.IdGrupo = gr.IdGrupo
            LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
            WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
              AND dp.IdGrupo = @p0
              AND dp.IdCarrera = @p1
              AND dp.IdGrado = @p2
            ORDER BY
                CASE WHEN ma.Estado = 'Reprobada' THEN 1 ELSE 2 END,
                m.Activo DESC,
                dp.Nombre ASC",
                    grupoId, carreraId, gradoId).ToList();

                System.Diagnostics.Debug.WriteLine($"✅ Materias encontradas: {materiasArrastreRaw.Count}");

                // Contar por estado y tipo
                var materiasActivas = materiasArrastreRaw.Where(m => m.MateriaEstaActiva).ToList();
                var materiasDesactivadas = materiasArrastreRaw.Where(m => !m.MateriaEstaActiva).ToList();
                var materiasArrastre = materiasArrastreRaw.Where(m => m.Estado == "Reprobada").ToList();
                var materiasExtraordinario = materiasArrastreRaw.Where(m => m.Estado == "Extraordinario").ToList();

                System.Diagnostics.Debug.WriteLine($"✅ Activas: {materiasActivas.Count}, Desactivadas: {materiasDesactivadas.Count}");
                System.Diagnostics.Debug.WriteLine($"✅ Arrastre: {materiasArrastre.Count}, Extraordinario: {materiasExtraordinario.Count}");

                if (!materiasArrastreRaw.Any())
                {
                    // Excel vacío con mensaje
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add("SIN DATOS");
                        worksheet.Cells[1, 1].Value = "NO SE ENCONTRARON MATERIAS EN ARRASTRE O EXTRAORDINARIO PARA ESTE GRUPO";
                        worksheet.Cells[1, 1].Style.Font.Bold = true;
                        worksheet.Cells[1, 1].Style.Font.Size = 14;

                        var excelBytes = package.GetAsByteArray();
                        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"SINDATOS_G{grupoId}C{carreraId}GD{gradoId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                    }
                }

                // Obtener información del grupo
                string nombreGrupo = "GRUPO " + grupoId;
                string nombreCarrera = "CARRERA " + carreraId;
                string nombreGrado = "GRADO " + gradoId;
                string nombreEspecialidad = "";

                try
                {
                    var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == grupoId);
                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId);
                    var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == gradoId);

                    if (grupo != null) nombreGrupo = grupo.Nombre.ToUpper();
                    if (carrera != null) nombreCarrera = carrera.Nombre.ToUpper();
                    if (grado != null) nombreGrado = grado.Nombre.ToUpper();

                    var especialidadId = db.Database.SqlQuery<int?>(
                        @"SELECT TOP 1 m.IdEspecialidad 
                  FROM Materias m 
                  WHERE m.IdCarrera = @p0 AND m.IdGrado = @p1",
                        carreraId, gradoId).FirstOrDefault();

                    if (especialidadId.HasValue)
                    {
                        var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == especialidadId.Value);
                        nombreEspecialidad = especialidad?.Nombre?.ToUpper() ?? "";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error obteniendo nombres: {ex.Message}");
                }

                // ✅ CREAR EXCEL CORREGIDO - TODO EN MAYÚSCULAS
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("MATERIAS EN ARRASTRE");

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // TÍTULO PRINCIPAL
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    worksheet.Cells[1, 1].Value = $"REPORTE DE MATERIAS EN ARRASTRE Y EXTRAORDINARIO - {nombreGrupo}";
                    worksheet.Cells[1, 1, 1, 12].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 18;
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // INFORMACIÓN DEL GRUPO
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    string infoGrupo = nombreCarrera;
                    if (!string.IsNullOrEmpty(nombreEspecialidad))
                    {
                        infoGrupo += " - " + nombreEspecialidad;
                    }
                    infoGrupo += " - " + nombreGrado;

                    worksheet.Cells[2, 1].Value = infoGrupo;
                    worksheet.Cells[2, 1, 2, 12].Merge = true;
                    worksheet.Cells[2, 1].Style.Font.Size = 14;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(31, 73, 125));

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // FECHA Y FILTROS APLICADOS
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    string filtrosTexto = $"GENERADO EL {DateTime.Now:dd/MM/yyyy} A LAS {DateTime.Now:HH:mm:ss} | USUARIO: {usuario.NombreCompleto.ToUpper()}";
                    if (soloCriticos == true)
                        filtrosTexto += " - SOLO MATERIAS CRÍTICAS";

                    worksheet.Cells[3, 1].Value = filtrosTexto;
                    worksheet.Cells[3, 1, 3, 12].Merge = true;
                    worksheet.Cells[3, 1].Style.Font.Size = 11;
                    worksheet.Cells[3, 1].Style.Font.Italic = true;
                    worksheet.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[3, 1].Style.Font.Color.SetColor(Color.Gray);

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // RESUMEN ESTADÍSTICO
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    int filaStats = 5;
                    var totalAlumnos = materiasArrastreRaw.Select(m => m.IdPersona).Distinct().Count();
                    var totalMaterias = materiasArrastreRaw.Count;

                    worksheet.Cells[filaStats, 1].Value = "RESUMEN";
                    worksheet.Cells[filaStats, 1, filaStats, 6].Merge = true;
                    worksheet.Cells[filaStats, 1].Style.Font.Bold = true;
                    worksheet.Cells[filaStats, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[filaStats, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 230, 241));
                    worksheet.Cells[filaStats, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaStats++;

                    // Primera línea de estadísticas
                    worksheet.Cells[filaStats, 1].Value = "TOTAL ALUMNOS:";
                    worksheet.Cells[filaStats, 2].Value = totalAlumnos;
                    worksheet.Cells[filaStats, 3].Value = "TOTAL MATERIAS:";
                    worksheet.Cells[filaStats, 4].Value = totalMaterias;
                    worksheet.Cells[filaStats, 5].Value = "MAT. ACTIVAS:";
                    worksheet.Cells[filaStats, 6].Value = materiasActivas.Count;
                    worksheet.Cells[filaStats, 6].Style.Font.Color.SetColor(Color.Green);
                    filaStats++;

                    // Segunda línea: Tipo de problema
                    worksheet.Cells[filaStats, 1].Value = "📌 ARRASTRE:";
                    worksheet.Cells[filaStats, 2].Value = materiasArrastre.Count;
                    worksheet.Cells[filaStats, 2].Style.Font.Color.SetColor(Color.Blue);
                    worksheet.Cells[filaStats, 2].Style.Font.Bold = true;

                    worksheet.Cells[filaStats, 3].Value = "⏱ EXTRAORDINARIO:";
                    worksheet.Cells[filaStats, 4].Value = materiasExtraordinario.Count;
                    worksheet.Cells[filaStats, 4].Style.Font.Color.SetColor(Color.DarkCyan);
                    worksheet.Cells[filaStats, 4].Style.Font.Bold = true;

                    worksheet.Cells[filaStats, 5].Value = "MAT. DESACT.:";
                    worksheet.Cells[filaStats, 6].Value = materiasDesactivadas.Count;
                    worksheet.Cells[filaStats, 6].Style.Font.Color.SetColor(Color.Orange);

                    filaStats += 2;

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // ✅ ENCABEZADOS CORREGIDOS (IGUAL QUE LA VISTA)
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    string[] encabezados = {
                "MATRÍCULA",
                "NOMBRE DEL ALUMNO",
                "MATERIA",
                "CUATRIM.",
                "TIPO",
                "INTENTOS",
                "ESTADO / PERIODO",
                "TIEMPO",
                "ESTADO MATERIA",
                "GRADO-GRUPO",
                "CRITICIDAD",
                "OBSERVACIONES"
            };

                    for (int i = 0; i < encabezados.Length; i++)
                    {
                        var celda = worksheet.Cells[filaStats, i + 1];
                        celda.Value = encabezados[i];
                        celda.Style.Font.Bold = true;
                        celda.Style.Font.Color.SetColor(Color.White);
                        celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 84, 106));
                        celda.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        celda.Style.WrapText = true;
                    }

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // ✅ DATOS CON LÓGICA DIFERENCIADA ARRASTRE VS EXTRAORDINARIO
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    int fila = filaStats + 1;
                    var materiasOrdenadas = materiasArrastreRaw
                        .OrderBy(m => m.Estado == "Extraordinario" ? 0 : 1)
                        .ThenBy(m => m.MateriaEstaActiva ? 0 : 1)
                        .ThenBy(m => m.CuatrimestreMateria)
                        .ThenBy(m => m.NombreAlumno);

                    foreach (var item in materiasOrdenadas)
                    {
                        bool esExtraordinario = item.Estado == "Extraordinario";
                        int cuatrimestre = item.CuatrimestreMateria ?? 1;
                        string cuatrimestreTexto = $"{cuatrimestre}°";

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 1: MATRÍCULA
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 1].Value = (item.Matricula ?? "").ToUpper();

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 2: NOMBRE DEL ALUMNO
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 2].Value = (item.NombreAlumno ?? "").ToUpper();

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 3: MATERIA
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 3].Value = (item.NombreMateria ?? "").ToUpper();

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 4: CUATRIMESTRE
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 4].Value = cuatrimestreTexto;
                        worksheet.Cells[fila, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // ✅ COLUMNA 5: TIPO (NUEVA - IGUAL QUE LA VISTA)
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        if (esExtraordinario)
                        {
                            worksheet.Cells[fila, 5].Value = "EXTRA";
                            worksheet.Cells[fila, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[fila, 5].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 237, 247));
                            worksheet.Cells[fila, 5].Style.Font.Color.SetColor(Color.FromArgb(12, 84, 96));
                        }
                        else
                        {
                            worksheet.Cells[fila, 5].Value = "ARRASTRE";
                            worksheet.Cells[fila, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[fila, 5].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(209, 236, 241));
                            worksheet.Cells[fila, 5].Style.Font.Color.SetColor(Color.FromArgb(31, 73, 125));
                        }
                        worksheet.Cells[fila, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[fila, 5].Style.Font.Bold = true;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 6: INTENTOS
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 6].Value = item.IntentosExtraordinarios;
                        worksheet.Cells[fila, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // ✅ COLUMNA 7: ESTADO / PERIODO (DIFERENCIADA)
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        string estadoPeriodoTexto;
                        if (esExtraordinario)
                        {
                            estadoPeriodoTexto = "CUATRIMESTRE EN CURSO PARA PRESENTAR EXAMEN";
                            worksheet.Cells[fila, 7].Style.Font.Color.SetColor(Color.FromArgb(12, 84, 96));
                        }
                        else
                        {
                            if (item.FechaInicioArrastre.HasValue)
                            {
                                var fechaLimite = item.FechaInicioArrastre.Value.AddMonths(8);
                                var diasRestantes = (fechaLimite - DateTime.Now).Days;

                                if (diasRestantes <= 0)
                                    estadoPeriodoTexto = "FUERA DE TIEMPO";
                                else if (diasRestantes <= 60)
                                    estadoPeriodoTexto = "CRÍTICO";
                                else if (diasRestantes <= 180)
                                    estadoPeriodoTexto = "EN RIESGO";
                                else
                                    estadoPeriodoTexto = "EN TIEMPO";
                            }
                            else
                            {
                                estadoPeriodoTexto = "SIN FECHA";
                            }
                        }
                        worksheet.Cells[fila, 7].Value = estadoPeriodoTexto;
                        worksheet.Cells[fila, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // ✅ COLUMNA 8: TIEMPO (DIFERENCIADA)
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        string tiempoTexto;
                        if (esExtraordinario)
                        {
                            if (item.FechaExamenExtraordinario.HasValue)
                            {
                                tiempoTexto = $"EXAMEN: {item.FechaExamenExtraordinario.Value:dd/MM/yyyy}";
                            }
                            else
                            {
                                tiempoTexto = "FECHA POR DEFINIR";
                            }
                            worksheet.Cells[fila, 8].Style.Font.Color.SetColor(Color.FromArgb(12, 84, 96));
                        }
                        else
                        {
                            if (item.FechaInicioArrastre.HasValue)
                            {
                                var fechaLimite = item.FechaInicioArrastre.Value.AddMonths(8);
                                var diasRestantes = (fechaLimite - DateTime.Now).Days;

                                if (diasRestantes <= 0)
                                {
                                    tiempoTexto = $"VENCIDO ({Math.Abs(diasRestantes)} DÍAS)";
                                    worksheet.Cells[fila, 8].Style.Font.Color.SetColor(Color.Red);
                                }
                                else
                                {
                                    tiempoTexto = $"{diasRestantes} DÍAS\nLÍMITE: {fechaLimite:dd/MM/yyyy}";

                                    if (diasRestantes <= 60)
                                        worksheet.Cells[fila, 8].Style.Font.Color.SetColor(Color.Red);
                                    else if (diasRestantes <= 180)
                                        worksheet.Cells[fila, 8].Style.Font.Color.SetColor(Color.Orange);
                                    else
                                        worksheet.Cells[fila, 8].Style.Font.Color.SetColor(Color.Green);
                                }
                            }
                            else
                            {
                                tiempoTexto = "SIN FECHA DE INICIO";
                            }
                        }
                        worksheet.Cells[fila, 8].Value = tiempoTexto;
                        worksheet.Cells[fila, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[fila, 8].Style.WrapText = true;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 9: ESTADO MATERIA
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 9].Value = (item.EstadoMateria ?? "MATERIA ACTIVA").ToUpper();
                        if (!item.MateriaEstaActiva)
                        {
                            worksheet.Cells[fila, 9].Style.Font.Color.SetColor(Color.Orange);
                            worksheet.Cells[fila, 9].Style.Font.Bold = true;
                        }
                        worksheet.Cells[fila, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 10: GRADO-GRUPO
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 10].Value = $"{(item.NombreGrado ?? "").ToUpper()} - {(item.NombreGrupo ?? "").ToUpper()}";

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 11: CRITICIDAD
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        string nivelCriticidad;
                        Color colorFondo;

                        if (esExtraordinario)
                        {
                            nivelCriticidad = "BAJA - EXTRAORDINARIO";
                            colorFondo = Color.FromArgb(217, 237, 247);
                        }
                        else
                        {
                            switch (cuatrimestre)
                            {
                                case 1:
                                case 2:
                                    nivelCriticidad = "CRÍTICA ALTA";
                                    colorFondo = Color.FromArgb(248, 215, 218);
                                    break;
                                case 3:
                                    nivelCriticidad = "CRÍTICA MEDIA";
                                    colorFondo = Color.FromArgb(255, 243, 205);
                                    break;
                                default:
                                    nivelCriticidad = "CRÍTICA BAJA";
                                    colorFondo = Color.FromArgb(217, 237, 247);
                                    break;
                            }
                        }

                        worksheet.Cells[fila, 11].Value = nivelCriticidad;
                        worksheet.Cells[fila, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // COLUMNA 12: OBSERVACIONES
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        worksheet.Cells[fila, 12].Value = (item.Observaciones ?? "").ToUpper();
                        worksheet.Cells[fila, 12].Style.WrapText = true;

                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        // APLICAR COLOR DE FONDO A TODA LA FILA
                        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                        if (!item.MateriaEstaActiva)
                        {
                            colorFondo = Color.FromArgb(255, 248, 220);
                        }

                        var rangoFila = worksheet.Cells[fila, 1, fila, 12];
                        rangoFila.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rangoFila.Style.Fill.BackgroundColor.SetColor(colorFondo);

                        if (!item.MateriaEstaActiva)
                        {
                            rangoFila.Style.Border.Left.Style = ExcelBorderStyle.Medium;
                            rangoFila.Style.Border.Left.Color.SetColor(Color.Orange);
                        }

                        fila++;
                    }

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // AJUSTAR ANCHOS DE COLUMNAS
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    worksheet.Column(1).Width = 12;
                    worksheet.Column(2).Width = 30;
                    worksheet.Column(3).Width = 35;
                    worksheet.Column(4).Width = 10;
                    worksheet.Column(5).Width = 12;
                    worksheet.Column(6).Width = 8;
                    worksheet.Column(7).Width = 25;
                    worksheet.Column(8).Width = 20;
                    worksheet.Column(9).Width = 18;
                    worksheet.Column(10).Width = 20;
                    worksheet.Column(11).Width = 18;
                    worksheet.Column(12).Width = 30;

                    fila++;
                    worksheet.Cells[fila, 1].Value = $"SISTEMA DE CONTROL ACADÉMICO - GENERADO EL {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Cells[fila, 1, fila, 12].Merge = true;
                    worksheet.Cells[fila, 1].Style.Font.Size = 9;
                    worksheet.Cells[fila, 1].Style.Font.Italic = true;
                    worksheet.Cells[fila, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    // CONFIGURACIÓN FINAL
                    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                    worksheet.View.ShowGridLines = true;
                    worksheet.PrinterSettings.Orientation = eOrientation.Landscape;

                    var excelBytes = package.GetAsByteArray();

                    string tipoReporte = soloCriticos == true ? "CRITICOS" : "COMPLETO";
                    string sufijo = materiasDesactivadas.Count > 0 ? $"_INC{materiasDesactivadas.Count}DESACT" : "";
                    sufijo += $"_ARR{materiasArrastre.Count}_EXT{materiasExtraordinario.Count}";
                    string nombreArchivo = $"ARRASTRE_{tipoReporte}_{nombreGrupo.Replace(" ", "_")}_G{grupoId}C{carreraId}GD{gradoId}{sufijo}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                    System.Diagnostics.Debug.WriteLine($"✅ Excel generado: {nombreArchivo}");

                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR EN EXPORTACIÓN: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "Error al generar el archivo Excel: " + ex.Message;
                return RedirectToAction("ArrastrePorGrupo", "ArrastreGrupo", new { id = idGrupo });
            }
        }


        // MÉTODO AUXILIAR PARA OBTENER PERÍODO ACTUAL

        private int ObtenerPeriodoActual()
        {
            try
            {
                var tiempo = DateTime.Now;
                if (tiempo.Month >= 1 && tiempo.Month <= 4)
                    return 1; // Enero-Abril = Periodo 1
                else if (tiempo.Month >= 5 && tiempo.Month <= 8)
                    return 2; // Mayo-Agosto = Periodo 2  
                else
                    return 3; // Sept-Dic = Periodo 3
            }
            catch
            {
                return 1;
            }
        }

        // MÉTODO CargarDropdownGrupos CORREGIDO EN MateriasAlumnoController.cs
        private void CargarDropdownGrupos(Usuario usuario, int? idSeleccionado = null)
        {
            try
            {
                List<TutoriaGrupal> tutorias = new List<TutoriaGrupal>();

                // CONTROL DE ACCESO SEGÚN NIVEL DE USUARIO
                switch (usuario.IdNivel)
                {
                    case 1: // ALUMNO - NO DEBE VER NADA
                        ViewBag.GruposDropdown = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Acceso denegado" }
                };
                        ViewData["selectGrupo"] = ViewBag.GruposDropdown;
                        return;

                    case 2: // TUTOR - SOLO SUS GRUPOS ASIGNADOS
                        tutorias = db.TutoriaGrupals
                            .Where(x => x.IdCarrera == usuario.IdCarrera && x.IdUsuario == usuario.IdUsuario)
                            .OrderByDescending(x => x.IdTutoriaGrupal)
                            .ToList();
                        break;

                    case 3: // COORDINADOR - TODOS LOS GRUPOS DE SU CARRERA
                    case 4: // COORDINADOR NIVEL 4 - TODOS LOS GRUPOS DE SU CARRERA
                        tutorias = db.TutoriaGrupals
                            .Where(x => x.IdCarrera == usuario.IdCarrera)
                            .OrderByDescending(x => x.IdTutoriaGrupal)
                            .ToList();
                        break;

                    default:
                        ViewBag.GruposDropdown = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Nivel no autorizado" }
                };
                        ViewData["selectGrupo"] = ViewBag.GruposDropdown;
                        return;
                }

                // VALIDAR SI HAY GRUPOS DISPONIBLES - Compatible C# 5
                if (tutorias.Count == 0)
                {
                    string mensaje = usuario.IdNivel == 2
                        ? "No tienes grupos asignados como tutor"
                        : "No hay grupos disponibles";

                    ViewBag.GruposDropdown = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = mensaje }
            };
                    ViewData["selectGrupo"] = ViewBag.GruposDropdown;
                    return;
                }

                // GENERAR NOMENCLATURA
                foreach (var item in tutorias)
                {
                    var nomenclatura = item.Carrera.Nombre.ToString() + ", " +
                                     item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " +
                                     item.Turno.Nombre.ToString() + ", " +
                                     item.Periodo.Nombre.ToString() + ", " +
                                     item.Año.ToString();
                    item.Nomenclatura = nomenclatura.ToString();
                }

                // AGREGAR ALUMNOS REMOVIDOS SOLO PARA COORDINADORES
                if (usuario.IdNivel >= 3)
                {
                    TutoriaGrupal removidos = new TutoriaGrupal();
                    removidos.IdTutoriaGrupal = -1;
                    removidos.Nomenclatura = "Alumnos Removidos";
                    tutorias.Add(removidos);
                }

                // CREAR DROPDOWN CON DEBUG MEJORADO
                var dropdownItems = new List<SelectListItem>();

                foreach (var p in tutorias)
                {
                    bool esSeleccionado = idSeleccionado.HasValue && p.IdTutoriaGrupal == idSeleccionado.Value;

                    dropdownItems.Add(new SelectListItem
                    {
                        Value = p.IdTutoriaGrupal.ToString(),
                        Text = p.Nomenclatura,
                        Selected = esSeleccionado
                    });

                    // DEBUG: Log detallado para verificar selección
                    if (esSeleccionado)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("✅ GRUPO SELECCIONADO: ID={0}, Nomenclatura={1}",
                            p.IdTutoriaGrupal, p.Nomenclatura));
                    }
                }

                ViewBag.GruposDropdown = dropdownItems;
                ViewData["selectGrupo"] = dropdownItems;

                // DEBUG ADICIONAL
                System.Diagnostics.Debug.WriteLine(string.Format("CargarDropdownGrupos - Usuario: {0} (Nivel {1})",
                    usuario.NombreCompleto, usuario.IdNivel));
                System.Diagnostics.Debug.WriteLine(string.Format("Total grupos cargados: {0}", tutorias.Count));
                System.Diagnostics.Debug.WriteLine(string.Format("ID a seleccionar: {0}", idSeleccionado ?? -999));

                var grupoSeleccionado = dropdownItems.FirstOrDefault(x => x.Selected);
                if (grupoSeleccionado != null)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Grupo marcado como seleccionado: {0}", grupoSeleccionado.Text));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠ NINGÚN GRUPO MARCADO COMO SELECCIONADO");
                }
            }
            catch (Exception ex)
            {
                var listaError = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "Error al cargar grupos: " + ex.Message }
        };

                ViewBag.GruposDropdown = listaError;
                ViewData["selectGrupo"] = listaError;

                System.Diagnostics.Debug.WriteLine(string.Format("Error en CargarDropdownGrupos (Materias): {0}", ex.Message));
            }
        }

        // MÉTODO AUXILIAR PARA VERIFICAR ACCESO AL GRUPO - ACTUALIZADO
        private bool UsuarioTieneAccesoAlGrupo(Usuario usuario, int idTutoriaGrupal)
        {
            if (usuario == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ Usuario es null en UsuarioTieneAccesoAlGrupo");
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔐 Verificando acceso usuario {usuario.NombreCompleto} (Nivel {usuario.IdNivel}) al grupo {idTutoriaGrupal}");

                // Los coordinadores tienen acceso a todos los grupos de su carrera
                if (usuario.IdNivel >= 3)
                {
                    var grupoCoordinador = db.TutoriaGrupals
                        .FirstOrDefault(x => x.IdTutoriaGrupal == idTutoriaGrupal && x.IdCarrera == usuario.IdCarrera);

                    var tieneAcceso = grupoCoordinador != null;
                    System.Diagnostics.Debug.WriteLine($"✅ COORDINADOR - Acceso al grupo: {tieneAcceso}");

                    if (tieneAcceso && grupoCoordinador != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"📋 Grupo encontrado: Carrera {grupoCoordinador.IdCarrera}, Grado {grupoCoordinador.IdGrado}");
                    }

                    return tieneAcceso;
                }

                // Los tutores solo tienen acceso a sus grupos asignados
                if (usuario.IdNivel == 2)
                {
                    var grupoTutor = db.TutoriaGrupals
                        .FirstOrDefault(x => x.IdTutoriaGrupal == idTutoriaGrupal &&
                                             x.IdUsuario == usuario.IdUsuario &&
                                             x.IdCarrera == usuario.IdCarrera);

                    var tieneAcceso = grupoTutor != null;
                    System.Diagnostics.Debug.WriteLine($"✅ TUTOR - Acceso al grupo: {tieneAcceso}");

                    if (tieneAcceso && grupoTutor != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"📋 Grupo asignado encontrado: Usuario {grupoTutor.IdUsuario}, Carrera {grupoTutor.IdCarrera}");
                    }

                    return tieneAcceso;
                }

                System.Diagnostics.Debug.WriteLine($"❌ Nivel de usuario no autorizado: {usuario.IdNivel}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error verificando acceso al grupo: {ex.Message}");
                return false;
            }
        }

        // MÉTODO AUXILIAR PARA VALIDAR ACCESO GENERAL - ACTUALIZADO
        private bool ValidarAccesoUsuario(Usuario usuario)
        {
            if (usuario == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ Usuario es null en ValidarAccesoUsuario");
                return false;
            }

            // Solo tutores (nivel 2) y coordinadores (nivel 3, 4) tienen acceso
            bool tieneAcceso = usuario.IdNivel >= 2 && usuario.IdNivel <= 4;

            System.Diagnostics.Debug.WriteLine($"🔐 ValidarAccesoUsuario - {usuario.NombreCompleto} (Nivel {usuario.IdNivel}): {(tieneAcceso ? "AUTORIZADO" : "DENEGADO")}");

            return tieneAcceso;
        }

        // MÉTODO DESCARGAR PDF

        public ActionResult DescargarPDF(int id)
        {
            try
            {
                var datosAlumno = ObtenerDatosReporte(id);

                using (MemoryStream stream = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 40, 40, 30, 30);
                    PdfWriter writer = PdfWriter.GetInstance(document, stream);
                    document.Open();

                    // FUENTES CORREGIDAS
                    var fontTitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(31, 73, 125));
                    var fontSubtitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                    var fontNormal = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                    var fontPequeña = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.GRAY);
                    var fontEstado = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12,
                        datosAlumno.AlumnoReprobado ? BaseColor.RED : new BaseColor(34, 139, 34));

                    // TÍTULO PRINCIPAL
                    var titulo = new iTextParagraph("REPORTE ACADÉMICO DEL ESTUDIANTE", fontTitulo)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 25f
                    };
                    document.Add(titulo);

                    // INFORMACIÓN DEL ALUMNO
                    var tablaAlumno = new PdfPTable(2) { WidthPercentage = 100 };
                    tablaAlumno.SetWidths(new float[] { 25f, 75f });

                    AgregarCeldaInfoPDF(tablaAlumno, "Nombre:", datosAlumno.NombreAlumno, fontSubtitulo, fontNormal);
                    AgregarCeldaInfoPDF(tablaAlumno, "Matrícula:", datosAlumno.Matricula, fontSubtitulo, fontNormal);
                    AgregarCeldaInfoPDF(tablaAlumno, "Carrera:", datosAlumno.Carrera, fontSubtitulo, fontNormal);
                    AgregarCeldaInfoPDF(tablaAlumno, "Estado Académico:",
                        datosAlumno.AlumnoReprobado ? "DADO DE BAJA ACADÉMICA" : "ACTIVO",
                        fontSubtitulo, fontEstado);

                    document.Add(tablaAlumno);

                    // RESUMEN ESTADÍSTICO
                    document.Add(new iTextParagraph("RESUMEN ESTADÍSTICO", fontSubtitulo)
                    {
                        SpacingBefore = 20f,
                        SpacingAfter = 10f
                    });

                    var tablaEstadisticas = new PdfPTable(5) { WidthPercentage = 100 };
                    tablaEstadisticas.SetWidths(new float[] { 20f, 20f, 20f, 20f, 20f });

                    // Encabezados con colores
                    AgregarCeldaHeaderPDF(tablaEstadisticas, "Total Materias", new BaseColor(52, 152, 219));
                    AgregarCeldaHeaderPDF(tablaEstadisticas, "Acreditadas", new BaseColor(39, 174, 96));
                    AgregarCeldaHeaderPDF(tablaEstadisticas, "Arrastre", new BaseColor(231, 76, 60));
                    AgregarCeldaHeaderPDF(tablaEstadisticas, "Extraordinario", new BaseColor(243, 156, 18));
                    AgregarCeldaHeaderPDF(tablaEstadisticas, "Pendientes", new BaseColor(149, 165, 166));

                    // Datos
                    int totalMaterias = datosAlumno.Materias.Count;
                    AgregarCeldaDatoPDF(tablaEstadisticas, totalMaterias.ToString(), fontNormal);
                    AgregarCeldaDatoPDF(tablaEstadisticas, datosAlumno.MateriasAcreditadas.ToString(), fontNormal);
                    AgregarCeldaDatoPDF(tablaEstadisticas, $"{datosAlumno.MateriasReprobadas}/2", fontNormal);
                    AgregarCeldaDatoPDF(tablaEstadisticas, $"{datosAlumno.MateriasExtraordinario}/3", fontNormal);
                    AgregarCeldaDatoPDF(tablaEstadisticas, datosAlumno.MateriasPendientes.ToString(), fontNormal);

                    document.Add(tablaEstadisticas);

                    // PROMEDIO CON COLOR
                    decimal promedio = CalcularPromedio(datosAlumno.Materias);
                    var promedioParrafo = new iTextParagraph($"Promedio General: {promedio:0.0}",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12,
                        promedio >= 7 ? new BaseColor(34, 139, 34) : BaseColor.RED))
                    {
                        SpacingBefore = 15f,
                        SpacingAfter = 20f,
                        Alignment = Element.ALIGN_CENTER
                    };
                    document.Add(promedioParrafo);

                    // DETALLE DE MATERIAS CON NUEVAS COLUMNAS
                    document.Add(new iTextParagraph("DETALLE DE MATERIAS", fontSubtitulo)
                    {
                        SpacingBefore = 10f,
                        SpacingAfter = 10f
                    });

                    var tablaMaterias = new PdfPTable(6) { WidthPercentage = 100 }; // Aumentada a 6 columnas
                    tablaMaterias.SetWidths(new float[] { 30f, 12f, 12f, 12f, 15f, 19f });

                    // Encabezados
                    AgregarCeldaHeaderPDF(tablaMaterias, "Materia", new BaseColor(44, 62, 80));
                    AgregarCeldaHeaderPDF(tablaMaterias, "Calificación", new BaseColor(44, 62, 80));
                    AgregarCeldaHeaderPDF(tablaMaterias, "Estado", new BaseColor(44, 62, 80));
                    AgregarCeldaHeaderPDF(tablaMaterias, "Intentos", new BaseColor(44, 62, 80));
                    AgregarCeldaHeaderPDF(tablaMaterias, "Fecha Límite", new BaseColor(44, 62, 80));
                    AgregarCeldaHeaderPDF(tablaMaterias, "Observaciones", new BaseColor(44, 62, 80));

                    // Datos de materias con colores según estado
                    foreach (var materia in datosAlumno.Materias.OrderBy(m => m.NombreMateria))
                    {
                        // Color de fondo según estado
                        BaseColor colorFondo = BaseColor.WHITE;
                        switch (materia.Estado?.ToLower())
                        {
                            case "acreditada":
                                colorFondo = new BaseColor(212, 237, 218);
                                break;
                            case "reprobada":
                                colorFondo = new BaseColor(248, 215, 218);
                                break;
                            case "extraordinario":
                                colorFondo = new BaseColor(255, 243, 205);
                                break;
                            case "pendiente":
                                colorFondo = new BaseColor(248, 249, 250);
                                break;
                        }

                        AgregarCeldaMateriaPDF(tablaMaterias, materia.NombreMateria, fontNormal, colorFondo);
                        AgregarCeldaMateriaPDF(tablaMaterias, materia.Calificacion?.ToString("0.00") ?? "N/A", fontNormal, colorFondo);
                        AgregarCeldaMateriaPDF(tablaMaterias, materia.Estado ?? "Pendiente", fontNormal, colorFondo);
                        AgregarCeldaMateriaPDF(tablaMaterias, materia.IntentosExtraordinarios > 0 ? materia.IntentosExtraordinarios.ToString() : "-", fontNormal, colorFondo);

                        // MOSTRAR FECHA LÍMITE PARA ARRASTRADAS
                        string fechaLimite = "-";
                        if (materia.Estado == "Reprobada" && materia.FechaInicioArrastre.HasValue)
                        {
                            var limite = materia.FechaInicioArrastre.Value.AddMonths(8);
                            fechaLimite = limite.ToString("dd/MM/yyyy");
                        }
                        AgregarCeldaMateriaPDF(tablaMaterias, fechaLimite, fontPequeña, colorFondo);

                        AgregarCeldaMateriaPDF(tablaMaterias, !string.IsNullOrEmpty(materia.Observaciones) ? materia.Observaciones : "-", fontPequeña, colorFondo);
                    }

                    document.Add(tablaMaterias);

                    // PIE DE PÁGINA
                    document.Add(new iTextParagraph($"Reporte generado el {DateTime.Now:dd/MM/yyyy 'a las' HH:mm:ss}", fontPequeña)
                    {
                        SpacingBefore = 20f,
                        Alignment = Element.ALIGN_RIGHT
                    });

                    document.Add(new iTextParagraph("Sistema de Control Académico - Plataforma Web (Versión Mejorada)", fontPequeña)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingBefore = 10f
                    });

                    document.Close();
                    writer.Close();

                    var pdfBytes = stream.ToArray();
                    return File(pdfBytes, "application/pdf", $"ReporteMaterias_{datosAlumno.Matricula}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar PDF: " + ex.Message;
                return RedirectToAction("Index", new { id = id });
            }
        }

        public ActionResult CalcularYGuardarPromedio(int idPersona)
        {
            try
            {
                // Rol Director = solo lectura: el SP calcula Y GUARDA el promedio (escritura).
                // Para un Director no se ejecuta; se devuelve respuesta benigna.
                var _usrDir = Session["Usuario"] as Usuario;
                if (_usrDir != null && _usrDir.EsDirector)
                {
                    return Json(new { success = false, message = "Cuenta de Director: modo solo lectura." }, JsonRequestBehavior.AllowGet);
                }

                // Ejecutar el stored procedure
                var resultado = db.Database.SqlQuery<PromedioAlumnoDto>(
                    "EXEC sp_CalcularYGuardarPromedioAlumno @p0",
                    idPersona
                ).FirstOrDefault();

                if (resultado == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se pudo calcular el promedio"
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        promedioGeneral = resultado.PromedioGeneral,
                        planPredominante = resultado.PlanPredominante,
                        calificacionMinima = resultado.CalificacionMinima,
                        permiteDecimales = resultado.PermiteDecimales,
                        totalMateriasCalificadas = resultado.TotalMateriasCalificadas,
                        totalMateriasAcreditadas = resultado.TotalMateriasAcreditadas,
                        totalMateriasReprobadas = resultado.TotalMateriasReprobadas,
                        totalMateriasExtraordinario = resultado.TotalMateriasExtraordinario,
                        estadoPromedio = resultado.EstadoPromedio
                    },
                    message = "Promedio calculado y guardado correctamente"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al calcular el promedio: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        // MÉTODOS AUXILIARES

        private DatosReporte ObtenerDatosReporte(int idPersona)
        {
            var resultado = new DatosReporte();
            resultado.Materias = new List<MateriaReporte>();

            var alumno = db.Database.SqlQuery<EstudianteDto>(
                @"SELECT IdPersona, Nombre, Matricula, IdCarrera, IdGrado 
                  FROM DatosPersonales 
                  WHERE IdPersona = @p0", idPersona).FirstOrDefault();

            if (alumno != null)
            {
                resultado.NombreAlumno = alumno.Nombre;
                resultado.Matricula = alumno.Matricula;

                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == alumno.IdCarrera);
                resultado.Carrera = carrera?.Nombre ?? "N/A";
            }

            var materias = db.Database.SqlQuery<MateriaReporte>(
                @"SELECT 
                    ma.IdMateria,
                    m.Nombre as NombreMateria,
                    ma.Calificacion,
                    ma.Estado,
                    ma.IntentosExtraordinarios,
                    ma.FechaExamenExtraordinario,
                    ma.FechaInicioArrastre,
                    ma.Observaciones
                  FROM MateriasAlumno ma
                  INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                  WHERE ma.IdPersona = @p0
                  ORDER BY m.Nombre", idPersona).ToList();

            resultado.Materias = materias;

            resultado.MateriasReprobadas = materias.Count(m => m.Estado == "Reprobada");
            resultado.MateriasExtraordinario = materias.Count(m => m.Estado == "Extraordinario");
            resultado.MateriasAcreditadas = materias.Count(m => m.Estado == "Acreditada");
            resultado.MateriasPendientes = materias.Count(m => m.Estado == "Pendiente");

            // SOLO PARA MOSTRAR EN REPORTES
            resultado.AlumnoReprobado = resultado.MateriasReprobadas >= 4 || resultado.MateriasExtraordinario >= 4;

            return resultado;
        }

        // Métodos auxiliares para PDF mantenidos...
        private void AgregarCeldaInfoPDF(PdfPTable tabla, string etiqueta, string valor, iTextFont fontEtiqueta, iTextFont fontValor)
        {
            tabla.AddCell(new PdfPCell(new Phrase(etiqueta, fontEtiqueta))
            {
                Border = 0,
                PaddingBottom = 8f,
                PaddingLeft = 5f
            });

            tabla.AddCell(new PdfPCell(new Phrase(valor, fontValor))
            {
                Border = 0,
                PaddingBottom = 8f,
                PaddingLeft = 10f
            });
        }

        private void AgregarCeldaHeaderPDF(PdfPTable tabla, string texto, BaseColor color)
        {
            var celda = new PdfPCell(new Phrase(texto, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE)))
            {
                BackgroundColor = color,
                Padding = 8f,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };
            tabla.AddCell(celda);
        }

        private void AgregarCeldaDatoPDF(PdfPTable tabla, string texto, iTextFont font)
        {
            var celda = new PdfPCell(new Phrase(texto, font))
            {
                Padding = 6f,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                BackgroundColor = BaseColor.WHITE
            };
            tabla.AddCell(celda);
        }

        private void AgregarCeldaMateriaPDF(PdfPTable tabla, string texto, iTextFont font, BaseColor colorFondo)
        {
            var celda = new PdfPCell(new Phrase(texto, font))
            {
                Padding = 5f,
                BackgroundColor = colorFondo,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };
            tabla.AddCell(celda);
        }

        private decimal CalcularPromedio(List<MateriaReporte> materias)
        {
            var tieneReprobadas = materias.Any(m => m.Estado == "Reprobada");
            var tieneExtraordinario = materias.Any(m => m.Estado == "Extraordinario");

            if (tieneReprobadas || tieneExtraordinario)
            {
                return 6.9m;
            }
            else
            {
                var materiasConCalif = materias.Where(m => m.Calificacion.HasValue && m.Calificacion > 0).ToList();
                if (materiasConCalif.Any())
                {
                    return Math.Round(materiasConCalif.Average(m => m.Calificacion.Value), 1);
                }
                else
                {
                    return 0;
                }
            }
        }

        // MÉTODO: Obtener materias para selección de arrastre (incluyendo desactivadas)
        [HttpGet]
        public JsonResult ObtenerMateriasParaArrastre(int idCarrera, int idGrado, int idPersona)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📋 Buscando materias arrastre - Carrera: {idCarrera}, Grado: {idGrado}, Persona: {idPersona}");

                // 1. DETECTAR ID DE ESPECIALIDAD REAL (Igual que en el Index)
                // Buscamos en la configuración del grupo al que pertenece el alumno
                int? idEspecialidadDetectada = db.Database.SqlQuery<int?>(@"
            SELECT TOP 1 tg.IdEspecialidad
            FROM DatosPersonales dp
            INNER JOIN TutoriaGrupals tg ON 
                dp.IdGrupo = tg.IdGrupo AND 
                dp.IdCarrera = tg.IdCarrera AND 
                dp.IdGrado = tg.IdGrado AND
                dp.IdTurno = tg.IdTurno AND
                dp.IdPeriodo = tg.IdPeriodo AND
                dp.Año = tg.Año
            WHERE dp.IdPersona = @p0", idPersona).FirstOrDefault();

                int idEspecialidadFiltro = idEspecialidadDetectada ?? 0;

                System.Diagnostics.Debug.WriteLine($"🔍 Especialidad Detectada ID: {idEspecialidadFiltro}");

                // 2. CONSULTA DE MATERIAS (Usando ID + Tronco Común)
                var query = @"
            SELECT DISTINCT
                m.IdMateria,
                m.Nombre,
                m.IdPlanEstudio,
                m.NumeroUnidades,
                m.Activo,
                p.Nombre as NombrePlan,
                ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima,
                ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
            FROM Materias m
            INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdCarrera = @p0 
              AND m.IdGrado = @p1
              AND (
                  -- Coincidencia exacta por ID (Prioridad)
                  m.IdEspecialidad = @p2
                  OR
                  -- Materias de Tronco Común
                  e.Nombre LIKE '%Común%' OR e.Nombre LIKE '%Comun%'
              )
            ORDER BY m.Activo DESC, m.Nombre ASC";

                var materias = db.Database.SqlQuery<MateriaParaArrastreDto>(
                    query,
                    idCarrera, idGrado, idEspecialidadFiltro
                ).ToList();

                System.Diagnostics.Debug.WriteLine($"✅ Materias encontradas: {materias.Count}");

                return Json(materias, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en ObtenerMateriasParaArrastre: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ✅ MÉTODO NUEVO: Agregar materia de arrastre manual CON CALIFICACIONES DE UNIDADES
        [HttpPost]
        public JsonResult AgregarMateriaArrastreManualConUnidades(
                    int idPersona,
                    int idMateria,
                    DateTime fechaInicioArrastre,
                    decimal calificacionFinal,
                    decimal[] calificacionesUnidades,
                    int intentosExtraordinarios = 2, // ✅ NUEVO PARÁMETRO (default 2 = primer intento)
                    string observaciones = "")
        {
            // Envolver toda la operación en una transacción
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== Agregando materia de arrastre CON INTENTO SELECCIONADO ===");
                    System.Diagnostics.Debug.WriteLine($"IdPersona: {idPersona}, IdMateria: {idMateria}");
                    System.Diagnostics.Debug.WriteLine($"Intentos Extraordinarios (Input): {intentosExtraordinarios}");
                    System.Diagnostics.Debug.WriteLine($"Calificación Final: {calificacionFinal}");

                    // ✅ VALIDAR INTENTOS PERMITIDOS (2 o 3)
                    if (intentosExtraordinarios < 2 || intentosExtraordinarios > 3)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "El intento debe ser 2 (primer arrastre) o 3 (segundo arrastre - último)" });
                    }

                    // 1. Validar que el alumno existe
                    var alumno = db.Database.SqlQuery<EstudianteDto>(
                        "SELECT IdPersona, Nombre, IdGrado FROM DatosPersonales WHERE IdPersona = @p0",
                        idPersona).FirstOrDefault();

                    if (alumno == null)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Alumno no encontrado" });
                    }

                    // 2. Validar que la materia existe y obtener su información
                    var materia = db.Database.SqlQuery<MateriaConPlanDto>(@"
                SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdGrado, m.IdPlanEstudio, m.NumeroUnidades,
                       ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan, 
                       ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima, 
                       ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
                FROM Materias m
                LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
                WHERE m.IdMateria = @p0
            ", idMateria).FirstOrDefault();

                    if (materia == null)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Materia no encontrada" });
                    }

                    // 3. Validar que la materia es de un cuatrimestre anterior
                    if (materia.IdGrado >= alumno.IdGrado)
                    {
                        transaction.Rollback();
                        return Json(new
                        {
                            success = false,
                            message = $"La materia debe ser de un cuatrimestre anterior. Materia: {materia.IdGrado}°, Alumno: {alumno.IdGrado}°"
                        });
                    }

                    // 4. Validar que no exista ya un registro
                    var existeRegistro = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM MateriasAlumno WHERE IdPersona = @p0 AND IdMateria = @p1",
                        idPersona, idMateria).FirstOrDefault();

                    if (existeRegistro > 0)
                    {
                        transaction.Rollback();
                        return Json(new
                        {
                            success = false,
                            message = "Esta materia ya está registrada para el alumno"
                        });
                    }

                    // 5. Validar número de unidades
                    if (calificacionesUnidades == null || calificacionesUnidades.Length != materia.NumeroUnidades)
                    {
                        transaction.Rollback();
                        return Json(new
                        {
                            success = false,
                            message = $"Se esperaban {materia.NumeroUnidades} calificaciones de unidades, se recibieron {calificacionesUnidades?.Length ?? 0}"
                        });
                    }

                    // 6. Determinar estado según calificación final
                    var calificacionMinima = materia.CalificacionMinima ?? 7.0m;
                    var estadoFinal = calificacionFinal >= calificacionMinima ? "Acreditada" : "Reprobada";
                    bool esAprobatoriaFinal = (estadoFinal == "Acreditada");

                    // 7. AJUSTAR INTENTOS SEGÚN ESTADO Y SELECCIÓN DEL USUARIO
                    // Esta lógica es correcta y coincide con la de sp_GuardarMateriaAlumno
                    // 1er Arrastre (Input=2) -> Si falla, se guarda 2.
                    // 2do Arrastre (Input=3) -> Si falla, se guarda 3.
                    int intentosFinales = intentosExtraordinarios;

                    if (estadoFinal == "Acreditada")
                    {
                        // Si acredita, resetear intentos
                        intentosFinales = 0; // Se resetea para que no cuente
                    }

                    // 8. Preparar observaciones con información del intento
                    string textoIntento = intentosExtraordinarios == 2
                        ? "1er intento de arrastre"
                        : "2do intento de arrastre (ÚLTIMO)";

                    var observacionesCompletas = string.IsNullOrWhiteSpace(observaciones)
                        ? $"Materia de arrastre agregada manualmente el {DateTime.Now:dd/MM/yyyy}. " +
                          $"Cuatrimestre: {materia.IdGrado}°. {textoIntento}. " +
                          $"Calificación final: {calificacionFinal:0.00} ({estadoFinal})."
                        : $"{observaciones} [Agregada manualmente el {DateTime.Now:dd/MM/yyyy} - {textoIntento} - Calif: {calificacionFinal:0.00}]";

                    // 9. Crear registro de MateriasAlumno
                    db.Database.ExecuteSqlCommand(@"
                INSERT INTO MateriasAlumno 
                (IdMateria, IdPersona, Calificacion, Estado, IntentosExtraordinarios, FechaInicioArrastre, Observaciones, FechaRegistro, FechaActualizacion)
                VALUES 
                (@p0, @p1, @p2, @p3, @p4, @p5, @p6, GETDATE(), GETDATE())
            ", idMateria, idPersona, calificacionFinal, estadoFinal, intentosFinales, fechaInicioArrastre, observacionesCompletas);

                    System.Diagnostics.Debug.WriteLine($"✅ Materia de arrastre creada en MateriasAlumno con intento {intentosFinales}");

                    // 10. Obtener el Id recién creado
                    var idMateriaAlumno = db.Database.SqlQuery<int>(
                        "SELECT Id FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                        idMateria, idPersona).FirstOrDefault();

                    if (idMateriaAlumno == 0)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Error: No se pudo obtener el ID del registro creado" });
                    }

                    // ====================================================================
                    // ✅ INICIO DE LA SOLUCIÓN: INSERTAR HISTORIAL FALSO
                    // ====================================================================

                    // Preparar una calificación reprobatoria falsa (5.0) ajustada al plan
                    decimal califAjustadaFail = 5.0m;
                    if (!(materia.PermiteDecimales ?? true))
                    {
                        // Lógica del Plan 2020 para < 8.0
                        califAjustadaFail = Math.Floor(5.0m);
                    }

                    // Fechas falsas para el historial
                    var fechaOrdinario = fechaInicioArrastre.AddMonths(-2);
                    var fechaExtra = fechaInicioArrastre.AddMonths(-1);
                    string obsHistorial = "Registro automático por arrastre manual";

                    // Insertar Falso Intento 1: Ordinario (Reprobado)
                    db.Database.ExecuteSqlCommand(@"
                        INSERT INTO HistorialIntentosMateria
                            (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                             CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                        VALUES
                            (@p0, 1, 'Ordinario', 5.0, @p1, 0, @p2, @p3)",
                        idMateriaAlumno, califAjustadaFail, fechaOrdinario, obsHistorial);

                    // Insertar Falso Intento 2: Extraordinario (Reprobado)
                    db.Database.ExecuteSqlCommand(@"
                        INSERT INTO HistorialIntentosMateria
                            (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                             CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                        VALUES
                            (@p0, 2, 'Extraordinario', 5.0, @p1, 0, @p2, @p3)",
                        idMateriaAlumno, califAjustadaFail, fechaExtra, obsHistorial);

                    System.Diagnostics.Debug.WriteLine($"✅ Historial falso insertado: Ordinario (1) y Extraordinario (2)");

                    int numeroIntentoActual;
                    string tipoIntentoActual;

                    // Si el usuario seleccionó "2do Intento de Arrastre" (valor 3)
                    if (intentosExtraordinarios == 3)
                    {
                        // Debemos insertar también el Falso Intento 3: Arrastre 1 (Reprobado)
                        var fechaArrastre1 = fechaInicioArrastre.AddDays(-5);
                        db.Database.ExecuteSqlCommand(@"
                            INSERT INTO HistorialIntentosMateria
                                (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                                 CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                            VALUES
                                (@p0, 3, 'Arrastre (Intento 1)', 5.0, @p1, 0, @p2, @p3)",
                            idMateriaAlumno, califAjustadaFail, fechaArrastre1, obsHistorial);

                        numeroIntentoActual = 4;
                        tipoIntentoActual = "Arrastre (Intento 2)";
                        System.Diagnostics.Debug.WriteLine($"✅ Historial falso insertado: Arrastre 1 (3)");
                    }
                    else // El usuario seleccionó "1er Intento de Arrastre" (valor 2)
                    {
                        numeroIntentoActual = 3;
                        tipoIntentoActual = "Arrastre (Intento 1)";
                    }

                    // Insertar el Intento REAL (Intento 3 o 4)
                    db.Database.ExecuteSqlCommand(@"
                        INSERT INTO HistorialIntentosMateria
                            (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                             CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                        VALUES
                            (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                        idMateriaAlumno,
                        numeroIntentoActual,
                        tipoIntentoActual,
                        calificacionFinal, // Calificacion Original
                        calificacionFinal, // Calificacion Ajustada
                        esAprobatoriaFinal,
                        fechaInicioArrastre,
                        observacionesCompletas);

                    System.Diagnostics.Debug.WriteLine($"✅ Historial REAL insertado: {tipoIntentoActual} ({numeroIntentoActual})");

                    // ====================================================================
                    // ✅ FIN DE LA SOLUCIÓN
                    // ====================================================================


                    // 11. Guardar calificaciones de cada unidad
                    for (int i = 0; i < calificacionesUnidades.Length; i++)
                    {
                        int numeroUnidad = i + 1;
                        decimal califUnidad = calificacionesUnidades[i];

                        db.Database.ExecuteSqlCommand(@"
                    INSERT INTO CalificacionesUnidades 
                    (IdMateriaAlumno, NumeroUnidad, Calificacion, FechaRegistro, FechaActualizacion)
                    VALUES 
                    (@p0, @p1, @p2, GETDATE(), GETDATE())
                ", idMateriaAlumno, numeroUnidad, califUnidad);

                        System.Diagnostics.Debug.WriteLine($"✅ Unidad {numeroUnidad} guardada: {califUnidad}");
                    }

                    // 12. Calcular fecha límite
                    var fechaLimite = fechaInicioArrastre.AddMonths(8);
                    var diasRestantes = (fechaLimite - DateTime.Now).Days;

                    // 13. Verificar límites académicos
                    System.Threading.Thread.Sleep(200);

                    var conteoPost = db.Database.SqlQuery<ContadorSimple>(
                        @"SELECT 
                    COUNT(CASE WHEN Estado = 'Reprobada' THEN 1 END) as Reprobadas,
                    COUNT(CASE WHEN Estado = 'Extraordinario' THEN 1 END) as Extraordinario
                  FROM MateriasAlumno 
                  WHERE IdPersona = @p0", idPersona).FirstOrDefault();

                    bool excedioExtraordinarios = (conteoPost?.Extraordinario ?? 0) >= 4;
                    bool excedioArrastre = (conteoPost?.Reprobadas ?? 0) >= 4;
                    bool dadoDeBaja = excedioArrastre || excedioExtraordinarios;

                    // 14. MENSAJE PERSONALIZADO SEGÚN INTENTO
                    string mensajeIntento = intentosExtraordinarios == 2
                        ? "Registrada como 1er intento de arrastre"
                        : "⚠ Registrada como ÚLTIMO intento de arrastre";

                    // Confirmar la transacción
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = $"Materia de arrastre agregada: {materia.NombreMateria} - {textoIntento} - Calificación: {calificacionFinal:0.00} ({estadoFinal}). {mensajeIntento}",
                        detalles = new
                        {
                            alumno = alumno.Nombre,
                            materia = materia.NombreMateria,
                            cuatrimestre = materia.IdGrado,
                            plan = materia.NombrePlan,
                            calificacionFinal = calificacionFinal,
                            estado = estadoFinal,
                            intentoArrastre = intentosExtraordinarios,
                            esUltimoIntento = intentosExtraordinarios >= 3,
                            unidadesCalificadas = calificacionesUnidades.Length,
                            fechaInicio = fechaInicioArrastre.ToString("dd/MM/yyyy"),
                            fechaLimite = fechaLimite.ToString("dd/MM/yyyy"),
                            diasRestantes = diasRestantes
                        },
                        alumnoReprobado = dadoDeBaja,
                        conteoActual = new
                        {
                            reprobadas = conteoPost?.Reprobadas ?? 0,
                            extraordinarios = conteoPost?.Extraordinario ?? 0
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Revertir la transacción en caso de error
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"❌ Error en AgregarMateriaArrastreManualConUnidades: {ex.Message}");
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
        }


        // ====================================================================
        // ✅ AGREGAR MATERIA DE ARRASTRE CON HISTORIAL COMPLETO
        // ====================================================================
        [HttpPost]
        public JsonResult AgregarMateriaArrastreConHistorialCompleto(ArrastreConHistorialDto datos)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Guardando arrastre con historial completo: Materia {datos.IdMateria}, Persona {datos.IdPersona} ===");

                // ============================================================
                // PASO 1: VALIDAR DATOS BÁSICOS
                // ============================================================
                if (datos == null || datos.Intentos == null || datos.Intentos.Count == 0)
                {
                    return Json(new { success = false, message = "Datos incompletos o sin intentos" });
                }

                // Validar que exista intento ordinario (número 1)
                if (!datos.Intentos.Any(i => i.NumeroIntento == 1))
                {
                    return Json(new { success = false, message = "Debe incluir al menos el intento ordinario" });
                }

                // ============================================================
                // PASO 2: VERIFICAR SI YA EXISTE LA MATERIA
                // ============================================================
                var existeMateria = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    datos.IdMateria, datos.IdPersona
                ).FirstOrDefault();

                if (existeMateria > 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Esta materia ya está registrada para el alumno. Use el módulo de materias para editarla."
                    });
                }

                // ============================================================
                // PASO 3: OBTENER INFORMACIÓN DE LA MATERIA Y PLAN
                // ============================================================
                var infoMateria = db.Database.SqlQuery<MateriaConPlanDto>(@"
            SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdGrado, m.IdPlanEstudio, m.NumeroUnidades,
                   ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan, 
                   ISNULL(p.Año, 2024) as AñoPlan, 
                   ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima, 
                   ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
            FROM Materias m
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdMateria = @p0", datos.IdMateria).FirstOrDefault();

                if (infoMateria == null)
                {
                    return Json(new { success = false, message = "Materia no encontrada en el catálogo" });
                }

                System.Diagnostics.Debug.WriteLine($"Plan: {infoMateria.NombrePlan}, Mínima: {infoMateria.CalificacionMinima}, Decimales: {infoMateria.PermiteDecimales}");

                // ============================================================
                // PASO 4: INSERTAR REGISTRO EN MateriasAlumno
                // ============================================================
                db.Database.ExecuteSqlCommand(@"
            INSERT INTO MateriasAlumno 
            (IdPersona, IdMateria, Calificacion, Estado, IntentosExtraordinarios, 
             FechaInicioArrastre, Observaciones, FechaRegistro, FechaActualizacion)
            VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, GETDATE(), GETDATE())",
                    datos.IdPersona,
                    datos.IdMateria,
                    datos.CalificacionFinal,
                    datos.EstadoFinal,
                    datos.IntentosExtraordinarios,
                    DateTime.Parse(datos.FechaInicioArrastre),
                    datos.Observaciones ?? ""
                );

                // Obtener el ID recién insertado
                var idMateriaAlumno = db.Database.SqlQuery<int>(
                    "SELECT Id FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    datos.IdMateria, datos.IdPersona
                ).FirstOrDefault();

                System.Diagnostics.Debug.WriteLine($"MateriasAlumno creado con ID: {idMateriaAlumno}");

                // ============================================================
                // PASO 5: GUARDAR CALIFICACIONES DE UNIDADES (ORDINARIO)
                // ============================================================
                var intentoOrdinario = datos.Intentos.FirstOrDefault(i => i.NumeroIntento == 1);
                if (intentoOrdinario != null && intentoOrdinario.CalificacionesUnidades != null && intentoOrdinario.CalificacionesUnidades.Count > 0)
                {
                    for (int i = 0; i < intentoOrdinario.CalificacionesUnidades.Count; i++)
                    {
                        var calificacionUnidad = intentoOrdinario.CalificacionesUnidades[i];

                        db.Database.ExecuteSqlCommand(@"
                    INSERT INTO CalificacionesUnidades 
                    (IdMateriaAlumno, NumeroUnidad, Calificacion, FechaRegistro, FechaActualizacion)
                    VALUES (@p0, @p1, @p2, GETDATE(), GETDATE())",
                            idMateriaAlumno,
                            i + 1,
                            calificacionUnidad
                        );
                    }
                    System.Diagnostics.Debug.WriteLine($"Guardadas {intentoOrdinario.CalificacionesUnidades.Count} unidades");
                }

                // ============================================================
                // PASO 6: GUARDAR TODOS LOS INTENTOS EN HISTORIAL
                // ============================================================
                foreach (var intento in datos.Intentos)
                {
                    db.Database.ExecuteSqlCommand(@"
                INSERT INTO HistorialIntentosMateria 
                (IdMateriaAlumno, NumeroIntento, TipoIntento, Calificacion, 
                 CalificacionAjustada, EsAprobatoria, FechaRegistro, Observaciones)
                VALUES (@p0, @p1, @p2, @p3, @p4, @p5, GETDATE(), @p6)",
                        idMateriaAlumno,
                        intento.NumeroIntento,
                        intento.TipoIntento,
                        intento.Calificacion,
                        intento.CalificacionAjustada,
                        intento.EsAprobatoria ? 1 : 0,
                        intento.Observaciones ?? ""
                    );

                    System.Diagnostics.Debug.WriteLine($"Intento {intento.NumeroIntento} guardado: {intento.TipoIntento} - {intento.CalificacionAjustada} ({(intento.EsAprobatoria ? "APR" : "REP")})");
                }

                // ============================================================
                // PASO 7: VERIFICAR LÍMITES ACADÉMICOS
                // ============================================================
                System.Threading.Thread.Sleep(200);

                var conteoPost = db.Database.SqlQuery<ContadorSimple>(
                    @"SELECT 
                COUNT(CASE WHEN Estado = 'Reprobada' THEN 1 END) as Reprobadas,
                COUNT(CASE WHEN Estado = 'Extraordinario' THEN 1 END) as Extraordinario
              FROM MateriasAlumno 
              WHERE IdPersona = @p0", datos.IdPersona).FirstOrDefault();

                bool excedioArrastre = (conteoPost?.Reprobadas ?? 0) >= 4;
                bool excedioExtraordinarios = (conteoPost?.Extraordinario ?? 0) >= 4;
                bool dadoDeBaja = excedioArrastre || excedioExtraordinarios;

                // ============================================================
                // PASO 8: GENERAR RESPUESTA
                // ============================================================
                var mensajeExito = $"Materia agregada con {datos.Intentos.Count} intento(s) registrado(s). Estado final: {datos.EstadoFinal}";

                if (dadoDeBaja)
                {
                    mensajeExito += "\n\n⚠ ALERTA: El alumno ha excedido los límites académicos";
                }

                // Determinar si la materia está bloqueada
                bool materiaReprobadaDefinitiva = datos.EstadoFinal == "Reprobada" && datos.IntentosExtraordinarios >= 3;

                return Json(new
                {
                    success = true,
                    message = mensajeExito,
                    idMateriaAlumno = idMateriaAlumno,
                    estadoFinal = datos.EstadoFinal,
                    calificacionFinal = datos.CalificacionFinal,
                    intentosRegistrados = datos.Intentos.Count,
                    materiaReprobadaDefinitiva = materiaReprobadaDefinitiva,
                    alumnoEnRiesgo = dadoDeBaja,
                    conteoActual = new
                    {
                        reprobadas = conteoPost.Reprobadas,
                        extraordinarios = conteoPost.Extraordinario
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR al guardar arrastre con historial: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new
                {
                    success = false,
                    message = "Error al guardar la materia: " + ex.Message
                });
            }
        }

        // MÉTODO: Agregar materia de arrastre manual
        [HttpPost]
        public JsonResult AgregarMateriaArrastreManual(int idPersona, int idMateria, DateTime fechaInicioArrastre, string observaciones = "")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Agregando materia de arrastre manual ===");
                System.Diagnostics.Debug.WriteLine($"IdPersona: {idPersona}, IdMateria: {idMateria}, Fecha: {fechaInicioArrastre:yyyy-MM-dd}");

                // 1. Validar que el alumno existe
                var alumno = db.Database.SqlQuery<EstudianteDto>(
                    "SELECT IdPersona, Nombre, IdGrado FROM DatosPersonales WHERE IdPersona = @p0",
                    idPersona).FirstOrDefault();

                if (alumno == null)
                {
                    return Json(new { success = false, message = "Alumno no encontrado" });
                }

                // 2. Validar que la materia existe y obtener su información
                var materia = db.Database.SqlQuery<MateriaConPlanDto>(@"
            SELECT m.IdMateria, m.Nombre as NombreMateria, m.IdGrado, m.IdPlanEstudio,
                   p.Nombre as NombrePlan, p.CalificacionMinima, p.PermiteDecimales
            FROM Materias m
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdMateria = @p0
        ", idMateria).FirstOrDefault();

                if (materia == null)
                {
                    return Json(new { success = false, message = "Materia no encontrada" });
                }

                // 3. Validar que la materia es de un cuatrimestre anterior
                if (materia.IdGrado >= alumno.IdGrado)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"La materia debe ser de un cuatrimestre anterior. Materia: {materia.IdGrado}°, Alumno: {alumno.IdGrado}°"
                    });
                }

                // 4. Validar que no exista ya un registro de esta materia para este alumno
                var existeRegistro = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM MateriasAlumno WHERE IdPersona = @p0 AND IdMateria = @p1",
                    idPersona, idMateria).FirstOrDefault();

                if (existeRegistro > 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Esta materia ya está registrada para el alumno. Use la función de edición para modificarla."
                    });
                }

                // 5. Preparar observaciones
                var observacionesCompletas = string.IsNullOrWhiteSpace(observaciones)
                    ? $"Materia de arrastre agregada manualmente el {DateTime.Now:dd/MM/yyyy}. " +
                      $"Cuatrimestre: {materia.IdGrado}°, Fecha inicio arrastre: {fechaInicioArrastre:dd/MM/yyyy}."
                    : $"{observaciones} [Agregada manualmente el {DateTime.Now:dd/MM/yyyy}]";

                // 6. Crear registro de materia en arrastre
                // IntentosExtraordinarios = 2 porque ya está en arrastre (viene de haber reprobado extraordinario)
                db.Database.ExecuteSqlCommand(@"
            INSERT INTO MateriasAlumno 
            (IdMateria, IdPersona, Estado, IntentosExtraordinarios, FechaInicioArrastre, Observaciones, FechaRegistro, FechaActualizacion)
            VALUES 
            (@p0, @p1, 'Reprobada', 2, @p2, @p3, GETDATE(), GETDATE())
        ", idMateria, idPersona, fechaInicioArrastre, observacionesCompletas);

                System.Diagnostics.Debug.WriteLine($"✅ Materia de arrastre agregada exitosamente");

                // 7. Calcular fecha límite
                var fechaLimite = fechaInicioArrastre.AddMonths(8);
                var diasRestantes = (fechaLimite - DateTime.Now).Days;

                return Json(new
                {
                    success = true,
                    message = $"Materia de arrastre agregada: {materia.NombreMateria} ({materia.NombrePlan})",
                    detalles = new
                    {
                        alumno = alumno.Nombre,
                        materia = materia.NombreMateria,
                        cuatrimestre = materia.IdGrado,
                        plan = materia.NombrePlan,
                        fechaInicio = fechaInicioArrastre.ToString("dd/MM/yyyy"),
                        fechaLimite = fechaLimite.ToString("dd/MM/yyyy"),
                        diasRestantes = diasRestantes
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en AgregarMateriaArrastreManual: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }



        public class PromedioAlumnoDto
        {
            public int IdPersona { get; set; }
            public decimal PromedioGeneral { get; set; }
            public string PlanPredominante { get; set; }
            public decimal CalificacionMinima { get; set; }
            public bool PermiteDecimales { get; set; }
            public int TotalMateriasCalificadas { get; set; }
            public int TotalMateriasAcreditadas { get; set; }
            public int TotalMateriasReprobadas { get; set; }
            public int TotalMateriasExtraordinario { get; set; }
            public string EstadoPromedio { get; set; }
        }

        // DTO para materias disponibles para arrastre
        public class MateriaParaArrastreDto
        {
            public int IdMateria { get; set; }
            public string Nombre { get; set; }
            public int IdPlanEstudio { get; set; }
            public int NumeroUnidades { get; set; }
            public bool Activo { get; set; }
            public string NombrePlan { get; set; }
            public decimal CalificacionMinima { get; set; }
            public bool PermiteDecimales { get; set; }
        }

        // DTO para agregar arrastre con historial completo
        public class ArrastreConHistorialDto
        {
            public int IdPersona { get; set; }
            public int IdMateria { get; set; }
            public string FechaInicioArrastre { get; set; }
            public string EstadoFinal { get; set; }
            public decimal CalificacionFinal { get; set; }
            public int IntentosExtraordinarios { get; set; }
            public List<IntentoDto> Intentos { get; set; }
            public string Observaciones { get; set; }
        }

        // DTO para cada intento individual
        public class IntentoDto
        {
            public int NumeroIntento { get; set; }
            public string TipoIntento { get; set; }
            public decimal Calificacion { get; set; }
            public decimal CalificacionAjustada { get; set; }
            public bool EsAprobatoria { get; set; }
            public string Observaciones { get; set; }
            public List<decimal> CalificacionesUnidades { get; set; }
        }

        public ActionResult DescargarExcel(int id)
        {
            try
            {
                var datosAlumno = ObtenerDatosReporte(id);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("REPORTE ACADÉMICO");

                    // Título
                    worksheet.Cells[1, 1].Value = "REPORTE ACADÉMICO DEL ESTUDIANTE";
                    worksheet.Cells[1, 1, 1, 5].Merge = true;

                    var tituloRange = worksheet.Cells[1, 1];
                    tituloRange.Style.Font.Size = 18;
                    tituloRange.Style.Font.Bold = true;
                    tituloRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    tituloRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 73, 125));
                    tituloRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    tituloRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                    // NOMBRE DEL ESTUDIANTE
                    int fila = 2;
                    worksheet.Cells[fila, 1].Value = "ESTUDIANTE:";
                    worksheet.Cells[fila, 2].Value = datosAlumno.NombreAlumno?.ToUpper() ?? "N/A";
                    worksheet.Cells[fila, 2, fila, 5].Merge = true;
                    worksheet.Cells[fila, 1].Style.Font.Bold = true;
                    worksheet.Cells[fila, 2].Style.Font.Bold = true;

                    fila++;
                    worksheet.Cells[fila, 1].Value = "MATRÍCULA:";
                    worksheet.Cells[fila, 2].Value = datosAlumno.Matricula?.ToUpper() ?? "N/A";
                    worksheet.Cells[fila, 1].Style.Font.Bold = true;

                    fila += 2;
                    worksheet.Cells[fila, 1].Value = "DETALLE DE MATERIAS";
                    worksheet.Cells[fila, 1, fila, 5].Merge = true;
                    AplicarEstiloEncabezadoExcel(worksheet.Cells[fila, 1, fila, 5], System.Drawing.Color.FromArgb(44, 62, 80));
                    fila++;

                    // ENCABEZADOS SIN COLUMNA "INTENTOS"
                    string[] encabezados = { "MATERIA", "CALIFICACIÓN", "ESTADO", "FECHA LÍMITE", "OBSERVACIONES" };
                    for (int i = 0; i < encabezados.Length; i++)
                    {
                        worksheet.Cells[fila, i + 1].Value = encabezados[i];
                    }

                    var headerMaterias = worksheet.Cells[fila, 1, fila, 5];
                    headerMaterias.Style.Font.Bold = true;
                    headerMaterias.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    headerMaterias.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerMaterias.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(44, 62, 80));
                    headerMaterias.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    fila++;

                    // DATOS DE MATERIAS - TODO EN MAYÚSCULAS
                    foreach (var materia in datosAlumno.Materias.OrderBy(m => m.NombreMateria))
                    {
                        worksheet.Cells[fila, 1].Value = materia.NombreMateria?.ToUpper() ?? "N/A";
                        worksheet.Cells[fila, 2].Value = materia.Calificacion?.ToString("0.00") ?? "N/A";
                        worksheet.Cells[fila, 3].Value = (materia.Estado ?? "PENDIENTE").ToUpper();

                        // FECHA LÍMITE
                        string fechaLimite = "-";
                        if (materia.Estado == "Reprobada" && materia.FechaInicioArrastre.HasValue)
                        {
                            var limite = materia.FechaInicioArrastre.Value.AddMonths(8);
                            fechaLimite = limite.ToString("dd/MM/yyyy");
                        }
                        worksheet.Cells[fila, 4].Value = fechaLimite;

                        worksheet.Cells[fila, 5].Value = (!string.IsNullOrEmpty(materia.Observaciones) ? materia.Observaciones : "-").ToUpper();

                        // COLOR SEGÚN ESTADO
                        var filaRange = worksheet.Cells[fila, 1, fila, 5];
                        switch (materia.Estado?.ToLower())
                        {
                            case "acreditada":
                                filaRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                filaRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(212, 237, 218));
                                break;
                            case "reprobada":
                                filaRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                filaRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(248, 215, 218));
                                break;
                            case "extraordinario":
                                filaRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                filaRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 243, 205));
                                break;
                        }

                        fila++;
                    }

                    // AJUSTAR ANCHOS DE COLUMNAS
                    worksheet.Column(1).Width = 40; // MATERIA
                    worksheet.Column(2).Width = 15; // CALIFICACIÓN
                    worksheet.Column(3).Width = 20; // ESTADO
                    worksheet.Column(4).Width = 18; // FECHA LÍMITE
                    worksheet.Column(5).Width = 35; // OBSERVACIONES

                    // PIE DE PÁGINA
                    fila += 2;
                    worksheet.Cells[fila, 1].Value = $"REPORTE GENERADO EL {DateTime.Now:dd/MM/yyyy} A LAS {DateTime.Now:HH:mm:ss}";
                    worksheet.Cells[fila, 1, fila, 5].Merge = true;
                    worksheet.Cells[fila, 1].Style.Font.Size = 9;
                    worksheet.Cells[fila, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
                    worksheet.Cells[fila, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    // BORDES
                    var rangoCompleto = worksheet.Cells[worksheet.Dimension.Address];
                    rangoCompleto.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    rangoCompleto.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    rangoCompleto.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    rangoCompleto.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

                    var excelBytes = package.GetAsByteArray();
                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"REPORTE_MATERIAS_{datosAlumno.Matricula?.ToUpper()}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar Excel: " + ex.Message;
                return RedirectToAction("Index", new { id = id });
            }
        }

        private void AplicarEstiloEncabezadoExcel(ExcelRange rango, System.Drawing.Color color)
        {
            rango.Style.Font.Bold = true;
            rango.Style.Font.Size = 12;
            rango.Style.Font.Color.SetColor(System.Drawing.Color.White);
            rango.Style.Fill.PatternType = ExcelFillStyle.Solid;
            rango.Style.Fill.BackgroundColor.SetColor(color);
            rango.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            rango.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // AGREGAR ESTE MÉTODO EN AMBOS CONTROLADORES PARA VALIDAR ACCESO
        private bool ValidarAccesoCoordinador(Usuario usuario)
        {
            if (usuario == null) return false;
            return usuario.IdNivel == 3;
        }

        // MÉTODO PARA VERIFICAR SI EL USUARIO TIENE ACCESO AL GRUPO ESPECÍFICO
        private bool TieneAccesoAGrupoAlumno(Usuario usuario, int idTutoriaGrupal)
        {
            if (usuario == null)
                return false;

            try
            {
                // Los coordinadores tienen acceso a todos los grupos de su carrera
                if (usuario.IdNivel >= 3)
                {
                    var grupoCoordinador = db.TutoriaGrupals
                        .FirstOrDefault(x => x.IdTutoriaGrupal == idTutoriaGrupal && x.IdCarrera == usuario.IdCarrera);
                    return grupoCoordinador != null;
                }

                // Los tutores solo tienen acceso a sus grupos asignados
                if (usuario.IdNivel == 2)
                {
                    var grupoTutor = db.TutoriaGrupals
                        .FirstOrDefault(x => x.IdTutoriaGrupal == idTutoriaGrupal &&
                                             x.IdUsuario == usuario.IdUsuario &&
                                             x.IdCarrera == usuario.IdCarrera);
                    return grupoTutor != null;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando acceso al grupo: {ex.Message}");
                return false;
            }
        }
    }

    #region DTOs y Clases de Apoyo

    // DTO para materias con información completa del plan
    public class MateriaCompletaConPlanDto
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

        // Información del plan
        public string NombrePlan { get; set; }
        public int? AñoPlan { get; set; }
        public decimal? CalificacionMinima { get; set; }
        public bool? PermiteDecimales { get; set; }
        public string DescripcionPlan { get; set; }
    }

    // DTO para información básica de materia con plan
    public class MateriaConPlanDto
    {
        public int IdMateria { get; set; }
        public string NombreMateria { get; set; }

        public int IdGrado { get; set; }
        public int IdPlanEstudio { get; set; }
        public string NombrePlan { get; set; }
        public int? AñoPlan { get; set; }
        public decimal? CalificacionMinima { get; set; }
        public bool? PermiteDecimales { get; set; }
        public string DescripcionPlan { get; set; }
        public int NumeroUnidades { get; set; }
    }

    // Resultado de validación según plan
    public class ResultadoValidacionPlan
    {
        public bool EsValida { get; set; }
        public decimal? CalificacionAjustada { get; set; }
        public string EstadoFinal { get; set; }
        public string MensajeValidacion { get; set; }
        public string MensajeError { get; set; }
    }

    public class EstudianteDto
    {
        public int IdPersona { get; set; }
        public string Nombre { get; set; }
        public string Matricula { get; set; }
        public int IdCarrera { get; set; }
        public int IdGrado { get; set; }
        public string Especialidad { get; set; }
    }

    public class MateriaAlumnoDto
    {
        public int IdMateria { get; set; }
        public int IdPersona { get; set; }
        public decimal? Calificacion { get; set; }
        public string Estado { get; set; }
        public string Observaciones { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaExamenExtraordinario { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
    }

    public class MateriaGuardar
    {
        public int IdMateria { get; set; }
        public int IdPersona { get; set; }
        public decimal? Calificacion { get; set; }
        public string Estado { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public string Observaciones { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
    }

    public class ContadorSimple
    {
        public int Reprobadas { get; set; }
        public int Extraordinario { get; set; }
    }

    public class DatosReporte
    {
        public string NombreAlumno { get; set; }
        public string Matricula { get; set; }
        public string Carrera { get; set; }
        public bool AlumnoReprobado { get; set; }
        public int MateriasReprobadas { get; set; }
        public int MateriasExtraordinario { get; set; }
        public int MateriasAcreditadas { get; set; }
        public int MateriasPendientes { get; set; }
        public List<MateriaReporte> Materias { get; set; }
    }

    public class MateriaReporte
    {
        public int IdMateria { get; set; }
        public string NombreMateria { get; set; }
        public decimal? Calificacion { get; set; }
        public string Estado { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaExamenExtraordinario { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
    }

    public class ProcedimientoResultado
    {
        public int Success { get; set; }
        public string Mensaje { get; set; }
        public int IdMateria { get; set; }
        public int IdPersona { get; set; }
    }

    public class VerificacionResultado
    {
        public int IdPersona { get; set; }
        public int MateriasReprobadas { get; set; }
        public int MateriasExtraordinario { get; set; }
        public bool RequiereBaja { get; set; }
        public string Mensaje { get; set; }
        public string EstadoFinal { get; set; }
    }

    public class DatosPruebaDto
    {
        public string Matricula { get; set; }
        public string Nombre { get; set; }
        public string Materia { get; set; }
    }

    #endregion
}