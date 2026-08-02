using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Plataforma_Web.Data;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb.BecasTransporte.Models;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using OfficeOpenXml;
using System.Web;
using System.Text;
using PlataformaWeb;
using PlataformaWeb.Models;
using ProyectoIntegracion.Functionalities;
using Newtonsoft.Json;

namespace Plataforma_Web.Controllers
{
    [LecturaPermitida]
    public class NuevasEstadisticasController : Controller
    {
        private GestionUsuariosContext usuariosDb = new GestionUsuariosContext();
        private TutoriasContext tutoriasDb = new TutoriasContext();
        private ModeloPlataforma db = new ModeloPlataforma();
        private EstadiasUTTNContext estadiasDb = new EstadiasUTTNContext();

        // Validar acceso al controlador
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // El corte programado lo dispara Windows Task Scheduler SIN sesion y se autentica por
            // token dentro de la accion; omitir aqui el guard de sesion para que el request llegue.
            var _accionSistema = filterContext.ActionDescriptor.ActionName;
            if (string.Equals(_accionSistema, "EjecutarCorteProgramado", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_accionSistema, "EnviarAlertaCierre", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var usuario = Session["Usuario"] as Usuario;    

            // Bloquear si no tiene nivel 3 o 4
            if (usuario == null || (usuario.IdNivel != 3 && usuario.IdNivel != 4))
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado. Solo coordinadores y administradores pueden acceder a esta sección.");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // Clase para mapear el resultado de la consulta SQL de totales
        public class VulnerabilidadTotalesResult
        {
            public int TotalEstudiantes { get; set; }
            public int TotalVulnerablesEconomicos { get; set; }
            public int TotalVulnerablesAcademicos { get; set; }
            public int TotalVulnerablesPersonales { get; set; }
            public int TotalNoVulnerables { get; set; }
            public int TotalSinSeguimiento { get; set; }
        }

        // Clase modelo para estadísticas por carrera
        public class EstadisticaCarrera
        {
            public int IdCarrera { get; set; }
            public string Nombre { get; set; }
            public int Cantidad { get; set; }
            public int Hombres { get; set; }
            public int Mujeres { get; set; }
            public int SinSexo { get; set; }
            public int VulnerablesEconomicos { get; set; }
            public int VulnerablesAcademicos { get; set; }
            public int VulnerablesPersonales { get; set; }
            public int NoVulnerables { get; set; }
            public int SinSeguimiento { get; set; }
        }

        // Clase modelo para estadísticas de vulnerabilidades
        public class EstadisticasVulnerabilidad
        {
            public int TotalEstudiantes { get; set; }
            public int VulnerablesEconomicos { get; set; }
            public int VulnerablesAcademicos { get; set; }
            public int VulnerablesPersonales { get; set; }
            public int TotalVulnerables { get; set; }
            public int NoVulnerables { get; set; }
            public int SinSeguimiento { get; set; } // compat: = SinInformacion
            public int ClasificadosPorSeguimiento { get; set; }
            public int ClasificadosPorIdentificacion { get; set; } // fallback: entrevista inicial, tutor sin seguimiento
            public int SinInformacion { get; set; } // ni seguimiento ni entrevista clasificable
        }

        // Detalle por alumno de la clasificacion de vulnerabilidad (para el modal de tarjetas)
        private class AlumnoVulnDetalle
        {
            public string Matricula;
            public bool Econ, Acad, Pers, NoVul, SinInfo;
            public bool PorIdentificacion;
        }

        // Clase modelo para estadísticas por grupo
        public class EstadisticaGrupo
        {
            public int Año { get; set; }
            public int IdPeriodo { get; set; }
            public int IdCarrera { get; set; }
            public int IdGrado { get; set; }
            public int IdGrupo { get; set; }
            public int IdTurno { get; set; }
            public int TotalEstudiantes { get; set; }
            public int Hombres { get; set; }
            public int Mujeres { get; set; }
            public int Bajas { get; set; }
            public int VulnerablesEconomicos { get; set; }
            public int VulnerablesAcademicos { get; set; }
            public int VulnerablesPersonales { get; set; }
            public int SinSeguimiento { get; set; }
            public int NoVulnerables { get; set; }
            public string NombreCarrera { get; set; }
            public string NombreTurno { get; set; }
            public string NombrePeriodo { get; set; }
            public string Especialidad { get; set; }
            public string GrupoId => $"{IdGrado}{GetLetraGrupo(IdGrupo)}";

            private string GetLetraGrupo(int idGrupo)
            {
                if (idGrupo >= 1 && idGrupo <= 26)
                {
                    return ((char)('A' + idGrupo - 1)).ToString();
                }
                return idGrupo.ToString();
            }
        }

        // Fila cruda para estadísticas de materias reprobadas (SqlQuery)
        public class MateriaReprobadaRow
        {
            public string NombreMateria { get; set; }
            public string Matricula { get; set; }
            public string NombreAlumno { get; set; }
            public string Estado { get; set; }
            public int IntentosExtraordinarios { get; set; }
            public int IdCarrera { get; set; }
            public string CarreraNombre { get; set; }
            public string EspecialidadAlumno { get; set; }
            public string EspecialidadMateria { get; set; }
        }

        // DTO para GetReprobadosDetalle (2026-07-29): detalle plano de registros reprobados/extraordinario
        public class ReprobadoDetalleRow
        {
            public string NombreMateria { get; set; }
            public string Matricula { get; set; }
            public string NombreAlumno { get; set; }
            public string Estado { get; set; }
            public int IntentosExtraordinarios { get; set; }
            public int IdCarrera { get; set; }
            public string CarreraNombre { get; set; }
            public string EspecialidadAlumno { get; set; }
            public string EspecialidadMateria { get; set; }
            public int? IdGrado { get; set; }
            public int? IdGrupo { get; set; }
        }

        // Cache estático para nombres de carreras (se carga una vez)
        private static Dictionary<int, string> _cacheNombresCarreras = null;
        private static object _lockCache = new object();

        // Método optimizado para cargar todos los nombres de carreras de una vez
        private Dictionary<int, string> CargarNombresCarreras(List<int> idsCarreras)
        {
            var resultado = new Dictionary<int, string>();

            try
            {
                // PRIMERO: Buscar en EstadiasUTTN usando IdCarrera como IdArea
                // IMPORTANTE: IdCarrera de GestionUsuarios.Alumnos corresponde directamente a IdArea de EstadiasUTTN.Carreras
                var carrerasEstadias = estadiasDb.Carreras
                    .Where(c => idsCarreras.Contains(c.IdArea))
                    .Select(c => new { c.IdArea, c.Area })
                    .ToList();

                foreach (var carrera in carrerasEstadias)
                {
                    if (!string.IsNullOrWhiteSpace(carrera.Area))
                    {
                        resultado[carrera.IdArea] = carrera.Area.Trim();
                    }
                }

                // SEGUNDO: Buscar en Tutorias.Carreras solo para las que NO se encontraron en EstadiasUTTN
                // Esto es un fallback por si acaso
                var idsNoEncontrados = idsCarreras.Where(id => !resultado.ContainsKey(id)).ToList();
                if (idsNoEncontrados.Any())
                {
                    var carrerasTutorias = tutoriasDb.Carreras
                        .Where(c => idsNoEncontrados.Contains(c.IdCarrera))
                        .Select(c => new { c.IdCarrera, c.Nombre })
                        .ToList();

                    foreach (var carrera in carrerasTutorias)
                    {
                        if (!string.IsNullOrWhiteSpace(carrera.Nombre))
                        {
                            resultado[carrera.IdCarrera] = carrera.Nombre.Trim();
                        }
                    }
                }

                // Para las que no se encontraron, usar nombre genérico
                foreach (var idCarrera in idsCarreras)
                {
                    if (!resultado.ContainsKey(idCarrera))
                    {
                        resultado[idCarrera] = $"Carrera {idCarrera}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar nombres de carreras: {ex.Message}");
                // En caso de error, retornar nombres genéricos
                foreach (var idCarrera in idsCarreras)
                {
                    if (!resultado.ContainsKey(idCarrera))
                    {
                        resultado[idCarrera] = $"Carrera {idCarrera}";
                    }
                }
            }

            return resultado;
        }

        private string GetNombreCarrera(int idCarrera, Dictionary<int, string> cacheCarreras = null)
        {
            // Si se proporciona un cache, usarlo
            if (cacheCarreras != null && cacheCarreras.ContainsKey(idCarrera))
            {
                return cacheCarreras[idCarrera];
            }

            try
            {
                // PRIMERO: Buscar en EstadiasUTTN usando IdCarrera como IdArea
                // IMPORTANTE: IdCarrera de GestionUsuarios.Alumnos corresponde directamente a IdArea de EstadiasUTTN.Carreras
                try
                {
                    var carreraEstadias = estadiasDb.Carreras.FirstOrDefault(c => c.IdArea == idCarrera);
                    if (carreraEstadias != null && !string.IsNullOrWhiteSpace(carreraEstadias.Area))
                    {
                        return carreraEstadias.Area.Trim();
                    }
                }
                catch (Exception exEstadias)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al buscar en EstadiasUTTN para carrera {idCarrera}: {exEstadias.Message}");
                }

                // SEGUNDO: Si no se encuentra en EstadiasUTTN, buscar en Tutorias.Carreras como fallback
                // Esto es un fallback por si acaso
                try
                {
                    var carreraTutorias = tutoriasDb.Carreras.FirstOrDefault(c => c.IdCarrera == idCarrera);
                    if (carreraTutorias != null && !string.IsNullOrWhiteSpace(carreraTutorias.Nombre))
                    {
                        return carreraTutorias.Nombre.Trim();
                    }
                }
                catch (Exception exTutorias)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al buscar en Tutorias para carrera {idCarrera}: {exTutorias.Message}");
                }

                // Si no se encuentra en ninguna base de datos, retornar un nombre genérico
                System.Diagnostics.Debug.WriteLine($"No se encontró nombre para carrera {idCarrera} en ninguna base de datos");
                return $"Carrera {idCarrera}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error general al obtener nombre de carrera {idCarrera}: {ex.Message}");
                return $"Carrera {idCarrera}";
            }
        }

        // Método simple para obtener nombre de carrera (igual que versión vieja)
        // Si no está en el diccionario, busca en las bases de datos
        private string GetNombreCarreraSimple(int idCarrera)
        {
            var catalogo = new Dictionary<int, string>
            {
                {1, "Tecnologías de la Información"}, {2, "Mantenimiento Industrial"}, {3, "Mecatrónica"},
                {4, "Administración"}, {5, "Industrial"}, {6, "Energías Renovables"}, {7, "Logística"},
                {8, "Logística Internacional"}, {9, "Aeronaútica en Manufactura"},
                {10, "Microelectrónica y Semiconductores"}, {11, "Ciencia de datos e Inteligencia Artificial"}
            };

            // Si está en el diccionario, devolverlo
            if (catalogo.ContainsKey(idCarrera))
            {
                return catalogo[idCarrera];
            }

            // Si no está, buscar en las bases de datos usando GetNombreCarrera
            try
            {
                string nombre = GetNombreCarrera(idCarrera);
                // Si GetNombreCarrera encontró un nombre real (no es el genérico "Carrera {id}"), devolverlo
                string nombreGenerico = $"Carrera {idCarrera}";
                if (nombre != nombreGenerico)
                {
                    return nombre;
                }
            }
            catch
            {
                // Si hay error, continuar con el nombre genérico
            }

            return $"Carrera {idCarrera}";
        }

        private string GetNombreTurno(int idTurno)
        {
            switch (idTurno)
            {
                case 1: return "Matutino";
                case 2: return "Vespertino";
                case 3: return "Despresurizado";
                default: return $"Turno {idTurno}";
            }
        }

        private string GetNombrePeriodo(int idPeriodo)
        {
            switch (idPeriodo)
            {
                case 1: return "Enero - Abril";
                case 2: return "Mayo - Agosto";
                case 3: return "Septiembre - Diciembre";
                default: return $"Periodo {idPeriodo}";
            }
        }

        // Diccionario de meses para filtros
        private static readonly Dictionary<int, string> NombresMeses = new Dictionary<int, string>
        {
            { 1, "Enero" },
            { 2, "Febrero" },
            { 3, "Marzo" },
            { 4, "Abril" },
            { 5, "Mayo" },
            { 6, "Junio" },
            { 7, "Julio" },
            { 8, "Agosto" },
            { 9, "Septiembre" },
            { 10, "Octubre" },
            { 11, "Noviembre" },
            { 12, "Diciembre" }
        };

        // Método auxiliar para obtener nombre del mes
        private string GetNombreMes(int mes)
        {
            return NombresMeses.ContainsKey(mes) ? NombresMeses[mes] : $"Mes {mes}";
        }

        // Método auxiliar para calcular período basado en mes
        private int CalcularPeriodoPorMes(int mes)
        {
            if (mes >= 1 && mes <= 4) return 1;
            if (mes >= 5 && mes <= 8) return 2;
            return 3;
        }

        private string GetLetraGrupo(int idGrupo)
        {
            if (idGrupo >= 1 && idGrupo <= 26)
            {
                return ((char)('A' + idGrupo - 1)).ToString();
            }
            return idGrupo.ToString();
        }

        // Método auxiliar para normalizar strings eliminando acentos
        private string NormalizarSinAcentos(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return texto;

            var normalizedString = texto.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        // Método auxiliar para normalizar matrículas
        // Elimina espacios normales, NBSP (ASCII 160), tabs y otros caracteres invisibles
        // IMPORTANTE: Esto soluciona problemas con matrículas que tienen caracteres especiales
        private string NormalizarMatricula(string matricula)
        {
            if (string.IsNullOrEmpty(matricula))
                return "";

            // Eliminar Non-Breaking Space (ASCII 160 / Unicode \u00A0)
            var resultado = matricula.Replace("\u00A0", "");
            // Eliminar tabs
            resultado = resultado.Replace("\t", "");
            // Eliminar espacios normales al inicio y final
            resultado = resultado.Trim();

            return resultado;
        }

        /// <summary>
        /// Matriculas (normalizadas) con baja ACTIVA cuya Fecha cae dentro del periodo calendario indicado.
        /// Si no se pasa periodo, usa el periodo vigente (DateTime.Now). Centraliza el filtro de bajas.
        /// </summary>
        private HashSet<string> ObtenerMatriculasBajaDelPeriodo(PeriodoInfo periodo = null)
        {
            var p = periodo ?? PeriodoHelper.Obtener(DateTime.Now);
            var desde = CorteAplicable(p.Inicio, p.Fin) ?? p.Inicio;
            return new HashSet<string>(
                db.Bajas
                    .Where(b => b.Activo == true && b.Matricula != null
                                && b.Fecha >= desde && b.Fecha <= p.Fin)
                    .Select(b => b.Matricula)
                    .ToList()
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m)),
                StringComparer.OrdinalIgnoreCase
            );
        }

        private bool _corteVigenteCargado;
        private DateTime? _fechaCorteVigente;

        /// <summary>
        /// Fecha del ultimo corte historico guardado dentro del cuatrimestre calendario vigente,
        /// o null si no hay. Tras un corte, las estadisticas en vivo "empiezan desde cero":
        /// cuentan la actividad a partir de esa fecha; lo anterior se consulta en el historico.
        /// </summary>
        private DateTime? ObtenerFechaUltimoCorteVigente()
        {
            if (!_corteVigenteCargado)
            {
                var p = PeriodoHelper.Obtener(DateTime.Now);
                _fechaCorteVigente = db.EstadisticasHistoricoCortes
                    .Where(c => c.FechaCorte >= p.Inicio && c.FechaCorte <= p.Fin)
                    .OrderByDescending(c => c.FechaCorte)
                    .Select(c => (DateTime?)c.FechaCorte)
                    .FirstOrDefault();
                _corteVigenteCargado = true;
            }
            return _fechaCorteVigente;
        }

        /// <summary>
        /// Devuelve la fecha del ultimo corte vigente solo si cae dentro del rango consultado
        /// [inicio, fin]; null en caso contrario (rangos pasados no se recortan).
        /// </summary>
        private DateTime? CorteAplicable(DateTime inicio, DateTime fin)
        {
            var corte = ObtenerFechaUltimoCorteVigente();
            return (corte.HasValue && corte.Value > inicio && corte.Value <= fin) ? corte : null;
        }

        // Si hay corte vigente en el cuatrimestre actual, devuelve el conjunto de matriculas (normalizadas)
        // con DatosPersonales o EntrevistaInicial posterior al corte. Devuelve null si NO hay corte (sin restriccion).
        // Sirve para reiniciar a cero las secciones basadas en padron tras un corte: la poblacion se restringe
        // a alumnos con registro/actividad posterior al corte.
        private HashSet<string> ObtenerMatriculasActivasPostCorte()
        {
            var p = PeriodoHelper.Obtener(DateTime.Now);
            var corte = CorteAplicable(p.Inicio, p.Fin);
            if (!corte.HasValue) return null;
            return new HashSet<string>(
                tutoriasDb.DatosPersonales
                    .Where(dp => dp.Fecha >= corte.Value && dp.Matricula != null)
                    .Select(dp => dp.Matricula)
                    .ToList()
                .Concat(
                    tutoriasDb.EntrevistaInicials
                        .Where(e => e.Fecha >= corte.Value && e.Matricula != null)
                        .Select(e => e.Matricula)
                        .ToList())
                .Select(NormalizarMatricula)
                .Where(m => !string.IsNullOrEmpty(m)),
                StringComparer.OrdinalIgnoreCase);
        }

        // Si el corte/seccion existe, devuelve su JSON crudo como ContentResult; si no, null.
        private ActionResult ServirSeccionHistorico(int corteId, string seccion)
        {
            var sec = db.EstadisticasHistoricoSecciones
                .FirstOrDefault(s => s.IdCorte == corteId && s.Seccion == seccion);
            if (sec == null || string.IsNullOrEmpty(sec.DatosJson))
                return null;
            return Content(sec.DatosJson, "application/json");
        }

        // Puebla los ViewBag de las secciones server-side (demografia, nivel de estudio)
        // desde el snapshot guardado. El JSON viene envuelto en {"success":true,"data":{...}}.
        private void PoblarViewBagDesdeCorte(int corteId, PlataformaWeb.Models.Historico.EstadisticasHistoricoCorte corte)
        {
            var secciones = db.EstadisticasHistoricoSecciones
                .Where(s => s.IdCorte == corteId).ToList()
                .ToDictionary(s => s.Seccion, s => s.DatosJson);

            Func<Newtonsoft.Json.Linq.JToken, string, int> I = (o, k) =>
                (o != null && o[k] != null && o[k].Type != Newtonsoft.Json.Linq.JTokenType.Null) ? (int)o[k] : 0;

            // ResumenGlobal: data = demografia + situacion familiar + vulnerabilidad (global)
            if (secciones.ContainsKey("ResumenGlobal"))
            {
                var data = Newtonsoft.Json.Linq.JObject.Parse(secciones["ResumenGlobal"])["data"] as Newtonsoft.Json.Linq.JObject;
                if (data != null)
                {
                    ViewBag.TotalEstudiantes = I(data, "totalEstudiantes");
                    ViewBag.TotalHombres = I(data, "totalHombres");
                    ViewBag.TotalMujeres = I(data, "totalMujeres");
                    ViewBag.TotalSinSexo = I(data, "totalSinSexo");
                    ViewBag.Embarazadas = I(data, "embarazadas");
                    ViewBag.Madres = I(data, "madres");
                    ViewBag.Padres = I(data, "padres");
                    ViewBag.PadresFamilia = I(data, "padresFamilia");
                    ViewBag.AlumnosTrabajando = I(data, "alumnosTrabajando");
                    ViewBag.Vulnerabilidades = new
                    {
                        TotalEstudiantes = I(data, "totalEstudiantes"),
                        VulnerablesEconomicos = I(data, "vulnerablesEconomicos"),
                        VulnerablesAcademicos = I(data, "vulnerablesAcademicos"),
                        VulnerablesPersonales = I(data, "vulnerablesPersonales"),
                        TotalVulnerables = I(data, "totalVulnerables"),
                        NoVulnerables = I(data, "noVulnerables"),
                        SinSeguimiento = I(data, "sinSeguimiento"),
                        SinInformacion = I(data, "sinInformacion"),
                        ClasificadosPorSeguimiento = I(data, "clasificadosPorSeguimiento"),
                        ClasificadosPorIdentificacion = I(data, "clasificadosPorIdentificacion"),
                        TieneDesgloseFuente = data["clasificadosPorSeguimiento"] != null
                    };
                    ViewBag.TieneDesgloseFuente = data["clasificadosPorSeguimiento"] != null;
                }
            }

            // NivelEstudio: data = { total, tsu:{total,hombres,mujeres,econ,acad,pers,noVul,sinSeg}, ingenieria, licenciatura }
            if (secciones.ContainsKey("NivelEstudio"))
            {
                var data = Newtonsoft.Json.Linq.JObject.Parse(secciones["NivelEstudio"])["data"] as Newtonsoft.Json.Linq.JObject;
                if (data != null)
                {
                    var tsu = data["tsu"] as Newtonsoft.Json.Linq.JObject;
                    var ing = data["ingenieria"] as Newtonsoft.Json.Linq.JObject;
                    var lic = data["licenciatura"] as Newtonsoft.Json.Linq.JObject;
                    if (tsu != null) { ViewBag.TotalTSU = I(tsu, "total"); ViewBag.HombresTSU = I(tsu, "hombres"); ViewBag.MujeresTSU = I(tsu, "mujeres"); ViewBag.EconTSU = I(tsu, "econ"); ViewBag.AcadTSU = I(tsu, "acad"); ViewBag.PersTSU = I(tsu, "pers"); ViewBag.NoVulTSU = I(tsu, "noVul"); ViewBag.SinSegTSU = I(tsu, "sinSeg"); }
                    if (ing != null) { ViewBag.TotalIngenieria = I(ing, "total"); ViewBag.HombresIngenieria = I(ing, "hombres"); ViewBag.MujeresIngenieria = I(ing, "mujeres"); ViewBag.EconIng = I(ing, "econ"); ViewBag.AcadIng = I(ing, "acad"); ViewBag.PersIng = I(ing, "pers"); ViewBag.NoVulIng = I(ing, "noVul"); ViewBag.SinSegIng = I(ing, "sinSeg"); }
                    if (lic != null) { ViewBag.TotalLicenciatura = I(lic, "total"); ViewBag.HombresLicenciatura = I(lic, "hombres"); ViewBag.MujeresLicenciatura = I(lic, "mujeres"); ViewBag.EconLic = I(lic, "econ"); ViewBag.AcadLic = I(lic, "acad"); ViewBag.PersLic = I(lic, "pers"); ViewBag.NoVulLic = I(lic, "noVul"); ViewBag.SinSegLic = I(lic, "sinSeg"); }
                }
            }
        }

        // Método auxiliar para normalizar cuatrimestre (eliminar espacios, normalizar guiones, mayúsculas)
        // Esto maneja diferencias sutiles como:
        // - Espacios: "SEPTIEMBRE - DICIEMBRE" vs "SEPTIEMBRE-DICIEMBRE"
        // - Guiones diferentes: hyphen (-) vs en-dash (–) vs em-dash (—)
        private string NormalizarCuatrimestre(string cuatrimestre)
        {
            if (string.IsNullOrEmpty(cuatrimestre))
                return "";

            // Convertir a mayúsculas y eliminar espacios (incluyendo NBSP)
            var resultado = cuatrimestre.Replace("\u00A0", " ").ToUpper().Replace(" ", "");

            // Normalizar diferentes tipos de guiones a hyphen regular
            resultado = resultado.Replace("–", "-"); // en-dash
            resultado = resultado.Replace("—", "-"); // em-dash
            resultado = resultado.Replace("−", "-"); // minus sign
            resultado = resultado.Replace("‐", "-"); // hyphen character

            return resultado;
        }

        // Método para calcular alumnos trabajando filtrado por período
        // Filtra por alumnos que existen en GestionUsuarios.Alumnos Y tienen IdTrabajo = 1 en el período
        // Si no se especifica mes ni período, usa el período actual basado en la fecha del sistema
        private int CalcularAlumnosTrabajandoPorPeriodo(HashSet<string> alumnosMatriculasHashSet, int? mes = null, int? periodo = null)
        {
            try
            {
                // Calcular año y período a usar
                int añoActual = DateTime.Now.Year;
                int periodoActual;

                if (periodo.HasValue)
                {
                    periodoActual = periodo.Value;
                }
                else if (mes.HasValue)
                {
                    // Calcular período basado en el mes
                    periodoActual = (mes.Value >= 1 && mes.Value <= 4) ? 1 : (mes.Value <= 8 ? 2 : 3);
                }
                else
                {
                    // Usar período actual basado en la fecha del sistema
                    periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
                }

                // Calcular rango de fechas del período
                DateTime fechaInicioPeriodo, fechaFinPeriodo;
                switch (periodoActual)
                {
                    case 1: // Enero - Abril
                        fechaInicioPeriodo = new DateTime(añoActual, 1, 1);
                        fechaFinPeriodo = new DateTime(añoActual, 4, 30, 23, 59, 59, 999);
                        break;
                    case 2: // Mayo - Agosto
                        fechaInicioPeriodo = new DateTime(añoActual, 5, 1);
                        fechaFinPeriodo = new DateTime(añoActual, 8, 31, 23, 59, 59, 999);
                        break;
                    case 3: // Septiembre - Diciembre
                    default:
                        fechaInicioPeriodo = new DateTime(añoActual, 9, 1);
                        fechaFinPeriodo = new DateTime(añoActual, 12, 31, 23, 59, 59, 999);
                        break;
                }

                // Tras un corte dentro del periodo consultado, contar solo a partir del corte
                fechaInicioPeriodo = CorteAplicable(fechaInicioPeriodo, fechaFinPeriodo) ?? fechaInicioPeriodo;

                // Consultar EntrevistaInicials filtrado por período Y por alumnos registrados
                // IMPORTANTE: Solo cuenta alumnos que existen en GestionUsuarios.Alumnos
                int alumnosTrabajando = tutoriasDb.EntrevistaInicials
                    .Where(x => alumnosMatriculasHashSet.Contains(x.Matricula) &&
                               x.IdTrabajo == 1 &&
                               x.Fecha >= fechaInicioPeriodo &&
                               x.Fecha <= fechaFinPeriodo)
                    .Select(x => x.Matricula)
                    .Distinct()
                    .Count();

                return alumnosTrabajando;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en CalcularAlumnosTrabajandoPorPeriodo: {ex.Message}");
                return 0;
            }
        }

        public class EstadisticaNivel
        {
            public string Nombre { get; set; }
            public int Total { get; set; }
            public int Hombres { get; set; }
            public int Mujeres { get; set; }
            public int SinSexo { get; set; }
            public int VulnerablesEconomicos { get; set; }
            public int VulnerablesAcademicos { get; set; }
            public int VulnerablesPersonales { get; set; }
            public int SinSeguimiento { get; set; }
        }

        // Clase modelo para estadísticas de bajas
        public class EstadisticaBaja
        {
            public int TotalBajas { get; set; }
            public int PorCarrera { get; set; }
            public string NombreCarrera { get; set; }
            public int PorCausa { get; set; }
            public string Causa { get; set; }
            public int PorVulnerabilidad { get; set; }
            public string Vulnerabilidad { get; set; }
            public Dictionary<string, int> BajasPorCausa { get; set; }
            public Dictionary<string, int> BajasPorCarrera { get; set; }
            public Dictionary<string, int> BajasPorVulnerabilidad { get; set; }
            public Dictionary<string, int> BajasPorEspecialidad { get; set; }
        }

        // Clase modelo para estadísticas de PATs
        public class EstadisticaPAT
        {
            public int TotalPATs { get; set; }
            public int Aprobados { get; set; }
            public int EnProgreso { get; set; }
            public int PendientesRevision { get; set; }
            public int PorCarrera { get; set; }
            public string NombreCarrera { get; set; }
            public Dictionary<string, int> PATsPorEstado { get; set; }
            public Dictionary<string, int> PATsPorCarrera { get; set; }
        }

        // Clase modelo para estadísticas de arrastre
        public class EstadisticaArrastre
        {
            public int TotalMateriasArrastre { get; set; }
            public int TotalAlumnosConArrastre { get; set; }
            public int FueraDeTiempo { get; set; }
            public int Criticos { get; set; }
            public int Medios { get; set; }
            public int EnTiempo { get; set; }
            public int PorCarrera { get; set; }
            public string NombreCarrera { get; set; }
            public Dictionary<string, int> ArrastrePorCarrera { get; set; }
            public Dictionary<string, int> ArrastrePorEstado { get; set; }
        }

        // Clase auxiliar para información de estudiantes sin sexo
        private class EstudianteSinSexoInfo
        {
            public string Matricula { get; set; }
            public string Nombre { get; set; }
            public int IdCarrera { get; set; }
            public int IdGrado { get; set; }
            public int IdGrupo { get; set; }
        }

        // Método para mostrar la vista de estadísticas
        public ActionResult SeguimientoEstadisticas(int? mes = null, int? año = null, int? periodo = null, bool incluirBajas = false, int? corteId = null)
        {
            try
            {
                // Aumentar timeout para consultas complejas
                usuariosDb.Database.CommandTimeout = 300; // 5 minutos
                tutoriasDb.Database.CommandTimeout = 300; // 5 minutos
                db.Database.CommandTimeout = 300; // 5 minutos
                estadiasDb.Database.CommandTimeout = 300; // 5 minutos

                // Obtener usuario de la sesión
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    ViewBag.Error = "Sesión expirada. Por favor, inicie sesión nuevamente.";
                    return View();
                }

                // Obtener IdArea del coordinador si es necesario
                int? idAreaCoordinador = null;
                string nombreCarreraCoordinador = null;
                if (usuario.IdNivel == 3)
                {
                    System.Diagnostics.Debug.WriteLine("=== DEBUG COORDINADOR ===");
                    System.Diagnostics.Debug.WriteLine($"Usuario.IdNivel: {usuario.IdNivel}");
                    System.Diagnostics.Debug.WriteLine($"Usuario.IdCarrera (desde Tutorias): {usuario.IdCarrera}");

                    // Usar el método de mapeo que implementa la consulta SQL proporcionada
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                    System.Diagnostics.Debug.WriteLine($"IdArea mapeado: {idAreaCoordinador?.ToString() ?? "NULL"}");

                    if (idAreaCoordinador.HasValue)
                    {
                        // Obtener el nombre de la carrera desde EstadiasUTTN para usar en el filtro
                        var carreraEstadias = estadiasDb.Carreras.FirstOrDefault(c => c.IdArea == idAreaCoordinador.Value);
                        if (carreraEstadias != null && !string.IsNullOrWhiteSpace(carreraEstadias.Area))
                        {
                            nombreCarreraCoordinador = carreraEstadias.Area.Trim();
                            System.Diagnostics.Debug.WriteLine($"Nombre carrera desde EstadiasUTTN: '{nombreCarreraCoordinador}'");
                        }
                        else
                        {
                            // Fallback: obtener nombre desde Tutorias
                            var carreraTutorias = tutoriasDb.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                            nombreCarreraCoordinador = carreraTutorias?.Nombre?.Trim() ?? "Carrera del Coordinador";
                            System.Diagnostics.Debug.WriteLine($"No se encontró carrera en EstadiasUTTN, usando nombre de Tutorias: '{nombreCarreraCoordinador}'");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: No se pudo mapear IdCarrera a IdArea. No se mostrarán alumnos.");
                    }
                    System.Diagnostics.Debug.WriteLine("=== FIN DEBUG COORDINADOR ===");
                }

                // IMPORTANTE: Obtener alumnos directamente desde GestionUsuarios.Alumnos (sin filtrar por habilitado)
                // Esta es la lógica correcta según el controlador de referencia
                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();

                // Si es coordinador, filtrar por IdArea
                if (idAreaCoordinador.HasValue)
                {
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                }

                // Obtener todas las matrículas directamente desde Alumnos
                // IMPORTANTE: Replica exactamente la consulta SQL: WHERE Matricula IS NOT NULL AND Matricula <> ''
                // La consulta SQL NO usa DISTINCT en el COUNT(*), cuenta todas las filas que cumplen la condición
                var alumnosData = alumnosQuery
                    .Select(a => new { a.Matricula, a.IdCarrera })
                    .ToList();

                // Filtrar en memoria exactamente igual que la SQL: WHERE Matricula IS NOT NULL AND Matricula <> ''
                // IMPORTANTE: Usar NormalizarMatricula() para eliminar espacios, NBSP y otros caracteres invisibles
                // IMPORTANTE: Usar Distinct() para eliminar duplicados reales (mismo alumno registrado más de una vez)
                var alumnosMatriculas = alumnosData
                    .Where(a => !string.IsNullOrWhiteSpace(a.Matricula))
                    .Select(a => NormalizarMatricula(a.Matricula))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                // Reinicio por corte: si hay corte vigente, la poblacion del render server-side (demografia,
                // situacion familiar, vulnerabilidad y derivados) parte de cero y solo cuenta alumnos con
                // DatosPersonales/EntrevistaInicial posterior al corte. Mantiene el page-load coherente con el
                // endpoint AJAX (CalcularResumenDetalladoDatos). Sin corte, el helper devuelve null y no recorta.
                var matriculasPostCorte = ObtenerMatriculasActivasPostCorte();
                if (matriculasPostCorte != null)
                    alumnosMatriculas = alumnosMatriculas
                        .Where(m => m != null && matriculasPostCorte.Contains(NormalizarMatricula(m)))
                        .ToList();

                ViewBag.IncluirBajas = incluirBajas;

                // Estado del periodo (banner): periodo activo + dias restantes al cierre del cuatrimestre.
                var pBanner = PeriodoHelper.Obtener(DateTime.Now);
                ViewBag.PeriodoActivoNombre = pBanner.Nombre;
                ViewBag.PeriodoFin = pBanner.Fin;
                ViewBag.DiasRestantesCierre = (int)(pBanner.Fin.Date - DateTime.Now.Date).TotalDays;

                // Optimización: Usar HashSet para Contains() más rápido (case-insensitive)
                var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);

                // Obtener información de carrera directamente desde Alumnos (IdCarrera ya es IdArea de EstadiasUTTN)
                // IMPORTANTE: Normalizar matrículas para que coincidan con alumnosMatriculas
                var alumnosInfo = alumnosData
                    .Where(a => !string.IsNullOrWhiteSpace(a.Matricula) && NormalizarMatricula(a.Matricula) != "")
                    .GroupBy(a => NormalizarMatricula(a.Matricula), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().IdCarrera, StringComparer.OrdinalIgnoreCase);

                // Obtener especialidades para el filtro.
                // Nivel 3 (Director/Coordinador): solo las de su carrera. Máster (4): todas.
                var especialidades = tutoriasDb.Especialidads
                    .OrderBy(e => e.Nombre)
                    .ToList();
                if (usuario.IdNivel == 3)
                {
                    especialidades = especialidades
                        .Where(e => e.IdCarrera == usuario.IdCarrera)
                        .ToList();
                }

                // IMPORTANTE: La consulta SQL cuenta todas las filas (COUNT(*)), no solo matrículas únicas
                // Pero para obtener el sexo, usamos matrículas únicas (el diccionario solo guarda una entrada por matrícula)
                int totalEstudiantes = alumnosMatriculas.Count;

                // IMPORTANTE: La consulta SQL cuenta todas las filas (incluso duplicados), pero el sexo se obtiene una vez por matrícula
                // Obtener matrículas únicas para el diccionario de sexo (evitar duplicados en el diccionario)
                // Pero asegurarse de que TODAS las matrículas de alumnosMatriculas estén en el diccionario
                var matriculasUnicasParaSexo = alumnosMatriculas.Distinct().ToList();

                // Usar la lógica de consulta_sexo_alumnos.sql para obtener sexo por matrícula
                // Prioridad 1: DatosPersonales (más reciente), Prioridad 2: EntrevistaInicials (más reciente)
                // IMPORTANTE: Pasar TODAS las matrículas (incluyendo duplicados) para que el diccionario tenga entradas para todas
                var sexoInfoPorMatricula = ObtenerSexoPorMatricula(alumnosMatriculas);

                // Calcular Hombres/Mujeres/SinSexo según la lógica EXACTA de la consulta SQL
                // IMPORTANTE: Contar todas las filas (alumnosMatriculas), pero usar el sexo del diccionario (por matrícula única)
                // Replica EXACTAMENTE: WHEN SexoFinal IS NOT NULL AND LTRIM(RTRIM(SexoFinal)) <> '' AND (UPPER(LTRIM(RTRIM(SexoFinal))) = 'H' OR 'HOMBRE' OR 'MASCULINO')
                // IMPORTANTE: En SQL, LTRIM(RTRIM(NULL)) devuelve NULL, no lanza error. En C#, null.Trim() lanza excepción, así que verificamos null primero.
                int totalHombres = alumnosMatriculas.Count(m =>
                {
                    // Normalizar matrícula para búsqueda en el diccionario
                    string matriculaNormalizada = m?.Trim() ?? "";
                    if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                    var sexo = sexoInfoPorMatricula[matriculaNormalizada].Sexo;
                    // Replica: WHEN SexoFinal IS NOT NULL
                    if (sexo == null) return false;
                    // Replica: AND LTRIM(RTRIM(SexoFinal)) <> ''
                    // IMPORTANTE: En SQL, LTRIM(RTRIM(NULL)) = NULL, así que si sexo es null, ya retornamos false arriba
                    var sexoTrimmed = sexo.Trim();
                    if (string.IsNullOrEmpty(sexoTrimmed)) return false;
                    // Replica: AND (UPPER(LTRIM(RTRIM(SexoFinal))) = 'H' OR 'HOMBRE' OR 'MASCULINO')
                    // IMPORTANTE: La consulta SQL normaliza en cada comparación, así que normalizamos aquí también
                    var sexoUpper = sexoTrimmed.ToUpper();
                    return sexoUpper == "H" || sexoUpper == "HOMBRE" || sexoUpper == "MASCULINO";
                });

                int totalMujeres = alumnosMatriculas.Count(m =>
                {
                    // Normalizar matrícula para búsqueda en el diccionario
                    string matriculaNormalizada = m?.Trim() ?? "";
                    if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                    var sexo = sexoInfoPorMatricula[matriculaNormalizada].Sexo;
                    // Replica: WHEN SexoFinal IS NOT NULL
                    if (sexo == null) return false;
                    // Replica: AND LTRIM(RTRIM(SexoFinal)) <> ''
                    // IMPORTANTE: En SQL, LTRIM(RTRIM(NULL)) = NULL, así que si sexo es null, ya retornamos false arriba
                    var sexoTrimmed = sexo.Trim();
                    if (string.IsNullOrEmpty(sexoTrimmed)) return false;
                    // Replica: AND (UPPER(LTRIM(RTRIM(SexoFinal))) = 'M' OR 'MUJER' OR 'FEMENINO')
                    // IMPORTANTE: La consulta SQL normaliza en cada comparación, así que normalizamos aquí también
                    var sexoUpper = sexoTrimmed.ToUpper();
                    return sexoUpper == "M" || sexoUpper == "MUJER" || sexoUpper == "FEMENINO";
                });

                // Solo contar como "Sin sexo" si tiene registro en alguna de las dos tablas Y su Sexo es 'No especificado' OR IS NULL
                // Replica: WHEN TieneRegistro = 1 AND (SexoFinal = 'No especificado' OR SexoFinal IS NULL)
                int totalSinSexo = alumnosMatriculas.Count(m =>
                {
                    // Normalizar matrícula para búsqueda en el diccionario
                    string matriculaNormalizada = m?.Trim() ?? "";
                    if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                    var info = sexoInfoPorMatricula[matriculaNormalizada];
                    // Primero verificar que tiene registro (TieneRegistro = 1)
                    if (!info.TieneRegistro) return false;
                    // Luego verificar que SexoFinal = 'No especificado' OR IS NULL
                    var sexo = info.Sexo;
                    if (sexo == null) return true; // IS NULL
                    // Replica comparación exacta con 'No especificado'
                    sexo = sexo.Trim();
                    return sexo == "" || sexo.Equals("No especificado", StringComparison.OrdinalIgnoreCase);
                });

                // IMPORTANTE: Traer los datos primero y filtrar en memoria para que la normalización funcione correctamente
                // (las matrículas en Alumnos pueden tener espacios al final que SQL no maneja correctamente)
                var todosDatosPersonales = tutoriasDb.DatosPersonales
                    .Where(d => d.Matricula != null)
                    .Select(d => new { d.Matricula, d.Sexo, d.IdPersona, d.IdCarrera, d.Especialidad, d.Fecha })
                    .ToList()
                    .Where(d => alumnosMatriculasHashSet.Contains(NormalizarMatricula(d.Matricula)))
                    .GroupBy(x => NormalizarMatricula(x.Matricula), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Fecha).First(), StringComparer.OrdinalIgnoreCase);

