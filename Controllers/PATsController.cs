using Microsoft.Reporting.Common;
using Microsoft.Reporting.WebForms;
using Newtonsoft.Json;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesPAT;
using Plataforma_Web.Models.MongoDB;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Models;
using PlataformaWeb.Models.ClasesPAT;
using PlataformaWeb.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;

namespace Plataforma_Web.Controllers
{
    [CustomAuthorize(Nivel = 2)]
    public class PATsController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        private readonly MongoDBService _mongoService = new MongoDBService();
        // ====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EliminarSemana(int idPat, int idSemana)
        {
            try
            {
                // Buscar todas las actividades de ese PAT y semana
                var actividades = db.actividadesSemanals
                    .Where(a => a.IdEntrevistaInicial == idPat && a.IdSemana == idSemana)
                    .ToList();

                if (!actividades.Any())
                {
                    return Json(new { success = false, message = "No se encontraron actividades para esa semana." });
                }

                // Eliminar todas las actividades de la semana, sin importar notas o realizadas

                // Depuración: mostrar IDs de actividades a eliminar
                var ids = string.Join(",", actividades.Select(a => a.IdActividad));
                System.Diagnostics.Debug.WriteLine($"[EliminarSemana] Eliminando actividades: {ids}");

                // Eliminar actividades
                db.actividadesSemanals.RemoveRange(actividades);
                db.SaveChanges();

                // Verificar que se eliminaron
                var actividadesRestantes = db.actividadesSemanals
                    .Where(a => a.IdEntrevistaInicial == idPat && a.IdSemana == idSemana)
                    .ToList();
                if (actividadesRestantes.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[EliminarSemana] ERROR: No se eliminaron todas las actividades. IDs restantes: {string.Join(",", actividadesRestantes.Select(a => a.IdActividad))}");
                    return Json(new { success = false, message = "Error: No se eliminaron todas las actividades." });
                }

                System.Diagnostics.Debug.WriteLine($"[EliminarSemana] Semana eliminada correctamente.");
                return Json(new { success = true, message = "Semana eliminada correctamente." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EliminarSemana] Exception: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = "Error al eliminar la semana: " + ex.Message });
            }
        }
        // Index con filtros multi-selección
        // ====================================================
        // GET: PATs
        public ActionResult Index()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null) return RedirectToAction("Login", "Account");

                var tiempo = DateTime.Now;
                var periodoActual = ObtenerPeriodoActual(tiempo);

                // --- CORRECCIÓN: Traer TODO el historial, sin filtrar por año/periodo aquí ---
                List<PAT> pats;

                if (usuario.IdNivel == 4) // Master
                {
                    pats = db.PATs
                        .Include(p => p.Periodo)
                        .Include(p => p.Carrera)
                        .ToList();
                }
                else // Tutor/Coordinador
                {
                    pats = db.PATs
                        .Where(x => x.IdTutor == usuario.IdUsuario)
                        .Include(p => p.Periodo)
                        .Include(p => p.Carrera)
                        .ToList();
                }

                // Generación automática SOLO si no existen para el periodo actual
                var existenActuales = pats.Any(x => x.IdPeriodo == periodoActual && x.Fecha.Year == tiempo.Year);

                if (!existenActuales)
                {
                    CrearPATsAutomaticamente(usuario, periodoActual, tiempo.Year);
                    // Recargar la lista completa
                    if (usuario.IdNivel == 4)
                        pats = db.PATs.Include(p => p.Periodo).Include(p => p.Carrera).ToList();
                    else
                        pats = db.PATs.Where(x => x.IdTutor == usuario.IdUsuario).Include(p => p.Periodo).Include(p => p.Carrera).ToList();
                }

                // Generar nomenclatura de grupos
                foreach (var item in pats)
                {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == item.IdTutoriaGrupal);
                    item.TutoriaGrupal = GenerarNomenclaturaGrupo(tuto);
                }

                // Ordenar: Primero por año (desc), luego periodo (desc), luego grupo
                pats = pats
                    .OrderByDescending(x => x.Fecha.Year)
                    .ThenByDescending(x => x.IdPeriodo)
                    .ThenBy(x => ObtenerClaveOrdenamientoGrupo(x.TutoriaGrupal))
                    .ToList();

                return View(pats);
            }
            catch (Exception ex)
            {
                return View(new List<PAT>());
            }
        }


        // MÃ©todo de debug de actividades
        public ActionResult DebugActividades()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { Error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                // Obtener PATs del usuario
                var pats = db.PATs.Where(p => p.IdTutor == usuario.IdUsuario).Take(5).ToList();
                var patIds = pats.Select(p => p.IdEntrevistaInicial).ToList();

                // Obtener actividades
                var actividades = db.actividadesSemanals
                    .Where(a => patIds.Contains(a.IdEntrevistaInicial))
                    .ToList();

                // Cargar relaciones
                foreach (var actividad in actividades)
                {
                    try
                    {
                        actividad.Semana = db.Semanas.FirstOrDefault(s => s.IdSemana == actividad.IdSemana);
                    }
                    catch { }
                }

                // Agrupar por PAT
                var actividadesPorPAT = actividades
                    .GroupBy(a => a.IdEntrevistaInicial)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var resultado = new
                {
                    UsuarioId = usuario.IdUsuario,
                    UsuarioNombre = usuario.NombreCompleto,
                    TotalPATs = pats.Count,
                    PatIds = patIds,
                    TotalActividades = actividades.Count,
                    ActividadesPorPAT = actividadesPorPAT.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new {
                            Cantidad = kvp.Value.Count,
                            Grupo = pats.FirstOrDefault(p => p.IdEntrevistaInicial == kvp.Key)?.TutoriaGrupal ?? "N/A",
                            Actividades = kvp.Value.Take(3).Select(a => new {
                                Id = a.IdActividad,
                                Semana = a.IdSemana,
                                SemanaLabel = a.Semana?.Nombre ?? ("Semana " + a.IdSemana),
                                Tipo = a.IdTipoTutoria,
                                Actividad = a.Actividad?.Length > 50 ? a.Actividad.Substring(0, 50) + "..." : a.Actividad,
                                Realizada = a.RealizoActividad
                            }).ToList()
                        }
                    ),
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return Json(resultado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // AcciÃ³n optimizada para obtener PATs paginados y filtrados
        [HttpGet]
        public JsonResult ObtenerPATs(string carrera = "", string estado = "", int page = 1, int pageSize = 30)
        {
            var query = db.PATs.AsQueryable();

            if (!string.IsNullOrEmpty(carrera))
                query = query.Where(x => x.Carrera.Nombre == carrera);

            if (!string.IsNullOrEmpty(estado))
            {
                if (estado == "Aprobado")
                    query = query.Where(x => x.estado == true);
                else if (estado == "En progreso")
                    query = query.Where(x => x.estado == false);
            }

            int total = query.Count();
            var pats = query
                .OrderBy(x => x.Fecha)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new {
                    x.IdEntrevistaInicial,
                    Grupo = x.TutoriaGrupal,
                    Turno = x.TutoriaGrupal != null ? (x.TutoriaGrupal.StartsWith("M") ? "Matutino" : x.TutoriaGrupal.StartsWith("I") ? "Vespertino" : x.TutoriaGrupal.StartsWith("D") ? "Despresurizado" : "-") : "-",
                    x.Tutor,
                    Nombre = x.Carrera != null ? x.Carrera.Nombre : "-",
                    Periodo = x.Periodo != null ? x.Periodo.Nombre : "-",
                    Fecha = x.Fecha.Year,
                    x.CantidadAlumno,
                    x.estado
                })
                .ToList();

            return Json(new { pats, total }, JsonRequestBehavior.AllowGet);
        }
        // POST: AsignarAsesores/ForzarGenerarPATs
        [HttpPost]
        public ActionResult ForzarGenerarPATs()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado." });
                }

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int añoActual = tiempo.Year;

                // Obtener grupos asignados al tutor actual en el periodo y año actual
                var gruposAsignados = db.TutoriaGrupals.Where(tg => tg.IdUsuario == usuario.IdUsuario && tg.IdPeriodo == periodoActual && tg.Año == añoActual).ToList();

                // Obtener PATs ya existentes para esos grupos
                var patsExistentes = db.PATs.Where(p => p.IdTutor == usuario.IdUsuario && p.IdPeriodo == periodoActual && p.Fecha.Year == añoActual)
                                            .Select(p => p.IdTutoriaGrupal).ToList();

                // Filtrar grupos que no tienen PAT
                var gruposSinPAT = gruposAsignados.Where(g => !patsExistentes.Contains(g.IdTutoriaGrupal)).ToList();

                int patsCreados = 0;
                foreach (var grupo in gruposSinPAT)
                {
                    // Contar alumnos activos en el grupo
                    int cantidadAlumnos = db.DatosPersonales.Count(dp => dp.IdCarrera == grupo.IdCarrera
                        && dp.IdGrado == grupo.IdGrado
                        && dp.IdGrupo == grupo.IdGrupo
                        && dp.IdTurno == grupo.IdTurno
                        && dp.IdPeriodo == grupo.IdPeriodo
                        && dp.Año == grupo.Año
                        && dp.Estado == true);

                    var nuevoPAT = new PAT
                    {
                        IdTutor = usuario.IdUsuario,
                        Tutor = usuario.NombreCompleto,
                        IdTutoriaGrupal = grupo.IdTutoriaGrupal,
                        IdCarrera = grupo.IdCarrera,
                        IdPeriodo = periodoActual,
                        Fecha = DateTime.Now,
                        CantidadAlumno = cantidadAlumnos,
                        estado = true,
                        EstadoRevision = 0,
                        VunerableEconomico = 0,
                        VunerablePersonal = 0,
                        VunerableAcademico = 0,
                        DescripcionEconomico = "",
                        DescripcionPersonal = "",
                        DescripcionAcademico = ""
                    };
                    db.PATs.Add(nuevoPAT);
                    patsCreados++;
                }
                if (patsCreados > 0)
                {
                    db.SaveChanges();
                }
                return Json(new { success = true, message = $"Se crearon {patsCreados} PATs exitosamente.", patsCreados });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al forzar la generación de PATs: " + ex.Message });
            }
        }


        // Metodo de debug de la estructura de la base de datos
        public ActionResult VerificarEstructuraBD()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { Error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var resultado = new
                {
                    // Verificar tablas principales
                    TablasExisten = new
                    {
                        PATs = db.PATs != null,
                        ActividadesSemanals = db.actividadesSemanals != null,
                        Semanas = db.Semanas != null,
                        TipoTutorias = db.TipoTutorias != null
                    },

                    // Contar registros
                    ConteoRegistros = new
                    {
                        TotalPATs = db.PATs.Count(),
                        TotalActividades = db.actividadesSemanals.Count(),
                        TotalSemanas = db.Semanas.Count(),
                        TotalTipoTutorias = db.TipoTutorias.Count()
                    },

                    // PATs del usuario actual
                    PATsUsuario = new
                    {
                        Total = db.PATs.Where(p => p.IdTutor == usuario.IdUsuario).Count(),
                        IDs = db.PATs.Where(p => p.IdTutor == usuario.IdUsuario)
                             .Select(p => p.IdEntrevistaInicial)
                             .Take(10)
                             .ToList()
                    },

                    // Actividades relacionadas con PATs del usuario
                    ActividadesUsuario = new
                    {
                        Total = db.actividadesSemanals
                               .Where(a => db.PATs.Where(p => p.IdTutor == usuario.IdUsuario)
                                                 .Select(p => p.IdEntrevistaInicial)
                                                 .Contains(a.IdEntrevistaInicial))
                               .Count(),

                        Muestra = db.actividadesSemanals
                                 .Where(a => db.PATs.Where(p => p.IdTutor == usuario.IdUsuario)
                                                   .Select(p => p.IdEntrevistaInicial)
                                                   .Contains(a.IdEntrevistaInicial))
                                 .Take(5)
                                 .Select(a => new
                                 {
                                     Id = a.IdActividad,
                                     PatId = a.IdEntrevistaInicial,
                                     Semana = a.IdSemana,
                                     Tipo = a.IdTipoTutoria,
                                     Actividad = a.Actividad != null && a.Actividad.Length > 30
                                                ? a.Actividad.Substring(0, 30) + "..."
                                                : a.Actividad
                                 })
                                 .ToList()
                    },

                    // Muestra general de actividades
                    MuestraActividades = db.actividadesSemanals
                                          .Take(10)
                                          .Select(a => new
                                          {
                                              Id = a.IdActividad,
                                              PatId = a.IdEntrevistaInicial,
                                              Semana = a.IdSemana,
                                              Tipo = a.IdTipoTutoria
                                          })
                                          .ToList(),

                    // Información del usuario
                    UsuarioInfo = new
                    {
                        Id = usuario.IdUsuario,
                        Nombre = usuario.NombreCompleto
                    },

                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return Json(resultado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerException = ex.InnerException?.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // MÃ©todo para obtener informaciÃ³n de PAT 
        public ActionResult InfoPAT(int id)
        {
            try
            {
                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == id);
                var actividades = db.actividadesSemanals.Where(a => a.IdEntrevistaInicial == id).ToList();

                if (pat == null)
                {
                    return Json(new { Error = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var resultado = new
                {
                    PatInfo = new
                    {
                        Id = pat.IdEntrevistaInicial,
                        Grupo = pat.TutoriaGrupal,
                        Tutor = pat.Tutor,
                        Estado = pat.estado
                    },
                    ActividadesInfo = new
                    {
                        Total = actividades.Count,
                        Completadas = actividades.Count(a => a.RealizoActividad),
                        Detalles = actividades.Take(5).Select(a => new
                        {
                            Id = a.IdActividad,
                            Semana = a.IdSemana,
                            Tipo = a.IdTipoTutoria,
                            Actividad = a.Actividad,
                            Realizada = a.RealizoActividad
                        }).ToList()
                    }
                };

                return Json(resultado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult CambiarEstadoPat(int idPat, string estado, int estadoRevision)
        {
            try
            {
                var pat = db.PATs.FirstOrDefault(x => x.IdEntrevistaInicial == idPat);
                if (pat == null)
                {
                    return Json(new { success = false, message = "PAT no encontrado" });
                }

                // Convertir estado a booleano (true = En progreso, false = Cerrado)
                bool nuevoEstado = estado == "true";

                // --- VALIDACIÓN PUNTO 2: IMPEDIR CERRAR SI FALTAN EVIDENCIAS ---
                // Si se intenta cerrar el PAT (nuevoEstado == false)
                if (nuevoEstado == false)
                {
                    // 1. Obtener cuántas semanas con actividades tiene este PAT
                    var semanasRequeridas = db.actividadesSemanals
                        .Where(a => a.IdEntrevistaInicial == idPat)
                        .Select(a => a.IdSemana).Distinct().Count();

                    // 2. Obtener cuántas evidencias APROBADAS existen en Mongo
                    // Nota: Usamos Task.Run para llamar al servicio asíncrono desde este método síncrono
                    var evidencias = Task.Run(() => _mongoService.ObtenerEvidenciasPorPATAsync(idPat)).Result;

                    var semanasAprobadas = evidencias
                        .Where(e => e.EstadoAprobacion == 1) // 1 = Aprobado
                        .Select(e => e.Metadata.Semana).Distinct().Count();

                    // 3. Comparar
                    if (semanasAprobadas < semanasRequeridas)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"No se puede cerrar el PAT. Faltan evidencias aprobadas. (Aprobadas: {semanasAprobadas} / Requeridas: {semanasRequeridas})"
                        });
                    }
                }
                // --- FIN VALIDACIÓN ---

                pat.estado = nuevoEstado;
                pat.EstadoRevision = estadoRevision;
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // MÃ©todo para obtener PATs del tutor actual
        public ActionResult VerificarActividades()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { Error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                // Obtener PATs del tutor actual
                var patsDelTutor = db.PATs.Where(p => p.IdTutor == usuario.IdUsuario).ToList();
                var patIds = patsDelTutor.Select(p => p.IdEntrevistaInicial).ToList();

                // Obtener actividades para todos los PATs del tutor
                var actividadesDelTutor = db.actividadesSemanals
                    .Where(a => patIds.Contains(a.IdEntrevistaInicial))
                    .ToList();

                // Contar actividades por PAT
                var actividadesPorPAT = actividadesDelTutor
                    .GroupBy(a => a.IdEntrevistaInicial)
                    .ToDictionary(g => g.Key, g => g.Count());

                var resultado = new
                {
                    TutorId = usuario.IdUsuario,
                    TutorNombre = usuario.NombreCompleto,
                    TotalActividadesEnTabla = db.actividadesSemanals.Count(),
                    PATsDelTutor = patsDelTutor.Count,
                    PatIds = patIds,
                    TotalActividadesDelTutor = actividadesDelTutor.Count,
                    ActividadesPorPAT = actividadesPorPAT,
                    DetallesPrimerosPATs = patsDelTutor.Take(3).Select(p => new
                    {
                        PatId = p.IdEntrevistaInicial,
                        Grupo = p.TutoriaGrupal,
                        ActividadesCount = actividadesDelTutor.Count(a => a.IdEntrevistaInicial == p.IdEntrevistaInicial),
                        PrimerasActividades = actividadesDelTutor
                            .Where(a => a.IdEntrevistaInicial == p.IdEntrevistaInicial)
                            .Take(2)
                            .Select(a => new
                            {
                                Id = a.IdActividad,
                                Semana = a.IdSemana,
                                Actividad = a.Actividad?.Substring(0, Math.Min(50, a.Actividad?.Length ?? 0)) + "..."
                            }).ToList()
                    }).ToList()
                };

                return Json(resultado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message, StackTrace = ex.StackTrace }, JsonRequestBehavior.AllowGet);
            }
        }

        // MÃ©todo debug de lista de actividades de PAT
        public ActionResult TestActividades(int id = 23169)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== TEST ACTIVIDADES PARA PAT ID: {id} ===");

                // 1. Verificar que el PAT existe
                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == id);
                if (pat == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: PAT no encontrado");
                    return Content("PAT no encontrado");
                }
                System.Diagnostics.Debug.WriteLine($"PAT encontrado: {pat.TutoriaGrupal}");

                // 2. Buscar actividades directamente
                var actividades = db.actividadesSemanals.Where(a => a.IdEntrevistaInicial == id).ToList();
                System.Diagnostics.Debug.WriteLine($"Actividades encontradas: {actividades.Count}");

                // 3. Mostrar detalles de cada actividad
                foreach (var actividad in actividades)
                {
                    System.Diagnostics.Debug.WriteLine($"- Actividad ID: {actividad.IdActividad}, Semana: {actividad.IdSemana}, Tipo: {actividad.IdTipoTutoria}, Descripción: {actividad.Actividad}");
                }

                // 4. Verificar tabla completa (primeras 10)
                var todasActividades = db.actividadesSemanals.Take(10).ToList();
                System.Diagnostics.Debug.WriteLine($"Total actividades en tabla (muestra): {todasActividades.Count}");
                foreach (var actividad in todasActividades)
                {
                    System.Diagnostics.Debug.WriteLine($"- ID: {actividad.IdActividad}, PAT: {actividad.IdEntrevistaInicial}, Semana: {actividad.IdSemana}");
                }

                // 5. Preparar resultado para mostrar en pantalla
                var resultado = new
                {
                    PATEncontrado = pat != null,
                    PATInfo = pat?.TutoriaGrupal ?? "N/A",
                    ActividadesCount = actividades.Count,
                    Actividades = actividades.Select(a => new {
                        a.IdActividad,
                        a.IdSemana,
                        a.IdTipoTutoria,
                        a.Actividad
                    }).ToList(),
                    TotalEnTabla = db.actividadesSemanals.Count()
                };

                ViewBag.TestResult = resultado;
                return View("TestActividades");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR en TestActividades: {ex.Message}");
                return Content($"Error: {ex.Message}");
            }
        }

        // MÃ©todo para cargar actividades en Index:
        private void CargarActividadesParaIndexCorregido(List<PAT> pats)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIO CARGA ACTIVIDADES CORREGIDO ===");

                if (pats == null || !pats.Any())
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Lista de PATs vacía o nula");
                    ViewBag.ActividadesPorPAT = new Dictionary<int, List<ActividadesSemanal>>();
                    ViewBag.Actividades = new List<ActividadesSemanal>();
                    return;
                }

                var patIds = pats.Select(p => p.IdEntrevistaInicial).ToList();
                System.Diagnostics.Debug.WriteLine($"Buscando actividades para PATs: {string.Join(", ", patIds)}");

                // Verificar conexiÃ³n a base de datos
                var totalActividadesEnTabla = db.actividadesSemanals.Count();
                System.Diagnostics.Debug.WriteLine($"Total actividades en tabla: {totalActividadesEnTabla}");

                // Buscar actividades
                var todasLasActividades = db.actividadesSemanals
                    .Where(x => patIds.Contains(x.IdEntrevistaInicial))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Actividades encontradas: {todasLasActividades.Count}");

                if (!todasLasActividades.Any())
                {
                    // Verificar si hay actividades para cualquier ID
                    var algunasActividades = db.actividadesSemanals.Take(5).ToList();
                    System.Diagnostics.Debug.WriteLine("Muestra de actividades en tabla:");
                    foreach (var act in algunasActividades)
                    {
                        System.Diagnostics.Debug.WriteLine($"  PAT ID: {act.IdEntrevistaInicial}, Actividad ID: {act.IdActividad}");
                    }
                }

                // Cargar relaciones
                foreach (var actividad in todasLasActividades)
                {
                    try
                    {
                        actividad.Semana = db.Semanas.FirstOrDefault(s => s.IdSemana == actividad.IdSemana);
                        // No intentar cargar Tipo si no existe la propiedad
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error cargando semana para actividad {actividad.IdActividad}: {ex.Message}");
                    }
                }

                // Crear diccionario
                var actividadesPorPAT = todasLasActividades
                    .GroupBy(a => a.IdEntrevistaInicial)
                    .ToDictionary(g => g.Key, g => g.ToList());

                System.Diagnostics.Debug.WriteLine($"=== DEBUG CONTROLADOR ===");
                System.Diagnostics.Debug.WriteLine($"Total PATs: {pats.Count}");
                System.Diagnostics.Debug.WriteLine($"Total actividades encontradas: {todasLasActividades.Count}");
                System.Diagnostics.Debug.WriteLine($"PATs en el diccionario: {string.Join(", ", actividadesPorPAT.Keys)}");
                foreach (var pat in pats)
                {
                    System.Diagnostics.Debug.WriteLine($"PAT ID: {pat.IdEntrevistaInicial}");
                }
                // Asignar a ViewBag
                ViewBag.ActividadesPorPAT = actividadesPorPAT;
                ViewBag.Actividades = todasLasActividades; // TambiÃ©n asignar todas las actividades

                System.Diagnostics.Debug.WriteLine("ViewBag asignado correctamente");
                System.Diagnostics.Debug.WriteLine("=== FIN CARGA ACTIVIDADES CORREGIDO ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR FATAL en CargarActividadesParaIndexCorregido: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                ViewBag.ActividadesPorPAT = new Dictionary<int, List<ActividadesSemanal>>();
                ViewBag.Actividades = new List<ActividadesSemanal>();
            }
        }

        // ====================================================
        // Acciones AJAX con filtros multi-selecciÃ³n
        // ====================================================

        [LecturaPermitida]
        [HttpPost]
        public JsonResult FiltrarPATsMultiSeleccion(
    string search = "",
    string[] carreras = null,
    string[] grados = null,
    string[] grupos = null,
    string[] periodos = null,
    string[] anos = null,
    string[] estados = null)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var tiempo = DateTime.Now;
                var periodoActual = ObtenerPeriodoActual(tiempo);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                List<PAT> pats;

                // --- CORRECCIÓN PUNTOS 1, 2 y 4: QUITAR FILTRO DE FECHA HARDCODEADO ---
                // Antes forzaba periodoActual y tiempo.Year. Ahora traemos todo para que los filtros decidan.
                if (usuario.IdNivel == 4) // Master
                {
                    pats = db.PATs
                        .Include(p => p.Periodo) // Asegurar carga de relaciones
                        .Include(p => p.Carrera)
                        .ToList();
                }
                else // Tutor/Coordinador
                {
                    pats = db.PATs
                        .Where(x => x.IdTutor == usuario.IdUsuario)
                        .Include(p => p.Periodo)
                        .Include(p => p.Carrera)
                        .ToList();
                }
                // -----------------------------------------------------------------------

                // Aplicar filtros multi-selección (Aquí es donde se filtrarán los años y periodos seleccionados)
                pats = AplicarFiltrosMultiSeleccion(pats, carreras, grados, grupos, periodos, anos, estados, periodoActual, tiempo);

                // Generar nomenclatura de grupos si es necesario (para visualización)
                foreach (var item in pats)
                {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == item.IdTutoriaGrupal);
                    item.TutoriaGrupal = GenerarNomenclaturaGrupo(tuto);
                }

                // Ordenar los PATs
                pats = pats.OrderBy(x => ObtenerClaveOrdenamientoGrupo(x.TutoriaGrupal)).ToList();

                // Mapear a objeto anónimo para evitar referencias circulares en JSON
                var resultData = pats.Select(x => new {
                    x.IdEntrevistaInicial,
                    x.TutoriaGrupal,
                    Tutor = x.Tutor,
                    Carrera = x.Carrera.Nombre,
                    Periodo = x.Periodo.Nombre,
                    Ano = x.Fecha.Year, // Asegúrate de devolver el Año
                    x.CantidadAlumno,
                    x.estado,
                    x.EstadoRevision, // Necesario para determinar el estado en JS
                                      // Calculamos el estado texto aquí para enviarlo listo al JS si se requiere
                    EstadoTexto = DeterminarEstadoPAT(x, periodoActual, tiempo)
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = resultData, // Devolvemos la data procesada
                    filtrosAplicados = new
                    {
                        search = search,
                        carreras = carreras ?? new string[0],
                        grados = grados ?? new string[0],
                        grupos = grupos ?? new string[0],
                        periodos = periodos ?? new string[0],
                        anos = anos ?? new string[0],
                        estados = estados ?? new string[0]
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al filtrar PATs: " + ex.Message
                });
            }
        }

        // MÃ©todo para obtener los valores del filtro mÃºltiple
        [HttpGet]
        public JsonResult ObtenerValoresFiltroMultiple(string filtro)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var tiempo = DateTime.Now;
                var periodoActual = ObtenerPeriodoActual(tiempo);

                var valores = ObtenerOpcionesFiltro(filtro, usuario.IdUsuario, periodoActual, tiempo.Year);

                return Json(new
                {
                    success = true,
                    valores = valores,
                    filtro = filtro
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // MÃ©todo para obtener estadísticas de los filtros actuales
        [HttpGet]
        public JsonResult ObtenerEstadisticasFiltrosActuales(
            string search = "",
            string[] carreras = null,
            string[] grados = null,
            string[] grupos = null,
            string[] periodos = null,
            string[] anos = null,
            string[] estados = null)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var tiempo = DateTime.Now;
                var periodoActual = ObtenerPeriodoActual(tiempo);

                // --- CORRECCIÓN: Traer todo el historial para estadísticas ---
                var pats = db.PATs.Where(x => x.IdTutor == usuario.IdUsuario).ToList();

                // Generar nomenclatura de grupos
                foreach (var item in pats)
                {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == item.IdTutoriaGrupal);
                    item.TutoriaGrupal = GenerarNomenclaturaGrupo(tuto);
                }

                // Aplicar filtros
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    pats = pats.Where(x =>
                        x.Tutor.ToLower().Contains(search) ||
                        x.Carrera.Nombre.ToLower().Contains(search) ||
                        x.TutoriaGrupal.ToLower().Contains(search) ||
                        x.Periodo.Nombre.ToLower().Contains(search)
                    ).ToList();
                }


                pats = AplicarFiltrosMultiSeleccion(pats, carreras, grados, grupos, periodos, anos, estados, periodoActual, tiempo);

                var estadisticas = new
                {
                    TotalFiltrados = pats.Count,
                    PorCarrera = pats.GroupBy(x => x.Carrera.Nombre).Select(g => new {
                        Nombre = g.Key,
                        Cantidad = g.Count()
                    }).ToList(),
                    PorEstado = new
                    {
                        Aprobados = pats.Count(x => DeterminarEstadoPAT(x, periodoActual, tiempo) == "Aprobado"),
                        EnProgreso = pats.Count(x => DeterminarEstadoPAT(x, periodoActual, tiempo) == "En progreso"),
                        Inactivos = pats.Count(x => DeterminarEstadoPAT(x, periodoActual, tiempo) == "Inactivo")
                    },
                    VulnerabilidadesTotales = new
                    {
                        Economico = pats.Sum(x => x.VunerableEconomico),
                        Academico = pats.Sum(x => x.VunerableAcademico),
                        Personal = pats.Sum(x => x.VunerablePersonal)
                    },
                    AlumnosTotales = pats.Sum(x => x.CantidadAlumno)
                };

                return Json(estadisticas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ====================================================
        // MÃ‰TODOS AUXILIARES PARA FILTROS MULTI-SELECCIÃ“N
        // ====================================================

        private List<PAT> AplicarFiltrosMultiSeleccion(
            List<PAT> pats,
            string[] carreras,
            string[] grados,
            string[] grupos,
            string[] periodos,
            string[] anos,
            string[] estados,
            int periodoActual,
            DateTime tiempo)
        {
            // Filtro por carrera no se aplica ya que estÃ¡ deshabilitado en la interfaz

            // Filtro por grados
            if (grados != null && grados.Length > 0 && grados.Any(g => !string.IsNullOrEmpty(g)))
            {
                var gradosValidos = grados.Where(g => !string.IsNullOrEmpty(g)).ToList();
                pats = pats.Where(x => {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == x.IdTutoriaGrupal);
                    if (tuto != null)
                    {
                        var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tuto.IdGrado);
                        return grado != null && gradosValidos.Contains(grado.Nombre);
                    }
                    return false;
                }).ToList();
            }

            // Filtro por grupos
            if (grupos != null && grupos.Length > 0 && grupos.Any(g => !string.IsNullOrEmpty(g)))
            {
                var gruposValidos = grupos.Where(g => !string.IsNullOrEmpty(g)).ToList();
                pats = pats.Where(x => gruposValidos.Any(g => x.TutoriaGrupal.Contains(g))).ToList();
            }

            // Filtro por perí­odos
            if (periodos != null && periodos.Length > 0 && periodos.Any(p => !string.IsNullOrEmpty(p)))
            {
                var periodosValidos = periodos.Where(p => !string.IsNullOrEmpty(p)).ToList();
                pats = pats.Where(x => periodosValidos.Any(p => x.Periodo.Nombre.Contains(p))).ToList();
            }

            // Filtro por años
            if (anos != null && anos.Length > 0 && anos.Any(a => !string.IsNullOrEmpty(a)))
            {
                var anosValidos = anos.Where(a => !string.IsNullOrEmpty(a)).ToList();
                pats = pats.Where(x => {
                    var anoString = x.Fecha.Year.ToString();
                    return anosValidos.Contains(anoString);
                }).ToList();
            }
            // Si no se especifica ningún año, mostrar todos los años (no filtrar)

            // Filtro por estados con valores default
            if (estados != null && estados.Length > 0 && estados.Any(e => !string.IsNullOrEmpty(e)))
            {
                var estadosValidos = estados.Where(e => !string.IsNullOrEmpty(e)).ToList();
                pats = pats.Where(x => {
                    var estadoPAT = DeterminarEstadoPAT(x, periodoActual, tiempo);
                    return estadosValidos.Contains(estadoPAT);
                }).ToList();
            }
            else
            {
                // Si no se especifican estados, aplicar valores por defecto
                pats = pats.Where(x => {
                    var estadoPAT = DeterminarEstadoPAT(x, periodoActual, tiempo);
                    return estadoPAT == "Aprobado" || estadoPAT == "En progreso";
                }).ToList();
            }

            return pats;
        }

        private List<string> ObtenerOpcionesFiltro(string filtro, int idTutor, int periodoActual, int año)
        {
            // --- CORRECCIÓN: Quitar filtros de fecha para ver opciones históricas ---
            var pats = db.PATs.Where(x => x.IdTutor == idTutor).ToList();

            List<string> valores = new List<string>();

            switch (filtro.ToLower())
            {
                case "carrera":
                    valores = pats.Select(x => x.Carrera.Nombre).Distinct().OrderBy(x => x).ToList();
                    break;
                case "periodo":
                    valores = pats.Select(x => x.Periodo.Nombre).Distinct().OrderBy(x => x).ToList();
                    break;
                case "estado":
                    valores = new List<string> { "Aprobado", "En progreso", "En revisión", "Rechazado", "Inactivo", "Cerrado" };
                    break;
                case "grado":
                    valores = pats.Select(x => {
                        var tuto = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == x.IdTutoriaGrupal);
                        var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tuto.IdGrado);
                        return grado?.Nombre;
                    }).Where(x => x != null).Distinct()
                    .OrderBy(x => {
                        if (int.TryParse(x, out int grado)) return grado;
                        return 999;
                    }).ToList();
                    break;
                case "grupo":
                    foreach (var item in pats)
                    {
                        var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == item.IdTutoriaGrupal);
                        item.TutoriaGrupal = GenerarNomenclaturaGrupo(tuto);
                    }
                    valores = pats.Select(x => x.TutoriaGrupal).Distinct()
                    .OrderBy(x => ObtenerClaveOrdenamientoGrupo(x)).ToList();
                    break;
                case "ano":
                    valores = pats.Select(x => x.Fecha.Year.ToString()).Distinct()
                    .OrderByDescending(x => {
                        if (int.TryParse(x, out int ano)) return ano;
                        return 0;
                    }).ToList();
                    break;
            }
            return valores;
        }

        /// Genera una clave de ordenamiento para grupos basada en grado y letra
        /// Formato: "001A", "001B", "002A", etc.
        private string ObtenerClaveOrdenamientoGrupo(string nombreGrupo)
        {
            if (string.IsNullOrEmpty(nombreGrupo))
                return "999Z";

            try
            {
                // Buscar patrón: números seguidos de letras (ej: "MIS1A" -> "1A")
                var match = System.Text.RegularExpressions.Regex.Match(nombreGrupo, @"(\d+)([A-Z]+)$");

                if (match.Success)
                {
                    int grado = int.Parse(match.Groups[1].Value);
                    string letra = match.Groups[2].Value;

                    // Formatear como "001A", "002B", etc. para ordenamiento correcto
                    return $"{grado:D3}{letra}";
                }

                // Fallback: buscar cualquier número y letra por separado
                var gradoMatch = System.Text.RegularExpressions.Regex.Match(nombreGrupo, @"\d+");
                var letraMatch = System.Text.RegularExpressions.Regex.Match(nombreGrupo, @"[A-Z]+");

                int gradoFallback = gradoMatch.Success ? int.Parse(gradoMatch.Value) : 999;
                string letraFallback = letraMatch.Success ? letraMatch.Value : "Z";

                return $"{gradoFallback:D3}{letraFallback}";
            }
            catch
            {
                // En caso de error, devolver una clave que ponga el elemento al final
                return "999Z";
            }
        }

        // REEMPLAZA CON ESTO (Para que coincida con tus filtros visuales)
        private string DeterminarEstadoPAT(PAT item, int periodoActual, DateTime tiempo)
        {
            if (item.IdPeriodo != periodoActual || item.Fecha.Year != tiempo.Year)
            {
                return "Inactivo";
            }
            else if (item.estado == true)
            {
                // Detallar el estado exacto
                if (item.EstadoRevision == 1) return "En revisión";
                if (item.EstadoRevision == 2) return "Aprobado"; // Aprobado pero aún abierto
                if (item.EstadoRevision == 3) return "Rechazado";
                return "En progreso";
            }
            else
            {
                return "Cerrado"; // Coincide con el filtro "Cerrado" y el ícono de palomita
            }
        }

        // Método para obtener estadí­sticas de filtros aplicados
        [HttpGet]
        public JsonResult ObtenerEstadisticasFiltros(
            string search = "",
            string carreras = "",
            string grados = "",
            string grupos = "",
            string periodos = "",
            string anos = "",
            string estados = "")
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var tiempo = DateTime.Now;
                var periodoActual = ObtenerPeriodoActual(tiempo);

                // Convertir strings a arrays
                var carrerasArray = string.IsNullOrEmpty(carreras) ? null : carreras.Split(',').Where(c => !string.IsNullOrEmpty(c.Trim())).ToArray();
                var gradosArray = string.IsNullOrEmpty(grados) ? null : grados.Split(',').Where(g => !string.IsNullOrEmpty(g.Trim())).ToArray();
                var gruposArray = string.IsNullOrEmpty(grupos) ? null : grupos.Split(',').Where(g => !string.IsNullOrEmpty(g.Trim())).ToArray();
                var periodosArray = string.IsNullOrEmpty(periodos) ? null : periodos.Split(',').Where(p => !string.IsNullOrEmpty(p.Trim())).ToArray();
                var anosArray = string.IsNullOrEmpty(anos) ? null : anos.Split(',').Where(a => !string.IsNullOrEmpty(a.Trim())).ToArray();
                var estadosArray = string.IsNullOrEmpty(estados) ? new string[] { "Aprobado", "En progreso" } : estados.Split(',').Where(e => !string.IsNullOrEmpty(e.Trim())).ToArray();

                // Obtener PATs del tutor
                var pats = db.PATs.Where(x => x.IdTutor == usuario.IdUsuario).ToList();

                // Generar nomenclatura de grupos
                foreach (var item in pats)
                {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == item.IdTutoriaGrupal);
                    item.TutoriaGrupal = GenerarNomenclaturaGrupo(tuto);
                }

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    pats = pats.Where(x =>
                        x.Tutor.ToLower().Contains(search) ||
                        x.Carrera.Nombre.ToLower().Contains(search) ||
                        x.TutoriaGrupal.ToLower().Contains(search) ||
                        x.Periodo.Nombre.ToLower().Contains(search) ||
                        x.Fecha.Year.ToString().Contains(search)
                    ).ToList();
                }

                // Aplicar filtros multi-selección
                pats = AplicarFiltrosMultiSeleccionMejorado(pats, carrerasArray, gradosArray, gruposArray, periodosArray, anosArray, estadosArray, periodoActual, tiempo);

                var estadisticas = new
                {
                    totalFiltrados = pats.Count,
                    porCarrera = pats.GroupBy(x => x.Carrera.Nombre).Select(g => new {
                        nombre = g.Key,
                        cantidad = g.Count()
                    }).OrderByDescending(x => x.cantidad).ToList(),
                    porEstado = new
                    {
                        aprobados = pats.Count(x => DeterminarEstadoPAT(x, periodoActual, tiempo) == "Aprobado"),
                        enProgreso = pats.Count(x => DeterminarEstadoPAT(x, periodoActual, tiempo) == "En progreso"),
                        inactivos = pats.Count(x => DeterminarEstadoPAT(x, periodoActual, tiempo) == "Inactivo")
                    },
                    porGrado = pats.GroupBy(x => {
                        var tuto = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == x.IdTutoriaGrupal);
                        var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tuto.IdGrado);
                        return grado?.Nombre ?? "Sin grado";
                    }).Select(g => new {
                        nombre = g.Key,
                        cantidad = g.Count()
                    }).OrderBy(x => {
                        if (int.TryParse(x.nombre, out int grado))
                            return grado;
                        return 999;
                    }).ToList(),
                    vulnerabilidadesTotales = new
                    {
                        economico = pats.Sum(x => x.VunerableEconomico),
                        academico = pats.Sum(x => x.VunerableAcademico),
                        personal = pats.Sum(x => x.VunerablePersonal)
                    },
                    alumnosTotales = pats.Sum(x => x.CantidadAlumno),
                    fechaConsulta = DateTime.Now
                };

                return Json(estadisticas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ====================================================
        // MÉTODOS AUXILIARES PARA FILTROS MULTI-SELECCIÓN
        // ====================================================

        private List<PAT> AplicarFiltrosMultiSeleccionMejorado(
            List<PAT> pats,
            string[] carreras,
            string[] grados,
            string[] grupos,
            string[] periodos,
            string[] anos,
            string[] estados,
            int periodoActual,
            DateTime tiempo)
        {
            // Filtro por carreras
            if (carreras != null && carreras.Length > 0 && carreras.Any(c => !string.IsNullOrEmpty(c)))
            {
                var carrerasValidas = carreras.Where(c => !string.IsNullOrEmpty(c)).ToList();
                pats = pats.Where(x => carrerasValidas.Contains(x.Carrera.Nombre)).ToList();
            }

            // Filtro por grados
            if (grados != null && grados.Length > 0 && grados.Any(g => !string.IsNullOrEmpty(g)))
            {
                var gradosValidos = grados.Where(g => !string.IsNullOrEmpty(g)).ToList();
                pats = pats.Where(x => {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == x.IdTutoriaGrupal);
                    if (tuto != null)
                    {
                        var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tuto.IdGrado);
                        return grado != null && gradosValidos.Contains(grado.Nombre);
                    }
                    return false;
                }).ToList();
            }

            // Filtro por grupos
            if (grupos != null && grupos.Length > 0 && grupos.Any(g => !string.IsNullOrEmpty(g)))
            {
                var gruposValidos = grupos.Where(g => !string.IsNullOrEmpty(g)).ToList();
                pats = pats.Where(x => gruposValidos.Any(g => x.TutoriaGrupal.Contains(g))).ToList();
            }

            // Filtro por períodos
            if (periodos != null && periodos.Length > 0 && periodos.Any(p => !string.IsNullOrEmpty(p)))
            {
                var periodosValidos = periodos.Where(p => !string.IsNullOrEmpty(p)).ToList();
                pats = pats.Where(x => periodosValidos.Any(p => x.Periodo.Nombre.Contains(p))).ToList();
            }

            // Filtro por años
            if (anos != null && anos.Length > 0 && anos.Any(a => !string.IsNullOrEmpty(a)))
            {
                var anosValidos = anos.Where(a => !string.IsNullOrEmpty(a)).ToList();
                pats = pats.Where(x => {
                    var anoString = x.Fecha.Year.ToString();
                    return anosValidos.Contains(anoString);
                }).ToList();
            }

            // Filtro por estados
            if (estados != null && estados.Length > 0 && estados.Any(e => !string.IsNullOrEmpty(e)))
            {
                var estadosValidos = estados.Where(e => !string.IsNullOrEmpty(e)).ToList();
                pats = pats.Where(x => {
                    var estadoPAT = DeterminarEstadoPAT(x, periodoActual, tiempo);
                    return estadosValidos.Contains(estadoPAT);
                }).ToList();
            }
            else
            {
                // Si no se especifican estados, aplicar valores por defecto
                pats = pats.Where(x => {
                    var estadoPAT = DeterminarEstadoPAT(x, periodoActual, tiempo);
                    return estadoPAT == "Aprobado" || estadoPAT == "En progreso";
                }).ToList();
            }

            return pats;
        }

        // Método para validar y procesar filtros de URL
        [HttpGet]
        public JsonResult ValidarFiltrosURL(
            string search = "",
            string carreras = "",
            string grados = "",
            string grupos = "",
            string periodos = "",
            string anos = "",
            string estados = "",
            string view = "cards",
            int page = 1)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                // Procesar filtros desde strings separados por comas
                var filtrosValidados = new
                {
                    search = search ?? "",
                    carreras = string.IsNullOrEmpty(carreras) ? new string[0] : carreras.Split(',').Where(c => !string.IsNullOrEmpty(c.Trim())).ToArray(),
                    grados = string.IsNullOrEmpty(grados) ? new string[0] : grados.Split(',').Where(g => !string.IsNullOrEmpty(g.Trim())).ToArray(),
                    grupos = string.IsNullOrEmpty(grupos) ? new string[0] : grupos.Split(',').Where(g => !string.IsNullOrEmpty(g.Trim())).ToArray(),
                    periodos = string.IsNullOrEmpty(periodos) ? new string[0] : periodos.Split(',').Where(p => !string.IsNullOrEmpty(p.Trim())).ToArray(),
                    anos = string.IsNullOrEmpty(anos) ? new string[0] : anos.Split(',').Where(a => !string.IsNullOrEmpty(a.Trim())).ToArray(),
                    estados = string.IsNullOrEmpty(estados) ? new string[] { "Aprobado", "En progreso" } : estados.Split(',').Where(e => !string.IsNullOrEmpty(e.Trim())).ToArray(),
                    view = (view == "table" || view == "cards") ? view : "cards",
                    page = Math.Max(1, page)
                };

                // Obtener opciones disponibles para validación
                var opcionesDisponibles = new
                {
                    carreras = ObtenerOpcionesFiltroTodos("carrera", usuario.IdUsuario),
                    grados = ObtenerOpcionesFiltroTodos("grado", usuario.IdUsuario),
                    grupos = ObtenerOpcionesFiltroTodos("grupo", usuario.IdUsuario),
                    periodos = ObtenerOpcionesFiltroTodos("periodo", usuario.IdUsuario),
                    anos = ObtenerOpcionesFiltroTodos("ano", usuario.IdUsuario),
                    estados = new List<string> { "Aprobado", "En progreso", "Inactivo" }
                };

                return Json(new
                {
                    success = true,
                    filtros = filtrosValidados,
                    opciones = opcionesDisponibles
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al validar filtros: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Guardar(int id)
        {
            var pAT = db.PATs.FirstOrDefault(x => x.IdEntrevistaInicial == id);
            if (pAT == null)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "No se encontró el PAT." });
                return HttpNotFound();
            }

            // PUNTO 8: VALIDACIÓN DE EVIDENCIAS AL CERRAR
            var semanasRequeridas = db.actividadesSemanals
                .Where(a => a.IdEntrevistaInicial == id)
                .Select(a => a.IdSemana).Distinct().Count();

            // Llamada síncrona forzada al servicio async para validar (o haz el método async Task<ActionResult>)
            var evidencias = Task.Run(() => _mongoService.ObtenerEvidenciasPorPATAsync(id)).Result;
            var semanasAprobadas = evidencias.Where(e => e.EstadoAprobacion == 1)
                                             .Select(e => e.Metadata.Semana).Distinct().Count();

            if (semanasAprobadas < semanasRequeridas)
            {
                string msg = $"No se puede cerrar. Faltan evidencias aprobadas ({semanasAprobadas}/{semanasRequeridas}).";
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction("Details", new { id = id });
            }

            // Validar que el PAT esté aprobado (EstadoRevision = 2) antes de finalizar
            if (pAT.EstadoRevision != 2)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "El PAT debe estar aprobado antes de finalizar. Primero debe enviarlo a revisión." });
                TempData["Error"] = "El PAT debe estar aprobado antes de finalizar. Primero debe enviarlo a revisión.";
                return RedirectToAction("Details", new { id = id });
            }

            pAT.estado = false;
            db.Entry(pAT).State = EntityState.Modified;
            db.SaveChanges();

            if (Request.IsAjaxRequest())
                return Json(new { success = true, message = "PAT aprobado correctamente." });
            return RedirectToAction("Index", new { id = id });
        }


        [LecturaPermitida]
        [HttpPost]
        public async Task<JsonResult> ObtenerIdsConPendientes(List<int> patIds)
        {
            if (patIds == null || !patIds.Any()) return Json(new { ids = new int[0] });

            // Lógica simple: iterar y checar. Optimizar si son muchos.
            var conPendientes = new List<int>();
            foreach (var id in patIds)
            {
                var evs = await _mongoService.ObtenerEvidenciasPorPATAsync(id);
                if (evs.Any(e => e.EstadoAprobacion == 0)) conPendientes.Add(id);
            }
            return Json(new { ids = conPendientes });
        }

        public ActionResult Guardar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var pAT = db.PATs.FirstOrDefault(x => x.IdEntrevistaInicial == id);
            if (pAT == null)
            {
                return HttpNotFound();
            }
            return View(pAT);
        }


        public ActionResult Edit(int? id)
        {
            SetListas();
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Verificar autenticacación
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Permitir a administradores editar cualquier PAT, tutores solo los suyos
            PAT pAT;
            if (usuario.IdNivel >= 3)
            {
                // Administradores pueden editar cualquier PAT
                pAT = db.PATs.Find(id);
            }
            else
            {
                // Tutores solo pueden editar sus PATs
                pAT = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == id && p.IdTutor == usuario.IdUsuario);
            }

            if (pAT == null)
            {
                return HttpNotFound();
            }
            ViewBag.id = pAT.IdEntrevistaInicial;
            return View(pAT);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(PAT pAT)
        {
            ViewBag.id = pAT.IdEntrevistaInicial;

            var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pAT.IdTutoriaGrupal);
            if (grupo == null)
            {
                ModelState.AddModelError("", "No se encontró la tutoría grupal asociada al PAT.");
                return View(pAT);
            }

            // 1) Alumnos activos del grupo (no por entrevistas)
            int cantidadAlumnos = QueryAlumnosDelGrupo(grupo).Count();

            // 2) Vulnerabilidades por la ÃšLTIMA EI de cada alumno (evita duplicados)
            var ultimas = UltimaEntrevistaPorAlumno(grupo);

            int vunEco = ultimas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 1);
            int vunAca = ultimas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 2);
            int vunPer = ultimas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 3);

            pAT.CantidadAlumno = cantidadAlumnos;
            pAT.VunerableEconomico = vunEco;
            pAT.VunerableAcademico = vunAca;
            pAT.VunerablePersonal = vunPer;

            var returnTo = Request["returnTo"];

            if (ModelState.IsValid)
            {
                db.Entry(pAT).State = EntityState.Modified;
                db.SaveChanges();

                if (returnTo == "AsignarAsesoresPatDetalles")
                    return RedirectToAction("PatDetalles", "AsignarAsesores", new { id = pAT.IdEntrevistaInicial });
                else
                    return RedirectToAction("Details", new { id = pAT.IdEntrevistaInicial });
            }

            return View(pAT);
        }


        public ActionResult AsignarTutoria(int? id)
        {
            List<Semana> semana = db.Semanas.ToList();
            var lista = db.actividadesSemanals.Where(x => x.IdEntrevistaInicial == id).ToList();
            foreach (var item in lista)
            {
                semana.RemoveAll(x => x.IdSemana == item.IdSemana);
            }
            ViewBag.IdSemana = semana.Select(p => new SelectListItem() { Value = p.IdSemana.ToString(), Text = p.Nombre }).ToList<SelectListItem>();
            ViewBag.IdTipoTutoria = new SelectList(db.TipoTutorias, "IdTipoTutoria", "Nombre");
            ViewBag.id = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarTutoria(ActividadSemanalAux actividadesSemanal, int id)
        {
            if (ModelState.IsValid)
            {
                ActividadesSemanal act1 = new ActividadesSemanal();
                ActividadesSemanal act2 = new ActividadesSemanal();

                act1.IdEntrevistaInicial = id;
                act1.IdSemana = actividadesSemanal.IdSemana;
                act1.IdTipoTutoria = 1;
                act1.Actividad = actividadesSemanal.Actividad1;
                db.actividadesSemanals.Add(act1);
                db.SaveChanges();

                act2.IdEntrevistaInicial = id;
                act2.IdSemana = actividadesSemanal.IdSemana;
                act2.IdTipoTutoria = 2;
                act2.Actividad = actividadesSemanal.Actividad2;
                db.actividadesSemanals.Add(act2);
                db.SaveChanges();
                return RedirectToAction("Details", new { id = id });
            }
            ViewBag.IdSemana = new SelectList(db.Semanas, "IdSemana", "Nombre");
            ViewBag.IdTipoTutoria = new SelectList(db.TipoTutorias, "IdTipoTutoria", "Nombre");
            ViewBag.id = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditActividad(ActividadesSemanal actividadesSemanal)
        {
            if (ModelState.IsValid)
            {
                var act = db.actividadesSemanals.Find(actividadesSemanal.IdActividad);
                if (act == null)
                {
                    return HttpNotFound();
                }

                // Actualiza los valores
                act.Actividad = actividadesSemanal.Actividad;
                act.Comentarios = actividadesSemanal.Comentarios;
                act.RealizoActividad = actividadesSemanal.RealizoActividad;

                db.Entry(act).State = EntityState.Modified;
                db.SaveChanges();

                return RedirectToAction("Details", new { id = act.IdEntrevistaInicial });
            }

            ViewBag.IdSemana = new SelectList(db.Semanas, "IdSemana", "Nombre");
            ViewBag.IdTipoTutoria = new SelectList(db.TipoTutorias, "IdTipoTutoria", "Nombre");
            return View(actividadesSemanal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Comentarios(ActividadesSemanal actividadesSemanal)
        {
            if (ModelState.IsValid)
            {
                var actividad = db.actividadesSemanals.Find(actividadesSemanal.IdActividad);
                if (actividad == null)
                {
                    return HttpNotFound();
                }

                // Actualiza los comentarios
                actividad.Comentarios = actividadesSemanal.Comentarios;
                actividad.RealizoActividad = actividadesSemanal.RealizoActividad;

                db.Entry(actividad).State = EntityState.Modified;
                db.SaveChanges();

                return RedirectToAction("Details", new { id = actividad.IdEntrevistaInicial });
            }

            return View(actividadesSemanal);
        }

        public ActionResult Delete(int? id)
        {
            SetListas();
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PAT pAT = db.PATs.Find(id);
            if (pAT == null)
            {
                return HttpNotFound();
            }
            return View(pAT);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            PAT pAT = db.PATs.Find(id);
            db.PATs.Remove(pAT);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // ACCIÓN PARA ELIMINAR PAT (Tu método EliminarPAT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarPAT(int id)
        {
            try
            {
                PAT pAT = db.PATs.Find(id);
                if (pAT != null)
                {
                    db.PATs.Remove(pAT);
                    db.SaveChanges();
                    TempData["Success"] = "PAT eliminado correctamente";
                }
                else
                {
                    TempData["Error"] = "PAT no encontrado";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar PAT: " + ex.Message;
            }
            return RedirectToAction("Index");
        }


        public ActionResult Reporte(int id, PAT pAT)
        {
            //Muestra la vista del reporte
            // --- INICIO DE LA CORRECCIÓN ---
            // 1. Obtener el PAT principal
            PAT patPrincipal = db.PATs.FirstOrDefault(x => x.IdEntrevistaInicial == id);
            if (patPrincipal == null)
            {
                return HttpNotFound();
            }

            // 2. Obtener su tutoría asociada
            var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == patPrincipal.IdTutoriaGrupal);

            // 3. Recalcular los datos (si la tutoría existe)
            if (tuto != null)
            {
                // 3a. Recalcular cantidad de alumnos
                patPrincipal.CantidadAlumno = QueryAlumnosDelGrupo(tuto).Count();
                /*
                // 3b. Recalcular vulnerabilidades
                var ultimasEntrevistas = UltimaEntrevistaPorAlumno(tuto);
                patPrincipal.VunerableEconomico = ultimasEntrevistas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 1);
                patPrincipal.VunerableAcademico = ultimasEntrevistas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 2);
                patPrincipal.VunerablePersonal = ultimasEntrevistas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 3);
                */
            }

            // 3c. Corregir el nombre del tutor (para jalar el nombre completo)
            // Si el tutor no tiene nombre o no tiene apellido (no hay espacio)
            if ((string.IsNullOrEmpty(patPrincipal.Tutor) || !patPrincipal.Tutor.Contains(" ")) && patPrincipal.IdTutor > 0)
            {
                var usuarioTutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == patPrincipal.IdTutor);
                if (usuarioTutor != null)
                {
                    patPrincipal.Tutor = usuarioTutor.NombreCompleto;
                }
            }

            // 4. Poner el PAT actualizado en la lista 'solicitud'
            List<PAT> solicitud = new List<PAT>();
            solicitud.Add(patPrincipal);
            // --- FIN DE LA CORRECCIÓN ---

            List<ActividadesSemanal> actividades = new List<ActividadesSemanal>();
            foreach (PAT v in solicitud)
            {
                actividades = db.actividadesSemanals.Where(x => x.IdEntrevistaInicial == v.IdEntrevistaInicial).ToList();
            }
            foreach (ActividadesSemanal x in actividades)
            {
                PAT aux = new PAT();
                if (x.IdSemana == 1)
                {
                    aux.Semana1 = "X";
                    aux.IdPeriodo = 1;
                }
                else if (x.IdSemana == 2)
                {
                    aux.Semana2 = "X";
                    aux.IdPeriodo = 2;
                }
                else if (x.IdSemana == 3)
                {
                    aux.Semana3 = "X";
                    aux.IdPeriodo = 3;
                }
                else if (x.IdSemana == 4)
                {
                    aux.Semana4 = "X";
                    aux.IdPeriodo = 4;
                }
                else if (x.IdSemana == 5)
                {
                    aux.Semana5 = "X";
                    aux.IdPeriodo = 5;
                }
                else if (x.IdSemana == 6)
                {
                    aux.Semana6 = "X";
                    aux.IdPeriodo = 6;
                }
                else if (x.IdSemana == 7)
                {
                    aux.Semana7 = "X";
                    aux.IdPeriodo = 7;
                }
                else if (x.IdSemana == 8)
                {
                    aux.Semana8 = "X";
                    aux.IdPeriodo = 8;
                }
                else if (x.IdSemana == 9)
                {
                    aux.Semana9 = "X";
                    aux.IdPeriodo = 9;
                }
                else if (x.IdSemana == 10)
                {
                    aux.Semana10 = "X";
                    aux.IdPeriodo = 10;
                }
                else if (x.IdSemana == 11)
                {
                    aux.Semana11 = "X";
                    aux.IdPeriodo = 11;
                }
                else if (x.IdSemana == 12)
                {
                    aux.Semana12 = "X";
                    aux.IdPeriodo = 12;
                }
                else if (x.IdSemana == 13)
                {
                    aux.Semana13 = "X";
                    aux.IdPeriodo = 13;
                }
                else if (x.IdSemana == 14)
                {
                    aux.Semana14 = "X";
                    aux.IdPeriodo = 14;
                }
                else if (x.IdSemana == 15)
                {
                    aux.Semana15 = "X";
                    aux.IdPeriodo = 15;
                }
                else
                {
                    aux.Semana16 = "X";
                    aux.IdPeriodo = 16;
                }
                if (x.IdTipoTutoria == 1)
                {
                    aux.TipoTutoria1 = "X";
                }
                else
                {
                    aux.TipoTutoria2 = "X";
                }
                if (x.RealizoActividad == true)
                {
                    aux.RealizoActividad = "SI"; // Cambiar "Si" por "SI"
                }
                else
                {
                    aux.RealizoActividad = "NO"; // Cambiar "No" por "NO"
                }

                // --- CAMBIO PRINCIPAL AQUÍ ---
                // Convertir Comentarios y Actividad a Mayúsculas, protegiendo contra nulos
                aux.Comentarios = (x.Comentarios ?? "").ToUpper();
                aux.Actividad = (x.Actividad ?? "").ToUpper();

                solicitud.Add(aux);
                aux = null;
            }

            try
            {
                foreach (PAT v in solicitud)
                {
                    //Carrera
                    Carrera carrera = db.Carreras.Find(v.IdCarrera);
                    if (carrera != null)
                    {
                        // Convertir nombre de carrera a Mayúsculas
                        v.Carreras = (carrera.Nombre ?? "").ToUpper();

                        //Periodo
                        Periodo periodo = db.Periodos.Find(v.IdPeriodo);
                        if (periodo != null)
                        {
                            // Convertir nombre de periodo a Mayúsculas
                            v.Periodos = (periodo.Nombre ?? "").ToUpper();
                        }

                        //Grupo (Usando la función auxiliar y convirtiendo a mayúsculas)
                        tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == v.IdTutoriaGrupal);
                        v.Grupo = GenerarNomenclaturaGrupo(tuto).ToUpper();

                        var cuatr = db.Periodos.FirstOrDefault(x => x.IdPeriodo == v.IdPeriodo);
                        if (cuatr != null)
                        {
                            // Convertir cuatrimestre a Mayúsculas
                            v.Cuatrimestre = (cuatr.Nombre ?? "").ToUpper();
                        }
                    }

                    // Convertir nombre del Tutor a Mayúsculas
                    if (!string.IsNullOrEmpty(v.Tutor))
                    {
                        v.Tutor = v.Tutor.ToUpper();
                    }
                }


                //Configuraremos el origen de datos del informe (.rdlc)
                ReportViewer report1 = new ReportViewer();//Objeto de report viewer
                ReportDataSource rds = new ReportDataSource();//origen de datos
                rds.Value = solicitud;//asigna la consulta de ventas como origen de datos
                rds.Name = "DataSet1";//Este nombre debe coincidir con el del informe
                report1.LocalReport.DataSources.Add(rds);//asignamos el origen de datos
                report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/ReportePAT.rdlc");
                ViewBag.ReportViewer = report1;//pasamos el objeto de reporte a la vista
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = ex.Message;
            }

            return View();
        }

        [HttpPost]
        public ActionResult Reporte(PAT pAT)
        {
            return View();
        }

        // MÉTODOS AUXILIARES Y AUTOMÁTICOS
        private void CrearPATsAutomaticamente(Usuario usuario, int periodoActual, int año)
        {
            // Obtener tutorías grupales del usuario para el período actual
            List<TutoriaGrupal> tutorias = db.TutoriaGrupals
                .Where(x => x.IdUsuario == usuario.IdUsuario &&
                           x.IdPeriodo == periodoActual &&
                           x.Año == año).ToList();

            // Obtener PATs ya existentes del usuario
            var patsExistentes = db.PATs.Where(x => x.IdTutor == usuario.IdUsuario &&
                                                   x.IdPeriodo == periodoActual &&
                                                   x.Fecha.Year == año)
                                        .Select(x => x.IdTutoriaGrupal).ToList();

            // Filtrar tutorÃ­as que no tienen PAT
            var tutoriasSinPAT = tutorias.Where(x => !patsExistentes.Contains(x.IdTutoriaGrupal)).ToList();

            // LOG de depuraciÃ³n
            System.Diagnostics.Debug.WriteLine($"[CrearPATsAutomaticamente] Usuario: {usuario.IdUsuario}, Periodo: {periodoActual}, Año: {año}");
            System.Diagnostics.Debug.WriteLine($"Tutorías grupales encontradas: {tutorias.Count}");
            System.Diagnostics.Debug.WriteLine($"PATs existentes: {patsExistentes.Count}");
            System.Diagnostics.Debug.WriteLine($"Tutorías sin PAT: {tutoriasSinPAT.Count}");
            foreach (var tutoria in tutoriasSinPAT)
            {
                System.Diagnostics.Debug.WriteLine($"Generando PAT para TutoriaGrupal ID: {tutoria.IdTutoriaGrupal}");
                var nuevoPAT = CrearPATParaTutoria(tutoria, usuario, periodoActual);
                db.PATs.Add(nuevoPAT);
            }

            if (tutoriasSinPAT.Any())
            {
                db.SaveChanges();
                System.Diagnostics.Debug.WriteLine($"PATs generados y guardados: {tutoriasSinPAT.Count}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No se genero ningún PAT nuevo.");
            }
        }

        private List<string> ObtenerOpcionesFiltroTodos(string filtro, int idTutor)
        {
            // Obtener TODOS los PATs del tutor sin filtrar por perÃ­odo o aÃ±o
            var pats = db.PATs.Where(x => x.IdTutor == idTutor).ToList();

            List<string> valores = new List<string>();

            switch (filtro.ToLower())
            {
                case "carrera":
                    valores = pats.Select(x => x.Carrera.Nombre).Distinct().OrderBy(x => x).ToList();
                    break;
                case "periodo":
                    valores = pats.Select(x => x.Periodo.Nombre).Distinct().OrderBy(x => x).ToList();
                    break;
                case "estado":
                    valores = new List<string> { "Aprobado", "En progreso", "Inactivo" };
                    break;
                case "grado":
                    valores = pats.Select(x => {
                        var tuto = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == x.IdTutoriaGrupal);
                        var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tuto.IdGrado);
                        return grado?.Nombre;
                    }).Where(x => x != null).Distinct()
                    .OrderBy(x => {
                        // Ordenamiento numÃ©rico para grados (0, 1, 2, 3...)
                        if (int.TryParse(x, out int grado))
                            return grado;
                        return 999; // Valores no numÃ©ricos al final
                    }).ToList();
                    break;
                case "grupo":
                    // Generar nomenclatura de grupos para obtener valores Ãºnicos
                    foreach (var item in pats)
                    {
                        var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == item.IdTutoriaGrupal);
                        item.TutoriaGrupal = GenerarNomenclaturaGrupo(tuto);
                    }
                    valores = pats.Select(x => x.TutoriaGrupal).Distinct()
                    .OrderBy(x => {
                        // Ordenamiento especial: primero por grado, luego por letra
                        return ObtenerClaveOrdenamientoGrupo(x);
                    }).ToList();
                    break;
                case "ano":
                    valores = pats.Select(x => x.Fecha.Year.ToString()).Distinct()
                    .OrderByDescending(x => {
                        // Ordenamiento descendente por aÃ±o (mÃ¡s reciente primero)
                        if (int.TryParse(x, out int ano))
                            return ano;
                        return 0; // Valores no numÃ©ricos al final
                    }).ToList();
                    break;
            }

            return valores;
        }


        private PAT CrearPATParaTutoria(TutoriaGrupal tutoria, Usuario usuario, int periodoActual)
        {
            // 1) Alumnos activos del grupo
            int cantidadAlumnos = QueryAlumnosDelGrupo(tutoria).Count();

            // 2) Vulnerabilidades por la ÃšLTIMA EI de cada alumno
            var ultimas = UltimaEntrevistaPorAlumno(tutoria);

            int vunEco = ultimas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 1);
            int vunAca = ultimas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 2);
            int vunPer = ultimas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 3);

            var nuevoPAT = new PAT
            {
                IdTutoriaGrupal = tutoria.IdTutoriaGrupal,
                IdCarrera = tutoria.IdCarrera,
                IdTutor = usuario.IdUsuario,
                IdPeriodo = periodoActual,
                Tutor = usuario.NombreCompleto,
                Fecha = DateTime.Now,
                CantidadAlumno = cantidadAlumnos,
                VunerableEconomico = vunEco,
                VunerablePersonal = vunPer,
                VunerableAcademico = vunAca,
                estado = true,
                DescripcionEconomico = string.Empty,
                DescripcionPersonal = string.Empty,
                DescripcionAcademico = string.Empty
            };

            return nuevoPAT;
        }


        private int ObtenerPeriodoActual(DateTime tiempo)
        {
            if (tiempo.Month >= 1 && tiempo.Month <= 4)
            {
                return 1;
            }
            else if (tiempo.Month >= 5 && tiempo.Month <= 8)
            {
                return 2;
            }
            else
            {
                return 3;
            }
        }

        // --- INICIO FUNCIÓN NOMENCLATURA CORREGIDA ---
        // REEMPLAZA esta función en PATsController.cs (línea 431)
        private string GenerarNomenclaturaGrupo(TutoriaGrupal tuto)
        {
            // --- INICIO DE LA CORRECCIÓN: Manejar si tuto es nulo ---
            if (tuto == null) return "SIN GRUPO";
            // --- FIN DE LA CORRECCIÓN ---

            var grupo = "";
            // ELIMINADO: var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == tuto.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == tuto.IdCarrera);
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == tuto.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == tuto.IdGrupo);

            // --- LÓGICA ACTUALIZADA (SIN PREFIJO DE TURNO) ---
            grupo += c?.Nomenclatura ?? "??";
            grupo += grado?.Nombre ?? "?";
            grupo += grup?.Nombre ?? "?";
            // --- FIN LÓGICA ACTUALIZADA ---

            return grupo;
        }
        // --- FIN FUNCIÓN NOMENCLATURA CORREGIDA ---


        private void SetListas()
        {
            ViewBag.Careras = db.Carreras.ToList();
        }


        public ActionResult PatDetalles(int id, string returnUrl = null)
        {
            try
            {
                // Si hay returnUrl, guardarlo en TempData para usar despuÃ©s
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    TempData["ReturnUrl"] = returnUrl;
                }

                return RedirectToAction("Details", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al acceder a los detalles del PAT: " + ex.Message;

                // Si hay returnUrl, redirigir ahÃ­; sino, al Index
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult GuardarNotaActividad(int IdActividad, string Comentarios, bool RealizoActividad)
        {
            try
            {
                var actividad = db.actividadesSemanals.Find(IdActividad);
                if (actividad == null)
                {
                    return Json(new { success = false, message = "Actividad no encontrada." });
                }

                // Aplicar los cambios
                actividad.Comentarios = Comentarios ?? "";
                actividad.RealizoActividad = RealizoActividad;

                db.Entry(actividad).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();

                return Json(new { success = true, message = "Nota guardada." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar: " + ex.Message });
            }
        }

        public ActionResult PatReporte(int id, string returnUrl = null)
        {
            try
            {
                // Si hay returnUrl, guardarlo en TempData para usar despuÃ©s
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    TempData["ReturnUrl"] = returnUrl;
                }

                return RedirectToAction("Reporte", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al acceder al reporte del PAT: " + ex.Message;

                // Si hay returnUrl, redirigir ahÃ­; sino, al Index
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index");
            }
        }

        // MÃ‰TODOS AUXILIARES ADICIONALES PARA MULTI-SELECCIÃ“N

        /// Convierte un array de strings en una cadena separada por comas para logging
        private string ArrayToString(string[] array)
        {
            return array != null && array.Length > 0 ? string.Join(", ", array.Where(x => !string.IsNullOrEmpty(x))) : "Ninguno";
        }

        /// Valida que los filtros recibidos sean vÃ¡lidos
        private bool ValidarFiltros(string[] carreras, string[] grados, string[] grupos, string[] periodos, string[] estados)
        {
            // AquÃ­ puedes agregar validaciones especÃ­ficas si es necesario
            // Por ejemplo, verificar que los valores existan en la base de datos
            return true;
        }

        /// Obtiene un resumen de los filtros aplicados para mostrar al usuario
        [HttpGet]
        public JsonResult ObtenerResumenFiltros(
            string search = "",
            string[] carreras = null,
            string[] grados = null,
            string[] grupos = null,
            string[] periodos = null,
            string[] anos = null,
            string[] estados = null)
        {
            try
            {
                var resumen = new
                {
                    busqueda = !string.IsNullOrEmpty(search) ? search : null,
                    filtros = new
                    {
                        carreras = carreras?.Where(c => !string.IsNullOrEmpty(c)).ToArray() ?? new string[0],
                        grados = grados?.Where(g => !string.IsNullOrEmpty(g)).ToArray() ?? new string[0],
                        grupos = grupos?.Where(g => !string.IsNullOrEmpty(g)).ToArray() ?? new string[0],
                        periodos = periodos?.Where(p => !string.IsNullOrEmpty(p)).ToArray() ?? new string[0],
                        anos = anos?.Where(a => !string.IsNullOrEmpty(a)).ToArray() ?? new string[0],
                        estados = estados?.Where(e => !string.IsNullOrEmpty(e)).ToArray() ?? new string[0]
                    },
                    hayFiltrosActivos = !string.IsNullOrEmpty(search) ||
                                       (carreras?.Any(c => !string.IsNullOrEmpty(c)) == true) ||
                                       (grados?.Any(g => !string.IsNullOrEmpty(g)) == true) ||
                                       (grupos?.Any(g => !string.IsNullOrEmpty(g)) == true) ||
                                       (periodos?.Any(p => !string.IsNullOrEmpty(p)) == true) ||
                                       (anos?.Any(a => !string.IsNullOrEmpty(a)) == true) ||
                                       (estados?.Any(e => !string.IsNullOrEmpty(e)) == true)
                };

                return Json(resumen, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        // -------------------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeshacerAprobacion(int id)
        {
            var pat = db.PATs.Find(id);
            if (pat != null && pat.estado == false)
            {
                pat.estado = true;
                db.SaveChanges();
                if (Request.IsAjaxRequest())
                    return Json(new { success = true, message = "El PAT ha sido reactivado correctamente." });
                TempData["Success"] = "El PAT ha sido reactivado correctamente.";
            }
            else
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "No se pudo reactivar el PAT." });
                TempData["Error"] = "No se pudo reactivar el PAT.";
            }
            return RedirectToAction("Details", new { id = id });
        }

        // MÃ©todo para enviar PAT a revisiÃ³n
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EnviarRevision(int id)
        {
            try
            {
                var pat = db.PATs.Find(id);
                if (pat == null)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "PAT no encontrado." });
                    TempData["Error"] = "PAT no encontrado.";
                    return RedirectToAction("Index");
                }

                // Verificar que el PAT esté en progreso (estado = true)
                if (pat.estado == false)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "No se puede enviar a revisión un PAT finalizado." });
                    TempData["Error"] = "No se puede enviar a revisión un PAT finalizado.";
                    return RedirectToAction("Details", new { id = id });
                }

                // Cambiar estado a "En revisión"
                pat.EstadoRevision = 1;
                db.Entry(pat).State = EntityState.Modified;
                db.SaveChanges();

                string mensaje = pat.EstadoRevision == 3
                    ? "PAT reenviado a revisión correctamente."
                    : "PAT enviado a revisión correctamente.";

                if (Request.IsAjaxRequest())
                    return Json(new { success = true, message = mensaje });

                TempData["Success"] = mensaje;
                return RedirectToAction("Details", new { id = id });
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Error: " + ex.Message });

                TempData["Error"] = "Error al enviar el PAT a revisión: " + ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }



        // GET: PATs/Details/5
        public ActionResult Details(int? id)
        {
            SetListas();
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            PAT pAT = db.PATs.Find(id);
            if (pAT == null) return HttpNotFound();

            var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pAT.IdTutoriaGrupal);

            if (tuto != null)
            {
                // 1. Obtener alumnos históricos del grupo
                var todosAlumnos = db.DatosPersonales.Where(dp =>
                    dp.IdCarrera == tuto.IdCarrera && dp.IdGrado == tuto.IdGrado &&
                    dp.IdGrupo == tuto.IdGrupo && dp.IdTurno == tuto.IdTurno &&
                    dp.IdPeriodo == tuto.IdPeriodo && dp.Año == tuto.Año
                ).ToList();

                // --- CORRECCIÓN: Filtrar REALMENTE a los activos ---
                // NO usar dp.Estado == true porque puede estar desincronizado con revocaciones.
                // Usar la tabla Bajas como fuente de verdad.
                var idsTodos = todosAlumnos.Select(x => x.IdPersona).ToList();

                // Identificar IDs que son BAJAS ACTIVAS (Dropouts reales)
                var idsBajasReales = db.Bajas
                    .Where(b => idsTodos.Contains(b.IdPersona) && b.Activo == true)
                    .Select(b => b.IdPersona)
                    .ToList();

                ViewBag.CantidadBajas = idsBajasReales.Count;

                // Alumnos Activos = Todos - BajasReales
                // Estos son los únicos que deben contar para vulnerabilidades
                var idsAlumnosActivos = idsTodos.Except(idsBajasReales).ToList();

                pAT.CantidadAlumno = todosAlumnos.Count; // Total en lista (incluye bajas) o idsAlumnosActivos.Count (solo activos)? 
                                                         // Generalmente CantidadAlumno es "Matrícula total", dejémoslo en todosAlumnos.Count o ajustamos según feedback.
                                                         // Para consistencia con el "Total Vulnerables + No", usaremos idsAlumnosActivos luego si fuera necesario, 
                                                         // pero el usuario quiere que el badge "TOTAL" sume lo que se ve.

                // --- LÓGICA DE VULNERABILIDAD BASADA EN ÚLTIMO SEGUIMIENTO ---
                // Calcular fechas del periodo
                int ano = tuto.Año;
                int idPeriodo = tuto.IdPeriodo;
                DateTime fechaInicio, fechaFin;

                if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
                else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
                else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

                var seguimientos = (from s in db.Seguimientoes
                                    join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                    where idsAlumnosActivos.Contains(i.IdPersona) && // <-- MODIFICADO: Solo activos (excluye bajas)
                                          s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                    select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                                   .ToList();

                var ultimosSeguimientos = seguimientos
                    .GroupBy(x => x.IdPersona)
                    .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                    .ToList();

                // Recalcular siempre (ignorar lo guardado en PAT para tener dato fresco)
                // Case-insensitive check para coincidir con "ECONOMICO", "Economico", etc.
                pAT.VunerableEconomico = ultimosSeguimientos.Count(x => x != null && string.Equals(x.Vulnerabilidad, "Economico", StringComparison.OrdinalIgnoreCase));
                pAT.VunerableAcademico = ultimosSeguimientos.Count(x => x != null && string.Equals(x.Vulnerabilidad, "Academico", StringComparison.OrdinalIgnoreCase));
                pAT.VunerablePersonal = ultimosSeguimientos.Count(x => x != null && string.Equals(x.Vulnerabilidad, "Personal", StringComparison.OrdinalIgnoreCase));

                // CAMBIO: Contar explícitamente los "No vulnerable"
                // Esto excluye a los que no tienen seguimiento (pendientes)
                int noVulnerablesCount = ultimosSeguimientos.Count(x => x != null &&
                    (string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Vulnerabilidad, "NO VULNERABLE", StringComparison.OrdinalIgnoreCase)));

                ViewBag.NoVulnerablesCount = noVulnerablesCount;
            }
            else
            {
                ViewBag.NoVulnerablesCount = 0;
                ViewBag.CantidadBajas = 0;
            }
            // --- FIN CALCULOS ACTUALIZADOS ---

            // El resto del cÃ³digo para generar el nombre del grupo y preparar la vista...
            var grupo = GenerarNomenclaturaGrupo(tuto); // <-- USA LA FUNCIÓN CORREGIDA
            pAT.TutoriaGrupal = grupo; // Asignar el nombre generado

            if (pAT.IdTutor > 0)
            {
                var usuarioTutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == pAT.IdTutor);
                if (usuarioTutor != null)
                {
                    // Asignamos el NombreCompleto real que viene de la tabla Usuarios
                    pAT.Tutor = (usuarioTutor.NombreCompleto ?? "").ToUpper();
                }
                else
                {
                    pAT.Tutor = "TUTOR NO ENCONTRADO";
                }
            }
            else
            {
                pAT.Tutor = "SIN ASIGNAR";
            }

            var actividades = db.actividadesSemanals.Where(x => x.IdEntrevistaInicial == id).ToList();
            ViewBag.Actividades = actividades.OrderBy(f => f.IdSemana).ToList();
            ViewBag.PAT = pAT; // Pasar el objeto pAT actualizado

            return View(pAT);
        }



        // MÃ©todo para obtener PATs pendientes de revisiÃ³n (MODIFICADO)
        [HttpGet]
        public ActionResult ObtenerPATsPendientesRevision()
        {
            try
            {
                // 1. Obtener usuario actual y su carrera
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }
                int idCarreraCoordinador = usuario.IdCarrera;

                var tiempo = DateTime.Now;
                int periodoActual = ObtenerPeriodoActual(tiempo);

                // 2. Obtener PATs pendientes de revisión.
                //    - Coordinador (Nivel 3): sólo de su carrera.
                //    - Master (Nivel 4): de todas las carreras.
                var patsPendientesQuery = db.PATs
                    .Where(p => p.EstadoRevision == 1 &&
                               p.IdPeriodo == periodoActual &&
                               p.Fecha.Year == tiempo.Year);
                if (usuario.IdNivel != 4)
                {
                    patsPendientesQuery = patsPendientesQuery.Where(p => p.IdCarrera == idCarreraCoordinador);
                }

                // 3. Materializar primero los IDs/tutor/carrera, luego enriquecer en memoria.
                //    Evitamos sub-queries con ?? dentro del Select de LINQ-to-Entities,
                //    que pueden fallar en EF6 al traducirse a SQL.
                var rows = patsPendientesQuery
                    .Select(p => new {
                        Id = p.IdEntrevistaInicial,
                        IdTutoriaGrupal = p.IdTutoriaGrupal,
                        IdTutor = p.IdTutor,
                        TutorCache = p.Tutor,
                        CarreraNombre = p.Carrera != null ? p.Carrera.Nombre : null
                    })
                    .ToList();

                var idsTutores = rows.Select(r => r.IdTutor).Distinct().ToList();
                var dicTutores = db.Usuarios
                                    .Where(u => idsTutores.Contains(u.IdUsuario))
                                    .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);

                // Resolución de tutor con fallback al cache de PAT.Tutor: si el IdTutor ya no
                // está en Usuarios (cuenta eliminada/reasignada), mostramos el nombre que
                // quedó guardado en el PAT al momento de su creación.
                Func<int, string, string> resolverNombreTutor = (idTutor, cache) =>
                {
                    if (idTutor > 0 && dicTutores.ContainsKey(idTutor) && !string.IsNullOrWhiteSpace(dicTutores[idTutor]))
                        return dicTutores[idTutor].ToUpper();
                    if (!string.IsNullOrWhiteSpace(cache))
                        return cache.ToUpper();
                    return "Sin tutor";
                };

                var resultado = rows
                    .Select(r => new {
                        Id = r.Id,
                        Grupo = GenerarNomenclaturaGrupoSinTurno(r.IdTutoriaGrupal),
                        Tutor = resolverNombreTutor(r.IdTutor, r.TutorCache),
                        Carrera = r.CarreraNombre ?? "Sin carrera"
                    })
                    .OrderBy(p => p.Grupo)
                    .ToList();

                return Json(resultado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR en ObtenerPATsPendientesRevision: {ex.ToString()}");
                string detalle = (ex.Message ?? "Error desconocido").Replace("\r", " ").Replace("\n", " ");
                return Json(new { error = "Error al obtener PATs pendientes: " + detalle }, JsonRequestBehavior.AllowGet);
            }
        }

        // Método auxiliar para generar nomenclatura SIN inicial de turno
        private string GenerarNomenclaturaGrupoSinTurno(int idTutoriaGrupal)
        {
            try
            {
                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == idTutoriaGrupal);
                if (tutoriaGrupal != null)
                {
                    var grupo = "";
                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == tutoriaGrupal.IdCarrera);
                    var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tutoriaGrupal.IdGrado);
                    var grup = db.Grupoes.FirstOrDefault(g => g.IdGrupo == tutoriaGrupal.IdGrupo);

                    // Construir sin inicial de turno
                    grupo += carrera?.Nomenclatura ?? "";
                    grupo += grado?.Nombre ?? "";
                    grupo += grup?.Nombre ?? "";

                    return grupo;
                }
                return "Sin grupo";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarNomenclaturaGrupoSinTurno: {ex.Message}");
                return "Error";
            }
        }
        // MÃ©todo auxiliar para obtener el nombre del grupo
        private string ObtenerNombreGrupo(int idTutoriaGrupal)
        {
            try
            {
                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == idTutoriaGrupal);
                if (tutoriaGrupal != null)
                {
                    return GenerarNomenclaturaGrupo(tutoriaGrupal);
                }
                return "Sin grupo";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // MÃ©todo temporal para pruebas
        [HttpGet]
        public ActionResult TestObtenerPATsPendientes()
        {
            try
            {
                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;

                // Obtener todos los PATs para prueba
                var todosPATs = db.PATs
                    .Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == tiempo.Year)
                    .ToList();

                var resultado = todosPATs.Select(p => new
                {
                    Id = p.IdEntrevistaInicial,
                    Grupo = $"Grupo-{p.IdTutoriaGrupal}",
                    Tutor = p.Tutor ?? "Sin tutor",
                    EstadoRevision = p.EstadoRevision,
                    IdPeriodo = p.IdPeriodo,
                    Ano = p.Fecha.Year
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = resultado,
                    total = resultado.Count,
                    pendientes = resultado.Count(r => r.EstadoRevision == 1)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // MÃ©todo para aprobar PAT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPAT(int id)
        {
            var pat = db.PATs.Find(id);
            if (pat != null)
            {
                pat.EstadoRevision = 2; // Aprobado
                db.Entry(pat).State = EntityState.Modified;
                db.SaveChanges();

                if (Request.IsAjaxRequest())
                    return Json(new { success = true, message = "PAT aprobado correctamente." });
                TempData["Success"] = "PAT aprobado correctamente.";
            }
            else
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "No se pudo aprobar el PAT." });
                TempData["Error"] = "No se pudo aprobar el PAT.";
            }
            return RedirectToAction("Index");
        }

        // MÃ©todo para rechazar PAT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RechazarPAT(int id)
        {
            var pat = db.PATs.Find(id);
            if (pat != null)
            {
                pat.EstadoRevision = 3; // Rechazado
                db.Entry(pat).State = EntityState.Modified;
                db.SaveChanges();

                if (Request.IsAjaxRequest())
                    return Json(new { success = true, message = "PAT rechazado correctamente." });
                TempData["Success"] = "PAT rechazado correctamente.";
            }
            else
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "No se pudo rechazar el PAT." });
                TempData["Error"] = "No se pudo rechazar el PAT.";
            }
            return RedirectToAction("Index");
        }

        // --- INICIO MÉTODO CORREGIDO (VERSIÓN 3 - VALIDATION FAILED) ---
        // MÉTODO MEJORADO PARA EditarVariosComentarios EN PATsController.cs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EditarVariosComentarios(List<ActividadesSemanal> comentarios)
        {
            try
            {
                if (comentarios == null || !comentarios.Any())
                {
                    return Json(new { success = false, message = "No se recibieron comentarios para actualizar" });
                }

                System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Total comentarios recibidos: {comentarios.Count}");

                var comentariosUnicos = comentarios
                    .GroupBy(c => c.IdActividad)
                    .Select(g => g.First())
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Comentarios únicos a procesar: {comentariosUnicos.Count}");

                // --- INICIO DE LA CORRECCIÓN ---
                // Deshabilitar la validación temporalmente es la clave
                // para actualizar entidades parciales (stubs) sin que fallen por campos requeridos.
                db.Configuration.ValidateOnSaveEnabled = false;
                // --- FIN DE LA CORRECCIÓN ---

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        foreach (var comentario in comentariosUnicos)
                        {
                            // 1. Creamos un "stub" (marcador de posición) que solo tiene la Llave Primaria.
                            var actividadStub = new ActividadesSemanal { IdActividad = comentario.IdActividad };

                            // 2. Lo "adjuntamos" al contexto. EF ahora sabe que este objeto "existe" en la BD.
                            db.actividadesSemanals.Attach(actividadStub);

                            // 3. Actualizamos los valores en el stub.
                            actividadStub.Comentarios = comentario.Comentarios ?? "";
                            actividadStub.RealizoActividad = comentario.RealizoActividad;

                            // 4. Marcamos explícitamente SÓLO las propiedades que queremos actualizar.
                            // EF ignorará todos los demás campos (como 'Actividad', 'IdSemana', etc.)
                            db.Entry(actividadStub).Property(x => x.Comentarios).IsModified = true;
                            db.Entry(actividadStub).Property(x => x.RealizoActividad).IsModified = true;

                            System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Marcado para actualizar IdActividad: {comentario.IdActividad}");
                        }

                        // 5. Guardamos todos los cambios marcados a la vez.
                        // Como ValidateOnSaveEnabled = false, esto no fallará por el campo 'Actividad' nulo.
                        db.SaveChanges();
                        transaction.Commit();

                        System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Comentarios actualizados exitosamente");
                        return Json(new { success = true, message = "Comentarios actualizados correctamente" });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();

                        // Este bloque de código es para mostrarte errores de validación detallados si ocurren
                        if (ex is System.Data.Entity.Validation.DbEntityValidationException dbEx)
                        {
                            var errorMessages = dbEx.EntityValidationErrors
                                .SelectMany(x => x.ValidationErrors)
                                .Select(x => $"{x.PropertyName}: {x.ErrorMessage}");
                            var fullErrorMessage = string.Join("; ", errorMessages);
                            System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Error de VALIDACIÓN: {fullErrorMessage}");
                            return Json(new { success = false, message = "Error de validación: " + fullErrorMessage });
                        }

                        System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Error en transacción: {ex.Message}");
                        return Json(new { success = false, message = "Error en la transacción: " + ex.Message });
                    }
                    finally
                    {
                        // --- INICIO DE LA CORRECCIÓN ---
                        // RE-HABILITAR la validación, pase lo que pase, para el resto de la aplicación.
                        db.Configuration.ValidateOnSaveEnabled = true;
                        // --- FIN DE LA CORRECCIÓN ---
                    }
                }
            }
            catch (Exception ex)
            {
                // Re-habilitamos por si el error fue antes del using
                db.Configuration.ValidateOnSaveEnabled = true;
                System.Diagnostics.Debug.WriteLine($"[EditarVariosComentarios] Error general: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = "Error al actualizar los comentarios: " + ex.Message });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EditarVariasActividades(List<ActividadesSemanal> actividades)
        {
            try
            {
                if (actividades == null || !actividades.Any())
                {
                    return Json(new { success = false, message = "No se recibieron actividades para actualizar" });
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        foreach (var actividad in actividades)
                        {
                            var actividadExistente = db.actividadesSemanals.FirstOrDefault(a => a.IdActividad == actividad.IdActividad);
                            if (actividadExistente != null)
                            {
                                actividadExistente.Actividad = actividad.Actividad ?? "";
                                // Solo actualizar comentarios y realizado si se proporcionan
                                if (!string.IsNullOrEmpty(actividad.Comentarios))
                                {
                                    actividadExistente.Comentarios = actividad.Comentarios;
                                }
                                // RealizoActividad se mantiene como estaba si no se especifica

                                db.Entry(actividadExistente).State = EntityState.Modified;
                            }
                        }

                        db.SaveChanges();
                        transaction.Commit();

                        return Json(new { success = true, message = "Actividades actualizadas correctamente" });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Error en la transacción: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar las actividades: " + ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AsignarSemana(int idPat, int idSemana, string actividadGrupal, string actividadIndividual)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== INICIO AsignarSemana ===");
                System.Diagnostics.Debug.WriteLine($"idPat: {idPat}, idSemana: {idSemana}");
                System.Diagnostics.Debug.WriteLine($"actividadGrupal: '{actividadGrupal}'");
                System.Diagnostics.Debug.WriteLine($"actividadIndividual: '{actividadIndividual}'");

                // Validaciones de entrada
                if (string.IsNullOrWhiteSpace(actividadGrupal))
                {
                    return Json(new { success = false, message = "La actividad grupal es requerida" });
                }

                if (string.IsNullOrWhiteSpace(actividadIndividual))
                {
                    return Json(new { success = false, message = "La actividad individual es requerida" });
                }

                // Verificar si el PAT existe
                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == idPat);
                if (pat == null)
                {
                    return Json(new { success = false, message = "PAT no encontrado" });
                }

                // Verificar si la semana existe
                var semana = db.Semanas.FirstOrDefault(s => s.IdSemana == idSemana);
                if (semana == null)
                {
                    return Json(new { success = false, message = "Semana no válida" });
                }

                // Verificar que los tipos de tutorÃ­a existan
                var tipoGrupal = db.TipoTutorias.FirstOrDefault(t => t.IdTipoTutoria == 1);
                var tipoIndividual = db.TipoTutorias.FirstOrDefault(t => t.IdTipoTutoria == 2);

                if (tipoGrupal == null || tipoIndividual == null)
                {
                    return Json(new { success = false, message = "Tipos de tutorí­a no encontrados" });
                }

                // Verificar si ya existen actividades para esta semana
                var actividadesExistentes = db.actividadesSemanals
                    .Where(a => a.IdEntrevistaInicial == idPat && a.IdSemana == idSemana)
                    .ToList();

                if (actividadesExistentes.Any())
                {
                    return Json(new { success = false, message = "Ya existen actividades para esta semana" });
                }

                // Crear actividad GRUPAL (IdTipoTutoria = 1)
                var actividadGrupalNueva = new ActividadesSemanal
                {
                    IdEntrevistaInicial = idPat,
                    IdSemana = idSemana,
                    IdTipoTutoria = 1, // Grupal
                    Actividad = actividadGrupal.Trim(),
                    RealizoActividad = false,
                    Comentarios = string.Empty, // Importante: no null
                    Firma = false
                };

                // Crear actividad INDIVIDUAL (IdTipoTutoria = 2)
                var actividadIndividualNueva = new ActividadesSemanal
                {
                    IdEntrevistaInicial = idPat,
                    IdSemana = idSemana,
                    IdTipoTutoria = 2, // Individual
                    Actividad = actividadIndividual.Trim(),
                    RealizoActividad = false,
                    Comentarios = string.Empty, // Importante: no null
                    Firma = false
                };

                System.Diagnostics.Debug.WriteLine("Objetos creados, validando modelo...");

                // Validar modelos antes de guardar
                var context = new ValidationContext(actividadGrupalNueva);
                var results = new List<ValidationResult>();

                if (!Validator.TryValidateObject(actividadGrupalNueva, context, results, true))
                {
                    var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
                    return Json(new { success = false, message = "Error de validación grupal: " + errors });
                }

                context = new ValidationContext(actividadIndividualNueva);
                results = new List<ValidationResult>();

                if (!Validator.TryValidateObject(actividadIndividualNueva, context, results, true))
                {
                    var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
                    return Json(new { success = false, message = "Error de validación individual: " + errors });
                }

                System.Diagnostics.Debug.WriteLine("Modelos validados, guardando en BD...");

                // Usar transacciÃ³n para garantizar consistencia
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        db.actividadesSemanals.Add(actividadGrupalNueva);
                        db.actividadesSemanals.Add(actividadIndividualNueva);
                        db.SaveChanges();
                        transaction.Commit();

                        System.Diagnostics.Debug.WriteLine("Actividades guardadas exitosamente");
                        return Json(new { success = true, message = "Semana agregada correctamente" });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Error en transacción: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        }
                        return Json(new { success = false, message = "Error al guardar: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR GENERAL: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"INNER EXCEPTION: {ex.InnerException.Message}");
                }
                System.Diagnostics.Debug.WriteLine($"STACK TRACE: {ex.StackTrace}");

                return Json(new { success = false, message = "Error al procesar la solicitud: " + ex.Message });
            }
        }


        [HttpGet]
        public JsonResult ObtenerAlumnosVulnerables(int patId, int tipoVulnerabilidad)
        {
            try
            {
                // 0) Seguridad (Tu lógica original, que es buena)
                var usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);

                // 1) PAT autorizado (Tu lógica original, que es más segura)
                var pat = (usuario.IdNivel >= 3)
                    ? db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId)
                    : db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId && p.IdTutor == usuario.IdUsuario);

                if (pat == null)
                    return Json(new { success = false, message = "PAT no encontrado o no autorizado" }, JsonRequestBehavior.AllowGet);

                // 2) Grupo de la tutoría (Tu lógica original)
                var g = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (g == null)
                    return Json(new { success = false, message = "Tutoría grupal no encontrada" }, JsonRequestBehavior.AllowGet);

                // 3) Alumnos del grupo (Tu lógica original, GroupBy es robusta)
                var alumnosGrupo = QueryAlumnosDelGrupo(g)
                    .GroupBy(dp => dp.IdPersona)
                    .Select(gp => new
                    {
                        IdPersona = gp.Key,
                        Nombre = gp.Select(x => x.Nombre).FirstOrDefault(),
                        Matricula = gp.Select(x => x.Matricula).FirstOrDefault(),
                        Email = gp.Select(x => x.Email).FirstOrDefault(),
                        Foto = gp.Select(x => x.Foto).FirstOrDefault(),
                    })
                    .ToList();

                // 4) Última EI por alumno (Tu lógica original) NO - SEGUIMIENTOS
                // Estrategia: Obtener el último seguimiento para cada alumno del grupo.
                var idsTodos = alumnosGrupo.Select(x => x.IdPersona).ToList();

                // Filtro "Bajas Reales": Excluir quienes esten en Bajas con Activo==true
                var idsBajasReales = db.Bajas
                    .Where(b => idsTodos.Contains(b.IdPersona) && b.Activo == true)
                    .Select(b => b.IdPersona)
                    .ToList();

                var idsAlumnosActivos = idsTodos.Except(idsBajasReales).ToList();

                // Calcular fechas del periodo (Igual que en Details)
                int ano = g.Año;
                int idPeriodo = g.IdPeriodo;
                DateTime fechaInicio, fechaFin;

                if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
                else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
                else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

                var seguimientos = (from s in db.Seguimientoes
                                    join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                    where idsAlumnosActivos.Contains(i.IdPersona) && // Solo Activos
                                          s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                    select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                                   .ToList();

                var ultimosSeguimientos = seguimientos
                    .GroupBy(x => x.IdPersona)
                    // CAMBIO: Tomar el PRIMERO (OrderBy ascendente)
                    .Select(grp => grp.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                    .ToList();

                string vulnerabilidadBuscada = "";
                if (tipoVulnerabilidad == 1) vulnerabilidadBuscada = "Economico"; // Coincide con IdEleccionVunerabilidad == 1
                else if (tipoVulnerabilidad == 2) vulnerabilidadBuscada = "Academico";
                else if (tipoVulnerabilidad == 3) vulnerabilidadBuscada = "Personal";

                // 5) Ids vulnerables
                var idsVulnerables = ultimosSeguimientos
                    .Where(e => e != null && string.Equals(e.Vulnerabilidad, vulnerabilidadBuscada, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.IdPersona)
                    .Distinct()
                    .ToList();

                // 6) Resultado base (Tu lógica original)
                var resultado = alumnosGrupo
                    .Where(p => idsVulnerables.Contains(p.IdPersona))
                    .Select(p => new
                    {
                        idPersona = p.IdPersona,
                        nombre = p.Nombre ?? "Sin nombre",
                        matricula = p.Matricula ?? "Sin matrícula",
                        email = p.Email ?? string.Empty,
                        foto = ProcesarFoto(p.Foto) // Usando tu método ProcesarFoto
                    })
                    .ToList();

                // 6.1) DEDUPE (Tu lógica original)
                resultado = resultado
                    .GroupBy(x => new
                    {
                        P = x.idPersona,
                        M = (x.matricula ?? "").Trim().ToUpperInvariant(),
                        E = (x.email ?? "").Trim().ToUpperInvariant()
                    })
                    .Select(gx => gx.First())
                    .OrderBy(x => x.nombre)
                    .ToList();

                // --- INICIO DE LAS CORRECCIONES (basadas en AsignarAsesoresController.cs) ---

                // MEJORA 1: Limitar resultados para seguridad
                if (resultado.Count > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObtenerAlumnosVulnerables] Limitando de {resultado.Count} a 50 alumnos");
                    resultado = resultado.Take(50).ToList();
                }

                // MEJORA 2: Aumentar el MaxJsonLength
                // Esta es la corrección más importante para el error 500
                var jsonResult = new JsonResult
                {
                    Data = new
                    {
                        success = true,
                        alumnos = resultado,
                        total = resultado.Count,
                        tipoVulnerabilidad = tipoVulnerabilidad
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = Int32.MaxValue // <-- CORRECCIÓN CLAVE
                };

                return jsonResult;

                // --- FIN DE LAS CORRECCIONES ---
            }
            catch (Exception ex)
            {
                // MEJORA 3: Logging de error detallado
                // Esto te ayudará a ver el error real en la consola de "Salida" (Output) de Visual Studio
                System.Diagnostics.Debug.WriteLine($"ERROR FATAL en PATsController.ObtenerAlumnosVulnerables: {ex.ToString()}");

                return Json(new
                {
                    success = false,
                    // Devolvemos el mensaje genérico que tu frontend ya espera
                    message = "Error de conexión. Por favor, inténtelo de nuevo."
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult ObtenerAlumnosNoVulnerables(int patId)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId);
                if (pat == null)
                {
                    return Json(new { success = false, message = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (tutoriaGrupal == null)
                {
                    return Json(new { success = false, message = "Tutoría grupal no encontrada" }, JsonRequestBehavior.AllowGet);
                }

                // Obtener todos los alumnos activos del grupo (DISTINTOS por IdPersona)
                var alumnosEnGrupo = QueryAlumnosDelGrupo(tutoriaGrupal)
                    .GroupBy(dp => dp.IdPersona) // Agrupar por IdPersona para obtener registros Ãºnicos por alumno
                    .Select(g => g.FirstOrDefault()) // Tomar el primer registro de cada grupo
                    .Select(dp => new { dp.IdPersona, dp.Nombre, dp.Matricula, dp.Email, dp.Foto })
                    .ToList();

                // IDs de alumnos Activos en el grupo
                var idsTodos = alumnosEnGrupo.Select(x => x.IdPersona).ToList();

                // Filtro "Bajas Reales": Excluir quienes esten en Bajas con Activo==true
                var idsBajasReales = db.Bajas
                    .Where(b => idsTodos.Contains(b.IdPersona) && b.Activo == true)
                    .Select(b => b.IdPersona)
                    .ToList();

                var idsAlumnosActivos = idsTodos.Except(idsBajasReales).ToList();

                // Calcular fechas del periodo
                int ano = tutoriaGrupal.Año;
                int idPeriodo = tutoriaGrupal.IdPeriodo;
                DateTime fechaInicio, fechaFin;
                if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
                else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
                else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

                // Obtener seguimientos (mismo bloque que arriba)
                var seguimientos = (from s in db.Seguimientoes
                                    join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                    where idsAlumnosActivos.Contains(i.IdPersona) &&
                                          s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                    select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                                   .ToList();

                var ultimosSeguimientos = seguimientos
                    .GroupBy(x => x.IdPersona)
                    .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                    .ToList();

                // Lista de IDs que TIENEN alguna vulnerabilidad marcada
                var idsConVulnerabilidad = ultimosSeguimientos
                    .Where(x => x != null && (
                         string.Equals(x.Vulnerabilidad, "Economico", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Vulnerabilidad, "Academico", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Vulnerabilidad, "Personal", StringComparison.OrdinalIgnoreCase)
                    ))
                    .Select(x => x.IdPersona)
                    .ToList();

                // Alumnos NO vulnerables (EXPLÍCITO)
                // Solo aquellos cuyo último seguimiento dice "No vulnerable"
                var idsNoVulnerablesTotales = ultimosSeguimientos
                    .Where(x => x != null && (
                         string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Vulnerabilidad, "NO VULNERABLE", StringComparison.OrdinalIgnoreCase)
                    ))
                    .Select(x => x.IdPersona)
                    .ToList();

                // Filtrar la lista completa de alumnos del grupo usando los IDs combinados
                var resultadoFinal = alumnosEnGrupo
                    .Where(alumno => idsNoVulnerablesTotales.Contains(alumno.IdPersona))
                    .Select(a => new
                    {
                        idPersona = a.IdPersona,
                        nombre = a.Nombre ?? "Sin nombre",
                        matricula = a.Matricula ?? "Sin matrícula",
                        email = a.Email ?? "",
                        // << --- LÃ NEA CORREGIDA --- >>
                        foto = ProcesarFoto(a.Foto ?? "") // Usar ProcesarFoto directamente con el string a.Foto
                        // << --- FIN LÃ NEA CORREGIDA --- >>
                    })
                    .OrderBy(a => a.nombre).ToList();

                // MEJORA 1: Limitar resultados
                if (resultadoFinal.Count > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObtenerAlumnosNoVulnerables] Limitando de {resultadoFinal.Count} a 50 alumnos");
                    resultadoFinal = resultadoFinal.Take(50).ToList();
                }

                // MEJORA 2: Aumentar MaxJsonLength
                var jsonResult = new JsonResult
                {
                    Data = new
                    {
                        success = true,
                        alumnos = resultadoFinal,
                        total = resultadoFinal.Count // Mantenemos el total original para información.
                                                     // O usa resultadoFinal.Count después del Take(50) si prefieres.
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = Int32.MaxValue // <-- CORRECCIÓN CLAVE
                };
                return jsonResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR FATAL en PATsController.ObtenerAlumnosNoVulnerables: {ex.ToString()}"); // <-- Usa ToString()
                return Json(new
                {
                    success = false,
                    message = "Error de conexión. Por favor, inténtelo de nuevo." // <-- Mensaje consistente para el usuario
                }, JsonRequestBehavior.AllowGet);
            }
        }


        // En PATsController.cs

        [HttpGet]
        public JsonResult ObtenerAlumnosBajas(int patId)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null) return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId);

                // Validación de permiso para tutor (Nivel 2)
                if (usuario.IdNivel == 2 && pat.IdTutor != usuario.IdUsuario)
                    return Json(new { success = false, message = "No autorizado" }, JsonRequestBehavior.AllowGet);

                if (pat == null) return Json(new { success = false, message = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);

                var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (tuto == null) return Json(new { success = false, message = "Grupo no encontrado" }, JsonRequestBehavior.AllowGet);

                // 1. Obtener IDs de alumnos del grupo
                var alumnosDelGrupo = db.DatosPersonales.Where(dp =>
                    dp.IdCarrera == tuto.IdCarrera && dp.IdGrado == tuto.IdGrado &&
                    dp.IdGrupo == tuto.IdGrupo && dp.IdTurno == tuto.IdTurno &&
                    dp.IdPeriodo == tuto.IdPeriodo && dp.Año == tuto.Año
                ).Select(x => x.IdPersona).ToList();

                // 2. Buscar en tabla Bajas
                var listaBajas = (from b in db.Bajas
                                  join dp in db.DatosPersonales on b.IdPersona equals dp.IdPersona
                                  where alumnosDelGrupo.Contains(b.IdPersona) && b.Activo == true
                                  select new
                                  {
                                      idPersona = dp.IdPersona,
                                      nombre = dp.Nombre,
                                      matricula = dp.Matricula,
                                      email = dp.Email,
                                      foto = dp.Foto
                                  }).Distinct().ToList();

                var resultado = listaBajas.Select(lb => new {
                    lb.idPersona,
                    lb.nombre,
                    lb.matricula,
                    lb.email,
                    foto = ProcesarFoto(lb.foto) // Usar el método ProcesarFoto local de este controlador
                }).OrderBy(x => x.nombre).ToList();

                // --- CORRECCIÓN CLAVE AQUÍ ---
                var jsonResult = new JsonResult
                {
                    Data = new { success = true, alumnos = resultado },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = Int32.MaxValue // <--- ESTO EVITA EL ERROR 500 POR TAMAÑO
                };

                return jsonResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en PATsController.ObtenerAlumnosBajas: " + ex.Message);
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        #region Helpers de grupo/entrevistas (anti-duplicados)

        // Alumnos activos del grupo con misma clave de TutoriaGrupal
        private IQueryable<DatosPersonales> QueryAlumnosDelGrupo(TutoriaGrupal g)
        {
            // Modificado: NO filtrar por Estado aqui, para poder manejar BajasRevocadas correctamente arriba
            return db.DatosPersonales.Where(dp =>
                dp.IdCarrera == g.IdCarrera &&
                dp.IdGrado == g.IdGrado &&
                dp.IdGrupo == g.IdGrupo &&
                dp.IdTurno == g.IdTurno &&
                dp.IdPeriodo == g.IdPeriodo &&
                dp.Año == g.Año);
            // && dp.Estado == true); // <-- ELIMINADO: Se controla con la tabla Bajas en el Controller
        }

        // --- PEGAR ESTO DENTRO DE LA CLASE PATsController, AL FINAL JUNTO A LOS OTROS HELPERS ---

        // Helper para obtener la PRIMERA entrevista del periodo (SNAPSHOT / FOTO INICIAL)
        private List<EntrevistaInicial> PrimeraEntrevistaPorAlumno(TutoriaGrupal g)
        {
            var idsAlumnosDelGrupo = QueryAlumnosDelGrupo(g).Select(dp => dp.IdPersona).Distinct().ToList();

            var primeras = db.EntrevistaInicials
                .Where(ei => idsAlumnosDelGrupo.Contains(ei.IdPersona))
                .OrderBy(e => e.Fecha) // Orden ASCENDENTE (De la más vieja a la más nueva)
                .ThenBy(e => e.IdEntrevistaInicial)
                .ToList()
                .GroupBy(e => e.IdPersona)
                .Select(grp => grp.First()) // Tomamos la primera
                .ToList();

            return primeras;
        }

        // Ãšltima EntrevistaInicial por alumno del grupo (se hace in-memory sobre el subconjunto del grupo)
        private List<EntrevistaInicial> UltimaEntrevistaPorAlumno(TutoriaGrupal g)
        {
            var idsAlumnos = QueryAlumnosDelGrupo(g).Select(dp => dp.IdPersona).ToList();

            var ultimas = db.EntrevistaInicials
                .Where(ei => idsAlumnos.Contains(ei.IdPersona))
                .OrderByDescending(e => e.Fecha)
                .ThenByDescending(e => e.IdEntrevistaInicial)
                .ToList()
                .GroupBy(e => e.IdPersona)
                .Select(grp => grp.First())
                .ToList();

            return ultimas;
        }

        #endregion

        // MÃ©todo auxiliar para procesar fotos
        private string ProcesarFoto(string foto)
        {
            if (string.IsNullOrEmpty(foto))
                return "";

            try
            {
                // Si la foto viene desde EntrevistaInicial, ya está procesada por el getter
                // Solo necesitamos limpiar prefijos adicionales si existen
                if (foto.StartsWith("data:image"))
                {
                    var index = foto.IndexOf(",");
                    if (index > 0)
                    {
                        return foto.Substring(index + 1);
                    }
                }

                // Devolver la foto tal cual
                return foto;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcesarFoto] Error procesando foto: {ex.Message}");
                return "";
            }
        }

        // MÃ©todo de debug para verificar la estructura de datos de alumnos
        [HttpGet]
        public JsonResult DebugEstructuraAlumnos(int patId)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { error = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                // Permitir a administradores acceder a cualquier PAT, tutores solo a los suyos
                PAT pat;
                if (usuario.IdNivel >= 3)
                {
                    // Administradores pueden acceder a cualquier PAT
                    pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId);
                }
                else
                {
                    // Tutores solo pueden acceder a sus PATs
                    pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId && p.IdTutor == usuario.IdUsuario);
                }

                if (pat == null)
                {
                    return Json(new { error = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (tutoriaGrupal == null)
                {
                    return Json(new { error = "Tutoría grupal no encontrada" }, JsonRequestBehavior.AllowGet);
                }

                // Obtener todos los alumnos del grupo (sin filtro de vulnerabilidad)
                var todosLosAlumnos = (from ei in db.EntrevistaInicials
                                       join dp in db.DatosPersonales on ei.IdPersona equals dp.IdPersona
                                       where dp.IdCarrera == tutoriaGrupal.IdCarrera
                                          && dp.IdGrado == tutoriaGrupal.IdGrado
                                          && dp.IdGrupo == tutoriaGrupal.IdGrupo
                                          && dp.IdTurno == tutoriaGrupal.IdTurno
                                          && dp.IdPeriodo == tutoriaGrupal.IdPeriodo
                                          && dp.Año == tutoriaGrupal.Año
                                          && dp.Estado == true
                                       select new
                                       {
                                           IdPersona = dp.IdPersona,
                                           NombreDP = dp.Nombre,
                                           NombreEI = ei.Nombre,
                                           MatriculaDP = dp.Matricula,
                                           MatriculaEI = ei.Matricula,
                                           EmailDP = dp.Email,
                                           EmailEI = ei.Email,
                                           FotoDP = dp.Foto,
                                           FotoEI = ei.Foto,
                                           IdVulnerable = ei.IdVulnerable,
                                           IdEleccionVunerabilidad = ei.IdEleccionVunerabilidad
                                       }).ToList();

                // Contar vulnerabilidades por tipo
                var conteoVulnerabilidades = new
                {
                    TotalAlumnos = todosLosAlumnos.Count,
                    TotalVulnerables = todosLosAlumnos.Count(a => a.IdVulnerable == 1),
                    Economicos = todosLosAlumnos.Count(a => a.IdVulnerable == 1 && a.IdEleccionVunerabilidad == 1),
                    Academicos = todosLosAlumnos.Count(a => a.IdVulnerable == 1 && a.IdEleccionVunerabilidad == 2),
                    Personales = todosLosAlumnos.Count(a => a.IdVulnerable == 1 && a.IdEleccionVunerabilidad == 3)
                };

                // Muestra de algunos alumnos para ver la estructura
                var muestraAlumnos = todosLosAlumnos.Take(5).Select(a => new
                {
                    IdPersona = a.IdPersona,
                    NombreFinal = a.NombreDP ?? a.NombreEI,
                    MatriculaFinal = a.MatriculaDP ?? a.MatriculaEI,
                    EmailFinal = a.EmailDP ?? a.EmailEI,
                    TieneFotoDP = !string.IsNullOrEmpty(a.FotoDP),
                    TieneFotoEI = !string.IsNullOrEmpty(a.FotoEI),
                    IdVulnerable = a.IdVulnerable,
                    TipoVulnerabilidad = a.IdEleccionVunerabilidad
                }).ToList();

                return Json(new
                {
                    success = true,
                    parametrosBusqueda = new
                    {
                        idCarrera = tutoriaGrupal.IdCarrera,
                        idGrado = tutoriaGrupal.IdGrado,
                        idGrupo = tutoriaGrupal.IdGrupo,
                        idTurno = tutoriaGrupal.IdTurno,
                        idPeriodo = tutoriaGrupal.IdPeriodo,
                        año = tutoriaGrupal.Año
                    },
                    conteoVulnerabilidades = conteoVulnerabilidades,
                    muestraAlumnos = muestraAlumnos,
                    timestamp = DateTime.Now
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }
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