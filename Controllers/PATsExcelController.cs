using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Plataforma_Web.Models;
using System.Text;
using System.Net;
using PlataformaWeb;
using Plataforma_Web.Models.PrimeraEntrevista; // Asegúrate de tener este using
using Plataforma_Web.Models.MongoDB;          // Necesario para Reporte de Notas
using System.Data.Entity;                    // Necesario para Include()
using System.Text.RegularExpressions;       // <--- AÑADIR ESTE USING para el Regex

namespace Plataforma_Web.Controllers
{
    [CustomAuthorize(Nivel = 2)] // O el nivel apropiado para coordinadores/master
    public class PATsExcelController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        private readonly PlataformaWeb.Services.MongoDBService _mongoService = new PlataformaWeb.Services.MongoDBService(); // Para reporte de notas

        // ====================================================
        // MÉTODOS PARA GENERAR REPORTES EXCEL
        // ====================================================

        [HttpGet]
        public ActionResult GenerarExcelNotasPAT(
            int? carreraId = null,
            int? anio = null,
            string periodos = null,
            int? carrera = null,
            string estados = null,
            string grados = null,
            string grupos = null,
            string turnos = null,
            string search = null)
        {
            bool conParams = anio.HasValue
                || !string.IsNullOrEmpty(periodos)
                || carrera.HasValue
                || !string.IsNullOrEmpty(estados)
                || !string.IsNullOrEmpty(grados)
                || !string.IsNullOrEmpty(grupos)
                || !string.IsNullOrEmpty(turnos)
                || !string.IsNullOrEmpty(search);
            if (conParams)
            {
                // Compatibilidad: si llegan ambos (carreraId legacy y carrera nuevo), prefiere el nuevo.
                int? carreraEfectiva = carrera ?? carreraId;
                return GenerarExcelNotasPATConFiltros(anio, periodos, carreraEfectiva, estados, grados, grupos, turnos, search);
            }

            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;

                // Obtener PATs del período actual (en progreso y finalizados, NO inactivos) - filtrar por carrera si no es usuario master
                var patsQuery = db.PATs.Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == tiempo.Year);
                if (usuario.IdNivel != 4)
                {
                    patsQuery = patsQuery.Where(p => p.IdCarrera == usuario.IdCarrera);
                }
                // Si el usuario es master y seleccionó una carrera específica en el filtro
                if (usuario.IdNivel == 4 && carreraId.HasValue && carreraId.Value > 0)
                {
                    patsQuery = patsQuery.Where(p => p.IdCarrera == carreraId.Value);
                }
                var pats = patsQuery.ToList();

                // Obtener todas las notas de revisión (MongoDB)
                var notasPAT = _mongoService.ObtenerTodasNotasActivas();
                // Si hay filtro de carrera, filtrar las notas por los PATs de esa carrera
                if (pats.Any())
                {
                    var patIds = new HashSet<int>(pats.Select(p => p.IdEntrevistaInicial));
                    notasPAT = notasPAT.Where(n => patIds.Contains(n.PatId)).ToList();
                }

                // Agrupar notas por Tutor y PAT
                var notasAgrupadas = notasPAT
                    .GroupBy(n => new { n.PatId, n.Usuario })
                    .Select(g => new
                    {
                        PatId = g.Key.PatId,
                        Tutor = g.Key.Usuario,
                        Notas = g.ToList()
                    })
                    .ToList();

                var html = new StringBuilder();
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html>");
                html.AppendLine("<head>");
                html.AppendLine("<meta charset='UTF-8'>");
                html.AppendLine("<style>");
                html.AppendLine("table { border-collapse: collapse; width: 100%; }\nth, td { border: 1px solid #ddd; padding: 8px; text-align: left; }\nth { background-color: #4CAF50; color: white; font-weight: bold; }");
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");