                // Usar los datos ya cargados para calcular padres/madres/embarazadas
                // Usar el sexo desde sexoInfoPorMatricula (que incluye EntrevistaInicials) en lugar de solo DatosPersonales
                var datosPersonalesCompletos = todosDatosPersonales.Values
                    .Select(dp => new
                    {
                        dp.Matricula,
                        Sexo = sexoInfoPorMatricula.ContainsKey(dp.Matricula) ? sexoInfoPorMatricula[dp.Matricula].Sexo : dp.Sexo,
                        dp.IdPersona
                    })
                    .ToList();

                // Obtener lista de IdPersona como primitivos para la consulta
                var idPersonasList = datosPersonalesCompletos.Select(d => d.IdPersona).ToList();

                // Obtener IdHijo e IdEmbarazo desde AspectosPersonales
                // IdHijo: 1 = Sí tiene hijos, 2 = No tiene hijos
                // IdEmbarazo: 1 = No está embarazada, 2 = Sí está embarazada
                var aspectosPersonalesCompletos = db.AspectosPersonales
                    .Where(ap => idPersonasList.Contains(ap.IdPersona))
                    .Select(ap => new { ap.IdPersona, ap.IdHijo, ap.IdEmbarazo })
                    .ToList()
                    .GroupBy(ap => ap.IdPersona)
                    .ToDictionary(g => g.Key, g => g.First());

                // Optimización: Obtener IdTrabajo desde EntrevistaInicial
                // IMPORTANTE: Filtrar en memoria para normalizar matrículas correctamente
                var entrevistasIniciales = tutoriasDb.EntrevistaInicials
                    .Where(x => x.Matricula != null)
                    .Select(x => new { x.Matricula, x.IdTrabajo })
                    .ToList()
                    .Where(x => alumnosMatriculasHashSet.Contains(NormalizarMatricula(x.Matricula)))
                    .GroupBy(x => NormalizarMatricula(x.Matricula), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // Calcular padres, madres y embarazadas
                int embarazadas = 0;
                int madres = 0;
                int padres = 0;

                foreach (var dp in datosPersonalesCompletos)
                {
                    if (!aspectosPersonalesCompletos.ContainsKey(dp.IdPersona))
                        continue;

                    var aspectos = aspectosPersonalesCompletos[dp.IdPersona];
                    bool tieneHijo = aspectos.IdHijo == 1; // 1 = Sí tiene hijos
                    bool esEmbarazada = aspectos.IdEmbarazo == 2; // 2 = Sí está embarazada
                    string sexo = dp.Sexo ?? "";

                    if (esEmbarazada)
                    {
                        embarazadas++;
                    }
                    else if (tieneHijo)
                    {
                        if (sexo.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                            sexo.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                            sexo.Equals("Femenino", StringComparison.OrdinalIgnoreCase))
                        {
                            madres++;
                        }
                        else if (sexo.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                                 sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                                 sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase))
                        {
                            padres++;
                        }
                    }
                }

                // IMPORTANTE: Alumnos Trabajando - Filtrar por período actual
                // Replica la consulta SQL: WHERE IdTrabajo = 1 AND Fecha BETWEEN fechaInicioPeriodo AND fechaFinPeriodo
                int alumnosTrabajando = CalcularAlumnosTrabajandoPorPeriodo(alumnosMatriculasHashSet, null, null);

                // El catálogo de carreras se construirá después de que carreraPorMatricula esté completo
                var catalogoCarreras = new Dictionary<int, string>();

