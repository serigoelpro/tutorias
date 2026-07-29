using OfficeOpenXml;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Models.Materias;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.Materias
{
    [CustomAuthorize(Nivel = 1)]
    public class ImportarController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // CONSTANTE VIEWS SEGÚN ESPECIFICACIÓN - CORREGIDA
        private const string VIEWS = "Views/ImportarMateriasGrupo";

        // MÉTODO INDEX - VISTA DE FORMULARIO PARA SUBIR EXCEL

        public ActionResult Index(int? idTutoriaGrupal, int? idGrupo, int? idCarrera, int? idGrado)
        {
            ViewBag.Title = "Importar Calificaciones desde Excel";

            ViewBag.IdTutoriaGrupal = idTutoriaGrupal ?? 0;
            ViewBag.IdGrupo = idGrupo ?? 1;
            ViewBag.IdCarrera = idCarrera ?? 1;
            ViewBag.IdGrado = idGrado ?? 1;

            // Obtener nombre del grupo
            try
            {
                var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == (idGrupo ?? 1));
                ViewBag.NombreGrupo = grupo != null ? grupo.Nombre : "Grupo " + (idGrupo ?? 1).ToString();
            }
            catch
            {
                ViewBag.NombreGrupo = "Grupo " + (idGrupo ?? 1).ToString();
            }

            // USAR CONSTANTE VIEWS PARA LA CARPETA
            return View("~/" + VIEWS + "/Index.cshtml");
        }

        // MÉTODO PROCESAREXCEL - LÓGICA DE IMPORTACIÓN, VALIDACIÓN Y GUARDADO

        [HttpPost]
        public ActionResult ProcesarExcel(HttpPostedFileBase archivoExcel, int? idTutoriaGrupal, int? idGrupo, int? idCarrera, int? idGrado)
        {
            var resultado = new ResultadoImportacion();
            bool esAjax = Request.IsAjaxRequest();

            System.Diagnostics.Debug.WriteLine($"🔍 PARÁMETROS RECIBIDOS:");
            System.Diagnostics.Debug.WriteLine($"   IdTutoriaGrupal: {idTutoriaGrupal}");
            System.Diagnostics.Debug.WriteLine($"   IdGrupo: {idGrupo}, IdCarrera: {idCarrera}, IdGrado: {idGrado}");

            try
            {

                // VALIDACIÓN 1: ARCHIVO SELECCIONADO
                if (archivoExcel == null || archivoExcel.ContentLength == 0)
                {
                    resultado.Exito = false;
                    resultado.MensajeGeneral = "❌ No se seleccionó ningún archivo o el archivo está vacío.";

                    if (esAjax)
                    {
                        return Json(new
                        {
                            exito = false,
                            mensaje = resultado.MensajeGeneral,
                            html = RenderPartialViewToString("~/" + VIEWS + "/ResultadoParcial.cshtml", resultado)
                        });
                    }
                    return View("~/" + VIEWS + "/Resultado.cshtml", resultado);
                }

                // VALIDACIÓN 2: EXTENSIÓN DE ARCHIVO (.xlsx o .xls)
                var extension = Path.GetExtension(archivoExcel.FileName);
                if (extension != null)
                    extension = extension.ToLower();

                if (extension != ".xlsx" && extension != ".xls")
                {
                    resultado.Exito = false;
                    resultado.MensajeGeneral = "❌ El archivo debe ser un archivo Excel (.xlsx o .xls).";

                    if (esAjax)
                    {
                        return Json(new
                        {
                            exito = false,
                            mensaje = resultado.MensajeGeneral,
                            html = RenderPartialViewToString("~/" + VIEWS + "/ResultadoParcial.cshtml", resultado)
                        });
                    }
                    return View("~/" + VIEWS + "/Resultado.cshtml", resultado);
                }

                // Usar valores por defecto para parámetros del grupo
                int grupoId = idGrupo ?? 1;
                int carreraId = idCarrera ?? 1;
                int gradoId = idGrado ?? 1;

                // Obtener información del grupo para el reporte
                var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == grupoId);
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId);
                var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == gradoId);

                resultado.NombreGrupo = grupo != null ? grupo.Nombre : grupoId.ToString();
                resultado.NombreCarrera = carrera != null ? carrera.Nombre : "Carrera " + carreraId;
                resultado.NombreGrado = grado != null ? grado.Nombre : "Grado " + gradoId;

                // PROCESAR ARCHIVO EXCEL
                using (var stream = archivoExcel.InputStream)
                {
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets.First();
                        int tutoriaGrupalId = idTutoriaGrupal ?? 0;
                        var resultadoProcesamiento = ProcesarHojaExcel(worksheet, tutoriaGrupalId, grupoId, carreraId, gradoId);

                        // Combinar resultados del procesamiento
                        resultado.Exito = resultadoProcesamiento.Exito;
                        resultado.MensajeGeneral = resultadoProcesamiento.MensajeGeneral;
                        resultado.AlumnosProcesados = resultadoProcesamiento.AlumnosProcesados;
                        resultado.AlumnosConErrores = resultadoProcesamiento.AlumnosConErrores;
                        resultado.DetallesImportacion = resultadoProcesamiento.DetallesImportacion;
                        resultado.TotalCalificacionesImportadas = resultadoProcesamiento.TotalCalificacionesImportadas;
                        resultado.TotalCalificacionesYaExistian = resultadoProcesamiento.TotalCalificacionesYaExistian;
                    }
                }

                // RESPUESTA SEGÚN TIPO DE PETICIÓN (AJAX O NORMAL)
                if (esAjax)
                {
                    return Json(new
                    {
                        exito = resultado.Exito,
                        mensaje = resultado.MensajeGeneral,
                        totalImportadas = resultado.TotalCalificacionesImportadas,
                        totalErrores = resultado.AlumnosConErrores?.Count ?? 0,
                        html = RenderPartialViewToString("~/" + VIEWS + "/ResultadoParcial.cshtml", resultado)
                    });
                }
                else
                {
                    return View("~/" + VIEWS + "/Resultado.cshtml", resultado);
                }
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                // ✅ AGREGADO: Mostrar el error completo con stack trace
                resultado.MensajeGeneral = string.Format("❌ Error al procesar el archivo: {0}\n\nDetalles técnicos: {1}",
                    ex.Message,
                    ex.InnerException?.Message ?? "Sin detalles adicionales");

                // ✅ AGREGADO: Log en consola del servidor
                System.Diagnostics.Debug.WriteLine($"ERROR COMPLETO: {ex.ToString()}");
                System.Diagnostics.Debug.WriteLine($"STACK TRACE: {ex.StackTrace}");

                if (esAjax)
                {
                    return Json(new
                    {
                        exito = false,
                        mensaje = resultado.MensajeGeneral,
                        errorCompleto = ex.ToString(), // ✅ AGREGADO para debugging
                        html = RenderPartialViewToString("~/" + VIEWS + "/ResultadoParcial.cshtml", resultado)
                    });
                }
                return View("~/" + VIEWS + "/Resultado.cshtml", resultado);
            }
        }

        //  MÉTODO PARA PROCESAR EXCEL VÍA AJAX ESPECÍFICAMENTE

        [HttpPost]
        public ActionResult ProcesarExcelAjax(HttpPostedFileBase archivoExcel, int? idTutoriaGrupal, int? idGrupo, int? idCarrera, int? idGrado)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 AJAX - PARÁMETROS RECIBIDOS:");
            System.Diagnostics.Debug.WriteLine($"   IdTutoriaGrupal: {idTutoriaGrupal}");
            System.Diagnostics.Debug.WriteLine($"   IdGrupo: {idGrupo}, IdCarrera: {idCarrera}, IdGrado: {idGrado}");
            {
                var resultado = new ResultadoImportacion();

                // ✅ LOGGING CRÍTICO - Ver qué parámetros llegan
                System.Diagnostics.Debug.WriteLine("╔═══════════════════════════════════════════════════════════");
                System.Diagnostics.Debug.WriteLine("║ 🔍 INICIO ProcesarExcelAjax");
                System.Diagnostics.Debug.WriteLine("╠═══════════════════════════════════════════════════════════");
                System.Diagnostics.Debug.WriteLine($"║ idTutoriaGrupal: {idTutoriaGrupal?.ToString() ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"║ idGrupo: {idGrupo?.ToString() ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"║ idCarrera: {idCarrera?.ToString() ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"║ idGrado: {idGrado?.ToString() ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"║ Archivo: {archivoExcel?.FileName ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine("╚═══════════════════════════════════════════════════════════");


                try
                {
                    // VALIDACIÓN 1: ARCHIVO SELECCIONADO
                    if (archivoExcel == null || archivoExcel.ContentLength == 0)
                    {
                        return Json(new
                        {
                            exito = false,
                            mensaje = "❌ No se seleccionó ningún archivo o el archivo está vacío."
                        });
                    }

                    // VALIDACIÓN 2: EXTENSIÓN DE ARCHIVO (.xlsx o .xls)
                    var extension = Path.GetExtension(archivoExcel.FileName);
                    if (extension != null)
                        extension = extension.ToLower();

                    if (extension != ".xlsx" && extension != ".xls")
                    {
                        return Json(new
                        {
                            exito = false,
                            mensaje = "❌ El archivo debe ser un archivo Excel (.xlsx o .xls)."
                        });
                    }

                    // Usar valores por defecto para parámetros del grupo
                    int grupoId = idGrupo ?? 1;
                    int carreraId = idCarrera ?? 1;
                    int gradoId = idGrado ?? 1;

                    // Obtener información del grupo para el reporte
                    var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == grupoId);
                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId);
                    var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == gradoId);

                    resultado.NombreGrupo = grupo != null ? grupo.Nombre : grupoId.ToString();
                    resultado.NombreCarrera = carrera != null ? carrera.Nombre : "Carrera " + carreraId;
                    resultado.NombreGrado = grado != null ? grado.Nombre : "Grado " + gradoId;

                    // PROCESAR ARCHIVO EXCEL
                    using (var stream = archivoExcel.InputStream)
                    {
                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets.First();
                            int tutoriaGrupalId = idTutoriaGrupal ?? 0;
                            var resultadoProcesamiento = ProcesarHojaExcel(worksheet, tutoriaGrupalId, grupoId, carreraId, gradoId);

                            // Combinar resultados del procesamiento
                            resultado.Exito = resultadoProcesamiento.Exito;
                            resultado.MensajeGeneral = resultadoProcesamiento.MensajeGeneral;
                            resultado.AlumnosProcesados = resultadoProcesamiento.AlumnosProcesados;
                            resultado.AlumnosConErrores = resultadoProcesamiento.AlumnosConErrores;
                            resultado.DetallesImportacion = resultadoProcesamiento.DetallesImportacion;
                            resultado.TotalCalificacionesImportadas = resultadoProcesamiento.TotalCalificacionesImportadas;
                            resultado.TotalCalificacionesYaExistian = resultadoProcesamiento.TotalCalificacionesYaExistian;
                        }
                    }

                    // GENERAR HTML PARCIAL PARA EL MODAL
                    string htmlResultado = RenderPartialViewToString("~/" + VIEWS + "/ResultadoParcial.cshtml", resultado);

                    return Json(new
                    {
                        exito = resultado.Exito,
                        mensaje = resultado.MensajeGeneral,
                        totalImportadas = resultado.TotalCalificacionesImportadas,
                        totalErrores = resultado.AlumnosConErrores?.Count ?? 0,
                        alumnosExitosos = resultado.AlumnosProcesados?.Count ?? 0,
                        html = htmlResultado
                    });
                }
                catch (Exception ex)
                {
                    return Json(new
                    {
                        exito = false,
                        mensaje = string.Format("❌ Error al procesar el archivo: {0}", ex.Message)
                    });
                }
            }
        }

        // MÉTODO AUXILIAR PARA RENDERIZAR VISTA PARCIAL COMO STRING

        private string RenderPartialViewToString(string viewName, object model)
        {
            if (string.IsNullOrEmpty(viewName))
                viewName = ControllerContext.RouteData.GetRequiredString("action");

            ViewData.Model = model;

            using (var sw = new StringWriter())
            {
                var viewResult = ViewEngines.Engines.FindPartialView(ControllerContext, viewName);
                var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw);
                viewResult.View.Render(viewContext, sw);

                return sw.GetStringBuilder().ToString();
            }
        }

        // MÉTODO PRINCIPAL PARA PROCESAR HOJA DE EXCEL
        private ResultadoImportacion ProcesarHojaExcel(ExcelWorksheet worksheet, int idTutoriaGrupal, int grupoId, int carreraId, int gradoId)
        {
            var resultado = new ResultadoImportacion();
            var alumnosProcesados = new List<AlumnoImportado>();
            var alumnosConErrores = new List<AlumnoImportado>();
            var detallesImportacion = new List<string>();
            int totalCalificaciones = 0;

            try
            {
                if (worksheet == null || worksheet.Dimension == null)
                {
                    resultado.Exito = false;
                    resultado.MensajeGeneral = "❌ Error: El archivo Excel está vacío";
                    return resultado;
                }

                // ==============================================================================
                // 1. OBTENER LA ESPECIALIDAD DEL GRUPO (CRÍTICO)
                // ==============================================================================
                var datosGrupo = db.Database.SqlQuery<GrupoInfoTemp>(@"
    SELECT 
        tg.IdEspecialidad, 
        e.Nombre as NombreEspecialidad
    FROM TutoriaGrupals tg
    LEFT JOIN Especialidads e ON tg.IdEspecialidad = e.Id
    WHERE tg.IdTutoriaGrupal = @p0",
                    idTutoriaGrupal).FirstOrDefault();

                System.Diagnostics.Debug.WriteLine($"🔍 GRUPO ENCONTRADO:");
                System.Diagnostics.Debug.WriteLine($"   IdTutoriaGrupal: {idTutoriaGrupal}");
                System.Diagnostics.Debug.WriteLine($"   IdEspecialidad: {datosGrupo?.IdEspecialidad}");
                System.Diagnostics.Debug.WriteLine($"   Nombre: {datosGrupo?.NombreEspecialidad}");

                int? idEspecialidadGrupo = datosGrupo?.IdEspecialidad;
                string nombreEspecialidadGrupo = datosGrupo?.NombreEspecialidad ?? "Tronco Común";

                detallesImportacion.Add($"📋 Grupo configurado como: {nombreEspecialidadGrupo} (ID: {idEspecialidadGrupo})");

                // ==============================================================================
                // 2. CREAR LA "LISTA BLANCA" DE MATERIAS VÁLIDAS PARA ESTE GRUPO
                // ==============================================================================
                // Traemos TODAS las materias posibles para este grado/carrera
                var todasLasMaterias = db.Database.SqlQuery<MateriaConUnidadesInfo>(
                    @"SELECT m.IdMateria, m.Nombre, m.NumeroUnidades, m.IdEspecialidad, 
                     ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima,
                     ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
              FROM Materias m
              LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
              WHERE m.IdCarrera = @carrera AND m.IdGrado = @grado AND m.Activo = 1",
                    new System.Data.SqlClient.SqlParameter("@carrera", carreraId),
                    new System.Data.SqlClient.SqlParameter("@grado", gradoId)
                ).ToList();

                // FILTRADO ESTRICTO EN MEMORIA:
                // Solo aceptamos materias que:
                // A) Tengan la MISMA especialidad que el grupo.
                // B) No tengan especialidad (Tronco Común, null o 0).
                // C) Descartamos explícitamente las que tengan un ID de especialidad DIFERENTE al del grupo.
                var materiasValidas = todasLasMaterias.Where(m =>
                {
                    // 1. Si el grupo tiene especialidad y la materia coincide → VÁLIDA
                    if (idEspecialidadGrupo.HasValue && m.IdEspecialidad == idEspecialidadGrupo.Value)
                        return true;

                    // 2. Si la materia es Tronco Común (IdEspecialidad es 0 o NULL) → VÁLIDA
                    if (m.IdEspecialidad == 0 || !m.IdEspecialidad.HasValue)
                        return true;

                    // 3. En cualquier otro caso → RECHAZAR (es de otra especialidad)
                    return false;
                }).ToList();

                // ==============================================================================
                // 3. MAPEAR COLUMNAS DEL EXCEL USANDO LA LISTA BLANCA
                // ==============================================================================
                var materiasPorColumna = new Dictionary<int, MateriaConUnidadesInfo>();
                int col = 3;

                while (col <= worksheet.Dimension.End.Column)
                {
                    var nombreMateriaExcel = worksheet.Cells[1, col].Text.Trim();
                    if (string.IsNullOrEmpty(nombreMateriaExcel)) break;

                    // ============================================================================
                    // 🔥 NUEVA LÓGICA DE BÚSQUEDA PRIORIZADA
                    // ============================================================================
                    // Prioridad 1: Buscar materia con el mismo nombre Y la misma especialidad del grupo
                    // Prioridad 2: Buscar materia con el mismo nombre Y que sea Tronco Común (NULL o 0)
                    // Prioridad 3: Búsqueda flexible (contiene) con la misma especialidad
                    // Prioridad 4: Búsqueda flexible en tronco común
                    // ============================================================================

                    MateriaConUnidadesInfo materiaCandidata = null;

                    // PRIORIDAD 1: Coincidencia EXACTA de nombre + especialidad del grupo
                    if (idEspecialidadGrupo.HasValue && idEspecialidadGrupo.Value > 0)
                    {
                        materiaCandidata = materiasValidas.FirstOrDefault(m =>
                            string.Equals(m.Nombre.Trim(), nombreMateriaExcel, StringComparison.OrdinalIgnoreCase) &&
                            m.IdEspecialidad.HasValue &&
                            m.IdEspecialidad.Value == idEspecialidadGrupo.Value
                        );

                        if (materiaCandidata != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ PRIORIDAD 1: '{nombreMateriaExcel}' → IdMateria: {materiaCandidata.IdMateria} (Especialidad: {idEspecialidadGrupo})");
                        }
                    }

                    // PRIORIDAD 2: Coincidencia EXACTA de nombre + Tronco Común (si no encontró en Prioridad 1)
                    if (materiaCandidata == null)
                    {
                        materiaCandidata = materiasValidas.FirstOrDefault(m =>
                            string.Equals(m.Nombre.Trim(), nombreMateriaExcel, StringComparison.OrdinalIgnoreCase) &&
                            (!m.IdEspecialidad.HasValue || m.IdEspecialidad.Value == 0)
                        );

                        if (materiaCandidata != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ PRIORIDAD 2: '{nombreMateriaExcel}' → IdMateria: {materiaCandidata.IdMateria} (Tronco Común)");
                        }
                    }

                    // PRIORIDAD 3: Búsqueda flexible (contiene) + especialidad del grupo
                    if (materiaCandidata == null && idEspecialidadGrupo.HasValue && idEspecialidadGrupo.Value > 0)
                    {
                        materiaCandidata = materiasValidas.FirstOrDefault(m =>
                            (m.Nombre.Trim().ToLower().Contains(nombreMateriaExcel.ToLower()) ||
                             nombreMateriaExcel.ToLower().Contains(m.Nombre.Trim().ToLower())) &&
                            m.IdEspecialidad.HasValue &&
                            m.IdEspecialidad.Value == idEspecialidadGrupo.Value
                        );

                        if (materiaCandidata != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ PRIORIDAD 3: '{nombreMateriaExcel}' → IdMateria: {materiaCandidata.IdMateria} (Búsqueda flexible + Especialidad)");
                        }
                    }

                    // PRIORIDAD 4: Búsqueda flexible + Tronco Común (último recurso)
                    if (materiaCandidata == null)
                    {
                        materiaCandidata = materiasValidas.FirstOrDefault(m =>
                            (m.Nombre.Trim().ToLower().Contains(nombreMateriaExcel.ToLower()) ||
                             nombreMateriaExcel.ToLower().Contains(m.Nombre.Trim().ToLower())) &&
                            (!m.IdEspecialidad.HasValue || m.IdEspecialidad.Value == 0)
                        );

                        if (materiaCandidata != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ PRIORIDAD 4: '{nombreMateriaExcel}' → IdMateria: {materiaCandidata.IdMateria} (Búsqueda flexible + Tronco Común)");
                        }
                    }

                    // ============================================================================
                    // PROCESAR LA MATERIA ENCONTRADA
                    // ============================================================================
                    if (materiaCandidata != null)
                    {
                        // ENCONTRADA: Asignar columnas para cada unidad
                        materiaCandidata.ColumnasUnidades = new Dictionary<int, int>();
                        for (int u = 1; u <= materiaCandidata.NumeroUnidades; u++)
                        {
                            materiaCandidata.ColumnasUnidades[u] = col;
                            col++;
                        }
                        materiasPorColumna[materiaCandidata.ColumnasUnidades[1]] = materiaCandidata;

                        // Logging mejorado
                        string tipo;
                        if (materiaCandidata.IdEspecialidad.HasValue && materiaCandidata.IdEspecialidad.Value > 0)
                        {
                            if (idEspecialidadGrupo.HasValue && materiaCandidata.IdEspecialidad.Value == idEspecialidadGrupo.Value)
                            {
                                tipo = $"✅ ESPECIALIDAD CORRECTA (ID: {materiaCandidata.IdEspecialidad.Value})";
                            }
                            else
                            {
                                tipo = $"⚠️ OTRA ESPECIALIDAD (ID: {materiaCandidata.IdEspecialidad.Value})";
                            }
                        }
                        else
                        {
                            tipo = "✅ TRONCO COMÚN";
                        }

                        string mensaje = $"✓ Mapeada: {materiaCandidata.Nombre} → IdMateria: {materiaCandidata.IdMateria} [{tipo}]";
                        detallesImportacion.Add(mensaje);
                        System.Diagnostics.Debug.WriteLine(mensaje);
                    }
                    else
                    {
                        // No está en la lista blanca (ignorar)
                        string mensajeIgnorado = $"⚠️ Ignorada: '{nombreMateriaExcel}' (No corresponde a {nombreEspecialidadGrupo})";
                        detallesImportacion.Add(mensajeIgnorado);
                        System.Diagnostics.Debug.WriteLine(mensajeIgnorado);

                        // Saltar columnas (estimación de unidades)
                        var materiaSucia = todasLasMaterias.FirstOrDefault(m =>
                            string.Equals(m.Nombre.Trim(), nombreMateriaExcel, StringComparison.OrdinalIgnoreCase)
                        );
                        int salto = materiaSucia != null ? materiaSucia.NumeroUnidades : 1;
                        col += salto;
                    }
                }

                // ============================================================================
                // VERIFICACIÓN ADICIONAL: LOGGING DE TODAS LAS MATERIAS MAPEADAS
                // ============================================================================
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"RESUMEN DE MAPEO - Grupo: {nombreEspecialidadGrupo} (ID Esp: {idEspecialidadGrupo})");
                System.Diagnostics.Debug.WriteLine("========================================");
                foreach (var kvp in materiasPorColumna)
                {
                    var mat = kvp.Value;
                    System.Diagnostics.Debug.WriteLine($"  - IdMateria: {mat.IdMateria} | Nombre: {mat.Nombre} | IdEspecialidad: {mat.IdEspecialidad}");
                }
                System.Diagnostics.Debug.WriteLine("========================================");


                // ==============================================================================
                // 4. PROCESAR ALUMNOS (Igual que siempre)
                // ==============================================================================
                int fila = 3;
                while (fila <= worksheet.Dimension.End.Row)
                {
                    var matricula = worksheet.Cells[fila, 1].Text.Trim();
                    var nombre = worksheet.Cells[fila, 2].Text.Trim();

                    if (string.IsNullOrEmpty(matricula)) break;

                    var resultadoAlumno = ProcesarFilaAlumnoConUnidades(
                        worksheet, fila, matricula, nombre,
                        materiasPorColumna, idTutoriaGrupal, grupoId, carreraId, gradoId
                    );

                    if (resultadoAlumno.TieneErrores)
                        alumnosConErrores.Add(resultadoAlumno);
                    else
                    {
                        alumnosProcesados.Add(resultadoAlumno);
                        totalCalificaciones += resultadoAlumno.CalificacionesImportadas;
                    }
                    fila++;
                }

                // RESULTADOS FINALES
                resultado.AlumnosProcesados = alumnosProcesados;
                resultado.AlumnosConErrores = alumnosConErrores;
                resultado.DetallesImportacion = detallesImportacion;
                resultado.TotalCalificacionesImportadas = totalCalificaciones;

                if (alumnosConErrores.Count == 0 && alumnosProcesados.Count > 0)
                {
                    resultado.MensajeGeneral = $"🎉 Éxito: {totalCalificaciones} calificaciones importadas al grupo {nombreEspecialidadGrupo}.";
                    resultado.Exito = true;
                }
                else if (alumnosProcesados.Count > 0)
                {
                    resultado.MensajeGeneral = $"⚠️ Importación parcial.";
                    resultado.Exito = true;
                }
                else
                {
                    resultado.MensajeGeneral = "❌ No se importaron alumnos.";
                    resultado.Exito = false;
                }

                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.MensajeGeneral = "❌ Error técnico: " + ex.Message;
                return resultado;
            }
        }

        // MÉTODO PARA LEER ENCABEZADOS DE LA PRIMERA FILA

        private List<string> LeerEncabezados(ExcelWorksheet worksheet)
        {
            var encabezados = new List<string>();
            int columna = 1;

            while (columna <= worksheet.Dimension.End.Column)
            {
                var valorCelda = worksheet.Cells[1, columna].Value;
                var valorTexto = valorCelda != null ? valorCelda.ToString().Trim() : "";

                if (string.IsNullOrEmpty(valorTexto))
                    break;

                encabezados.Add(valorTexto);
                columna++;
            }

            return encabezados;
        }

        // MÉTODO PARA OBTENER MATERIAS DEL GRUPO DESDE LA BD

        private List<MateriaDB> ObtenerMateriasPorGrupo(int carreraId, int gradoId)
        {
            try
            {
                return db.Materias
                    .Where(m => m.IdCarrera == carreraId && m.IdGrado == gradoId && m.Activo == true)
                    .Select(m => new MateriaDB { IdMateria = m.IdMateria, Nombre = m.Nombre })
                    .ToList();
            }
            catch
            {
                return new List<MateriaDB>();
            }
        }

        // MÉTODO PARA VALIDAR CORRESPONDENCIA ENTRE EXCEL Y BD

        private Dictionary<int, int> ValidarCorrespondenciaMaterias(List<string> encabezados, List<MateriaDB> materiasDB)
        {
            var correspondencias = new Dictionary<int, int>(); // columnaExcel -> idMateria

            for (int i = 2; i < encabezados.Count; i++) // Empezar desde columna 3 (después de Matrícula y Nombre)
            {
                var nombreMateriaExcel = encabezados[i].Trim();

                // EMPAREJAR: Buscar materia correspondiente en la BD (búsqueda flexible)
                var materiaEncontrada = materiasDB.FirstOrDefault(m =>
                    string.Equals(m.Nombre.Trim(), nombreMateriaExcel, StringComparison.OrdinalIgnoreCase) ||
                    m.Nombre.Trim().Contains(nombreMateriaExcel) ||
                    nombreMateriaExcel.Contains(m.Nombre.Trim())
                );

                if (materiaEncontrada != null)
                {
                    correspondencias[i + 1] = materiaEncontrada.IdMateria;
                }
            }

            return correspondencias;
        }

        // MÉTODO PARA PROCESAR UNA FILA DE ALUMNO - MODIFICADO

        // ✅ PROCESAR ALUMNO CON UNIDADES
        private AlumnoImportado ProcesarFilaAlumnoConUnidades(
            ExcelWorksheet worksheet, int fila, string matricula, string nombre,
            Dictionary<int, MateriaConUnidadesInfo> materiasPorColumna,
            int idTutoriaGrupal, int grupoId, int carreraId, int gradoId)
        {
            var resultado = new AlumnoImportado
            {
                Matricula = matricula,
                NombreExcel = nombre
            };

            var errores = new List<string>();
            var calificacionesImportadas = new List<CalificacionImportada>();

            try
            {
                // Validar que el alumno exista
                // Primero obtener los datos completos del grupo desde TutoriaGrupals
                var grupoCompleto = db.TutoriaGrupals.FirstOrDefault(tg => tg.IdTutoriaGrupal == idTutoriaGrupal);

                if (grupoCompleto == null)
                {
                    errores.Add($"No se encontró el grupo con IdTutoriaGrupal: {idTutoriaGrupal}");
                    resultado.TieneErrores = true;
                    resultado.Errores = errores;
                    return resultado;
                }

                // Buscar al alumno usando TODOS los campos del grupo
                var alumnoEnBD = db.DatosPersonales.FirstOrDefault(dp =>
                    dp.Matricula.Trim().Equals(matricula, StringComparison.OrdinalIgnoreCase) &&
                    dp.IdGrupo == grupoCompleto.IdGrupo &&
                    dp.IdCarrera == grupoCompleto.IdCarrera &&
                    dp.IdGrado == grupoCompleto.IdGrado &&
                    dp.IdTurno == grupoCompleto.IdTurno &&
                    dp.IdPeriodo == grupoCompleto.IdPeriodo &&
                    dp.Año == grupoCompleto.Año);
                if (alumnoEnBD == null)
                {
                    errores.Add($"Alumno con matrícula '{matricula}' no encontrado en el grupo");
                    resultado.TieneErrores = true;
                    resultado.Errores = errores;
                    return resultado;
                }

                resultado.IdPersona = alumnoEnBD.IdPersona;
                resultado.NombreBD = alumnoEnBD.Nombre;

                // ✅ PROCESAR CADA MATERIA Y SUS UNIDADES
                foreach (var materiaInfo in materiasPorColumna.Values)
                {
                    var unidadesCalificadas = new List<decimal>();
                    bool todasUnidadesValidas = true;

                    // Leer calificaciones de cada unidad
                    for (int u = 1; u <= materiaInfo.NumeroUnidades; u++)
                    {
                        var colUnidad = materiaInfo.ColumnasUnidades[u];
                        var valorCelda = worksheet.Cells[fila, colUnidad].Text.Trim();

                        if (string.IsNullOrEmpty(valorCelda))
                        {
                            todasUnidadesValidas = false;
                            continue;
                        }

                        if (decimal.TryParse(valorCelda, out decimal calificacionUnidad))
                        {
                            if (calificacionUnidad < 0 || calificacionUnidad > 10)
                            {
                                errores.Add($"{materiaInfo.Nombre} U{u}: Calificación fuera de rango (0-10)");
                                todasUnidadesValidas = false;
                                continue;
                            }

                            // Aplicar reglas del plan
                            decimal calificacionAjustada = calificacionUnidad;
                            if (!materiaInfo.PermiteDecimales)
                            {
                                if (calificacionUnidad < 8.0m)
                                    calificacionAjustada = Math.Floor(calificacionUnidad);
                                else
                                    calificacionAjustada = Math.Round(calificacionUnidad, 0);
                            }
                            else
                            {
                                calificacionAjustada = Math.Round(calificacionUnidad, 2);
                            }

                            unidadesCalificadas.Add(calificacionAjustada);
                        }
                        else
                        {
                            errores.Add($"{materiaInfo.Nombre} U{u}: Valor no numérico '{valorCelda}'");
                            todasUnidadesValidas = false;
                        }
                    }

                    // ✅ SI TODAS LAS UNIDADES ESTÁN CALIFICADAS, PROCESAR
                    if (todasUnidadesValidas && unidadesCalificadas.Count == materiaInfo.NumeroUnidades)
                    {
                        // Calcular promedio
                        decimal promedio = unidadesCalificadas.Average();

                        // 🔴 APLICAR REGLAS SEGÚN EL PLAN
                        decimal calificacionFinal;
                        string estado;

                        if (!materiaInfo.PermiteDecimales) // PLAN 2020
                        {
                            // ══════════════════════════════════════════════════════════════
                            // PLAN 2020: TODAS LAS UNIDADES DEBEN SER ≥ 8
                            // ══════════════════════════════════════════════════════════════
                            bool tieneUnidadReprobada = unidadesCalificadas.Any(u => u < 8.0m);

                            if (tieneUnidadReprobada)
                            {
                                // SI TIENE ALGUNA UNIDAD < 8 → REPROBATORIA (MÁXIMO 7)
                                if (promedio < 8.0m)
                                    calificacionFinal = Math.Floor(promedio);
                                else
                                    calificacionFinal = 7.0m; // Castigo: tope en 7 aunque el promedio sea > 8

                                estado = "Extraordinario";
                            }
                            else
                            {
                                // TODAS LAS UNIDADES ≥ 8 → Aplicar redondeo normal
                                if (promedio < 8.0m)
                                    calificacionFinal = Math.Floor(promedio);
                                else
                                    calificacionFinal = Math.Round(promedio, 0);

                                // Estado según calificación final (mínima 8.0 para Plan 2020)
                                estado = calificacionFinal >= 8.0m ? "Acreditada" : "Extraordinario";
                            }
                        }
                        else // PLAN 2024
                        {
                            // ══════════════════════════════════════════════════════════════
                            // PLAN 2024: SOLO IMPORTA EL PROMEDIO FINAL ≥ 7
                            // ══════════════════════════════════════════════════════════════
                            calificacionFinal = Math.Round(promedio, 2);
                            estado = calificacionFinal >= 7.0m ? "Acreditada" : "Extraordinario";
                        }

                        // ✅ GUARDAR EN BASE DE DATOS
                        var resultadoGuardado = GuardarCalificacionesConUnidades(
                            resultado.IdPersona,
                            materiaInfo.IdMateria,
                            calificacionFinal,
                            estado,
                            unidadesCalificadas
                        );

                        if (resultadoGuardado.Exito)
                        {
                            calificacionesImportadas.Add(new CalificacionImportada
                            {
                                NombreMateria = materiaInfo.Nombre,
                                Calificacion = calificacionFinal,
                                Estado = estado,
                                Mensaje = $"✓ Guardada con {materiaInfo.NumeroUnidades} unidades",
                                Tipo = TipoOperacion.Guardada
                            });
                        }
                        else
                        {
                            if (resultadoGuardado.Mensaje.Contains("Ya tiene calificación"))
                            {
                                calificacionesImportadas.Add(new CalificacionImportada
                                {
                                    NombreMateria = materiaInfo.Nombre,
                                    Calificacion = calificacionFinal,
                                    Estado = estado,
                                    Mensaje = "⚠️ " + resultadoGuardado.Mensaje,
                                    Tipo = TipoOperacion.YaExistia
                                });
                            }
                            else
                            {
                                errores.Add($"Error en {materiaInfo.Nombre}: {resultadoGuardado.Mensaje}");
                            }
                        }
                    }
                    else if (unidadesCalificadas.Count > 0)
                    {
                        errores.Add($"{materiaInfo.Nombre}: Solo {unidadesCalificadas.Count}/{materiaInfo.NumeroUnidades} unidades calificadas");
                    }
                }

                resultado.CalificacionesImportadas = calificacionesImportadas.Count(c => c.Tipo == TipoOperacion.Guardada);
                resultado.CalificacionesYaExistian = calificacionesImportadas.Count(c => c.Tipo == TipoOperacion.YaExistia);
                resultado.Calificaciones = calificacionesImportadas;
                resultado.TieneErrores = errores.Count > 0;
                resultado.Errores = errores;

                return resultado;
            }
            catch (Exception ex)
            {
                errores.Add($"Error interno: {ex.Message}");
                resultado.TieneErrores = true;
                resultado.Errores = errores;
                return resultado;
            }
        }

        // DTO interno para detectar duplicados por nombre+grado
        private class RegistroMateriaExistente
        {
            public int Id { get; set; }
            public decimal? Calificacion { get; set; }
        }

        // ✅ GUARDAR CALIFICACIONES CON UNIDADES (versión anti-duplicados)
        private ResultadoGuardado GuardarCalificacionesConUnidades(
            int idPersona, int idMateria, decimal calificacionFinal,
            string estado, List<decimal> calificacionesUnidades)
        {
            try
            {
                // ── VERIFICACIÓN 1: ¿Ya existe calificación para este IdMateria exacto? ──
                var calExacta = db.Database.SqlQuery<decimal?>(
                    "SELECT Calificacion FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona).FirstOrDefault();

                if (calExacta.HasValue && calExacta.Value > 0)
                    return new ResultadoGuardado { Exito = false, Mensaje = $"Ya tiene calificación: {calExacta.Value:F1}" };

                // ── VERIFICACIÓN 2: ¿Existe otro registro para la MISMA materia (nombre+grado)
                //    pero con diferente IdMateria (especialidad distinta)? ──
                // Esto previene crear duplicados cuando el mismo grupo se importó antes
                // con una especialidad incorrecta o diferente.
                var duplicado = db.Database.SqlQuery<RegistroMateriaExistente>(@"
                    SELECT ma.Id, ma.Calificacion
                    FROM MateriasAlumno ma
                    INNER JOIN Materias m    ON ma.IdMateria  = m.IdMateria
                    INNER JOIN Materias mNva ON mNva.IdMateria = @p0
                    WHERE ma.IdPersona  = @p1
                      AND m.Nombre      = mNva.Nombre
                      AND m.IdGrado     = mNva.IdGrado
                      AND ma.IdMateria <> @p0",
                    idMateria, idPersona).FirstOrDefault();

                int idMateriaAlumno;

                if (duplicado != null)
                {
                    if (duplicado.Calificacion.HasValue && duplicado.Calificacion.Value > 0)
                    {
                        // Ya calificada bajo otra especialidad → no crear duplicado
                        System.Diagnostics.Debug.WriteLine(
                            $"⚠️ [Import] Omitido duplicado: IdMateria={idMateria} IdPersona={idPersona} " +
                            $"— ya calificada en otro registro (Id={duplicado.Id}, Cal={duplicado.Calificacion:F1})");
                        return new ResultadoGuardado
                        {
                            Exito = false,
                            Mensaje = $"Ya tiene calificación para esta materia en otra especialidad ({duplicado.Calificacion.Value:F1}). Omitido para evitar duplicado."
                        };
                    }

                    // Existe un registro sin calificación (Pendiente) con especialidad incorrecta.
                    // Corregir: cambiar el IdMateria al correcto y aplicar la calificación.
                    System.Diagnostics.Debug.WriteLine(
                        $"🔧 [Import] Corrigiendo registro huérfano Id={duplicado.Id}: " +
                        $"IdMateria anterior → {idMateria} (especialidad correcta)");

                    db.Database.ExecuteSqlCommand(
                        "DELETE FROM CalificacionesUnidades WHERE IdMateriaAlumno = @p0",
                        duplicado.Id);

                    db.Database.ExecuteSqlCommand(@"
                        UPDATE MateriasAlumno
                        SET IdMateria = @p0, Calificacion = @p1, Estado = @p2,
                            IntentosExtraordinarios = @p3, FechaActualizacion = GETDATE()
                        WHERE Id = @p4",
                        idMateria, calificacionFinal, estado,
                        estado == "Extraordinario" ? 1 : 0,
                        duplicado.Id);

                    idMateriaAlumno = duplicado.Id;
                }
                else
                {
                    // ── FLUJO NORMAL: no hay duplicado ──
                    // ✅ 1. CREAR/ACTUALIZAR REGISTRO EN MateriasAlumno
                    var existe = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                        idMateria, idPersona).First();

                    if (existe > 0)
                    {
                        db.Database.ExecuteSqlCommand(
                            @"UPDATE MateriasAlumno
                      SET Calificacion = @p0, Estado = @p1,
                          IntentosExtraordinarios = @p2,
                          FechaActualizacion = GETDATE()
                      WHERE IdMateria = @p3 AND IdPersona = @p4",
                            calificacionFinal, estado,
                            estado == "Extraordinario" ? 1 : 0,
                            idMateria, idPersona);
                    }
                    else
                    {
                        db.Database.ExecuteSqlCommand(
                            @"INSERT INTO MateriasAlumno
                      (IdMateria, IdPersona, Calificacion, Estado, IntentosExtraordinarios, FechaRegistro, FechaActualizacion)
                      VALUES (@p0, @p1, @p2, @p3, @p4, GETDATE(), GETDATE())",
                            idMateria, idPersona, calificacionFinal, estado,
                            estado == "Extraordinario" ? 1 : 0);
                    }

                    idMateriaAlumno = db.Database.SqlQuery<int>(
                        "SELECT Id FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                        idMateria, idPersona).First();
                }

                // ✅ 2. GUARDAR CALIFICACIONES DE CADA UNIDAD
                for (int i = 0; i < calificacionesUnidades.Count; i++)
                {
                    int numeroUnidad = i + 1;
                    decimal calificacionUnidad = calificacionesUnidades[i];

                    var existeUnidad = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM CalificacionesUnidades 
                  WHERE IdMateriaAlumno = @p0 AND NumeroUnidad = @p1",
                        idMateriaAlumno, numeroUnidad
                    ).First();

                    if (existeUnidad > 0)
                    {
                        db.Database.ExecuteSqlCommand(
                            @"UPDATE CalificacionesUnidades 
                      SET Calificacion = @p0, FechaActualizacion = GETDATE()
                      WHERE IdMateriaAlumno = @p1 AND NumeroUnidad = @p2",
                            calificacionUnidad, idMateriaAlumno, numeroUnidad
                        );
                    }
                    else
                    {
                        db.Database.ExecuteSqlCommand(
                            @"INSERT INTO CalificacionesUnidades 
                      (IdMateriaAlumno, NumeroUnidad, Calificacion, FechaRegistro, FechaActualizacion)
                      VALUES (@p0, @p1, @p2, GETDATE(), GETDATE())",
                            idMateriaAlumno, numeroUnidad, calificacionUnidad
                        );
                    }
                }

                // ✅ 3. CREAR REGISTRO EN HISTORIAL DE INTENTOS (PARA EDICIÓN MASTER)
                // Sin esto, el usuario Master no podrá editar calificaciones importadas
                try
                {
                    // Verificar si ya existe un intento registrado
                    var existeIntento = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM HistorialIntentosMateria 
                  WHERE IdMateriaAlumno = @p0",
                        idMateriaAlumno
                    ).First();

                    if (existeIntento == 0)
                    {
                        // Obtener información del plan de estudios
                        var planInfo = db.Database.SqlQuery<PlanInfoDto>(
                            @"SELECT 
                        ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima,
                        ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
                      FROM Materias m
                      LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
                      WHERE m.IdMateria = @p0",
                            idMateria
                        ).FirstOrDefault();

                        decimal calificacionMinima = planInfo?.CalificacionMinima ?? 7.0m;
                        bool permiteDecimales = planInfo?.PermiteDecimales ?? true;

                        // Determinar si es aprobatoria
                        bool esAprobatoria = calificacionFinal >= calificacionMinima;

                        // Determinar tipo de intento
                        string tipoIntento = estado == "Acreditada" ? "Ordinario" : "Extraordinario";

                        // ✅ CORRECCIÓN: La tabla NO tiene columna 'Estado'
                        // Insertar en HistorialIntentosMateria (sin Estado)
                        db.Database.ExecuteSqlCommand(
                            @"INSERT INTO HistorialIntentosMateria 
                      (IdMateriaAlumno, NumeroIntento, TipoIntento, 
                       Calificacion, CalificacionAjustada, EsAprobatoria, 
                       FechaRegistro, Observaciones)
                      VALUES (@p0, 1, @p1, @p2, @p3, @p4, GETDATE(), @p5)",
                            idMateriaAlumno,
                            tipoIntento,
                            calificacionFinal,  // Calificacion (original)
                            calificacionFinal,  // CalificacionAjustada (igual porque ya viene ajustada)
                            esAprobatoria ? 1 : 0,
                            "Importado desde Excel"
                        );

                        System.Diagnostics.Debug.WriteLine($"✅ Historial creado para IdMateriaAlumno: {idMateriaAlumno}");
                    }
                }
                catch (Exception exHistorial)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Error creando historial: {exHistorial.Message}");
                    // No detener el proceso de importación si falla el historial
                }

                return new ResultadoGuardado
                {
                    Exito = true,
                    Mensaje = $"Guardado con {calificacionesUnidades.Count} unidades"
                };
            }
            catch (Exception ex)
            {
                return new ResultadoGuardado
                {
                    Exito = false,
                    Mensaje = ex.Message
                };
            }
        }

        public class GrupoInfoTemp
        {
            public int? IdEspecialidad { get; set; }
            public string NombreEspecialidad { get; set; }
        }

        // ✅ CLASE DTO AUXILIAR
        public class MateriaConUnidadesInfo
        {
            public int IdMateria { get; set; }
            public string Nombre { get; set; }
            public int NumeroUnidades { get; set; }
            public decimal CalificacionMinima { get; set; }
            public bool PermiteDecimales { get; set; }
            public string NombrePlan { get; set; }
            public int? IdEspecialidad { get; set; }
            public string NombreEspecialidad { get; set; }
            public Dictionary<int, int> ColumnasUnidades { get; set; }
        }


        // ✅ NUEVO MÉTODO: OBTENER PLANES DE ESTUDIO DE MÚLTIPLES MATERIAS
        private Dictionary<int, PlanEstudioInfo> ObtenerPlanesEstudioDeMaterias(List<int> idsMateria)
        {
            var resultado = new Dictionary<int, PlanEstudioInfo>();

            try
            {
                if (idsMateria == null || idsMateria.Count == 0)
                {
                    return resultado;
                }

                // Crear lista de parámetros para la consulta SQL
                var idsMateriasStr = string.Join(",", idsMateria);

                var query = string.Format(@"
            SELECT m.IdMateria, m.IdPlanEstudio,
                   ISNULL(p.Nombre, 'Plan Estándar') as NombrePlan,
                   ISNULL(p.CalificacionMinima, 7.0) as CalificacionMinima,
                   ISNULL(p.PermiteDecimales, 1) as PermiteDecimales
            FROM Materias m
            LEFT JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
            WHERE m.IdMateria IN ({0})", idsMateriasStr);

                var planesInfo = db.Database.SqlQuery<PlanEstudioInfoDto>(query).ToList();

                foreach (var plan in planesInfo)
                {
                    resultado[plan.IdMateria] = new PlanEstudioInfo
                    {
                        IdPlanEstudio = plan.IdPlanEstudio,
                        NombrePlan = plan.NombrePlan ?? "Plan Estándar",
                        CalificacionMinima = plan.CalificacionMinima ?? 7.0m,
                        PermiteDecimales = plan.PermiteDecimales ?? true
                    };
                }

                System.Diagnostics.Debug.WriteLine($"✅ Planes de estudio cargados: {resultado.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo planes de estudio: {ex.Message}");
            }

            return resultado;
        }

        // MÉTODO PARA GUARDAR CALIFICACIÓN EN LA BASE DE DATOS - CON VALIDACIÓN

        private ResultadoGuardado GuardarCalificacionAlumno(int idPersona, int idMateria, decimal calificacion, string estado)
        {
            try
            {
                // VERIFICAR SI YA EXISTE UNA CALIFICACIÓN REGISTRADA
                var calificacionExistente = db.Database.SqlQuery<decimal?>(
                    @"SELECT Calificacion FROM MateriasAlumno 
                      WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona
                ).FirstOrDefault();

                // VALIDACIÓN: Si ya tiene calificación registrada, NO modificar
                if (calificacionExistente.HasValue && calificacionExistente.Value > 0)
                {
                    return new ResultadoGuardado
                    {
                        Exito = false,
                        Mensaje = $"Ya tiene calificación registrada: {calificacionExistente.Value:F1}. No se modificó."
                    };
                }

                // VERIFICAR SI EXISTE EL REGISTRO (sin calificación o con calificación 0)
                var existe = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM MateriasAlumno WHERE IdMateria = @p0 AND IdPersona = @p1",
                    idMateria, idPersona
                ).First();

                int intentosExtraordinarios = estado == "Extraordinario" ? 1 : 0;

                if (existe > 0)
                {
                    // ACTUALIZAR registro existente (solo si no tenía calificación válida)
                    db.Database.ExecuteSqlCommand(
                        @"UPDATE MateriasAlumno 
                          SET Calificacion = @p0, Estado = @p1, 
                              IntentosExtraordinarios = @p2,
                              FechaActualizacion = GETDATE()
                          WHERE IdMateria = @p3 AND IdPersona = @p4",
                        calificacion, estado, intentosExtraordinarios, idMateria, idPersona
                    );
                }
                else
                {
                    // INSERTAR nuevo registro en tabla MateriasAlumno
                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO MateriasAlumno 
                          (IdMateria, IdPersona, Calificacion, Estado, IntentosExtraordinarios)
                          VALUES (@p0, @p1, @p2, @p3, @p4)",
                        idMateria, idPersona, calificacion, estado, intentosExtraordinarios
                    );
                }

                return new ResultadoGuardado { Exito = true, Mensaje = "Guardado correctamente" };
            }
            catch (Exception ex)
            {
                return new ResultadoGuardado { Exito = false, Mensaje = ex.Message };
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

    // CLASES DTO PARA EL PROCESO DE IMPORTACIÓN

    public class ResultadoImportacion
    {
        public bool Exito { get; set; }
        public string MensajeGeneral { get; set; }
        public string NombreGrupo { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreGrado { get; set; }
        public List<AlumnoImportado> AlumnosProcesados { get; set; } = new List<AlumnoImportado>();
        public List<AlumnoImportado> AlumnosConErrores { get; set; } = new List<AlumnoImportado>();
        public List<string> DetallesImportacion { get; set; } = new List<string>();
        public int TotalCalificacionesImportadas { get; set; }
        public int TotalCalificacionesYaExistian { get; set; }
    }

    public class AlumnoImportado
    {
        public int IdPersona { get; set; }
        public string Matricula { get; set; }
        public string NombreExcel { get; set; }
        public string NombreBD { get; set; }
        public int CalificacionesImportadas { get; set; }
        public int CalificacionesYaExistian { get; set; }
        public bool TieneErrores { get; set; }
        public List<string> Errores { get; set; } = new List<string>();
        public List<CalificacionImportada> Calificaciones { get; set; } = new List<CalificacionImportada>();
    }

    public class CalificacionImportada
    {
        public string NombreMateria { get; set; }
        public decimal Calificacion { get; set; }
        public string Estado { get; set; }
        public string Mensaje { get; set; }
        public TipoOperacion Tipo { get; set; }
    }

    public class MateriaDB
    {
        public int IdMateria { get; set; }
        public string Nombre { get; set; }
    }

    public class ResultadoGuardado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
    }

    public enum TipoOperacion
    {
        Guardada,
        YaExistia
    }
}

// ✅ CLASES DTO PARA INFORMACIÓN DE PLANES DE ESTUDIO

public class PlanEstudioInfo
{
    public int IdPlanEstudio { get; set; }
    public string NombrePlan { get; set; }
    public decimal CalificacionMinima { get; set; }
    public bool PermiteDecimales { get; set; }
}

public class PlanInfoDto
{
    public decimal CalificacionMinima { get; set; }
    public bool PermiteDecimales { get; set; }
}

public class PlanEstudioInfoDto
{
    public int IdMateria { get; set; }
    public int IdPlanEstudio { get; set; }
    public string NombrePlan { get; set; }
    public decimal? CalificacionMinima { get; set; }
    public bool? PermiteDecimales { get; set; }
}