                string tituloReporte = "Reporte de Comentarios de Revisión PATs - " + tiempo.ToString("dd/MM/yyyy");
                if (usuario.IdNivel != 4)
                {
                    var carreraDelUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                    tituloReporte += " - " + (carreraDelUsuario?.Nombre ?? "Carrera desconocida");
                }
                else if (carreraId.HasValue && carreraId.Value > 0) // Si es Master y filtró por carrera
                {
                    var carreraFiltrada = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId.Value);
                    tituloReporte += " - " + (carreraFiltrada?.Nombre ?? $"Carrera ID:{carreraId.Value}");
                }
                else // Master, todas las carreras
                {
                    tituloReporte += " - Todas las carreras";
                }
                html.AppendLine("<h2>" + tituloReporte + "</h2>");

                // --- INICIO CORRECCIÓN TUTOR ---
                // Cargar todos los tutores necesarios de una vez
                var idsTutores = pats.Select(p => p.IdTutor).Distinct().ToList();
                // Esta vez el diccionario SÍ se crea correctamente como <int, string>
                var dicTutores = db.Usuarios
                                    .Where(u => idsTutores.Contains(u.IdUsuario))
                                    .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);

                var gruposOrdenados = pats
                    .Select(pat => new {
                        PatId = pat.IdEntrevistaInicial,
                        TutoriaGrupalId = pat.IdTutoriaGrupal,
                        StaleTutorName = pat.Tutor, // <-- Obtenemos el nombre viejo
                        IdTutor = pat.IdTutor       // <-- Obtenemos el ID
                    })
                    .ToList()
                    .Select(patInfo => {
                        var tutoria = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == patInfo.TutoriaGrupalId);

                        // Lógica para obtener el nombre correcto
                        string nombreTutorCorrecto = "Sin tutor";
                        if (patInfo.IdTutor > 0 && dicTutores.ContainsKey(patInfo.IdTutor))
                        {
                            nombreTutorCorrecto = dicTutores[patInfo.IdTutor]; // Esto es string
                        }
                        else if (!string.IsNullOrEmpty(patInfo.StaleTutorName) && patInfo.StaleTutorName.Contains(" "))
                        {
                            nombreTutorCorrecto = patInfo.StaleTutorName; // Fallback al nombre viejo si es válido
                        }

                        return new
                        {
                            patInfo.PatId,
                            NombreGrupo = GenerarNomenclaturaGrupo(tutoria), // <-- Usará la nueva función corregida
                            Tutor = nombreTutorCorrecto // <-- Usa el nombre corregido
                        };
                    })
                    // --- INICIO CORRECCIÓN ORDEN ---
                    .OrderBy(g => ObtenerClaveOrdenamientoGrupo(g.NombreGrupo).Item1) // Ordena por número (Grado)
                    .ThenBy(g => ObtenerClaveOrdenamientoGrupo(g.NombreGrupo).Item2)  // Luego por letra (Grupo)
                                                                                      // --- FIN CORRECCIÓN ORDEN ---
                    .ToList();
                // --- FIN CORRECCIÓN TUTOR ---


                foreach (var grupo in gruposOrdenados)
                {
                    html.AppendLine("<h3 style='margin-top:30px;margin-bottom:5px;color:#1976D2;'>" + HttpUtility.HtmlEncode(grupo.NombreGrupo) + "</h3>");
                    html.AppendLine("<table style='margin-bottom:20px;'>");
                    html.AppendLine("<tr>");
                    html.AppendLine("<th>Tutor</th>");
                    html.AppendLine("<th>Fecha de observación</th>");
                    html.AppendLine("<th>Autor de observación</th>");
                    html.AppendLine("<th>Nota</th>");
                    html.AppendLine("</tr>");
                    var notasDelGrupo = notasPAT.Where(n => n.PatId == grupo.PatId).OrderBy(n => n.FechaCreacion).ToList();
                    if (notasDelGrupo.Count == 0)
                    {
                        html.AppendLine("<tr>");
                        html.AppendLine($"<td>{HttpUtility.HtmlEncode(grupo.Tutor)}</td>");
                        html.AppendLine("<td colspan='3' style='color:#888;'>Sin notas de revisión</td>");
                        html.AppendLine("</tr>");
                    }
                    else
                    {
                        foreach (var nota in notasDelGrupo)
                        {
                            html.AppendLine("<tr>");
                            html.AppendLine($"<td>{HttpUtility.HtmlEncode(grupo.Tutor)}</td>");
                            html.AppendLine($"<td>{nota.FechaCreacion:dd/MM/yyyy HH:mm}</td>");
                            html.AppendLine($"<td>{HttpUtility.HtmlEncode(nota.Usuario ?? "Desconocido")}</td>");
                            html.AppendLine($"<td>{HttpUtility.HtmlEncode(nota.Comentario ?? "")}</td>");
                            html.AppendLine("</tr>");
                        }
                    }
                    html.AppendLine("</table>");
                }
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                string fileName;
                if (usuario.IdNivel == 4)
                {
                    if (carreraId.HasValue && carreraId.Value > 0)
                    {
                        var carreraFiltrada = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId.Value);
                        string nomCarrera = carreraFiltrada?.Nomenclatura ?? $"ID{carreraId.Value}";
                        fileName = $"Reporte_Comentarios_PATs_{nomCarrera}_{tiempo:yyyyMMdd_HHmmss}.xls";
                    }
                    else
                    {
                        fileName = $"Reporte_Comentarios_PATs_TodasCarreras_{tiempo:yyyyMMdd_HHmmss}.xls";
                    }
                }
                else
                {
                    var carreraDelUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                    string nombreCarrera = carreraDelUsuario?.Nomenclatura ?? "CarreraDesconocida";
                    fileName = $"Reporte_Comentarios_PATs_{nombreCarrera}_{tiempo:yyyyMMdd_HHmmss}.xls";
                }

                var bytes = Encoding.UTF8.GetBytes(html.ToString());
                // Usar application/vnd.ms-excel para forzar descarga como .xls
                return File(bytes, "application/vnd.ms-excel", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelNotasPAT: {ex.ToString()}"); // Log detallado
                TempData["Error"] = "Error al generar el reporte de notas: " + ex.Message;
                // Considerar redirigir a una página de error o Index de PATsExcel si existe
                return RedirectToAction("PAT", "AsignarAsesores"); // O a donde tenga sentido
            }
        }

        [HttpGet]
        public ActionResult GenerarExcelActividades(
            int? anio = null,
            string periodos = null,
            int? carrera = null,
            string estados = null,
            string grados = null,
            string grupos = null,
            string turnos = null,
            string search = null)
        {
            // === CAMINO NUEVO: si vienen parámetros, generar uno o varios .xls (ZIP) ===
            bool conParams = anio.HasValue
                || !string.IsNullOrEmpty(periodos)
                || carrera.HasValue
                || !string.IsNullOrEmpty(estados)
                || !string.IsNullOrEmpty(grados)
                || !string.IsNullOrEmpty(grupos)
                || !string.IsNullOrEmpty(turnos)
                || !string.IsNullOrEmpty(search);
            if (conParams)
            {
                return GenerarExcelActividadesConFiltros(anio, periodos, carrera, estados, grados, grupos, turnos, search);
            }

            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    // Para GET, es mejor devolver error HTTP que JSON
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Usuario no autenticado");
                }

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;

                // Obtener PATs del período actual - filtrar por carrera si no es usuario master
                var patsQuery = db.PATs.Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == tiempo.Year);

                if (usuario.IdNivel != 4)
                {
                    patsQuery = patsQuery.Where(p => p.IdCarrera == usuario.IdCarrera);
                }

                var pats = patsQuery.Include(p => p.Carrera) // Incluir Carrera
                                    .ToList();

                // Optimización: Obtener todas las actividades y tutorías relevantes de una vez
                var patIds = pats.Select(p => p.IdEntrevistaInicial).ToList();
                var actividadesTodas = db.actividadesSemanals
                                        .Include(a => a.Tipo) // Incluir TipoTutoria
                                        .Where(a => patIds.Contains(a.IdEntrevistaInicial))
                                        .ToList();

                var tutoriaIds = pats.Select(p => p.IdTutoriaGrupal).Distinct().ToList();
                var tutoriasTodas = db.TutoriaGrupals
                                     .Include(t => t.Grado) // Incluir Grado
                                     .Include(t => t.Grupo) // Incluir Grupo (letra)
                                     .Where(t => tutoriaIds.Contains(t.IdTutoriaGrupal))
                                     .ToDictionary(t => t.IdTutoriaGrupal); // Diccionario para búsqueda rápida

                // --- INICIO CORRECCIÓN TUTOR ---
                var idsTutores = pats.Select(p => p.IdTutor).Distinct().ToList();
                var dicTutores = db.Usuarios
                                    .Where(u => idsTutores.Contains(u.IdUsuario))
                                    .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);
                // --- FIN CORRECCIÓN TUTOR ---

                var html = new StringBuilder();

                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html xmlns:x='urn:schemas-microsoft-com:office:excel'>");
                html.AppendLine("<head>");
                html.AppendLine("<meta charset='UTF-8'>");
                html.AppendLine("<style>");
                html.AppendLine("table { border-collapse: collapse; width: 100%; }");
                html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; vertical-align: top; }"); // Alineación vertical
                html.AppendLine("th { background-color: #4CAF50; color: white; font-weight: bold; }");
                // Estilo para texto largo
                html.AppendLine("td.wrap { white-space: normal; }"); // Permitir que el texto se ajuste
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");

                string tituloReporte = "Reporte de Actividades PATs - " + tiempo.ToString("dd/MM/yyyy");
                if (usuario.IdNivel != 4)
                {
                    var carreraDelUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera); // Recargar si es necesario
                    tituloReporte += " - " + (carreraDelUsuario?.Nombre ?? "Carrera desconocida");
                }
                else
                {
                    tituloReporte += " - Todas las carreras";
                }

                html.AppendLine("<h2>" + tituloReporte + "</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>Grado</th>");
                html.AppendLine("<th>Grupo</th>");
                html.AppendLine("<th>Tutor</th>");
                if (usuario.IdNivel == 4)
                {
                    html.AppendLine("<th>Carrera</th>");
                }
                html.AppendLine("<th>Tipo de Actividad</th>");
                html.AppendLine("<th>Nombre de Actividad</th>");
                html.AppendLine("<th>Comentarios</th>");
                html.AppendLine("<th>Realizado</th>");
                html.AppendLine("</tr>");

                // Llenar datos (usando datos precargados)
                // --- INICIO CORRECCIÓN ORDEN ---
                foreach (var pat in pats.OrderBy(p => ObtenerClaveOrdenamientoGrupo(GenerarNomenclaturaGrupo(tutoriasTodas.ContainsKey(p.IdTutoriaGrupal) ? tutoriasTodas[p.IdTutoriaGrupal] : null)).Item1)
                                       .ThenBy(p => ObtenerClaveOrdenamientoGrupo(GenerarNomenclaturaGrupo(tutoriasTodas.ContainsKey(p.IdTutoriaGrupal) ? tutoriasTodas[p.IdTutoriaGrupal] : null)).Item2)) // Ordenar por grupo
                // --- FIN CORRECCIÓN ORDEN ---
                {
                    var actividadesDelPat = actividadesTodas.Where(a => a.IdEntrevistaInicial == pat.IdEntrevistaInicial).ToList();
                    TutoriaGrupal tutoriaGrupal = tutoriasTodas.ContainsKey(pat.IdTutoriaGrupal) ? tutoriasTodas[pat.IdTutoriaGrupal] : null;

                    if (tutoriaGrupal != null)
                    {
                        var gradoNombre = tutoriaGrupal.Grado?.Nombre ?? "Sin grado";
                        var nombreGrupo = GenerarNomenclaturaGrupo(tutoriaGrupal); // Usar nomenclatura completa (YA CORREGIDA)

                        // --- INICIO CORRECCIÓN TUTOR ---
                        string nombreTutorCorrecto = "Sin tutor";
                        if (pat.IdTutor > 0 && dicTutores.ContainsKey(pat.IdTutor))
                        {
                            nombreTutorCorrecto = dicTutores[pat.IdTutor];
                        }
                        else if (!string.IsNullOrEmpty(pat.Tutor) && pat.Tutor.Contains(" "))
                        {
                            nombreTutorCorrecto = pat.Tutor; // Fallback al nombre viejo si es válido
                        }
                        // --- FIN CORRECCIÓN TUTOR ---

                        foreach (var actividad in actividadesDelPat)
                        {
                            var tipoTutoriaNombre = actividad.Tipo?.Nombre ?? "Sin tipo"; // Usar propiedad de navegación

                            html.AppendLine("<tr>");
                            html.AppendLine($"<td>{gradoNombre}</td>");
                            html.AppendLine($"<td>{nombreGrupo}</td>"); // Nomenclatura completa
                            html.AppendLine($"<td>{HttpUtility.HtmlEncode(nombreTutorCorrecto)}</td>"); // <-- USA NOMBRE CORREGIDO
                            if (usuario.IdNivel == 4)
                            {
                                // Usar pat.Carrera que ya incluimos
                                html.AppendLine($"<td>{HttpUtility.HtmlEncode(pat.Carrera?.Nombre ?? "Sin carrera")}</td>");
                            }
                            html.AppendLine($"<td>{HttpUtility.HtmlEncode(tipoTutoriaNombre)}</td>");
                            // Aplicar clase 'wrap' para texto largo
                            html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode(actividad.Actividad ?? "Sin actividad")}</td>");
                            html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode(actividad.Comentarios ?? "")}</td>");
                            html.AppendLine($"<td>{(actividad.RealizoActividad == true ? "Sí" : (actividad.RealizoActividad == false ? "No" : "Sin datos"))}</td>");
                            html.AppendLine("</tr>");
                        }
                    }
                    else // Manejar caso raro donde no se encuentra la tutoría
                    {
                        System.Diagnostics.Debug.WriteLine($"Advertencia: No se encontró TutoriaGrupal para PAT ID {pat.IdEntrevistaInicial} (TutoriaGrupal ID: {pat.IdTutoriaGrupal})");
                        // Podrías añadir una fila indicando el problema si es necesario
                    }
                }

                html.AppendLine("</table>");
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                string fileName;
                if (usuario.IdNivel == 4)
                {
                    fileName = $"Reporte_Actividades_PATs_TodasCarreras_{tiempo:yyyyMMdd_HHmmss}.xls";
                }
                else
                {
                    var carreraDelUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera); // Recargar
                    string nombreCarrera = carreraDelUsuario?.Nomenclatura ?? "CarreraDesconocida";
                    fileName = $"Reporte_Actividades_PATs_{nombreCarrera}_{tiempo:yyyyMMdd_HHmmss}.xls";
                }

                var bytes = Encoding.UTF8.GetBytes(html.ToString());

                // Usar application/vnd.ms-excel para forzar descarga como .xls
                return File(bytes, "application/vnd.ms-excel", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelActividades: {ex.ToString()}"); // Log detallado
                TempData["Error"] = "Error al generar el reporte de actividades: " + ex.Message;
                // Considerar redirigir a una página de error o Index de PATsExcel si existe
                return RedirectToAction("PAT", "AsignarAsesores"); // O a donde tenga sentido
            }
        }

        // ====================================================
        // MÉTODO CORREGIDO PARA GENERAR EXCEL DE VULNERABLES
        // ====================================================
        [HttpGet] // Mantenemos HttpGet ya que el JS fue cambiado a GET
        public ActionResult GenerarExcelVulnerables(
            int? anio = null,
            string periodos = null,
            int? carrera = null,
            string estados = null,
            string grados = null,
            string grupos = null,
            string turnos = null,
            string search = null)
        {
            bool conParams = anio.HasValue
                || !string.IsNullOrEmpty(periodos)
                || carrera.HasValue
                || !string.IsNullOrEmpty(estados)
                || !string.IsNullOrEmpty(grados)
                || !string.IsNullOrEmpty(grupos)
                || !string.IsNullOrEmpty(turnos)
                || !string.IsNullOrEmpty(search);
            if (conParams)
            {
                return GenerarExcelVulnerablesConFiltros(anio, periodos, carrera, estados, grados, grupos, turnos, search);
            }

            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Usuario no autenticado");
                }

                DateTime tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;

                // 1. OBTENER TUTORIAS GRUPALES (en lugar de PATs directamente)
                var tutoriasQuery = db.TutoriaGrupals
                    .Where(tg => tg.IdPeriodo == periodoActual && tg.Año == tiempo.Year);

                if (usuario.IdNivel != 4)
                {
                    tutoriasQuery = tutoriasQuery.Where(tg => tg.IdCarrera == usuario.IdCarrera);
                }

                var tutorias = tutoriasQuery.Include(tg => tg.Carrera) // Incluir Carrera para el nombre
                                            .ToList();

                // 2. OBTENER PATs EXISTENTES para esas tutorías (optimizado)
                var tutoriaIds = tutorias.Select(tg => tg.IdTutoriaGrupal).ToList();
                var patsExistentes = db.PATs
                    .Where(p => tutoriaIds.Contains(p.IdTutoriaGrupal))
                    .ToDictionary(p => p.IdTutoriaGrupal);

                // 3. PROCESAR CADA TUTORIA GRUPAL
                var datosParaReporte = new List<dynamic>();

                // Optimización: Cargar todos los tutores necesarios de una vez
                var tutorIdsNecesarios = tutorias.Select(tg => tg.IdUsuario)
                                                .Union(patsExistentes.Values.Select(p => p.IdTutor))
                                                .Distinct()
                                                .Where(id => id > 0)
                                                .ToList();

                // --- ESTA ES LA LÍNEA QUE SE CORRIGIÓ DE LA V2 ---
                var tutoresDic = db.Usuarios.Where(u => tutorIdsNecesarios.Contains(u.IdUsuario))
                                            .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);
                // --- FIN DE LA CORRECCIÓN ---


                foreach (var tg in tutorias)
                {
                    PAT pat = patsExistentes.ContainsKey(tg.IdTutoriaGrupal) ? patsExistentes[tg.IdTutoriaGrupal] : null;
                    string nombreGrupo = GenerarNomenclaturaGrupo(tg); // <-- ESTO USA LA FUNCIÓN CORREGIDA

                    // --- OBTENER NOMBRE DEL TUTOR (Optimizado) ---
                    int tutorId = pat?.IdTutor ?? tg.IdUsuario; // Prioriza el tutor del PAT
                    string tutorName = pat?.Tutor; // Nombre del PAT si existe

                    // Si el nombre del PAT es nulo, vacío, o no es un nombre completo (sin espacio)
                    if (string.IsNullOrEmpty(tutorName) || !tutorName.Contains(" "))
                    {
                        if (tutorId > 0 && tutoresDic.ContainsKey(tutorId))
                        {
                            tutorName = tutoresDic[tutorId]; // <-- ESTO AHORA FUNCIONA
                        }
                        else if (tutorId == 0 && tg.IdUsuario > 0 && tutoresDic.ContainsKey(tg.IdUsuario))
                        {
                            // Fallback: Si el PAT no tiene IdTutor, usar el IdUsuario del grupo
                            tutorName = tutoresDic[tg.IdUsuario]; // <-- ESTO AHORA FUNCIONA
                        }
                        else
                        {
                            tutorName = "Sin tutor asignado";
                        }
                    }
                    // --- FIN OBTENER NOMBRE DEL TUTOR ---

                    int vunEco = 0;
                    int vunAca = 0;
                    int vunPer = 0;
                    int cantidadAlumnos = 0;

                    var alumnosDelGrupoQuery = QueryAlumnosDelGrupo(tg);
                    cantidadAlumnos = alumnosDelGrupoQuery.Count();

                    if (cantidadAlumnos > 0)
                    {
                        var ultimasEntrevistas = UltimaEntrevistaPorAlumno(tg);
                        vunEco = ultimasEntrevistas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 1);
                        vunAca = ultimasEntrevistas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 2);
                        vunPer = ultimasEntrevistas.Count(e => e != null && e.IdVulnerable == 1 && e.IdEleccionVunerabilidad == 3);
                    }

                    datosParaReporte.Add(new
                    {
                        Grupo = nombreGrupo,
                        Tutor = tutorName,
                        CarreraNombre = tg.Carrera?.Nombre ?? "Sin carrera",
                        VunerableEconomico = vunEco,
                        VunerableAcademico = vunAca,
                        VunerablePersonal = vunPer,
                        TotalVulnerables = vunEco + vunAca + vunPer,
                        CantidadAlumnos = cantidadAlumnos
                    });
                }

                // 4. GENERAR CONTENIDO EXCEL
                string htmlContent = GenerarHtmlExcelVulnerabilidades(datosParaReporte, usuario, tiempo); // <--- LA LLAMADA
                // 5. GENERAR NOMBRE DE ARCHIVO Y DEVOLVER
                string fileName;
                string nombreCarreraArchivo = "CarreraDesconocida"; // Valor por defecto

                if (usuario.IdNivel != 4)
                {
                    // Cargar la carrera del usuario una sola vez
                    var carreraUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                    nombreCarreraArchivo = carreraUsuario?.Nomenclatura ?? nombreCarreraArchivo;
                    fileName = $"Reporte_Alumnos_Vulnerables_{nombreCarreraArchivo}_{tiempo:yyyyMMdd_HHmmss}.xls";
                }
                else // Nivel 4 (Master)
                {
                    fileName = $"Reporte_Alumnos_Vulnerables_TodasCarreras_{tiempo:yyyyMMdd_HHmmss}.xls";
                }


                var bytes = Encoding.UTF8.GetBytes(htmlContent);
                return File(bytes, "application/vnd.ms-excel", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelVulnerables: {ex.ToString()}");
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Error al generar el reporte: {ex.Message}");
            }
        }

        // ====================================================
        // MÉTODO AUXILIAR PARA GENERAR HTML (Debe estar DENTRO de la clase PATsExcelController)
        // ====================================================
        private string GenerarHtmlExcelVulnerabilidades(List<dynamic> datosParaReporte, Usuario usuario, DateTime tiempo) // <--- LA DEFINICIÓN
        {
            var html = new StringBuilder();

            // Título del reporte con información del filtro
            string tituloReporte = "Reporte de Alumnos Vulnerables - " + tiempo.ToString("dd/MM/yyyy");
            if (usuario.IdNivel != 4)
            {
                // Cargar carrera explícitamente si es necesario
                var carreraUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                tituloReporte += " - " + (carreraUsuario?.Nombre ?? "Carrera desconocida");
            }
            else
            {
                tituloReporte += " - Todas las carreras";
            }

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html xmlns:x='urn:schemas-microsoft-com:office:excel'>"); // Para mejor compatibilidad Excel
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<title>Reporte Alumnos Vulnerables</title>");
            html.AppendLine("<style>");
            // (Los mismos estilos que tenías)
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h2 { color: #2c3e50; text-align: center; margin-bottom: 30px; }");
            html.AppendLine("h3 { color: #34495e; margin-top: 30px; margin-bottom: 15px; }");
            html.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 30px; }");
            html.AppendLine("th, td { border: 1px solid #bdc3c7; padding: 8px; text-align: center; vertical-align: top; }"); // Alineación vertical top
            html.AppendLine("th { background-color: #3498db; color: white; font-weight: bold; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f8f9fa; }");
            // Estilo para números (alinear derecha)
            html.AppendLine("td.numero { text-align: right; mso-number-format:0; }"); // mso para forzar formato número en Excel
                                                                                      // Estilo para porcentajes
            html.AppendLine("td.porcentaje { text-align: right; mso-number-format:'0.0%'; }");
            // Estilo para texto largo
            html.AppendLine("td.wrap { white-space: normal; text-align: left; }"); // Permitir ajuste y alinear izquierda
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            html.AppendLine("<h2>" + tituloReporte + "</h2>");

            // TABLA 1: ALUMNOS VULNERABLES POR GRUPO
            html.AppendLine("<h3>1. Alumnos Vulnerables por Grupo</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Grupo</th>");
            html.AppendLine("<th>Tutor</th>");
            if (usuario.IdNivel == 4) // Mostrar carrera solo para Master
            {
                html.AppendLine("<th>Carrera</th>");
            }
            html.AppendLine("<th>Total Vulnerables (Recalculado)</th>");
            html.AppendLine("</tr>");

            // --- INICIO CORRECCIÓN ORDEN ---
            var datosOrdenadosGrupo = datosParaReporte
                .OrderBy(d => ObtenerClaveOrdenamientoGrupo(d.Grupo).Item1) // Ordena por número (Grado)
                .ThenBy(d => ObtenerClaveOrdenamientoGrupo(d.Grupo).Item2)  // Luego por letra (Grupo)
                .ToList();
            // --- FIN CORRECCIÓN ORDEN ---

            foreach (var dato in datosOrdenadosGrupo)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{HttpUtility.HtmlEncode(dato.Grupo)}</td>");
                html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode(dato.Tutor)}</td>"); // Aplicar wrap
                if (usuario.IdNivel == 4)
                {
                    html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode(dato.CarreraNombre)}</td>"); // Aplicar wrap
                }
                html.AppendLine($"<td class='numero' style='font-weight: bold; color: #27ae60;'>{dato.TotalVulnerables}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // TABLA 2: ALUMNOS POR TIPO DE VULNERABILIDAD
            html.AppendLine("<h3>2. Alumnos por Tipo de Vulnerabilidad (Recalculado)</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Tipo de Vulnerabilidad</th>");
            html.AppendLine("<th>Total de Alumnos</th>");
            html.AppendLine("</tr>");

            int totalEconomicos = datosParaReporte.Sum(p => (int)p.VunerableEconomico);
            int totalAcademicos = datosParaReporte.Sum(p => (int)p.VunerableAcademico);
            int totalPersonales = datosParaReporte.Sum(p => (int)p.VunerablePersonal);

            var tiposVulnerabilidad = new[]
            {
            new { Tipo = "Económicos", Total = totalEconomicos },
            new { Tipo = "Académicos", Total = totalAcademicos },
            new { Tipo = "Personales", Total = totalPersonales }
        };
            var tiposOrdenados = tiposVulnerabilidad.OrderByDescending(t => t.Total).ToArray();

            foreach (var tipo in tiposOrdenados)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{tipo.Tipo}</td>");
                html.AppendLine($"<td class='numero' style='font-weight: bold;'>{tipo.Total}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // TABLA RESUMEN GENERAL
            html.AppendLine("<h3>3. Resumen General (Recalculado)</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Concepto</th>");
            html.AppendLine("<th>Cantidad</th>");
            html.AppendLine("</tr>");

            int totalAlumnos = datosParaReporte.Sum(p => (int)p.CantidadAlumnos);
            int totalVulnerables = totalEconomicos + totalAcademicos + totalPersonales;
            double porcentajeVulnerables = totalAlumnos > 0 ? (double)totalVulnerables / totalAlumnos : 0.0;

            html.AppendLine("<tr>");
            html.AppendLine("<td>Total de Alumnos Activos</td>");
            html.AppendLine($"<td class='numero' style='font-weight: bold;'>{totalAlumnos}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Total de Alumnos Vulnerables</td>");
            html.AppendLine($"<td class='numero' style='font-weight: bold; color: #e74c3c;'>{totalVulnerables}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Porcentaje de Vulnerabilidad</td>");
            html.AppendLine($"<td class='porcentaje' style='font-weight: bold; color: #f39c12;'>{porcentajeVulnerables}</td>"); // Formato porcentaje
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Grupos Evaluados</td>");
            html.AppendLine($"<td class='numero' style='font-weight: bold;'>{datosParaReporte.Count}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("</table>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        [HttpGet]
        public ActionResult GenerarExcelGraficos(
            int? anio = null,
            string periodos = null,
            int? carrera = null,
            string estados = null,
            string grados = null,
            string grupos = null,
            string turnos = null,
            string search = null)
        {
            bool conParams = anio.HasValue
                || !string.IsNullOrEmpty(periodos)
                || carrera.HasValue
                || !string.IsNullOrEmpty(estados)
                || !string.IsNullOrEmpty(grados)
                || !string.IsNullOrEmpty(grupos)
                || !string.IsNullOrEmpty(turnos)
                || !string.IsNullOrEmpty(search);
            if (conParams)
            {
                return GenerarExcelGraficosConFiltros(anio, periodos, carrera, estados, grados, grupos, turnos, search);
            }

            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    // Para GET, devolver error HTTP
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Usuario no autenticado");
                }

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;

                // Obtener PATs del período actual - filtrar por carrera si no es usuario master
                var patsQuery = db.PATs.Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == tiempo.Year);

                if (usuario.IdNivel != 4)
                {
                    patsQuery = patsQuery.Where(p => p.IdCarrera == usuario.IdCarrera);
                }

                var pats = patsQuery.Include(p => p.Carrera) // Incluir Carrera
                                    .ToList();

                // Optimización: Cargar datos relacionados de una vez
                var patIds = pats.Select(p => p.IdEntrevistaInicial).ToList();
                var actividadesTodas = db.actividadesSemanals
                                        .Where(a => patIds.Contains(a.IdEntrevistaInicial))
                                        .ToList();

                var tutoriaIds = pats.Select(p => p.IdTutoriaGrupal).Distinct().ToList();
                var tutoriasTodas = db.TutoriaGrupals
                                     .Where(t => tutoriaIds.Contains(t.IdTutoriaGrupal))
                                     .ToDictionary(t => t.IdTutoriaGrupal);

                // --- INICIO CORRECCIÓN TUTOR (¡LA QUE FALTABA!) ---
                var idsTutores = pats.Select(p => p.IdTutor).Distinct().ToList();
                var dicTutores = db.Usuarios
                                    .Where(u => idsTutores.Contains(u.IdUsuario))
                                    .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);
                // --- FIN CORRECCIÓN TUTOR ---


                // Datos estructurados por grupo y tutor
                var datosGrupoTutor = new List<DatosGrupoTutor>();

                foreach (var pat in pats)
                {
                    TutoriaGrupal tutoriaGrupal = tutoriasTodas.ContainsKey(pat.IdTutoriaGrupal) ? tutoriasTodas[pat.IdTutoriaGrupal] : null;

                    if (tutoriaGrupal != null)
                    {
                        var nombreGrupo = GenerarNomenclaturaGrupo(tutoriaGrupal); // <-- USA FUNCIÓN CORREGIDA
                        // Usar pat.Carrera que ya incluimos
                        var carreraNombre = pat.Carrera?.Nombre ?? "Sin carrera";
                        var actividadesDelPat = actividadesTodas.Where(a => a.IdEntrevistaInicial == pat.IdEntrevistaInicial).ToList();

                        // --- INICIO CORRECCIÓN TUTOR ---
                        string nombreTutorCorrecto = "Sin tutor";
                        if (pat.IdTutor > 0 && dicTutores.ContainsKey(pat.IdTutor))
                        {
                            nombreTutorCorrecto = dicTutores[pat.IdTutor];
                        }
                        else if (!string.IsNullOrEmpty(pat.Tutor) && pat.Tutor.Contains(" "))
                        {
                            nombreTutorCorrecto = pat.Tutor; // Fallback
                        }
                        // --- FIN CORRECCIÓN TUTOR ---

                        var datos = new DatosGrupoTutor
                        {
                            Grupo = nombreGrupo,
                            Carrera = carreraNombre,
                            Tutor = nombreTutorCorrecto, // <-- USA LA VARIABLE CORREGIDA
                            ActividadesIndividualRegistradas = new int[16],
                            ActividadesGrupalRegistradas = new int[16],
                            ActividadesIndividualRealizadas = new int[16],
                            ActividadesGrupalRealizadas = new int[16]
                        };

                        // Llenar datos por semana (1-16)
                        for (int semana = 1; semana <= 16; semana++)
                        {
                            var actividadesSemana = actividadesDelPat.Where(a => a.IdSemana == semana).ToList();

                            // Registradas
                            datos.ActividadesIndividualRegistradas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 2);
                            datos.ActividadesGrupalRegistradas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 1);

                            // Realizadas
                            datos.ActividadesIndividualRealizadas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 2 && a.RealizoActividad == true);
                            datos.ActividadesGrupalRealizadas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 1 && a.RealizoActividad == true);
                        }

                        datosGrupoTutor.Add(datos);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Advertencia: No se encontró TutoriaGrupal para PAT ID {pat.IdEntrevistaInicial} (TutoriaGrupal ID: {pat.IdTutoriaGrupal})");
                    }
                }

                // Generar contenido Excel con las dos tablas
                var excelContent = GenerarExcelRendimiento(datosGrupoTutor, usuario, tiempo);

                // Generar nombre de archivo
                string fileName;
                if (usuario.IdNivel == 4)
                {
                    fileName = $"Reporte_Estadistica_PATs_TodasCarreras_{tiempo:yyyyMMdd_HHmmss}.xls";
                }
                else
                {
                    var carreraDelUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera); // Recargar
                    string nombreCarrera = carreraDelUsuario?.Nomenclatura ?? "CarreraDesconocida";
                    fileName = $"Reporte_Estadistica_PATs_{nombreCarrera}_{tiempo:yyyyMMdd_HHmmss}.xls";
                }

                var bytes = Encoding.UTF8.GetBytes(excelContent);

                // Usar application/vnd.ms-excel para forzar descarga como .xls
                return File(bytes, "application/vnd.ms-excel", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelGraficos: {ex.ToString()}"); // Log detallado
                TempData["Error"] = "Error al generar el reporte de rendimiento: " + ex.Message;
                // Considerar redirigir a una página de error o Index de PATsExcel si existe
                return RedirectToAction("PAT", "AsignarAsesores"); // O a donde tenga sentido
            }
        }

        // ====================================================
        // MÉTODOS AUXILIARES
        // ====================================================

        public class DatosGrupoTutor
        {
            public string Grupo { get; set; }
            public string Carrera { get; set; }
            public string Tutor { get; set; }
            public int[] ActividadesIndividualRegistradas { get; set; } = new int[16];
            public int[] ActividadesGrupalRegistradas { get; set; } = new int[16];
            public int[] ActividadesIndividualRealizadas { get; set; } = new int[16];
            public int[] ActividadesGrupalRealizadas { get; set; } = new int[16];
        }

        private string GenerarExcelRendimiento(List<DatosGrupoTutor> datosGrupoTutor, Usuario usuario, DateTime tiempo)
        {
            var html = new StringBuilder();

            string tituloReporte = "Reporte de estadística - " + tiempo.ToString("dd/MM/yyyy");
            if (usuario.IdNivel != 4)
            {
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                tituloReporte += " - " + (carrera?.Nombre ?? "Carrera desconocida");
            }
            else
            {
                tituloReporte += " - Todas las carreras";
            }

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html xmlns:x='urn:schemas-microsoft-com:office:excel'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<style>");
            // (Tus estilos existentes)
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #2E7D32; text-align: center; font-size: 18px; margin-bottom: 30px; }");
            html.AppendLine("h2 { color: #1976D2; font-size: 16px; margin-top: 30px; margin-bottom: 15px; }");
            html.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 30px; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 6px; text-align: center; font-size: 11px; vertical-align: top; }"); // Alineación vertical
            html.AppendLine("th { background-color: #4CAF50; color: white; font-weight: bold; }");
            html.AppendLine("td.info { background-color: #f5f5f5; font-weight: bold; text-align: left; }");
            html.AppendLine("td.tipo { background-color: #e8f5e8; font-weight: bold; text-align: left; }");
            html.AppendLine("td.numero { text-align: right; mso-number-format:0; }"); // Formato número
                                                                                      // Estilos para colores
            html.AppendLine("td.nivel0 { background-color: #ffffff; border: 1px solid #ddd; }");
            html.AppendLine("td.nivel1 { background-color: #c8e6c9; color: #2e7d32; font-weight: bold; }");
            html.AppendLine("td.nivel2 { background-color: #81c784; color: #1b5e20; font-weight: bold; }");
            html.AppendLine("td.nivel3 { background-color: #4caf50; color: white; font-weight: bold; }");
            html.AppendLine("td.nivel4 { background-color: #388e3c; color: white; font-weight: bold; }");
            html.AppendLine("td.nivel5 { background-color: #2e7d32; color: white; font-weight: bold; }");
            html.AppendLine(".leyenda { margin: 15px 0; padding: 10px; background-color: #f8f9fa; border-left: 4px solid #4CAF50; }");
            html.AppendLine(".leyenda-item { display: inline-block; margin: 5px 10px; padding: 5px 10px; border: 1px solid #ddd; text-align: center; font-size: 10px; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            html.AppendLine("<h1>" + tituloReporte + "</h1>");

            // Leyenda de colores
            html.AppendLine("<div class='leyenda'>");
            html.AppendLine("<strong>Leyenda de colores (cantidad de actividades):</strong><br>");
            html.AppendLine("<span class='leyenda-item nivel0'>0</span>");
            html.AppendLine("<span class'leyenda-item nivel1'>1</span>");
            html.AppendLine("<span class='leyenda-item nivel2'>2</span>");
            html.AppendLine("<span class='leyenda-item nivel3'>3</span>");
            html.AppendLine("<span class='leyenda-item nivel4'>4</span>");
            html.AppendLine("<span class='leyenda-item nivel5'>5+</span>");
            html.AppendLine("</div>");

            // GRÁFICA 1: Actividades Registradas
            html.AppendLine("<h2>Tabla 1: Actividades Registradas por Semana</h2>");
            GenerarTablaRendimiento(html, datosGrupoTutor, true); // true = registradas

            // GRÁFICA 2: Actividades Realizadas
            html.AppendLine("<h2>Tabla 2: Actividades Realizadas por Semana</h2>");
            GenerarTablaRendimiento(html, datosGrupoTutor, false); // false = realizadas

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private void GenerarTablaRendimiento(StringBuilder html, List<DatosGrupoTutor> datosGrupoTutor, bool esRegistradas)
        {
            html.AppendLine("<table>");

            html.AppendLine("<tr>");
            html.AppendLine("<th style='width: 80px;'>Grupo</th>");
            html.AppendLine("<th style='width: 120px;'>Carrera</th>");
            html.AppendLine("<th style='width: 120px;'>Tutor</th>");
            html.AppendLine("<th style='width: 80px;'>Tipo Actividad</th>");

            for (int semana = 1; semana <= 16; semana++)
            {
                html.AppendLine($"<th style='width: 35px;'>S{semana}</th>");
            }
            html.AppendLine("</tr>");

            // --- INICIO CORRECCIÓN ORDEN ---
            foreach (var datos in datosGrupoTutor
                .OrderBy(d => ObtenerClaveOrdenamientoGrupo(d.Grupo).Item1) // Ordena por número (Grado)
                .ThenBy(d => ObtenerClaveOrdenamientoGrupo(d.Grupo).Item2)  // Luego por letra (Grupo)
                .ThenBy(d => d.Tutor))
            // --- FIN CORRECCIÓN ORDEN ---
            {
                // Fila individuales
                html.AppendLine("<tr>");
                html.AppendLine($"<td class='info' rowspan='2'>{HttpUtility.HtmlEncode(datos.Grupo)}</td>");
                html.AppendLine($"<td class'info' rowspan='2'>{HttpUtility.HtmlEncode(datos.Carrera)}</td>");
                html.AppendLine($"<td class='info' rowspan='2'>{HttpUtility.HtmlEncode(datos.Tutor)}</td>");
                html.AppendLine("<td class='tipo'>Individual</td>");

                for (int semana = 0; semana < 16; semana++)
                {
                    int cantidad = esRegistradas ? datos.ActividadesIndividualRegistradas[semana] : datos.ActividadesIndividualRealizadas[semana];
                    string nivelColor = ObtenerClaseColor(cantidad);
                    string valorMostrar = cantidad > 0 ? cantidad.ToString() : "";
                    // Añadir clase 'numero' para formato Excel
                    html.AppendLine($"<td class='numero {nivelColor}'>{valorMostrar}</td>");
                }
                html.AppendLine("</tr>");

                // Fila grupales
                html.AppendLine("<tr>");
                html.AppendLine("<td class'tipo'>Grupal</td>");

                for (int semana = 0; semana < 16; semana++)
                {
                    int cantidad = esRegistradas ? datos.ActividadesGrupalRegistradas[semana] : datos.ActividadesGrupalRealizadas[semana];
                    string nivelColor = ObtenerClaseColor(cantidad);
                    string valorMostrar = cantidad > 0 ? cantidad.ToString() : "";
                    // Añadir clase 'numero' para formato Excel
                    html.AppendLine($"<td class='numero {nivelColor}'>{valorMostrar}</td>");
                }
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");
        }

        // Método sin cambios
        private string ObtenerClaseColor(int cantidad)
        {
            if (cantidad == 0) return "nivel0";
            if (cantidad == 1) return "nivel1";
            if (cantidad == 2) return "nivel2";
            if (cantidad == 3) return "nivel3";
            if (cantidad == 4) return "nivel4";
            return "nivel5";
        }


        // ====================================================
        // MÉTODOS AUXILIARES (COPIADOS/ADAPTADOS)
        // ====================================================

        private IQueryable<DatosPersonales> QueryAlumnosDelGrupo(TutoriaGrupal g)
        {
            if (g == null) return Enumerable.Empty<DatosPersonales>().AsQueryable(); // Devolver vacío si g es null
            return db.DatosPersonales.Where(dp =>
                dp.IdCarrera == g.IdCarrera &&
                dp.IdGrado == g.IdGrado &&
                dp.IdGrupo == g.IdGrupo &&
                dp.IdTurno == g.IdTurno &&
                dp.IdPeriodo == g.IdPeriodo &&
                dp.Año == g.Año &&
                dp.Estado == true);
        }

        private List<EntrevistaInicial> UltimaEntrevistaPorAlumno(TutoriaGrupal g)
        {
            if (g == null) return new List<EntrevistaInicial>(); // Devolver vacío si g es null
            var idsAlumnosDelGrupo = QueryAlumnosDelGrupo(g).Select(dp => dp.IdPersona).Distinct().ToList();

            if (!idsAlumnosDelGrupo.Any())
            {
                return new List<EntrevistaInicial>();
            }

            var todasEntrevistas = db.EntrevistaInicials
                .Where(ei => idsAlumnosDelGrupo.Contains(ei.IdPersona))
                .OrderByDescending(e => e.Fecha)
                .ThenByDescending(e => e.IdEntrevistaInicial)
                .ToList();

            var ultimas = todasEntrevistas
                .GroupBy(e => e.IdPersona)
                .Select(grp => grp.First())
                .ToList();

            return ultimas;
        }

        // --- INICIO FUNCIÓN NOMENCLATURA CORREGIDA ---
        // Esta es la nueva lógica sin prefijo de turno.
        private string GenerarNomenclaturaGrupo(TutoriaGrupal tuto)
        {
            if (tuto == null) return "Grupo Desconocido";

            try
            {
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generando nomenclatura para TutoriaGrupal ID {tuto.IdTutoriaGrupal}: {ex.Message}");
                return $"ErrorNomenclatura ({tuto.IdTutoriaGrupal})";
            }
        }
        // --- FIN FUNCIÓN NOMENCLATURA CORREGIDA ---

        // --- INICIO NUEVO HELPER DE ORDENAMIENTO ---
        private (int, string) ObtenerClaveOrdenamientoGrupo(string nombreGrupo)
        {
            if (string.IsNullOrEmpty(nombreGrupo)) return (999, "Z");

            // Usar Regex para extraer el número (Grado) y la letra (Grupo)
            // Asume formato como "TI10A" o "MLCE1A"
            var match = System.Text.RegularExpressions.Regex.Match(nombreGrupo, @"[A-Z]+(\d+)([A-Z]+)");

            if (match.Success && match.Groups.Count == 3)
            {
                int grado = 999;
                int.TryParse(match.Groups[1].Value, out grado);
                string letra = match.Groups[2].Value;
                return (grado, letra);
            }

            // Fallback por si no coincide el patrón
            return (999, nombreGrupo);
        }
        // --- FIN NUEVO HELPER DE ORDENAMIENTO ---


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                // Considera disponer _mongoService si implementa IDisposable
            }
            base.Dispose(disposing);
        }

        // ====================================================
        // INFRAESTRUCTURA PARA REPORTES CON FILTROS
        // (usada únicamente cuando el endpoint recibe parámetros de filtro;
        //  el comportamiento sin parámetros mantiene la lógica original.)
        // ====================================================

        private class FiltrosReporte
        {
            public int? IdCarrera { get; set; }                           // null = todas (master) o la del coordinador
            public List<string> Estados { get; set; }                     // p.ej. "Aprobado", "En revisión"
            public List<int> Grados { get; set; }                         // IdGrado
            public List<string> Grupos { get; set; }                      // nomenclatura completa "TI6C"
            public List<string> Turnos { get; set; }                      // "Matutino", "Vespertino", "Despresurizado"
            public string Search { get; set; }                            // texto libre

            public bool TieneAlgunFiltro =>
                IdCarrera.HasValue
                || (Estados != null && Estados.Count > 0)
                || (Grados != null && Grados.Count > 0)
                || (Grupos != null && Grupos.Count > 0)
                || (Turnos != null && Turnos.Count > 0)
                || !string.IsNullOrEmpty(Search);
        }

        private static List<int> ParseCsvInts(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<int>();
            var result = new List<int>();
            foreach (var p in csv.Split(','))
            {
                if (int.TryParse(p.Trim(), out int v)) result.Add(v);
            }
            return result;
        }

        private static List<string> ParseCsvStrings(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        /// <summary>"Ene-Abr", "May-Ago", "Sep-Dic" — usado para nombres de archivo dentro del ZIP.</summary>
        private static string NombreCortoCuatri(int idPeriodo)
        {
            if (idPeriodo == 1) return "Ene-Abr";
            if (idPeriodo == 2) return "May-Ago";
            return "Sep-Dic";
        }

        /// <summary>"Enero - Abril", etc — para títulos visibles dentro del Excel.</summary>
        private static string NombreLargoCuatri(int idPeriodo)
        {
            if (idPeriodo == 1) return "Enero - Abril";
            if (idPeriodo == 2) return "Mayo - Agosto";
            return "Septiembre - Diciembre";
        }

        /// <summary>Empaqueta múltiples archivos como ZIP en memoria.</summary>
        private static byte[] EmpaquetarZip(List<KeyValuePair<string, byte[]>> archivos)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var a in archivos)
                    {
                        var entry = zip.CreateEntry(a.Key, System.IO.Compression.CompressionLevel.Fastest);
                        using (var entryStream = entry.Open())
                        {
                            entryStream.Write(a.Value, 0, a.Value.Length);
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Construye la query base de PATs con los filtros recibidos.
        /// Aplica seguridad: coordinador (Nivel 3) queda forzado a su carrera.
        /// </summary>
        private IQueryable<PAT> BuildPatsQuery(Usuario usuario, int anio, int idPeriodo, FiltrosReporte f)
        {
            var q = db.PATs.Where(p => p.IdPeriodo == idPeriodo && p.Fecha.Year == anio);

            // Seguridad: coordinador sólo ve su carrera. Master puede filtrar por una carrera.
            if (usuario.IdNivel != 4)
            {
                q = q.Where(p => p.IdCarrera == usuario.IdCarrera);
            }
            else if (f != null && f.IdCarrera.HasValue)
            {
                int idCar = f.IdCarrera.Value;
                q = q.Where(p => p.IdCarrera == idCar);
            }

            if (f != null && f.Grados != null && f.Grados.Count > 0)
            {
                var grados = f.Grados;
                q = q.Where(p => db.TutoriaGrupals.Any(t => t.IdTutoriaGrupal == p.IdTutoriaGrupal && grados.Contains(t.IdGrado)));
            }

            return q;
        }

        /// <summary>
        /// Filtros post-query (en memoria) que dependen de campos textuales (nomenclatura del
        /// grupo, nombre del tutor, estado, turno por nombre, búsqueda libre).
        /// </summary>
        private bool PatPasaFiltrosMemoria(PAT pat, TutoriaGrupal tuto, string nomenclatura, string nombreTutor, string nombreTurno, FiltrosReporte f)
        {
            if (f == null) return true;

            if (f.Grupos != null && f.Grupos.Count > 0)
            {
                if (string.IsNullOrEmpty(nomenclatura)
                    || !f.Grupos.Any(g => string.Equals(g, nomenclatura, StringComparison.OrdinalIgnoreCase))) return false;
            }

            if (f.Turnos != null && f.Turnos.Count > 0)
            {
                if (string.IsNullOrEmpty(nombreTurno)
                    || !f.Turnos.Any(t => string.Equals(t, nombreTurno, StringComparison.OrdinalIgnoreCase))) return false;
            }

            if (f.Estados != null && f.Estados.Count > 0)
            {
                string estadoPat = DeterminarEstadoTextoExcel(pat);
                if (!f.Estados.Any(e => string.Equals(e, estadoPat, StringComparison.OrdinalIgnoreCase))) return false;
            }

            if (!string.IsNullOrEmpty(f.Search))
            {
                string s = f.Search.Trim().ToUpperInvariant();
                string nom = (nomenclatura ?? "").ToUpperInvariant();
                string tut = (nombreTutor ?? "").ToUpperInvariant();
                if (!nom.Contains(s) && !tut.Contains(s)) return false;
            }

            return true;
        }

        // ====================================================
        // RAMA "CON FILTROS" — REPORTE DE ACTIVIDADES
        // ====================================================

        private ActionResult GenerarExcelActividadesConFiltros(int? anio, string periodos, int? carrera, string estados, string grados, string grupos, string turnos, string search)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null) return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Usuario no autenticado");

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int anioFinal = anio ?? tiempo.Year;

                var periodosLista = ParseCsvInts(periodos);
                if (periodosLista.Count == 0) periodosLista = new List<int> { periodoActual };
                periodosLista = periodosLista.Where(p => p >= 1 && p <= 3).Distinct().OrderBy(p => p).ToList();

                var filtros = new FiltrosReporte
                {
                    IdCarrera = carrera,
                    Estados = ParseCsvStrings(estados),
                    Grados = ParseCsvInts(grados),
                    Grupos = ParseCsvStrings(grupos),
                    Turnos = ParseCsvStrings(turnos),
                    Search = search
                };

                var archivos = new List<KeyValuePair<string, byte[]>>();
                foreach (int p in periodosLista)
                {
                    byte[] bytes = ConstruirActividadesXlsBytes(usuario, tiempo, anioFinal, p, filtros);
                    string nombreInterno = $"Actividades_{NombreCortoCuatri(p)}_{anioFinal}.xls";
                    archivos.Add(new KeyValuePair<string, byte[]>(nombreInterno, bytes));
                }

                if (archivos.Count == 1)
                {
                    return File(archivos[0].Value, "application/vnd.ms-excel", archivos[0].Key);
                }

                byte[] zipBytes = EmpaquetarZip(archivos);
                string zipName = $"Reporte_Actividades_{anioFinal}_{tiempo:yyyyMMdd_HHmmss}.zip";
                return File(zipBytes, "application/zip", zipName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelActividadesConFiltros: {ex.ToString()}");
                TempData["Error"] = "Error al generar el reporte de actividades (con filtros): " + ex.Message;
                return RedirectToAction("PAT", "AsignarAsesores");
            }
        }

        /// <summary>
        /// Genera los bytes de UN .xls de Actividades para un (anio, periodo) específico,
        /// aplicando los filtros del usuario. La estructura y estilo replican el reporte
        /// legacy (sin parámetros) pero con título adaptado al cuatrimestre.
        /// </summary>
        private byte[] ConstruirActividadesXlsBytes(Usuario usuario, DateTime tiempo, int anio, int idPeriodo, FiltrosReporte f)
        {
            var patsQuery = BuildPatsQuery(usuario, anio, idPeriodo, f);

            var pats = patsQuery.Include(p => p.Carrera).ToList();

            var patIds = pats.Select(p => p.IdEntrevistaInicial).ToList();
            var actividadesTodas = db.actividadesSemanals
                                    .Include(a => a.Tipo)
                                    .Where(a => patIds.Contains(a.IdEntrevistaInicial))
                                    .ToList();

            var tutoriaIds = pats.Select(p => p.IdTutoriaGrupal).Distinct().ToList();
            var tutoriasTodas = db.TutoriaGrupals
                                 .Include(t => t.Grado)
                                 .Include(t => t.Grupo)
                                 .Where(t => tutoriaIds.Contains(t.IdTutoriaGrupal))
                                 .ToDictionary(t => t.IdTutoriaGrupal);

            var idsTutores = pats.Select(p => p.IdTutor).Distinct().ToList();
            var dicTutores = db.Usuarios
                                .Where(u => idsTutores.Contains(u.IdUsuario))
                                .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);

            var todosLosTurnos = db.Turnoes.ToDictionary(t => t.IdTurno, t => t.Nombre);

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html xmlns:x='urn:schemas-microsoft-com:office:excel'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<style>");
            html.AppendLine("table { border-collapse: collapse; width: 100%; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; vertical-align: top; }");
            html.AppendLine("th { background-color: #4CAF50; color: white; font-weight: bold; }");
            html.AppendLine("td.wrap { white-space: normal; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            string tituloReporte = "Reporte de Actividades PATs — " + NombreLargoCuatri(idPeriodo) + " " + anio;
            if (usuario.IdNivel != 4)
            {
                var carreraUsr = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                tituloReporte += " - " + (carreraUsr?.Nombre ?? "Carrera desconocida");
            }
            else if (f != null && f.IdCarrera.HasValue)
            {
                var carreraFiltro = db.Carreras.FirstOrDefault(c => c.IdCarrera == f.IdCarrera.Value);
                tituloReporte += " - " + (carreraFiltro?.Nombre ?? "Carrera desconocida");
            }
            else
            {
                tituloReporte += " - Todas las carreras";
            }

            html.AppendLine("<h2>" + HttpUtility.HtmlEncode(tituloReporte) + "</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Grado</th>");
            html.AppendLine("<th>Grupo</th>");
            html.AppendLine("<th>Tutor</th>");
            if (usuario.IdNivel == 4) html.AppendLine("<th>Carrera</th>");
            html.AppendLine("<th>Período</th>");
            html.AppendLine("<th>Año</th>");
            html.AppendLine("<th>Tipo de Actividad</th>");
            html.AppendLine("<th>Nombre de Actividad</th>");
            html.AppendLine("<th>Comentarios</th>");
            html.AppendLine("<th>Realizado</th>");
            html.AppendLine("</tr>");

            var ordenados = pats.OrderBy(p => ObtenerClaveOrdenamientoGrupo(GenerarNomenclaturaGrupo(tutoriasTodas.ContainsKey(p.IdTutoriaGrupal) ? tutoriasTodas[p.IdTutoriaGrupal] : null)).Item1)
                                .ThenBy(p => ObtenerClaveOrdenamientoGrupo(GenerarNomenclaturaGrupo(tutoriasTodas.ContainsKey(p.IdTutoriaGrupal) ? tutoriasTodas[p.IdTutoriaGrupal] : null)).Item2)
                                .ToList();

            foreach (var pat in ordenados)
            {
                TutoriaGrupal tutoriaGrupal = tutoriasTodas.ContainsKey(pat.IdTutoriaGrupal) ? tutoriasTodas[pat.IdTutoriaGrupal] : null;
                if (tutoriaGrupal == null) continue;

                string nombreGrupo = GenerarNomenclaturaGrupo(tutoriaGrupal);

                // Resolución con fallback al cache de PAT.Tutor: si el IdTutor ya no está
                // en Usuarios (cuenta eliminada/reasignada), preservamos el nombre cacheado
                // al crear el PAT — refleja el tutor real en aquel cuatrimestre.
                string nombreTutorCorrecto;
                if (pat.IdTutor > 0 && dicTutores.ContainsKey(pat.IdTutor) && !string.IsNullOrWhiteSpace(dicTutores[pat.IdTutor]))
                    nombreTutorCorrecto = dicTutores[pat.IdTutor];
                else if (!string.IsNullOrWhiteSpace(pat.Tutor))
                    nombreTutorCorrecto = pat.Tutor;
                else
                    nombreTutorCorrecto = "Sin tutor";

                string nombreTurnoReal = todosLosTurnos.ContainsKey(tutoriaGrupal.IdTurno) ? todosLosTurnos[tutoriaGrupal.IdTurno] : "Sin turno";

                // Filtros en memoria (grupo, turno, estado, search)
                if (!PatPasaFiltrosMemoria(pat, tutoriaGrupal, nombreGrupo, nombreTutorCorrecto, nombreTurnoReal, f)) continue;

                var actividadesDelPat = actividadesTodas.Where(a => a.IdEntrevistaInicial == pat.IdEntrevistaInicial).ToList();
                if (actividadesDelPat.Count == 0) continue; // mantiene comportamiento legacy (sólo PATs con actividades)

                string gradoNombre = tutoriaGrupal.Grado?.Nombre ?? "Sin grado";

                foreach (var actividad in actividadesDelPat)
                {
                    var tipoTutoriaNombre = actividad.Tipo?.Nombre ?? "Sin tipo";

                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{HttpUtility.HtmlEncode(gradoNombre)}</td>");
                    html.AppendLine($"<td>{HttpUtility.HtmlEncode(nombreGrupo)}</td>");
                    html.AppendLine($"<td>{HttpUtility.HtmlEncode(nombreTutorCorrecto)}</td>");
                    if (usuario.IdNivel == 4)
                    {
                        html.AppendLine($"<td>{HttpUtility.HtmlEncode(pat.Carrera?.Nombre ?? "Sin carrera")}</td>");
                    }
                    html.AppendLine($"<td>{HttpUtility.HtmlEncode(NombreLargoCuatri(idPeriodo))}</td>");
                    html.AppendLine($"<td>{anio}</td>");
                    html.AppendLine($"<td>{HttpUtility.HtmlEncode(tipoTutoriaNombre)}</td>");
                    html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode(actividad.Actividad ?? "Sin actividad")}</td>");
                    html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode(actividad.Comentarios ?? "")}</td>");
                    html.AppendLine($"<td>{(actividad.RealizoActividad == true ? "Sí" : (actividad.RealizoActividad == false ? "No" : "Sin datos"))}</td>");
                    html.AppendLine("</tr>");
                }
            }

            html.AppendLine("</table>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return Encoding.UTF8.GetBytes(html.ToString());
        }

        // ====================================================
        // RAMA "CON FILTROS" — REPORTE DE VULNERABLES
        // ====================================================

        private ActionResult GenerarExcelVulnerablesConFiltros(int? anio, string periodos, int? carrera, string estados, string grados, string grupos, string turnos, string search)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null) return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Usuario no autenticado");

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int anioFinal = anio ?? tiempo.Year;

                var periodosLista = ParseCsvInts(periodos);
                if (periodosLista.Count == 0) periodosLista = new List<int> { periodoActual };
                periodosLista = periodosLista.Where(p => p >= 1 && p <= 3).Distinct().OrderBy(p => p).ToList();

                var filtros = new FiltrosReporte
                {
                    IdCarrera = carrera,
                    Estados = ParseCsvStrings(estados),
                    Grados = ParseCsvInts(grados),
                    Grupos = ParseCsvStrings(grupos),
                    Turnos = ParseCsvStrings(turnos),
                    Search = search
                };

                var archivos = new List<KeyValuePair<string, byte[]>>();
                foreach (int p in periodosLista)
                {
                    byte[] bytes = ConstruirVulnerablesXlsBytes(usuario, tiempo, anioFinal, p, filtros);
                    string nombreInterno = $"Vulnerables_{NombreCortoCuatri(p)}_{anioFinal}.xls";
                    archivos.Add(new KeyValuePair<string, byte[]>(nombreInterno, bytes));
                }

                if (archivos.Count == 1)
                {
                    return File(archivos[0].Value, "application/vnd.ms-excel", archivos[0].Key);
                }

                byte[] zipBytes = EmpaquetarZip(archivos);
                string zipName = $"Reporte_Vulnerables_{anioFinal}_{tiempo:yyyyMMdd_HHmmss}.zip";
                return File(zipBytes, "application/zip", zipName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelVulnerablesConFiltros: {ex.ToString()}");
                TempData["Error"] = "Error al generar el reporte de vulnerables: " + (ex.Message ?? "").Replace("\r", " ").Replace("\n", " ");
                return RedirectToAction("PAT", "AsignarAsesores");
            }
        }

        private byte[] ConstruirVulnerablesXlsBytes(Usuario usuario, DateTime tiempo, int anio, int idPeriodo, FiltrosReporte f)
        {
            // 1. Obtener tutorías del cuatri con seguridad de carrera + filtro de grados
            var tutoriasQuery = db.TutoriaGrupals.Where(tg => tg.IdPeriodo == idPeriodo && tg.Año == anio);
            if (usuario.IdNivel != 4) tutoriasQuery = tutoriasQuery.Where(tg => tg.IdCarrera == usuario.IdCarrera);
            else if (f != null && f.IdCarrera.HasValue)
            {
                int idC = f.IdCarrera.Value;
                tutoriasQuery = tutoriasQuery.Where(tg => tg.IdCarrera == idC);
            }
            if (f != null && f.Grados != null && f.Grados.Count > 0)
            {
                var grados = f.Grados;
                tutoriasQuery = tutoriasQuery.Where(tg => grados.Contains(tg.IdGrado));
            }
            var tutorias = tutoriasQuery.Include(tg => tg.Carrera).ToList();

            // 2. PATs y tutores para resolver nombre del tutor
            var tutoriaIds = tutorias.Select(tg => tg.IdTutoriaGrupal).ToList();
            var patsExistentes = db.PATs.Where(p => tutoriaIds.Contains(p.IdTutoriaGrupal)).ToDictionary(p => p.IdTutoriaGrupal);

            var tutorIdsNecesarios = tutorias.Select(tg => tg.IdUsuario)
                                             .Union(patsExistentes.Values.Select(p => p.IdTutor))
                                             .Distinct().Where(id => id > 0).ToList();
            var tutoresDic = db.Usuarios.Where(u => tutorIdsNecesarios.Contains(u.IdUsuario))
                                        .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);
            var todosLosTurnos = db.Turnoes.ToDictionary(t => t.IdTurno, t => t.Nombre);

            // 3. Procesar cada tutoría calculando vulnerabilidades vía Individuals+Seguimientoes
            var datosParaReporte = new List<dynamic>();
            foreach (var tg in tutorias)
            {
                string nombreGrupo = GenerarNomenclaturaGrupo(tg);

                PAT pat = patsExistentes.ContainsKey(tg.IdTutoriaGrupal) ? patsExistentes[tg.IdTutoriaGrupal] : null;
                // Resolución con fallback al cache: prioridad
                //   1) Usuarios[IdTutor del PAT]
                //   2) Usuarios[IdUsuario del TutoriaGrupal] (si el PAT no tenía IdTutor)
                //   3) Cache PAT.Tutor (snapshot histórico)
                //   4) "Sin tutor asignado"
                int tutorId = pat?.IdTutor ?? tg.IdUsuario;
                string tutorName = null;
                if (tutorId > 0 && tutoresDic.ContainsKey(tutorId) && !string.IsNullOrWhiteSpace(tutoresDic[tutorId]))
                    tutorName = tutoresDic[tutorId];
                else if (tg.IdUsuario > 0 && tutoresDic.ContainsKey(tg.IdUsuario) && !string.IsNullOrWhiteSpace(tutoresDic[tg.IdUsuario]))
                    tutorName = tutoresDic[tg.IdUsuario];
                else if (pat != null && !string.IsNullOrWhiteSpace(pat.Tutor))
                    tutorName = pat.Tutor;
                else
                    tutorName = "Sin tutor asignado";

                string nombreTurnoReal = todosLosTurnos.ContainsKey(tg.IdTurno) ? todosLosTurnos[tg.IdTurno] : "Sin turno";

                if (pat != null && !PatPasaFiltrosMemoria(pat, tg, nombreGrupo, tutorName, nombreTurnoReal, f)) continue;
                // Si no hay PAT, aplicamos sólo los filtros que no dependen de PAT
                if (pat == null && f != null)
                {
                    if (f.Grupos != null && f.Grupos.Count > 0 && !f.Grupos.Contains(nombreGrupo)) continue;
                    if (f.Turnos != null && f.Turnos.Count > 0 && !f.Turnos.Contains(nombreTurnoReal)) continue;
                    if (!string.IsNullOrEmpty(f.Search))
                    {
                        string s = f.Search.Trim().ToUpperInvariant();
                        if (!(nombreGrupo ?? "").ToUpperInvariant().Contains(s) && !(tutorName ?? "").ToUpperInvariant().Contains(s)) continue;
                    }
                }

                var v = CalcularVulnerabilidadesGrupo(tg);
                datosParaReporte.Add(new
                {
                    Grupo = nombreGrupo,
                    Tutor = tutorName,
                    CarreraNombre = tg.Carrera?.Nombre ?? "Sin carrera",
                    VunerableEconomico = v.Eco,
                    VunerableAcademico = v.Aca,
                    VunerablePersonal = v.Per,
                    TotalVulnerables = v.Eco + v.Aca + v.Per,
                    CantidadAlumnos = v.TotalAlumnos
                });
            }

            // 4. Generar HTML con título de cuatrimestre+año
            string htmlContent = GenerarHtmlExcelVulnerabilidadesConCuatri(datosParaReporte, usuario, idPeriodo, anio, f);
            return Encoding.UTF8.GetBytes(htmlContent);
        }

        private string GenerarHtmlExcelVulnerabilidadesConCuatri(List<dynamic> datosParaReporte, Usuario usuario, int idPeriodo, int anio, FiltrosReporte f)
        {
            var html = new StringBuilder();
            string tituloReporte = "Reporte de Alumnos Vulnerables — " + NombreLargoCuatri(idPeriodo) + " " + anio;
            if (usuario.IdNivel != 4)
            {
                var carreraUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                tituloReporte += " - " + (carreraUsuario?.Nombre ?? "Carrera desconocida");
            }
            else if (f != null && f.IdCarrera.HasValue)
            {
                var carreraFiltro = db.Carreras.FirstOrDefault(c => c.IdCarrera == f.IdCarrera.Value);
                tituloReporte += " - " + (carreraFiltro?.Nombre ?? "Carrera desconocida");
            }
            else tituloReporte += " - Todas las carreras";

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html xmlns:x='urn:schemas-microsoft-com:office:excel'>");
            html.AppendLine("<head><meta charset='UTF-8'><title>Reporte Alumnos Vulnerables</title>");
            html.AppendLine("<style>body{font-family:Arial,sans-serif;margin:20px}h2{color:#2c3e50;text-align:center;margin-bottom:30px}h3{color:#34495e;margin-top:30px;margin-bottom:15px}table{border-collapse:collapse;width:100%;margin-bottom:30px}th,td{border:1px solid #bdc3c7;padding:8px;text-align:center;vertical-align:top}th{background-color:#3498db;color:white;font-weight:bold}tr:nth-child(even){background-color:#f8f9fa}td.numero{text-align:right;mso-number-format:0}td.porcentaje{text-align:right;mso-number-format:'0.0%'}td.wrap{white-space:normal;text-align:left}</style>");
            html.AppendLine("</head><body>");
            html.AppendLine("<h2>" + HttpUtility.HtmlEncode(tituloReporte) + "</h2>");

            // Tabla 1
            html.AppendLine("<h3>1. Alumnos Vulnerables por Grupo</h3>");
            html.AppendLine("<table><tr><th>Grupo</th><th>Tutor</th>");
            if (usuario.IdNivel == 4) html.AppendLine("<th>Carrera</th>");
            html.AppendLine("<th>Total Vulnerables</th></tr>");
            var ord = datosParaReporte
                .OrderBy(d => ObtenerClaveOrdenamientoGrupo((string)d.Grupo).Item1)
                .ThenBy(d => ObtenerClaveOrdenamientoGrupo((string)d.Grupo).Item2)
                .ToList();
            foreach (var d in ord)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{HttpUtility.HtmlEncode((string)d.Grupo)}</td>");
                html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode((string)d.Tutor)}</td>");
                if (usuario.IdNivel == 4) html.AppendLine($"<td class='wrap'>{HttpUtility.HtmlEncode((string)d.CarreraNombre)}</td>");
                html.AppendLine($"<td class='numero' style='font-weight:bold;color:#27ae60'>{d.TotalVulnerables}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // Tabla 2: tipos
            int totalEconomicos = datosParaReporte.Sum(p => (int)p.VunerableEconomico);
            int totalAcademicos = datosParaReporte.Sum(p => (int)p.VunerableAcademico);
            int totalPersonales = datosParaReporte.Sum(p => (int)p.VunerablePersonal);

            html.AppendLine("<h3>2. Alumnos por Tipo de Vulnerabilidad</h3>");
            html.AppendLine("<table><tr><th>Tipo</th><th>Total de Alumnos</th></tr>");
            var tipos = new[] {
                new { Tipo = "Económicos", Total = totalEconomicos },
                new { Tipo = "Académicos", Total = totalAcademicos },
                new { Tipo = "Personales", Total = totalPersonales }
            }.OrderByDescending(t => t.Total).ToArray();
            foreach (var t in tipos)
            {
                html.AppendLine($"<tr><td>{t.Tipo}</td><td class='numero' style='font-weight:bold'>{t.Total}</td></tr>");
            }
            html.AppendLine("</table>");

            // Tabla 3: resumen
            int totalAlumnos = datosParaReporte.Sum(p => (int)p.CantidadAlumnos);
            int totalVulnerables = totalEconomicos + totalAcademicos + totalPersonales;
            double porc = totalAlumnos > 0 ? (double)totalVulnerables / totalAlumnos : 0.0;

            html.AppendLine("<h3>3. Resumen General</h3>");
            html.AppendLine("<table><tr><th>Concepto</th><th>Cantidad</th></tr>");
            html.AppendLine($"<tr><td>Total de Alumnos</td><td class='numero' style='font-weight:bold'>{totalAlumnos}</td></tr>");
            html.AppendLine($"<tr><td>Total de Vulnerables</td><td class='numero' style='font-weight:bold'>{totalVulnerables}</td></tr>");
            html.AppendLine($"<tr><td>Porcentaje de Vulnerabilidad</td><td class='porcentaje' style='font-weight:bold'>{porc.ToString("0.0%", System.Globalization.CultureInfo.InvariantCulture)}</td></tr>");
            html.AppendLine("</table>");

            html.AppendLine("</body></html>");
            return html.ToString();
        }

        // ====================================================
        // RAMA "CON FILTROS" — REPORTE DE GRÁFICOS
        // ====================================================

        private ActionResult GenerarExcelGraficosConFiltros(int? anio, string periodos, int? carrera, string estados, string grados, string grupos, string turnos, string search)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null) return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Usuario no autenticado");

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int anioFinal = anio ?? tiempo.Year;

                var periodosLista = ParseCsvInts(periodos);
                if (periodosLista.Count == 0) periodosLista = new List<int> { periodoActual };
                periodosLista = periodosLista.Where(p => p >= 1 && p <= 3).Distinct().OrderBy(p => p).ToList();

                var filtros = new FiltrosReporte
                {
                    IdCarrera = carrera,
                    Estados = ParseCsvStrings(estados),
                    Grados = ParseCsvInts(grados),
                    Grupos = ParseCsvStrings(grupos),
                    Turnos = ParseCsvStrings(turnos),
                    Search = search
                };

                var archivos = new List<KeyValuePair<string, byte[]>>();
                foreach (int p in periodosLista)
                {
                    byte[] bytes = ConstruirGraficosXlsBytes(usuario, tiempo, anioFinal, p, filtros);
                    string nombreInterno = $"Graficos_{NombreCortoCuatri(p)}_{anioFinal}.xls";
                    archivos.Add(new KeyValuePair<string, byte[]>(nombreInterno, bytes));
                }

                if (archivos.Count == 1)
                    return File(archivos[0].Value, "application/vnd.ms-excel", archivos[0].Key);

                byte[] zipBytes = EmpaquetarZip(archivos);
                string zipName = $"Reporte_Graficos_{anioFinal}_{tiempo:yyyyMMdd_HHmmss}.zip";
                return File(zipBytes, "application/zip", zipName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelGraficosConFiltros: {ex.ToString()}");
                TempData["Error"] = "Error: " + ex.Message;
                return RedirectToAction("PAT", "AsignarAsesores");
            }
        }

        private byte[] ConstruirGraficosXlsBytes(Usuario usuario, DateTime tiempo, int anio, int idPeriodo, FiltrosReporte f)
        {
            var patsQuery = BuildPatsQuery(usuario, anio, idPeriodo, f);
            var pats = patsQuery.Include(p => p.Carrera).ToList();

            var patIds = pats.Select(p => p.IdEntrevistaInicial).ToList();
            var actividadesTodas = db.actividadesSemanals
                                    .Where(a => patIds.Contains(a.IdEntrevistaInicial))
                                    .ToList();

            var tutoriaIds = pats.Select(p => p.IdTutoriaGrupal).Distinct().ToList();
            var tutoriasTodas = db.TutoriaGrupals
                                 .Where(t => tutoriaIds.Contains(t.IdTutoriaGrupal))
                                 .ToDictionary(t => t.IdTutoriaGrupal);

            var idsTutores = pats.Select(p => p.IdTutor).Distinct().ToList();
            var dicTutores = db.Usuarios.Where(u => idsTutores.Contains(u.IdUsuario))
                                        .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);
            var todosLosTurnos = db.Turnoes.ToDictionary(t => t.IdTurno, t => t.Nombre);

            var datosGrupoTutor = new List<DatosGrupoTutor>();
            foreach (var pat in pats)
            {
                TutoriaGrupal tutoriaGrupal = tutoriasTodas.ContainsKey(pat.IdTutoriaGrupal) ? tutoriasTodas[pat.IdTutoriaGrupal] : null;
                if (tutoriaGrupal == null) continue;

                var nombreGrupo = GenerarNomenclaturaGrupo(tutoriaGrupal);
                var carreraNombre = pat.Carrera?.Nombre ?? "Sin carrera";

                // Resolución con fallback al cache de PAT.Tutor: si el IdTutor ya no está
                // en Usuarios (cuenta eliminada/reasignada), preservamos el nombre cacheado
                // al crear el PAT — refleja el tutor real en aquel cuatrimestre.
                string nombreTutorCorrecto;
                if (pat.IdTutor > 0 && dicTutores.ContainsKey(pat.IdTutor) && !string.IsNullOrWhiteSpace(dicTutores[pat.IdTutor]))
                    nombreTutorCorrecto = dicTutores[pat.IdTutor];
                else if (!string.IsNullOrWhiteSpace(pat.Tutor))
                    nombreTutorCorrecto = pat.Tutor;
                else
                    nombreTutorCorrecto = "Sin tutor";

                string nombreTurnoReal = todosLosTurnos.ContainsKey(tutoriaGrupal.IdTurno) ? todosLosTurnos[tutoriaGrupal.IdTurno] : "Sin turno";

                if (!PatPasaFiltrosMemoria(pat, tutoriaGrupal, nombreGrupo, nombreTutorCorrecto, nombreTurnoReal, f)) continue;

                var actividadesDelPat = actividadesTodas.Where(a => a.IdEntrevistaInicial == pat.IdEntrevistaInicial).ToList();
                var datos = new DatosGrupoTutor
                {
                    Grupo = nombreGrupo,
                    Carrera = carreraNombre,
                    Tutor = nombreTutorCorrecto,
                    ActividadesIndividualRegistradas = new int[16],
                    ActividadesGrupalRegistradas = new int[16],
                    ActividadesIndividualRealizadas = new int[16],
                    ActividadesGrupalRealizadas = new int[16]
                };
                for (int semana = 1; semana <= 16; semana++)
                {
                    var actividadesSemana = actividadesDelPat.Where(a => a.IdSemana == semana).ToList();
                    datos.ActividadesIndividualRegistradas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 2);
                    datos.ActividadesGrupalRegistradas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 1);
                    datos.ActividadesIndividualRealizadas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 2 && a.RealizoActividad == true);
                    datos.ActividadesGrupalRealizadas[semana - 1] = actividadesSemana.Count(a => a.IdTipoTutoria == 1 && a.RealizoActividad == true);
                }
                datosGrupoTutor.Add(datos);
            }

            // Reusar el generador legacy con el título ajustado al cuatri.
            string excelContent = GenerarExcelRendimiento(datosGrupoTutor, usuario, tiempo);
            // Adjustar título: el helper original arma su propio título; sobreescribimos el primer <h1>.
            string tituloPersonalizado = $"Reporte de estadística — {NombreLargoCuatri(idPeriodo)} {anio}";
            // Reemplazo simple del primer h1 sólo si lo encontramos para no romper el HTML.
            int h1Start = excelContent.IndexOf("<h1>");
            int h1End = excelContent.IndexOf("</h1>");
            if (h1Start >= 0 && h1End > h1Start)
            {
                excelContent = excelContent.Substring(0, h1Start) + "<h1>" + HttpUtility.HtmlEncode(tituloPersonalizado) + excelContent.Substring(h1End);
            }
            return Encoding.UTF8.GetBytes(excelContent);
        }

        // ====================================================
        // RAMA "CON FILTROS" — REPORTE DE NOTAS DE REVISIÓN
        // ====================================================

        private ActionResult GenerarExcelNotasPATConFiltros(int? anio, string periodos, int? carrera, string estados, string grados, string grupos, string turnos, string search)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null) return Json(new { success = false, message = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int anioFinal = anio ?? tiempo.Year;

                var periodosLista = ParseCsvInts(periodos);
                if (periodosLista.Count == 0) periodosLista = new List<int> { periodoActual };
                periodosLista = periodosLista.Where(p => p >= 1 && p <= 3).Distinct().OrderBy(p => p).ToList();

                var filtros = new FiltrosReporte
                {
                    IdCarrera = carrera,
                    Estados = ParseCsvStrings(estados),
                    Grados = ParseCsvInts(grados),
                    Grupos = ParseCsvStrings(grupos),
                    Turnos = ParseCsvStrings(turnos),
                    Search = search
                };

                var archivos = new List<KeyValuePair<string, byte[]>>();
                foreach (int p in periodosLista)
                {
                    byte[] bytes = ConstruirNotasPATXlsBytes(usuario, tiempo, anioFinal, p, filtros);
                    string nombreInterno = $"Notas_{NombreCortoCuatri(p)}_{anioFinal}.xls";
                    archivos.Add(new KeyValuePair<string, byte[]>(nombreInterno, bytes));
                }

                if (archivos.Count == 1)
                    return File(archivos[0].Value, "application/vnd.ms-excel", archivos[0].Key);

                byte[] zipBytes = EmpaquetarZip(archivos);
                string zipName = $"Reporte_Notas_{anioFinal}_{tiempo:yyyyMMdd_HHmmss}.zip";
                return File(zipBytes, "application/zip", zipName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GenerarExcelNotasPATConFiltros: {ex.ToString()}");
                TempData["Error"] = "Error al generar el reporte de notas: " + (ex.Message ?? "").Replace("\r", " ").Replace("\n", " ");
                return RedirectToAction("PAT", "AsignarAsesores");
            }
        }

        private byte[] ConstruirNotasPATXlsBytes(Usuario usuario, DateTime tiempo, int anio, int idPeriodo, FiltrosReporte f)
        {
            var patsQuery = BuildPatsQuery(usuario, anio, idPeriodo, f);
            var pats = patsQuery.ToList();

            // Aplicar filtros de memoria (Grupos/Turnos/Estado/Search)
            var tutoriaIds = pats.Select(p => p.IdTutoriaGrupal).Distinct().ToList();
            var tutoriasDic = db.TutoriaGrupals.Where(t => tutoriaIds.Contains(t.IdTutoriaGrupal)).ToDictionary(t => t.IdTutoriaGrupal);
            var idsTutores = pats.Select(p => p.IdTutor).Distinct().ToList();
            var dicTutores = db.Usuarios.Where(u => idsTutores.Contains(u.IdUsuario))
                                        .ToDictionary(u => u.IdUsuario, u => u.NombreCompleto);
            var todosLosTurnos = db.Turnoes.ToDictionary(t => t.IdTurno, t => t.Nombre);

            var patsFiltrados = new List<PAT>();
            foreach (var pat in pats)
            {
                TutoriaGrupal tg = tutoriasDic.ContainsKey(pat.IdTutoriaGrupal) ? tutoriasDic[pat.IdTutoriaGrupal] : null;
                if (tg == null) continue;
                string nombreGrupo = GenerarNomenclaturaGrupo(tg);
                string nombreTutor;
                if (pat.IdTutor > 0 && dicTutores.ContainsKey(pat.IdTutor) && !string.IsNullOrWhiteSpace(dicTutores[pat.IdTutor]))
                    nombreTutor = dicTutores[pat.IdTutor];
                else
                    nombreTutor = pat.Tutor ?? "";
                string nombreTurnoReal = todosLosTurnos.ContainsKey(tg.IdTurno) ? todosLosTurnos[tg.IdTurno] : "Sin turno";
                if (!PatPasaFiltrosMemoria(pat, tg, nombreGrupo, nombreTutor, nombreTurnoReal, f)) continue;
                patsFiltrados.Add(pat);
            }

            // Obtener notas activas para los PATs filtrados
            var patIdsFiltrados = new HashSet<int>(patsFiltrados.Select(p => p.IdEntrevistaInicial));
            var notasPAT = _mongoService.ObtenerTodasNotasActivas();
            notasPAT = notasPAT.Where(n => patIdsFiltrados.Contains(n.PatId)).ToList();

            var notasAgrupadas = notasPAT
                .GroupBy(n => new { n.PatId, n.Usuario })
                .Select(g => new { g.Key.PatId, Tutor = g.Key.Usuario, Notas = g.ToList() })
                .ToList();

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><meta charset='UTF-8'>");
            html.AppendLine("<style>table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px;text-align:left}th{background-color:#4CAF50;color:white;font-weight:bold}</style>");
            html.AppendLine("</head><body>");

            string tituloReporte = $"Reporte de Comentarios de Revisión PATs — {NombreLargoCuatri(idPeriodo)} {anio}";
            if (usuario.IdNivel != 4)
            {
                var carreraUsuario = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                tituloReporte += " - " + (carreraUsuario?.Nombre ?? "Carrera desconocida");
            }
            else if (f != null && f.IdCarrera.HasValue)
            {
                var carreraFiltrada = db.Carreras.FirstOrDefault(c => c.IdCarrera == f.IdCarrera.Value);
                tituloReporte += " - " + (carreraFiltrada?.Nombre ?? "Carrera desconocida");
            }
            else tituloReporte += " - Todas las carreras";

            html.AppendLine("<h2>" + HttpUtility.HtmlEncode(tituloReporte) + "</h2>");

            html.AppendLine("<table><tr><th>Grupo</th><th>Tutor</th><th>Comentarios</th></tr>");
            foreach (var pat in patsFiltrados.OrderBy(p => ObtenerClaveOrdenamientoGrupo(GenerarNomenclaturaGrupo(tutoriasDic.ContainsKey(p.IdTutoriaGrupal) ? tutoriasDic[p.IdTutoriaGrupal] : null)).Item1))
            {
                TutoriaGrupal tg = tutoriasDic.ContainsKey(pat.IdTutoriaGrupal) ? tutoriasDic[pat.IdTutoriaGrupal] : null;
                string grupoNom = GenerarNomenclaturaGrupo(tg);
                string nombreTutor;
                if (pat.IdTutor > 0 && dicTutores.ContainsKey(pat.IdTutor) && !string.IsNullOrWhiteSpace(dicTutores[pat.IdTutor]))
                    nombreTutor = dicTutores[pat.IdTutor];
                else if (!string.IsNullOrWhiteSpace(pat.Tutor))
                    nombreTutor = pat.Tutor;
                else
                    nombreTutor = "Sin tutor";

                var notasDelPat = notasAgrupadas.FirstOrDefault(n => n.PatId == pat.IdEntrevistaInicial);
                string comentariosTexto = "";
                if (notasDelPat != null && notasDelPat.Notas.Count > 0)
                {
                    comentariosTexto = string.Join("<br/>", notasDelPat.Notas.Select(n => HttpUtility.HtmlEncode(n.Comentario ?? "")));
                }
                else comentariosTexto = "<em>Sin comentarios</em>";

                html.AppendLine("<tr>");
                html.AppendLine($"<td>{HttpUtility.HtmlEncode(grupoNom)}</td>");
                html.AppendLine($"<td>{HttpUtility.HtmlEncode(nombreTutor)}</td>");
                html.AppendLine($"<td>{comentariosTexto}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table></body></html>");
            return Encoding.UTF8.GetBytes(html.ToString());
        }

        // ====================================================
        // CÁLCULO DE VULNERABILIDADES (lógica B: Individuals+Seguimientoes)
        // ====================================================

        private bool EsHistoricoCuatri(int anio, int idPeriodo)
        {
            var t = DateTime.Now;
            int periodoActual = (t.Month >= 1 && t.Month <= 4) ? 1 : (t.Month >= 5 && t.Month <= 8) ? 2 : 3;
            int anioActual = t.Year;
            return anio < anioActual || (anio == anioActual && idPeriodo < periodoActual);
        }

        private string CuatriStringIndividuals(int idPeriodo)
        {
            if (idPeriodo == 1) return "Enero - Abril";
            if (idPeriodo == 2) return "Mayo - Agosto";
            return "Septiembre - Diciembre";
        }

        /// <summary>
        /// Calcula vulnerabilidades (Económico, Académico, Personal, NoVulnerable, TotalAlumnos)
        /// para un grupo+cuatrimestre. Para PATs históricos usa Individuals (snapshot real);
        /// para PATs vigentes usa DatosPersonales sin bajas.
        /// </summary>
        private (int Eco, int Aca, int Per, int NoVuln, int TotalAlumnos) CalcularVulnerabilidadesGrupo(TutoriaGrupal tg)
        {
            int ano = tg.Año;
            int idPeriodo = tg.IdPeriodo;

            DateTime fechaInicio, fechaFin;
            if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
            else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
            else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

            List<int> idsAlumnos;
            if (EsHistoricoCuatri(ano, idPeriodo))
            {
                string grupoNom = GenerarNomenclaturaGrupo(tg);
                string cuatri = CuatriStringIndividuals(idPeriodo);
                idsAlumnos = db.Individuals
                    .Where(i => i.Grupo == grupoNom && i.Cuatrimestre == cuatri && i.Fecha.Year == ano)
                    .Select(i => i.IdPersona).Distinct().ToList();
            }
            else
            {
                var todos = db.DatosPersonales.Where(dp =>
                    dp.IdCarrera == tg.IdCarrera && dp.IdGrado == tg.IdGrado &&
                    dp.IdGrupo == tg.IdGrupo && dp.IdTurno == tg.IdTurno &&
                    dp.IdPeriodo == tg.IdPeriodo && dp.Año == tg.Año
                ).Select(x => x.IdPersona).ToList();
                var bajas = db.Bajas.Where(b => todos.Contains(b.IdPersona) && b.Activo == true).Select(b => b.IdPersona).ToList();
                idsAlumnos = todos.Except(bajas).ToList();
            }

            int total = idsAlumnos.Count;
            if (total == 0) return (0, 0, 0, 0, 0);

            var seguimientos = (from s in db.Seguimientoes
                                join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                where idsAlumnos.Contains(i.IdPersona) &&
                                      s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                               .ToList();

            var primeros = seguimientos
                .GroupBy(x => x.IdPersona)
                .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).First())
                .ToList();

            int eco = primeros.Count(x => string.Equals(x.Vulnerabilidad, "Economico", StringComparison.OrdinalIgnoreCase));
            int aca = primeros.Count(x => string.Equals(x.Vulnerabilidad, "Academico", StringComparison.OrdinalIgnoreCase));
            int per = primeros.Count(x => string.Equals(x.Vulnerabilidad, "Personal", StringComparison.OrdinalIgnoreCase));
            int nv = primeros.Count(x => string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase));

            return (eco, aca, per, nv, total);
        }

        /// <summary>
        /// Determina el texto de estado del PAT con la misma lógica que la vista PAT.cshtml.
        /// </summary>
        private string DeterminarEstadoTextoExcel(PAT pat)
        {
            var tiempo = DateTime.Now;
            int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
            int añoActual = tiempo.Year;

            int idPeriodo = pat.IdPeriodo;
            int fechaYear = pat.Fecha.Year;

            if (idPeriodo != periodoActual || fechaYear != añoActual) return "Inactivo";
            if (pat.estado == true)
            {
                if (pat.EstadoRevision == 1) return "En revisión";
                if (pat.EstadoRevision == 2) return "Aprobado";
                if (pat.EstadoRevision == 3) return "Rechazado";
                return "En progreso";
            }
            return "Cerrado";
        }
    }
}