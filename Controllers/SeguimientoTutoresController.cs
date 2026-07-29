using OfficeOpenXml;
using OfficeOpenXml.Style;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesPAT;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers
{
    // DTOs que estructuran la informacion para las vistas y APIs del modulo.

    // DTO principal: datos del tutor + contadores de progreso.
    public class TutorConEstadisticas
    {
        // *** Datos generales del tutor (vienen de la tabla Usuarios). *** //
        public int IdUsuario { get; set; }
        public string NombreCompleto { get; set; }
        public string UserName { get; set; }
        public string CorreoElectronico { get; set; }
        public int IdCarrera { get; set; }
        public string NombreCarrera { get; set; }
        public int IdNivel { get; set; }
        public bool Estado { get; set; }
        public string MicrosoftIdentifier { get; set; }

        // *** Contadores de progreso *** //
        // Los datos se obtienen mediante query en DB.

        // Estados de revision del PAT: 0=En Progreso, 1=En Revisión, 2=Aprobado, 3=Rechazado.
        public int EntrevistasRevisadas { get; set; } // Clasificados como vulnerable o no vulnerable en su seguimiento.
        public int PATsTotales { get; set; } // PATs con estado activo (1).
        public int PATsAprobados { get; set; } // PATs con EstadoRevision=2 y estado activo.
        public int TotalAlumnosActivos { get; set; } // Incluye bajas (cuando idsBajasFilter está vacío en DetallesTutor).
        public int TotalAlumnosActivosOnly { get; set; } // Solo activos. Valor default en la vista.
        public int EntrevistasRevisadasActivosOnly { get; set; } // Solo activos, default en badges.
        public int AlumnosEnBajasCount { get; set; }
        public int AlumnosEnArrastresCount { get; set; }
        public int AlumnosEnExtraordinarioCount { get; set; }
        public int SemanasCreadas { get; set; }
        public int SemanasIndividualesCompletadas { get; set; }
        public int SemanasGrupalesCompletadas { get; set; }
        public int TotalSemanas { get; set; } // Maximo de semanas definido por el master en DB para el periodo actual.

        // *** Datos para navegacion y acciones *** //
        public string TutorGrupo { get; set; } // Grupo mostrado en DetallesTutor (ej. "TI5A"). Si hay varios grupos, se itera por cada uno.
        public int? PatId { get; set; } // Id del PAT del tutor para enlazar a la vista de gestion.

        // Lista de alumnos del grupo (principalmente activos).
        public List<AlumnoDetalle> AlumnosTotalesList { get; set; } = new List<AlumnoDetalle>();
    }

    // DTO para cada fila de alumno en la tabla de DetallesTutor.
    public class AlumnoDetalle
    {
        public int IdPersona { get; set; }
        public string Matricula { get; set; }
        public string NombreCompleto { get; set; }
        public bool Estado { get; set; }
        public string Email { get; set; }
        public string Area { get; set; }

        // Grado, Grupo y Nomenclatura se concatenan para formar el grupo completo. Ej: TI5A
        public string Grado { get; set; }
        public string Grupo { get; set; }
        public string Nomenclatura { get; set; }
        public bool TieneEntrevista { get; set; } // Si tiene al menos un seguimiento en el periodo actual.
        public bool TieneEntrevistaRevisada { get; set; } // Si el primer seguimiento tiene Vulnerabilidad clasificada.
        public bool TieneBaja { get; set; }
        public int ArrastresCount { get; set; } // Materias con estado "Reprobada".
        public int ExtraordinariosCount { get; set; } // Materias con estado "Extraordinario".
    }

    // Extiende AlumnoDetalle con datos especificos de baja.
    public class AlumnoConBaja : AlumnoDetalle
    {
        // Los datos se obtienen mediante query en DB.
        public DateTime? FechaBaja { get; set; }
        public string MotivoBaja { get; set; }
    }

    // Extiende AlumnoDetalle con datos de materia (arrastre o extraordinario).
    public class AlumnoConMateria : AlumnoDetalle
    {
        // Los datos se obtienen mediante query en DB.
        public string Materia { get; set; }
        public string EstadoMateria { get; set; }
        public decimal? Calificacion { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public DateTime? FechaRegistro { get; set; }
        public DateTime? FechaActualizacion { get; set; }
        public int? IntentosExtraordinarios { get; set; }
        public DateTime? FechaExamenExtraordinario { get; set; }
        public int? CuatrimestreMateria { get; set; }
    }

    // DTO interno para contar materias agrupadas por alumno y estado.
    internal class ConteoMateriaDto
    {
        // Los datos se obtienen mediante query en DB.
        public int IdPersona { get; set; }
        public string Estado { get; set; }
        public int Cantidad { get; set; }
    }

    [ValidarSesion] // Filtro que verifica sesion activa y nivel >= 3 antes de cualquier accion.
    [CustomAuthorize(Nivel = 3)] // Restringe el controlador a nivel 3 (coordinador) o superior.
    public class SeguimientoTutoresController : Controller
    {
        private ModeloPlataforma modeloDb = new ModeloPlataforma();

        /*
         ───────────────────────────────────────── VISTAS GENERALES  ─────────────────────────────────────────
         */

        // GET: SeguimientoTutores/Index
        public ActionResult Index(bool incluirBajas = false)
        {
            try
            {
                var user = ObtenerUsuarioSesion();

                var (añoActual, periodoActual) = PeriodoActualData;

                string nombreCarrera = ObtenerNombreCarrera(modeloDb, user.IdCarrera);
                ViewBag.NombreCarrera = nombreCarrera;

                var tutores = ObtenerTutoresQuery(user, null, periodoActual, añoActual).ToList();
                if (!tutores.Any())
                {
                    ViewBag.IncluirBajas = incluirBajas;
                    SetIndexViewBag(user, null);
                    return View(new List<TutorConEstadisticas>());
                }

                var tutorIds = tutores.Select(t => t.IdUsuario).ToList();

                // Construye el identificador del grupo concatenando Nomenclatura + Grado + Grupo (ej. "TI5A").
                // Se unen 4 tablas (TutoriaGrupals -> Carreras -> Gradoes -> Grupoes) para obtener el nombre completo del grupo.
                var grupos = modeloDb.TutoriaGrupals
                    .Where(tg => tutorIds.Contains(tg.IdUsuario)
                              && tg.IdPeriodo == periodoActual
                              && tg.Año == añoActual)
                    .Join(modeloDb.Carreras, tg => tg.IdCarrera, c => c.IdCarrera, (tg, c) => new { tg, c })
                    .Join(modeloDb.Gradoes, x => x.tg.IdGrado, g => g.IdGrado, (x, g) => new { x.tg, x.c, g })
                    .Join(modeloDb.Grupoes, x => x.tg.IdGrupo, gr => gr.IdGrupo, (x, gr) => new
                    {
                        x.tg.IdUsuario,
                        Grupo = x.c.Nomenclatura + x.g.Nombre + gr.Nombre,
                        CarreraNombre = x.c.Nombre,
                        x.tg.IdCarrera,
                        x.tg.IdGrado,
                        x.tg.IdGrupo,
                        x.tg.IdTurno
                    })
                    .AsNoTracking()
                    .ToList();

                var tutoresDict = tutores.ToDictionary(t => t.IdUsuario);

                // Itera por cada grupo del tutor (un tutor puede tener varios grupos).
                // Cada grupo genera una fila separada en la tabla del Index.
                var result = grupos.Select(g =>
                {
                    var tutor = tutoresDict[g.IdUsuario];
                    var vm = MapTutorBase(tutor, g.CarreraNombre);
                    vm.TutorGrupo = g.Grupo;
                    vm.PatId = GetPatId(modeloDb, g.IdUsuario, periodoActual, añoActual);
                    return vm;
                }).ToList();

                ViewBag.IncluirBajas = incluirBajas;
                SetIndexViewBag(user, null);
                return View(result);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los tutores: " + ex.Message;
                ViewBag.UserNivel = 0;
                ViewBag.IncluirBajas = false;
                return View(new List<TutorConEstadisticas>());
            }
        }

        // GET: DetallesTutor/id        DetallesTutor/1
        // POST: DetallesTutor
        // Bajas / Arrastres / Extraordinario se obtienen mediante APIs.
        [LecturaPermitida]
        public ActionResult DetallesTutor(int id, string grupo = null)
        {
            try
            {
                var user = ObtenerUsuarioSesion();

                var (añoActual, periodoActual) = PeriodoActualData;

                var tutor = GetTutor(id);
                if (tutor == null) return HttpNotFound();

                if (string.IsNullOrEmpty(grupo)) return HttpNotFound();

                int? filtroIdGrado = null, filtroIdGrupo = null, filtroIdCarrera = null, filtroIdTutoriaGrupal = null;
                // Resuelve el grupo recibido como string (ej. "TI5A") a sus IDs reales en BD.
                // La segunda condicion (Any) asegura que la tutoria grupal existe para el periodo/año actual.
                var tgFiltro = modeloDb.TutoriaGrupals
                    .Join(modeloDb.Carreras, tg => tg.IdCarrera, c => c.IdCarrera, (tg, c) => new { tg, c })
                    .Join(modeloDb.Gradoes, x => x.tg.IdGrado, g => g.IdGrado, (x, g) => new { x.tg, x.c, g })
                    .Join(modeloDb.Grupoes, x => x.tg.IdGrupo, gr => gr.IdGrupo, (x, gr) => new
                    {
                        x.tg.IdTutoriaGrupal,
                        x.tg.IdUsuario,
                        x.tg.IdCarrera,
                        x.tg.IdGrado,
                        x.tg.IdGrupo,
                        ComputedKey = x.c.Nomenclatura + x.g.Nombre + gr.Nombre
                    })
                    .FirstOrDefault(x => x.IdUsuario == id
                                        && x.ComputedKey == grupo
                                        && modeloDb.TutoriaGrupals.Any(tg2 => tg2.IdUsuario == id
                                            && tg2.IdCarrera == x.IdCarrera
                                            && tg2.IdGrado == x.IdGrado
                                            && tg2.IdGrupo == x.IdGrupo
                                            && tg2.IdPeriodo == periodoActual
                                            && tg2.Año == añoActual));

                if (tgFiltro == null) return HttpNotFound();
                filtroIdCarrera = tgFiltro.IdCarrera;
                filtroIdGrado = tgFiltro.IdGrado;
                filtroIdGrupo = tgFiltro.IdGrupo;
                filtroIdTutoriaGrupal = tgFiltro.IdTutoriaGrupal;

                var (semanasContadas,semanasIndividualesCompletadas, semanasGrupalesCompletadas)
                    = ObtenerEstadisticasPAT(id, filtroIdTutoriaGrupal, periodoActual, añoActual);

                var idsBajasActivas = IdsBajasActivasData;
                var bajasSet = new HashSet<int>(idsBajasActivas);

                var carrera = modeloDb.Carreras.AsNoTracking().FirstOrDefault(c => c.IdCarrera == tutor.IdCarrera);

                // Obtiene los alumnos del tutor filtrando por grupo, periodo y año.
                // Usa JOIN compuesto (6 campos) para emparejar DatosPersonales con TutoriaGrupals.
                var alumnosTotales = modeloDb.DatosPersonales
                    .Join(modeloDb.TutoriaGrupals.Where(t => t.IdUsuario == id
                            && t.IdPeriodo == periodoActual
                            && t.Año == añoActual
                            && (filtroIdCarrera == null || t.IdCarrera == filtroIdCarrera)
                            && (filtroIdGrado == null || t.IdGrado == filtroIdGrado)
                            && (filtroIdGrupo == null || t.IdGrupo == filtroIdGrupo)),
                          dp => new { dp.IdCarrera, dp.IdGrado, dp.IdGrupo, dp.IdTurno, dp.IdPeriodo, dp.Año },
                          tg => new { tg.IdCarrera, tg.IdGrado, tg.IdGrupo, tg.IdTurno, tg.IdPeriodo, tg.Año },
                          (dp, tg) => dp)
                    .Join(modeloDb.Gradoes,
                          dp => dp.IdGrado,
                          g => g.IdGrado,
                          (dp, g) => new { dp, GradoNombre = g.Nombre })
                    .Join(modeloDb.Grupoes,
                          x => x.dp.IdGrupo,
                          gr => gr.IdGrupo,
                          (x, gr) => new { x.dp, x.GradoNombre, GrupoNombre = gr.Nombre })
                    .Join(modeloDb.Carreras,
                          x => x.dp.IdCarrera,
                          c => c.IdCarrera,
                          (x, c) => new AlumnoDetalle
                          {
                              IdPersona = x.dp.IdPersona,
                              Matricula = x.dp.Matricula,
                              NombreCompleto = x.dp.Nombre,
                              Estado = x.dp.Estado,
                              Email = x.dp.Email,
                              Area = x.dp.Area,
                              Grado = x.GradoNombre,
                              Grupo = x.GrupoNombre,
                              Nomenclatura = c.Nomenclatura
                          })
                    .Distinct()
                    .ToList();

                var idAlumnos = alumnosTotales.Select(a => a.IdPersona).Distinct().ToList();

                // idsBajasFilter vacio = incluye todos los alumnos (sin excluir bajas).
                var idsBajasFilter = new List<int>();

                var (vulnerables, noVulnerables, totalAlumnosActivos)
                    = ObtenerEstadisticasVulnerabilidad(idAlumnos, idsBajasFilter, añoActual, periodoActual);

                var idsConSeguimiento = ObtenerIdsAlumnosConSeguimiento(idAlumnos, idsBajasFilter, añoActual, periodoActual);
                var idsConEntrevistaRevisada = ObtenerIdsAlumnosConEntrevistaRevisada(idAlumnos, idsBajasFilter, añoActual, periodoActual);
                foreach (var a in alumnosTotales)
                {
                    a.TieneEntrevista = idsConSeguimiento.Contains(a.IdPersona);
                    a.TieneEntrevistaRevisada = idsConEntrevistaRevisada.Contains(a.IdPersona);
                    a.TieneBaja = bajasSet.Contains(a.IdPersona);
                }

                // statsSinBajas para el toggle client-side (default vista)
                var totalActivosOnly = alumnosTotales.Count(a => !a.TieneBaja);
                var entRevActivosOnly = alumnosTotales.Count(a => !a.TieneBaja && a.TieneEntrevista);

                // Consulta SQL directa para contar materias Reprobada y Extraordinario por alumno.
                // Se usa tabla temporal en memoria para evitar inyeccion SQL.
                var insertRows = string.Join(",", idAlumnos.Select(i => $"({i})"));
                var conteosSql = $@"
                    DECLARE @personas TABLE (IdPersona INT PRIMARY KEY);
                    INSERT INTO @personas (IdPersona) VALUES {insertRows};
                    SELECT IdPersona, Estado, COUNT(*) AS Cantidad
                    FROM MateriasAlumno
                    WHERE IdPersona IN (SELECT IdPersona FROM @personas)
                    GROUP BY IdPersona, Estado";
                var conteos = modeloDb.Database.SqlQuery<ConteoMateriaDto>(conteosSql).ToList();
                var arrastreLookup = conteos
                    .Where(c => c.Estado == "Reprobada")
                    .ToDictionary(c => c.IdPersona, c => c.Cantidad);
                var extraLookup = conteos
                    .Where(c => c.Estado == "Extraordinario")
                    .ToDictionary(c => c.IdPersona, c => c.Cantidad);
                foreach (var a in alumnosTotales)
                {
                    arrastreLookup.TryGetValue(a.IdPersona, out var arr);
                    extraLookup.TryGetValue(a.IdPersona, out var extra);
                    a.ArrastresCount = arr;
                    a.ExtraordinariosCount = extra;
                }

                var statsLookup = ObtenerEstadisticasBatch(new List<int> { id }, periodoActual, añoActual, filtroIdTutoriaGrupal);
                statsLookup.TryGetValue(id, out var stats);

                var vm = MapTutorBase(tutor, carrera?.Nombre);
                vm.TutorGrupo = grupo;
                vm.EntrevistasRevisadas = vulnerables + noVulnerables;
                vm.PATsTotales = stats.PATsTotales;
                vm.PATsAprobados = stats.PATsAprobados;
                vm.TotalAlumnosActivos = totalAlumnosActivos; // statsConBajas en JS
                vm.TotalAlumnosActivosOnly = totalActivosOnly; // statsSinBajas en JS
                vm.EntrevistasRevisadasActivosOnly = entRevActivosOnly;
                vm.AlumnosEnBajasCount = stats?.AlumnosEnBajasCount ?? 0;
                vm.AlumnosEnArrastresCount = stats?.AlumnosEnArrastresCount ?? 0;
                vm.AlumnosEnExtraordinarioCount = stats?.AlumnosEnExtraordinarioCount ?? 0;
                vm.SemanasCreadas = semanasContadas;
                vm.SemanasIndividualesCompletadas = semanasIndividualesCompletadas;
                vm.SemanasGrupalesCompletadas = semanasGrupalesCompletadas;
                vm.TotalSemanas = TotalSemanasCountData;
                vm.AlumnosTotalesList = alumnosTotales;

                return View(vm);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los tutores: " + ex.Message;
                ViewBag.UserNivel = 0;
                return View(new List<TutorConEstadisticas>());
            }
        }

        /*
         ───────────────────────────────────────── APIs POST (General)  ─────────────────────────────────────────
         */

        // POST: SeguimientoTutores/ActualizarMaxSemanas
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActualizarMaxSemanas(int maxSemanas)
        {
            try
            {
                var user = ObtenerUsuarioSesion();
                if (!Autorizar(user, 4, out var error)) return error;

                // Validacion: el maximo de semanas debe estar dentro del rango permitido.
                if (maxSemanas < 1 || maxSemanas > 20)
                    return JsonError("El valor debe estar entre 1 y 20.");

                var (año, periodo) = PeriodoActualData;
                var record = modeloDb.SemanasPeriodos
                    .FirstOrDefault(sp => sp.IdPeriodo == periodo && sp.Año == año);

                if (record == null)
                {
                    record = new SemanasPeriodo
                    {
                        IdPeriodo = periodo,
                        Año = año,
                        MaxSemanas = maxSemanas,
                        ModificadoPor = user.IdUsuario,
                        FechaCreacion = DateTime.Now
                    };
                    modeloDb.SemanasPeriodos.Add(record);
                }
                else
                {
                    record.MaxSemanas = maxSemanas;
                    record.ModificadoPor = user.IdUsuario;
                    record.FechaModificacion = DateTime.Now;
                }

                modeloDb.SaveChanges();
                return Json(new { success = true, maxSemanas = record.MaxSemanas });
            }
            catch (Exception ex)
            {
                return JsonError("Error al actualizar: " + ex.Message);
            }
        }

        /*
         ───────────────────────────────────────── APIs GET (para carga rapida de datos mediante AJAX)  ─────────────────────────────────────────
         */

        // GET SeguimientoTutores/ObtenerEstadisticasTodos?ids=1,2,3
        // Devuelve estadisticas agrupadas por grupo para la tabla del Index.
        // Cada grupo del tutor es una fila independiente (un tutor con 2 grupos devuelve 2 registros).
        [HttpGet]
        public ActionResult ObtenerEstadisticasTodos(string ids, bool incluirBajas = false)
        {
            try
            {
                if (string.IsNullOrEmpty(ids)) return Json(new object[0], JsonRequestBehavior.AllowGet);

                var tutorIds = ids.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var n) ? n : (int?)null)
                    .Where(n => n.HasValue).Select(n => n.Value).Distinct().ToList();
                if (!tutorIds.Any()) return Json(new object[0], JsonRequestBehavior.AllowGet);

                var (añoActual, periodoActual) = PeriodoActualData;

                // Sin idTutoriaGrupal, devuelve todos los grupos de cada tutor.
                var rows = EjecutarSqlEstadisticas(tutorIds, periodoActual, añoActual, incluirBajas: incluirBajas);

                var result = rows.Select(r => new
                {
                    r.IdUsuario,
                    r.TutorGrupo,
                    Bajas = r.AlumnosEnBajasCount,
                    Arrastres = r.AlumnosEnArrastresCount,
                    Extraordinarios = r.AlumnosEnExtraordinarioCount,
                    r.PATsTotales,
                    r.PATsAprobados,
                    r.SemanasCreadas,
                    TotalSemanas = TotalSemanasCountData,
                    SemanasIndividuales = r.SemanasIndividualesCompletadas,
                    SemanasGrupales = r.SemanasGrupalesCompletadas,
                    r.EntrevistasRevisadas,
                    r.TotalAlumnosActivos
                });

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }
        }

        // GET: SeguimientoTutores/ObtenerTutoresConEstadisticas?idCarrera=1
        // Usado por el filtro de carrera en Index via AJAX, sin recargar la pagina.
        // Devuelve datos del tutor + estadisticas filtrados por carrera.
        [HttpGet]
        public ActionResult ObtenerTutoresConEstadisticas(int? idCarrera, bool incluirBajas = false)
        {
            try
            {
                var user = ObtenerUsuarioSesion();
                var (añoActual, periodoActual) = PeriodoActualData;

                var tutores = ObtenerTutoresQuery(user, idCarrera, periodoActual, añoActual).ToList();
                if (!tutores.Any())
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);

                var tutorIds = tutores.Select(t => t.IdUsuario).ToList();
                var tutorDict = tutores.ToDictionary(t => t.IdUsuario);

                var grupos = modeloDb.TutoriaGrupals
                    .Where(tg => tutorIds.Contains(tg.IdUsuario)
                              && tg.IdPeriodo == periodoActual
                              && tg.Año == añoActual)
                    .Join(modeloDb.Carreras, tg => tg.IdCarrera, c => c.IdCarrera, (tg, c) => new { tg, c })
                    .Join(modeloDb.Gradoes, x => x.tg.IdGrado, g => g.IdGrado, (x, g) => new { x.tg, x.c, g })
                    .Join(modeloDb.Grupoes, x => x.tg.IdGrupo, gr => gr.IdGrupo, (x, gr) => new
                    {
                        x.tg.IdUsuario,
                        Grupo = x.c.Nomenclatura + x.g.Nombre + gr.Nombre,
                        CarreraNombre = x.c.Nombre,
                    })
                    .AsNoTracking()
                    .ToList();

                var statsRows = EjecutarSqlEstadisticas(tutorIds, periodoActual, añoActual, incluirBajas: incluirBajas);
                var statsByGroup = statsRows.ToDictionary(r => r.TutorGrupo);

                var result = grupos
                    .Where(g => tutorDict.ContainsKey(g.IdUsuario))
                    .Select(g =>
                {
                    var tutor = tutorDict[g.IdUsuario];
                    statsByGroup.TryGetValue(g.Grupo, out var s);
                    var patId = GetPatId(modeloDb, g.IdUsuario, periodoActual, añoActual);

                    return new
                    {
                        g.IdUsuario,
                        g.CarreraNombre,
                        tutor.NombreCompleto,
                        tutor.CorreoElectronico,
                        tutor.IdCarrera,
                        g.Grupo,
                        PatId = patId,
                        EntrevistasRevisadas = s?.EntrevistasRevisadas ?? 0,
                        TotalAlumnosActivos = s?.TotalAlumnosActivos ?? 0,
                        PATsTotales = s?.PATsTotales ?? 0,
                        PATsAprobados = s?.PATsAprobados ?? 0,
                        SemanasCreadas = s?.SemanasCreadas ?? 0,
                        TotalSemanas = TotalSemanasCountData,
                        SemanasIndividuales = s?.SemanasIndividualesCompletadas ?? 0,
                        SemanasGrupales = s?.SemanasGrupalesCompletadas ?? 0,
                        Bajas = s?.AlumnosEnBajasCount ?? 0,
                        Arrastres = s?.AlumnosEnArrastresCount ?? 0,
                        Extraordinarios = s?.AlumnosEnExtraordinarioCount ?? 0,
                    };
                }).ToList();

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        // GET: SeguimientoTutores/ObtenerSeccionTutorias?id=5&grupo=TI5A
        // Se utiliza para la vista DetallesTutor.
        [HttpGet]
        public ActionResult ObtenerSeccionTutorias(int id, string grupo)
        {
            try
            {
                var (añoActual, periodoActual) = PeriodoActualData;

                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodoActual, añoActual);

                var pat = BuildPatQuery(id, periodoActual, añoActual, idTutoriaGrupal).FirstOrDefault();
                if (pat == null)
                    return JsonError("El tutor no ha generado su Plan de Acción Tutorial (PAT).");

                var actividades = modeloDb.actividadesSemanals
                    .Include(x => x.Tipo)
                    .Include(x => x.Semana)
                    .Where(x => x.IdEntrevistaInicial == pat.IdEntrevistaInicial)
                    .OrderBy(x => x.IdSemana)
                    .ToList();

                if (!actividades.Any())
                {
                    return Json(new
                    {
                        success = true,
                        message = "No hay semanas o actividades configuradas para este PAT.",
                        estadoRevision = pat.EstadoRevision
                    }, JsonRequestBehavior.AllowGet);
                }

                var resultado = actividades.Select(a => new
                {
                    a.IdSemana,
                    SemanaNombre = a.Semana?.Nombre ?? "Semana " + a.IdSemana,
                    TipoActividad = a.Tipo?.Nombre ?? "N/A",
                    Nomenclatura = a.Tipo?.Nomenclatura ?? "",
                    Realizado = a.RealizoActividad
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = resultado,
                    estadoRevision = pat.EstadoRevision
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /SeguimientoTutores/ObtenerSeccionBajas?id=5&grupo=TI5A
        // Se utiliza para la vista DetallesTutor.
        [HttpGet]
        public ActionResult ObtenerSeccionBajas(int id, string grupo)
        {
            try
            {
                var (añoActual, periodoActual) = PeriodoActualData;
                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodoActual, añoActual);
                var bajas = ObtenerBajasParaExport(id, periodoActual, añoActual, idTutoriaGrupal);

                var idsBaja = bajas.Select(b => b.IdPersona).ToList();
                if (idsBaja.Any())
                {
                    var idsConSeguimiento = ObtenerIdsAlumnosConSeguimiento(idsBaja, new List<int>(), añoActual, periodoActual);
                    foreach (var b in bajas)
                    {
                        b.TieneEntrevista = idsConSeguimiento.Contains(b.IdPersona);
                    }
                }

                return Json(bajas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /SeguimientoTutores/ObtenerSeccionArrastres?id=5&grupo=TI5A
        // Se utiliza para la vista DetallesTutor.
        [HttpGet]
        public ActionResult ObtenerSeccionArrastres(int id, string grupo)
        {
            try 
            {
                var (añoActual, periodoActual) = PeriodoActualData;
                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodoActual, añoActual);
                var arrastres = ObtenerArrastresParaExport(id, periodoActual, añoActual, idTutoriaGrupal);
                return Json(arrastres, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /SeguimientoTutores/ObtenerSeccionExtraordinario?id=5&grupo=TI5A
        // Se utiliza para la vista DetallesTutor.
        [HttpGet]
        public ActionResult ObtenerSeccionExtraordinario(int id, string grupo)
        {
            try
            {
                var (añoActual, periodoActual) = PeriodoActualData;
                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodoActual, añoActual);
                var extraordinario = ObtenerExtraordinariosParaExport(id, periodoActual, añoActual, idTutoriaGrupal);
                return Json(extraordinario, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /SeguimientoTutores/ObtenerConteosExportacion?id=5&grupo=TI5A&incluirBajas=false
        [HttpGet]
        public ActionResult ObtenerConteosExportacion(int id, string grupo, bool incluirBajas = false)
        {
            try
            {
                var (añoActual, periodoActual) = PeriodoActualData;
                var stats = EjecutarSqlEstadisticas(new List<int> { id }, periodoActual, añoActual, incluirBajas: incluirBajas)
                    .FirstOrDefault(s => s.TutorGrupo == grupo);

                return Json(new
                {
                    bajas = stats?.AlumnosEnBajasCount ?? 0,
                    arrastres = stats?.AlumnosEnArrastresCount ?? 0,
                    extraordinarios = stats?.AlumnosEnExtraordinarioCount ?? 0
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }
        }

        /*
         ───────────────────────────────────────── Excel Export actions (Index)  ─────────────────────────────────────────
         */

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarEntrevistasGlobal(int? idCarrera, bool incluirBajas = false)
        {
            modeloDb.Database.CommandTimeout = 120;
            try
            {
                var user = ObtenerUsuarioSesion();

                var (año, periodo) = PeriodoActualData;

                var tutores = ObtenerTutoresQuery(user, idCarrera, periodo, año).AsNoTracking().ToList();

                var idsBajas = IdsBajasActivasData;

                var filas = tutores.SelectMany(t =>
                {
                    var grupos = modeloDb.TutoriaGrupals
                        .Where(tg => tg.IdUsuario == t.IdUsuario && tg.IdPeriodo == periodo && tg.Año == año)
                        .Join(modeloDb.Carreras, tg => tg.IdCarrera, c => c.IdCarrera, (tg, c) => new { tg, c })
                        .Join(modeloDb.Gradoes, x => x.tg.IdGrado, g => g.IdGrado, (x, g) => new { x.tg, x.c, g })
                        .Join(modeloDb.Grupoes, x => x.tg.IdGrupo, gr => gr.IdGrupo, (x, gr) => new
                        {
                            x.tg.IdTutoriaGrupal,
                            Grupo = x.c.Nomenclatura + x.g.Nombre + gr.Nombre
                        })
                        .ToList();

                    return grupos.Select(g =>
                    {
                        var datos = ObtenerDatosTutor(t.IdUsuario, periodo, año, g.IdTutoriaGrupal, incluirBajas);
                        return (
                            grupo: g.Grupo,
                            nombreTutor: t.NombreCompleto,
                            alumnosTotales: datos.alumnosActivos,
                            datos.entrevistas,
                            datos.estadoRevision,
                            semanas: datos.actividades.Select(a => (a.IdSemana, (bool?)a.RealizoActividad, a.Tipo?.Nombre ?? "")).ToList()
                        );
                    });
                }).OrderBy(f => NaturalSortKey(f.grupo)).ToList();

                string nombreArchivoTutorias = user.IdNivel >= 4 ? "Entrevistas_Global" : ObtenerNombreCarrera(modeloDb, user.IdCarrera);

                return GenerarArchivoExcel("Revisión_Plataforma", pkg =>
                {
                    EscribirHojaTutoriasGlobal(pkg.Workbook.Worksheets.Add(nombreArchivoTutorias), filas);
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarArrastresGlobal(int? idCarrera, bool incluirBajas = false)
        {
            modeloDb.Database.CommandTimeout = 120;
            try
            {
                var user = ObtenerUsuarioSesion();
                var (año, periodo) = PeriodoActualData;

                var tutores = ObtenerTutoresQuery(user, idCarrera, periodo, año).AsNoTracking().ToList();

                var filas = tutores.SelectMany(t =>
                {
                    var grupos = modeloDb.TutoriaGrupals
                        .Where(tg => tg.IdUsuario == t.IdUsuario && tg.IdPeriodo == periodo && tg.Año == año)
                        .Join(modeloDb.Carreras, tg => tg.IdCarrera, c => c.IdCarrera, (tg, c) => new { tg, c })
                        .Join(modeloDb.Gradoes, x => x.tg.IdGrado, g => g.IdGrado, (x, g) => new { x.tg, x.c, g })
                        .Join(modeloDb.Grupoes, x => x.tg.IdGrupo, gr => gr.IdGrupo, (x, gr) => new
                        {
                            x.tg.IdTutoriaGrupal,
                            Grupo = x.c.Nomenclatura + x.g.Nombre + gr.Nombre
                        })
                        .ToList();

                    return grupos.Select(g => (
                        Grupo: g.Grupo,
                        NombreTutor: t.NombreCompleto,
                        Alumnos: ObtenerMateriasConTempTable(t.IdUsuario, "Reprobada", periodo, año, g.IdTutoriaGrupal, incluirBajas)
                    ));
                })
                .Where(f => f.Alumnos.Any())
                .OrderBy(f => NaturalSortKey(f.Grupo))
                .ToList();

                var porTutor = filas
                    .GroupBy(f => f.NombreTutor)
                    .Select(g => (NombreTutor: g.Key, Alumnos: g.SelectMany(f => f.Alumnos).ToList()))
                    .ToList();

                string nombreArchivoArrastres = user.IdNivel >= 4 ? "Arrastres_Global" : "Arrastres";

                return GenerarArchivoExcel("Materias_Arrastres", pkg => {
                    EscribirHojaMateriaGlobal(pkg.Workbook.Worksheets.Add(nombreArchivoArrastres), porTutor);
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        /*
         ───────────────────────────────────────── Excel Export actions (DetallesTutor)  ─────────────────────────────────────────
         */

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarReporteGeneral(int id, string grupo = null, bool incluirBajas = false)
        {
            try 
            {
                modeloDb.Database.CommandTimeout = 120;
                var user = ObtenerUsuarioSesion();

                var tutor = GetTutor(id);
                if (tutor == null) return HttpNotFound();

                var (año, periodo) = PeriodoActualData;
                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodo, año);
                var datos = ObtenerDatosTutor(id, periodo, año, idTutoriaGrupal, incluirBajas);

                string grupoLabel = !string.IsNullOrEmpty(grupo) ? grupo : GetGrupoLabelFallback(id);

                string nombreArchivoTutorias = user.IdNivel >= 4 ? "Tutorias" : ObtenerNombreCarrera(modeloDb, user.IdCarrera);

                return GenerarArchivoExcel($"ReporteGeneral_{tutor.NombreCompleto}", pkg => {
                    EscribirHojaTutorias(pkg.Workbook.Worksheets.Add(nombreArchivoTutorias), tutor, grupoLabel, datos.alumnosActivos, datos.entrevistas, datos.estadoRevision, datos.actividades);
                    EscribirHojaBajas(pkg.Workbook.Worksheets.Add("Bajas"), tutor.NombreCompleto, ObtenerBajasParaExport(id, periodo, año, idTutoriaGrupal));
                    EscribirHojaMateria(pkg.Workbook.Worksheets.Add("Arrastres"), tutor.NombreCompleto, ObtenerArrastresParaExport(id, periodo, año, idTutoriaGrupal, incluirBajas));
                    EscribirHojaMateria(pkg.Workbook.Worksheets.Add("Extraordinario"), tutor.NombreCompleto, ObtenerExtraordinariosParaExport(id, periodo, año, idTutoriaGrupal, incluirBajas));
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarTutorias(int id, string grupo = null, bool incluirBajas = false)
        {
            try
            {
                var user = ObtenerUsuarioSesion();
                var tutor = GetTutor(id);
                if (tutor == null) return HttpNotFound();

                var (año, periodo) = PeriodoActualData;
                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodo, año);
                var datos = ObtenerDatosTutor(id, periodo, año, idTutoriaGrupal, incluirBajas);

                string grupoLabel = !string.IsNullOrEmpty(grupo) ? grupo : GetGrupoLabelFallback(id);

                return GenerarArchivoExcel($"Seguimiento_PAT_{tutor.NombreCompleto}", pkg =>
                {
                    EscribirHojaTutorias(pkg.Workbook.Worksheets.Add("Seguimiento Semanal"), tutor, grupoLabel, datos.alumnosActivos, datos.entrevistas, datos.estadoRevision, datos.actividades);
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarBajas(int id, string grupo = null)
        {
            try
            {
                var user = ObtenerUsuarioSesion();
                var tutor = GetTutor(id);
                if (tutor == null) return HttpNotFound();

                var (año, periodo) = PeriodoActualData;

                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodo, año);

                return GenerarArchivoExcel($"Bajas_{tutor.NombreCompleto}", pkg =>
                {
                    EscribirHojaBajas(pkg.Workbook.Worksheets.Add("Bajas"), tutor.NombreCompleto, ObtenerBajasParaExport(id, periodo, año, idTutoriaGrupal));
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarArrastres(int id, string grupo = null, bool incluirBajas = false)
        {
            try
            {
                var user = ObtenerUsuarioSesion();
                var tutor = GetTutor(id);
                if (tutor == null) return HttpNotFound();

                var (año, periodo) = PeriodoActualData;

                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodo, año);

                return GenerarArchivoExcel($"Arrastres_{tutor.NombreCompleto}", pkg =>
                {
                    EscribirHojaMateria(pkg.Workbook.Worksheets.Add("Arrastres"), tutor.NombreCompleto, ObtenerArrastresParaExport(id, periodo, año, idTutoriaGrupal, incluirBajas));
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarExtraordinario(int id, string grupo = null, bool incluirBajas = false)
        {
            try
            {
                var user = ObtenerUsuarioSesion();

                var tutor = GetTutor(id);
                if (tutor == null) return HttpNotFound();

                var (año, periodo) = PeriodoActualData;

                var idTutoriaGrupal = ResolverIdTutoriaGrupal(id, grupo, periodo, año);

                return GenerarArchivoExcel($"Extraordinario_{tutor.NombreCompleto}", pkg =>
                {
                    EscribirHojaMateria(pkg.Workbook.Worksheets.Add("Extraordinario"), tutor.NombreCompleto, ObtenerExtraordinariosParaExport(id, periodo, año, idTutoriaGrupal, incluirBajas));
                }, user.IdNivel);
            }
            catch (Exception ex) when (EsCancelacion(ex))
            {
                return new HttpStatusCodeResult(499, "Cancelado");
            }
        }

        /*
         ───────────────────────────────────────── Funciones auxiliares (Obtener datos para Excel Export)  ─────────────────────────────────────────
         */

        private int? ResolverIdTutoriaGrupal(int idTutor, string grupo, int periodo, int anio)
        {
            if (string.IsNullOrEmpty(grupo)) return null;

            return modeloDb.TutoriaGrupals
                .Join(modeloDb.Carreras, tg => tg.IdCarrera, c => c.IdCarrera, (tg, c) => new { tg, c })
                .Join(modeloDb.Gradoes, x => x.tg.IdGrado, g => g.IdGrado, (x, g) => new { x.tg, x.c, g })
                .Join(modeloDb.Grupoes, x => x.tg.IdGrupo, gr => gr.IdGrupo, (x, gr) => new { x.tg, x.c, x.g, gr })
                .Where(x => x.tg.IdUsuario == idTutor
                         && x.tg.IdPeriodo == periodo
                         && x.tg.Año == anio
                         && (x.c.Nomenclatura + x.g.Nombre + x.gr.Nombre) == grupo)
                .Select(x => (int?)x.tg.IdTutoriaGrupal)
                .FirstOrDefault();
        }

        // Configuracion generica: nombre y creacion del archivo.
        private ActionResult GenerarArchivoExcel(string fileName, Action<ExcelPackage> configuracion, int userNivel = 0)
        {
            // Agregar la terminacion "Master" al archivo excel si el usuario es master.
            if (userNivel >= 4) fileName += "_Master";

            using (var package = new ExcelPackage())
            {
                configuracion(package);
                var bytes = package.GetAsByteArray();
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}_{DateTime.Now:dd-MM-yyyy}.xlsx");
            }
        }

        // Datos del tutor
        private (int alumnosActivos, int entrevistas, int estadoRevision, List<ActividadesSemanal> actividades) ObtenerDatosTutor(int idTutor, int periodo, int año, int? idTutoriaGrupal = null, bool incluirBajas = false)
        {
            var pat = BuildPatQuery(idTutor, periodo, año, idTutoriaGrupal).FirstOrDefault();
            var estadoRevision = pat?.EstadoRevision ?? 0;

            var actividades = pat != null
                ? modeloDb.actividadesSemanals
                    .Include(x => x.Tipo)
                    .Where(x => x.IdEntrevistaInicial == pat.IdEntrevistaInicial).ToList()
                : new List<ActividadesSemanal>();

            var idsAlumnos = ObtenerIdPersonasDeTutor(idTutor, periodo, año, idTutoriaGrupal);
            var idsBajas = incluirBajas ? new List<int>() : IdsBajasActivasData;
            int alumnosActivos = idsAlumnos.Distinct().Count(idA => !idsBajas.Contains(idA));

            var (vuln, noVuln, _) = ObtenerEstadisticasVulnerabilidad(idsAlumnos, idsBajas, año, periodo);

            return (alumnosActivos, vuln + noVuln, estadoRevision, actividades);
        }

        // Tabla temporal para obtener materias.
        private List<AlumnoConMateria> ObtenerMateriasConTempTable(int idTutor, string estado, int periodo, int anio, int? idTutoriaGrupal = null, bool incluirBajas = false)
        {
                var idPersonas = ObtenerIdPersonasDeTutor(idTutor, periodo, anio, idTutoriaGrupal);
                if (!incluirBajas)
                {
                    var idsBajas = IdsBajasActivasData;
                    idPersonas = idPersonas.Where(id => !idsBajas.Contains(id)).ToList();
                }
                if (!idPersonas.Any()) return new List<AlumnoConMateria>();

                var insertRows = string.Join(",", idPersonas.Select(i => $"({i})"));

                // Tabla temporal en memoria (@personas) para pasar los IDs de alumnos al SQL plano.
                // Evita problemas de rendimiento con IN lists muy largas y permite reutilizar los IDs.
                var sql = $@"
                DECLARE @personas TABLE (IdPersona INT PRIMARY KEY);
                INSERT INTO @personas (IdPersona) VALUES {insertRows};

                SELECT DISTINCT
                    dp.IdPersona,
                    dp.Matricula,
                    dp.Nombre            AS NombreCompleto,
                    dp.Estado,
                    dp.Email,
                    dp.Area,
                    g.Nombre             AS Grado,
                    gr.Nombre            AS Grupo,
                    c.Nomenclatura,
                    m.Nombre             AS Materia,
                    m.IdGrado            AS CuatrimestreMateria,
                    ma.Estado            AS EstadoMateria,
                    ma.Calificacion,
                    ma.FechaInicioArrastre,
                    ma.FechaRegistro,
                    ma.FechaActualizacion,
                    ma.IntentosExtraordinarios,
                    ma.FechaExamenExtraordinario
                FROM  [Tutorias].[dbo].[MateriasAlumno]   ma
                INNER JOIN [Tutorias].[dbo].[DatosPersonales] dp ON dp.IdPersona  = ma.IdPersona
                INNER JOIN [Tutorias].[dbo].[Materias]        m  ON m.IdMateria   = ma.IdMateria
                INNER JOIN [Tutorias].[dbo].[Gradoes]         g  ON g.IdGrado     = dp.IdGrado
                INNER JOIN [Tutorias].[dbo].[Grupoes]         gr ON gr.IdGrupo    = dp.IdGrupo
                INNER JOIN [Tutorias].[dbo].[Carreras]        c  ON c.IdCarrera   = dp.IdCarrera
                WHERE ma.Estado     = @estado
                  AND dp.IdPersona IN (SELECT IdPersona FROM @personas);";

            return modeloDb.Database
                .SqlQuery<AlumnoConMateria>(sql, new SqlParameter("@estado", estado))
                .ToList();
        }

        private List<AlumnoConBaja> ObtenerBajasParaExport(int idTutor, int periodo, int anio, int? idTutoriaGrupal = null)
        {
            var idPersonas = ObtenerIdPersonasDeTutor(idTutor, periodo, anio, idTutoriaGrupal);
            if (!idPersonas.Any()) return new List<AlumnoConBaja>();

            return modeloDb.Bajas
                .Where(b => b.Activo == true && idPersonas.Contains(b.IdPersona))
                .Join(modeloDb.DatosPersonales,
                      b => b.IdPersona,
                      dp => dp.IdPersona,
                      (b, dp) => new AlumnoConBaja
                      {
                          IdPersona = dp.IdPersona,
                          Matricula = dp.Matricula,
                          NombreCompleto = dp.Nombre,
                          Estado = dp.Estado,
                          FechaBaja = b.Fecha,
                          MotivoBaja = b.Causa
                      })
                .ToList();
        }

        private List<AlumnoConMateria> ObtenerArrastresParaExport(int idTutor, int periodo, int anio, int? idTutoriaGrupal = null, bool incluirBajas = false)
            => ObtenerMateriasConTempTable(idTutor, "Reprobada", periodo, anio, idTutoriaGrupal, incluirBajas);

        private List<AlumnoConMateria> ObtenerExtraordinariosParaExport(int idTutor, int periodo, int anio, int? idTutoriaGrupal = null, bool incluirBajas = false)
            => ObtenerMateriasConTempTable(idTutor, "Extraordinario", periodo, anio, idTutoriaGrupal, incluirBajas);


        /*
        ───────────────────────────────────────── Funciones auxiliares (Crear el archivo Excel con datos)  ─────────────────────────────────────────
        */

        // Se utiliza en Index para datos de todos los tutores.
        // Unifies both the Global and Single Tutor versions for Tutoring sheets
        private void EscribirHojaTutoriasGeneric(
            ExcelWorksheet ws,
            List<(string Grupo, string NombreTutor, int AlumnosTotales, int EntrevistasRevisadas, int estadoRevision, List<(int IdSemana, bool? Realizado, string TipoNombre)> Semanas)> filas)
        {
            // Fixed cols: Grupo, Tutor, Alumnos Totales, En Plataforma, Faltantes, PAT
            // Then: totalSemanas individuales | totalSemanas grupales | Observaciones
            int totalSemanas = TotalSemanasCountData;

            int colPat = 6;
            int colInicioInd = colPat + 1;
            int colFinInd = colInicioInd + totalSemanas - 1;
            int colInicioGrup = colFinInd + 1;
            int colFinGrup = colInicioGrup + totalSemanas - 1;
            int colObs = colFinGrup + 1;
            int totalColumnasHeader = colObs;

            ConfigureBaseFontAndFreeze(ws, freezeRow: 3, freezeCol: 1);
            ApplyHeaderStyle(ws.Cells[1, 1, 2, totalColumnasHeader]);

            // 1. Fixed column headers (rows 1-2 merged)
            string[] fijos = { "GRUPO", "TUTOR", "ALUMNOS TOTALES", "EN PLATAFORMA", "FALTANTES" };
            for (int i = 0; i < fijos.Length; i++)
            {
                ws.Cells[1, i + 1, 2, i + 1].Merge = true;
                ws.Cells[1, i + 1].Value = fijos[i];
            }

            // 2. PAT header
            ws.Cells[1, colPat, 2, colPat].Merge = true;
            ws.Cells[1, colPat].Value = "PAT";
            ApplySpecificHeaderStyle(ws.Cells[1, colPat], Color.FromArgb(44, 62, 80));

            // 3. Individuales header block
            if (totalSemanas > 0)
            {
                var hdrInd = ws.Cells[1, colInicioInd, 1, colFinInd];
                hdrInd.Merge = true;
                hdrInd.Value = "TUTORÍAS INDIVIDUALES";
                ApplySpecificHeaderStyle(hdrInd, Color.FromArgb(21, 101, 192));
                hdrInd.Style.Border.BorderAround(ExcelBorderStyle.Medium);

                for (int i = 1; i <= totalSemanas; i++)
                {
                    var hdr = ws.Cells[2, colInicioInd + i - 1];
                    hdr.Value = $"S{i}";
                    ApplySpecificHeaderStyle(hdr, Color.FromArgb(30, 136, 229));
                }

                // 4. Grupales header block
                var hdrGrup = ws.Cells[1, colInicioGrup, 1, colFinGrup];
                hdrGrup.Merge = true;
                hdrGrup.Value = "TUTORÍAS GRUPALES";
                ApplySpecificHeaderStyle(hdrGrup, Color.FromArgb(27, 94, 32));
                hdrGrup.Style.Border.BorderAround(ExcelBorderStyle.Medium);

                for (int i = 1; i <= totalSemanas; i++)
                {
                    var hdr = ws.Cells[2, colInicioGrup + i - 1];
                    hdr.Value = $"S{i}";
                    ApplySpecificHeaderStyle(hdr, Color.FromArgb(56, 142, 60));
                }
            }

            // 5. Observaciones header
            CreateMergedHeaderCell(ws.Cells[1, colObs, 2, colObs], "OBSERVACIONES", Color.DarkSlateGray);

            // 6. Data rows
            int row = 3;
            foreach (var fila in filas)
            {
                ws.Cells[row, 1].Value = fila.Grupo;
                ws.Cells[row, 2].Value = fila.NombreTutor;
                ws.Cells[row, 3].Value = fila.AlumnosTotales;
                ws.Cells[row, 4].Value = fila.EntrevistasRevisadas;
                ws.Cells[row, 5].Value = Math.Max(0, fila.AlumnosTotales - fila.EntrevistasRevisadas);
                ws.Cells[row, 1, row, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // PAT status cell
                var celdaPat = ws.Cells[row, colPat];
                celdaPat.Style.Fill.PatternType = ExcelFillStyle.Solid;
                celdaPat.Value =
                    fila.estadoRevision == 1 ? "*" :
                    fila.estadoRevision == 2 ? "✓" :
                    fila.estadoRevision == 3 ? "X" : "-";
                celdaPat.Style.Fill.BackgroundColor.SetColor(
                    fila.estadoRevision == 1 ? Color.Khaki :
                    fila.estadoRevision == 2 ? Color.LightGreen :
                    fila.estadoRevision == 3 ? Color.LightCoral : Color.LightGray);
                celdaPat.Style.Font.Color.SetColor(
                    fila.estadoRevision == 1 ? Color.DarkGoldenrod :
                    fila.estadoRevision == 2 ? Color.DarkGreen :
                    fila.estadoRevision == 3 ? Color.DarkRed : Color.DimGray);
                celdaPat.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                celdaPat.Style.Border.BorderAround(ExcelBorderStyle.Thin);

                // Helper: render a yes/no/null cell for a given week and activity type
                void RenderWeekCell(int col, int semana, string tipoNombre)
                {
                    var entry = fila.Semanas.FirstOrDefault(s =>
                        s.IdSemana == semana &&
                        s.TipoNombre.Equals(tipoNombre, StringComparison.OrdinalIgnoreCase));

                    var celda = ws.Cells[row, col];
                    celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    celda.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    celda.Style.Border.BorderAround(ExcelBorderStyle.Thin);

                    // entry.IdSemana == 0 means no match (default struct)
                    bool hasEntry = fila.Semanas.Any(s =>
                        s.IdSemana == semana &&
                        s.TipoNombre.Equals(tipoNombre, StringComparison.OrdinalIgnoreCase));

                    if (!hasEntry)
                    {
                        // null — semana no registrada para este tipo
                        celda.Value = "-";
                        celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(224, 224, 224));
                        celda.Style.Font.Color.SetColor(Color.Gray);
                    }
                    else if (entry.Realizado == true)
                    {
                        celda.Value = "✓";
                        celda.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                        celda.Style.Font.Color.SetColor(Color.DarkGreen);
                    }
                    else
                    {
                        celda.Value = "X";
                        celda.Style.Fill.BackgroundColor.SetColor(Color.Salmon);
                        celda.Style.Font.Color.SetColor(Color.DarkRed);
                    }
                }

                for (int i = 1; i <= totalSemanas; i++)
                {
                    RenderWeekCell(colInicioInd + i - 1, i, "Tutoria Individual");
                    RenderWeekCell(colInicioGrup + i - 1, i, "Tutoria Grupal");
                }

                ApplyZebraStriping(ws.Cells[row, 1, row, 5], row, Color.FromArgb(242, 242, 242));
                row++;
            }

            // 7. Borders and column widths
            if (row > 3) ApplyBorderToRange(ws.Cells[2, 1, row - 1, colObs]);

            int[] widths = { 10, 45, 20, 18, 12 };
            for (int idx = 0; idx < widths.Length; idx++) ws.Column(idx + 1).Width = widths[idx];

            ws.Column(colPat).Width = 7;
            for (int c = colInicioInd; c <= colFinGrup; c++) ws.Column(c).Width = 6;
            ws.Column(colObs).Width = 40;
        }

        // Wrapper for the Global Tutoring sheet signature
        private void EscribirHojaTutoriasGlobal(
            ExcelWorksheet ws,
            List<(string Grupo, string NombreTutor, int AlumnosTotales, int EntrevistasRevisadas, int estadoRevision, List<(int IdSemana, bool? Realizado, string TipoNombre)> Semanas)> filas)
        {
            EscribirHojaTutoriasGeneric(ws, filas);
        }

        // Wrapper for the Individual Tutoring sheet signature
        private void EscribirHojaTutorias(ExcelWorksheet ws, Usuario tutor, string grupoLabel, int alumnosActivos, int entrevistas, int estadoRevision, List<ActividadesSemanal> actividades)
        {
            string grupo = GetGrupoLabelFallback(tutor.IdUsuario);

            var singleTutorData = new List<(string Grupo, string NombreTutor, int AlumnosTotales, int EntrevistasRevisadas, int EstadoRevision, List<(int IdSemana, bool? Realizado, string TipoNombre)> Semanas)>
            {
                (
                    Grupo: grupoLabel,
                    NombreTutor: tutor.NombreCompleto,
                    AlumnosTotales: alumnosActivos,
                    EntrevistasRevisadas: entrevistas,
                    EstadoRevision: estadoRevision,
                    Semanas: actividades.Select(a => (a.IdSemana, (bool?)a.RealizoActividad, a.Tipo?.Nombre ?? "")).ToList()
                )
            };

            EscribirHojaTutoriasGeneric(ws, singleTutorData);
        }

        public void EscribirHojaBajas(ExcelWorksheet ws, string nombreTutor, List<AlumnoConBaja> bajas)
        {
            ConfigureBaseFontAndFreeze(ws, freezeRow: 4, freezeCol: 1);

            // Tutor Bar Header
            ws.Cells[1, 1].Value = "Tutor:";
            ws.Cells[1, 2].Value = nombreTutor;
            var tutorRange = ws.Cells[1, 1, 1, 2];
            tutorRange.Style.Font.Bold = true;
            tutorRange.Style.Font.Size = 11;
            tutorRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            tutorRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(58, 63, 74));
            tutorRange.Style.Font.Color.SetColor(Color.White);
            tutorRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);

            // Main Headers
            string[] headers = { "Matrícula", "Nombre", "Fecha de Baja", "Motivo" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[3, i + 1];
                cell.Value = headers[i];
                ApplySpecificHeaderStyle(cell, Color.FromArgb(72, 84, 96));
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
            ws.Row(3).Height = 20;

            int row = 4;
            if (!bajas.Any())
            {
                var emptyCellRange = ws.Cells[row, 1, row, 4];
                emptyCellRange.Merge = true;
                ws.Cells[row, 1].Value = "No hay bajas registradas.";
                ws.Cells[row, 1].Style.Font.Italic = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(Color.Gray);
                ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(245, 245, 245));
                ApplyBorderToRange(emptyCellRange);
                row++;
            }
            else
            {
                foreach (var a in bajas)
                {
                    ws.Cells[row, 1].Value = a.Matricula;
                    ws.Cells[row, 2].Value = a.NombreCompleto;
                    ws.Cells[row, 3].Value = a.FechaBaja;
                    ws.Cells[row, 3].Style.Numberformat.Format = "dd/MM/yyyy";
                    ws.Cells[row, 4].Value = string.IsNullOrWhiteSpace(a.MotivoBaja) ? "—" : a.MotivoBaja;

                    ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[row, 4].Style.WrapText = true;

                    ApplyZebraStriping(ws.Cells[row, 1, row, 4], row, Color.FromArgb(236, 239, 241));
                    ApplyBorderToRange(ws.Cells[row, 1, row, 4]);
                    row++;
                }
            }

            ws.Cells[3, 1, row - 1, 4].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Cells.AutoFitColumns();
            ws.Column(1).Width = 16; ws.Column(2).Width = 34; ws.Column(3).Width = 16; ws.Column(4).Width = 42;
        }

        // Funcion reutilizable para EscribirHojaMateriaGlobal y EscribirHojaMateria.
        private void EscribirHojaMateriaGeneric(ExcelWorksheet ws, string labelTutorHeader, List<(string NombreTutor, List<AlumnoConMateria> Alumnos)> porTutor, bool isGlobal)
        {
            ConfigureBaseFontAndFreeze(ws, freezeRow: isGlobal ? 3 : 4, freezeCol: 1);

            int startHeaderRow = isGlobal ? 1 : 2;
            int subHeaderRow = startHeaderRow + 1;
            int dataStartRow = subHeaderRow + 1;

            if (!isGlobal && !string.IsNullOrEmpty(labelTutorHeader))
            {
                ws.Cells[1, 1].Value = "Tutor:";
                ws.Cells[1, 2].Value = labelTutorHeader;
                ws.Cells[1, 1, 1, 2].Style.Font.Bold = true;
                ws.Cells[1, 1, 1, 2].Style.Font.Size = 11;
            }

            var todosAlumnos = porTutor.SelectMany(t => t.Alumnos).ToList();
            var cuatrimestres = todosAlumnos.Where(a => a.CuatrimestreMateria.HasValue)
                                            .Select(a => a.CuatrimestreMateria.Value)
                                            .Distinct().OrderBy(c => c).ToList();

            int baseColsCount = isGlobal ? 3 : 2;
            int lastCol = baseColsCount + cuatrimestres.Count;

            if (cuatrimestres.Any())
            {
                var topHeader = ws.Cells[startHeaderRow, baseColsCount + 1, startHeaderRow, lastCol];
                CreateMergedHeaderCell(topHeader, "Cuatrimestre", Color.FromArgb(31, 78, 120));
                if (!isGlobal) topHeader.Style.Border.BorderAround(ExcelBorderStyle.Medium);

                for (int i = 0; i < cuatrimestres.Count; i++)
                {
                    var hdr = ws.Cells[subHeaderRow, baseColsCount + 1 + i];
                    hdr.Value = cuatrimestres[i];
                    ApplySpecificHeaderStyle(hdr, Color.FromArgb(44, 62, 80));
                }
            }

            string[] standardHeaders = isGlobal ? new[] { "Tutor", "Grupo", "Nombre" } : new[] { "Nombre", "Grupo" };
            for (int i = 0; i < standardHeaders.Length; i++)
            {
                ws.Cells[subHeaderRow, i + 1].Value = standardHeaders[i];
            }

            var subHeaderRange = ws.Cells[subHeaderRow, 1, subHeaderRow, lastCol];
            ApplyHeaderStyle(subHeaderRange);
            subHeaderRange.Style.Fill.BackgroundColor.SetColor(isGlobal ? Color.FromArgb(44, 62, 80) : Color.DarkSlateGray);

            int row = dataStartRow;
            if (!todosAlumnos.Any())
            {
                ws.Cells[row, 1].Value = "No hay registros.";
                ws.Cells[row, 1, row, Math.Max(lastCol, baseColsCount + 1)].Merge = true;
                ws.Cells[row, 1].Style.Font.Italic = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(Color.Gray);
                ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                row++;
            }
            else
            {
                foreach (var (nombreTutor, alumnos) in porTutor)
                {
                    var grupos = alumnos.GroupBy(a => new { a.NombreCompleto, a.Nomenclatura, a.Grado, a.Grupo });
                    foreach (var g in grupos)
                    {
                        if (isGlobal)
                        {
                            ws.Cells[row, 1].Value = nombreTutor;
                            ws.Cells[row, 2].Value = $"{g.Key.Nomenclatura}{g.Key.Grado}{g.Key.Grupo}";
                            ws.Cells[row, 3].Value = g.Key.NombreCompleto;
                            ws.Cells[row, 1, row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }
                        else
                        {
                            ws.Cells[row, 1].Value = g.Key.NombreCompleto;
                            ws.Cells[row, 2].Value = $"{g.Key.Nomenclatura}{g.Key.Grado}{g.Key.Grupo}";
                            ws.Cells[row, 1, row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }

                        for (int i = 0; i < cuatrimestres.Count; i++)
                        {
                            int cuat = cuatrimestres[i];
                            var materias = g.Where(m => m.CuatrimestreMateria == cuat).Select(m => m.Materia);
                            int col = (baseColsCount + 1) + i;

                            if (materias.Any())
                            {
                                ws.Cells[row, col].Value = string.Join(", ", materias);
                                ws.Cells[row, col].Style.WrapText = true;
                            }
                            else
                            {
                                ws.Cells[row, col].Value = "—";
                                ws.Cells[row, col].Style.Font.Color.SetColor(Color.Gray);
                            }
                            ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }

                        ApplyZebraStriping(ws.Cells[row, 1, row, lastCol], row, Color.FromArgb(242, 242, 242));
                        row++;
                    }
                }
            }

            if (row > dataStartRow) ApplyBorderToRange(ws.Cells[subHeaderRow, 1, row - 1, lastCol]);

            ws.Cells[subHeaderRow, 1, row - 1, lastCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Cells.AutoFitColumns();

            if (isGlobal) { ws.Column(1).Width = 40; ws.Column(3).Width = 40; }

            for (int i = 0; i < cuatrimestres.Count; i++)
            {
                int colIndex = (baseColsCount + 1) + i;
                ws.Column(colIndex).Width = 25;
                ws.Column(colIndex).Style.WrapText = true;
            }
        }

        // Wrapper matching signature for EscribirHojaMateriaGlobal
        private void EscribirHojaMateriaGlobal(ExcelWorksheet ws, List<(string NombreTutor, List<AlumnoConMateria> Alumnos)> porTutor)
        {
            EscribirHojaMateriaGeneric(ws, null, porTutor, isGlobal: true);
        }

        // Wrapper matching signature for EscribirHojaMateria
        private void EscribirHojaMateria(ExcelWorksheet ws, string nombreTutor, List<AlumnoConMateria> alumnos)
        {
            var scopedData = new List<(string NombreTutor, List<AlumnoConMateria> Alumnos)> { (nombreTutor, alumnos) };
            EscribirHojaMateriaGeneric(ws, nombreTutor, scopedData, isGlobal: false);
        }

        /*
        ───────────────────────────────────────── Funciones auxiliares para reducir codigo repetido (Estilos del archivo excel)  ─────────────────────────────────────────
        */

        private static void ConfigureBaseFontAndFreeze(ExcelWorksheet ws, int freezeRow, int freezeCol, string name = "Calibri", int size = 11)
        {
            ws.Cells.Style.Font.Name = name;
            ws.Cells.Style.Font.Size = size;
            ws.View.FreezePanes(freezeRow, freezeCol);
        }

        private static void ApplyHeaderStyle(ExcelRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.DarkSlateGray);
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }
        private static void ApplySpecificHeaderStyle(ExcelRange cell, Color bgColor)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(bgColor);
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        private static void CreateMergedHeaderCell(ExcelRange range, string value, Color bgColor)
        {
            range.Merge = true;
            range.Value = value;
            ApplySpecificHeaderStyle(range, bgColor);
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Border.BorderAround(ExcelBorderStyle.Medium);
        }

        private static void ApplyBorderToRange(ExcelRange range, ExcelBorderStyle style = ExcelBorderStyle.Thin)
        {
            range.Style.Border.Top.Style = style;
            range.Style.Border.Bottom.Style = style;
            range.Style.Border.Left.Style = style;
            range.Style.Border.Right.Style = style;
        }

        private static void ApplyZebraStriping(ExcelRange range, int rowNumber, Color zebraColor)
        {
            if (rowNumber % 2 == 0)
            {
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(zebraColor);
            }
        }

        private string GetGrupoLabelFallback(int idUsuario)
        {
            var (añoActual, periodoActual) = PeriodoActualData;
            var tutorias = modeloDb.TutoriaGrupals
                .Where(t => t.IdUsuario == idUsuario && t.IdPeriodo == periodoActual && t.Año == añoActual)
                .ToList();

            if (!tutorias.Any()) return "N/A";

            return string.Join(" / ", tutorias.Select(t =>
            {
                var nomenclatura = modeloDb.Carreras.Where(c => c.IdCarrera == t.IdCarrera).Select(c => c.Nomenclatura).FirstOrDefault();
                var grupoNombre = modeloDb.Grupoes.Where(g => g.IdGrupo == t.IdGrupo).Select(g => g.Nombre).FirstOrDefault();
                return $"{nomenclatura}{t.IdGrado}{grupoNombre}";
            }));
        }

        // Convierte un string a una clave de ordenamiento natural (ADM3A -> ADM00003A, ADM11A -> ADM00011A)
        private static string NaturalSortKey(string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(value ?? "", @"(\d+)", m => m.Groups[1].Value.PadLeft(10, '0'));
        }

        /*
         ───────────────────────────────────────── Funciones auxiliares (Generales)  ─────────────────────────────────────────
         */

        // Funcion para evitar repetir la estructura para datos del tutor.
        private TutorConEstadisticas MapTutorBase(Usuario tutor, string nombreCarrera)
        {
            return new TutorConEstadisticas
            {
                IdUsuario = tutor.IdUsuario,
                NombreCompleto = tutor.NombreCompleto,
                UserName = tutor.UserName,
                CorreoElectronico = tutor.CorreoElectronico,
                IdCarrera = tutor.IdCarrera,
                NombreCarrera = nombreCarrera ?? "Sin carrera",
                IdNivel = tutor.IdNivel,
                Estado = tutor.Estado,
                MicrosoftIdentifier = tutor.MicrosoftIdentifier
            };
        }

        // Nucleo de estadisticas via SQL directo. Usado por Index y DetallesTutor.
        // Cada subquery (Bajas, Arrastres, PATs, Semanas, etc.) es una columna calculada independiente.
        // Si idTutoriaGrupal tiene valor, filtra a ese grupo especifico; si es null, devuelve todos los grupos del tutor.
        private List<TutorConEstadisticas> EjecutarSqlEstadisticas(
            List<int> tutorIds, int periodo, int anio, int? idTutoriaGrupal = null, bool incluirBajas = false)
        {
            if (!tutorIds.Any()) return new List<TutorConEstadisticas>();

            var valueRows = string.Join(",", tutorIds.Select(i => $"({i})"));

            var sql = $@"
                DECLARE @ids TABLE (IdUsuario INT PRIMARY KEY);
                INSERT INTO @ids (IdUsuario) VALUES {valueRows};

                DECLARE @fIni DATE = CASE @per WHEN 1 THEN DATEFROMPARTS(@anio, 1, 1)
                                               WHEN 2 THEN DATEFROMPARTS(@anio, 5, 1)
                                                      ELSE DATEFROMPARTS(@anio, 9, 1) END;
                DECLARE @fFin DATE = CASE @per WHEN 1 THEN DATEFROMPARTS(@anio, 4, 30)
                                               WHEN 2 THEN DATEFROMPARTS(@anio, 8, 31)
                                                      ELSE DATEFROMPARTS(@anio, 12, 31) END;

                SELECT
                    u.IdUsuario,
                    CONCAT(c.Nomenclatura, g.Nombre, gr.Nombre) AS TutorGrupo,

                    -- Cuenta alumnos con baja activa en el mismo grupo/periodo
                    ISNULL((
                        SELECT COUNT(DISTINCT dp.IdPersona)
                        FROM   DatosPersonales dp
                        JOIN   BajasAlumnos b ON b.IdPersona = dp.IdPersona AND b.Activo = 1
                        WHERE  dp.IdCarrera = tg.IdCarrera AND dp.IdGrado  = tg.IdGrado
                           AND dp.IdGrupo   = tg.IdGrupo   AND dp.IdTurno  = tg.IdTurno
                           AND dp.IdPeriodo = tg.IdPeriodo  AND dp.Año     = tg.Año
                    ), 0) AS AlumnosEnBajasCount,

                    -- Cuenta alumnos con al menos una materia reprobada
                    ISNULL((
                        SELECT COUNT(DISTINCT dp.IdPersona)
                        FROM   DatosPersonales dp
                        JOIN   MateriasAlumno ma ON ma.IdPersona = dp.IdPersona AND ma.Estado = 'Reprobada'
                        WHERE  dp.IdCarrera = tg.IdCarrera AND dp.IdGrado  = tg.IdGrado
                           AND dp.IdGrupo   = tg.IdGrupo   AND dp.IdTurno  = tg.IdTurno
                           AND dp.IdPeriodo = tg.IdPeriodo  AND dp.Año     = tg.Año
                           AND (@incluirBajas = 1 OR NOT EXISTS (
                               SELECT 1 FROM BajasAlumnos b
                               WHERE b.IdPersona = dp.IdPersona AND b.Activo = 1
                           ))
                    ), 0) AS AlumnosEnArrastresCount,

                    -- Cuenta alumnos con al menos una materia en extraordinario
                    ISNULL((
                        SELECT COUNT(DISTINCT dp.IdPersona)
                        FROM   DatosPersonales dp
                        JOIN   MateriasAlumno ma ON ma.IdPersona = dp.IdPersona AND ma.Estado = 'Extraordinario'
                        WHERE  dp.IdCarrera = tg.IdCarrera AND dp.IdGrado  = tg.IdGrado
                           AND dp.IdGrupo   = tg.IdGrupo   AND dp.IdTurno  = tg.IdTurno
                           AND dp.IdPeriodo = tg.IdPeriodo  AND dp.Año     = tg.Año
                           AND (@incluirBajas = 1 OR NOT EXISTS (
                               SELECT 1 FROM BajasAlumnos b
                               WHERE b.IdPersona = dp.IdPersona AND b.Activo = 1
                           ))
                    ), 0) AS AlumnosEnExtraordinarioCount,

                    -- PATs del tutor en el periodo (activos, cualquier estado de revision)
                    ISNULL((
                        SELECT COUNT(*)
                        FROM   PATs p
                        WHERE  p.IdTutor          = u.IdUsuario
                           AND p.estado           = 1
                           AND p.IdPeriodo        = @per
                           AND YEAR(p.Fecha)      = @anio
                           AND p.IdTutoriaGrupal  = tg.IdTutoriaGrupal
                    ), 0) AS PATsTotales,

                    -- PATs con EstadoRevision = 2 (Aprobado)
                    ISNULL((
                        SELECT COUNT(*)
                        FROM   PATs p
                        WHERE  p.IdTutor          = u.IdUsuario
                           AND p.EstadoRevision   = 2
                           AND p.estado           = 1
                           AND p.IdPeriodo        = @per
                           AND YEAR(p.Fecha)      = @anio
                           AND p.IdTutoriaGrupal  = tg.IdTutoriaGrupal
                    ), 0) AS PATsAprobados,

                    -- Semanas distintas que tienen al menos una actividad registrada
                    ISNULL((
                        SELECT COUNT(DISTINCT ac.IdSemana)
                        FROM   PATs p
                        JOIN   actividadesSemanals ac ON ac.IdEntrevistaInicial = p.IdEntrevistaInicial
                        WHERE  p.IdTutor          = u.IdUsuario
                           AND p.IdPeriodo        = @per
                           AND YEAR(p.Fecha)      = @anio
                           AND p.IdTutoriaGrupal  = tg.IdTutoriaGrupal
                    ), 0) AS SemanasCreadas,

                    -- Semanas con tutoria individual realizada (RealizoActividad = 1)
                    ISNULL((
                        SELECT COUNT(DISTINCT ac.IdSemana)
                        FROM   PATs p
                        JOIN   actividadesSemanals ac ON ac.IdEntrevistaInicial = p.IdEntrevistaInicial
                        JOIN   TipoTutorias        tp ON tp.IdTipoTutoria        = ac.IdTipoTutoria
                        WHERE  p.IdTutor          = u.IdUsuario
                           AND p.IdPeriodo        = @per
                           AND YEAR(p.Fecha)      = @anio
                           AND p.IdTutoriaGrupal  = tg.IdTutoriaGrupal
                           AND tp.Nombre          = 'Tutoria Individual'
                           AND ac.RealizoActividad = 1
                    ), 0) AS SemanasIndividualesCompletadas,

                    -- Semanas con tutoria grupal realizada (RealizoActividad = 1)
                    ISNULL((
                        SELECT COUNT(DISTINCT ac.IdSemana)
                        FROM   PATs p
                        JOIN   actividadesSemanals ac ON ac.IdEntrevistaInicial = p.IdEntrevistaInicial
                        JOIN   TipoTutorias        tp ON tp.IdTipoTutoria        = ac.IdTipoTutoria
                        WHERE  p.IdTutor          = u.IdUsuario
                           AND p.IdPeriodo        = @per
                           AND YEAR(p.Fecha)      = @anio
                           AND p.IdTutoriaGrupal  = tg.IdTutoriaGrupal
                           AND tp.Nombre          = 'Tutoria Grupal'
                           AND ac.RealizoActividad = 1
                    ), 0) AS SemanasGrupalesCompletadas,

                    -- Primer seguimiento de cada alumno en el periodo (vulnerable o no vulnerable).
                    -- ROW_NUMBER particiona por alumno para tomar solo el registro mas antiguo.
                    -- La condicion @incluirBajas controla si se excluyen alumnos con baja activa.
                    ISNULL((
                        SELECT COUNT(DISTINCT sub.IdPersona)
                        FROM (
                            SELECT i.IdPersona,
                                   ROW_NUMBER() OVER (PARTITION BY i.IdPersona ORDER BY s.Fecha, s.IdSeguimiento) AS rn
                            FROM   DatosPersonales dp
                            JOIN   Individuals     i ON i.IdPersona   = dp.IdPersona
                            JOIN   Seguimientoes   s ON s.IdIndividual = i.IdIndividual
                                                   AND s.Fecha BETWEEN @fIni AND @fFin
                            WHERE  dp.IdCarrera = tg.IdCarrera AND dp.IdGrado  = tg.IdGrado
                               AND dp.IdGrupo   = tg.IdGrupo   AND dp.IdTurno  = tg.IdTurno
                               AND dp.IdPeriodo = tg.IdPeriodo  AND dp.Año     = tg.Año
                               AND (@incluirBajas = 1 OR NOT EXISTS (
                                   SELECT 1 FROM BajasAlumnos b
                                   WHERE b.IdPersona = dp.IdPersona AND b.Activo = 1
                               ))
                        ) sub WHERE sub.rn = 1
                    ), 0) AS EntrevistasRevisadas,

                    -- Total de alumnos del grupo. Filtra bajas activas a menos que @incluirBajas = 1.
                    ISNULL((
                        SELECT COUNT(DISTINCT dp.IdPersona)
                        FROM   DatosPersonales dp
                        WHERE  dp.IdCarrera = tg.IdCarrera AND dp.IdGrado  = tg.IdGrado
                           AND dp.IdGrupo   = tg.IdGrupo   AND dp.IdTurno  = tg.IdTurno
                           AND dp.IdPeriodo = tg.IdPeriodo  AND dp.Año     = tg.Año
                           AND (@incluirBajas = 1 OR NOT EXISTS (
                               SELECT 1 FROM BajasAlumnos b
                               WHERE b.IdPersona = dp.IdPersona AND b.Activo = 1
                           ))
                    ), 0) AS TotalAlumnosActivos

                FROM Usuarios u
                JOIN @ids          i  ON i.IdUsuario  = u.IdUsuario
                JOIN TutoriaGrupals tg ON tg.IdUsuario = u.IdUsuario
                                       AND tg.IdPeriodo = @per
                                       AND tg.Año       = @anio
                                       -- Filtro de grupo específico: si viene null, devuelve todos los grupos del tutor.
                                       AND (@idTutoriaGrupal IS NULL OR tg.IdTutoriaGrupal = @idTutoriaGrupal)
                JOIN Carreras c  ON c.IdCarrera = tg.IdCarrera
                JOIN Gradoes  g  ON g.IdGrado   = tg.IdGrado
                JOIN Grupoes  gr ON gr.IdGrupo  = tg.IdGrupo;";

            try
            {
                return modeloDb.Database.SqlQuery<TutorConEstadisticas>(sql,
                    new SqlParameter("@anio", anio),
                    new SqlParameter("@per", periodo),
                    new SqlParameter("@incluirBajas", incluirBajas ? 1 : 0),
                    new SqlParameter("@idTutoriaGrupal",
                        idTutoriaGrupal.HasValue ? (object)idTutoriaGrupal.Value : DBNull.Value))
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en EjecutarSqlEstadisticas: {ex.Message}");
                return new List<TutorConEstadisticas>();
            }
        }

        // Funcion para devolver datos para APIs y vista.
        private Dictionary<int, TutorConEstadisticas> ObtenerEstadisticasBatch(List<int> tutorIds, int periodo, int anio, int? idTutoriaGrupal = null)
        {
            // EjecutarSqlEstadisticas ya maneja lista vacía y errores internamente.
            return EjecutarSqlEstadisticas(tutorIds, periodo, anio, idTutoriaGrupal)
                .GroupBy(r => r.IdUsuario)
                .ToDictionary(
                    g => g.Key,
                    // Si hay múltiples grupos (idTutoriaGrupal == null), toma el primero.
                    // Con idTutoriaGrupal específico siempre será uno solo.
                    g => g.First()
                );
        }

        // Devuelve (año, periodo) de la fecha actual.
        private (int Año, int Periodo) ObtenerPeriodoActual()
        {
            var ahora = DateTime.Now;
            int periodo = (ahora.Month <= 4) ? 1 : (ahora.Month <= 8) ? 2 : 3;
            return (ahora.Year, periodo);
        }

        // Para llamarlo como variable "PeriodoActualData"
        private (int añoActual, int periodoActual) PeriodoActualData => ObtenerPeriodoActual();

        // Funcion que obtiene los IDs de personas con bajas activas.
        private List<int> ObtenerIdsBajasActivas()
        {
            return modeloDb.Bajas
                .Where(b => b.Activo == true)
                .Select(b => b.IdPersona)
                .ToList();
        }

        // Para llamarlo como variable "IdsBajasActivasData"
        private List<int> IdsBajasActivasData => ObtenerIdsBajasActivas();

        // Para llamarlo como variable "TotalSemanas" (cacheado por request)
        private int TotalSemanasCountData
        {
            get
            {
                if (HttpContext.Items["_TotalSemanasCached"] is int cached)
                    return cached;
                var val = ObtenerMaxSemanas().MaxSemanas;
                HttpContext.Items["_TotalSemanasCached"] = val;
                return val;
            }
        }

        private SemanasPeriodo ObtenerMaxSemanas()
        {
            var (año, periodo) = PeriodoActualData;
            var record = modeloDb.SemanasPeriodos
                .FirstOrDefault(sp => sp.IdPeriodo == periodo && sp.Año == año);

            if (record == null)
            {
                var usuario = Session["Usuario"] as Usuario;
                record = new SemanasPeriodo
                {
                    IdPeriodo = periodo,
                    Año = año,
                    MaxSemanas = modeloDb.Semanas.Count(),
                    ModificadoPor = usuario?.IdUsuario,
                    FechaCreacion = DateTime.Now
                };
                modeloDb.SemanasPeriodos.Add(record);
                modeloDb.SaveChanges();
            }

            return record;
        }

        // Filtra tutores (nivel=2) que tengan tutorias activas en el periodo actual.
        // El nivel del usuario que consulta determina que tutores puede ver:
        //   nivel 3 (coordinador) -> solo su carrera y con MicrosoftIdentifier asignado
        //   nivel 4+ (master)     -> todas las carreras, o una especifica si se filtra
        private IQueryable<Usuario> ObtenerTutoresQuery(Usuario user, int? idCarrera, int periodoActual,int añoActual)
        {
            var query = modeloDb.Usuarios.Where(x =>
                x.IdNivel == 2 &&
                modeloDb.TutoriaGrupals.Any(tg =>
                    tg.IdUsuario == x.IdUsuario &&
                    tg.IdPeriodo == periodoActual &&
                    tg.Año == añoActual));

            switch (user.IdNivel)
            {
                case 3:
                    query = query.Where(x =>
                        x.IdCarrera == user.IdCarrera &&
                        !string.IsNullOrEmpty(x.MicrosoftIdentifier));
                    break;

                case int nivel when nivel >= 4:
                    if (idCarrera.HasValue && idCarrera > 0)
                        query = query.Where(x => x.IdCarrera == idCarrera);
                    break;

                case 0:
                    query = query.Where(x => false);
                    break;
            }

            return query.AsNoTracking();
        }

        // Seleccionar PAT especifico(s) y reciente.
        private IQueryable<PAT> BuildPatQuery(int idTutor, int idPeriodo, int año, int? idTutoriaGrupal = null)
        {
            var query = modeloDb.PATs.Where(p =>
                p.IdTutor == idTutor &&
                p.IdPeriodo == idPeriodo &&
                p.Fecha.Year == año);

            if (idTutoriaGrupal.HasValue)
            {
                query = query.Where(p =>
                    p.IdTutoriaGrupal == idTutoriaGrupal.Value);
            }

            return query;
        }

        // Obtener datos del PAT (Contadores).
        private (int SemanasCreadas, int SemanasIndividualesCompletadas,int SemanasGrupalesCompletadas)
            ObtenerEstadisticasPAT(int idTutor,int? idTutoriaGrupal,int periodoActual,int añoActual)
            {

            var patsQuery = BuildPatQuery(idTutor, periodoActual, añoActual, idTutoriaGrupal);

            var patIdsEntrevista = patsQuery.Select(p => p.IdEntrevistaInicial).ToList();

            if (!patIdsEntrevista.Any())
                return (0, 0, 0);

            var actividades = modeloDb.actividadesSemanals
                .Include(x => x.Tipo)
                .Where(x => patIdsEntrevista.Contains(x.IdEntrevistaInicial))
                .ToList();

            int semanasCreadas = actividades
                .Select(x => x.IdSemana)
                .Distinct()
                .Count();

            int individuales = actividades
                .Where(x =>
                    x.Tipo.Nombre == "Tutoria Individual" &&
                    x.RealizoActividad)
                .Select(x => x.IdSemana)
                .Distinct()
                .Count();

            int grupales = actividades
                .Where(x =>
                    x.Tipo.Nombre == "Tutoria Grupal" &&
                    x.RealizoActividad)
                .Select(x => x.IdSemana)
                .Distinct()
                .Count();

            return (semanasCreadas, individuales,grupales);
        }

        // Devuelve los IdPersona de alumnos que tienen al menos un Seguimiento en el periodo actual.
        private HashSet<int> ObtenerIdsAlumnosConSeguimiento(List<int> idAlumnos, List<int> idsBajasActivas, int año, int periodo)
        {
            DateTime fechaInicio, fechaFin;
            if (periodo == 1) { fechaInicio = new DateTime(año, 1, 1); fechaFin = new DateTime(año, 4, 30); }
            else if (periodo == 2) { fechaInicio = new DateTime(año, 5, 1); fechaFin = new DateTime(año, 8, 31); }
            else { fechaInicio = new DateTime(año, 9, 1); fechaFin = new DateTime(año, 12, 31); }

            var ids = modeloDb.Seguimientoes
                .Join(modeloDb.Individuals, s => s.IdIndividual, i => i.IdIndividual, (s, i) => new { s, i })
                .Where(x => idAlumnos.Contains(x.i.IdPersona)
                            && !idsBajasActivas.Contains(x.i.IdPersona)
                            && x.s.Fecha >= fechaInicio
                            && x.s.Fecha <= fechaFin)
                .Select(x => x.i.IdPersona)
                .Distinct()
                .ToList();

            return new HashSet<int>(ids);
        }

        private HashSet<int> ObtenerIdsAlumnosConEntrevistaRevisada(List<int> idAlumnos, List<int> idsBajasActivas, int año, int periodo)
        {
            DateTime fechaInicio, fechaFin;
            if (periodo == 1) { fechaInicio = new DateTime(año, 1, 1); fechaFin = new DateTime(año, 4, 30); }
            else if (periodo == 2) { fechaInicio = new DateTime(año, 5, 1); fechaFin = new DateTime(año, 8, 31); }
            else { fechaInicio = new DateTime(año, 9, 1); fechaFin = new DateTime(año, 12, 31); }

            var ids = (from s in modeloDb.Seguimientoes
                       join i in modeloDb.Individuals on s.IdIndividual equals i.IdIndividual
                       where idAlumnos.Contains(i.IdPersona)
                             && !idsBajasActivas.Contains(i.IdPersona)
                             && s.Fecha >= fechaInicio
                             && s.Fecha <= fechaFin
                       select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                      .ToList()
                      .GroupBy(x => x.IdPersona)
                      .Select(g => g.OrderBy(x => x.Fecha).ThenBy(x => x.IdSeguimiento).First())
                      .Where(x => !string.IsNullOrEmpty(x.Vulnerabilidad))
                      .Select(x => x.IdPersona)
                      .ToList();

            return new HashSet<int>(ids);
        }

        // Devuelve cantidad de vulnerable, no vulnerables y totalAlumnosActivos para la lista de alumnos para tutores.
        private (int Vulnerables, int NoVulnerables, int TotalAlumnosActivos)
            ObtenerEstadisticasVulnerabilidad(List<int> idAlumnos, List<int> idsBajasActivas, int año, int periodo)
        {
            if (!idAlumnos.Any()) return (0, 0, 0);

            int totalActivos = idAlumnos.Distinct().Count(id => !idsBajasActivas.Contains(id));

            DateTime fechaInicio, fechaFin;
            if (periodo == 1) { fechaInicio = new DateTime(año, 1, 1); fechaFin = new DateTime(año, 4, 30); }
            else if (periodo == 2) { fechaInicio = new DateTime(año, 5, 1); fechaFin = new DateTime(año, 8, 31); }
            else { fechaInicio = new DateTime(año, 9, 1); fechaFin = new DateTime(año, 12, 31); }

            // Toma el PRIMER seguimiento de cada alumno en el periodo (por fecha, luego por IdSeguimiento).
            // Solo ese primer registro determina si el alumno cuenta como "entrevista revisada".
            // Clasifica: "No vulnerable" vs el resto (vulnerable).
            var primerosSeguimientos = (from s in modeloDb.Seguimientoes
                                        join i in modeloDb.Individuals on s.IdIndividual equals i.IdIndividual
                                        where idAlumnos.Contains(i.IdPersona)
                                              && !idsBajasActivas.Contains(i.IdPersona)
                                              && s.Fecha >= fechaInicio
                                              && s.Fecha <= fechaFin
                                        select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                                       .ToList()
                                       .GroupBy(x => x.IdPersona)
                                       .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).First())
                                       .ToList();

            int noVulnerables = primerosSeguimientos.Count(x =>
                string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase));

            return (primerosSeguimientos.Count - noVulnerables, noVulnerables, totalActivos);
        }

        // Obtiene los IdPersona de los alumnos asignados al tutor.
        // Usa join compuesto de 6 campos porque DatosPersonales y TutoriaGrupals comparten la misma clave de grupo.
        private List<int> ObtenerIdPersonasDeTutor(int idTutor, int? periodo = null, int? anio = null, int? idTutoriaGrupal = null)
        {
            var tgQuery = modeloDb.TutoriaGrupals.Where(t => t.IdUsuario == idTutor);
            if (periodo.HasValue) tgQuery = tgQuery.Where(t => t.IdPeriodo == periodo.Value);
            if (anio.HasValue) tgQuery = tgQuery.Where(t => t.Año == anio.Value);
            if (idTutoriaGrupal.HasValue) tgQuery = tgQuery.Where(t => t.IdTutoriaGrupal == idTutoriaGrupal.Value);

            return modeloDb.DatosPersonales
                .Join(tgQuery,
                      dp => new { dp.IdCarrera, dp.IdGrado, dp.IdGrupo, dp.IdTurno, dp.IdPeriodo, dp.Año },
                      tg => new { tg.IdCarrera, tg.IdGrado, tg.IdGrupo, tg.IdTurno, tg.IdPeriodo, tg.Año },
                      (dp, tg) => dp.IdPersona)
                .Distinct()
                .ToList();
        }

        // Obtener id del pat de un tutor asignado.
        public int? GetPatId(ModeloPlataforma modeloDb, int tutorId, int periodoActual, int anio)
        {
            return modeloDb.PATs
                .Where(p => p.IdTutor == tutorId
                         && p.IdPeriodo == periodoActual
                         && p.Fecha.Year == anio)
                .Select(p => p.IdEntrevistaInicial)
                .FirstOrDefault();
        }

        // ViewBag helper para el filtro de carrera para usuario master (nivel 4). Tambien se usa para mostrar el periodo actual (General).
        private void SetIndexViewBag(Usuario user, int? _)
        {
            ViewBag.Carreras = modeloDb.Carreras
                .AsNoTracking()
                .ToDictionary(c => c.IdCarrera, c => c.Nombre);

            if (user.IdNivel >= 4)
            {
                ViewBag.CarrerasSelectList = new SelectList(modeloDb.Carreras.ToList(), "IdCarrera", "Nombre");
            }

            var (año, periodo) = PeriodoActualData;
            ViewBag.PeriodoActual = ObtenerPeriodoTextoActual(periodo);
            ViewBag.AñoActual = año;

            var sp = ObtenerMaxSemanas();
            ViewBag.MaxSemanas = sp.MaxSemanas;
            ViewBag.IdMaxSemana = sp.IdMaxSemana;

            ViewBag.UserNivel = user.IdNivel;
            ViewBag.UserCarrera = user.IdCarrera;
        }

        // Funcion que solo puede utilizarse en una clase especifica. Devuelve al tutor deseado.
        protected Usuario GetTutor(int id)
        {
            return modeloDb.Usuarios.AsNoTracking().FirstOrDefault(u => u.IdUsuario == id);
        }

        // Obtener texto del periodo segun el id del periodo para mostrar en Index.
        private string ObtenerPeriodoTextoActual(int periodo)
        {
            switch (periodo)
            {
                case 1:
                    return "Enero - Abril";
                case 2:
                    return "Mayo - Agosto";
                case 3:
                    return "Septiembre - Diciembre";
                default:
                    return "Periodo desconocido";
            }
        }

        // Funcion para verificar si hay sesion.
        private Usuario ObtenerUsuarioSesion()
        {
            var user = Session["Usuario"] as Usuario;

            if (user == null)
                return DenegarAcceso("Sesión expirada. Por favor, inicie sesión nuevamente.", 0);

            if (user.IdNivel < 3)
                return DenegarAcceso("No cuenta con los permisos para acceder a este recurso.", user.IdNivel);

            return user;
        }

        // Establece ViewBag de error y redirige a la vista Restringido.
        private Usuario DenegarAcceso(string mensaje, int nivel)
        {
            ViewBag.Error = mensaje;
            ViewBag.UserNivel = nivel;

            Response.Redirect(Url.Action("Restringido", "Home"));
            return null;
        }

        /*
         ───────────────────────────────────────── Funciones auxiliares (Otros)  ─────────────────────────────────────────
         */

        // Realiza la siguiente accion al momento de ejecutarse. Debe colocarse como atributo en la parte de arriba del controlador para usarlo.
        // Usarlo asi: [ValidarSesion]
        public class ValidarSesionAttribute : ActionFilterAttribute
        {
            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                var user = filterContext.HttpContext.Session["Usuario"] as Usuario;

                // Usuario denegado si no hay sesion o su nivel es menor a 3 (coordinador).
                if (user == null || user.IdNivel < 3)
                {
                    // Vista predeterminada para bloquear acceso.
                    filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado");
                    return;
                }

                base.OnActionExecuting(filterContext);
            }
        }

        private ActionResult JsonError(string mensaje = "No tienes permisos para realizar esta acción.")
        {
            return Json(new { success = false, message = mensaje }, JsonRequestBehavior.AllowGet);
        }

        private ActionResult JsonExito(object datos = null)
        {
            return Json(new { success = true, datos }, JsonRequestBehavior.AllowGet);
        }

        private bool Autorizar(Usuario user, int nivelMinimo, out ActionResult errorResult)
        {
            if (user == null || user.IdNivel < nivelMinimo)
            {
                errorResult = JsonError();
                return false;
            }
            errorResult = null;
            return true;
        }

        private static bool EsCancelacion(Exception ex)
        {
            if (ex is OperationCanceledException) return true;
            // EF wraps SqlClient cancellations as SqlException with number 0
            var sql = ex as System.Data.SqlClient.SqlException
                      ?? (ex.InnerException as System.Data.SqlClient.SqlException);
            return sql != null && sql.Number == 0 && sql.Class == 11;
        }

        /*
         ───────────────────────────────────────── Funciones auxiliares (Uso poco o minimo)  ─────────────────────────────────────────
         */

        private static string ObtenerNombreCarrera(ModeloPlataforma modeloDb, int? idCarrera)
        {
            if (idCarrera == null) return "Sin Carrera";

            return modeloDb.Carreras
                .Where(c => c.IdCarrera == idCarrera)
                .Select(c => c.Nombre)
                .SingleOrDefault() ?? "Sin Carrera";
        }

    }

}

// DEVELOPED BY: ARC