                // Obtener año y período actuales
                int añoActual = DateTime.Now.Year;
                int periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);

                // Determinar el período académico basado en el período actual
                string periodoAcademico = "";
                switch (periodoActual)
                {
                    case 1:
                        periodoAcademico = "ENERO-ABRIL";
                        break;
                    case 2:
                        periodoAcademico = "MAYO-AGOSTO";
                        break;
                    case 3:
                        periodoAcademico = "SEPTIEMBRE-DICIEMBRE";
                        break;
                }

                // OPTIMIZACIÓN: Estudiantes con hoja en Individuals del período actual
                // Usar la misma lógica que la consulta SQL: rango de fechas del año actual
                DateTime fechaInicio = new DateTime(añoActual, 1, 1);
                DateTime fechaFin = new DateTime(añoActual + 1, 1, 1);

                // Tras un corte dentro del cuatrimestre vigente, contar solo actividad posterior al corte
                var pVigenteHojas = PeriodoHelper.Obtener(añoActual, periodoActual);
                DateTime? corteDesdeHojas = CorteAplicable(pVigenteHojas.Inicio, pVigenteHojas.Fin);

                // OPTIMIZACIÓN: Cargar Individuals y Seguimientoes de una vez
                var individualsQuery = tutoriasDb.Individuals
                    .Where(ind => alumnosMatriculasHashSet.Contains(ind.Matricula) &&
                                 ind.Fecha >= fechaInicio &&
                                 ind.Fecha < fechaFin &&
                                 ind.Cuatrimestre.ToUpper().Replace(" ", "") == periodoAcademico);
                if (corteDesdeHojas.HasValue)
                {
                    var cdHojas = corteDesdeHojas.Value;
                    individualsQuery = individualsQuery.Where(ind => ind.Fecha >= cdHojas);
                }
                var todosIndividuals = individualsQuery
                    .Select(ind => new { ind.Matricula, ind.IdIndividual, ind.Fecha })
                                              .ToList();

                var estudiantesConIndividual = new HashSet<string>(todosIndividuals.Select(i => i.Matricula).Distinct());

                // OPTIMIZACIÓN: Seguimientos del período - cargar de una vez
                var todosSeguimientosData = (from ind in todosIndividuals
                                             join seg in tutoriasDb.Seguimientoes on ind.IdIndividual equals seg.IdIndividual
                                             where seg.Fecha.Year == añoActual
                                                   && (corteDesdeHojas == null || seg.Fecha >= corteDesdeHojas.Value)
                                             select new { ind.Matricula, seg.Fecha, seg.Vulnerabilidad })
                                            .ToList();

                // IMPORTANTE: Filtrar por el ÚLTIMO MES CON REGISTROS del período (igual que CalcularVulnerabilidades)
                // Esto asegura que las tablas de nivel y carrera muestren los mismos datos que la sección Vulnerabilidad
                int mesInicioPeriodoVista = 0, mesFinPeriodoVista = 0;
                switch (periodoActual)
                {
                    case 1: mesInicioPeriodoVista = 1; mesFinPeriodoVista = 4; break;
                    case 2: mesInicioPeriodoVista = 5; mesFinPeriodoVista = 8; break;
                    case 3: mesInicioPeriodoVista = 9; mesFinPeriodoVista = 12; break;
                }

                var seguimientosDelPeriodoVista = todosSeguimientosData
                    .Where(s => s.Fecha.Month >= mesInicioPeriodoVista && s.Fecha.Month <= mesFinPeriodoVista)
                    .ToList();

                Dictionary<string, dynamic> seguimientoUltimo;
                if (seguimientosDelPeriodoVista.Any())
                {
                    // Encontrar el último mes con registros
                    int ultimoMesConRegistrosVista = seguimientosDelPeriodoVista.Max(s => s.Fecha.Month);

                    // Filtrar solo seguimientos del último mes con registros
                    seguimientoUltimo = seguimientosDelPeriodoVista
                        .Where(s => s.Fecha.Month == ultimoMesConRegistrosVista)
                                          .GroupBy(x => x.Matricula)
                        .ToDictionary(g => g.Key, g => (dynamic)g.OrderByDescending(x => x.Fecha).First());
                }
                else
                {
                    seguimientoUltimo = new Dictionary<string, dynamic>();
                }

                // Estadísticas de vulnerabilidades - usar la lógica de consulta_vulnerabilidades_filtrada.sql
                // IMPORTANTE: Usar el método CalcularVulnerabilidades que replica exactamente la consulta SQL
                // Validar que mes y periodo no se usen juntos
                if (mes.HasValue && periodo.HasValue)
                {
                    ViewBag.Error = "Los filtros de mes y período no pueden usarse simultáneamente.";
                    return View();
                }

                // El año siempre será el año actual (no se puede cambiar)
                int añoParaFiltro = DateTime.Now.Year;
                int? mesParaFiltro = mes;
                int? periodoParaFiltro = periodo;

                // IMPORTANTE: Si no hay mes especificado, usar el mes actual por defecto
                if (!mes.HasValue && !periodo.HasValue)
                {
                    mesParaFiltro = DateTime.Now.Month; // Usar mes actual
                }

                var vulnerabilidades = CalcularVulnerabilidades(alumnosMatriculas, idAreaCoordinador, null, null, mesParaFiltro, añoParaFiltro, periodoParaFiltro);

                // Usar los datos ya cargados de DatosPersonales (evitar consulta duplicada)
                // IMPORTANTE: todosDatosPersonales ya tiene solo el registro más reciente por matrícula
                var datosDP = todosDatosPersonales.Values
                    .Select(dp => new { dp.Matricula, IdCarrera = (int?)dp.IdCarrera, dp.Sexo, Fecha = (DateTime?)dp.Fecha })
                    .ToList();

                // IMPORTANTE: Usar DatosPersonales.IdCarrera para el agrupamiento por carrera
                // porque tiene la información correcta de la carrera actual del estudiante
                // Crear diccionario que mapea matrícula -> IdCarrera desde DatosPersonales
                var carreraPorMatricula = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in todosDatosPersonales)
                {
                    var matriculaNorm = NormalizarMatricula(kvp.Key);
                    if (!string.IsNullOrEmpty(matriculaNorm) && !carreraPorMatricula.ContainsKey(matriculaNorm))
                    {
                        carreraPorMatricula[matriculaNorm] = kvp.Value.IdCarrera > 0 ? kvp.Value.IdCarrera : 0;
                    }
                }

                // Para estudiantes que no tienen DatosPersonales, usar la carrera de Alumnos
                foreach (var matricula in alumnosMatriculas)
                {
                    if (!carreraPorMatricula.ContainsKey(matricula))
                    {
                        // Buscar en alumnosInfo (de Alumnos)
                        if (alumnosInfo.ContainsKey(matricula) && alumnosInfo[matricula] > 0)
                        {
                            carreraPorMatricula[matricula] = alumnosInfo[matricula];
                        }
                        else
                        {
                            carreraPorMatricula[matricula] = 0;
                        }
                    }
                }

                // Construir catálogo de carreras ahora que carreraPorMatricula está completo
                // Obtener todas las carreras únicas de TODOS los alumnos (habilitados y no habilitados)
                // IMPORTANTE: Ahora todas las carreras están normalizadas a IdArea de EstadiasUTTN
                // SIMPLE: Usar solo carreras desde GestionUsuarios.Alumnos (ya son IdArea de EstadiasUTTN)
                // Esto evita duplicados entre Tutorias y EstadiasUTTN
                var idsCarrerasUnicas = new HashSet<int>(
                    usuariosDb.Alumnos
                        .Where(a => a.Matricula != null && a.Matricula != "" && a.IdCarrera > 0)
                    .Select(a => a.IdCarrera)
                    .Distinct()
                        .ToList()
                );

                // Si es coordinador, filtrar por su carrera (usando IdArea mapeado)
                if (usuario.IdNivel == 3 && idAreaCoordinador.HasValue)
                {
                    idsCarrerasUnicas = new HashSet<int>(idsCarrerasUnicas.Where(id => id == idAreaCoordinador.Value));
                }

                // OPTIMIZACIÓN: Cargar todos los nombres de carreras de una vez en lugar de consultar uno por uno
                if (idsCarrerasUnicas.Any())
                {
                    catalogoCarreras = CargarNombresCarreras(idsCarrerasUnicas.ToList());
                }

                // SIMPLE: Agrupar estudiantes por carrera usando Alumnos.IdCarrera (no DatosPersonales)
                // alumnosInfo ya tiene el mapeo matrícula -> IdCarrera desde Alumnos
                var estudiantesPorCarrera = alumnosMatriculas
                    .GroupBy(m => alumnosInfo.ContainsKey(m) ? alumnosInfo[m] : 0)
                    .Select(g => new { IdCarrera = g.Key, Matriculas = g.ToList() })
                    .ToList();

                var carrerasDesdeDP = estudiantesPorCarrera
                    .Select(g =>
                    {
                        // Contar desde TODOS los registros de esta carrera
                        var matriculasCarrera = g.Matriculas;

                        // Usar sexoInfoPorMatricula que incluye la lógica de consulta_sexo_alumnos.sql
                        int hombres = matriculasCarrera.Count(m =>
                        {
                            if (!sexoInfoPorMatricula.ContainsKey(m)) return false;
                            var sexo = sexoInfoPorMatricula[m].Sexo ?? "";
                            sexo = sexo.Trim();
                            return !string.IsNullOrEmpty(sexo) &&
                                   (sexo.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                                    sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                                    sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase));
                        });
                        int mujeres = matriculasCarrera.Count(m =>
                        {
                            if (!sexoInfoPorMatricula.ContainsKey(m)) return false;
                            var sexo = sexoInfoPorMatricula[m].Sexo ?? "";
                            sexo = sexo.Trim();
                            return !string.IsNullOrEmpty(sexo) &&
                                   (sexo.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                                    sexo.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                                    sexo.Equals("Femenino", StringComparison.OrdinalIgnoreCase));
                        });
                        // Solo contar como "Sin sexo" si tiene registro Y su Sexo es 'No especificado' OR IS NULL
                        int sinSexo = matriculasCarrera.Count(m =>
                        {
                            if (!sexoInfoPorMatricula.ContainsKey(m)) return false;
                            var info = sexoInfoPorMatricula[m];
                            if (!info.TieneRegistro) return false;
                            var sexo = info.Sexo ?? "";
                            sexo = sexo.Trim();
                            return sexo == "" || sexo.Equals("No especificado", StringComparison.OrdinalIgnoreCase);
                        });

                        // IMPORTANTE: Usar CalcularVulnerabilidades para cada carrera
                        // IMPORTANTE: Para la tabla por carrera, si no hay filtros aplicados,
                        // usar el período actual en lugar del mes actual para mantener la funcionalidad anterior
                        int? mesParaCarrera = mesParaFiltro;
                        int? periodoParaCarrera = periodoParaFiltro;

                        // Si no hay filtros aplicados (ni mes ni período), usar el período actual para la tabla por carrera
                        if (!mes.HasValue && !periodo.HasValue)
                        {
                            mesParaCarrera = null; // No usar mes específico
                            periodoParaCarrera = periodoActual; // Usar período actual
                        }

                        // Esto garantiza que los conteos de vulnerabilidades coincidan con la sección Vulnerabilidad
                        var vulnCarrera = CalcularVulnerabilidades(matriculasCarrera, idAreaCoordinador, null, null, mesParaCarrera, añoParaFiltro, periodoParaCarrera);

                        return new EstadisticaCarrera
                        {
                            IdCarrera = g.IdCarrera,
                            Nombre = catalogoCarreras.ContainsKey(g.IdCarrera)
                                ? catalogoCarreras[g.IdCarrera]
                                : (g.IdCarrera == 0 ? "Sin carrera asignada" : GetNombreCarrera(g.IdCarrera, catalogoCarreras)),
                            Cantidad = matriculasCarrera.Count,
                            Hombres = hombres,
                            Mujeres = mujeres,
                            SinSexo = sinSexo,
                            VulnerablesEconomicos = vulnCarrera.VulnerablesEconomicos,
                            VulnerablesAcademicos = vulnCarrera.VulnerablesAcademicos,
                            VulnerablesPersonales = vulnCarrera.VulnerablesPersonales,
                            NoVulnerables = vulnCarrera.NoVulnerables,
                            SinSeguimiento = vulnCarrera.SinSeguimiento
                        };
                    })
                    .OrderBy(x => x.Nombre)
                    .ToList();

                // Ya no necesitamos agregar estudiantes sin carrera por separado, 
                // porque ahora los incluimos en el grupo con IdCarrera = 0

                var estadisticasPorGrupo = CalcularEstadisticasPorGrupo();

                // Usar los datos ya cargados de DatosPersonales para especialidad (evitar consulta duplicada)
                var datosEspecialidad = todosDatosPersonales.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Especialidad
                );

                // PASO 1: Clasificar matrículas por nivel de estudio basándose en la especialidad
                CompareInfo compareInfo = CultureInfo.InvariantCulture.CompareInfo;

                var matriculasTSU = new List<string>();
                var matriculasIngenieria = new List<string>();
                var matriculasLicenciatura = new List<string>();

                int totalTSU = 0, totalIngenieria = 0, totalLicenciatura = 0;
                int hombresTSU = 0, mujeresTSU = 0;
                int hombresIngenieria = 0, mujeresIngenieria = 0;
                int hombresLicenciatura = 0, mujeresLicenciatura = 0;

                // Clasificar cada matrícula por nivel de estudio y contar totales/sexo
                foreach (var matricula in alumnosMatriculas)
                {
                    string especialidad = datosEspecialidad.ContainsKey(matricula) ? (datosEspecialidad[matricula] ?? "") : "";
                    string sexo = sexoInfoPorMatricula.ContainsKey(matricula) ? (sexoInfoPorMatricula[matricula].Sexo ?? "") : "";
                    sexo = sexo.Trim();
                    bool esHombre = !string.IsNullOrEmpty(sexo) && (sexo.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                                                                   sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                                                                   sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase));
                    bool esMujer = !string.IsNullOrEmpty(sexo) && (sexo.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                                                                   sexo.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                                                                   sexo.Equals("Femenino", StringComparison.OrdinalIgnoreCase));

                    if (compareInfo.IndexOf(especialidad, "ingenieria", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    {
                        matriculasIngenieria.Add(matricula);
                        totalIngenieria++;
                        if (esHombre) hombresIngenieria++;
                        if (esMujer) mujeresIngenieria++;
                    }
                    else if (compareInfo.IndexOf(especialidad, "licenciatura", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    {
                        matriculasLicenciatura.Add(matricula);
                        totalLicenciatura++;
                        if (esHombre) hombresLicenciatura++;
                        if (esMujer) mujeresLicenciatura++;
                    }
                    else
                    {
                        // TSU por defecto (incluyendo especialidades específicas de TSU)
                        matriculasTSU.Add(matricula);
                        totalTSU++;
                        if (esHombre) hombresTSU++;
                        if (esMujer) mujeresTSU++;
                    }
                }

                // PASO 2: Usar CalcularVulnerabilidades para cada nivel de estudio
                // IMPORTANTE: Para la tabla por nivel de estudio, si no hay filtros aplicados,
                // usar el período actual en lugar del mes actual para mantener la funcionalidad anterior
                int? mesParaNivel = mesParaFiltro;
                int? periodoParaNivel = periodoParaFiltro;

                // Si no hay filtros aplicados (ni mes ni período), usar el período actual para la tabla por nivel
                if (!mes.HasValue && !periodo.HasValue)
                {
                    mesParaNivel = null; // No usar mes específico
                    periodoParaNivel = periodoActual; // Usar período actual
                }

                // Esto garantiza que los conteos de vulnerabilidades coincidan con la sección Vulnerabilidad
                var vulnTSU = CalcularVulnerabilidades(matriculasTSU, idAreaCoordinador, null, null, mesParaNivel, añoParaFiltro, periodoParaNivel);
                var vulnIngenieria = CalcularVulnerabilidades(matriculasIngenieria, idAreaCoordinador, null, null, mesParaNivel, añoParaFiltro, periodoParaNivel);
                var vulnLicenciatura = CalcularVulnerabilidades(matriculasLicenciatura, idAreaCoordinador, null, null, mesParaNivel, añoParaFiltro, periodoParaNivel);

                // Extraer conteos de vulnerabilidades
                int econTSU = vulnTSU.VulnerablesEconomicos;
                int acadTSU = vulnTSU.VulnerablesAcademicos;
                int persTSU = vulnTSU.VulnerablesPersonales;
                int noVulTSU = vulnTSU.NoVulnerables;
                int sinSegTSU = vulnTSU.SinSeguimiento;

                int econIng = vulnIngenieria.VulnerablesEconomicos;
                int acadIng = vulnIngenieria.VulnerablesAcademicos;
                int persIng = vulnIngenieria.VulnerablesPersonales;
                int noVulIng = vulnIngenieria.NoVulnerables;
                int sinSegIng = vulnIngenieria.SinSeguimiento;

                int econLic = vulnLicenciatura.VulnerablesEconomicos;
                int acadLic = vulnLicenciatura.VulnerablesAcademicos;
                int persLic = vulnLicenciatura.VulnerablesPersonales;
                int noVulLic = vulnLicenciatura.NoVulnerables;
                int sinSegLic = vulnLicenciatura.SinSeguimiento;

                ViewBag.TotalEstudiantes = totalEstudiantes;
                ViewBag.TotalHombres = totalHombres;
                ViewBag.TotalMujeres = totalMujeres;
                ViewBag.TotalSinSexo = totalSinSexo;
                ViewBag.Embarazadas = embarazadas;
                ViewBag.Madres = madres;
                ViewBag.Padres = padres;
                ViewBag.PadresFamilia = embarazadas + madres + padres; // Total para compatibilidad
                ViewBag.AlumnosTrabajando = alumnosTrabajando;
                ViewBag.EstadisticasPorCarrera = carrerasDesdeDP;
                ViewBag.Vulnerabilidades = vulnerabilidades;
                ViewBag.TieneDesgloseFuente = true;
                ViewBag.SinSeguimientoTotal = vulnerabilidades.SinSeguimiento;
                ViewBag.EstadisticasPorGrupo = estadisticasPorGrupo;
                ViewBag.TotalTSU = totalTSU;
                ViewBag.TotalIngenieria = totalIngenieria;
                ViewBag.TotalLicenciatura = totalLicenciatura;
                ViewBag.HombresTSU = hombresTSU;
                ViewBag.MujeresTSU = mujeresTSU;
                ViewBag.HombresIngenieria = hombresIngenieria;
                ViewBag.MujeresIngenieria = mujeresIngenieria;
                ViewBag.HombresLicenciatura = hombresLicenciatura;
                ViewBag.MujeresLicenciatura = mujeresLicenciatura;

                // Vulnerabilidades por nivel de estudio
                ViewBag.EconTSU = econTSU;
                ViewBag.AcadTSU = acadTSU;
                ViewBag.PersTSU = persTSU;
                ViewBag.NoVulTSU = noVulTSU;
                ViewBag.SinSegTSU = sinSegTSU;

                ViewBag.EconIng = econIng;
                ViewBag.AcadIng = acadIng;
                ViewBag.PersIng = persIng;
                ViewBag.NoVulIng = noVulIng;
                ViewBag.SinSegIng = sinSegIng;

                ViewBag.EconLic = econLic;
                ViewBag.AcadLic = acadLic;
                ViewBag.PersLic = persLic;
                ViewBag.NoVulLic = noVulLic;
                ViewBag.SinSegLic = sinSegLic;

                ViewBag.Especialidades = especialidades;
                ViewBag.Carreras = catalogoCarreras;

                // Cascada carrera->especialidad (2026-07-29): nombre de la carrera (Tutorias) de cada especialidad.
                var carrerasTutoriasNombres = db.Carreras.ToDictionary(c => c.IdCarrera, c => c.Nombre);
                ViewBag.EspecialidadCarrera = especialidades.ToDictionary(
                    e => e.Id,
                    e => carrerasTutoriasNombres.ContainsKey(e.IdCarrera) ? (carrerasTutoriasNombres[e.IdCarrera] ?? "") : "");

                // Catálogo para el filtro de Materias Reprobadas: IDs de Tutorias.Carreras (los que usa
                // GetEstadisticasMaterias vía dp.IdCarrera), NO los IDs de área de ViewBag.Carreras.
                ViewBag.CarrerasMaterias = db.Carreras
                    .OrderBy(c => c.Nombre)
                    .ToList()
                    .ToDictionary(c => c.IdCarrera, c => (c.Nombre ?? "").Trim());
                // Nombre (Tutorias) de la carrera del nivel 3, para filtrar el histórico client-side.
                ViewBag.CarreraNombreMateriasNivel3 = usuario.IdNivel == 3
                    ? db.Carreras.Where(c => c.IdCarrera == usuario.IdCarrera).Select(c => c.Nombre).FirstOrDefault()
                    : null;

                // Catalogos para los filtros especificos (peticion direccion 2026-07-16). Con try/catch:
                // un catalogo caido deja el dropdown vacio pero NO tira la pagina.
                try
                {
                    ViewBag.FiltroGrados = tutoriasDb.Gradoes.OrderBy(g => g.IdGrado)
                        .ToList().ToDictionary(g => g.IdGrado, g => (g.Nombre ?? "").Trim());
                    ViewBag.FiltroGrupos = tutoriasDb.Grupoes.OrderBy(g => g.IdGrupo)
                        .ToList().ToDictionary(g => g.IdGrupo, g => (g.Nombre ?? "").Trim());
                    ViewBag.FiltroTurnos = tutoriasDb.Turnoes.OrderBy(t => t.IdTurno)
                        .ToList().ToDictionary(t => t.IdTurno, t => (t.Nombre ?? "").Trim());
                }
                catch { ViewBag.FiltroGrados = null; ViewBag.FiltroGrupos = null; ViewBag.FiltroTurnos = null; }
                try
                {
                    // Materias con reprobacion/extraordinario (nombres unicos), acotadas a la carrera del nivel 3.
                    var materiasQry = db.Database.SqlQuery<string>(usuario.IdNivel == 3 ? @"
                        SELECT DISTINCT m.Nombre FROM Materias m
                        INNER JOIN MateriasAlumno ma ON ma.IdMateria = m.IdMateria
                        WHERE ma.Estado IN ('Reprobada','Extraordinario') AND m.IdCarrera = @p0 ORDER BY m.Nombre" : @"
                        SELECT DISTINCT m.Nombre FROM Materias m
                        INNER JOIN MateriasAlumno ma ON ma.IdMateria = m.IdMateria
                        WHERE ma.Estado IN ('Reprobada','Extraordinario') ORDER BY m.Nombre",
                        usuario.IdCarrera).ToList();
                    ViewBag.FiltroMaterias = materiasQry;
                }
                catch { ViewBag.FiltroMaterias = new List<string>(); }
                try
                {
                    ViewBag.FiltroCausasBaja = db.Database.SqlQuery<string>(
                        "SELECT DISTINCT Causa FROM BajasAlumnos WHERE Causa IS NOT NULL AND LTRIM(RTRIM(Causa)) <> '' ORDER BY Causa").ToList();
                }
                catch { ViewBag.FiltroCausasBaja = new List<string>(); }
                try
                {
                    var aniosCat = db.Database.SqlQuery<int>(
                        "SELECT DISTINCT AnioPeriodo FROM EstadisticasHistoricoCorte").ToList();
                    if (!aniosCat.Contains(DateTime.Now.Year)) aniosCat.Add(DateTime.Now.Year);
                    aniosCat.Sort(); aniosCat.Reverse();
                    ViewBag.FiltroAnios = aniosCat;
                }
                catch { ViewBag.FiltroAnios = new List<int> { DateTime.Now.Year }; }

                ViewBag.UsuarioNivel = usuario.IdNivel;
                // Para coordinadores, usar el IdArea mapeado; para otros, usar el IdCarrera original
                ViewBag.UsuarioCarrera = (usuario.IdNivel == 3 && idAreaCoordinador.HasValue) ? idAreaCoordinador.Value : usuario.IdCarrera;
                // Pasar el nombre de la carrera del coordinador para pre-llenar el filtro
                ViewBag.NombreCarreraCoordinador = nombreCarreraCoordinador;
                ViewBag.IdAreaCoordinador = idAreaCoordinador;

                // Calcular período actual si no se especifica
                int periodoActualCalculado = periodo ?? ((DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3));

                // Pasar filtros de fecha para la vista
                // NOTA: El año siempre es el actual y no se puede cambiar por el usuario
                // IMPORTANTE: Si no hay mes especificado, usar el mes actual
                ViewBag.MesFiltro = mesParaFiltro ?? DateTime.Now.Month;
                ViewBag.AñoFiltro = DateTime.Now.Year; // Siempre el año actual
                ViewBag.PeriodoFiltro = periodo ?? periodoActualCalculado; // Si no se especifica período, usar el actual
                ViewBag.PeriodoActual = periodoActualCalculado; // Pasar también el período actual calculado
                ViewBag.NombresMeses = NombresMeses;

                // Calcular estadísticas de bajas, PATs y arrastre
                // Para coordinadores usamos directamente el IdCarrera de Tutorias (las tablas fuente comparten esa clave)
                int? carreraIdTutoriasFiltro = null;
                if (usuario.IdNivel == 3)
                {
                    carreraIdTutoriasFiltro = usuario.IdCarrera;
                }
                var estadisticasBajas = CalcularEstadisticasBajas(carreraIdTutoriasFiltro);
                var estadisticasPATs = CalcularEstadisticasPATs(carreraIdTutoriasFiltro);
                var estadisticasArrastre = CalcularEstadisticasArrastre(carreraIdTutoriasFiltro);

                ViewBag.EstadisticasBajas = estadisticasBajas;
                ViewBag.EstadisticasPATs = estadisticasPATs;
                ViewBag.EstadisticasArrastre = estadisticasArrastre;

                // Modo historico: si se pidio un corte, sobrescribe las secciones server-side
                // (demografia, situacion familiar, nivel de estudio) con los datos congelados del snapshot.
                // Las secciones AJAX (carrera, cierre, detalladas) usan corteId por su cuenta.
                ViewBag.CorteIdActual = corteId;
                if (corteId.HasValue)
                {
                    var corteHist = db.EstadisticasHistoricoCortes.Find(corteId.Value);
                    if (corteHist != null)
                    {
                        PoblarViewBagDesdeCorte(corteId.Value, corteHist);
                        ViewBag.IncluirBajas = false;
                    }
                }
                else
                {
                    // En vivo: si ya hubo corte en el cuatrimestre vigente, los datos cuentan desde el
                    ViewBag.CorteVigenteDesde = ObtenerFechaUltimoCorteVigente();
                }

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los datos: " + ex.Message;
                return View();
            }
        }

        // Clase auxiliar para el último seguimiento por alumno
        public class SegUlt
        {
            public string Matricula { get; set; }
            public string Vulnerabilidad { get; set; }
        }

        [HttpPost]
        public ActionResult GetEstadisticasDetalladas(int? mes = null, int? año = null, int? periodo = null, bool incluirBajas = false, int? corteId = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "Detalladas");
                    if (hist != null) return hist;
                }
                // Debug: Log de parámetros recibidos
                System.Diagnostics.Debug.WriteLine($"GetEstadisticasDetalladas - Parámetros recibidos: mes={mes?.ToString() ?? "NULL"}, año={año?.ToString() ?? "NULL"}, periodo={periodo?.ToString() ?? "NULL"}");
                // Obtener usuario de la sesión
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, error = "Sesión expirada" });
                }

                // Obtener IdArea del coordinador si es necesario
                int? idAreaCoordinador = null;
                if (usuario.IdNivel == 3)
                {
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                }

                // IMPORTANTE: Obtener alumnos directamente desde GestionUsuarios.Alumnos (sin filtrar por habilitado)
                // Esta es la lógica correcta según el controlador de referencia
                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();

                // Si es coordinador, filtrar por IdArea
                if (idAreaCoordinador.HasValue)
                {
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                }

                // Obtener todas las matrículas directamente desde Alumnos
                // IMPORTANTE: Replica exactamente la consulta SQL: WHERE Matricula IS NOT NULL AND Matricula <> ''
                // La consulta SQL NO usa DISTINCT en el COUNT(*), cuenta todas las filas que cumplen la condición
                var alumnosData = alumnosQuery
                    .Select(a => a.Matricula)
                    .ToList();

                // Filtrar en memoria exactamente igual que la SQL: WHERE Matricula IS NOT NULL AND Matricula <> ''
                // IMPORTANTE: Usar NormalizarMatricula() para eliminar espacios, NBSP y otros caracteres invisibles
                // IMPORTANTE: Usar Distinct() para que los totales coincidan con las tablas por nivel/carrera/grupo
                // Usar Distinct() para eliminar duplicados reales
                var alumnosMatriculas = alumnosData
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                // Totales - matrículas únicas
                int totalEstudiantes = alumnosMatriculas.Count;

                // Usar la lógica de consulta_sexo_alumnos.sql para obtener sexo por matrícula
                // IMPORTANTE: Igual que SeguimientoEstadisticas
                var sexoInfoPorMatricula = ObtenerSexoPorMatricula(alumnosMatriculas);

                // IMPORTANTE: Usar la misma lógica EXACTA que SeguimientoEstadisticas
                // Replica EXACTAMENTE: WHEN SexoFinal IS NOT NULL AND LTRIM(RTRIM(SexoFinal)) <> '' AND (UPPER(LTRIM(RTRIM(SexoFinal))) = 'H' OR 'HOMBRE' OR 'MASCULINO')
                int totalHombres = alumnosMatriculas.Count(m =>
                {
                    // Normalizar matrícula para búsqueda en el diccionario
                    string matriculaNormalizada = m?.Trim() ?? "";
                    if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                    var sexo = sexoInfoPorMatricula[matriculaNormalizada].Sexo;
                    // Replica: WHEN SexoFinal IS NOT NULL
                    if (sexo == null) return false;
                    // Replica: AND LTRIM(RTRIM(SexoFinal)) <> ''
                    var sexoTrimmed = sexo.Trim();
                    if (string.IsNullOrEmpty(sexoTrimmed)) return false;
                    // Replica: AND (UPPER(LTRIM(RTRIM(SexoFinal))) = 'H' OR 'HOMBRE' OR 'MASCULINO')
                    var sexoUpper = sexoTrimmed.ToUpper();
                    return sexoUpper == "H" || sexoUpper == "HOMBRE" || sexoUpper == "MASCULINO";
                });

                int totalMujeres = alumnosMatriculas.Count(m =>
                {
                    // Normalizar matrícula para búsqueda en el diccionario
                    string matriculaNormalizada = m?.Trim() ?? "";
                    if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                    var sexo = sexoInfoPorMatricula[matriculaNormalizada].Sexo;
                    // Replica: WHEN SexoFinal IS NOT NULL
                    if (sexo == null) return false;
                    // Replica: AND LTRIM(RTRIM(SexoFinal)) <> ''
                    var sexoTrimmed = sexo.Trim();
                    if (string.IsNullOrEmpty(sexoTrimmed)) return false;
                    // Replica: AND (UPPER(LTRIM(RTRIM(SexoFinal))) = 'M' OR 'MUJER' OR 'FEMENINO')
                    var sexoUpper = sexoTrimmed.ToUpper();
                    return sexoUpper == "M" || sexoUpper == "MUJER" || sexoUpper == "FEMENINO";
                });

                // Solo contar como "Sin sexo" si tiene registro Y su Sexo es 'No especificado' OR IS NULL
                // Replica: WHEN TieneRegistro = 1 AND (SexoFinal = 'No especificado' OR SexoFinal IS NULL)
                int totalSinSexo = alumnosMatriculas.Count(m =>
                {
                    // Normalizar matrícula para búsqueda en el diccionario
                    string matriculaNormalizada = m?.Trim() ?? "";
                    if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                    var info = sexoInfoPorMatricula[matriculaNormalizada];
                    // Primero verificar que tiene registro (TieneRegistro = 1)
                    if (!info.TieneRegistro) return false;
                    // Luego verificar que SexoFinal = 'No especificado' OR IS NULL
                    var sexo = info.Sexo;
                    if (sexo == null) return true; // IS NULL
                    // Replica comparación exacta con 'No especificado'
                    sexo = sexo.Trim();
                    return sexo == "" || sexo.Equals("No especificado", StringComparison.OrdinalIgnoreCase);
                });

                // Calcular vulnerabilidades usando la lógica de consulta_vulnerabilidades_filtrada.sql
                // Validar que mes y periodo no se usen juntos
                if (mes.HasValue && periodo.HasValue)
                {
                    return Json(new { success = false, error = "Los filtros de mes y período no pueden usarse simultáneamente." });
                }

                // El año siempre es el año actual (no se puede cambiar)
                int añoParaFiltro = DateTime.Now.Year;

                // IMPORTANTE: Si no hay mes especificado, usar el mes actual por defecto
                int? mesParaFiltro = mes;
                if (!mes.HasValue && !periodo.HasValue)
                {
                    mesParaFiltro = DateTime.Now.Month; // Usar mes actual
                }

                System.Diagnostics.Debug.WriteLine($"Llamando a CalcularVulnerabilidades con: mes={mesParaFiltro?.ToString() ?? "NULL"}, año={añoParaFiltro}, periodo={periodo?.ToString() ?? "NULL"}");

                var vulnerabilidades = CalcularVulnerabilidades(alumnosMatriculas, idAreaCoordinador, null, null, mesParaFiltro, añoParaFiltro, periodo);

                // Indicadores adicionales desde EntrevistaInicial
                var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas);
                var ei = tutoriasDb.EntrevistaInicials
                    .Where(x => alumnosMatriculasHashSet.Contains(x.Matricula))
                    .Select(x => new { x.IdHijo, x.IdTrabajo })
                    .ToList();

                int padresFamilia = ei.Count(x => x.IdHijo == 1);

                // IMPORTANTE: Alumnos Trabajando - Filtrar por período actual o el especificado
                // Usar mesParaFiltro si está disponible (mes actual por defecto)
                int alumnosTrabajando = CalcularAlumnosTrabajandoPorPeriodo(alumnosMatriculasHashSet, mesParaFiltro ?? mes, periodo);

                var estadisticas = new
                {
                    totalEstudiantes,
                    totalHombres,
                    totalMujeres,
                    totalVulnerables = vulnerabilidades.TotalVulnerables,
                    vulnerablesEconomicos = vulnerabilidades.VulnerablesEconomicos,
                    vulnerablesPersonales = vulnerabilidades.VulnerablesPersonales,
                    vulnerablesAcademicos = vulnerabilidades.VulnerablesAcademicos,
                    noVulnerables = vulnerabilidades.NoVulnerables,
                    sinSeguimiento = vulnerabilidades.SinSeguimiento,
                    sinInformacion = vulnerabilidades.SinInformacion,
                    clasificadosPorSeguimiento = vulnerabilidades.ClasificadosPorSeguimiento,
                    clasificadosPorIdentificacion = vulnerabilidades.ClasificadosPorIdentificacion,
                    padresFamilia,
                    alumnosTrabajando,
                    totalSinSexo
                };

                return Json(new { success = true, data = estadisticas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Obtener estadísticas por cierre de cuatrimestres (todos los períodos)
        [HttpPost]
        public ActionResult GetEstadisticasPorCierreCuatrimestres(int? año = null, bool incluirBajas = false, int? corteId = null, int? carreraId = null, int? especialidadId = null, int? gradoId = null, int? grupoId = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "CierreCuatrimestres");
                    if (hist != null) return hist;
                }
                // Obtener usuario de la sesión
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, error = "Sesión expirada" });
                }

                // Obtener IdArea del coordinador si es necesario
                int? idAreaCoordinador = null;
                if (usuario.IdNivel == 3)
                {
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                }

                // El año siempre es el año actual
                int añoActual = año ?? DateTime.Now.Year;

                // Obtener alumnos directamente desde GestionUsuarios.Alumnos
                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();

                // Si es coordinador, filtrar por IdArea (fail-closed: ignora carreraId del cliente)
                if (idAreaCoordinador.HasValue)
                {
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                }
                else if (carreraId.HasValue && usuario.IdNivel == 4)
                {
                    // Filtro de carrera para Master (direccion 2026-07-16); IDs de AREA como ViewBag.Carreras.
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == carreraId.Value);
                }

                // Obtener todas las matrículas
                var alumnosData = alumnosQuery
                    .Select(a => a.Matricula)
                    .ToList();

                var alumnosMatriculas = alumnosData
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                // Filtros por seccion (2026-07-29): especialidad + grado/grupo.
                if (especialidadId.HasValue)
                    alumnosMatriculas = FiltrarMatriculasPorEspecialidad(alumnosMatriculas, especialidadId.Value);
                alumnosMatriculas = FiltrarMatriculasPorGrupoGrado(alumnosMatriculas, grupoId, gradoId);

                // Reinicio por corte: restringir la poblacion a alumnos con actividad posterior al corte.
                var _activasPostCorte = ObtenerMatriculasActivasPostCorte();
                if (_activasPostCorte != null)
                    alumnosMatriculas = alumnosMatriculas.Where(m => m != null && _activasPostCorte.Contains(NormalizarMatricula(m))).ToList();

                // Lista de períodos a procesar
                var periodos = new[]
                {
                    new { Id = 1, Nombre = "Enero - Abril", MesInicio = 1, MesFin = 4 },
                    new { Id = 2, Nombre = "Mayo - Agosto", MesInicio = 5, MesFin = 8 },
                    new { Id = 3, Nombre = "Septiembre - Diciembre", MesInicio = 9, MesFin = 12 }
                };

                var estadisticasPorPeriodo = new List<object>();

                foreach (var periodoInfo in periodos)
                {
                    try
                    {
                        // Calcular vulnerabilidades para este período
                        var vulnerabilidades = CalcularVulnerabilidades(
                            alumnosMatriculas,
                            idAreaCoordinador,
                            null,
                            null,
                            null, // mes
                            añoActual,
                            periodoInfo.Id // periodo
                        );

                        // Obtener sexo por matrícula
                        var sexoInfoPorMatricula = ObtenerSexoPorMatricula(alumnosMatriculas);

                        // Calcular totales de sexo
                        int totalHombres = alumnosMatriculas.Count(m =>
                        {
                            string matriculaNormalizada = m?.Trim() ?? "";
                            if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                            var sexo = sexoInfoPorMatricula[matriculaNormalizada].Sexo;
                            if (sexo == null) return false;
                            var sexoTrimmed = sexo.Trim();
                            if (string.IsNullOrEmpty(sexoTrimmed)) return false;
                            var sexoUpper = sexoTrimmed.ToUpper();
                            return sexoUpper == "H" || sexoUpper == "HOMBRE" || sexoUpper == "MASCULINO";
                        });

                        int totalMujeres = alumnosMatriculas.Count(m =>
                        {
                            string matriculaNormalizada = m?.Trim() ?? "";
                            if (string.IsNullOrEmpty(matriculaNormalizada) || !sexoInfoPorMatricula.ContainsKey(matriculaNormalizada)) return false;
                            var sexo = sexoInfoPorMatricula[matriculaNormalizada].Sexo;
                            if (sexo == null) return false;
                            var sexoTrimmed = sexo.Trim();
                            if (string.IsNullOrEmpty(sexoTrimmed)) return false;
                            var sexoUpper = sexoTrimmed.ToUpper();
                            return sexoUpper == "M" || sexoUpper == "MUJER" || sexoUpper == "FEMENINO";
                        });

                        // Calcular alumnos trabajando para este período
                        var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas);
                        int alumnosTrabajando = CalcularAlumnosTrabajandoPorPeriodo(alumnosMatriculasHashSet, null, periodoInfo.Id);

                        estadisticasPorPeriodo.Add(new
                        {
                            periodoId = periodoInfo.Id,
                            periodoNombre = periodoInfo.Nombre,
                            año = añoActual,
                            totalEstudiantes = alumnosMatriculas.Count,
                            totalHombres = totalHombres,
                            totalMujeres = totalMujeres,
                            vulnerablesEconomicos = vulnerabilidades.VulnerablesEconomicos,
                            vulnerablesAcademicos = vulnerabilidades.VulnerablesAcademicos,
                            vulnerablesPersonales = vulnerabilidades.VulnerablesPersonales,
                            noVulnerables = vulnerabilidades.NoVulnerables,
                            sinSeguimiento = vulnerabilidades.SinSeguimiento,
                            totalVulnerables = vulnerabilidades.TotalVulnerables,
                            alumnosTrabajando = alumnosTrabajando,
                            tieneDatos = true
                        });
                    }
                    catch (Exception ex)
                    {
                        // Si hay error, agregar período sin datos
                        estadisticasPorPeriodo.Add(new
                        {
                            periodoId = periodoInfo.Id,
                            periodoNombre = periodoInfo.Nombre,
                            año = añoActual,
                            totalEstudiantes = 0,
                            totalHombres = 0,
                            totalMujeres = 0,
                            vulnerablesEconomicos = 0,
                            vulnerablesAcademicos = 0,
                            vulnerablesPersonales = 0,
                            noVulnerables = 0,
                            sinSeguimiento = 0,
                            totalVulnerables = 0,
                            alumnosTrabajando = 0,
                            tieneDatos = false,
                            error = ex.Message
                        });
                    }
                }

                return Json(new { success = true, data = estadisticasPorPeriodo });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Método de prueba para verificar que el controlador funciona
        [HttpPost]
        public JsonResult TestConnection()
        {
            try
            {
                var totalAlumnos = usuariosDb.Alumnos.Count();
                var totalHabilitados = usuariosDb.Alumnos.Where(a => a.Habilitado == true).Count();

                return Json(new
                {
                    success = true,
                    message = "Conexión exitosa",
                    totalAlumnos = totalAlumnos,
                    totalHabilitados = totalHabilitados
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Método adicional para obtener estadísticas por carrera
        [HttpPost]
        public ActionResult GetEstadisticasPorCarrera(bool incluirBajas = false, int? corteId = null, int? especialidadId = null, int? gradoId = null, int? grupoId = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "Carrera");
                    if (hist != null) return hist;
                }
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, error = "Sesión expirada" });

                usuariosDb.Database.CommandTimeout = 300;
                tutoriasDb.Database.CommandTimeout = 300;
                db.Database.CommandTimeout = 300;

                int? idAreaCoordinador = null;
                if (usuario.IdNivel == 3)
                {
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                    if (!idAreaCoordinador.HasValue)
                        return Json(new { success = true, data = new object[0] });
                }

                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();
                if (idAreaCoordinador.HasValue)
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);

                var alumnosData = alumnosQuery
                    .Where(a => a.Matricula != null && a.Matricula != "")
                    .Select(a => new { a.Matricula, a.IdCarrera })
                    .ToList();

                var alumnosMatriculas = alumnosData
                    .Select(a => NormalizarMatricula(a.Matricula))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                // Filtros por seccion (2026-07-29): especialidad + grado/grupo.
                if (especialidadId.HasValue)
                    alumnosMatriculas = FiltrarMatriculasPorEspecialidad(alumnosMatriculas, especialidadId.Value);
                alumnosMatriculas = FiltrarMatriculasPorGrupoGrado(alumnosMatriculas, grupoId, gradoId);

                // Reinicio por corte: restringir la poblacion a alumnos con actividad posterior al corte.
                var _activasPostCorte = ObtenerMatriculasActivasPostCorte();
                if (_activasPostCorte != null)
                    alumnosMatriculas = alumnosMatriculas.Where(m => m != null && _activasPostCorte.Contains(NormalizarMatricula(m))).ToList();

                var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);

                var alumnosInfoDict = alumnosData
                    .GroupBy(a => NormalizarMatricula(a.Matricula), StringComparer.OrdinalIgnoreCase)
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(g => g.Key, g => g.First().IdCarrera, StringComparer.OrdinalIgnoreCase);

                var sexoInfo = ObtenerSexoPorMatricula(alumnosMatriculas);

                int periodoActual = (DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
                int añoActual = DateTime.Now.Year;
                int totalGeneral = alumnosMatriculas.Count;

                var resultado = alumnosMatriculas
                    .GroupBy(m => alumnosInfoDict.ContainsKey(m) ? alumnosInfoDict[m] : 0)
                    .Where(g => g.Key > 0)
                    .Select(g =>
                    {
                        var mats = g.ToList();
                        int h = mats.Count(m => { var s = (sexoInfo.ContainsKey(m) ? sexoInfo[m].Sexo ?? "" : "").Trim().ToUpper(); return s == "H" || s == "HOMBRE" || s == "MASCULINO"; });
                        int mu = mats.Count(m => { var s = (sexoInfo.ContainsKey(m) ? sexoInfo[m].Sexo ?? "" : "").Trim().ToUpper(); return s == "M" || s == "MUJER" || s == "FEMENINO"; });
                        var vuln = CalcularVulnerabilidades(mats, null, null, null, null, añoActual, periodoActual);
                        double pct = totalGeneral > 0 ? Math.Round((double)mats.Count / totalGeneral * 100, 1) : 0;
                        return new
                        {
                            nombre = GetNombreCarrera(g.Key),
                            cantidad = mats.Count,
                            hombres = h,
                            mujeres = mu,
                            econ = vuln.VulnerablesEconomicos,
                            acad = vuln.VulnerablesAcademicos,
                            pers = vuln.VulnerablesPersonales,
                            noVul = vuln.NoVulnerables,
                            sinSeg = vuln.SinSeguimiento,
                            porcentaje = pct
                        };
                    })
                    .OrderByDescending(c => c.cantidad)
                    .ToList();

                return Json(new { success = true, data = resultado, total = totalGeneral });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Método para obtener estadísticas por nivel de estudio (TSU / Ingeniería / Licenciatura) vía AJAX
        [HttpPost]
        public ActionResult GetEstadisticasPorNivelEstudio(bool incluirBajas = false, int? corteId = null, int? especialidadId = null, int? carreraId = null, int? gradoId = null, int? grupoId = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "NivelEstudio");
                    if (hist != null) return hist;
                }
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, error = "Sesión expirada" });

                usuariosDb.Database.CommandTimeout = 300;
                tutoriasDb.Database.CommandTimeout = 300;
                db.Database.CommandTimeout = 300;

                int? idAreaCoordinador = null;
                if (usuario.IdNivel == 3)
                {
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                    if (!idAreaCoordinador.HasValue)
                        return Json(new { success = true, data = new { total = 0, tsu = (object)null, ingenieria = (object)null, licenciatura = (object)null } });
                }

                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();
                if (idAreaCoordinador.HasValue)
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                else if (carreraId.HasValue && usuario.IdNivel == 4)
                {
                    // Filtro de carrera para Master (filtros por seccion 2026-07-29); IDs de AREA como ViewBag.Carreras.
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == carreraId.Value);
                }

                var alumnosMatriculas = alumnosQuery
                    .Select(a => a.Matricula)
                    .ToList()
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                // Filtro por especialidad (direccion 2026-07-16): mismo patron NormalizarSinAcentos que el Resumen.
                if (especialidadId.HasValue)
                {
                    var espNivel = tutoriasDb.Especialidads.Find(especialidadId.Value);
                    if (espNivel != null && !string.IsNullOrEmpty(espNivel.Nombre))
                    {
                        string espNivelNorm = NormalizarSinAcentos(espNivel.Nombre.Trim());
                        var hashNivel = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);
                        var matsEsp = tutoriasDb.DatosPersonales
                            .Where(dp => dp.Matricula != null && dp.Especialidad != null)
                            .Select(dp => new { dp.Matricula, dp.Especialidad })
                            .ToList()
                            .Where(dp => hashNivel.Contains(NormalizarMatricula(dp.Matricula))
                                      && NormalizarSinAcentos(dp.Especialidad.Trim()).Equals(espNivelNorm, StringComparison.OrdinalIgnoreCase))
                            .Select(dp => NormalizarMatricula(dp.Matricula))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var hashEsp = new HashSet<string>(matsEsp, StringComparer.OrdinalIgnoreCase);
                        alumnosMatriculas = alumnosMatriculas.Where(m => hashEsp.Contains(m)).ToList();
                    }
                }

                // Filtros por seccion (2026-07-29): grado (cuatrimestre) y grupo (letra).
                alumnosMatriculas = FiltrarMatriculasPorGrupoGrado(alumnosMatriculas, grupoId, gradoId);

                // Reinicio por corte: restringir la poblacion a alumnos con actividad posterior al corte.
                var _activasPostCorte = ObtenerMatriculasActivasPostCorte();
                if (_activasPostCorte != null)
                    alumnosMatriculas = alumnosMatriculas.Where(m => m != null && _activasPostCorte.Contains(NormalizarMatricula(m))).ToList();

                var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);

                var datosEspecialidad = tutoriasDb.DatosPersonales
                    .Where(d => d.Matricula != null)
                    .Select(d => new { d.Matricula, d.Especialidad })
                    .ToList()
                    .Where(d => alumnosMatriculasHashSet.Contains(NormalizarMatricula(d.Matricula)))
                    .GroupBy(d => NormalizarMatricula(d.Matricula), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Especialidad ?? "", StringComparer.OrdinalIgnoreCase);

                var sexoInfo = ObtenerSexoPorMatricula(alumnosMatriculas);

                var matriculasTSU = new List<string>();
                var matriculasIng = new List<string>();
                var matriculasLic = new List<string>();
                int totalTSU = 0, hTSU = 0, mTSU = 0;
                int totalIng = 0, hIng = 0, mIng = 0;
                int totalLic = 0, hLic = 0, mLic = 0;

                var ci = CultureInfo.InvariantCulture.CompareInfo;

                foreach (var mat in alumnosMatriculas)
                {
                    string esp = datosEspecialidad.ContainsKey(mat) ? datosEspecialidad[mat] : "";
                    string sexo = (sexoInfo.ContainsKey(mat) ? sexoInfo[mat].Sexo ?? "" : "").Trim().ToUpper();
                    bool esH = sexo == "H" || sexo == "HOMBRE" || sexo == "MASCULINO";
                    bool esM = sexo == "M" || sexo == "MUJER" || sexo == "FEMENINO";

                    if (ci.IndexOf(esp, "ingenieria", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    { matriculasIng.Add(mat); totalIng++; if (esH) hIng++; if (esM) mIng++; }
                    else if (ci.IndexOf(esp, "licenciatura", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    { matriculasLic.Add(mat); totalLic++; if (esH) hLic++; if (esM) mLic++; }
                    else
                    { matriculasTSU.Add(mat); totalTSU++; if (esH) hTSU++; if (esM) mTSU++; }
                }

                int periodoActual = (DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
                int añoActual = DateTime.Now.Year;

                var vulnTSU = CalcularVulnerabilidades(matriculasTSU, idAreaCoordinador, null, null, null, añoActual, periodoActual);
                var vulnIng = CalcularVulnerabilidades(matriculasIng, idAreaCoordinador, null, null, null, añoActual, periodoActual);
                var vulnLic = CalcularVulnerabilidades(matriculasLic, idAreaCoordinador, null, null, null, añoActual, periodoActual);

                int total = totalTSU + totalIng + totalLic;

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        total,
                        tsu = new { total = totalTSU, hombres = hTSU, mujeres = mTSU, econ = vulnTSU.VulnerablesEconomicos, acad = vulnTSU.VulnerablesAcademicos, pers = vulnTSU.VulnerablesPersonales, noVul = vulnTSU.NoVulnerables, sinSeg = vulnTSU.SinSeguimiento },
                        ingenieria = new { total = totalIng, hombres = hIng, mujeres = mIng, econ = vulnIng.VulnerablesEconomicos, acad = vulnIng.VulnerablesAcademicos, pers = vulnIng.VulnerablesPersonales, noVul = vulnIng.NoVulnerables, sinSeg = vulnIng.SinSeguimiento },
                        licenciatura = new { total = totalLic, hombres = hLic, mujeres = mLic, econ = vulnLic.VulnerablesEconomicos, acad = vulnLic.VulnerablesAcademicos, pers = vulnLic.VulnerablesPersonales, noVul = vulnLic.NoVulnerables, sinSeg = vulnLic.SinSeguimiento }
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Método auxiliar para obtener matrículas únicas de alumnos usando la lógica de búsqueda (matrícula, nombre, correo)
        // Ignora duplicados cuando un alumno cambió de carrera (toma solo el registro más reciente por fecha)
        // Retorna una lista de matrículas únicas
        private List<string> ObtenerAlumnosUnicos(int? idAreaCoordinador = null)
        {
            try
            {
                // 1. Obtener todos los alumnos de GestionUsuarios (sin filtrar por habilitado)
                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();

                // Si es coordinador, filtrar por IdArea
                if (idAreaCoordinador.HasValue)
                {
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                }

                var alumnosGestionUsuarios = alumnosQuery
                    .Select(a => new
                    {
                        a.Matricula,
                        a.Nombre,
                        a.ApellidoPaterno,
                        a.ApellidoMaterno,
                        a.CorreoElectronico
                    })
                    .ToList();

                // 2. Obtener todos los DatosPersonales de Tutorias y agrupar por identificador único (nombre/correo)
                // Tomar solo el registro más reciente por cada combinación nombre/correo
                var datosPersonalesUnicos = tutoriasDb.DatosPersonales
                    .Select(dp => new
                    {
                        dp.Matricula,
                        dp.Nombre,
                        dp.Email,
                        Fecha = (DateTime?)dp.Fecha
                    })
                    .ToList()
                    .GroupBy(dp => new
                    {
                        Nombre = (dp.Nombre ?? "").Trim().ToUpper(),
                        Correo = (dp.Email ?? "").Trim().ToUpper()
                    })
                    .Where(g => !string.IsNullOrEmpty(g.Key.Nombre) || !string.IsNullOrEmpty(g.Key.Correo))
                    .Select(g => g.OrderByDescending(x => x.Fecha ?? DateTime.MinValue).First())
                    .ToList();

                // 3. Crear diccionarios para búsqueda rápida
                var dpPorMatricula = datosPersonalesUnicos
                    .Where(dp => !string.IsNullOrWhiteSpace(dp.Matricula))
                    .GroupBy(dp => dp.Matricula.Trim().ToUpper())
                    .ToDictionary(g => g.Key, g => g.First());

                var dpPorCorreo = datosPersonalesUnicos
                    .Where(dp => !string.IsNullOrWhiteSpace(dp.Email))
                    .GroupBy(dp => dp.Email.Trim().ToUpper())
                    .ToDictionary(g => g.Key, g => g.First());

                var dpPorNombre = datosPersonalesUnicos
                    .Where(dp => !string.IsNullOrWhiteSpace(dp.Nombre))
                    .GroupBy(dp => dp.Nombre.Trim().ToUpper())
                    .ToDictionary(g => g.Key, g => g.First());

                // 4. Para cada alumno de GestionUsuarios, buscar coincidencias y obtener matrícula única
                var matriculasUnicas = new HashSet<string>();

                foreach (var alumno in alumnosGestionUsuarios)
                {
                    if (string.IsNullOrWhiteSpace(alumno.Matricula))
                        continue;

                    string matriculaNormalizada = alumno.Matricula.Trim().ToUpper();
                    string correoNormalizado = (alumno.CorreoElectronico ?? "").Trim().ToUpper();
                    string nombreCompleto = $"{alumno.Nombre} {alumno.ApellidoPaterno} {alumno.ApellidoMaterno}".Trim().ToUpper();

                    // Buscar coincidencias: primero por matrícula, luego por correo, luego por nombre
                    string matriculaEncontrada = null;

                    // Coincidencia por matrícula (prioridad 1)
                    if (dpPorMatricula.ContainsKey(matriculaNormalizada))
                    {
                        matriculaEncontrada = matriculaNormalizada;
                    }
                    // Coincidencia por correo (prioridad 2)
                    else if (!string.IsNullOrEmpty(correoNormalizado) && dpPorCorreo.ContainsKey(correoNormalizado))
                    {
                        matriculaEncontrada = dpPorCorreo[correoNormalizado].Matricula?.Trim().ToUpper();
                    }
                    // Coincidencia por nombre (prioridad 3)
                    else if (!string.IsNullOrEmpty(nombreCompleto))
                    {
                        // Buscar coincidencias parciales de nombre
                        var coincidenciaNombre = dpPorNombre.FirstOrDefault(kvp =>
                            nombreCompleto.Contains(kvp.Key) || kvp.Key.Contains(nombreCompleto));

                        if (coincidenciaNombre.Key != null)
                        {
                            matriculaEncontrada = coincidenciaNombre.Value.Matricula?.Trim().ToUpper();
                        }
                    }

                    // Si encontramos una matrícula, usarla; si no, usar la matrícula de GestionUsuarios
                    string matriculaFinal = !string.IsNullOrEmpty(matriculaEncontrada) ? matriculaEncontrada : matriculaNormalizada;

                    if (!string.IsNullOrEmpty(matriculaFinal))
                    {
                        matriculasUnicas.Add(matriculaFinal);
                    }
                }

                // 5. Eliminar duplicados: si hay múltiples matrículas que corresponden al mismo alumno (mismo nombre/correo),
                // tomar solo una (la más reciente según DatosPersonales)
                var alumnosPorIdentidad = new Dictionary<string, string>(); // clave: nombre+correo, valor: matrícula

                foreach (var matricula in matriculasUnicas)
                {
                    // Buscar en DatosPersonales por matrícula
                    var dp = datosPersonalesUnicos.FirstOrDefault(d =>
                        (d.Matricula ?? "").Trim().ToUpper() == matricula);

                    if (dp != null)
                    {
                        string nombre = (dp.Nombre ?? "").Trim().ToUpper();
                        string correo = (dp.Email ?? "").Trim().ToUpper();
                        string claveIdentidad = $"{nombre}|{correo}";

                        if (!string.IsNullOrEmpty(nombre) || !string.IsNullOrEmpty(correo))
                        {
                            if (!alumnosPorIdentidad.ContainsKey(claveIdentidad))
                            {
                                alumnosPorIdentidad[claveIdentidad] = matricula;
                            }
                            else
                            {
                                // Ya existe un alumno con esta identidad, verificar cuál es más reciente
                                var dpExistente = datosPersonalesUnicos.FirstOrDefault(d =>
                                    (d.Matricula ?? "").Trim().ToUpper() == alumnosPorIdentidad[claveIdentidad]);

                                if (dpExistente != null && (dp.Fecha ?? DateTime.MinValue) > (dpExistente.Fecha ?? DateTime.MinValue))
                                {
                                    // Este registro es más reciente, reemplazar
                                    alumnosPorIdentidad[claveIdentidad] = matricula;
                                }
                            }
                        }
                        else
                        {
                            // Si no tiene nombre ni correo, agregarlo directamente
                            if (!alumnosPorIdentidad.ContainsKey(claveIdentidad))
                            {
                                alumnosPorIdentidad[claveIdentidad] = matricula;
                            }
                        }
                    }
                    else
                    {
                        // Si no está en DatosPersonales, agregarlo directamente
                        string claveIdentidad = $"|{matricula}";
                        if (!alumnosPorIdentidad.ContainsKey(claveIdentidad))
                        {
                            alumnosPorIdentidad[claveIdentidad] = matricula;
                        }
                    }
                }

                return alumnosPorIdentidad.Values.Distinct().ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR en ObtenerAlumnosUnicos: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return new List<string>();
            }
        }

        // Método para obtener estadísticas por grupo
        // Método auxiliar para mapear IdCarrera de Tutorias.Carreras a IdArea de EstadiasUTTN.Carreras
        // IMPORTANTE: Los coordinadores tienen su IdCarrera en Tutorias.Carreras, pero para filtrar
        // alumnos necesitamos el IdArea de EstadiasUTTN (porque Alumnos.IdCarrera = EstadiasUTTN.IdArea)
        // Usa la consulta SQL proporcionada:
        // SELECT cEstadias.IdArea, cEstadias.Area
        // FROM Tutorias.dbo.Usuarios AS u
        // INNER JOIN Tutorias.dbo.Carreras AS cTutorias ON u.IdCarrera = cTutorias.IdCarrera
        // LEFT JOIN EstadiasUTTN.dbo.Carreras AS cEstadias ON 
        //     UPPER(LTRIM(RTRIM(cTutorias.Nombre))) = UPPER(LTRIM(RTRIM(cEstadias.Area)))
        // WHERE u.IdCarrera = idCarreraTutorias
        private int? MapearIdCarreraCoordinadorAIdArea(int? idCarreraTutorias)
        {
            if (!idCarreraTutorias.HasValue)
                return null;

            try
            {
                System.Diagnostics.Debug.WriteLine($"=== MAPEANDO CARRERA COORDINADOR ===");
                System.Diagnostics.Debug.WriteLine($"IdCarreraTutorias: {idCarreraTutorias.Value}");

                // Intentar primero con SQL directo (método preferido)
                // IMPORTANTE: La tabla en EstadiasUTTN se llama "Carrera" (singular), no "Carreras"
                try
                {
                    string sqlQuery = @"
                        SELECT 
                            cEstadias.IdArea AS IdAreaEstadiasUTTN
                        FROM 
                            Tutorias.dbo.Carreras AS cTutorias
                            LEFT JOIN EstadiasUTTN.dbo.Carrera AS cEstadias ON 
                                UPPER(LTRIM(RTRIM(cTutorias.Nombre))) = UPPER(LTRIM(RTRIM(cEstadias.Area)))
                        WHERE 
                            cTutorias.IdCarrera = {0}";

                    var resultado = tutoriasDb.Database.SqlQuery<int?>(sqlQuery, idCarreraTutorias.Value).FirstOrDefault();

                    if (resultado.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ COINCIDENCIA ENCONTRADA (SQL): IdArea={resultado.Value}");

                        // Verificar que existe en EstadiasUTTN
                        var carreraEstadiasVerificacion = estadiasDb.Carreras.FirstOrDefault(c => c.IdArea == resultado.Value);
                        if (carreraEstadiasVerificacion != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Verificado en EstadiasUTTN: IdArea={resultado.Value}, Area='{carreraEstadiasVerificacion.Area}'");
                            return resultado.Value;
                        }
                    }
                }
                catch (Exception sqlEx)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL directo falló, usando LINQ: {sqlEx.Message}");
                }

                // Método alternativo con LINQ (misma lógica que la consulta SQL)
                var carreraTutorias = tutoriasDb.Carreras.FirstOrDefault(c => c.IdCarrera == idCarreraTutorias.Value);
                if (carreraTutorias == null || string.IsNullOrWhiteSpace(carreraTutorias.Nombre))
                {
                    System.Diagnostics.Debug.WriteLine($"No se encontró carrera en Tutorias con IdCarrera: {idCarreraTutorias.Value}");
                    return null;
                }

                string nombreCarreraTutorias = carreraTutorias.Nombre.Trim();
                string nombreCarreraTutoriasNormalizado = nombreCarreraTutorias.ToUpper().Trim();

                System.Diagnostics.Debug.WriteLine($"Nombre carrera Tutorias: '{nombreCarreraTutorias}'");
                System.Diagnostics.Debug.WriteLine($"Nombre normalizado: '{nombreCarreraTutoriasNormalizado}'");

                // Buscar en EstadiasUTTN usando la misma lógica que la consulta SQL
                // IMPORTANTE: Cargar primero los datos en memoria para poder usar IsNullOrWhiteSpace
                var carrerasEstadias = estadiasDb.Carreras.ToList();
                var carreraEstadias = carrerasEstadias
                    .Where(c => !string.IsNullOrWhiteSpace(c.Area))
                    .FirstOrDefault(c => c.Area.Trim().ToUpper() == nombreCarreraTutoriasNormalizado);

                if (carreraEstadias != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ COINCIDENCIA ENCONTRADA (LINQ): IdArea={carreraEstadias.IdArea}, Area='{carreraEstadias.Area}'");
                    return carreraEstadias.IdArea;
                }

                // Si no se encuentra, listar todas las carreras disponibles para debug
                System.Diagnostics.Debug.WriteLine($"✗ NO SE ENCONTRÓ mapeo para IdCarrera: {idCarreraTutorias.Value}, Nombre: '{nombreCarreraTutorias}'");
                System.Diagnostics.Debug.WriteLine($"Carreras disponibles en EstadiasUTTN (excluyendo VINCULACIÓN y MAESTRÍA):");
                var carrerasDisponibles = carrerasEstadias
                    .Where(c => !string.IsNullOrWhiteSpace(c.Area) &&
                               !c.Area.Trim().ToUpper().StartsWith("VINCULACI") &&
                               !c.Area.Trim().ToUpper().StartsWith("MAESTR"))
                    .Select(c => new { c.IdArea, c.Area })
                    .OrderBy(c => c.IdArea)
                    .ToList();

                foreach (var c in carrerasDisponibles)
                {
                    System.Diagnostics.Debug.WriteLine($"  - IdArea: {c.IdArea} -> Area: '{c.Area}'");
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR al mapear IdCarrera de coordinador a IdArea: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        // Método auxiliar para mapear IdArea de EstadiasUTTN a IdCarrera de Tutorias.Carreras
        // IMPORTANTE: Para "Estadísticas por Grupo", debemos usar IdCarrera de Tutorias.Carreras
        // porque TutoriaGrupals.IdCarrera se refiere a esa tabla
        private int? MapearIdCarreraParaGrupos(int? idAreaEstadias)
        {
            if (!idAreaEstadias.HasValue)
                return null;

            try
            {
                // Primero, obtener el nombre de la carrera desde EstadiasUTTN usando IdArea
                var carreraEstadias = estadiasDb.Carreras.FirstOrDefault(c => c.IdArea == idAreaEstadias.Value);
                if (carreraEstadias == null || string.IsNullOrWhiteSpace(carreraEstadias.Area))
                    return null;

                string nombreCarreraEstadias = NormalizarSinAcentos(carreraEstadias.Area.Trim());

                // Buscar en Tutorias.Carreras por nombre (case-insensitive, sin acentos, búsqueda parcial)
                // Primero intentar coincidencia exacta
                var carreraTutorias = tutoriasDb.Carreras
                    .Where(c => !string.IsNullOrWhiteSpace(c.Nombre))
                    .ToList()
                    .FirstOrDefault(c => NormalizarSinAcentos(c.Nombre.Trim()).Equals(nombreCarreraEstadias, StringComparison.OrdinalIgnoreCase));

                if (carreraTutorias != null)
                {
                    return carreraTutorias.IdCarrera;
                }

                // Si no hay coincidencia exacta, buscar por palabras clave
                // Extraer palabras clave del nombre (eliminar artículos y palabras comunes)
                var palabrasClave = nombreCarreraEstadias
                    .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => p.Length > 2 && !p.Equals("de", StringComparison.OrdinalIgnoreCase)
                                         && !p.Equals("la", StringComparison.OrdinalIgnoreCase)
                                         && !p.Equals("el", StringComparison.OrdinalIgnoreCase)
                                         && !p.Equals("y", StringComparison.OrdinalIgnoreCase)
                                         && !p.Equals("e", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (palabrasClave.Any())
                {
                    // Buscar carreras que contengan todas las palabras clave importantes
                    var carrerasTutorias = tutoriasDb.Carreras
                        .Where(c => !string.IsNullOrWhiteSpace(c.Nombre))
                        .ToList();

                    foreach (var carrera in carrerasTutorias)
                    {
                        string nombreNormalizado = NormalizarSinAcentos(carrera.Nombre.Trim());
                        // Verificar si contiene todas las palabras clave importantes
                        bool contieneTodas = palabrasClave.All(palabra =>
                            nombreNormalizado.IndexOf(palabra, StringComparison.OrdinalIgnoreCase) >= 0);

                        if (contieneTodas)
                        {
                            return carrera.IdCarrera;
                        }
                    }
                }

                // Si no se encuentra por nombre, intentar buscar directamente por IdCarrera (por si acaso coinciden)
                var carreraDirecta = tutoriasDb.Carreras.FirstOrDefault(c => c.IdCarrera == idAreaEstadias.Value);
                if (carreraDirecta != null)
                {
                    return carreraDirecta.IdCarrera;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al mapear IdCarrera para grupos: {ex.Message}");
                return null;
            }
        }

        [HttpPost]
        public ActionResult GetEstadisticasPorGrupo(int? carreraId = null, int? corteId = null, int? turnoId = null, int? especialidadId = null, int? gradoId = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "Grupo");
                    if (hist != null) return hist;
                }
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, error = "Sesión expirada" });

                var estadisticasGrupos = CalcularEstadisticasPorGrupo();

                // Fail-closed nivel 3 (fix 2026-07-16): antes el JSON llevaba TODOS los grupos al
                // navegador y el aislamiento era solo client-side (DataTables). Ahora se filtra aqui.
                if (usuario.IdNivel == 3)
                {
                    estadisticasGrupos = estadisticasGrupos.Where(g => g.IdCarrera == usuario.IdCarrera).ToList();
                }
                else if (usuario.IdNivel == 4 && carreraId.HasValue)
                {
                    int? carreraIdTutorias = MapearIdCarreraParaGrupos(carreraId.Value);
                    if (carreraIdTutorias.HasValue)
                        estadisticasGrupos = estadisticasGrupos.Where(g => g.IdCarrera == carreraIdTutorias.Value).ToList();
                    else
                        estadisticasGrupos = new List<EstadisticaGrupo>();
                }

                // Filtros por seccion (direccion 2026-07-16): turno y especialidad del grupo.
                if (turnoId.HasValue)
                {
                    var turnoCat = tutoriasDb.Turnoes.Find(turnoId.Value);
                    if (turnoCat != null && !string.IsNullOrEmpty(turnoCat.Nombre))
                    {
                        string turnoNorm = NormalizarSinAcentos(turnoCat.Nombre.Trim());
                        estadisticasGrupos = estadisticasGrupos.Where(g => !string.IsNullOrWhiteSpace(g.NombreTurno)
                            && NormalizarSinAcentos(g.NombreTurno.Trim()).Equals(turnoNorm, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }
                if (especialidadId.HasValue)
                {
                    var espGrupo = tutoriasDb.Especialidads.Find(especialidadId.Value);
                    if (espGrupo != null && !string.IsNullOrEmpty(espGrupo.Nombre))
                    {
                        string espGrupoNorm = NormalizarSinAcentos(espGrupo.Nombre.Trim());
                        estadisticasGrupos = estadisticasGrupos.Where(g => !string.IsNullOrWhiteSpace(g.Especialidad)
                            && NormalizarSinAcentos(g.Especialidad.Trim()).Equals(espGrupoNorm, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }

                // Filtro por grado/cuatrimestre (2026-07-29): IdGrado del grupo, mismo espacio que ViewBag.FiltroGrados.
                if (gradoId.HasValue)
                    estadisticasGrupos = estadisticasGrupos.Where(g => g.IdGrado == gradoId.Value).ToList();

                var data = estadisticasGrupos.Select(g => new
                {
                    grupoId = g.GrupoId,
                    carreraId = g.IdCarrera,
                    carreraNombre = g.NombreCarrera,
                    especialidad = g.Especialidad,
                    turno = g.NombreTurno,
                    periodo = g.NombrePeriodo,
                    totalEstudiantes = g.TotalEstudiantes,
                    totalHombres = g.Hombres,
                    totalMujeres = g.Mujeres,
                    bajas = g.Bajas,
                    vulnerablesEconomicos = g.VulnerablesEconomicos,
                    vulnerablesAcademicos = g.VulnerablesAcademicos,
                    vulnerablesPersonales = g.VulnerablesPersonales,
                    noVulnerables = g.NoVulnerables,
                    sinSeguimiento = g.SinSeguimiento
                }).ToList();

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Método para calcular vulnerabilidades por especialidad usando la lógica de consulta_vulnerabilidades_filtrada.sql
        // Este método ahora usa CalcularVulnerabilidades para mantener consistencia
        private EstadisticasVulnerabilidad CalcularVulnerabilidadesPorEspecialidad(List<string> alumnosMatriculas, int? idAreaCoordinador = null, int? idCarrera = null, int? mes = null, int? año = null, int? periodo = null)
        {
            // Usar el método principal que replica la consulta SQL
            return CalcularVulnerabilidades(alumnosMatriculas, idAreaCoordinador, idCarrera, null, mes, año, periodo);
        }

        // Método para calcular vulnerabilidades usando una consulta SQL directa y optimizada
        // Método auxiliar para obtener sexo de alumnos según la lógica de consulta_sexo_alumnos.sql
        // Prioridad 1: DatosPersonales (más reciente por fecha)
        // Prioridad 2: EntrevistaInicials (más reciente por fecha)
        // Solo cuenta como "Sin sexo" si tiene registro en alguna de las dos tablas Y su Sexo es 'No especificado' OR IS NULL
        // IMPORTANTE: Replica exactamente la lógica de la consulta SQL
        private Dictionary<string, SexoInfo> ObtenerSexoPorMatricula(List<string> alumnosMatriculas)
        {
            // IMPORTANTE: Usar comparador que ignore mayúsculas/minúsculas para coincidir con SQL
            var resultado = new Dictionary<string, SexoInfo>(StringComparer.OrdinalIgnoreCase);
            // Normalizar matrículas para comparación exacta (eliminar espacios)
            var alumnosMatriculasNormalizadas = alumnosMatriculas
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct()
                .ToList();
            var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculasNormalizadas, StringComparer.OrdinalIgnoreCase);

            // Obtener sexo desde DatosPersonales (prioridad 1) - más reciente por matrícula
            // Replica: SELECT TOP 1 Sexo FROM DatosPersonales WHERE Matricula = a.Matricula ORDER BY Fecha DESC
            // IMPORTANTE: Cargar primero en memoria para poder usar IsNullOrWhiteSpace, Trim() y Contains() del HashSet
            // Primero cargar todos los datos que podrían coincidir (sin filtrar por matrícula normalizada en SQL)
            var sexoDesdeDP = tutoriasDb.DatosPersonales
                .Where(d => d.Matricula != null)
                .Select(d => new { d.Matricula, d.Sexo, Fecha = (DateTime?)d.Fecha })
                .ToList()
                .Where(d => !string.IsNullOrWhiteSpace(d.Matricula))
                .Select(d => new { Matricula = d.Matricula.Trim(), d.Sexo, d.Fecha })
                .Where(d => alumnosMatriculasHashSet.Contains(d.Matricula))
                .GroupBy(d => d.Matricula, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Fecha ?? DateTime.MinValue).First(),
                    StringComparer.OrdinalIgnoreCase
                );

            // Obtener sexo desde EntrevistaInicials (prioridad 2) - más reciente por matrícula
            // Replica: SELECT TOP 1 Sexo FROM EntrevistaInicials WHERE Matricula = a.Matricula ORDER BY Fecha DESC
            // IMPORTANTE: Cargar primero en memoria para poder usar IsNullOrWhiteSpace, Trim() y Contains() del HashSet
            // Primero cargar todos los datos que podrían coincidir (sin filtrar por matrícula normalizada en SQL)
            var sexoDesdeEI = tutoriasDb.EntrevistaInicials
                .Where(e => e.Matricula != null)
                .Select(e => new { e.Matricula, e.Sexo, Fecha = (DateTime?)e.Fecha })
                .ToList()
                .Where(e => !string.IsNullOrWhiteSpace(e.Matricula))
                .Select(e => new { Matricula = e.Matricula.Trim(), e.Sexo, e.Fecha })
                .Where(e => alumnosMatriculasHashSet.Contains(e.Matricula))
                .GroupBy(e => e.Matricula, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Fecha ?? DateTime.MinValue).First(),
                    StringComparer.OrdinalIgnoreCase
                );

            // Verificar si tiene registro en alguna de las dos tablas (replica EXISTS)
            // IMPORTANTE: Normalizar matrículas para comparación exacta
            var tieneRegistroEnDP = new HashSet<string>(
                sexoDesdeDP.Keys.Select(k => k.Trim()),
                StringComparer.OrdinalIgnoreCase
            );
            var tieneRegistroEnEI = new HashSet<string>(
                sexoDesdeEI.Keys.Select(k => k.Trim()),
                StringComparer.OrdinalIgnoreCase
            );

            // Combinar: prioridad DatosPersonales > EntrevistaInicials (replica COALESCE)
            // IMPORTANTE: Incluir TODOS los alumnos (incluso duplicados), pero el diccionario solo guarda una entrada por matrícula única
            // La consulta SQL cuenta todas las filas, pero el sexo se obtiene una vez por matrícula
            var matriculasUnicas = alumnosMatriculas
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var matricula in matriculasUnicas)
            {
                string matriculaNormalizada = matricula.Trim();
                string sexoFinal = null;

                // Prioridad 1: DatosPersonales (replica COALESCE - primera opción)
                if (sexoDesdeDP.ContainsKey(matriculaNormalizada))
                {
                    // Guardar el valor original (sin normalizar)
                    // La consulta SQL normaliza en cada comparación con LTRIM(RTRIM()), así que normalizamos en el conteo
                    sexoFinal = sexoDesdeDP[matriculaNormalizada].Sexo;
                }
                // Prioridad 2: EntrevistaInicials (solo si no se encontró en DatosPersonales)
                else if (sexoDesdeEI.ContainsKey(matriculaNormalizada))
                {
                    // Guardar el valor original (sin normalizar)
                    // La consulta SQL normaliza en cada comparación con LTRIM(RTRIM()), así que normalizamos en el conteo
                    sexoFinal = sexoDesdeEI[matriculaNormalizada].Sexo;
                }

                // Verificar si tiene registro en alguna de las dos tablas (replica EXISTS)
                // Esto es necesario porque la consulta SQL verifica EXISTS antes de contar como "Sin sexo"
                bool tieneRegistroEnAlguna = tieneRegistroEnDP.Contains(matriculaNormalizada) || tieneRegistroEnEI.Contains(matriculaNormalizada);

                // IMPORTANTE: Guardar con la matrícula normalizada como clave para búsqueda eficiente
                // Pero también crear entradas para todas las variaciones de la matrícula (con/sin espacios)
                // para que coincida con alumnosMatriculas que puede tener espacios
                resultado[matriculaNormalizada] = new SexoInfo
                {
                    Sexo = sexoFinal, // Puede ser null si no tiene registro
                    TieneRegistro = tieneRegistroEnAlguna // TRUE si existe en alguna tabla
                };
            }

            return resultado;
        }

        // Clase auxiliar para información de sexo
        private class SexoInfo
        {
            public string Sexo { get; set; }
            public bool TieneRegistro { get; set; }
        }

        // Método para calcular vulnerabilidades usando LINQ (más eficiente que SQL embebido)
        // Replica EXACTAMENTE la lógica de consulta_vulnerabilidades_por_mes_2025.sql y consulta_vulnerabilidades_por_periodo_2025.sql
        // Parámetros opcionales para filtros: idAreaCoordinador, idCarrera, nivelAcademico, mes, año, periodo
        // IMPORTANTE: mes y periodo NO pueden usarse juntos (validación incluida)
        private EstadisticasVulnerabilidad CalcularVulnerabilidades(List<string> alumnosMatriculas, int? idAreaCoordinador = null, int? idCarrera = null, string nivelAcademico = null, int? mes = null, int? año = null, int? periodo = null, List<AlumnoVulnDetalle> detalleOut = null)
        {
            try
            {
                // DEBUG: Parámetros recibidos
                System.Diagnostics.Debug.WriteLine("=== DEBUG CalcularVulnerabilidades ===");
                System.Diagnostics.Debug.WriteLine($"Parámetros: mes={mes?.ToString() ?? "NULL"}, año={año?.ToString() ?? "NULL"}, periodo={periodo?.ToString() ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"Total alumnosMatriculas: {alumnosMatriculas?.Count ?? 0}");

                // Validación: mes y periodo no pueden usarse juntos
                if (mes.HasValue && periodo.HasValue)
                {
                    throw new ArgumentException("Los filtros de mes y período no pueden usarse simultáneamente.");
                }

                // Calcular año y período a usar
                int añoActual = año ?? DateTime.Now.Year;
                int? periodoActual = periodo;

                System.Diagnostics.Debug.WriteLine($"Año actual usado: {añoActual}");

                // Si se especifica mes, calcular el período correspondiente
                int? periodoDelMes = null;
                if (mes.HasValue)
                {
                    periodoDelMes = (mes.Value >= 1 && mes.Value <= 4) ? 1 : (mes.Value <= 8 ? 2 : 3);
                    periodoActual = periodoDelMes;
                }
                else if (!periodo.HasValue)
                {
                    // Si no se especifica ni mes ni período, usar el período actual
                    periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
                }

                // Determinar el cuatrimestre a filtrar y el rango de meses del período
                string cuatrimestreFiltro = null;
                int mesInicioPeriodo = 0, mesFinPeriodo = 0;
                if (periodoActual.HasValue)
                {
                    switch (periodoActual.Value)
                    {
                        case 1:
                            cuatrimestreFiltro = "ENERO-ABRIL";
                            mesInicioPeriodo = 1; mesFinPeriodo = 4;
                            break;
                        case 2:
                            cuatrimestreFiltro = "MAYO-AGOSTO";
                            mesInicioPeriodo = 5; mesFinPeriodo = 8;
                            break;
                        case 3:
                            cuatrimestreFiltro = "SEPTIEMBRE-DICIEMBRE";
                            mesInicioPeriodo = 9; mesFinPeriodo = 12;
                            break;
                    }
                }

                // Corte vigente: si cae dentro del rango consultado, la vista en vivo solo
                // cuenta registros a partir de el (lo previo se consulta en el historico).
                DateTime? corteDesde = null;
                if (mes.HasValue)
                {
                    var iniMes = new DateTime(añoActual, mes.Value, 1);
                    corteDesde = CorteAplicable(iniMes, iniMes.AddMonths(1).AddTicks(-1));
                }
                else if (mesInicioPeriodo > 0)
                {
                    var iniPer = new DateTime(añoActual, mesInicioPeriodo, 1);
                    var finPer = new DateTime(añoActual, mesFinPeriodo, DateTime.DaysInMonth(añoActual, mesFinPeriodo), 23, 59, 59, 999);
                    corteDesde = CorteAplicable(iniPer, finPer);
                }

                // IMPORTANTE: Usar matrículas únicas para el conteo (igual que la SQL que usa COUNT(*) sobre EstudiantesBase)
                // Pero mantener la lista original para el total de estudiantes
                int totalEstudiantes = alumnosMatriculas.Count;
                var matriculasUnicas = alumnosMatriculas.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var alumnosHashSet = new HashSet<string>(matriculasUnicas, StringComparer.OrdinalIgnoreCase);

                // 1. Obtener TODOS los Individuals del año (sin filtrar por cuatrimestre todavía)
                // IMPORTANTE: Normalizar matrícula para eliminar espacios, NBSP y otros caracteres invisibles
                var todosIndividualsDelAño = tutoriasDb.Individuals
                    .Where(i => i.Matricula != null &&
                               i.Fecha.Year == añoActual)
                    .Select(i => new { i.IdIndividual, i.Matricula, i.Fecha, i.Cuatrimestre })
                    .ToList()
                    .Select(i => new { i.IdIndividual, Matricula = NormalizarMatricula(i.Matricula), i.Fecha, i.Cuatrimestre })
                    .Where(i => !string.IsNullOrEmpty(i.Matricula) && alumnosHashSet.Contains(i.Matricula))
                    .ToList();

                // Filtrar por cuatrimestre del período en memoria (evitar REPLACE/UPPER en SQL)
                // IMPORTANTE: Normalizar el cuatrimestre para manejar diferencias sutiles:
                // - Espacios: "SEPTIEMBRE - DICIEMBRE" vs "SEPTIEMBRE-DICIEMBRE"
                // - Guiones diferentes: hyphen (-) vs en-dash (–) vs em-dash (—)
                var individualsDelPeriodo = todosIndividualsDelAño;
                if (!string.IsNullOrEmpty(cuatrimestreFiltro))
                {
                    individualsDelPeriodo = todosIndividualsDelAño
                        .Where(i => !string.IsNullOrEmpty(i.Cuatrimestre) &&
                                   NormalizarCuatrimestre(i.Cuatrimestre) == cuatrimestreFiltro)
                        .ToList();
                }

                var estudiantesConIndividualEnPeriodo = new HashSet<string>(individualsDelPeriodo.Select(i => i.Matricula).Distinct(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
                var idIndividualsDelPeriodo = new HashSet<int>(individualsDelPeriodo.Select(i => i.IdIndividual));

                // 2. Obtener TODOS los seguimientos del año para esos Individuals del período
                var seguimientosDelAño = tutoriasDb.Seguimientoes
                    .Where(s => idIndividualsDelPeriodo.Contains(s.IdIndividual) &&
                               s.Fecha.Year == añoActual)
                    .Select(s => new { s.IdIndividual, s.Fecha, s.Vulnerabilidad })
                    .ToList();

                // Crear diccionario de IdIndividual -> Matricula (puede haber múltiples Individuals por matrícula)
                var idIndividualAMatricula = individualsDelPeriodo
                    .GroupBy(i => i.IdIndividual)
                    .ToDictionary(g => g.Key, g => g.First().Matricula);

                // Agregar matrícula a cada seguimiento
                var seguimientosConMatricula = seguimientosDelAño
                    .Where(s => idIndividualAMatricula.ContainsKey(s.IdIndividual))
                    .Select(s => new
                    {
                        Matricula = idIndividualAMatricula[s.IdIndividual],
                        s.Fecha,
                        s.Vulnerabilidad
                    })
                    .ToList();

                // FALLBACK (requerimiento dirección): si el tutor NO capturó seguimiento, el alumno
                // conserva la clasificación con la que fue IDENTIFICADO (EntrevistaInicial del período).
                // Sin este fallback, los totales de vulnerables dependían del cumplimiento de los tutores.
                var catalogoVulnerablesFallback = tutoriasDb.Vulnerable
                    .Select(v => new { v.IdEleccionVunerabilidad, v.Nombre })
                    .ToList()
                    .ToDictionary(v => v.IdEleccionVunerabilidad, v => v.Nombre);

                var entrevistasFallbackRaw = tutoriasDb.EntrevistaInicials
                    .Where(e => e.Matricula != null && e.Fecha.Year == añoActual)
                    .Select(e => new { e.Matricula, e.Fecha, e.IdVulnerable, e.IdEleccionVunerabilidad })
                    .ToList();
                if (mesInicioPeriodo > 0)
                    entrevistasFallbackRaw = entrevistasFallbackRaw
                        .Where(e => e.Fecha.Month >= mesInicioPeriodo && e.Fecha.Month <= mesFinPeriodo)
                        .ToList();
                // NOTA: NO se filtra por corteDesde a propósito: la identificación se hace al inicio
                // del cuatrimestre y sigue vigente todo el período, incluso tras un corte manual.

                var entrevistaPorMatricula = entrevistasFallbackRaw
                    .Select(e => new { Matricula = NormalizarMatricula(e.Matricula), e.Fecha, e.IdVulnerable, e.IdEleccionVunerabilidad })
                    .Where(e => !string.IsNullOrEmpty(e.Matricula) && alumnosHashSet.Contains(e.Matricula))
                    .GroupBy(e => e.Matricula, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Fecha).First(), StringComparer.OrdinalIgnoreCase);

                var clasificacionIdentificacion = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvpEnt in entrevistaPorMatricula)
                {
                    var e = kvpEnt.Value;
                    if (e.IdEleccionVunerabilidad == 4 || e.IdVulnerable == 2)
                        clasificacionIdentificacion[kvpEnt.Key] = "No vulnerable";
                    else if (e.IdVulnerable == 1 && e.IdEleccionVunerabilidad > 0 && e.IdEleccionVunerabilidad < 4
                             && catalogoVulnerablesFallback.ContainsKey(e.IdEleccionVunerabilidad))
                        clasificacionIdentificacion[kvpEnt.Key] = catalogoVulnerablesFallback[e.IdEleccionVunerabilidad];
                    // otros valores: no clasificable -> no entra al diccionario
                }
                System.Diagnostics.Debug.WriteLine($"Fallback identificación: {clasificacionIdentificacion.Count} matrículas clasificables desde EntrevistaInicial");

                // 3. Filtrar seguimientos según mes o período y obtener el último por matrícula
                Dictionary<string, string> ultimoSeguimientoPorMatricula;

                if (mes.HasValue)
                {
                    // Determinar si es el primer mes del período (1=Enero, 5=Mayo, 9=Septiembre)
                    bool esPrimerMesDelPeriodo = (mes.Value == 1 || mes.Value == 5 || mes.Value == 9);

                    System.Diagnostics.Debug.WriteLine($"=== FILTRO POR MES ===");
                    System.Diagnostics.Debug.WriteLine($"Mes filtrado: {mes.Value}");
                    System.Diagnostics.Debug.WriteLine($"¿Es primer mes del período? {esPrimerMesDelPeriodo}");

                    if (esPrimerMesDelPeriodo)
                    {
                        System.Diagnostics.Debug.WriteLine(">>> Usando EntrevistaInicials (primer mes del período)");

                        // PRIMER MES DEL PERÍODO: Usar EntrevistaInicials en lugar de Seguimientoes
                        // IMPORTANTE: Obtener vulnerabilidades desde EntrevistaInicials para el mes especificado
                        System.Diagnostics.Debug.WriteLine($"Buscando EntrevistaInicials: Año={añoActual}, Mes={mes.Value}");

                        var entrevistasInicialesRaw = tutoriasDb.EntrevistaInicials
                            .Where(e => e.Matricula != null &&
                                       e.Fecha.Year == añoActual &&
                                       e.Fecha.Month == mes.Value)
                            .Select(e => new
                            {
                                e.Matricula,
                                e.Fecha,
                                e.IdVulnerable,
                                e.IdEleccionVunerabilidad
                            })
                            .ToList();

                        if (corteDesde.HasValue)
                            entrevistasInicialesRaw = entrevistasInicialesRaw.Where(e => e.Fecha >= corteDesde.Value).ToList();

                        System.Diagnostics.Debug.WriteLine($"Total EntrevistaInicials encontradas (antes de normalizar): {entrevistasInicialesRaw.Count}");

                        // DEBUG: Contar registros con IdEleccionVunerabilidad = 4 (No vulnerable)
                        var registrosNoVulnerablesRaw = entrevistasInicialesRaw.Count(e => e.IdEleccionVunerabilidad == 4);
                        System.Diagnostics.Debug.WriteLine($"  Registros con IdEleccionVunerabilidad = 4 (No vulnerable) en raw: {registrosNoVulnerablesRaw}");

                        var entrevistasInicialesNormalizadas = entrevistasInicialesRaw
                            .Select(e => new
                            {
                                Matricula = NormalizarMatricula(e.Matricula),
                                e.Fecha,
                                e.IdVulnerable,
                                e.IdEleccionVunerabilidad
                            })
                            .Where(e => !string.IsNullOrEmpty(e.Matricula))
                            .ToList();

                        System.Diagnostics.Debug.WriteLine($"Total EntrevistaInicials después de normalizar (sin filtrar por HashSet): {entrevistasInicialesNormalizadas.Count}");

                        // DEBUG: Contar cuántas matrículas están en el HashSet
                        var enHashSet = entrevistasInicialesNormalizadas.Where(e => alumnosHashSet.Contains(e.Matricula)).ToList();
                        var noEnHashSet = entrevistasInicialesNormalizadas.Where(e => !alumnosHashSet.Contains(e.Matricula)).ToList();

                        System.Diagnostics.Debug.WriteLine($"  Matrículas que SÍ están en alumnosHashSet: {enHashSet.Count}");
                        System.Diagnostics.Debug.WriteLine($"  Matrículas que NO están en alumnosHashSet: {noEnHashSet.Count}");

                        // DEBUG: Contar registros con IdEleccionVunerabilidad = 4 que están en HashSet
                        var registrosNoVulnerablesEnHashSet = enHashSet.Count(e => e.IdEleccionVunerabilidad == 4);
                        System.Diagnostics.Debug.WriteLine($"  Registros con IdEleccionVunerabilidad = 4 que están en HashSet: {registrosNoVulnerablesEnHashSet}");

                        // DEBUG: Mostrar algunas matrículas que no están en HashSet
                        if (noEnHashSet.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"  Primeras 5 matrículas NO en HashSet:");
                            foreach (var e in noEnHashSet.Take(5))
                            {
                                System.Diagnostics.Debug.WriteLine($"    Matrícula: '{e.Matricula}', IdVulnerable: {e.IdVulnerable}, IdEleccionVunerabilidad: {e.IdEleccionVunerabilidad}");
                            }
                        }

                        var entrevistasInicialesDelMes = enHashSet
                            .GroupBy(e => e.Matricula, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(
                                g => g.Key,
                                g => g.OrderByDescending(e => e.Fecha).First(),
                                StringComparer.OrdinalIgnoreCase
                            );

                        System.Diagnostics.Debug.WriteLine($"Total EntrevistaInicials después de filtrar por alumnosHashSet y agrupar: {entrevistasInicialesDelMes.Count}");

                        // DEBUG: Contar registros con IdEleccionVunerabilidad = 4 después de agrupar
                        var registrosNoVulnerablesAgrupados = entrevistasInicialesDelMes.Values.Count(e => e.IdEleccionVunerabilidad == 4);
                        System.Diagnostics.Debug.WriteLine($"  Registros con IdEleccionVunerabilidad = 4 después de agrupar: {registrosNoVulnerablesAgrupados}");

                        // DEBUG: Mostrar algunas matrículas encontradas
                        if (entrevistasInicialesDelMes.Any())
                        {
                            var primeras5 = entrevistasInicialesDelMes.Take(5);
                            foreach (var kvp in primeras5)
                            {
                                var e = kvp.Value;
                                System.Diagnostics.Debug.WriteLine($"  Matrícula: {kvp.Key}, IdVulnerable: {e.IdVulnerable}, IdEleccionVunerabilidad: {e.IdEleccionVunerabilidad}, Fecha: {e.Fecha:yyyy-MM-dd}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("  ⚠️ NO se encontraron EntrevistaInicials que coincidan con alumnosHashSet");
                            System.Diagnostics.Debug.WriteLine($"  Total alumnosHashSet: {alumnosHashSet.Count}");
                            if (entrevistasInicialesRaw.Any())
                            {
                                var primeras5Raw = entrevistasInicialesRaw.Take(5);
                                System.Diagnostics.Debug.WriteLine("  Primeras 5 matrículas en EntrevistaInicials (sin filtrar):");
                                foreach (var e in primeras5Raw)
                                {
                                    string matNorm = NormalizarMatricula(e.Matricula);
                                    bool estaEnHashSet = alumnosHashSet.Contains(matNorm);
                                    System.Diagnostics.Debug.WriteLine($"    Matrícula original: '{e.Matricula}', Normalizada: '{matNorm}', ¿Está en HashSet? {estaEnHashSet}");
                                }
                            }
                        }

                        // Obtener nombres de vulnerabilidades desde la tabla Vulnerable
                        System.Diagnostics.Debug.WriteLine("Cargando catálogo de vulnerabilidades desde tabla Vulnerable...");
                        var vulnerabilidadesCatalogo = tutoriasDb.Vulnerable
                            .Select(v => new { v.IdEleccionVunerabilidad, v.Nombre })
                            .ToList()
                            .ToDictionary(v => v.IdEleccionVunerabilidad, v => v.Nombre);

                        System.Diagnostics.Debug.WriteLine($"Total vulnerabilidades en catálogo: {vulnerabilidadesCatalogo.Count}");
                        foreach (var kvp in vulnerabilidadesCatalogo)
                        {
                            System.Diagnostics.Debug.WriteLine($"  IdEleccionVunerabilidad: {kvp.Key} -> Nombre: '{kvp.Value}'");
                        }

                        // Construir diccionario de vulnerabilidades por matrícula
                        ultimoSeguimientoPorMatricula = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        int contadorProcesadas = 0;
                        int contadorVulnerables = 0;
                        int contadorNoVulnerables = 0;
                        int contadorSinVulnerabilidad = 0;

                        foreach (var kvp in entrevistasInicialesDelMes)
                        {
                            contadorProcesadas++;
                            var entrevista = kvp.Value;
                            string vulnerabilidadFinal = null;

                            // IMPORTANTE: La lógica correcta es:
                            // - Si IdEleccionVunerabilidad = 4, entonces es "No vulnerable" (independientemente de IdVulnerable)
                            // - Si IdVulnerable = 1 y IdEleccionVunerabilidad = 1, 2 o 3, entonces es vulnerable (Económico, Académico o Personal)
                            // - Si IdVulnerable = 2, entonces es "No vulnerable"

                            int idEleccion = entrevista.IdEleccionVunerabilidad;

                            // Si IdEleccionVunerabilidad = 4, es "No vulnerable"
                            if (idEleccion == 4)
                            {
                                vulnerabilidadFinal = "No vulnerable";
                                contadorNoVulnerables++;
                                if (contadorProcesadas <= 10)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  ✓ Matrícula {kvp.Key}: IdEleccionVunerabilidad=4 -> 'No vulnerable'");
                                }
                            }
                            // Si IdVulnerable = 1 (Si es vulnerable) y tiene IdEleccionVunerabilidad válido (1, 2 o 3)
                            else if (entrevista.IdVulnerable == 1 && idEleccion > 0 && idEleccion < 4 && vulnerabilidadesCatalogo.ContainsKey(idEleccion))
                            {
                                vulnerabilidadFinal = vulnerabilidadesCatalogo[idEleccion];
                                contadorVulnerables++;
                                if (contadorProcesadas <= 10)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  ✓ Matrícula {kvp.Key}: IdVulnerable=1, IdEleccion={idEleccion} -> '{vulnerabilidadFinal}'");
                                }
                            }
                            // Si IdVulnerable = 2 (No es vulnerable)
                            else if (entrevista.IdVulnerable == 2)
                            {
                                vulnerabilidadFinal = "No vulnerable";
                                contadorNoVulnerables++;
                                if (contadorProcesadas <= 10)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  ✓ Matrícula {kvp.Key}: IdVulnerable=2 -> 'No vulnerable'");
                                }
                            }
                            // Si IdVulnerable es 0 o cualquier otro valor, se contará como "Sin seguimiento" (no se agrega al diccionario)
                            else
                            {
                                contadorSinVulnerabilidad++;
                                if (contadorProcesadas <= 10)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  ⚠️ Matrícula {kvp.Key}: IdVulnerable={entrevista.IdVulnerable}, IdEleccionVunerabilidad={idEleccion} (valor no esperado) -> Sin seguimiento");
                                }
                            }

                            // Solo agregar al diccionario si tiene vulnerabilidad definida
                            if (!string.IsNullOrEmpty(vulnerabilidadFinal))
                            {
                                ultimoSeguimientoPorMatricula[kvp.Key] = vulnerabilidadFinal;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"Resumen procesamiento EntrevistaInicials:");
                        System.Diagnostics.Debug.WriteLine($"  Total procesadas: {contadorProcesadas}");
                        System.Diagnostics.Debug.WriteLine($"  Vulnerables (agregadas al diccionario): {contadorVulnerables}");
                        System.Diagnostics.Debug.WriteLine($"  No vulnerables (agregadas al diccionario): {contadorNoVulnerables}");
                        System.Diagnostics.Debug.WriteLine($"  Sin vulnerabilidad definida (NO agregadas): {contadorSinVulnerabilidad}");
                        System.Diagnostics.Debug.WriteLine($"  Total en diccionario final: {ultimoSeguimientoPorMatricula.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(">>> Usando Seguimientoes (resto de meses)");
                        // RESTO DE MESES: Usar Seguimientoes (lógica original)
                        // FILTRO POR MES: Igual que consulta_vulnerabilidades_por_mes_2025.sql
                        // El seguimiento debe ser del mes especificado Y del año actual
                        // El Individual ya está filtrado por cuatrimestre del período correspondiente al mes
                        System.Diagnostics.Debug.WriteLine($"Total seguimientosConMatricula antes de filtrar por mes: {seguimientosConMatricula.Count}");
                        ultimoSeguimientoPorMatricula = seguimientosConMatricula
                            .Where(s => s.Fecha.Year == añoActual && s.Fecha.Month == mes.Value
                                        && (corteDesde == null || s.Fecha >= corteDesde.Value))
                            .GroupBy(s => s.Matricula, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(
                                g => g.Key,
                                g => g.OrderByDescending(s => s.Fecha).First().Vulnerabilidad ?? "",
                                StringComparer.OrdinalIgnoreCase
                            );
                        System.Diagnostics.Debug.WriteLine($"Total seguimientos después de filtrar por mes {mes.Value}: {ultimoSeguimientoPorMatricula.Count}");
                    }
                }
                else if (periodoActual.HasValue)
                {
                    // FILTRO POR PERÍODO: Igual que consulta_vulnerabilidades_por_periodo_2025.sql
                    // Primero filtrar seguimientos que estén dentro del rango de meses del período
                    var seguimientosDelPeriodo = seguimientosConMatricula
                        .Where(s => s.Fecha.Year == añoActual &&
                                   s.Fecha.Month >= mesInicioPeriodo &&
                                   s.Fecha.Month <= mesFinPeriodo &&
                                   (corteDesde == null || s.Fecha >= corteDesde.Value))
                        .ToList();

                    if (seguimientosDelPeriodo.Any())
                    {
                        // Encontrar el ÚLTIMO MES con registros dentro del período
                        int ultimoMesConRegistros = seguimientosDelPeriodo.Max(s => s.Fecha.Month);

                        // Obtener el último seguimiento por matrícula de ese mes
                        ultimoSeguimientoPorMatricula = seguimientosDelPeriodo
                            .Where(s => s.Fecha.Month == ultimoMesConRegistros)
                            .GroupBy(s => s.Matricula, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(
                                g => g.Key,
                                g => g.OrderByDescending(s => s.Fecha).First().Vulnerabilidad ?? "",
                                StringComparer.OrdinalIgnoreCase
                            );
                    }
                    else
                    {
                        ultimoSeguimientoPorMatricula = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    // Sin filtro de mes ni período, obtener el último seguimiento del año
                    ultimoSeguimientoPorMatricula = seguimientosConMatricula
                        .Where(s => s.Fecha.Year == añoActual
                                    && (corteDesde == null || s.Fecha >= corteDesde.Value))
                        .GroupBy(s => s.Matricula, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(s => s.Fecha).First().Vulnerabilidad ?? "",
                            StringComparer.OrdinalIgnoreCase
                        );
                }

                // 4. Clasificar vulnerabilidades (igual que la SQL)
                // IMPORTANTE: La SQL clasifica CADA FILA según su vulnerabilidad
                // Si hay matrículas duplicadas, cada una se clasifica igual (usando el mismo resultado del diccionario)
                System.Diagnostics.Debug.WriteLine($"=== CLASIFICACIÓN DE VULNERABILIDADES ===");
                System.Diagnostics.Debug.WriteLine($"Total matrículas únicas a clasificar: {matriculasUnicas.Count}");
                System.Diagnostics.Debug.WriteLine($"Total registros en ultimoSeguimientoPorMatricula: {ultimoSeguimientoPorMatricula.Count}");

                int vulnerablesEconomicos = 0;
                int vulnerablesAcademicos = 0;
                int vulnerablesPersonales = 0;
                int noVulnerables = 0;
                int sinSeguimiento = 0;

                // Cascada por matrícula: (1) seguimiento del período -> (2) entrevista inicial
                // (identificación, tutor sin seguimiento) -> (3) sin información.
                var clasificacionPorMatricula = new Dictionary<string, (bool esEcon, bool esAcad, bool esPers, bool esNoVul, bool esSinInfo, bool porIdentificacion)>(StringComparer.OrdinalIgnoreCase);

                Func<string, (bool, bool, bool, bool)> clasificarTexto = (vulnerabilidad) =>
                {
                    bool econ = !string.IsNullOrEmpty(vulnerabilidad) &&
                                (vulnerabilidad.IndexOf("ECON", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 vulnerabilidad.IndexOf("ECONÓ", StringComparison.OrdinalIgnoreCase) >= 0);
                    bool acad = !string.IsNullOrEmpty(vulnerabilidad) &&
                                vulnerabilidad.IndexOf("ACAD", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool pers = !string.IsNullOrEmpty(vulnerabilidad) &&
                                vulnerabilidad.IndexOf("PERS", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool noVul = !econ && !acad && !pers &&
                                 !string.IsNullOrEmpty(vulnerabilidad) && vulnerabilidad.Trim() != "";
                    return (econ, acad, pers, noVul);
                };

                bool esPrimerMesDelPeriodoClasif = mes.HasValue && (mes.Value == 1 || mes.Value == 5 || mes.Value == 9);

                foreach (var matriculaUnica in matriculasUnicas)
                {
                    bool tieneIndividualEnPeriodo = estudiantesConIndividualEnPeriodo.Contains(matriculaUnica);
                    bool tieneSeguimiento = ultimoSeguimientoPorMatricula.ContainsKey(matriculaUnica);
                    // Fuera del primer mes, el seguimiento solo cuenta si además tiene Individual del período.
                    bool seguimientoValido = tieneSeguimiento && (esPrimerMesDelPeriodoClasif || tieneIndividualEnPeriodo);

                    if (seguimientoValido)
                    {
                        var (econ, acad, pers, noVul) = clasificarTexto(ultimoSeguimientoPorMatricula[matriculaUnica]);
                        if (econ || acad || pers || noVul)
                        {
                            // OJO: en el primer mes ultimoSeguimientoPorMatricula viene de EntrevistaInicials;
                            // aun así se cuenta como fuente directa (es la captura del período).
                            clasificacionPorMatricula[matriculaUnica] = (econ, acad, pers, noVul, false, false);
                            continue;
                        }
                        // seguimiento existe pero con texto vacío/no clasificable -> intentar fallback
                    }

                    // FALLBACK: identificación (entrevista inicial)
                    if (clasificacionIdentificacion.ContainsKey(matriculaUnica))
                    {
                        var (econ, acad, pers, noVul) = clasificarTexto(clasificacionIdentificacion[matriculaUnica]);
                        if (econ || acad || pers || noVul)
                        {
                            clasificacionPorMatricula[matriculaUnica] = (econ, acad, pers, noVul, false, true);
                            continue;
                        }
                    }

                    // Sin seguimiento válido y sin entrevista clasificable
                    clasificacionPorMatricula[matriculaUnica] = (false, false, false, false, true, false);
                }

                // Contar TODAS las filas (incluyendo duplicados), como la SQL original
                int clasificadosPorSeguimiento = 0;
                int clasificadosPorIdentificacion = 0;
                foreach (var matricula in alumnosMatriculas)
                {
                    if (!clasificacionPorMatricula.ContainsKey(matricula))
                    {
                        sinSeguimiento++;
                        if (detalleOut != null)
                            detalleOut.Add(new AlumnoVulnDetalle { Matricula = matricula, SinInfo = true });
                        continue;
                    }

                    var (esEcon, esAcad, esPers, esNoVul, esSinInfo, porIdentificacion) = clasificacionPorMatricula[matricula];

                    if (detalleOut != null)
                        detalleOut.Add(new AlumnoVulnDetalle { Matricula = matricula, Econ = esEcon, Acad = esAcad, Pers = esPers, NoVul = esNoVul, SinInfo = esSinInfo, PorIdentificacion = porIdentificacion });

                    if (esSinInfo)
                    {
                        sinSeguimiento++;
                    }
                    else
                    {
                        if (esEcon) vulnerablesEconomicos++;
                        if (esAcad) vulnerablesAcademicos++;
                        if (esPers) vulnerablesPersonales++;
                        if (esNoVul) noVulnerables++;
                        if (porIdentificacion) clasificadosPorIdentificacion++;
                        else clasificadosPorSeguimiento++;
                    }
                }

                int totalVulnerables = vulnerablesEconomicos + vulnerablesAcademicos + vulnerablesPersonales;

                System.Diagnostics.Debug.WriteLine($"=== RESULTADOS FINALES ===");
                System.Diagnostics.Debug.WriteLine($"Total Estudiantes: {totalEstudiantes}");
                System.Diagnostics.Debug.WriteLine($"Vulnerables Económicos: {vulnerablesEconomicos}");
                System.Diagnostics.Debug.WriteLine($"Vulnerables Académicos: {vulnerablesAcademicos}");
                System.Diagnostics.Debug.WriteLine($"Vulnerables Personales: {vulnerablesPersonales}");
                System.Diagnostics.Debug.WriteLine($"Total Vulnerables: {totalVulnerables}");
                System.Diagnostics.Debug.WriteLine($"No Vulnerables: {noVulnerables}");
                System.Diagnostics.Debug.WriteLine($"Sin Seguimiento: {sinSeguimiento}");
                System.Diagnostics.Debug.WriteLine("=== FIN DEBUG CalcularVulnerabilidades ===");

                return new EstadisticasVulnerabilidad
                {
                    TotalEstudiantes = totalEstudiantes,
                    VulnerablesEconomicos = vulnerablesEconomicos,
                    VulnerablesAcademicos = vulnerablesAcademicos,
                    VulnerablesPersonales = vulnerablesPersonales,
                    TotalVulnerables = totalVulnerables,
                    NoVulnerables = noVulnerables,
                    SinSeguimiento = sinSeguimiento,
                    SinInformacion = sinSeguimiento,
                    ClasificadosPorSeguimiento = clasificadosPorSeguimiento,
                    ClasificadosPorIdentificacion = clasificadosPorIdentificacion
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== ERROR en CalcularVulnerabilidades ===");
                System.Diagnostics.Debug.WriteLine($"Mensaje: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Tipo de excepción: {ex.GetType().FullName}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Excepción interna: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Tipo excepción interna: {ex.InnerException.GetType().FullName}");
                }
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("=== FIN ERROR ===");

                // Retornar valores por defecto en caso de error
                return new EstadisticasVulnerabilidad
                {
                    TotalEstudiantes = alumnosMatriculas.Count,
                    VulnerablesEconomicos = 0,
                    VulnerablesAcademicos = 0,
                    VulnerablesPersonales = 0,
                    TotalVulnerables = 0,
                    NoVulnerables = 0,
                    SinSeguimiento = alumnosMatriculas.Count,
                    SinInformacion = alumnosMatriculas.Count,
                    ClasificadosPorSeguimiento = 0,
                    ClasificadosPorIdentificacion = 0
                };
            }
        }

        // Método para calcular estadísticas por grupo
        private List<EstadisticaGrupo> CalcularEstadisticasPorGrupo()
        {
            try
            {
                // Obtener año y período actuales
                int añoActual = DateTime.Now.Year;
                int periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);

                // Obtener grupos desde TutoriaGrupals
                var grupos = tutoriasDb.TutoriaGrupals
                    .Where(tg => tg.Año == añoActual && tg.IdPeriodo == periodoActual)
                    .Select(tg => new {
                        tg.Año,
                        tg.IdPeriodo,
                        tg.IdCarrera,
                        tg.IdGrado,
                        tg.IdGrupo,
                        tg.IdTurno
                    })
                    .ToList();

                // Obtener lista de alumnos habilitados
                // IMPORTANTE: Usar NormalizarMatricula para eliminar NBSP y otros caracteres invisibles
                var alumnosMatriculasRaw = usuariosDb.Alumnos
                    .Where(a => a.Habilitado == true)
                    .Select(a => a.Matricula)
                    .ToList();
                var alumnosMatriculas = alumnosMatriculasRaw
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Reinicio por corte: restringir la poblacion a alumnos con actividad posterior al corte.
                var _activasPostCorte = ObtenerMatriculasActivasPostCorte();
                if (_activasPostCorte != null)
                    alumnosMatriculas = alumnosMatriculas.Where(m => m != null && _activasPostCorte.Contains(NormalizarMatricula(m))).ToList();

                var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);

                var estadisticasGrupos = new List<EstadisticaGrupo>();

                foreach (var grupo in grupos)
                {
                    // Obtener alumnos del grupo desde DatosPersonales
                    var alumnosGrupo = tutoriasDb.DatosPersonales
                        .Where(dp => dp.IdCarrera == grupo.IdCarrera &&
                                    dp.IdGrado == grupo.IdGrado &&
                                    dp.IdGrupo == grupo.IdGrupo &&
                                    dp.IdTurno == grupo.IdTurno &&
                                    dp.IdPeriodo == grupo.IdPeriodo &&
                                    dp.Año == grupo.Año)
                        .Select(dp => new { dp.Matricula, dp.Especialidad, dp.Sexo, dp.IdPersona })
                        .ToList();

                    // Obtener la especialidad más común del grupo
                    var especialidadGrupo = alumnosGrupo
                        .Where(x => !string.IsNullOrEmpty(x.Especialidad))
                        .GroupBy(x => x.Especialidad)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault() ?? "Sin especificar";

                    // Filtrar solo alumnos habilitados para estadísticas
                    // IMPORTANTE: Normalizar matrículas para comparación correcta
                    var matriculasGrupo = alumnosGrupo
                        .Where(x => x.Matricula != null && alumnosMatriculasHashSet.Contains(NormalizarMatricula(x.Matricula)))
                        .Select(x => NormalizarMatricula(x.Matricula))
                        .ToList();

                    // Obtener IdPersona de los alumnos del grupo (habilitados) para calcular bajas
                    var idPersonasGrupo = alumnosGrupo
                        .Where(x => x.Matricula != null && alumnosMatriculasHashSet.Contains(NormalizarMatricula(x.Matricula)))
                        .Select(x => x.IdPersona)
                        .Distinct()
                        .ToList();

                    // Calcular bajas del grupo
                    int bajasGrupo = 0;
                    if (idPersonasGrupo.Any())
                    {
                        // Verificar si existe la columna 'Activo' en la tabla Bajas
                        bool tieneColActivo = db.Database
                            .SqlQuery<int>(
                                "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Bajas') AND name = 'Activo'")
                            .FirstOrDefault() > 0;

                        if (tieneColActivo)
                        {
                            // Si existe la columna Activo, filtrar solo las bajas activas (Activo = 1)
                            // Como la columna puede no estar en el modelo, usamos SQL directo pero validado
                            if (idPersonasGrupo.Count <= 1000) // Límite razonable para IN clause
                            {
                                string idsList = string.Join(",", idPersonasGrupo);
                                bajasGrupo = db.Database.SqlQuery<int>(
                                    $"SELECT COUNT(DISTINCT IdPersona) FROM dbo.Bajas WHERE IdPersona IN ({idsList}) AND Activo = 1"
                                ).FirstOrDefault();
                            }
                            else
                            {
                                // Para listas grandes, dividir en lotes
                                int batchSize = 1000;
                                var distinctIds = new HashSet<int>();
                                for (int i = 0; i < idPersonasGrupo.Count; i += batchSize)
                                {
                                    var batch = idPersonasGrupo.Skip(i).Take(batchSize).ToList();
                                    string idsList = string.Join(",", batch);
                                    var batchCount = db.Database.SqlQuery<int>(
                                        $"SELECT DISTINCT IdPersona FROM dbo.Bajas WHERE IdPersona IN ({idsList}) AND Activo = 1"
                                    ).ToList();
                                    foreach (var id in batchCount)
                                    {
                                        distinctIds.Add(id);
                                    }
                                }
                                bajasGrupo = distinctIds.Count;
                            }
                        }
                        else
                        {
                            // Si no existe la columna Activo, contar todas las bajas
                            bajasGrupo = db.Bajas
                                .Where(b => idPersonasGrupo.Contains(b.IdPersona))
                                .Select(b => b.IdPersona)
                                .Distinct()
                                .Count();
                        }
                    }

                    // Calcular hombres y mujeres por grupo (solo alumnos únicos)
                    var alumnosGrupoUnicos = alumnosGrupo
                        .Where(x => x.Matricula != null && alumnosMatriculasHashSet.Contains(NormalizarMatricula(x.Matricula)))
                        .ToList();

                    int hombres = alumnosGrupoUnicos.Count(x => !string.IsNullOrEmpty(x.Sexo) &&
                                                          (x.Sexo.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                                                           x.Sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                                                           x.Sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase)));
                    int mujeres = alumnosGrupoUnicos.Count(x => !string.IsNullOrEmpty(x.Sexo) &&
                                                          (x.Sexo.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                                                           x.Sexo.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                                                           x.Sexo.Equals("Femenino", StringComparison.OrdinalIgnoreCase)));

                    // IMPORTANTE: Usar CalcularVulnerabilidades para mantener consistencia con la sección Vulnerabilidad
                    // Esto usa la misma lógica de normalización de matrículas, filtrado por período y último mes con registros
                    var vulnerabilidades = CalcularVulnerabilidades(matriculasGrupo, null, null, null, null, añoActual, periodoActual);

                    int vulnerablesEconomicos = vulnerabilidades.VulnerablesEconomicos;
                    int vulnerablesAcademicos = vulnerabilidades.VulnerablesAcademicos;
                    int vulnerablesPersonales = vulnerabilidades.VulnerablesPersonales;
                    int sinSeguimiento = vulnerabilidades.SinSeguimiento;
                    int noVulnerables = vulnerabilidades.NoVulnerables;

                    estadisticasGrupos.Add(new EstadisticaGrupo
                    {
                        Año = grupo.Año,
                        IdPeriodo = grupo.IdPeriodo,
                        IdCarrera = grupo.IdCarrera,
                        IdGrado = grupo.IdGrado,
                        IdGrupo = grupo.IdGrupo,
                        IdTurno = grupo.IdTurno,
                        TotalEstudiantes = matriculasGrupo.Count,
                        Hombres = hombres,
                        Mujeres = mujeres,
                        Bajas = bajasGrupo,
                        VulnerablesEconomicos = vulnerablesEconomicos,
                        VulnerablesAcademicos = vulnerablesAcademicos,
                        VulnerablesPersonales = vulnerablesPersonales,
                        SinSeguimiento = sinSeguimiento,
                        NoVulnerables = noVulnerables,
                        NombreCarrera = GetNombreCarreraSimple(grupo.IdCarrera),
                        NombreTurno = GetNombreTurno(grupo.IdTurno),
                        NombrePeriodo = GetNombrePeriodo(grupo.IdPeriodo),
                        Especialidad = especialidadGrupo
                    });
                }

                return estadisticasGrupos.OrderBy(g => g.IdCarrera).ThenBy(g => g.IdGrado).ThenBy(g => g.IdGrupo).ToList();
            }
            catch
            {
                return new List<EstadisticaGrupo>();
            }
        }

        // Método para obtener estudiantes sin sexo especificado
        [HttpPost]
        public JsonResult GetEstudiantesSinSexo(int? especialidadId = null, int? carreraId = null)
        {
            try
            {
                // Obtener usuario de la sesión
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, error = "Sesión expirada" });
                }

                // Obtener TODOS los alumnos (habilitados y no habilitados) para resumen detallado
                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();

                // Si es coordinador (nivel 3), mapear su IdCarrera (de Tutorias) a IdArea (de EstadiasUTTN)
                // Usar la consulta SQL proporcionada para obtener el IdAreaEstadiasUTTN
                if (usuario.IdNivel == 3)
                {
                    int? idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                    if (idAreaCoordinador.HasValue)
                    {
                        alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                    }
                    else
                    {
                        // Si no se puede mapear, no mostrar ningún alumno
                        alumnosQuery = alumnosQuery.Where(a => false);
                    }
                }
                // Si se especifica una carrera (solo para Master), filtrar por ella
                else if (carreraId.HasValue)
                {
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == carreraId.Value);
                }

                var alumnosMatriculas = alumnosQuery.Select(a => a.Matricula).ToList();
                var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas);

                // Si se especifica una especialidad, filtrar por ella
                if (especialidadId.HasValue)
                {
                    var especialidad = tutoriasDb.Especialidads.Find(especialidadId.Value);
                    if (especialidad != null && !string.IsNullOrEmpty(especialidad.Nombre))
                    {
                        string nombreEspecialidadNormalizado = NormalizarSinAcentos(especialidad.Nombre.Trim());
                        var datosPersonales = tutoriasDb.DatosPersonales
                            .Where(dp => alumnosMatriculasHashSet.Contains(dp.Matricula) && dp.Especialidad != null)
                            .Select(dp => new { dp.Matricula, dp.Especialidad })
                    .ToList();

                        alumnosMatriculas = datosPersonales
                            .Where(dp => NormalizarSinAcentos(dp.Especialidad.Trim()).Equals(nombreEspecialidadNormalizado, StringComparison.OrdinalIgnoreCase))
                            .Select(dp => dp.Matricula)
                            .Distinct()
                            .ToList();
                        alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas);
                    }
                }

                // Obtener información de carrera desde Alumnos para todos los estudiantes
                var alumnosInfo = usuariosDb.Alumnos
                    .Where(a => alumnosMatriculasHashSet.Contains(a.Matricula))
                    .Select(a => new { a.Matricula, a.IdCarrera })
                    .ToList()
                    .GroupBy(a => a.Matricula)
                    .ToDictionary(g => g.Key, g => g.First());

                // Buscar estudiantes sin sexo usando la consulta SQL proporcionada:
                // WHERE Sexo = 'No especificado' OR Sexo IS NULL
                // Obtener el registro MÁS RECIENTE por matrícula para cada estudiante sin sexo
                var estudiantesSinSexoQuery = tutoriasDb.DatosPersonales
                    .Where(d => alumnosMatriculasHashSet.Contains(d.Matricula) &&
                               (d.Sexo == null || d.Sexo.Trim() == "" || d.Sexo.Trim().Equals("No especificado", StringComparison.OrdinalIgnoreCase)))
                    .Select(d => new { d.Matricula, d.Nombre, d.IdCarrera, d.IdGrado, d.IdGrupo, d.Fecha, d.Sexo })
                    .ToList()
                    .GroupBy(x => x.Matricula)
                    .Select(g => g.OrderByDescending(x => x.Fecha).First())
                    .ToList();

                var estudiantesSinSexoList = new List<EstudianteSinSexoInfo>();

                foreach (var estudiante in estudiantesSinSexoQuery)
                {
                    // Obtener información de carrera (prioridad: Alumnos > DatosPersonales)
                    // IMPORTANTE: Usar IdCarrera de Alumnos para que coincida con el filtrado
                    int idCarrera = 0;
                    string nombre = "Sin nombre";
                    int idGrado = 0;
                    int idGrupo = 0;

                    if (!string.IsNullOrWhiteSpace(estudiante.Nombre))
                    {
                        nombre = estudiante.Nombre.Trim();
                    }

                    // Obtener IdCarrera de Alumnos (prioridad) para que coincida con el filtrado
                    if (alumnosInfo.ContainsKey(estudiante.Matricula))
                    {
                        idCarrera = alumnosInfo[estudiante.Matricula].IdCarrera;
                    }
                    // Si no está en Alumnos, usar el de DatosPersonales
                    else if (estudiante.IdCarrera > 0)
                    {
                        idCarrera = estudiante.IdCarrera;
                    }

                    if (estudiante.IdGrado > 0)
                    {
                        idGrado = estudiante.IdGrado;
                    }
                    if (estudiante.IdGrupo > 0)
                    {
                        idGrupo = estudiante.IdGrupo;
                    }

                    estudiantesSinSexoList.Add(new EstudianteSinSexoInfo
                    {
                        Matricula = estudiante.Matricula,
                        Nombre = nombre,
                        IdCarrera = idCarrera,
                        IdGrado = idGrado,
                        IdGrupo = idGrupo
                    });
                }

                // Obtener información de carrera usando GetNombreCarreraSimple para evitar confusión entre IdCarrera e IdArea
                // GetNombreCarreraSimple tiene un mapeo directo y confiable de IdCarrera a nombre de carrera
                var resultado = estudiantesSinSexoList.Select(e => new
                {
                    nombre = e.Nombre ?? "Sin nombre",
                    matricula = e.Matricula,
                    carrera = e.IdCarrera > 0 ? GetNombreCarreraSimple(e.IdCarrera) : "Sin carrera",
                    grupo = e.IdGrado > 0 ? $"{e.IdGrado}{GetLetraGrupo(e.IdGrupo)}" : GetLetraGrupo(e.IdGrupo)
                }).OrderBy(e => e.nombre).ToList();

                return Json(new { success = true, data = resultado });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Misma lógica que alimenta el resumen detallado en pantalla (GetEstadisticasPorEspecialidad) y el Excel de ese bloque.
        /// </summary>
        private sealed class ResumenDetalladoDatos
        {
            public bool Ok { get; set; }
            public string Error { get; set; }
            public int TotalEstudiantes { get; set; }
            public int TotalHombres { get; set; }
            public int TotalMujeres { get; set; }
            public int TotalSinSexo { get; set; }
            public int Embarazadas { get; set; }
            public int Madres { get; set; }
            public int Padres { get; set; }
            public int AlumnosTrabajando { get; set; }
            public EstadisticasVulnerabilidad Vulnerabilidades { get; set; }
            public string NombreEspecialidad { get; set; }
            public string NombreCarrera { get; set; }
        }

        // Filtro por especialidad del alumno (texto libre dp.Especialidad vs catalogo Especialidads),
        // patron NormalizarSinAcentos ya usado en Resumen/Nivel/Materias. Extraido 2026-07-29.
        private List<string> FiltrarMatriculasPorEspecialidad(List<string> matriculas, int especialidadId)
        {
            var esp = tutoriasDb.Especialidads.Find(especialidadId);
            if (esp == null || string.IsNullOrEmpty(esp.Nombre)) return matriculas;
            string espNorm = NormalizarSinAcentos(esp.Nombre.Trim());
            var hash = new HashSet<string>(matriculas, StringComparer.OrdinalIgnoreCase);
            var matsEsp = tutoriasDb.DatosPersonales
                .Where(dp => dp.Matricula != null && dp.Especialidad != null)
                .Select(dp => new { dp.Matricula, dp.Especialidad })
                .ToList()
                .Where(dp => hash.Contains(NormalizarMatricula(dp.Matricula))
                          && NormalizarSinAcentos(dp.Especialidad.Trim()).Equals(espNorm, StringComparison.OrdinalIgnoreCase))
                .Select(dp => NormalizarMatricula(dp.Matricula))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var hashEsp = new HashSet<string>(matsEsp, StringComparer.OrdinalIgnoreCase);
            return matriculas.Where(m => hashEsp.Contains(m)).ToList();
        }

        // Filtro por grupo (letra) y/o grado (cuatrimestre) sobre el registro MAS RECIENTE de
        // DatosPersonales de cada matricula. Misma logica que ObtenerPoblacionResumen (~L3726).
        private List<string> FiltrarMatriculasPorGrupoGrado(List<string> matriculas, int? grupoId, int? gradoId)
        {
            if (!grupoId.HasValue && !gradoId.HasValue) return matriculas;
            var hashPobl = new HashSet<string>(matriculas, StringComparer.OrdinalIgnoreCase);
            var dpGrupo = tutoriasDb.DatosPersonales
                .Where(dp => dp.Matricula != null)
                .Select(dp => new { dp.Matricula, dp.IdGrupo, dp.IdGrado, Fecha = (DateTime?)dp.Fecha })
                .ToList()
                .Where(dp => !string.IsNullOrWhiteSpace(dp.Matricula) && hashPobl.Contains(dp.Matricula.Trim()))
                .GroupBy(dp => dp.Matricula.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Fecha ?? DateTime.MinValue).First())
                .Where(dp => (!grupoId.HasValue || dp.IdGrupo == grupoId.Value)
                          && (!gradoId.HasValue || dp.IdGrado == gradoId.Value))
                .Select(dp => dp.Matricula.Trim())
                .ToList();
            var hashGrupo = new HashSet<string>(dpGrupo, StringComparer.OrdinalIgnoreCase);
            return matriculas.Where(m => hashGrupo.Contains(m)).ToList();
        }

        // Poblacion base del Resumen Detallado (padron filtrado por rol/carrera/bajas/especialidad/post-corte).
        // Extraido de CalcularResumenDetalladoDatos para que GetAlumnosVulnerabilidad use LA MISMA poblacion.
        // grupoId/gradoId (filtros direccion 2026-07-16): acotan por DatosPersonales.IdGrupo/IdGrado
        // (registro mas reciente por matricula; cobertura 100% en activos — ver filtros-task0-hallazgo.md).
        private List<string> ObtenerPoblacionResumen(Usuario usuario, int? especialidadId, int? carreraId, bool incluirBajas, int? grupoId = null, int? gradoId = null, string sexo = null)
        {
            // Obtener TODOS los alumnos (habilitados y no habilitados) para resumen detallado
            var alumnosQuery = usuariosDb.Alumnos.AsQueryable();

            if (usuario.IdNivel == 3)
            {
                int? idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                if (idAreaCoordinador.HasValue)
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                else
                    alumnosQuery = alumnosQuery.Where(a => false);
            }
            else if (carreraId.HasValue)
            {
                alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == carreraId.Value);
            }

            // Normalizar y deduplicar igual que GetEstadisticasDetalladas para que alumnosHashSet
            // coincida con las matrículas normalizadas de Individuals/EntrevistaInicials en CalcularVulnerabilidades.
            // Sin esto, matrículas con NBSP/tabs en GestionUsuarios.Alumnos no coinciden con sus
            // contrapartes en Tutorias (que CalcularVulnerabilidades normaliza vía NormalizarMatricula),
            // causando que esos alumnos caigan en sinSeguimiento en vez de su categoría real.
            var alumnosMatriculas = alumnosQuery.Select(a => a.Matricula).ToList()
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => NormalizarMatricula(m))
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!incluirBajas)
            {
                var pBaja = PeriodoHelper.Obtener(DateTime.Now);
                var desdeBaja = CorteAplicable(pBaja.Inicio, pBaja.Fin) ?? pBaja.Inicio;
                var matriculasBaja = new HashSet<string>(
                    db.Bajas
                        .Where(b => b.Activo == true && b.Matricula != null
                                    && b.Fecha >= desdeBaja && b.Fecha <= pBaja.Fin)
                        .Select(b => b.Matricula)
                        .ToList()
                        .Select(m => NormalizarMatricula(m))
                        .Where(m => !string.IsNullOrEmpty(m)),
                    StringComparer.OrdinalIgnoreCase
                );
                if (matriculasBaja.Any())
                    alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
            }

            if (especialidadId.HasValue)
            {
                var especialidad = tutoriasDb.Especialidads.Find(especialidadId.Value);
                if (especialidad != null && !string.IsNullOrEmpty(especialidad.Nombre))
                {
                    string nombreEspecialidadNormalizado = NormalizarSinAcentos(especialidad.Nombre.Trim());
                    var alumnosMatriculasHashSetTemp = new HashSet<string>(alumnosMatriculas);
                    var datosPersonales = tutoriasDb.DatosPersonales
                        .Where(dp => alumnosMatriculasHashSetTemp.Contains(dp.Matricula) && dp.Especialidad != null)
                        .Select(dp => new { dp.Matricula, dp.Especialidad })
                        .ToList();

                    alumnosMatriculas = datosPersonales
                        .Where(dp => NormalizarSinAcentos(dp.Especialidad.Trim()).Equals(nombreEspecialidadNormalizado, StringComparison.OrdinalIgnoreCase))
                        .Select(dp => dp.Matricula)
                        .Distinct()
                        .ToList();
                }
            }

            // Filtro por grupo (letra) y/o grado (cuatrimestre): se evalua sobre el registro MAS RECIENTE
            // de DatosPersonales de cada matricula (un alumno puede tener historial de varios grupos).
            if (grupoId.HasValue || gradoId.HasValue)
            {
                var hashPobl = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);
                var dpGrupo = tutoriasDb.DatosPersonales
                    .Where(dp => dp.Matricula != null)
                    .Select(dp => new { dp.Matricula, dp.IdGrupo, dp.IdGrado, Fecha = (DateTime?)dp.Fecha })
                    .ToList()
                    .Where(dp => !string.IsNullOrWhiteSpace(dp.Matricula) && hashPobl.Contains(dp.Matricula.Trim()))
                    .GroupBy(dp => dp.Matricula.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.Fecha ?? DateTime.MinValue).First())
                    .Where(dp => (!grupoId.HasValue || dp.IdGrupo == grupoId.Value)
                              && (!gradoId.HasValue || dp.IdGrado == gradoId.Value))
                    .Select(dp => dp.Matricula.Trim())
                    .ToList();
                var hashGrupo = new HashSet<string>(dpGrupo, StringComparer.OrdinalIgnoreCase);
                alumnosMatriculas = alumnosMatriculas.Where(m => hashGrupo.Contains(m)).ToList();
            }

            // Reinicio por corte: si hubo corte en el cuatrimestre vigente, la demografia/situacion familiar
            // parten de cero y solo cuentan alumnos cuyo DatosPersonales o EntrevistaInicial es posterior al corte.
            var periodoResumen = PeriodoHelper.Obtener(DateTime.Now);
            var corteResumen = CorteAplicable(periodoResumen.Inicio, periodoResumen.Fin);
            if (corteResumen.HasValue)
            {
                // Normalizar ambos lados (NBSP/espacios) para no sub-contar al cruzar padron vs DatosPersonales/EntrevistaInicials.
                var matriculasPostCorte = new HashSet<string>(
                    tutoriasDb.DatosPersonales
                        .Where(dp => dp.Fecha >= corteResumen.Value && dp.Matricula != null)
                        .Select(dp => dp.Matricula)
                        .ToList()
                    .Concat(
                        tutoriasDb.EntrevistaInicials
                            .Where(e => e.Fecha >= corteResumen.Value && e.Matricula != null)
                            .Select(e => e.Matricula)
                            .ToList())
                    .Select(NormalizarMatricula)
                    .Where(m => !string.IsNullOrEmpty(m)),
                    StringComparer.OrdinalIgnoreCase);
                alumnosMatriculas = alumnosMatriculas
                    .Where(m => m != null && matriculasPostCorte.Contains(NormalizarMatricula(m)))
                    .ToList();
            }

            // Filtro por sexo (Task 4 2026-07-26): punto único para que tarjetas y modal cuadren.
            // Reutiliza exactamente la misma normalización que totalHombres/totalMujeres (~L3800).
            if (!string.IsNullOrEmpty(sexo))
            {
                var sexoInfoFiltro = ObtenerSexoPorMatricula(alumnosMatriculas);
                bool filtroBuscaHombre = sexo.Equals("H", StringComparison.OrdinalIgnoreCase);
                alumnosMatriculas = alumnosMatriculas.Where(m =>
                {
                    if (!sexoInfoFiltro.ContainsKey(m)) return false;
                    var sxStr = (sexoInfoFiltro[m].Sexo ?? "").Trim();
                    if (string.IsNullOrEmpty(sxStr)) return false;
                    bool esHombre = sxStr.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                                    sxStr.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                                    sxStr.Equals("Masculino", StringComparison.OrdinalIgnoreCase);
                    bool esMujer  = sxStr.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                                    sxStr.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                                    sxStr.Equals("Femenino", StringComparison.OrdinalIgnoreCase);
                    return filtroBuscaHombre ? esHombre : esMujer;
                }).ToList();
            }

            return alumnosMatriculas;
        }

        private ResumenDetalladoDatos CalcularResumenDetalladoDatos(Usuario usuario, int? especialidadId, int? carreraId, int? mes, int? año, int? periodo, bool incluirBajas, int? grupoId = null, int? gradoId = null, string filtroSexo = null)
        {
            var result = new ResumenDetalladoDatos { Ok = true };
            if (usuario == null)
            {
                result.Ok = false;
                result.Error = "Sesión expirada";
                return result;
            }

            var alumnosMatriculas = ObtenerPoblacionResumen(usuario, especialidadId, carreraId, incluirBajas, grupoId, gradoId, filtroSexo);

            string nombreEspecialidad = null;
            string nombreCarrera = null;

            if (especialidadId.HasValue)
            {
                var esp = tutoriasDb.Especialidads.Find(especialidadId.Value);
                nombreEspecialidad = esp?.Nombre;
            }

            if (carreraId.HasValue)
                nombreCarrera = GetNombreCarreraSimple(carreraId.Value);

            int totalEstudiantes = alumnosMatriculas.Count;
            var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas);
            var sexoInfoPorMatricula = ObtenerSexoPorMatricula(alumnosMatriculas);

            int totalHombres = alumnosMatriculas.Count(m =>
            {
                if (!sexoInfoPorMatricula.ContainsKey(m)) return false;
                var sexo = sexoInfoPorMatricula[m].Sexo ?? "";
                sexo = sexo.Trim();
                return !string.IsNullOrEmpty(sexo) &&
                       (sexo.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                        sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                        sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase));
            });

            int totalMujeres = alumnosMatriculas.Count(m =>
            {
                if (!sexoInfoPorMatricula.ContainsKey(m)) return false;
                var sexo = sexoInfoPorMatricula[m].Sexo ?? "";
                sexo = sexo.Trim();
                return !string.IsNullOrEmpty(sexo) &&
                       (sexo.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                        sexo.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                        sexo.Equals("Femenino", StringComparison.OrdinalIgnoreCase));
            });

            int totalSinSexo = alumnosMatriculas.Count(m =>
            {
                if (!sexoInfoPorMatricula.ContainsKey(m)) return false;
                var info = sexoInfoPorMatricula[m];
                if (!info.TieneRegistro) return false;
                var sexo = info.Sexo ?? "";
                sexo = sexo.Trim();
                return sexo == "" || sexo.Equals("No especificado", StringComparison.OrdinalIgnoreCase);
            });

            var datosPersonalesCompletos = tutoriasDb.DatosPersonales
                .Where(dp => alumnosMatriculasHashSet.Contains(dp.Matricula))
                .Select(dp => new { dp.Matricula, dp.Sexo, dp.IdPersona })
                .ToList();

            var idPersonasList = datosPersonalesCompletos.Select(d => d.IdPersona).ToList();

            var aspectosPersonalesCompletos = db.AspectosPersonales
                .Where(ap => idPersonasList.Contains(ap.IdPersona))
                .Select(ap => new { ap.IdPersona, ap.IdHijo, ap.IdEmbarazo })
                .ToList()
                .GroupBy(ap => ap.IdPersona)
                .ToDictionary(g => g.Key, g => g.First());

            var entrevistasIniciales = tutoriasDb.EntrevistaInicials
                .Where(x => alumnosMatriculasHashSet.Contains(x.Matricula))
                .Select(x => new { x.Matricula, x.IdTrabajo })
                .ToList()
                .GroupBy(x => x.Matricula)
                .ToDictionary(g => g.Key, g => g.First());

            int embarazadas = 0;
            int madres = 0;
            int padres = 0;

            foreach (var dp in datosPersonalesCompletos)
            {
                if (!aspectosPersonalesCompletos.ContainsKey(dp.IdPersona))
                    continue;

                var aspectos = aspectosPersonalesCompletos[dp.IdPersona];
                bool tieneHijo = aspectos.IdHijo == 1;
                bool esEmbarazada = aspectos.IdEmbarazo == 2;
                string sexo = dp.Sexo ?? "";

                if (esEmbarazada)
                    embarazadas++;
                else if (tieneHijo)
                {
                    if (sexo.Equals("M", StringComparison.OrdinalIgnoreCase) ||
                        sexo.Equals("Mujer", StringComparison.OrdinalIgnoreCase) ||
                        sexo.Equals("Femenino", StringComparison.OrdinalIgnoreCase))    
                        madres++;
                    else if (sexo.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                             sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase) ||
                             sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase))
                        padres++;
                }
            }

            if (mes.HasValue && periodo.HasValue)
            {
                result.Ok = false;
                result.Error = "Los filtros de mes y período no pueden usarse simultáneamente.";
                return result;
            }

            int añoParaFiltro = DateTime.Now.Year;
            int? mesParaFiltro = mes;
            if (!mes.HasValue && !periodo.HasValue)
                mesParaFiltro = DateTime.Now.Month;

            int? idAreaCoordinadorFiltro = null;
            if (usuario.IdNivel == 3)
                idAreaCoordinadorFiltro = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);

            var vulnerabilidades = CalcularVulnerabilidades(alumnosMatriculas, idAreaCoordinadorFiltro, carreraId, null, mesParaFiltro, añoParaFiltro, periodo);
            int alumnosTrabajando = CalcularAlumnosTrabajandoPorPeriodo(alumnosMatriculasHashSet, mesParaFiltro ?? mes, periodo);

            result.TotalEstudiantes = totalEstudiantes;
            result.TotalHombres = totalHombres;
            result.TotalMujeres = totalMujeres;
            result.TotalSinSexo = totalSinSexo;
            result.Embarazadas = embarazadas;
            result.Madres = madres;
            result.Padres = padres;
            result.AlumnosTrabajando = alumnosTrabajando;
            result.Vulnerabilidades = vulnerabilidades;
            result.NombreEspecialidad = nombreEspecialidad;
            result.NombreCarrera = nombreCarrera;
            return result;
        }

        [HttpPost]
        public ActionResult GetEstadisticasPorEspecialidad(int? especialidadId = null, int? carreraId = null, int? mes = null, int? año = null, int? periodo = null, bool incluirBajas = false, int? corteId = null, int? grupoId = null, int? gradoId = null, string sexo = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "ResumenGlobal");
                    if (hist != null) return hist;
                }
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, error = "Sesión expirada" });

                var r = CalcularResumenDetalladoDatos(usuario, especialidadId, carreraId, mes, año, periodo, incluirBajas, grupoId, gradoId, sexo /* filtroSexo */);
                if (!r.Ok)
                    return Json(new { success = false, error = r.Error });

                var v = r.Vulnerabilidades;
                var estadisticas = new
                {
                    totalEstudiantes = r.TotalEstudiantes,
                    totalHombres = r.TotalHombres,
                    totalMujeres = r.TotalMujeres,
                    totalSinSexo = r.TotalSinSexo,
                    embarazadas = r.Embarazadas,
                    madres = r.Madres,
                    padres = r.Padres,
                    padresFamilia = r.Embarazadas + r.Madres + r.Padres,
                    alumnosTrabajando = r.AlumnosTrabajando,
                    vulnerablesEconomicos = v.VulnerablesEconomicos,
                    vulnerablesAcademicos = v.VulnerablesAcademicos,
                    vulnerablesPersonales = v.VulnerablesPersonales,
                    totalVulnerables = v.TotalVulnerables,
                    noVulnerables = v.NoVulnerables,
                    sinSeguimiento = v.SinSeguimiento,
                    sinInformacion = v.SinInformacion,
                    clasificadosPorSeguimiento = v.ClasificadosPorSeguimiento,
                    clasificadosPorIdentificacion = v.ClasificadosPorIdentificacion,
                    especialidadFiltro = r.NombreEspecialidad,
                    carreraFiltro = r.NombreCarrera
                };

                return Json(new { success = true, data = estadisticas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private sealed class ExcelExportBytes
        {
            public bool Ok { get; set; }
            public byte[] Bytes { get; set; }
            public string FileName { get; set; }
            public string Error { get; set; }
        }

        private static void AgregarExcelAlZip(ZipArchive zip, ExcelExportBytes part)
        {
            if (part == null || !part.Ok || part.Bytes == null || part.Bytes.Length == 0)
                throw new InvalidOperationException(part != null ? part.Error : "Error al generar una de las exportaciones.");
            var name = string.IsNullOrWhiteSpace(part.FileName) ? "export.xlsx" : part.FileName;
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Fastest);
            using (var es = entry.Open())
                es.Write(part.Bytes, 0, part.Bytes.Length);
        }

        private ExcelExportBytes TryBuildEstadisticasDetalladasExcel(Usuario usuario, int? especialidadId, int? carreraId, int? mes, int? año, int? periodo, bool incluirBajas, int? grupoId = null, int? gradoId = null, string filtroSexo = null)
        {
            var datos = CalcularResumenDetalladoDatos(usuario, especialidadId, carreraId, mes, año, periodo, incluirBajas, grupoId, gradoId, filtroSexo);
            if (!datos.Ok)
                return new ExcelExportBytes { Ok = false, Error = datos.Error };

            int t = datos.TotalEstudiantes;
            string Pct(int valor)
            {
                return t > 0 ? ((double)valor / t * 100).ToString("F1", CultureInfo.InvariantCulture) + "%" : "0.0%";
            }

            void EstiloEncabezadoSeccion(ExcelWorksheet ws, int fila, int c1, int c2, string texto)
            {
                ws.Cells[fila, c1].Value = texto;
                ws.Cells[fila, c1, fila, c2].Merge = true;
                ws.Cells[fila, c1].Style.Font.Bold = true;
                ws.Cells[fila, c1].Style.Font.Size = 12;
                ws.Cells[fila, c1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[fila, c1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 150, 136));
                ws.Cells[fila, c1].Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            void FilaTabla(ExcelWorksheet ws, ref int fila, string indicador, int valor, string pct)
            {
                ws.Cells[fila, 1].Value = indicador;
                ws.Cells[fila, 2].Value = valor;
                ws.Cells[fila, 3].Value = pct;
                fila++;
            }

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Resumen detallado");

                worksheet.Cells[1, 1].Value = "RESUMEN DETALLADO";
                worksheet.Cells[1, 1, 1, 3].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;

                int row = 2;
                var filtrosTxt = new List<string>();
                if (!string.IsNullOrWhiteSpace(datos.NombreEspecialidad))
                    filtrosTxt.Add("Especialidad: " + datos.NombreEspecialidad);
                if (!string.IsNullOrWhiteSpace(datos.NombreCarrera))
                    filtrosTxt.Add("Carrera: " + datos.NombreCarrera);
                if (filtrosTxt.Count > 0)
                {
                    worksheet.Cells[row, 1].Value = string.Join(" | ", filtrosTxt);
                    worksheet.Cells[row, 1, row, 3].Merge = true;
                    row++;
                }

                worksheet.Cells[row, 1].Value = "Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                worksheet.Cells[row, 1, row, 3].Merge = true;
                row++;
                worksheet.Cells[row, 1].Value = "Bajas incluidas en el conteo: " + (incluirBajas ? "Sí" : "No");
                worksheet.Cells[row, 1, row, 3].Merge = true;
                row++;
                row++;

                EstiloEncabezadoSeccion(worksheet, row, 1, 3, "DEMOGRAFÍA");
                row++;
                worksheet.Cells[row, 1].Value = "Indicador";
                worksheet.Cells[row, 2].Value = "Valor";
                worksheet.Cells[row, 3].Value = "% del total de estudiantes";
                for (int c = 1; c <= 3; c++)
                {
                    worksheet.Cells[row, c].Style.Font.Bold = true;
                    worksheet.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                row++;

                FilaTabla(worksheet, ref row, "Total estudiantes", datos.TotalEstudiantes,
                    t > 0 ? (100.0).ToString("F1", CultureInfo.InvariantCulture) + "%" : "—");
                FilaTabla(worksheet, ref row, "Hombres", datos.TotalHombres, Pct(datos.TotalHombres));
                FilaTabla(worksheet, ref row, "Mujeres", datos.TotalMujeres, Pct(datos.TotalMujeres));
                FilaTabla(worksheet, ref row, "Sin especificar sexo", datos.TotalSinSexo, Pct(datos.TotalSinSexo));
                row++;

                EstiloEncabezadoSeccion(worksheet, row, 1, 3, "SITUACIÓN FAMILIAR");
                row++;
                worksheet.Cells[row, 1].Value = "Indicador";
                worksheet.Cells[row, 2].Value = "Valor";
                worksheet.Cells[row, 3].Value = "% del total de estudiantes";
                for (int c = 1; c <= 3; c++)
                {
                    worksheet.Cells[row, c].Style.Font.Bold = true;
                    worksheet.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                row++;

                FilaTabla(worksheet, ref row, "Embarazadas", datos.Embarazadas, Pct(datos.Embarazadas));
                FilaTabla(worksheet, ref row, "Madres", datos.Madres, Pct(datos.Madres));
                FilaTabla(worksheet, ref row, "Padres", datos.Padres, Pct(datos.Padres));
                FilaTabla(worksheet, ref row, "Estudiantes trabajando", datos.AlumnosTrabajando, Pct(datos.AlumnosTrabajando));

                worksheet.Cells[1, 1, row, 3].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"ResumenDetallado_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return new ExcelExportBytes { Ok = true, Bytes = stream.ToArray(), FileName = fileName };
            }
        }

        // Método para exportar estadísticas detalladas a Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarEstadisticasDetalladas(int? especialidadId = null, int? carreraId = null, int? mes = null, int? año = null, int? periodo = null, bool incluirBajas = false, int? grupoId = null, int? gradoId = null, string sexo = null)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var built = TryBuildEstadisticasDetalladasExcel(usuario, especialidadId, carreraId, mes, año, periodo, incluirBajas, grupoId, gradoId, sexo);
                if (!built.Ok)
                    return Json(new { success = false, error = built.Error });
                return File(built.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", built.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private ExcelExportBytes TryBuildVulnerabilidadesExcel(Usuario usuario, int? mes, bool incluirBajas)
        {
            if (usuario == null)
                return new ExcelExportBytes { Ok = false, Error = "Sesión expirada" };

                int? idAreaCoordinador = null;
                if (usuario.IdNivel == 3)
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);

                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();
                if (idAreaCoordinador.HasValue)
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);

                var alumnosMatriculas = alumnosQuery
                    .Select(a => a.Matricula)
                    .ToList()
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                int totalEstudiantes = alumnosMatriculas.Count;
                int añoActual = DateTime.Now.Year;
                int? mesParaFiltro = mes;
                if (!mesParaFiltro.HasValue)
                    mesParaFiltro = DateTime.Now.Month;

                var vulnerabilidades = CalcularVulnerabilidades(alumnosMatriculas, idAreaCoordinador, null, null, mesParaFiltro, añoActual, null);

                int totalVulnerablesTarjetas = vulnerabilidades.VulnerablesPersonales + vulnerabilidades.VulnerablesEconomicos + vulnerabilidades.VulnerablesAcademicos;

                string Pct(int v) => totalEstudiantes > 0 ? Math.Round((double)v / totalEstudiantes * 100, 1).ToString("0.0") + "%" : "0.0%";

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Vulnerabilidades");

                    worksheet.Cells[1, 1].Value = "VULNERABILIDAD";
                    worksheet.Cells[1, 1, 1, 3].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 14;

                    var nombresMeses = new Dictionary<int, string>
                    {
                        {1, "Enero"}, {2, "Febrero"}, {3, "Marzo"}, {4, "Abril"},
                        {5, "Mayo"}, {6, "Junio"}, {7, "Julio"}, {8, "Agosto"},
                        {9, "Septiembre"}, {10, "Octubre"}, {11, "Noviembre"}, {12, "Diciembre"}
                    };

                    int row = 3;
                    worksheet.Cells[row, 1].Value = "Mes:";
                    worksheet.Cells[row, 2].Value = nombresMeses.ContainsKey(mesParaFiltro.Value) ? nombresMeses[mesParaFiltro.Value] : mesParaFiltro.Value.ToString();
                    row++;
                    worksheet.Cells[row, 1].Value = "Año:";
                    worksheet.Cells[row, 2].Value = añoActual;
                    row++;
                    worksheet.Cells[row, 1].Value = "Total estudiantes:";
                    worksheet.Cells[row, 2].Value = totalEstudiantes;
                    row++;
                    row++;

                    worksheet.Cells[row, 1].Value = "Concepto";
                    worksheet.Cells[row, 2].Value = "Cantidad";
                    worksheet.Cells[row, 3].Value = "% sobre total";
                    using (var r = worksheet.Cells[row, 1, row, 3]) { r.Style.Font.Bold = true; }
                    row++;

                    void Fila(string label, int valor)
                    {
                        worksheet.Cells[row, 1].Value = label;
                        worksheet.Cells[row, 2].Value = valor;
                        worksheet.Cells[row, 3].Value = Pct(valor);
                        row++;
                    }

                    Fila("Total de vulnerables (personales + económicos + académicos)", totalVulnerablesTarjetas);
                    Fila("Vulnerables personales", vulnerabilidades.VulnerablesPersonales);
                    Fila("Vulnerables económicos", vulnerabilidades.VulnerablesEconomicos);
                    Fila("Vulnerables académicos", vulnerabilidades.VulnerablesAcademicos);
                    Fila("No vulnerables", vulnerabilidades.NoVulnerables);
                    Fila("Sin seguimiento", vulnerabilidades.SinSeguimiento);

                    worksheet.Cells[1, 1, row - 1, 3].AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    var mesNombre = $"_{mesParaFiltro.Value:00}";
                    var fileName = $"Vulnerabilidades{mesNombre}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return new ExcelExportBytes { Ok = true, Bytes = stream.ToArray(), FileName = fileName };
                }
        }

        // Método para exportar vulnerabilidades a Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarVulnerabilidades(int? mes = null, bool incluirBajas = false)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var built = TryBuildVulnerabilidadesExcel(usuario, mes, incluirBajas);
                if (!built.Ok)
                    return Json(new { success = false, error = built.Error });
                return File(built.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", built.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private ExcelExportBytes TryBuildCierreCuatrimestresExcel(Usuario usuario, int? año, bool incluirBajas)
        {
            if (usuario == null)
                return new ExcelExportBytes { Ok = false, Error = "Sesión expirada" };

                // Obtener IdArea del coordinador si es necesario
                int? idAreaCoordinador = null;
                if (usuario.IdNivel == 3)
                {
                    idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                }

                int añoActual = año ?? DateTime.Now.Year;

                // Obtener alumnos
                var alumnosQuery = usuariosDb.Alumnos.AsQueryable();
                if (idAreaCoordinador.HasValue)
                {
                    alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
                }

                var alumnosMatriculasRaw = alumnosQuery.Select(a => a.Matricula).ToList();
                var alumnosMatriculas = alumnosMatriculasRaw
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => NormalizarMatricula(m))
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!incluirBajas)
                {
                    var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                    if (matriculasBaja.Any())
                        alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
                }

                // Lista de períodos
                var periodos = new[]
                {
                    new { Id = 1, Nombre = "Enero - Abril" },
                    new { Id = 2, Nombre = "Mayo - Agosto" },
                    new { Id = 3, Nombre = "Septiembre - Diciembre" }
                };

                // Crear archivo Excel con EPPlus 4.5.3
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Cierre Cuatrimestres");

                    // Encabezados
                    worksheet.Cells[1, 1].Value = "ESTADÍSTICAS POR CIERRE DE CUATRIMESTRES";
                    worksheet.Cells[1, 1, 1, 7].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 16;
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[2, 1].Value = "Año:";
                    worksheet.Cells[2, 2].Value = añoActual;
                    worksheet.Cells[2, 2].Style.Font.Bold = true;

                    // Encabezados de columnas
                    int headerRow = 4;
                    worksheet.Cells[headerRow, 1].Value = "Período";
                    worksheet.Cells[headerRow, 2].Value = "Año";
                    worksheet.Cells[headerRow, 3].Value = "Económicos";
                    worksheet.Cells[headerRow, 4].Value = "Académicos";
                    worksheet.Cells[headerRow, 5].Value = "Personales";
                    worksheet.Cells[headerRow, 6].Value = "No Vulnerables";
                    worksheet.Cells[headerRow, 7].Value = "Sin Seguimiento";

                    // Estilo de encabezados
                    for (int col = 1; col <= 7; col++)
                    {
                        worksheet.Cells[headerRow, col].Style.Font.Bold = true;
                        worksheet.Cells[headerRow, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[headerRow, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 150, 136));
                        worksheet.Cells[headerRow, col].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }

                    int row = headerRow + 1;

                    foreach (var periodoInfo in periodos)
                    {
                        try
                        {
                            var vulnerabilidades = CalcularVulnerabilidades(
                                alumnosMatriculas,
                                idAreaCoordinador,
                                null,
                                null,
                                null,
                                añoActual,
                                periodoInfo.Id
                            );

                            worksheet.Cells[row, 1].Value = periodoInfo.Nombre;
                            worksheet.Cells[row, 2].Value = añoActual;
                            worksheet.Cells[row, 3].Value = vulnerabilidades.VulnerablesEconomicos;
                            worksheet.Cells[row, 4].Value = vulnerabilidades.VulnerablesAcademicos;
                            worksheet.Cells[row, 5].Value = vulnerabilidades.VulnerablesPersonales;
                            worksheet.Cells[row, 6].Value = vulnerabilidades.NoVulnerables;
                            worksheet.Cells[row, 7].Value = vulnerabilidades.SinSeguimiento;

                            row++;
                        }
                        catch (Exception ex)
                        {
                            worksheet.Cells[row, 1].Value = periodoInfo.Nombre;
                            worksheet.Cells[row, 2].Value = añoActual;
                            worksheet.Cells[row, 3].Value = "Sin datos por el momento";
                            worksheet.Cells[row, 3, row, 7].Merge = true;
                            worksheet.Cells[row, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            row++;
                        }
                    }

                    // Ajustar ancho de columnas
                    worksheet.Cells[1, 1, row - 1, 7].AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    var fileName = $"CierreCuatrimestres_{añoActual}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return new ExcelExportBytes { Ok = true, Bytes = stream.ToArray(), FileName = fileName };
                }
        }

        // Método para exportar estadísticas por cierre de cuatrimestres a Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarCierreCuatrimestres(int? año = null, bool incluirBajas = false)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var built = TryBuildCierreCuatrimestresExcel(usuario, año, incluirBajas);
                if (!built.Ok)
                    return Json(new { success = false, error = built.Error });
                return File(built.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", built.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private ExcelExportBytes TryBuildEstadisticasPorNivelExcel(Usuario usuario, bool incluirBajas)
        {
            if (usuario == null)
                return new ExcelExportBytes { Ok = false, Error = "Sesión expirada" };

            int? idAreaCoordinador = null;
            if (usuario.IdNivel == 3)
                idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);

            var alumnosQuery = usuariosDb.Alumnos.AsQueryable();
            if (idAreaCoordinador.HasValue)
                alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);
            else if (usuario.IdNivel == 3)
                alumnosQuery = alumnosQuery.Where(a => false);

            var alumnosMatriculas = alumnosQuery
                .Select(a => a.Matricula)
                .ToList()
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => NormalizarMatricula(m))
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!incluirBajas)
            {
                var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                if (matriculasBaja.Any())
                    alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
            }

            var alumnosMatriculasHashSet = new HashSet<string>(alumnosMatriculas, StringComparer.OrdinalIgnoreCase);

            var datosEspecialidad = tutoriasDb.DatosPersonales
                .Where(d => d.Matricula != null)
                .Select(d => new { d.Matricula, d.Especialidad })
                .ToList()
                .Where(d => alumnosMatriculasHashSet.Contains(NormalizarMatricula(d.Matricula)))
                .GroupBy(d => NormalizarMatricula(d.Matricula), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Especialidad ?? "", StringComparer.OrdinalIgnoreCase);

            var sexoInfo = ObtenerSexoPorMatricula(alumnosMatriculas);

            var matriculasTSU = new List<string>();
            var matriculasIng = new List<string>();
            var matriculasLic = new List<string>();
            int totalTSU = 0, hTSU = 0, mTSU = 0;
            int totalIng = 0, hIng = 0, mIng = 0;
            int totalLic = 0, hLic = 0, mLic = 0;

            var ci = CultureInfo.InvariantCulture.CompareInfo;

            foreach (var mat in alumnosMatriculas)
            {
                string esp = datosEspecialidad.ContainsKey(mat) ? datosEspecialidad[mat] : "";
                string sexo = (sexoInfo.ContainsKey(mat) ? sexoInfo[mat].Sexo ?? "" : "").Trim().ToUpper();
                bool esH = sexo == "H" || sexo == "HOMBRE" || sexo == "MASCULINO";
                bool esM = sexo == "M" || sexo == "MUJER" || sexo == "FEMENINO";

                if (ci.IndexOf(esp, "ingenieria", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                {
                    matriculasIng.Add(mat);
                    totalIng++;
                    if (esH) hIng++;
                    if (esM) mIng++;
                }
                else if (ci.IndexOf(esp, "licenciatura", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                {
                    matriculasLic.Add(mat);
                    totalLic++;
                    if (esH) hLic++;
                    if (esM) mLic++;
                }
                else
                {
                    matriculasTSU.Add(mat);
                    totalTSU++;
                    if (esH) hTSU++;
                    if (esM) mTSU++;
                }
            }

            int periodoActual = (DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
            int añoActual = DateTime.Now.Year;
            var vulnTSU = CalcularVulnerabilidades(matriculasTSU, idAreaCoordinador, null, null, null, añoActual, periodoActual);
            var vulnIng = CalcularVulnerabilidades(matriculasIng, idAreaCoordinador, null, null, null, añoActual, periodoActual);
            var vulnLic = CalcularVulnerabilidades(matriculasLic, idAreaCoordinador, null, null, null, añoActual, periodoActual);

            int total = totalTSU + totalIng + totalLic;
            string nombrePeriodo = periodoActual == 1 ? "Enero-Abril" : (periodoActual == 2 ? "Mayo-Agosto" : "Septiembre-Diciembre");

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Estadísticas por Nivel");
                worksheet.Cells[1, 1].Value = "ESTADÍSTICAS POR NIVEL DE ESTUDIO";
                worksheet.Cells[1, 1, 1, 10].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;

                int row = 3;
                worksheet.Cells[row, 1].Value = "Incluir bajas:";
                worksheet.Cells[row, 2].Value = incluirBajas ? "Sí" : "No";
                row++;
                worksheet.Cells[row, 1].Value = "Año:";
                worksheet.Cells[row, 2].Value = añoActual;
                row++;
                worksheet.Cells[row, 1].Value = "Período:";
                worksheet.Cells[row, 2].Value = nombrePeriodo;
                row++;
                worksheet.Cells[row, 1].Value = "Total estudiantes:";
                worksheet.Cells[row, 2].Value = total;
                row++;
                row++;

                worksheet.Cells[row, 1].Value = "Nivel de Estudio";
                worksheet.Cells[row, 2].Value = "Cantidad de Alumnos";
                worksheet.Cells[row, 3].Value = "Hombres";
                worksheet.Cells[row, 4].Value = "Mujeres";
                worksheet.Cells[row, 5].Value = "Económicos";
                worksheet.Cells[row, 6].Value = "Académicos";
                worksheet.Cells[row, 7].Value = "Personales";
                worksheet.Cells[row, 8].Value = "No Vulnerables";
                worksheet.Cells[row, 9].Value = "Sin Seguimiento";
                worksheet.Cells[row, 10].Value = "Porcentaje";
                for (int col = 1; col <= 10; col++)
                {
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                row++;

                Action<string, int, int, int, EstadisticasVulnerabilidad> escribeFila = (nombre, tn, th, tm, v) =>
                {
                    worksheet.Cells[row, 1].Value = nombre;
                    worksheet.Cells[row, 2].Value = tn;
                    worksheet.Cells[row, 3].Value = th;
                    worksheet.Cells[row, 4].Value = tm;
                    worksheet.Cells[row, 5].Value = v.VulnerablesEconomicos;
                    worksheet.Cells[row, 6].Value = v.VulnerablesAcademicos;
                    worksheet.Cells[row, 7].Value = v.VulnerablesPersonales;
                    worksheet.Cells[row, 8].Value = v.NoVulnerables;
                    worksheet.Cells[row, 9].Value = v.SinSeguimiento;
                    worksheet.Cells[row, 10].Value = total > 0 ? (double)tn / total : 0;
                    worksheet.Cells[row, 10].Style.Numberformat.Format = "0.0%";
                    row++;
                };

                escribeFila("TSU", totalTSU, hTSU, mTSU, vulnTSU);
                escribeFila("Ingeniería", totalIng, hIng, mIng, vulnIng);
                escribeFila("Licenciatura", totalLic, hLic, mLic, vulnLic);

                worksheet.Cells[1, 1, row - 1, 10].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"EstadisticasPorNivel_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return new ExcelExportBytes { Ok = true, Bytes = stream.ToArray(), FileName = fileName };
            }
        }

        // Método para exportar estadísticas por nivel de estudio a Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarEstadisticasPorNivel(bool incluirBajas = false)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var built = TryBuildEstadisticasPorNivelExcel(usuario, incluirBajas);
                if (!built.Ok)
                    return Json(new { success = false, error = built.Error });
                return File(built.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", built.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private ExcelExportBytes TryBuildEstadisticasPorCarreraExcel(Usuario usuario, bool incluirBajas)
        {
            if (usuario == null)
                return new ExcelExportBytes { Ok = false, Error = "Sesión expirada" };

            usuariosDb.Database.CommandTimeout = 300;
            tutoriasDb.Database.CommandTimeout = 300;
            db.Database.CommandTimeout = 300;

            int? idAreaCoordinador = null;
            if (usuario.IdNivel == 3)
            {
                idAreaCoordinador = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                if (!idAreaCoordinador.HasValue)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add("Estadísticas por Carrera");
                        worksheet.Cells[1, 1].Value = "ESTADÍSTICAS POR CARRERA";
                        worksheet.Cells[2, 1].Value = "Sin datos disponibles.";
                        worksheet.Cells[1, 1, 2, 10].AutoFitColumns();
                        var ms = new MemoryStream();
                        package.SaveAs(ms);
                        return new ExcelExportBytes { Ok = true, Bytes = ms.ToArray(), FileName = $"EstadisticasPorCarrera_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
                    }
                }
            }

            var alumnosQuery = usuariosDb.Alumnos.AsQueryable();
            if (idAreaCoordinador.HasValue)
                alumnosQuery = alumnosQuery.Where(a => a.IdCarrera == idAreaCoordinador.Value);

            var alumnosData = alumnosQuery
                .Where(a => a.Matricula != null && a.Matricula != "")
                .Select(a => new { a.Matricula, a.IdCarrera })
                .ToList();

            var alumnosMatriculas = alumnosData
                .Select(a => NormalizarMatricula(a.Matricula))
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!incluirBajas)
            {
                var matriculasBaja = ObtenerMatriculasBajaDelPeriodo();
                if (matriculasBaja.Any())
                    alumnosMatriculas = alumnosMatriculas.Where(m => !matriculasBaja.Contains(m)).ToList();
            }

            var alumnosInfoDict = alumnosData
                .GroupBy(a => NormalizarMatricula(a.Matricula), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToDictionary(g => g.Key, g => g.First().IdCarrera, StringComparer.OrdinalIgnoreCase);

            var sexoInfo = ObtenerSexoPorMatricula(alumnosMatriculas);
            int periodoActual = (DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
            int añoActual = DateTime.Now.Year;
            int totalGeneral = alumnosMatriculas.Count;

            var resultado = alumnosMatriculas
                .GroupBy(m => alumnosInfoDict.ContainsKey(m) ? alumnosInfoDict[m] : 0)
                .Where(g => g.Key > 0)
                .Select(g =>
                {
                    var mats = g.ToList();
                    int h = mats.Count(m =>
                    {
                        var s = (sexoInfo.ContainsKey(m) ? sexoInfo[m].Sexo ?? "" : "").Trim().ToUpper();
                        return s == "H" || s == "HOMBRE" || s == "MASCULINO";
                    });
                    int mu = mats.Count(m =>
                    {
                        var s = (sexoInfo.ContainsKey(m) ? sexoInfo[m].Sexo ?? "" : "").Trim().ToUpper();
                        return s == "M" || s == "MUJER" || s == "FEMENINO";
                    });
                    var vuln = CalcularVulnerabilidades(mats, null, null, null, null, añoActual, periodoActual);
                    double pct = totalGeneral > 0 ? Math.Round((double)mats.Count / totalGeneral * 100, 1) : 0;
                    return new
                    {
                        nombre = GetNombreCarrera(g.Key),
                        cantidad = mats.Count,
                        hombres = h,
                        mujeres = mu,
                        econ = vuln.VulnerablesEconomicos,
                        acad = vuln.VulnerablesAcademicos,
                        pers = vuln.VulnerablesPersonales,
                        noVul = vuln.NoVulnerables,
                        sinSeg = vuln.SinSeguimiento,
                        porcentaje = pct
                    };
                })
                .OrderByDescending(c => c.cantidad)
                .ToList();

            string nombrePeriodo = periodoActual == 1 ? "Enero-Abril" : (periodoActual == 2 ? "Mayo-Agosto" : "Septiembre-Diciembre");

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Estadísticas por Carrera");
                worksheet.Cells[1, 1].Value = "ESTADÍSTICAS POR CARRERA";
                worksheet.Cells[1, 1, 1, 10].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;

                int row = 3;
                worksheet.Cells[row, 1].Value = "Incluir bajas:";
                worksheet.Cells[row, 2].Value = incluirBajas ? "Sí" : "No";
                row++;
                worksheet.Cells[row, 1].Value = "Año:";
                worksheet.Cells[row, 2].Value = añoActual;
                row++;
                worksheet.Cells[row, 1].Value = "Período:";
                worksheet.Cells[row, 2].Value = nombrePeriodo;
                row++;
                worksheet.Cells[row, 1].Value = "Total estudiantes:";
                worksheet.Cells[row, 2].Value = totalGeneral;
                row++;
                row++;

                worksheet.Cells[row, 1].Value = "Carrera";
                worksheet.Cells[row, 2].Value = "Total";
                worksheet.Cells[row, 3].Value = "Hombres";
                worksheet.Cells[row, 4].Value = "Mujeres";
                worksheet.Cells[row, 5].Value = "Eco.";
                worksheet.Cells[row, 6].Value = "Acad.";
                worksheet.Cells[row, 7].Value = "Pers.";
                worksheet.Cells[row, 8].Value = "No Vuln.";
                worksheet.Cells[row, 9].Value = "Sin seg.";
                worksheet.Cells[row, 10].Value = "% total";
                for (int col = 1; col <= 10; col++)
                {
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                row++;

                foreach (var c in resultado)
                {
                    worksheet.Cells[row, 1].Value = c.nombre;
                    worksheet.Cells[row, 2].Value = c.cantidad;
                    worksheet.Cells[row, 3].Value = c.hombres;
                    worksheet.Cells[row, 4].Value = c.mujeres;
                    worksheet.Cells[row, 5].Value = c.econ;
                    worksheet.Cells[row, 6].Value = c.acad;
                    worksheet.Cells[row, 7].Value = c.pers;
                    worksheet.Cells[row, 8].Value = c.noVul;
                    worksheet.Cells[row, 9].Value = c.sinSeg;
                    worksheet.Cells[row, 10].Value = c.porcentaje / 100.0;
                    worksheet.Cells[row, 10].Style.Numberformat.Format = "0.0%";
                    row++;
                }

                worksheet.Cells[1, 1, Math.Max(row - 1, 5), 10].AutoFitColumns();
                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;
                return new ExcelExportBytes { Ok = true, Bytes = stream.ToArray(), FileName = $"EstadisticasPorCarrera_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
            }
        }

        // Método para exportar estadísticas por carrera a Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarEstadisticasPorCarrera(bool incluirBajas = false)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var built = TryBuildEstadisticasPorCarreraExcel(usuario, incluirBajas);
                if (!built.Ok)
                    return Json(new { success = false, error = built.Error });
                return File(built.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", built.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private ExcelExportBytes TryBuildEstadisticasPorGrupoExcel(Usuario usuario, int? carreraIdEstadias)
        {
            if (usuario == null)
                return new ExcelExportBytes { Ok = false, Error = "Sesión expirada" };

            var estadisticasGrupos = CalcularEstadisticasPorGrupo();

            if (usuario.IdNivel == 4 && carreraIdEstadias.HasValue)
            {
                int? idCarreraTutorias = MapearIdCarreraParaGrupos(carreraIdEstadias.Value);
                if (idCarreraTutorias.HasValue)
                    estadisticasGrupos = estadisticasGrupos.Where(g => g.IdCarrera == idCarreraTutorias.Value).ToList();
                else
                    estadisticasGrupos = new List<EstadisticaGrupo>();
            }

            int añoActual = DateTime.Now.Year;
            int periodoActual = (DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
            string nombrePeriodo = periodoActual == 1 ? "Enero-Abril" : (periodoActual == 2 ? "Mayo-Agosto" : "Septiembre-Diciembre");

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Estadísticas por Grupo");
                worksheet.Cells[1, 1].Value = "ESTADÍSTICAS POR GRUPO";
                worksheet.Cells[1, 1, 1, 14].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;

                int row = 3;
                worksheet.Cells[row, 1].Value = "Año / Período:";
                worksheet.Cells[row, 2].Value = añoActual + " — " + nombrePeriodo;
                row++;
                string valorCarreraFiltro = "Todas";
                if (carreraIdEstadias.HasValue)
                    valorCarreraFiltro = GetNombreCarrera(carreraIdEstadias.Value);
                else if (usuario.IdNivel == 3)
                {
                    int? idAreaCoord = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);
                    if (idAreaCoord.HasValue)
                        valorCarreraFiltro = GetNombreCarrera(idAreaCoord.Value);
                }
                worksheet.Cells[row, 1].Value = "Carrera:";
                worksheet.Cells[row, 2].Value = valorCarreraFiltro;
                row++;
                row++;

                worksheet.Cells[row, 1].Value = "Grupo";
                worksheet.Cells[row, 2].Value = "Carrera";
                worksheet.Cells[row, 3].Value = "Especialidad";
                worksheet.Cells[row, 4].Value = "Turno";
                worksheet.Cells[row, 5].Value = "Periodo";
                worksheet.Cells[row, 6].Value = "Total";
                worksheet.Cells[row, 7].Value = "Hombres";
                worksheet.Cells[row, 8].Value = "Mujeres";
                worksheet.Cells[row, 9].Value = "Bajas";
                worksheet.Cells[row, 10].Value = "Económicos";
                worksheet.Cells[row, 11].Value = "Académicos";
                worksheet.Cells[row, 12].Value = "Personales";
                worksheet.Cells[row, 13].Value = "No Vulnerables";
                worksheet.Cells[row, 14].Value = "Sin Seguimiento";

                for (int col = 1; col <= 14; col++)
                {
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                row++;

                foreach (var grupo in estadisticasGrupos)
                {
                    worksheet.Cells[row, 1].Value = grupo.GrupoId;
                    worksheet.Cells[row, 2].Value = grupo.NombreCarrera;
                    worksheet.Cells[row, 3].Value = grupo.Especialidad;
                    worksheet.Cells[row, 4].Value = grupo.NombreTurno;
                    worksheet.Cells[row, 5].Value = grupo.NombrePeriodo;
                    worksheet.Cells[row, 6].Value = grupo.TotalEstudiantes;
                    worksheet.Cells[row, 7].Value = grupo.Hombres;
                    worksheet.Cells[row, 8].Value = grupo.Mujeres;
                    worksheet.Cells[row, 9].Value = grupo.Bajas;
                    worksheet.Cells[row, 10].Value = grupo.VulnerablesEconomicos;
                    worksheet.Cells[row, 11].Value = grupo.VulnerablesAcademicos;
                    worksheet.Cells[row, 12].Value = grupo.VulnerablesPersonales;
                    worksheet.Cells[row, 13].Value = grupo.NoVulnerables;
                    worksheet.Cells[row, 14].Value = grupo.SinSeguimiento;
                    row++;
                }

                worksheet.Cells[1, 1, Math.Max(row - 1, 8), 14].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"EstadisticasPorGrupo_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return new ExcelExportBytes { Ok = true, Bytes = stream.ToArray(), FileName = fileName };
            }
        }

        // Método para exportar estadísticas por grupo a Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarEstadisticasPorGrupo(int? carreraId = null)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var built = TryBuildEstadisticasPorGrupoExcel(usuario, carreraId);
                if (!built.Ok)
                    return Json(new { success = false, error = built.Error });
                return File(built.Bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", built.FileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Método AJAX para obtener estadísticas de bajas bajo demanda
        [HttpPost]
        public ActionResult GetEstadisticasBajas(int? corteId = null, string causa = null, int? mes = null, int? carreraId = null, int? especialidadId = null, int? gradoId = null, int? grupoId = null)
        {
            try
            {
                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "Bajas");
                    if (hist != null) return hist;
                }
                // Obtener usuario de la sesión. Nivel 3: siempre su carrera (fail-closed);
                // Master: puede filtrar por carrera (IDs de Tutorias.Carreras, ViewBag.CarrerasMaterias).
                Usuario usuario = Session["Usuario"] as Usuario;
                int? carreraIdFiltro = (usuario != null && usuario.IdNivel == 3)
                    ? (int?)usuario.IdCarrera
                    : ((usuario != null && usuario.IdNivel == 4) ? carreraId : null);
                var estadisticasBajas = CalcularEstadisticasBajas(carreraIdFiltro, causa, mes, especialidadId, gradoId, grupoId);
                return Json(new { success = true, data = estadisticasBajas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ===== Cumplimiento de tutores (2026-08-02, propuesta de innovación) =====
        // Por cada tutor con grupo asignado en el cuatrimestre vigente (TutoriaGrupals):
        // % de sus alumnos con al menos un seguimiento del periodo, % con entrevista inicial
        // del periodo y días desde su última captura. Ataca la queja de dirección de que
        // "los datos no cuadran porque los tutores no capturan". Fail-closed: nivel 3 solo
        // ve los tutores de su carrera; Máster ve todos.
        [HttpPost]
        public ActionResult GetCumplimientoTutores()
        {
            try
            {
                db.Database.CommandTimeout = 300;
                Usuario usuario = Session["Usuario"] as Usuario;
                int? carreraFiltro = (usuario != null && usuario.IdNivel == 3) ? (int?)usuario.IdCarrera : null;

                var periodoCumpl = PeriodoHelper.Obtener(DateTime.Now);
                int anio = periodoCumpl.Anio, numPeriodo = periodoCumpl.NumPeriodo;
                DateTime inicio = periodoCumpl.Inicio, fin = periodoCumpl.Fin;

                var asignaciones = db.TutoriaGrupals
                    .Where(t => t.Año == anio && t.IdPeriodo == numPeriodo)
                    .ToList();
                if (carreraFiltro.HasValue)
                    asignaciones = asignaciones.Where(t => t.IdCarrera == carreraFiltro.Value).ToList();
                if (!asignaciones.Any())
                    return Json(new { success = true, data = new object[0], periodo = periodoCumpl.Nombre });

                // Alumnos activos del periodo; el match alumno->grupo usa las mismas llaves que
                // AsesorController.Grupo (carrera+grado+grupo+turno, ya acotado a año/periodo).
                var alumnosPeriodo = db.DatosPersonales
                    .Where(a => a.Año == anio && a.IdPeriodo == numPeriodo && a.Estado)
                    .Select(a => new { a.IdPersona, a.IdCarrera, a.IdGrado, a.IdGrupo, a.IdTurno, a.Matricula, a.Nombre })
                    .ToList();
                var infoAlumno = new Dictionary<int, Tuple<string, string>>();
                foreach (var a in alumnosPeriodo)
                    if (!infoAlumno.ContainsKey(a.IdPersona))
                        infoAlumno[a.IdPersona] = Tuple.Create((a.Matricula ?? "").Trim(), (a.Nombre ?? "").Trim());

                // Solo seguimientos CON CONTENIDO (vulnerabilidad o problematica capturada): la
                // importacion masiva dejo filas de seguimiento vacias y contarlas daria 100% falso
                // (mismo criterio que la cascada de vulnerabilidad, que hoy reporta 0 por seguimiento).
                var seguimientosPeriodo = (from s in db.Seguimientoes
                                           join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                           where s.Fecha >= inicio && s.Fecha <= fin
                                              && ((s.Vulnerabilidad != null && s.Vulnerabilidad.Trim() != "")
                                                  || (s.Problematica != null && s.Problematica.Trim() != ""))
                                           select new { i.IdPersona, s.Fecha }).ToList();
                var ultimoSegPorPersona = seguimientosPeriodo
                    .GroupBy(x => x.IdPersona)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Fecha));
                // "Este mes": la foto del momento (conecta con la Fuente de clasificación, que
                // por defecto filtra por el mes en curso).
                DateTime inicioMesActual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var conSegEsteMes = new HashSet<int>(seguimientosPeriodo
                    .Where(x => x.Fecha >= inicioMesActual).Select(x => x.IdPersona).Distinct());
                var conEntrevistaPeriodo = new HashSet<int>(db.EntrevistaInicials
                    .Where(e => e.Fecha >= inicio && e.Fecha <= fin)
                    .Select(e => e.IdPersona).Distinct().ToList());

                var nombresCarreraCumpl = db.Carreras.ToList().ToDictionary(c => c.IdCarrera, c => (c.Nombre ?? "").Trim());
                var nombresGradoCumpl = db.Gradoes.ToList().ToDictionary(g => g.IdGrado, g => (g.Nombre ?? "").Trim());
                var nombresGrupoCumpl = db.Grupoes.ToList().ToDictionary(g => g.IdGrupo, g => (g.Nombre ?? "").Trim());
                var idsTutores = asignaciones.Select(t => t.IdUsuario).Distinct().ToList();
                var nombresTutores = db.Usuarios.Where(u => idsTutores.Contains(u.IdUsuario)).ToList()
                    .ToDictionary(u => u.IdUsuario, u => ((u.NombreCompleto ?? u.UserName) ?? ("Tutor " + u.IdUsuario)).Trim());

                var filas = asignaciones.GroupBy(t => t.IdUsuario).Select(g =>
                {
                    var idsAlumnosTutor = new List<int>();
                    var etiquetasGrupos = new List<string>();
                    foreach (var asg in g)
                    {
                        etiquetasGrupos.Add(
                            (nombresGradoCumpl.ContainsKey(asg.IdGrado) ? nombresGradoCumpl[asg.IdGrado] : "?") +
                            (nombresGrupoCumpl.ContainsKey(asg.IdGrupo) ? nombresGrupoCumpl[asg.IdGrupo] : "?"));
                        idsAlumnosTutor.AddRange(alumnosPeriodo
                            .Where(a => a.IdCarrera == asg.IdCarrera && a.IdGrado == asg.IdGrado
                                     && a.IdGrupo == asg.IdGrupo && a.IdTurno == asg.IdTurno)
                            .Select(a => a.IdPersona));
                    }
                    var idsAlumnos = idsAlumnosTutor.Distinct().ToList();
                    int totalAl = idsAlumnos.Count;
                    int conSeg = idsAlumnos.Count(id => ultimoSegPorPersona.ContainsKey(id));
                    int conEnt = idsAlumnos.Count(id => conEntrevistaPeriodo.Contains(id));
                    int conSegMes = idsAlumnos.Count(id => conSegEsteMes.Contains(id));
                    DateTime? ultima = idsAlumnos
                        .Where(id => ultimoSegPorPersona.ContainsKey(id))
                        .Select(id => (DateTime?)ultimoSegPorPersona[id])
                        .DefaultIfEmpty(null).Max();
                    // Para el modal accionable: quiénes de sus alumnos NO tienen seguimiento del periodo.
                    var sinSeguimiento = idsAlumnos
                        .Where(id => !ultimoSegPorPersona.ContainsKey(id) && infoAlumno.ContainsKey(id))
                        .Select(id => new { matricula = infoAlumno[id].Item1, nombre = infoAlumno[id].Item2 })
                        .OrderBy(x => x.nombre)
                        .ToList();
                    var primeraAsg = g.First();
                    return new
                    {
                        idUsuario = g.Key,
                        tutor = nombresTutores.ContainsKey(g.Key) ? nombresTutores[g.Key] : ("Tutor " + g.Key),
                        carrera = nombresCarreraCumpl.ContainsKey(primeraAsg.IdCarrera) ? nombresCarreraCumpl[primeraAsg.IdCarrera] : "",
                        grupos = string.Join(", ", etiquetasGrupos.OrderBy(x => x)),
                        alumnos = totalAl,
                        conSeguimiento = conSeg,
                        pctSeguimiento = totalAl > 0 ? Math.Round(conSeg * 100.0 / totalAl, 1) : 0,
                        conEntrevista = conEnt,
                        pctEntrevista = totalAl > 0 ? Math.Round(conEnt * 100.0 / totalAl, 1) : 0,
                        conSeguimientoMes = conSegMes,
                        pctSeguimientoMes = totalAl > 0 ? Math.Round(conSegMes * 100.0 / totalAl, 1) : 0,
                        ultimaCaptura = ultima.HasValue ? ultima.Value.ToString("dd/MM/yyyy") : null,
                        diasSinCaptura = ultima.HasValue ? (int?)(DateTime.Now - ultima.Value).Days : null,
                        alumnosSinSeguimiento = sinSeguimiento
                    };
                })
                .OrderBy(f => f.pctSeguimiento).ThenBy(f => f.pctEntrevista).ThenBy(f => f.tutor)
                .ToList();

                return Json(new { success = true, data = filas, periodo = periodoCumpl.Nombre });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        public class Alumno360MateriaRow
        {
            public string Materia { get; set; }
            public string Estado { get; set; }
            public int Intentos { get; set; }
        }

        // Buscador de tutores para la ficha flotante (tutores con grupo en el periodo vigente).
        [HttpPost]
        public ActionResult BuscarTutor360(string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
                    return Json(new { success = true, data = new object[0] });
                string termino = q.Trim();
                Usuario usuario = Session["Usuario"] as Usuario;
                int? carreraFiltro = (usuario != null && usuario.IdNivel == 3) ? (int?)usuario.IdCarrera : null;

                var periodoB = PeriodoHelper.Obtener(DateTime.Now);
                var asignacionesB = db.TutoriaGrupals
                    .Where(t => t.Año == periodoB.Anio && t.IdPeriodo == periodoB.NumPeriodo);
                if (carreraFiltro.HasValue)
                    asignacionesB = asignacionesB.Where(t => t.IdCarrera == carreraFiltro.Value);
                var idsTutoresB = asignacionesB.Select(t => t.IdUsuario).Distinct().ToList();

                var nombresCarreraB = db.Carreras.ToList().ToDictionary(c => c.IdCarrera, c => (c.Nombre ?? "").Trim());
                var tutores = db.Usuarios
                    .Where(u => idsTutoresB.Contains(u.IdUsuario) && u.NombreCompleto.Contains(termino))
                    .Select(u => new { u.IdUsuario, u.NombreCompleto, u.IdCarrera })
                    .Take(10).ToList()
                    .Select(u => new
                    {
                        idUsuario = u.IdUsuario,
                        nombre = (u.NombreCompleto ?? "").Trim(),
                        carrera = nombresCarreraB.ContainsKey(u.IdCarrera) ? nombresCarreraB[u.IdCarrera] : ""
                    })
                    .OrderBy(u => u.nombre).ToList();
                return Json(new { success = true, data = tutores });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.GetBaseException().Message });
            }
        }

        public class TutorMateriasAlumnoRow
        {
            public int IdPersona { get; set; }
            public int Reprobadas { get; set; }
            public int Extraordinarios { get; set; }
        }

        // ===== Ficha del Tutor 360° (2026-08-02): expediente del tutor, integrado al tablero de
        // Cumplimiento (click en el nombre). Fail-closed: nivel 3 solo tutores de su carrera. =====
        [HttpPost]
        public ActionResult GetTutor360(int idUsuario)
        {
            try
            {
                db.Database.CommandTimeout = 300;
                Usuario usuarioSesion = Session["Usuario"] as Usuario;

                var periodoT = PeriodoHelper.Obtener(DateTime.Now);
                DateTime inicio = periodoT.Inicio, fin = periodoT.Fin;

                var asignaciones = db.TutoriaGrupals
                    .Where(t => t.IdUsuario == idUsuario && t.Año == periodoT.Anio && t.IdPeriodo == periodoT.NumPeriodo)
                    .ToList();
                if (usuarioSesion != null && usuarioSesion.IdNivel == 3 &&
                    asignaciones.Any() && asignaciones.All(a => a.IdCarrera != usuarioSesion.IdCarrera))
                    return Json(new { success = false, error = "Sin acceso a tutores de otra carrera." });

                var tutorInfo = db.Usuarios.Where(u => u.IdUsuario == idUsuario)
                    .Select(u => new { u.NombreCompleto, u.UserName, u.IdCarrera }).FirstOrDefault();
                if (tutorInfo == null)
                    return Json(new { success = false, error = "Tutor no encontrado." });

                var nombresCarreraT = db.Carreras.ToList().ToDictionary(c => c.IdCarrera, c => (c.Nombre ?? "").Trim());
                var nombresGradoT = db.Gradoes.ToList().ToDictionary(g => g.IdGrado, g => (g.Nombre ?? "").Trim());
                var nombresGrupoT = db.Grupoes.ToList().ToDictionary(g => g.IdGrupo, g => (g.Nombre ?? "").Trim());
                Func<int, int, string> etiquetaGrupoT = (idGrado, idGrupo) =>
                    (nombresGradoT.ContainsKey(idGrado) ? nombresGradoT[idGrado] : "?") +
                    (nombresGrupoT.ContainsKey(idGrupo) ? nombresGrupoT[idGrupo] : "?");

                // Alumnos a cargo (mismo match del tablero de cumplimiento).
                var alumnosTutor = new List<int>();
                var infoAlumnoT = new Dictionary<int, Tuple<string, string>>();
                foreach (var asg in asignaciones)
                {
                    var alumnosGrupo = db.DatosPersonales
                        .Where(a => a.Año == periodoT.Anio && a.IdPeriodo == periodoT.NumPeriodo && a.Estado
                                 && a.IdCarrera == asg.IdCarrera && a.IdGrado == asg.IdGrado
                                 && a.IdGrupo == asg.IdGrupo && a.IdTurno == asg.IdTurno)
                        .Select(a => new { a.IdPersona, a.Matricula, a.Nombre }).ToList();
                    foreach (var a in alumnosGrupo)
                    {
                        alumnosTutor.Add(a.IdPersona);
                        if (!infoAlumnoT.ContainsKey(a.IdPersona))
                            infoAlumnoT[a.IdPersona] = Tuple.Create((a.Matricula ?? "").Trim(), (a.Nombre ?? "").Trim());
                    }
                }
                var idsAlumnos = alumnosTutor.Distinct().ToList();

                // Seguimientos con contenido del periodo, de SUS alumnos (lista vacía => 0 filas).
                var segsTutor = (from s in db.Seguimientoes
                                 join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                 where idsAlumnos.Contains(i.IdPersona)
                                    && s.Fecha >= inicio && s.Fecha <= fin
                                    && ((s.Vulnerabilidad != null && s.Vulnerabilidad.Trim() != "")
                                        || (s.Problematica != null && s.Problematica.Trim() != ""))
                                 select new { i.IdPersona, s.Fecha, s.Vulnerabilidad }).ToList();

                var ultimoSegPorAlumno = segsTutor.GroupBy(x => x.IdPersona)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Fecha).First());
                DateTime inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var conSegMesSet = new HashSet<int>(segsTutor.Where(x => x.Fecha >= inicioMes).Select(x => x.IdPersona).Distinct());
                var conEntrevistaSet = idsAlumnos.Any()
                    ? new HashSet<int>(db.EntrevistaInicials
                        .Where(e => idsAlumnos.Contains(e.IdPersona) && e.Fecha >= inicio && e.Fecha <= fin)
                        .Select(e => e.IdPersona).Distinct().ToList())
                    : new HashSet<int>();

                int totalAl = idsAlumnos.Count;
                int conSeg = idsAlumnos.Count(id => ultimoSegPorAlumno.ContainsKey(id));
                DateTime? ultimaCap = segsTutor.Any() ? (DateTime?)segsTutor.Max(x => x.Fecha) : null;

                // Actividad: capturas por mes del periodo (para ver si trabaja parejo o en una sentada).
                var actividadMeses = Enumerable.Range(0, 4)
                    .Select(k => inicio.AddMonths(k)).Where(mIni => mIni <= fin)
                    .Select(mIni => new
                    {
                        mes = mIni.ToString("MMMM", new System.Globalization.CultureInfo("es-MX")),
                        capturas = segsTutor.Count(x => x.Fecha.Year == mIni.Year && x.Fecha.Month == mIni.Month)
                    }).ToList();

                // Estado de sus alumnos: clasificación simple (últ. seguimiento del periodo → entrevista → sin info).
                var vulnEntrevista = idsAlumnos.Any()
                    ? db.EntrevistaInicials.Where(e => idsAlumnos.Contains(e.IdPersona))
                        .Select(e => new { e.IdPersona, e.Fecha, e.Vulnerable }).ToList()
                        .GroupBy(e => e.IdPersona)
                        .ToDictionary(g => g.Key, g => (g.OrderByDescending(e => e.Fecha).First().Vulnerable ?? "").Trim())
                    : new Dictionary<int, string>();
                int econ = 0, acad = 0, pers = 0, noVul = 0, sinInfo = 0;
                foreach (var id in idsAlumnos)
                {
                    string texto = ultimoSegPorAlumno.ContainsKey(id) ? (ultimoSegPorAlumno[id].Vulnerabilidad ?? "") : "";
                    if (string.IsNullOrWhiteSpace(texto) && vulnEntrevista.ContainsKey(id)) texto = vulnEntrevista[id];
                    var t = (texto ?? "").Trim().ToUpperInvariant();
                    if (t == "") { sinInfo++; }
                    else if (t.Contains("ECON")) econ++;
                    else if (t.Contains("ACAD")) acad++;
                    else if (t.Contains("PERS")) pers++;
                    else noVul++;
                }

                // Alumnos con materias reprobadas/extraordinario y bajas del periodo en sus grupos.
                int alumnosConReprobadas = 0;
                if (idsAlumnos.Any())
                {
                    var idsCsv = string.Join(",", idsAlumnos); // ids internos (int), no input de usuario
                    alumnosConReprobadas = db.Database.SqlQuery<int>(
                        "SELECT COUNT(DISTINCT ma.IdPersona) FROM MateriasAlumno ma WHERE ma.IdPersona IN (" + idsCsv + ") AND ma.Estado IN ('Reprobada','Extraordinario')").First();
                }
                int bajasPeriodo = idsAlumnos.Any()
                    ? db.Bajas.Count(b => idsAlumnos.Contains(b.IdPersona) && b.Fecha >= inicio && b.Fecha <= fin)
                    : 0;

                // Canalizaciones REALIZADAS por el tutor (indicador positivo: sí actúa).
                var canalizacionesTutor = (from c in db.Canalizaciones
                                           join t in db.TipoCanalizaciones on c.IdTipoCanalizacion equals t.IdTipoCanalizacion
                                           where c.IdUsuario == idUsuario
                                           orderby c.Fecha descending
                                           select new { c.Fecha, t.Descripcion }).ToList();

                // Historial de grupos en otros cuatrimestres (antigüedad como tutor).
                var historial = db.TutoriaGrupals.Where(t => t.IdUsuario == idUsuario)
                    .ToList()
                    .OrderByDescending(t => t.Año).ThenByDescending(t => t.IdPeriodo)
                    .Select(t => new
                    {
                        periodo = PeriodoHelper.Obtener(t.Año, t.IdPeriodo).Nombre,
                        grupo = etiquetaGrupoT(t.IdGrado, t.IdGrupo),
                        carrera = nombresCarreraT.ContainsKey(t.IdCarrera) ? nombresCarreraT[t.IdCarrera] : ""
                    }).ToList();

                var alumnosSinSeg = idsAlumnos
                    .Where(id => !ultimoSegPorAlumno.ContainsKey(id) && infoAlumnoT.ContainsKey(id))
                    .Select(id => new { matricula = infoAlumnoT[id].Item1, nombre = infoAlumnoT[id].Item2 })
                    .OrderBy(x => x.nombre).ToList();

                // Detalle por alumno (para el "Ver alumnos a detalle" de la ficha): clasificación,
                // última captura y materias reprobadas/extraordinario de cada uno.
                var materiasPorAlumno = new Dictionary<int, TutorMateriasAlumnoRow>();
                if (idsAlumnos.Any())
                {
                    var idsCsvDet = string.Join(",", idsAlumnos); // ids internos (int), no input de usuario
                    materiasPorAlumno = db.Database.SqlQuery<TutorMateriasAlumnoRow>(
                        @"SELECT ma.IdPersona AS IdPersona,
                                 SUM(CASE WHEN ma.Estado = 'Reprobada' THEN 1 ELSE 0 END) AS Reprobadas,
                                 SUM(CASE WHEN ma.Estado = 'Extraordinario' THEN 1 ELSE 0 END) AS Extraordinarios
                          FROM MateriasAlumno ma
                          WHERE ma.IdPersona IN (" + idsCsvDet + @") AND ma.Estado IN ('Reprobada', 'Extraordinario')
                          GROUP BY ma.IdPersona").ToList()
                        .ToDictionary(x => x.IdPersona, x => x);
                }
                var alumnosDetalle = idsAlumnos
                    .Where(id => infoAlumnoT.ContainsKey(id))
                    .Select(id =>
                    {
                        string texto = ultimoSegPorAlumno.ContainsKey(id) ? (ultimoSegPorAlumno[id].Vulnerabilidad ?? "") : "";
                        if (string.IsNullOrWhiteSpace(texto) && vulnEntrevista.ContainsKey(id)) texto = vulnEntrevista[id];
                        var mat = materiasPorAlumno.ContainsKey(id) ? materiasPorAlumno[id] : null;
                        return new
                        {
                            idPersona = id,
                            matricula = infoAlumnoT[id].Item1,
                            nombre = infoAlumnoT[id].Item2,
                            clasificacion = string.IsNullOrWhiteSpace(texto) ? "Sin información" : texto.Trim(),
                            ultimaCaptura = ultimoSegPorAlumno.ContainsKey(id) ? ultimoSegPorAlumno[id].Fecha.ToString("dd/MM/yyyy") : null,
                            reprobadas = mat != null ? mat.Reprobadas : 0,
                            extraordinarios = mat != null ? mat.Extraordinarios : 0
                        };
                    })
                    .OrderBy(a => a.nombre).ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        idUsuario,
                        nombre = ((tutorInfo.NombreCompleto ?? tutorInfo.UserName) ?? "").Trim(),
                        carrera = nombresCarreraT.ContainsKey(tutorInfo.IdCarrera) ? nombresCarreraT[tutorInfo.IdCarrera] : "",
                        periodo = periodoT.Nombre,
                        grupos = asignaciones.Select(a => etiquetaGrupoT(a.IdGrado, a.IdGrupo)).OrderBy(x => x).ToList(),
                        alumnos = totalAl,
                        conSeguimiento = conSeg,
                        pctSeguimiento = totalAl > 0 ? Math.Round(conSeg * 100.0 / totalAl, 1) : 0,
                        conSeguimientoMes = idsAlumnos.Count(id => conSegMesSet.Contains(id)),
                        conEntrevista = idsAlumnos.Count(id => conEntrevistaSet.Contains(id)),
                        ultimaCaptura = ultimaCap.HasValue ? ultimaCap.Value.ToString("dd/MM/yyyy") : null,
                        diasSinCaptura = ultimaCap.HasValue ? (int?)(DateTime.Now - ultimaCap.Value).Days : null,
                        actividad = actividadMeses,
                        estadoAlumnos = new { econ, acad, pers, noVul, sinInfo, conReprobadas = alumnosConReprobadas, bajasPeriodo },
                        canalizaciones = new
                        {
                            total = canalizacionesTutor.Count,
                            ultima = canalizacionesTutor.Any() ? canalizacionesTutor.First().Fecha.ToString("dd/MM/yyyy") : null,
                            porTipo = canalizacionesTutor.GroupBy(c => (c.Descripcion ?? "").Trim())
                                .Select(g => new { tipo = g.Key, n = g.Count() }).OrderByDescending(x => x.n).ToList()
                        },
                        alumnosSinSeguimiento = alumnosSinSeg,
                        alumnosDetalle,
                        historial
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.GetBaseException().Message });
            }
        }

        // ===== Vista del Alumno 360° (2026-08-02, propuesta de innovación) =====
        // Ficha única por alumno: datos, tutor, clasificación de vulnerabilidad con fuente,
        // materias reprobadas/extraordinario con intentos, línea de tiempo de seguimientos,
        // entrevista inicial y bajas. Fail-closed: nivel 3 solo alumnos de su carrera.

        [HttpPost]
        public ActionResult BuscarAlumno360(string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
                    return Json(new { success = true, data = new object[0] });
                string termino = q.Trim();
                Usuario usuario = Session["Usuario"] as Usuario;
                int? carreraFiltro = (usuario != null && usuario.IdNivel == 3) ? (int?)usuario.IdCarrera : null;

                var query = db.DatosPersonales.Where(a => a.Estado &&
                    (a.Matricula.Contains(termino) || a.Nombre.Contains(termino)));
                if (carreraFiltro.HasValue)
                    query = query.Where(a => a.IdCarrera == carreraFiltro.Value);

                var nombresCarrera360 = db.Carreras.ToList().ToDictionary(c => c.IdCarrera, c => (c.Nombre ?? "").Trim());
                // Proyección (no materializar la entidad completa: DatosPersonales tiene columnas
                // con desajuste de mapeo y truena el SELECT *).
                var coincidencias = query
                    .OrderByDescending(a => a.Año).ThenByDescending(a => a.IdPeriodo).ThenBy(a => a.Nombre)
                    .Select(a => new { a.IdPersona, a.Matricula, a.Nombre, a.IdCarrera })
                    .Take(10).ToList()
                    .Select(a => new
                    {
                        idPersona = a.IdPersona,
                        matricula = (a.Matricula ?? "").Trim(),
                        nombre = (a.Nombre ?? "").Trim(),
                        carrera = nombresCarrera360.ContainsKey(a.IdCarrera) ? nombresCarrera360[a.IdCarrera] : ""
                    }).ToList();
                return Json(new { success = true, data = coincidencias });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.GetBaseException().Message });
            }
        }

        [HttpPost]
        public ActionResult GetAlumno360(int idPersona)
        {
            try
            {
                db.Database.CommandTimeout = 300;
                // Proyección (ver BuscarAlumno360: la entidad completa truena por mapeo).
                var dp = db.DatosPersonales.Where(x => x.IdPersona == idPersona)
                    .Select(x => new { x.IdPersona, x.Matricula, x.Nombre, x.IdCarrera, x.Especialidad, x.IdGrado, x.IdGrupo, x.IdTurno, x.Sexo, x.Año, x.IdPeriodo })
                    .FirstOrDefault();
                if (dp == null)
                    return Json(new { success = false, error = "Alumno no encontrado." });
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario != null && usuario.IdNivel == 3 && dp.IdCarrera != usuario.IdCarrera)
                    return Json(new { success = false, error = "Sin acceso a alumnos de otra carrera." });

                var nomCarrera = db.Carreras.Where(c => c.IdCarrera == dp.IdCarrera).Select(c => c.Nombre).FirstOrDefault() ?? "";
                var nomGrado = db.Gradoes.Where(g => g.IdGrado == dp.IdGrado).Select(g => g.Nombre).FirstOrDefault() ?? "";
                var nomGrupo = db.Grupoes.Where(g => g.IdGrupo == dp.IdGrupo).Select(g => g.Nombre).FirstOrDefault() ?? "";
                var nomTurno = db.Turnoes.Where(t => t.IdTurno == dp.IdTurno).Select(t => t.Nombre).FirstOrDefault() ?? "";

                // Tutor asignado: mismo match que AsesorController.Grupo / Cumplimiento de Tutores.
                var asignacionTutor = db.TutoriaGrupals.FirstOrDefault(t =>
                    t.Año == dp.Año && t.IdPeriodo == dp.IdPeriodo && t.IdCarrera == dp.IdCarrera &&
                    t.IdGrado == dp.IdGrado && t.IdGrupo == dp.IdGrupo && t.IdTurno == dp.IdTurno);
                string tutorNombre = null;
                if (asignacionTutor != null)
                {
                    tutorNombre = db.Usuarios.Where(u => u.IdUsuario == asignacionTutor.IdUsuario)
                        .Select(u => u.NombreCompleto).FirstOrDefault();
                }

                // Línea de tiempo de seguimientos (más recientes primero, tope 60).
                var seguimientos = (from s in db.Seguimientoes
                                    join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                    where i.IdPersona == idPersona
                                    orderby s.Fecha descending
                                    select new { s.Fecha, s.Vulnerabilidad, s.Problematica, s.Accion })
                                   .Take(60).ToList()
                                   .Select(s => new
                                   {
                                       fecha = s.Fecha.ToString("dd/MM/yyyy"),
                                       vulnerabilidad = (s.Vulnerabilidad ?? "").Trim(),
                                       problematica = (s.Problematica ?? "").Trim(),
                                       accion = (s.Accion ?? "").Trim()
                                   }).ToList();

                var entrevista = db.EntrevistaInicials.Where(e => e.IdPersona == idPersona)
                    .OrderByDescending(e => e.Fecha)
                    .Select(e => new { e.Fecha, e.Vulnerable }).FirstOrDefault();

                // SQL crudo: la entidad EF de MateriasAlumno no tiene mapeo de tabla utilizable
                // (todo el módulo de Materias consulta con SQL directo, mismo patrón aquí).
                var materias = db.Database.SqlQuery<Alumno360MateriaRow>(@"
                        SELECT m.Nombre AS Materia, ma.Estado AS Estado,
                               ISNULL(ma.IntentosExtraordinarios, 0) AS Intentos
                        FROM MateriasAlumno ma
                        INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                        WHERE ma.IdPersona = @idPersona AND ma.Estado IN ('Reprobada', 'Extraordinario')",
                        new System.Data.SqlClient.SqlParameter("@idPersona", idPersona))
                    .ToList()
                    .Select(m => new { materia = (m.Materia ?? "").Trim(), estado = m.Estado, intentos = m.Intentos })
                    .OrderByDescending(m => m.intentos).ThenBy(m => m.materia).ToList();

                var bajas = db.Bajas.Where(b => b.IdPersona == idPersona)
                    .OrderByDescending(b => b.Fecha).ToList()
                    .Select(b => new { fecha = b.Fecha.ToString("dd/MM/yyyy"), causa = (b.Causa ?? "").Trim() }).ToList();

                // Canalizaciones del alumno (psicología / atención): a dónde se envió y en qué quedó.
                var canalizaciones = (from c in db.Canalizaciones
                                      join t in db.TipoCanalizaciones on c.IdTipoCanalizacion equals t.IdTipoCanalizacion
                                      where c.IdPersona == idPersona
                                      orderby c.Fecha descending
                                      select new { c.Fecha, t.Descripcion, c.MotivoCanalizacion, c.Status })
                                     .ToList()
                                     .Select(c => new
                                     {
                                         fecha = c.Fecha.ToString("dd/MM/yyyy"),
                                         tipo = (c.Descripcion ?? "").Trim(),
                                         motivo = (c.MotivoCanalizacion ?? "").Trim(),
                                         status = (c.Status ?? "").Trim()
                                     }).ToList();

                // Contexto de situación familiar (mismos criterios del Resumen Detallado:
                // AspectosPersonales.IdHijo==1 tiene hijos, IdEmbarazo==2 embarazada;
                // EntrevistaInicial.IdTrabajo==1 trabaja).
                var aspectosPers = db.AspectosPersonales.Where(ap => ap.IdPersona == idPersona)
                    .Select(ap => new { ap.IdHijo, ap.IdEmbarazo }).FirstOrDefault();
                bool tieneHijos = aspectosPers != null && aspectosPers.IdHijo == 1;
                bool estaEmbarazada = aspectosPers != null && aspectosPers.IdEmbarazo == 2;
                bool trabaja = db.EntrevistaInicials.Any(e => e.IdPersona == idPersona && e.IdTrabajo == 1);

                // Clasificación con fuente (misma cascada, simplificada al alumno): último seguimiento
                // con contenido del periodo vigente -> fuente tutor; si no, entrevista -> identificación;
                // si no, sin información.
                var periodo360 = PeriodoHelper.Obtener(DateTime.Now);
                var ultimoSegPeriodo = (from s in db.Seguimientoes
                                        join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                        where i.IdPersona == idPersona
                                           && s.Fecha >= periodo360.Inicio && s.Fecha <= periodo360.Fin
                                           && s.Vulnerabilidad != null && s.Vulnerabilidad.Trim() != ""
                                        orderby s.Fecha descending
                                        select s.Vulnerabilidad).FirstOrDefault();
                string clasificacion, fuente;
                if (!string.IsNullOrWhiteSpace(ultimoSegPeriodo))
                {
                    clasificacion = ultimoSegPeriodo.Trim();
                    fuente = "Seguimiento del tutor";
                }
                else if (entrevista != null && !string.IsNullOrWhiteSpace(entrevista.Vulnerable))
                {
                    clasificacion = entrevista.Vulnerable.Trim();
                    fuente = "Identificación (entrevista inicial)";
                }
                else
                {
                    clasificacion = "Sin información";
                    fuente = "Sin capturas";
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        idPersona = dp.IdPersona,
                        matricula = (dp.Matricula ?? "").Trim(),
                        nombre = (dp.Nombre ?? "").Trim(),
                        carrera = nomCarrera.Trim(),
                        especialidad = (dp.Especialidad ?? "").Trim(),
                        grupo = (nomGrado + nomGrupo).Trim(),
                        turno = nomTurno.Trim(),
                        sexo = (dp.Sexo ?? "").Trim(),
                        periodoAlumno = dp.Año + " · periodo " + dp.IdPeriodo,
                        tutor = (tutorNombre ?? "").Trim(),
                        clasificacion,
                        fuente,
                        entrevista = entrevista == null ? null : new { fecha = entrevista.Fecha.ToString("dd/MM/yyyy"), vulnerable = (entrevista.Vulnerable ?? "").Trim() },
                        situacionFamiliar = new { tieneHijos, embarazada = estaEmbarazada, trabaja },
                        materias,
                        seguimientos,
                        bajas,
                        canalizaciones
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.GetBaseException().Message });
            }
        }

        // Método AJAX para obtener estadísticas de PATs bajo demanda
        [HttpPost]
        public JsonResult GetEstadisticasPATs()
        {
            try
            {
                // Obtener usuario de la sesión
                Usuario usuario = Session["Usuario"] as Usuario;
                int? carreraIdFiltro = (usuario != null && usuario.IdNivel == 3) ? (int?)usuario.IdCarrera : null;
                var estadisticasPATs = CalcularEstadisticasPATs(carreraIdFiltro);
                return Json(new { success = true, data = estadisticasPATs });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Método AJAX para obtener estadísticas de arrastre bajo demanda
        [HttpPost]
        public JsonResult GetEstadisticasArrastre()
        {
            try
            {
                // Obtener usuario de la sesión
                Usuario usuario = Session["Usuario"] as Usuario;
                int? carreraIdFiltro = (usuario != null && usuario.IdNivel == 3) ? (int?)usuario.IdCarrera : null;
                var estadisticasArrastre = CalcularEstadisticasArrastre(carreraIdFiltro);
                return Json(new { success = true, data = estadisticasArrastre });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Coincide con BajasController.GetCategoriasBaja (Value) para etiquetas canónicas en gráficas / reportes
        private static readonly string[] CategoriasCausaBajaOficiales =
        {
            "Académica",
            "Deserción",
            "Reprobación",
            "Problemas Económicos",
            "Motivos Personales",
            "Cambio de UTT",
            "Cambio de carrera",
            "Faltas al Reglamento Escolar",
            "Otras"
        };

        // Alineado con fallback de BajasController.GetListaVulnerabilidades + variantes frecuentes en BD
        private static readonly string[] EtiquetasVulnerabilidadBajaOficiales =
        {
            "Económico",
            "Académico",
            "Personal",
            "No vulnerable",
            "No aplica"
        };

        private const string ClaveAgrupacionSinCausa = "__sin_causa__";

        private string ClaveAgrupacionTextoBaja(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return ClaveAgrupacionSinCausa;
            return NormalizarSinAcentos(texto.Trim()).ToLowerInvariant();
        }

        private string ResolverEtiquetaCausaBaja(string claveNorm, List<string> textosEnGrupo)
        {
            if (claveNorm == ClaveAgrupacionSinCausa)
                return "Sin causa";

            foreach (var oficial in CategoriasCausaBajaOficiales)
            {
                if (ClaveAgrupacionTextoBaja(oficial) == claveNorm)
                    return oficial;
            }

            var modo = textosEnGrupo
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .GroupBy(c => c.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(gg => gg.Count())
                .Select(gg => gg.Key)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(modo) ? "Sin causa" : modo;
        }

        private string ResolverEtiquetaVulnerabilidadBaja(string claveNorm, List<string> textosEnGrupo)
        {
            foreach (var oficial in EtiquetasVulnerabilidadBajaOficiales)
            {
                if (ClaveAgrupacionTextoBaja(oficial) == claveNorm)
                    return oficial;
            }

            var modo = textosEnGrupo
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .GroupBy(c => c.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(gg => gg.Count())
                .Select(gg => gg.Key)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(modo) ? "Sin etiqueta" : modo;
        }

        // Método para calcular estadísticas de bajas
        // El parámetro carreraId representa el IdCarrera proveniente del módulo de Tutorias
        private EstadisticaBaja CalcularEstadisticasBajas(int? carreraId = null, string causa = null, int? mes = null, int? especialidadId = null, int? gradoId = null, int? grupoId = null)
        {
            try
            {
                // Aumentar timeout para esta consulta
                db.Database.CommandTimeout = 300;

                var estadistica = new EstadisticaBaja
                {
                    BajasPorCausa = new Dictionary<string, int>(),
                    BajasPorCarrera = new Dictionary<string, int>(),
                    BajasPorVulnerabilidad = new Dictionary<string, int>(),
                    BajasPorEspecialidad = new Dictionary<string, int>()
                };

                // Obtener todas las bajas con información de carrera
                // Optimizar: filtrar por carrera antes del join si es posible
                var queryBajas = from b in db.Bajas
                                 join p in db.DatosPersonales on b.IdPersona equals p.IdPersona
                                 join c in db.Carreras on p.IdCarrera equals c.IdCarrera
                                 select new { Baja = b, CarreraNombre = c.Nombre, IdCarrera = p.IdCarrera, p.Especialidad, p.IdGrado, p.IdGrupo };

                // Si se especifica una carrera, filtrar por ella (usando IdCarrera de Tutorias)
                if (carreraId.HasValue)
                {
                    queryBajas = queryBajas.Where(x => x.IdCarrera == carreraId.Value);
                }

                // Periodo vigente + reinicio por corte: contar solo bajas del cuatrimestre actual
                // (o, si hubo corte, solo las posteriores al corte).
                var periodoBajas = PeriodoHelper.Obtener(DateTime.Now);
                var corteBajas = CorteAplicable(periodoBajas.Inicio, periodoBajas.Fin);
                DateTime limiteInferiorBajas = corteBajas ?? periodoBajas.Inicio;
                queryBajas = queryBajas.Where(x => x.Baja.Fecha >= limiteInferiorBajas && x.Baja.Fecha <= periodoBajas.Fin);

                var bajasConCarrera = queryBajas.ToList();

                // Filtros por seccion (2026-07-29): especialidad (texto vs catalogo), grado y grupo del alumno.
                if (especialidadId.HasValue)
                {
                    var espBaja = tutoriasDb.Especialidads.Find(especialidadId.Value);
                    if (espBaja != null && !string.IsNullOrEmpty(espBaja.Nombre))
                    {
                        string espBajaNorm = NormalizarSinAcentos(espBaja.Nombre.Trim());
                        bajasConCarrera = bajasConCarrera.Where(x => !string.IsNullOrWhiteSpace(x.Especialidad)
                            && NormalizarSinAcentos(x.Especialidad.Trim()).Equals(espBajaNorm, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }
                if (gradoId.HasValue)
                    bajasConCarrera = bajasConCarrera.Where(x => x.IdGrado == gradoId.Value).ToList();
                if (grupoId.HasValue)
                    bajasConCarrera = bajasConCarrera.Where(x => x.IdGrupo == grupoId.Value).ToList();

                // Filtros por seccion (direccion 2026-07-16): causa (etiqueta canonica) y mes.
                if (!string.IsNullOrWhiteSpace(causa))
                {
                    var claveCausa = ClaveAgrupacionTextoBaja(causa);
                    bajasConCarrera = bajasConCarrera.Where(x => ClaveAgrupacionTextoBaja(x.Baja.Causa) == claveCausa).ToList();
                }
                if (mes.HasValue)
                    bajasConCarrera = bajasConCarrera.Where(x => x.Baja.Fecha != null && x.Baja.Fecha.Month == mes.Value).ToList();

                estadistica.TotalBajas = bajasConCarrera.Count;

                // Estadísticas por causa (una fila por variante de texto: mayúsculas, acentos, espacios)
                foreach (var g in bajasConCarrera.GroupBy(x => ClaveAgrupacionTextoBaja(x.Baja.Causa)))
                {
                    var etiqueta = ResolverEtiquetaCausaBaja(g.Key, g.Select(x => x.Baja.Causa).ToList());
                    estadistica.BajasPorCausa[etiqueta] = g.Count();
                }

                // Estadísticas por carrera
                var bajasPorCarrera = bajasConCarrera
                    .GroupBy(x => x.CarreraNombre)
                    .Select(g => new { Carrera = g.Key, Cantidad = g.Count() })
                    .ToList();

                foreach (var item in bajasPorCarrera)
                {
                    estadistica.BajasPorCarrera[item.Carrera] = item.Cantidad;
                }

                // Estadísticas por especialidad del alumno (2026-08-01, sección Bajas del dashboard).
                // dp.Especialidad es texto libre: se agrupa normalizado sin acentos y se muestra la
                // variante más frecuente; sin especialidad capturada => fuera del desglose.
                foreach (var g in bajasConCarrera
                             .Where(x => !string.IsNullOrWhiteSpace(x.Especialidad))
                             .GroupBy(x => NormalizarSinAcentos(x.Especialidad.Trim()).ToUpperInvariant()))
                {
                    var etiquetaEsp = g.GroupBy(x => x.Especialidad.Trim())
                                       .OrderByDescending(v => v.Count())
                                       .First().Key;
                    estadistica.BajasPorEspecialidad[etiquetaEsp] = g.Count();
                }

                // Estadísticas por vulnerabilidad (misma normalización que causas)
                foreach (var g in bajasConCarrera
                             .Where(x => !string.IsNullOrWhiteSpace(x.Baja.Vulnerabilidad))
                             .GroupBy(x => ClaveAgrupacionTextoBaja(x.Baja.Vulnerabilidad)))
                {
                    var etiqueta = ResolverEtiquetaVulnerabilidadBaja(g.Key, g.Select(x => x.Baja.Vulnerabilidad).ToList());
                    estadistica.BajasPorVulnerabilidad[etiqueta] = g.Count();
                }

                return estadistica;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculando estadísticas de bajas: {ex.Message}");
                return new EstadisticaBaja
                {
                    BajasPorCausa = new Dictionary<string, int>(),
                    BajasPorCarrera = new Dictionary<string, int>(),
                    BajasPorVulnerabilidad = new Dictionary<string, int>(),
                    BajasPorEspecialidad = new Dictionary<string, int>()
                };
            }
        }

        // Método para calcular estadísticas de PATs (IdCarrera de Tutorias)
        private EstadisticaPAT CalcularEstadisticasPATs(int? carreraId = null)
        {
            try
            {
                // Aumentar timeout para esta consulta
                db.Database.CommandTimeout = 300;

                var estadistica = new EstadisticaPAT
                {
                    PATsPorEstado = new Dictionary<string, int>(),
                    PATsPorCarrera = new Dictionary<string, int>()
                };

                // Obtener año y período actuales
                int añoActual = DateTime.Now.Year;
                int periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);

                // Obtener PATs del período actual (filtrar por IdPeriodo y año)
                var queryPats = db.PATs
                    .Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == añoActual);

                // Si se especifica una carrera, filtrar por ella (usando IdCarrera de Tutorias)
                if (carreraId.HasValue)
                {
                    queryPats = queryPats.Where(p => p.IdCarrera == carreraId.Value);
                }

                // Reinicio por corte: si hubo corte en el cuatrimestre vigente, contar solo PATs posteriores.
                var periodoPats = PeriodoHelper.Obtener(DateTime.Now);
                var cortePats = CorteAplicable(periodoPats.Inicio, periodoPats.Fin);
                if (cortePats.HasValue)
                {
                    queryPats = queryPats.Where(p => p.Fecha >= cortePats.Value);
                }

                var pats = queryPats.ToList();

                estadistica.TotalPATs = pats.Count;

                // Lógica corregida según la consulta SQL proporcionada
                // Aprobados: (EstadoRevision = 2 OR estado = 0)
                estadistica.Aprobados = pats.Count(p => (p.EstadoRevision == 2 || p.estado == false));

                // En Progreso: estado = 1 AND (EstadoRevision = 0 OR EstadoRevision = 3)
                estadistica.EnProgreso = pats.Count(p => p.estado == true && (p.EstadoRevision == 0 || p.EstadoRevision == 3));

                // Pendientes de Revisión: estado = 1 AND EstadoRevision = 1
                estadistica.PendientesRevision = pats.Count(p => p.estado == true && p.EstadoRevision == 1);

                // Estadísticas por estado
                estadistica.PATsPorEstado["Aprobados"] = estadistica.Aprobados;
                estadistica.PATsPorEstado["En Progreso"] = estadistica.EnProgreso;
                estadistica.PATsPorEstado["Pendientes de Revisión"] = estadistica.PendientesRevision;

                // Estadísticas por carrera
                var patsConCarrera = (from p in pats
                                      join c in db.Carreras on p.IdCarrera equals c.IdCarrera into carreraJoin
                                      from c in carreraJoin.DefaultIfEmpty()
                                      select new { PAT = p, CarreraNombre = c != null ? c.Nombre : "Sin carrera" })
                                     .ToList();

                var patsPorCarrera = patsConCarrera
                    .GroupBy(x => x.CarreraNombre)
                    .Select(g => new { Carrera = g.Key, Cantidad = g.Count() })
                    .ToList();

                foreach (var item in patsPorCarrera)
                {
                    estadistica.PATsPorCarrera[item.Carrera] = item.Cantidad;
                }

                return estadistica;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculando estadísticas de PATs: {ex.Message}");
                return new EstadisticaPAT
                {
                    PATsPorEstado = new Dictionary<string, int>(),
                    PATsPorCarrera = new Dictionary<string, int>()
                };
            }
        }

        // Clase auxiliar para mapear resultados de arrastre
        private class ArrastreResultado
        {
            public int IdPersona { get; set; }
            public string CarreraNombre { get; set; }
            public DateTime? FechaInicioArrastre { get; set; }
        }

        // Método para calcular estadísticas de arrastre (IdCarrera de Tutorias)
        private EstadisticaArrastre CalcularEstadisticasArrastre(int? carreraId = null)
        {
            try
            {
                // Aumentar timeout para esta consulta
                db.Database.CommandTimeout = 300;

                var estadistica = new EstadisticaArrastre
                {
                    ArrastrePorCarrera = new Dictionary<string, int>(),
                    ArrastrePorEstado = new Dictionary<string, int>()
                };

                // Usar SQL directo para acceder a MateriasAlumno (no está en DbSet)
                // Optimizar: agregar filtro de carrera directamente en la consulta
                // NOTA: se eliminó el filtro de especialidad (e.Nombre = dp.Especialidad)
                // para mantener consistencia con las vistas de tutor/coordinador, que
                // ahora muestran todos los registros (incluyendo duplicados por especialidad).
                // Se mantiene el filtro Estado = 'Reprobada' para que el KPI refleje
                // únicamente arrastres y NO extraordinarios.
                var consulta = @"
                    SELECT
                        dp.IdPersona,
                        c.Nombre as CarreraNombre,
                        ma.FechaInicioArrastre
                    FROM DatosPersonales dp
                    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
                    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                    INNER JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
                    WHERE ma.Estado = 'Reprobada'
                      AND dp.Estado = 1
                      AND m.IdCarrera = dp.IdCarrera";

                // Si se especifica una carrera, agregar filtro (usando IdCarrera de Tutorias)
                List<ArrastreResultado> materiasArrastre;
                if (carreraId.HasValue)
                {
                    consulta += " AND dp.IdCarrera = @carreraId";
                    var parametros = new object[] { new System.Data.SqlClient.SqlParameter("@carreraId", carreraId.Value) };
                    materiasArrastre = db.Database.SqlQuery<ArrastreResultado>(consulta, parametros).ToList();
                }
                else
                {
                    materiasArrastre = db.Database.SqlQuery<ArrastreResultado>(consulta).ToList();
                }

                estadistica.TotalMateriasArrastre = materiasArrastre.Count;
                estadistica.TotalAlumnosConArrastre = materiasArrastre.Select(x => x.IdPersona).Distinct().Count();

                // Calcular estados según días restantes
                foreach (var item in materiasArrastre)
                {
                    if (item.FechaInicioArrastre.HasValue)
                    {
                        var fechaLimite = item.FechaInicioArrastre.Value.AddMonths(8);
                        var diasRestantes = (fechaLimite - DateTime.Now).Days;

                        if (diasRestantes <= 0)
                            estadistica.FueraDeTiempo++;
                        else if (diasRestantes <= 60)
                            estadistica.Criticos++;
                        else if (diasRestantes <= 180)
                            estadistica.Medios++;
                        else
                            estadistica.EnTiempo++;
                    }
                    else
                    {
                        estadistica.FueraDeTiempo++; // Sin fecha se considera fuera de tiempo
                    }
                }

                // Estadísticas por estado
                estadistica.ArrastrePorEstado["Fuera de Tiempo"] = estadistica.FueraDeTiempo;
                estadistica.ArrastrePorEstado["Crítico"] = estadistica.Criticos;
                estadistica.ArrastrePorEstado["Medio"] = estadistica.Medios;
                estadistica.ArrastrePorEstado["En Tiempo"] = estadistica.EnTiempo;

                // Estadísticas por carrera
                var arrastrePorCarrera = materiasArrastre
                    .GroupBy(x => x.CarreraNombre)
                    .Select(g => new { Carrera = g.Key, Cantidad = g.Count() })
                    .ToList();

                foreach (var item in arrastrePorCarrera)
                {
                    estadistica.ArrastrePorCarrera[item.Carrera] = item.Cantidad;
                }

                return estadistica;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculando estadísticas de arrastre: {ex.Message}");
                return new EstadisticaArrastre
                {
                    ArrastrePorCarrera = new Dictionary<string, int>(),
                    ArrastrePorEstado = new Dictionary<string, int>()
                };
            }
        }

        // Exportación completa: ZIP con los mismos Excel que las exportaciones individuales (sin duplicar lógica)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportarModuloCompleto(
            bool incluirBajasResumen = false,
            bool incluirBajasNivel = false,
            bool incluirBajasCierre = false,
            bool incluirBajasCarrera = false,
            bool incluirBajasVulnerabilidad = false,
            int? mes = null,
            int? especialidadId = null,
            int? carreraIdResumen = null,
            int? carreraIdGrupo = null,
            int? grupoId = null,
            int? gradoId = null,
            string sexo = null)
        {
            try
            {
                usuariosDb.Database.CommandTimeout = 300;
                tutoriasDb.Database.CommandTimeout = 300;
                db.Database.CommandTimeout = 300;

                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    TempData["ExportError"] = "Sesión expirada. Vuelva a iniciar sesión.";
                    return RedirectToAction("SeguimientoEstadisticas");
                }

                var detalladas = TryBuildEstadisticasDetalladasExcel(usuario, especialidadId, carreraIdResumen, mes, null, null, incluirBajasResumen, grupoId, gradoId, sexo);
                if (!detalladas.Ok)
                {
                    TempData["ExportError"] = detalladas.Error;
                    return RedirectToAction("SeguimientoEstadisticas");
                }

                using (var zipStream = new MemoryStream())
                {
                    using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        AgregarExcelAlZip(zip, detalladas);
                        AgregarExcelAlZip(zip, TryBuildVulnerabilidadesExcel(usuario, mes, incluirBajasVulnerabilidad));
                        AgregarExcelAlZip(zip, TryBuildEstadisticasPorNivelExcel(usuario, incluirBajasNivel));
                        AgregarExcelAlZip(zip, TryBuildCierreCuatrimestresExcel(usuario, null, incluirBajasCierre));
                        AgregarExcelAlZip(zip, TryBuildEstadisticasPorCarreraExcel(usuario, incluirBajasCarrera));
                        AgregarExcelAlZip(zip, TryBuildEstadisticasPorGrupoExcel(usuario, carreraIdGrupo));
                    }

                    var zipName = $"EstadisticasModulo_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                    return File(zipStream.ToArray(), "application/zip", zipName);
                }
            }
            catch (Exception ex)
            {
                TempData["ExportError"] = "Error al exportar el módulo: " + ex.Message;
                return RedirectToAction("SeguimientoEstadisticas");
            }
        }

        // Estadísticas por materia reprobada: qué materia, cuántos alumnos, cuántos intentos.
        // Requerimiento dirección 2026-07. incluirAlumnos=false para el snapshot del corte
        // (el histórico guarda el agregado, no el detalle nominal).
        [HttpPost]
        public ActionResult GetEstadisticasMaterias(int? carreraId = null, int? corteId = null, bool incluirAlumnos = true, int? especialidadId = null, string materiaNombre = null, string estado = null, string intentos = null, string agruparPor = "materia", int? gradoId = null, int? grupoId = null)
        {
            try
            {
                // Fail-closed (mismo espíritu que SoloLecturaDirector): Coordinador/Director (nivel 3)
                // solo ven SU carrera; se ignora el carreraId que venga del cliente. usuario.IdCarrera
                // está en el espacio de Tutorias.Carreras, el mismo que dp.IdCarrera (sin mapear a área).
                // Nivel 3 con 1 sola carrera: agrupar por carrera no tiene sentido — forzar "materia".
                var usuarioSesion = Session["Usuario"] as Usuario;
                if (usuarioSesion != null && usuarioSesion.IdNivel == 3)
                {
                    carreraId = usuarioSesion.IdCarrera;
                    agruparPor = "materia";
                }

                if (corteId.HasValue)
                {
                    var hist = ServirSeccionHistorico(corteId.Value, "MateriasReprobadas");
                    if (hist != null) return hist;
                    return Json(new { success = false, noDisponible = true, error = "Sección no disponible en este corte." });
                }

                db.Database.CommandTimeout = 300;

                var consulta = @"
                    SELECT
                        m.Nombre               AS NombreMateria,
                        dp.Matricula           AS Matricula,
                        ISNULL(dp.Nombre, dp.Matricula) AS NombreAlumno,
                        ma.Estado              AS Estado,
                        ma.IntentosExtraordinarios AS IntentosExtraordinarios,
                        dp.IdCarrera           AS IdCarrera,
                        c.Nombre               AS CarreraNombre,
                        dp.Especialidad        AS EspecialidadAlumno,
                        e.Nombre               AS EspecialidadMateria
                    FROM DatosPersonales dp
                    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
                    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                    INNER JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
                    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
                    WHERE ma.Estado IN ('Reprobada', 'Extraordinario')
                      AND dp.Estado = 1
                      AND m.IdCarrera = dp.IdCarrera";

                List<MateriaReprobadaRow> filas;
                if (carreraId.HasValue)
                {
                    consulta += " AND dp.IdCarrera = @carreraId";
                    filas = db.Database.SqlQuery<MateriaReprobadaRow>(consulta,
                        new System.Data.SqlClient.SqlParameter("@carreraId", carreraId.Value)).ToList();
                }
                else
                {
                    filas = db.Database.SqlQuery<MateriaReprobadaRow>(consulta).ToList();
                }

                // Filtro por especialidad del ALUMNO (direccion 2026-07-16): las filas ya traen
                // dp.Especialidad; se compara sin acentos contra el nombre del catalogo.
                if (especialidadId.HasValue)
                {
                    var espMat = tutoriasDb.Especialidads.Find(especialidadId.Value);
                    if (espMat != null && !string.IsNullOrEmpty(espMat.Nombre))
                    {
                        string espMatNorm = NormalizarSinAcentos(espMat.Nombre.Trim());
                        filas = filas.Where(fx => !string.IsNullOrWhiteSpace(fx.EspecialidadAlumno)
                            && NormalizarSinAcentos(fx.EspecialidadAlumno.Trim()).Equals(espMatNorm, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }

                // El catalogo Materias tiene la misma materia una vez POR ESPECIALIDAD (mismo
                // Nombre, distinto IdMateria) y la insercion masiva inscribio a alumnos en varias
                // copias. NO se borran registros (la limpieza es de los tutores): una sola fila
                // por alumno+materia PREFIRIENDO la copia cuya especialidad coincide con la del
                // alumno (recomendacion del profe); si ninguna coincide (texto de especialidad
                // capturado distinto), se conserva al alumno con su fila mas avanzada.
                filas = filas
                    .GroupBy(f => new { Mat = (f.Matricula ?? "").ToUpperInvariant(), f.NombreMateria, f.CarreraNombre })
                    .Select(g => g.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.EspecialidadAlumno)
                                                          && !string.IsNullOrWhiteSpace(x.EspecialidadMateria)
                                                          && NormalizarSinAcentos(x.EspecialidadAlumno.Trim())
                                                             .Equals(NormalizarSinAcentos(x.EspecialidadMateria.Trim()), StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                                  .ThenByDescending(x => x.IntentosExtraordinarios)
                                  .ThenByDescending(x => x.Estado == "Extraordinario" ? 1 : 0)
                                  .First())
                    .ToList();

                // Filtro por grado/grupo del alumno (2026-07-29): mismo helper de poblacion que las demas secciones.
                if (gradoId.HasValue || grupoId.HasValue)
                {
                    var matsMaterias = filas.Select(f => NormalizarMatricula(f.Matricula))
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var matsGG = new HashSet<string>(
                        FiltrarMatriculasPorGrupoGrado(matsMaterias, grupoId, gradoId),
                        StringComparer.OrdinalIgnoreCase);
                    filas = filas.Where(f => matsGG.Contains(NormalizarMatricula(f.Matricula))).ToList();
                }

                // Filtros por seccion (direccion 2026-07-16): materia exacta, estado y nº de intentos.
                if (!string.IsNullOrWhiteSpace(materiaNombre))
                    filas = filas.Where(fx => string.Equals((fx.NombreMateria ?? "").Trim(), materiaNombre.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (estado == "Reprobada" || estado == "Extraordinario")
                    filas = filas.Where(fx => fx.Estado == estado).ToList();
                if (!string.IsNullOrWhiteSpace(intentos))
                {
                    if (intentos == "3+") filas = filas.Where(fx => fx.IntentosExtraordinarios >= 3).ToList();
                    else { int nInt; if (int.TryParse(intentos, out nInt)) filas = filas.Where(fx => fx.IntentosExtraordinarios == nInt).ToList(); }
                }

                var materias = filas
                    .GroupBy(f => new { f.NombreMateria, f.CarreraNombre })
                    .Select(g => new
                    {
                        nombreMateria = g.Key.NombreMateria,
                        carreraNombre = g.Key.CarreraNombre,
                        totalRegistros = g.Count(),
                        alumnosDistintos = g.Select(x => x.Matricula).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        reprobadas = g.Count(x => x.Estado == "Reprobada"),
                        extraordinarios = g.Count(x => x.Estado == "Extraordinario"),
                        intentos0 = g.Count(x => x.IntentosExtraordinarios == 0),
                        intentos1 = g.Count(x => x.IntentosExtraordinarios == 1),
                        intentos2 = g.Count(x => x.IntentosExtraordinarios == 2),
                        intentos3Mas = g.Count(x => x.IntentosExtraordinarios >= 3),
                        alumnos = incluirAlumnos
                            ? g.OrderByDescending(x => x.IntentosExtraordinarios)
                               .Select(x => new { matricula = x.Matricula, nombreAlumno = x.NombreAlumno, estado = x.Estado, intentos = x.IntentosExtraordinarios })
                               .ToList<object>()
                            : new List<object>()
                    })
                    .OrderByDescending(x => x.totalRegistros)
                    .ThenBy(x => x.nombreMateria)
                    .ToList();

                // Reagrupar por carrera si la directora lo solicita (ranking de carreras con más reprobados).
                // El shape JSON se mantiene idéntico: nombreMateria=carreraNombre, carreraNombre="", alumnos=[].
                // alumnosDistintos es una SUMA por materia (un alumno con varias materias cuenta varias veces)
                // — se advierte en el tooltip del select en la vista.
                object materiasJson = materias;
                if (agruparPor == "carrera")
                {
                    materiasJson = materias
                        .GroupBy(m => m.carreraNombre)
                        .Select(g => new
                        {
                            nombreMateria    = g.Key,
                            carreraNombre    = "",
                            totalRegistros   = g.Sum(x => x.totalRegistros),
                            alumnosDistintos = g.Sum(x => x.alumnosDistintos),
                            reprobadas       = g.Sum(x => x.reprobadas),
                            extraordinarios  = g.Sum(x => x.extraordinarios),
                            intentos0        = g.Sum(x => x.intentos0),
                            intentos1        = g.Sum(x => x.intentos1),
                            intentos2        = g.Sum(x => x.intentos2),
                            intentos3Mas     = g.Sum(x => x.intentos3Mas),
                            alumnos          = new List<object>()
                        })
                        .OrderByDescending(x => x.reprobadas + x.extraordinarios)
                        .ThenByDescending(x => x.alumnosDistintos)
                        .ToList();
                }

                var data = new
                {
                    materias = materiasJson,
                    agruparPor,
                    totalMateriasDistintas = materias.Count,
                    totalAlumnosAfectados = filas.Select(f => f.Matricula).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    incluyeAlumnos = incluirAlumnos
                };

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Detalle plano de registros reprobados/extraordinario para las secciones REPROBADOS,
        // EXTRAORDINARIOS y la tabla de alumnos (2026-07-29). MISMA consulta y dedupe que
        // GetEstadisticasMaterias (los numeros cuadran con esa seccion) + IdGrado/IdGrupo del
        // registro de inscripcion. Sin snapshot de corte: seccion solo en vivo.
        [HttpPost]
        public ActionResult GetReprobadosDetalle(int? carreraId = null, int? especialidadId = null, int? gradoId = null, int? grupoId = null)
        {
            try
            {
                // Fail-closed nivel 3: solo SU carrera (usuario.IdCarrera esta en el espacio de
                // Tutorias.Carreras, el mismo que dp.IdCarrera).
                var usuarioSesion = Session["Usuario"] as Usuario;
                if (usuarioSesion != null && usuarioSesion.IdNivel == 3)
                {
                    carreraId = usuarioSesion.IdCarrera;
                }

                db.Database.CommandTimeout = 300;

                var consulta = @"
                    SELECT
                        m.Nombre               AS NombreMateria,
                        dp.Matricula           AS Matricula,
                        ISNULL(dp.Nombre, dp.Matricula) AS NombreAlumno,
                        ma.Estado              AS Estado,
                        ma.IntentosExtraordinarios AS IntentosExtraordinarios,
                        dp.IdCarrera           AS IdCarrera,
                        c.Nombre               AS CarreraNombre,
                        dp.Especialidad        AS EspecialidadAlumno,
                        e.Nombre               AS EspecialidadMateria,
                        dp.IdGrado             AS IdGrado,
                        dp.IdGrupo             AS IdGrupo
                    FROM DatosPersonales dp
                    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
                    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                    INNER JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
                    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
                    WHERE ma.Estado IN ('Reprobada', 'Extraordinario')
                      AND dp.Estado = 1
                      AND m.IdCarrera = dp.IdCarrera";

                List<ReprobadoDetalleRow> filas;
                if (carreraId.HasValue)
                {
                    consulta += " AND dp.IdCarrera = @carreraId";
                    filas = db.Database.SqlQuery<ReprobadoDetalleRow>(consulta,
                        new System.Data.SqlClient.SqlParameter("@carreraId", carreraId.Value)).ToList();
                }
                else
                {
                    filas = db.Database.SqlQuery<ReprobadoDetalleRow>(consulta).ToList();
                }

                // Filtro por especialidad del ALUMNO (mismo patron que GetEstadisticasMaterias).
                if (especialidadId.HasValue)
                {
                    var espDet = tutoriasDb.Especialidads.Find(especialidadId.Value);
                    if (espDet != null && !string.IsNullOrEmpty(espDet.Nombre))
                    {
                        string espDetNorm = NormalizarSinAcentos(espDet.Nombre.Trim());
                        filas = filas.Where(fx => !string.IsNullOrWhiteSpace(fx.EspecialidadAlumno)
                            && NormalizarSinAcentos(fx.EspecialidadAlumno.Trim()).Equals(espDetNorm, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }

                // Filtro por grado/grupo del registro de inscripcion (mismo criterio que Bajas).
                if (gradoId.HasValue)
                    filas = filas.Where(fx => fx.IdGrado == gradoId.Value).ToList();
                if (grupoId.HasValue)
                    filas = filas.Where(fx => fx.IdGrupo == grupoId.Value).ToList();

                // Dedupe por alumno+materia PREFIRIENDO la copia cuya especialidad coincide con la
                // del alumno; sin match se conserva la fila mas avanzada (regla del profe, 2026-07-16).
                filas = filas
                    .GroupBy(f => new { Mat = (f.Matricula ?? "").ToUpperInvariant(), f.NombreMateria, f.CarreraNombre })
                    .Select(g => g.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.EspecialidadAlumno)
                                                          && !string.IsNullOrWhiteSpace(x.EspecialidadMateria)
                                                          && NormalizarSinAcentos(x.EspecialidadAlumno.Trim())
                                                             .Equals(NormalizarSinAcentos(x.EspecialidadMateria.Trim()), StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                                  .ThenByDescending(x => x.IntentosExtraordinarios)
                                  .ThenByDescending(x => x.Estado == "Extraordinario" ? 1 : 0)
                                  .First())
                    .ToList();

                var registros = filas.Select(f => new
                {
                    matricula = f.Matricula,
                    nombreAlumno = f.NombreAlumno,
                    materia = f.NombreMateria,
                    estado = f.Estado,
                    intentos = f.IntentosExtraordinarios,
                    carreraNombre = f.CarreraNombre,
                    especialidad = f.EspecialidadAlumno,
                    idGrado = f.IdGrado,
                    idGrupo = f.IdGrupo
                }).ToList();

                return Json(new { success = true, data = new { registros } });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Lista nominal de una categoria de vulnerabilidad (modal de las tarjetas).
        // Reusa la MISMA poblacion (ObtenerPoblacionResumen) y la MISMA cascada
        // (CalcularVulnerabilidades + detalleOut) que pintan las tarjetas.
        [HttpPost]
        public ActionResult GetAlumnosVulnerabilidad(string categoria, int? especialidadId = null, int? carreraId = null, int? mes = null, int? año = null, int? periodo = null, bool incluirBajas = false, int? grupoId = null, int? gradoId = null, string sexo = null)
        {
            try
            {
                var usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, error = "Sesión expirada" });
                if (mes.HasValue && periodo.HasValue)
                    return Json(new { success = false, error = "Mes y período no pueden usarse juntos." });

                // Fail-closed nivel 3 (mismo espiritu que GetEstadisticasMaterias): ObtenerPoblacionResumen
                // ya restringe por usuario.IdCarrera cuando IdNivel==3 e ignora carreraId ajeno.
                var poblacion = ObtenerPoblacionResumen(usuario, especialidadId, carreraId, incluirBajas, grupoId, gradoId, sexo);

                // Misma normalizacion de filtros que CalcularResumenDetalladoDatos (L3736-3745)
                int añoParaFiltro = DateTime.Now.Year;
                int? mesParaFiltro = mes;
                if (!mes.HasValue && !periodo.HasValue)
                    mesParaFiltro = DateTime.Now.Month;
                int? idAreaCoordinadorFiltro = null;
                if (usuario.IdNivel == 3)
                    idAreaCoordinadorFiltro = MapearIdCarreraCoordinadorAIdArea(usuario.IdCarrera);

                var detalle = new List<AlumnoVulnDetalle>();
                CalcularVulnerabilidades(poblacion, idAreaCoordinadorFiltro, carreraId, null, mesParaFiltro, añoParaFiltro, periodo, detalle);

                Func<AlumnoVulnDetalle, bool> filtro;
                switch ((categoria ?? "").Trim())
                {
                    case "economicos":     filtro = d => d.Econ; break;
                    case "academicos":     filtro = d => d.Acad; break;
                    case "personales":     filtro = d => d.Pers; break;
                    case "vulnerables":    filtro = d => d.Econ || d.Acad || d.Pers; break;
                    case "noVulnerables":  filtro = d => d.NoVul; break;
                    case "sinInformacion": filtro = d => d.SinInfo; break;
                    default: return Json(new { success = false, error = "Categoría desconocida." });
                }
                var seleccion = detalle.Where(filtro).ToList();

                // Nombre y especialidad: registro mas reciente de DatosPersonales por matricula
                var matriculasSel = new HashSet<string>(seleccion.Select(s => s.Matricula.Trim()), StringComparer.OrdinalIgnoreCase);
                var datos = tutoriasDb.DatosPersonales
                    .Where(d => d.Matricula != null)
                    .Select(d => new { d.Matricula, d.Nombre, d.Especialidad, Fecha = (DateTime?)d.Fecha })
                    .ToList()
                    .Where(d => !string.IsNullOrWhiteSpace(d.Matricula) && matriculasSel.Contains(d.Matricula.Trim()))
                    .GroupBy(d => d.Matricula.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Fecha ?? DateTime.MinValue).First(), StringComparer.OrdinalIgnoreCase);

                var alumnos = seleccion
                    .Select(s =>
                    {
                        var m = s.Matricula.Trim();
                        var d = datos.ContainsKey(m) ? datos[m] : null;
                        return new
                        {
                            matricula = m,
                            nombre = d != null ? (d.Nombre ?? "") : "",
                            especialidad = d != null ? (d.Especialidad ?? "") : "",
                            fuente = s.SinInfo ? (string)null : (s.PorIdentificacion ? "identificacion" : "seguimiento")
                        };
                    })
                    .OrderBy(a => a.nombre).ThenBy(a => a.matricula)
                    .ToList();

                return Json(new { success = true, categoria, total = alumnos.Count, alumnos });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Resultado interno de un corte.
        private class CorteResultado
        {
            public bool Ok;
            public int IdCorte;
            public string Nombre;
            public string Error;
            public bool YaExistia;
        }

        // Realiza un corte (snapshot + reinicio) atribuido a 'usuario'. Idempotente: si ya
        // existe un corte para el periodo vigente, NO duplica. ASUME que Session["Usuario"] ya
        // esta seteada a un Master (los 7 endpoints de calculo la leen).
        private CorteResultado RealizarCorte(Usuario usuario)
        {
            var res = new CorteResultado();
            try
            {
                int anio = DateTime.Now.Year;
                var periodo = PeriodoHelper.Obtener(DateTime.Now);

                // Idempotencia: un solo corte por periodo (NumPeriodo + AnioPeriodo).
                bool yaExiste = db.EstadisticasHistoricoCortes
                    .Any(c => c.NumPeriodo == periodo.NumPeriodo && c.AnioPeriodo == periodo.Anio);
                if (yaExiste)
                {
                    res.Ok = true; res.YaExistia = true; res.Nombre = periodo.Nombre;
                    return res;
                }

                // Calcular cada seccion reutilizando los endpoints existentes (sin bajas, en vivo).
                var fuentes = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ResumenGlobal",       ((JsonResult)GetEstadisticasPorEspecialidad(null, null, null, anio, null, false)).Data },
                    { "NivelEstudio",        ((JsonResult)GetEstadisticasPorNivelEstudio(false)).Data },
                    { "Carrera",             ((JsonResult)GetEstadisticasPorCarrera(false)).Data },
                    { "CierreCuatrimestres", ((JsonResult)GetEstadisticasPorCierreCuatrimestres(anio, false)).Data },
                    { "Detalladas",          ((JsonResult)GetEstadisticasDetalladas(null, anio, null, false)).Data },
                    { "Grupo",               ((JsonResult)GetEstadisticasPorGrupo(null)).Data },
                    { "Bajas",               ((JsonResult)GetEstadisticasBajas(null)).Data },
                    { "MateriasReprobadas",  ((JsonResult)GetEstadisticasMaterias(null, null, false)).Data },
                };

                using (var tx = db.Database.BeginTransaction())
                {
                    var corte = new PlataformaWeb.Models.Historico.EstadisticasHistoricoCorte
                    {
                        FechaCorte = DateTime.Now,
                        NumPeriodo = periodo.NumPeriodo,
                        AnioPeriodo = periodo.Anio,
                        NombrePeriodo = periodo.Nombre,
                        CreadoPorIdUsuario = usuario.IdUsuario,
                        CreadoPorNombre = (usuario.UserName ?? "").Trim()
                    };
                    db.EstadisticasHistoricoCortes.Add(corte);
                    db.SaveChanges(); // genera IdCorte

                    foreach (var f in fuentes)
                    {
                        db.EstadisticasHistoricoSecciones.Add(new PlataformaWeb.Models.Historico.EstadisticasHistoricoSeccion
                        {
                            IdCorte = corte.IdCorte,
                            Seccion = f.Key,
                            DatosJson = JsonConvert.SerializeObject(f.Value)
                        });
                    }
                    db.SaveChanges();
                    tx.Commit();

                    res.Ok = true; res.IdCorte = corte.IdCorte; res.Nombre = corte.NombrePeriodo;
                    return res;
                }
            }
            catch (Exception ex)
            {
                res.Ok = false; res.Error = "Error al guardar el corte: " + ex.Message;
                return res;
            }
        }

        // Interruptor de los correos del modulo (corte de ciclo y alerta de cierre).
        // appSetting "Estadisticas.NotificacionesCorreo": solo "true" los habilita.
        private static bool NotificacionesCorreoHabilitadas()
        {
            var v = System.Web.Configuration.WebConfigurationManager.AppSettings["Estadisticas.NotificacionesCorreo"];
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        // Alerta previa al cierre del cuatrimestre. La dispara una tarea DIARIA de Windows Task Scheduler
        // (sin sesion, token). Solo envia correo si hoy faltan exactamente 15, 7 o 1 dias para el cierre.
        [HttpPost]
        public JsonResult EnviarAlertaCierre(string token)
        {
            var esperado = System.Web.Configuration.WebConfigurationManager.AppSettings["CorteProgramado.Token"];
            if (string.IsNullOrWhiteSpace(esperado) || token != esperado)
                return Json(new { ok = false, error = "Token invalido." });

            // Los correos de Estadisticas estan deshabilitados via appSetting mientras el usuario lo decida.
            if (!NotificacionesCorreoHabilitadas())
                return Json(new { ok = true, enviado = false, motivo = "correos deshabilitados" });

            var periodo = PeriodoHelper.Obtener(DateTime.Now);
            int dias = (int)(periodo.Fin.Date - DateTime.Now.Date).TotalDays;
            if (dias != 15 && dias != 7 && dias != 1)
                return Json(new { ok = true, enviado = false, dias = dias });

            // Usuario sistema (Master) para que los endpoints de calculo lean Session.
            var sistema = new Usuario { IdUsuario = 0, UserName = "Sistema (alerta cierre)", IdNivel = 4, IdCarrera = 0, Estado = true };
            Session["Usuario"] = sistema;

            // Dato concreto: estudiantes sin seguimiento (reusando el endpoint en vivo).
            int sinSeg = 0;
            try
            {
                var data = ((JsonResult)GetEstadisticasPorEspecialidad(null, null, null, DateTime.Now.Year, null, false)).Data;
                var jo = Newtonsoft.Json.Linq.JObject.FromObject(data);
                sinSeg = (int?)jo["data"]?["sinSeguimiento"] ?? 0;
            }
            catch { /* si falla el conteo, se envia la alerta con 0 */ }

            var correos = tutoriasDb.Usuarios
                .Where(u => (u.IdNivel == 3 || u.IdNivel == 4) && u.Estado && u.CorreoElectronico != null && u.CorreoElectronico != "")
                .Select(u => u.CorreoElectronico)
                .ToList();
            if (!correos.Any())
                return Json(new { ok = true, enviado = false, motivo = "sin destinatarios" });

            string cuerpo =
                "<h3>Aviso de cierre de cuatrimestre</h3>" +
                "<p>El cuatrimestre <b>" + periodo.Nombre + "</b> finalizará el " + periodo.Fin.ToString("dd/MM/yyyy") +
                " (faltan " + dias + " días).</p>" +
                "<p>Hay <b>" + sinSeg + "</b> estudiantes sin seguimiento. Verifique que la información del periodo esté completa antes del cierre.</p>";
            // En segundo plano: un SMTP colgado no debe dejar sin respuesta al Task Scheduler.
            ProyectoIntegracion.Functionalities.EmailService.EnviarEnSegundoPlano("Aviso de cierre: " + periodo.Nombre + " (faltan " + dias + " días)", cuerpo, correos);

            return Json(new { ok = true, enviado = true, dias = dias, destinatarios = correos.Count });
        }

        // Corte automatico programado. Lo dispara Windows Task Scheduler (sin sesion) con un token.
        // Cierra el cuatrimestre vigente (snapshot + reinicio) y notifica al Master por correo.
        [HttpPost]
        public JsonResult EjecutarCorteProgramado(string token)
        {
            var esperado = System.Web.Configuration.WebConfigurationManager.AppSettings["CorteProgramado.Token"];
            if (string.IsNullOrWhiteSpace(esperado) || token != esperado)
                return Json(new { ok = false, error = "Token invalido." });

            // Usuario de sistema (Master) para que los endpoints de calculo lean Session y para la atribucion.
            var sistema = new Usuario { IdUsuario = 0, UserName = "Sistema (corte automatico)", IdNivel = 4, IdCarrera = 0, Estado = true };
            Session["Usuario"] = sistema;

            var r = RealizarCorte(sistema);
            if (!r.Ok)
                return Json(new { ok = false, error = r.Error });
            if (r.YaExistia)
                return Json(new { ok = true, yaExistia = true, nombre = r.Nombre, mensaje = "Ya existia un corte para este periodo; no se duplico." });

            // Notificar al Master (best-effort: si el correo falla, el corte ya quedo guardado).
            // Los correos de Estadisticas estan deshabilitados via appSetting mientras el usuario lo decida.
            if (!NotificacionesCorreoHabilitadas())
                return Json(new { ok = true, idCorte = r.IdCorte, nombre = r.Nombre, correo = "deshabilitado" });
            try
            {
                var correos = tutoriasDb.Usuarios
                    .Where(u => u.IdNivel == 4 && u.Estado && u.CorreoElectronico != null && u.CorreoElectronico != "")
                    .Select(u => u.CorreoElectronico)
                    .ToList();
                if (correos.Any())
                {
                    string cuerpo =
                        "<h3>Corte de ciclo realizado automaticamente</h3>" +
                        "<p>Se cerro el periodo <b>" + r.Nombre + "</b> el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ".</p>" +
                        "<p>Las estadisticas en vivo se reiniciaron. El ciclo cerrado queda consultable en el historial por seccion (corte #" + r.IdCorte + ").</p>" +
                        "<p>Se adjunta el reporte ejecutivo del periodo.</p>";
                    var anterior = db.EstadisticasHistoricoCortes.Where(c => c.IdCorte != r.IdCorte).OrderByDescending(c => c.FechaCorte).FirstOrDefault();
                    byte[] pdf = GenerarPdfEjecutivo(anterior != null ? anterior.IdCorte : r.IdCorte, r.IdCorte);
                    // En segundo plano: un SMTP colgado no debe dejar sin respuesta al Task Scheduler.
                    ProyectoIntegracion.Functionalities.EmailService.EnviarEnSegundoPlano("Corte de ciclo: " + r.Nombre, cuerpo, correos, pdf, "ReporteEjecutivo_" + r.Nombre + ".pdf");
                }
            }
            catch { /* el corte ya esta guardado; no fallar por el correo */ }

            return Json(new { ok = true, idCorte = r.IdCorte, nombre = r.Nombre });
        }

        // Corte manual de EMERGENCIA: respaldo por si el corte automatico programado no corre
        // (Task Scheduler caido, token mal configurado, etc.). Solo Master (IdNivel 4) con sesion,
        // desde el boton de la pantalla de Estadisticas (el cliente pide confirmacion antes).
        // Reusa RealizarCorte: idempotente, si el automatico ya cerro el periodo NO duplica.
        [HttpPost]
        public JsonResult EjecutarCorteManual()
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null || usuario.IdNivel != 4)
                return Json(new { ok = false, error = "Solo el Máster puede hacer cortes." });

            var r = RealizarCorte(usuario);
            if (!r.Ok)
                return Json(new { ok = false, error = r.Error });
            if (r.YaExistia)
                return Json(new { ok = true, yaExistia = true, nombre = r.Nombre, mensaje = "Ya existía un corte para el período " + r.Nombre + "; no se duplicó." });
            return Json(new { ok = true, idCorte = r.IdCorte, nombre = r.Nombre });
        }

        // Lista los cortes guardados para poblar el selector de periodo.
        [HttpGet]
        public JsonResult GetHistoricoCortes()
        {
            var cortes = db.EstadisticasHistoricoCortes
                .OrderByDescending(c => c.FechaCorte)
                .ToList()
                .Select(c => new
                {
                    idCorte = c.IdCorte,
                    nombre = c.NombrePeriodo,
                    fecha = c.FechaCorte.ToString("dd/MM/yyyy HH:mm"),
                    creadoPor = c.CreadoPorNombre
                })
                .ToList();
            return Json(cortes, JsonRequestBehavior.AllowGet);
        }

        // Lee los indicadores (ResumenGlobal + Bajas) de un corte desde sus snapshots.
        private System.Collections.Generic.Dictionary<string, int> LeerIndicadoresCorte(int idCorte)
        {
            var dic = new System.Collections.Generic.Dictionary<string, int>();
            var resumen = db.EstadisticasHistoricoSecciones.FirstOrDefault(s => s.IdCorte == idCorte && s.Seccion == "ResumenGlobal");
            if (resumen != null && !string.IsNullOrEmpty(resumen.DatosJson))
            {
                var d = Newtonsoft.Json.Linq.JObject.Parse(resumen.DatosJson)["data"];
                if (d != null)
                    foreach (var k in new[] { "totalEstudiantes", "totalHombres", "totalMujeres", "totalSinSexo", "embarazadas", "madres", "padres", "alumnosTrabajando", "vulnerablesEconomicos", "vulnerablesAcademicos", "vulnerablesPersonales", "totalVulnerables", "noVulnerables", "sinSeguimiento" })
                        dic[k] = (int?)d[k] ?? 0;
            }
            var bajas = db.EstadisticasHistoricoSecciones.FirstOrDefault(s => s.IdCorte == idCorte && s.Seccion == "Bajas");
            if (bajas != null && !string.IsNullOrEmpty(bajas.DatosJson))
            {
                var d = Newtonsoft.Json.Linq.JObject.Parse(bajas.DatosJson)["data"];
                dic["TotalBajas"] = (int?)(d?["TotalBajas"]) ?? 0;
            }
            // Tasa de reprobación del corte: suma de reprobadas / suma de registros del snapshot MateriasReprobadas.
            var secMat = db.EstadisticasHistoricoSecciones.FirstOrDefault(s => s.IdCorte == idCorte && s.Seccion == "MateriasReprobadas");
            dic["tasaReprobacionx10"] = -1;
            if (secMat != null && !string.IsNullOrEmpty(secMat.DatosJson))
            {
                try
                {
                    var dm = Newtonsoft.Json.Linq.JObject.Parse(secMat.DatosJson);
                    int rep = 0, reg = 0;
                    // El snapshot guarda la respuesta completa: {"success":true,"data":{"materias":[...]}}
                    var arr = (dm["data"] != null ? dm["data"]["materias"] : dm["materias"]) as Newtonsoft.Json.Linq.JArray;
                    if (arr != null)
                        foreach (var m in arr)
                        {
                            rep += (int?)m["reprobadas"] ?? 0;
                            reg += (int?)m["totalRegistros"] ?? 0;
                        }
                    if (reg > 0) dic["tasaReprobacionx10"] = (int)Math.Round(rep * 1000.0 / reg); // % ×10 (dic es de ints)
                }
                catch { }
            }
            return dic;
        }

        // Definicion de un indicador del comparativo.
        private class IndicadorDef { public string Clave; public string Etiqueta; public string Sentido; }

        // Una fila del comparativo (un indicador con sus dos valores y su variacion).
        private class FilaComparativa { public string Indicador; public int C1; public int C2; public double? Variacion; public bool? Mejora; }

        private static readonly IndicadorDef[] INDICADORES = new[]
        {
            new IndicadorDef { Clave = "totalEstudiantes", Etiqueta = "Total estudiantes", Sentido = "neutral" },
            new IndicadorDef { Clave = "totalHombres", Etiqueta = "Hombres", Sentido = "neutral" },
            new IndicadorDef { Clave = "totalMujeres", Etiqueta = "Mujeres", Sentido = "neutral" },
            new IndicadorDef { Clave = "totalSinSexo", Etiqueta = "Sin especificar sexo", Sentido = "neutral" },
            new IndicadorDef { Clave = "embarazadas", Etiqueta = "Embarazadas", Sentido = "neutral" },
            new IndicadorDef { Clave = "madres", Etiqueta = "Madres", Sentido = "neutral" },
            new IndicadorDef { Clave = "padres", Etiqueta = "Padres", Sentido = "neutral" },
            new IndicadorDef { Clave = "alumnosTrabajando", Etiqueta = "Estudiantes trabajando", Sentido = "neutral" },
            new IndicadorDef { Clave = "vulnerablesEconomicos", Etiqueta = "Vulnerables económicos", Sentido = "down" },
            new IndicadorDef { Clave = "vulnerablesAcademicos", Etiqueta = "Vulnerables académicos", Sentido = "down" },
            new IndicadorDef { Clave = "vulnerablesPersonales", Etiqueta = "Vulnerables personales", Sentido = "down" },
            new IndicadorDef { Clave = "totalVulnerables", Etiqueta = "Total vulnerables", Sentido = "down" },
            new IndicadorDef { Clave = "noVulnerables", Etiqueta = "No vulnerables", Sentido = "up" },
            new IndicadorDef { Clave = "sinSeguimiento", Etiqueta = "Sin seguimiento", Sentido = "down" },
            new IndicadorDef { Clave = "TotalBajas", Etiqueta = "Total de bajas", Sentido = "down" },
        };

        // Construye las filas del comparativo entre dos cortes (reusado por el endpoint y el PDF).
        private System.Collections.Generic.List<FilaComparativa> ConstruirFilasComparativo(int idC1, int idC2)
        {
            var v1 = LeerIndicadoresCorte(idC1);
            var v2 = LeerIndicadoresCorte(idC2);
            return INDICADORES.Select(ind =>
            {
                int c1 = v1.ContainsKey(ind.Clave) ? v1[ind.Clave] : 0;
                int c2 = v2.ContainsKey(ind.Clave) ? v2[ind.Clave] : 0;
                double? variacion = c1 != 0 ? (double?)((double)(c2 - c1) / c1 * 100.0) : null;
                bool? mejora = null;
                if (ind.Sentido == "up") mejora = c2 > c1 ? true : (c2 < c1 ? (bool?)false : null);
                if (ind.Sentido == "down") mejora = c2 < c1 ? true : (c2 > c1 ? (bool?)false : null);
                return new FilaComparativa { Indicador = ind.Etiqueta, C1 = c1, C2 = c2, Variacion = variacion, Mejora = mejora };
            }).ToList();
        }

        // Comparativo entre dos cortes (cuatrimestres): por indicador devuelve c1, c2, variacion% y
        // si la variacion es "mejora" (true=verde), "empeora" (false=rojo) o neutral (null=gris).
        [HttpPost]
        public JsonResult GetComparativoCortes(int idCorte1, int idCorte2)
        {
            var filas = ConstruirFilasComparativo(idCorte1, idCorte2)
                .Select(f => new { indicador = f.Indicador, c1 = f.C1, c2 = f.C2, variacion = f.Variacion, mejora = f.Mejora })
                .ToList();
            return Json(new { success = true, filas = filas });
        }

        // Series de tendencia de indicadores a lo largo de todos los cortes (cronologico).
        [HttpPost]
        public JsonResult GetTendenciasCortes()
        {
            var cortes = db.EstadisticasHistoricoCortes.OrderBy(c => c.FechaCorte).ToList();
            var labels = cortes.Select(c => c.NombrePeriodo + " (" + c.FechaCorte.ToString("dd/MM/yy") + ")").ToList();
            var claves = new[] { "totalEstudiantes", "vulnerablesEconomicos", "vulnerablesAcademicos", "vulnerablesPersonales", "noVulnerables", "sinSeguimiento", "TotalBajas", "embarazadas", "madres", "padres", "alumnosTrabajando", "tasaReprobacionx10" };
            var series = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>();
            foreach (var k in claves) series[k] = new System.Collections.Generic.List<int>();
            foreach (var c in cortes)
            {
                var v = LeerIndicadoresCorte(c.IdCorte);
                foreach (var k in claves) series[k].Add(v.ContainsKey(k) ? v[k] : 0);
            }
            return Json(new { success = true, labels = labels, series = series });
        }

        // Genera el PDF ejecutivo comparativo (corte C1 vs C2) con iTextSharp.
        private byte[] GenerarPdfEjecutivo(int idC1, int idC2)
        {
            var c1 = db.EstadisticasHistoricoCortes.FirstOrDefault(c => c.IdCorte == idC1);
            var c2 = db.EstadisticasHistoricoCortes.FirstOrDefault(c => c.IdCorte == idC2);
            var filas = ConstruirFilasComparativo(idC1, idC2);
            using (var ms = new System.IO.MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 36, 36);
                iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                doc.Open();
                var fTit = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 16, new iTextSharp.text.BaseColor(0, 105, 92));
                var fSub = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10, iTextSharp.text.BaseColor.DARK_GRAY);
                var fHead = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 10, iTextSharp.text.BaseColor.WHITE);
                var fCell = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10, iTextSharp.text.BaseColor.BLACK);

                doc.Add(new iTextSharp.text.Paragraph("Reporte ejecutivo comparativo", fTit));
                string l1 = c1 != null ? c1.NombrePeriodo + " (" + c1.FechaCorte.ToString("dd/MM/yyyy") + ")" : "—";
                string l2 = c2 != null ? c2.NombrePeriodo + " (" + c2.FechaCorte.ToString("dd/MM/yyyy") + ")" : "—";
                doc.Add(new iTextSharp.text.Paragraph("Cuatrimestre 1: " + l1, fSub));
                doc.Add(new iTextSharp.text.Paragraph("Cuatrimestre 2: " + l2, fSub));
                doc.Add(new iTextSharp.text.Paragraph("Generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fSub));
                doc.Add(new iTextSharp.text.Paragraph(" "));

                var t = new iTextSharp.text.pdf.PdfPTable(4) { WidthPercentage = 100 };
                t.SetWidths(new float[] { 40, 20, 20, 20 });
                var hb = new iTextSharp.text.BaseColor(0, 105, 92);
                foreach (var h in new[] { "Indicador", "Cuatrimestre 1", "Cuatrimestre 2", "Variación" })
                    t.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(h, fHead)) { BackgroundColor = hb, Padding = 5 });

                foreach (var f in filas)
                {
                    t.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(f.Indicador, fCell)) { Padding = 4 });
                    t.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(f.C1.ToString(), fCell)) { Padding = 4, HorizontalAlignment = 1 });
                    t.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(f.C2.ToString(), fCell)) { Padding = 4, HorizontalAlignment = 1 });
                    string txtVar; iTextSharp.text.BaseColor colVar;
                    if (f.Variacion == null) { txtVar = "—"; colVar = iTextSharp.text.BaseColor.GRAY; }
                    else
                    {
                        string flecha = f.C2 > f.C1 ? "+" : (f.C2 < f.C1 ? "-" : "=");
                        txtVar = flecha + " " + Math.Abs(f.Variacion.Value).ToString("0.0") + "%";
                        colVar = f.Mejora == true ? new iTextSharp.text.BaseColor(25, 135, 84) : (f.Mejora == false ? new iTextSharp.text.BaseColor(220, 53, 69) : iTextSharp.text.BaseColor.GRAY);
                    }
                    var fVar = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 10, colVar);
                    t.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(txtVar, fVar)) { Padding = 4, HorizontalAlignment = 1 });
                }
                doc.Add(t);
                doc.Close();
                return ms.ToArray();
            }
        }

        // Descarga el PDF ejecutivo de la comparacion seleccionada (C1 vs C2).
        [HttpGet]
        public ActionResult DescargarPdfEjecutivo(int idCorte1, int idCorte2)
        {
            var pdf = GenerarPdfEjecutivo(idCorte1, idCorte2);
            return File(pdf, "application/pdf", "ReporteEjecutivo.pdf");
        }

        // Fase 3 dashboard: Lee los 7 KPIs del corte del año anterior desde sus snapshots.
        // Helper propio — NO toca LeerIndicadoresCorte (el comparativo/PDF dependen de él).
        private System.Collections.Generic.Dictionary<string, int> LeerKpisCorte(int idCorte)
        {
            var kpis = new System.Collections.Generic.Dictionary<string, int>();

            // ResumenGlobal: totalEstudiantes, totalVulnerables, sinSeguimiento
            var secResumen = db.EstadisticasHistoricoSecciones
                .FirstOrDefault(s => s.IdCorte == idCorte && s.Seccion == "ResumenGlobal");
            if (secResumen != null && !string.IsNullOrEmpty(secResumen.DatosJson))
            {
                var d = Newtonsoft.Json.Linq.JObject.Parse(secResumen.DatosJson)["data"];
                if (d != null)
                {
                    kpis["totalEstudiantes"] = (int?)(d["totalEstudiantes"]) ?? 0;
                    kpis["totalVulnerables"]  = (int?)(d["totalVulnerables"])  ?? 0;
                    kpis["sinSeguimiento"]    = (int?)(d["sinSeguimiento"])    ?? 0;
                }
            }

            // MateriasReprobadas: totalAlumnosAfectados + Σ reprobadas + Σ extraordinarios
            var secMaterias = db.EstadisticasHistoricoSecciones
                .FirstOrDefault(s => s.IdCorte == idCorte && s.Seccion == "MateriasReprobadas");
            if (secMaterias != null && !string.IsNullOrEmpty(secMaterias.DatosJson))
            {
                var d = Newtonsoft.Json.Linq.JObject.Parse(secMaterias.DatosJson)["data"];
                if (d != null)
                {
                    kpis["alumnosAfectados"] = (int?)(d["totalAlumnosAfectados"]) ?? 0;
                    int sumRep = 0, sumExt = 0;
                    var materias = d["materias"] as Newtonsoft.Json.Linq.JArray;
                    if (materias != null)
                    {
                        foreach (var m in materias)
                        {
                            sumRep += (int?)(m["reprobadas"])    ?? 0;
                            sumExt += (int?)(m["extraordinarios"]) ?? 0;
                        }
                    }
                    kpis["materiasReprobadas"] = sumRep;
                    kpis["extraordinarios"]    = sumExt;
                }
            }

            // Bajas: TotalBajas (PascalCase — igual que LeerIndicadoresCorte)
            var secBajas = db.EstadisticasHistoricoSecciones
                .FirstOrDefault(s => s.IdCorte == idCorte && s.Seccion == "Bajas");
            if (secBajas != null && !string.IsNullOrEmpty(secBajas.DatosJson))
            {
                var d = Newtonsoft.Json.Linq.JObject.Parse(secBajas.DatosJson)["data"];
                kpis["totalBajas"] = (int?)(d?["TotalBajas"]) ?? 0;
            }

            return kpis;
        }

        // Fase 3 dashboard: devuelve los KPIs del corte del mismo cuatrimestre del año anterior.
        // Task 3 consume este endpoint para mostrar las flechas de variación (sólo Máster).
        [HttpPost]
        public ActionResult GetKpisAnioAnterior()
        {
            try
            {
                var usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                    return Json(new { success = false, error = "Sesión expirada" });
                if (usuario.IdNivel != 4)
                    return Json(new { success = false, error = "Solo disponible para el perfil Máster." });

                var periodo   = PeriodoHelper.Obtener(DateTime.Now);
                int numPeriodo = periodo.NumPeriodo;
                int anioAnterior = DateTime.Now.Year - 1;

                var corte = db.EstadisticasHistoricoCortes
                    .FirstOrDefault(c => c.NumPeriodo == numPeriodo && c.AnioPeriodo == anioAnterior);

                if (corte == null)
                    return Json(new { success = true, encontrado = false, nombrePeriodo = (string)null, kpis = (object)null });

                var k = LeerKpisCorte(corte.IdCorte);
                System.Func<string, int> v = key => k.ContainsKey(key) ? k[key] : 0;

                return Json(new
                {
                    success       = true,
                    encontrado    = true,
                    nombrePeriodo = corte.NombrePeriodo,
                    kpis          = new
                    {
                        totalEstudiantes  = v("totalEstudiantes"),
                        totalVulnerables  = v("totalVulnerables"),
                        sinSeguimiento    = v("sinSeguimiento"),
                        alumnosAfectados  = v("alumnosAfectados"),
                        materiasReprobadas = v("materiasReprobadas"),
                        extraordinarios   = v("extraordinarios"),
                        totalBajas        = v("totalBajas")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                usuariosDb?.Dispose();
                tutoriasDb?.Dispose();
                db?.Dispose();
                estadiasDb?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}