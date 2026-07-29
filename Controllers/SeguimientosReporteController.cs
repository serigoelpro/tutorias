using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.IO;

namespace PlataformaWeb.Controllers
{
    // Reporte de seguimientos individuales por año, periodo y filtros opcionales
    public class SeguimientosReporteController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null || (usuario.IdNivel != 3 && usuario.IdNivel != 4))
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        public class SeguimientoDetalle
        {
            public int Año { get; set; }
            public string PeriodoNombre { get; set; }
            public string Carrera { get; set; }
            public string Grado { get; set; }
            public string Grupo { get; set; }
            public int IdTutor { get; set; }
            public string Tutor { get; set; }
            public string Matricula { get; set; }
            public string Alumno { get; set; }
            public DateTime Fecha { get; set; }
            public string Vulnerabilidad { get; set; }
            public string Problematica { get; set; }
            public string Accion { get; set; }
        }

        public class SeguimientosFiltro
        {
            public int? Año { get; set; }
            public int? IdPeriodo { get; set; }
            public int? IdCarrera { get; set; }
            public int? IdTutor { get; set; }
        }

        public class SeguimientosReporteViewModel
        {
            public SeguimientosFiltro Filtro { get; set; }
            public IEnumerable<SelectListItem> Periodos { get; set; }
            public IEnumerable<SelectListItem> Años { get; set; }
            public IEnumerable<SelectListItem> Carreras { get; set; }
            public IEnumerable<SelectListItem> Tutores { get; set; }
            public List<SeguimientoDetalle> Detalles { get; set; }
            public PaginacionInfo Paginacion { get; set; }
        }

        public class PaginacionInfo
        {
            public int PaginaActual { get; set; }
            public int TotalPaginas { get; set; }
            public int TotalRegistros { get; set; }
            public int RegistrosPorPagina { get; set; } = 15;
            public bool TienePaginaAnterior => PaginaActual > 1;
            public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;
        }

        // GET: SeguimientosReporte
        public ActionResult Index(int? año, int? idPeriodo, int? idCarrera, int? idTutor, int pagina = 1, string busqueda = "")
        {
            var user = Session["Usuario"] as Usuario;

            // Defaults
            int añoActual = DateTime.Now.Year;
            int periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
            int añoFiltro = año ?? añoActual;
            int periodoFiltro = idPeriodo ?? periodoActual;

            // Meses del periodo
            int perInicio = periodoFiltro == 1 ? 1 : (periodoFiltro == 2 ? 5 : 9);
            int perFin = periodoFiltro == 1 ? 4 : (periodoFiltro == 2 ? 8 : 12);

            // Alcance por nivel
            int? carreraFiltro = idCarrera;
            if (user.IdNivel == 3)
            {
                carreraFiltro = user.IdCarrera;
            }

            // Usar Individuals como tabla principal - agrupar por grupos sin tutores
            // Filtrar por el periodo académico real del estudiante desde DatosPersonales
            // Usar la relación correcta con Carreras para obtener la carrera correcta
            string sql = @"
                SELECT 
                    @añoFiltro as Año,
                    CASE 
                        WHEN @periodoFiltro = 1 THEN 'Enero - Abril'
                        WHEN @periodoFiltro = 2 THEN 'Mayo - Agosto'
                        WHEN @periodoFiltro = 3 THEN 'Septiembre - Diciembre'
                        ELSE 'Desconocido'
                    END as PeriodoNombre,
                    c.Nombre as Carrera,
                    CASE 
                        WHEN @periodoFiltro = 1 THEN 'Enero - Abril'
                        WHEN @periodoFiltro = 2 THEN 'Mayo - Agosto'
                        WHEN @periodoFiltro = 3 THEN 'Septiembre - Diciembre'
                        ELSE 'Desconocido'
                    END as Grado,
                    ind.Grupo,
                    0 as IdTutor,
                    'Grupo: ' + ind.Grupo as Tutor,
                    ind.Matricula,
                    ind.Nombre as Alumno,
                    seg.Fecha,
                    seg.Vulnerabilidad,
                    seg.Problematica,
                    seg.Accion
                FROM Individuals ind
                INNER JOIN Seguimientoes seg ON ind.IdIndividual = seg.IdIndividual
                LEFT JOIN DatosPersonales dp ON ind.IdPersona = dp.IdPersona
                LEFT JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
                WHERE ind.Fecha >= CAST(@añoFiltro AS VARCHAR(4)) + '-01-01T00:00:00'
                    AND ind.Fecha < CAST(@añoFiltro + 1 AS VARCHAR(4)) + '-01-01T00:00:00'
                    AND (
                        (@periodoFiltro = 1 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'ENERO-ABRIL') OR
                        (@periodoFiltro = 2 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'MAYO-AGOSTO') OR
                        (@periodoFiltro = 3 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'SEPTIEMBRE-DICIEMBRE')
                    )
                    AND (@carreraFiltro IS NULL OR dp.IdCarrera = @carreraFiltro)
                    AND (@busqueda IS NULL OR @busqueda = '' OR 
                         ind.Grupo LIKE '%' + @busqueda + '%' OR 
                         ind.Nombre LIKE '%' + @busqueda + '%' OR 
                         ind.Matricula LIKE '%' + @busqueda + '%' OR 
                         c.Nombre LIKE '%' + @busqueda + '%')
                ORDER BY ind.Grupo, ind.Nombre ASC";

            var parametros = new object[]
            {
                new System.Data.SqlClient.SqlParameter("@añoFiltro", añoFiltro),
                new System.Data.SqlClient.SqlParameter("@periodoFiltro", periodoFiltro),
                new System.Data.SqlClient.SqlParameter("@carreraFiltro", carreraFiltro ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@idTutor", idTutor ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@busqueda", string.IsNullOrEmpty(busqueda) ? (object)DBNull.Value : busqueda)
            };

            // Aumentar timeout para consultas complejas
            db.Database.CommandTimeout = 300; // 5 minutos

            // Obtener todos los detalles
            var todosLosDetalles = db.Database.SqlQuery<SeguimientoDetalle>(sql, parametros).ToList();

            // Obtener todos los detalles con filtros aplicados

            List<SeguimientoDetalle> detalles;
            PaginacionInfo paginacion = null;

            if (string.IsNullOrEmpty(busqueda))
            {
                // Si no hay búsqueda, aplicar paginación normal
                var gruposUnicos = todosLosDetalles
                    .GroupBy(d => d.Grupo)
                    .OrderBy(g => g.Key)
                    .ToList();

                int totalGrupos = gruposUnicos.Count;
                int registrosPorPagina = 15;
                int totalPaginas = (int)Math.Ceiling((double)totalGrupos / registrosPorPagina);

                // Validar página
                if (pagina < 1) pagina = 1;
                if (pagina > totalPaginas && totalPaginas > 0) pagina = totalPaginas;

                // Obtener grupos para la página actual
                var gruposPagina = gruposUnicos
                    .Skip((pagina - 1) * registrosPorPagina)
                    .Take(registrosPorPagina)
                    .Select(g => g.Key)
                    .ToList();

                // Filtrar detalles para mostrar solo los grupos de la página actual
                detalles = todosLosDetalles
                    .Where(d => gruposPagina.Contains(d.Grupo))
                    .ToList();

                // Crear información de paginación
                paginacion = new PaginacionInfo
                {
                    PaginaActual = pagina,
                    TotalPaginas = totalPaginas,
                    TotalRegistros = totalGrupos,
                    RegistrosPorPagina = registrosPorPagina
                };
            }
            else
            {
                // Si hay búsqueda, mostrar todos los resultados sin paginación
                detalles = todosLosDetalles;
            }

            // Catálogos - Periodos hardcodeados como en el resto del sistema
            var periodos = new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Enero - Abril", Selected = periodoFiltro == 1 },
                new SelectListItem { Value = "2", Text = "Mayo - Agosto", Selected = periodoFiltro == 2 },
                new SelectListItem { Value = "3", Text = "Septiembre - Diciembre", Selected = periodoFiltro == 3 }
            };

            // Años disponibles (toma base de TutoriaGrupals).
            // Se proyecta a int? para tolerar filas con Año NULL en la base de datos.
            // El segundo Where es un filtro in-memory de seguridad por si EF no elimina todos los nulls.
            var añosDisponibles = db.TutoriaGrupals
                .Select(t => (int?)t.Año)
                .Where(a => a != null)
                .Distinct()
                .OrderByDescending(a => a)
                .ToList()
                .Where(a => a.HasValue)
                .Select(a => a.Value)
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString(),
                    Selected = a == añoFiltro
                });

            // Carreras según alcance - usar namespace correcto
            IQueryable<Plataforma_Web.Models.Carrera> carrerasQuery = db.Carreras;
            if (user.IdNivel == 3)
            {
                carrerasQuery = carrerasQuery.Where(c => c.IdCarrera == user.IdCarrera);
            }
            var carrerasSelect = carrerasQuery
                .OrderBy(c => c.Nombre)
                .ToList()
                .Select(c => new SelectListItem
                {
                    Value = c.IdCarrera.ToString(),
                    Text = c.Nombre,
                    Selected = carreraFiltro.HasValue && carreraFiltro.Value == c.IdCarrera
                });

            // Tutores según alcance (IdNivel = 2) - simplificado para la nueva consulta
            var tutoresSelect = db.Usuarios
                .Where(u => u.IdNivel == 2)
                .Where(u => user.IdNivel != 3 || u.IdCarrera == user.IdCarrera)
                .Where(u => !carreraFiltro.HasValue || u.IdCarrera == carreraFiltro.Value)
                .OrderBy(u => u.NombreCompleto)
                .ToList()
                .Select(u => new SelectListItem
                {
                    Value = u.IdUsuario.ToString(),
                    Text = u.NombreCompleto,
                    Selected = idTutor.HasValue && idTutor.Value == u.IdUsuario
                });

            // Preparar VM
            var vm = new SeguimientosReporteViewModel
            {
                Filtro = new SeguimientosFiltro
                {
                    Año = añoFiltro,
                    IdPeriodo = periodoFiltro,
                    IdCarrera = carreraFiltro,
                    IdTutor = idTutor
                },
                Periodos = periodos,
                Años = añosDisponibles,
                Carreras = (user.IdNivel >= 4 ? new List<SelectListItem> { new SelectListItem { Value = "", Text = "Todas las carreras", Selected = !carreraFiltro.HasValue } }.Concat(carrerasSelect) : carrerasSelect),
                Tutores = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Todos los tutores", Selected = !idTutor.HasValue } }.Concat(tutoresSelect),
                Detalles = detalles,
                Paginacion = paginacion
            };

            return View(vm);
        }


        public ActionResult ExportarExcel(int? año, int? idPeriodo, int? idCarrera, int? idTutor)
        {
            // Reutiliza la lógica de filtros del método Index
            var user = Session["Usuario"] as Usuario;

            int añoActual = DateTime.Now.Year;
            int periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
            int añoFiltro = año ?? añoActual;
            int periodoFiltro = idPeriodo ?? periodoActual;

            int? carreraFiltro = idCarrera;
            if (user.IdNivel == 3)
            {
                carreraFiltro = user.IdCarrera;
            }

            string sql = @"
                SELECT 
                    @añoFiltro as Año,
                    CASE 
                        WHEN @periodoFiltro = 1 THEN 'Enero - Abril'
                        WHEN @periodoFiltro = 2 THEN 'Mayo - Agosto'
                        WHEN @periodoFiltro = 3 THEN 'Septiembre - Diciembre'
                        ELSE 'Desconocido'
                    END as PeriodoNombre,
                    c.Nombre as Carrera,
                    CASE 
                        WHEN @periodoFiltro = 1 THEN 'Enero - Abril'
                        WHEN @periodoFiltro = 2 THEN 'Mayo - Agosto'
                        WHEN @periodoFiltro = 3 THEN 'Septiembre - Diciembre'
                        ELSE 'Desconocido'
                    END as Grado,
                    ind.Grupo,
                    COALESCE(tg.IdUsuario, 0) as IdTutor,
                    COALESCE(u.NombreCompleto, 'Grupo: ' + ind.Grupo) as Tutor,
                    ind.Matricula,
                    ind.Nombre as Alumno,
                    seg.Fecha,
                    seg.Vulnerabilidad,
                    seg.Problematica,
                    seg.Accion
                FROM Individuals ind
                INNER JOIN Seguimientoes seg ON ind.IdIndividual = seg.IdIndividual
                LEFT JOIN DatosPersonales dp ON ind.IdPersona = dp.IdPersona
                LEFT JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
                LEFT JOIN TutoriaGrupals tg ON (
                    -- Usar los campos de DatosPersonales directamente (forma correcta)
                    dp.IdCarrera = tg.IdCarrera
                    AND dp.IdGrado = tg.IdGrado
                    AND dp.IdGrupo = tg.IdGrupo
                    AND dp.IdTurno = tg.IdTurno
                    AND dp.IdPeriodo = tg.IdPeriodo
                    AND dp.Año = tg.Año
                )
                LEFT JOIN Usuarios u ON tg.IdUsuario = u.IdUsuario
                WHERE ind.Fecha >= CAST(@añoFiltro AS VARCHAR(4)) + '-01-01T00:00:00'
                    AND ind.Fecha < CAST(@añoFiltro + 1 AS VARCHAR(4)) + '-01-01T00:00:00'
                    AND (
                        (@periodoFiltro = 1 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'ENERO-ABRIL') OR
                        (@periodoFiltro = 2 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'MAYO-AGOSTO') OR
                        (@periodoFiltro = 3 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'SEPTIEMBRE-DICIEMBRE')
                    )
                    AND (@carreraFiltro IS NULL OR dp.IdCarrera = @carreraFiltro)
                ORDER BY ind.Grupo, ind.Nombre ASC";

            var parametros = new object[]
            {
                new System.Data.SqlClient.SqlParameter("@añoFiltro", añoFiltro),
                new System.Data.SqlClient.SqlParameter("@periodoFiltro", periodoFiltro),
                new System.Data.SqlClient.SqlParameter("@carreraFiltro", carreraFiltro ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@idTutor", idTutor ?? (object)DBNull.Value)
            };

            db.Database.CommandTimeout = 300;
            var detalles = db.Database.SqlQuery<SeguimientoDetalle>(sql, parametros).ToList();

            // Crear archivo Excel
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Seguimientos");

                // Encabezados
                ws.Cells[1, 1].Value = "Año";
                ws.Cells[1, 2].Value = "Periodo";
                ws.Cells[1, 3].Value = "Carrera";
                ws.Cells[1, 4].Value = "Grupo";
                ws.Cells[1, 5].Value = "Tutor";
                ws.Cells[1, 6].Value = "Matrícula";
                ws.Cells[1, 7].Value = "Alumno";
                ws.Cells[1, 8].Value = "Fecha";
                ws.Cells[1, 9].Value = "Vulnerabilidad";
                ws.Cells[1, 10].Value = "Problemática";
                ws.Cells[1, 11].Value = "Acción";

                // Datos
                int row = 2;
                foreach (var d in detalles)
                {
                    ws.Cells[row, 1].Value = d.Año;
                    ws.Cells[row, 2].Value = d.PeriodoNombre;
                    ws.Cells[row, 3].Value = d.Carrera;
                    ws.Cells[row, 4].Value = d.Grupo;
                    ws.Cells[row, 5].Value = d.Tutor;
                    ws.Cells[row, 6].Value = d.Matricula;
                    ws.Cells[row, 7].Value = d.Alumno;
                    ws.Cells[row, 8].Value = d.Fecha.ToString("dd/MM/yyyy HH:mm");
                    ws.Cells[row, 9].Value = d.Vulnerabilidad;
                    ws.Cells[row, 10].Value = d.Problematica;
                    ws.Cells[row, 11].Value = d.Accion;
                    row++;
                }

                // Configurar ancho de columnas para mejor visualización
                ws.Column(1).Width = 8;   // Año
                ws.Column(2).Width = 15;  // Periodo
                ws.Column(3).Width = 25;  // Carrera
                ws.Column(4).Width = 10;  // Grupo
                ws.Column(5).Width = 30;  // Tutor
                ws.Column(6).Width = 15;  // Matrícula
                ws.Column(7).Width = 30;  // Alumno
                ws.Column(8).Width = 18;  // Fecha
                ws.Column(9).Width = 15;  // Vulnerabilidad
                ws.Column(10).Width = 40; // Problemática
                ws.Column(11).Width = 40; // Acción

                // Habilitar ajuste de texto para columnas largas
                ws.Column(10).Style.WrapText = true; // Problemática
                ws.Column(11).Style.WrapText = true; // Acción

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"Seguimientos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        public ActionResult ExportarExcelGrupo(int? año, int? idPeriodo, int? idCarrera, string grupo)
        {
            // Reutiliza la lógica de filtros del método Index
            var user = Session["Usuario"] as Usuario;

            int añoActual = DateTime.Now.Year;
            int periodoActual = (DateTime.Now.Month >= 1 && DateTime.Now.Month <= 4) ? 1 : (DateTime.Now.Month <= 8 ? 2 : 3);
            int añoFiltro = año ?? añoActual;
            int periodoFiltro = idPeriodo ?? periodoActual;

            int? carreraFiltro = idCarrera;
            if (user.IdNivel == 3)
            {
                carreraFiltro = user.IdCarrera;
            }

            string sql = @"
                SELECT 
                    @añoFiltro as Año,
                    CASE 
                        WHEN @periodoFiltro = 1 THEN 'Enero - Abril'
                        WHEN @periodoFiltro = 2 THEN 'Mayo - Agosto'
                        WHEN @periodoFiltro = 3 THEN 'Septiembre - Diciembre'
                        ELSE 'Desconocido'
                    END as PeriodoNombre,
                    c.Nombre as Carrera,
                    CASE 
                        WHEN @periodoFiltro = 1 THEN 'Enero - Abril'
                        WHEN @periodoFiltro = 2 THEN 'Mayo - Agosto'
                        WHEN @periodoFiltro = 3 THEN 'Septiembre - Diciembre'
                        ELSE 'Desconocido'
                    END as Grado,
                    ind.Grupo,
                    COALESCE(tg.IdUsuario, 0) as IdTutor,
                    COALESCE(u.NombreCompleto, 'Grupo: ' + ind.Grupo) as Tutor,
                    ind.Matricula,
                    ind.Nombre as Alumno,
                    seg.Fecha,
                    seg.Vulnerabilidad,
                    seg.Problematica,
                    seg.Accion
                FROM Individuals ind
                INNER JOIN Seguimientoes seg ON ind.IdIndividual = seg.IdIndividual
                LEFT JOIN DatosPersonales dp ON ind.IdPersona = dp.IdPersona
                LEFT JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
                LEFT JOIN TutoriaGrupals tg ON (
                    -- Usar los campos de DatosPersonales directamente (forma correcta)
                    dp.IdCarrera = tg.IdCarrera
                    AND dp.IdGrado = tg.IdGrado
                    AND dp.IdGrupo = tg.IdGrupo
                    AND dp.IdTurno = tg.IdTurno
                    AND dp.IdPeriodo = tg.IdPeriodo
                    AND dp.Año = tg.Año
                )
                LEFT JOIN Usuarios u ON tg.IdUsuario = u.IdUsuario
                WHERE ind.Fecha >= CAST(@añoFiltro AS VARCHAR(4)) + '-01-01T00:00:00'
                    AND ind.Fecha < CAST(@añoFiltro + 1 AS VARCHAR(4)) + '-01-01T00:00:00'
                    AND (
                        (@periodoFiltro = 1 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'ENERO-ABRIL') OR
                        (@periodoFiltro = 2 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'MAYO-AGOSTO') OR
                        (@periodoFiltro = 3 AND REPLACE(UPPER(ind.Cuatrimestre), ' ', '') = 'SEPTIEMBRE-DICIEMBRE')
                    )
                    AND ind.Grupo = @grupo
                    AND (@carreraFiltro IS NULL OR dp.IdCarrera = @carreraFiltro)
                ORDER BY ind.Nombre ASC";

            var parametros = new object[]
            {
                new System.Data.SqlClient.SqlParameter("@añoFiltro", añoFiltro),
                new System.Data.SqlClient.SqlParameter("@periodoFiltro", periodoFiltro),
                new System.Data.SqlClient.SqlParameter("@carreraFiltro", carreraFiltro ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@grupo", grupo ?? (object)DBNull.Value)
            };

            db.Database.CommandTimeout = 300;
            var detalles = db.Database.SqlQuery<SeguimientoDetalle>(sql, parametros).ToList();

            // Crear archivo Excel
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add($"Seguimientos_{grupo}");

                // Encabezados
                ws.Cells[1, 1].Value = "Año";
                ws.Cells[1, 2].Value = "Periodo";
                ws.Cells[1, 3].Value = "Carrera";
                ws.Cells[1, 4].Value = "Grupo";
                ws.Cells[1, 5].Value = "Tutor";
                ws.Cells[1, 6].Value = "Matrícula";
                ws.Cells[1, 7].Value = "Alumno";
                ws.Cells[1, 8].Value = "Fecha";
                ws.Cells[1, 9].Value = "Vulnerabilidad";
                ws.Cells[1, 10].Value = "Problemática";
                ws.Cells[1, 11].Value = "Acción";

                // Datos
                int row = 2;
                foreach (var d in detalles)
                {
                    ws.Cells[row, 1].Value = d.Año;
                    ws.Cells[row, 2].Value = d.PeriodoNombre;
                    ws.Cells[row, 3].Value = d.Carrera;
                    ws.Cells[row, 4].Value = d.Grupo;
                    ws.Cells[row, 5].Value = d.Tutor;
                    ws.Cells[row, 6].Value = d.Matricula;
                    ws.Cells[row, 7].Value = d.Alumno;
                    ws.Cells[row, 8].Value = d.Fecha.ToString("dd/MM/yyyy HH:mm");
                    ws.Cells[row, 9].Value = d.Vulnerabilidad;
                    ws.Cells[row, 10].Value = d.Problematica;
                    ws.Cells[row, 11].Value = d.Accion;
                    row++;
                }

                // Configurar ancho de columnas para mejor visualización
                ws.Column(1).Width = 8;   // Año
                ws.Column(2).Width = 15;  // Periodo
                ws.Column(3).Width = 25;  // Carrera
                ws.Column(4).Width = 10;  // Grupo
                ws.Column(5).Width = 30;  // Tutor
                ws.Column(6).Width = 15;  // Matrícula
                ws.Column(7).Width = 30;  // Alumno
                ws.Column(8).Width = 18;  // Fecha
                ws.Column(9).Width = 15;  // Vulnerabilidad
                ws.Column(10).Width = 40; // Problemática
                ws.Column(11).Width = 40; // Acción

                // Habilitar ajuste de texto para columnas largas
                ws.Column(10).Style.WrapText = true; // Problemática
                ws.Column(11).Style.WrapText = true; // Acción

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"Seguimientos_{grupo}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
    }
}