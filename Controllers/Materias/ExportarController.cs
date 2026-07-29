using OfficeOpenXml;
using OfficeOpenXml.DataValidation;
using OfficeOpenXml.Style;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Controllers.Materias;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers
{
    [CustomAuthorize(Nivel = 1)]
    public class ExportarController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();




        // ============================================================
        // MÉTODO CORREGIDO (REPORTE COMPLETO CON CALIFICACIONES)
        // ============================================================
        public ActionResult MateriasGrupo(int? idGrupo, int? idCarrera, int? idGrado, int? idPeriodo, int? año)
        {
            try
            {
                // Obtener periodo y año actual si no se proporcionan
                int periodoActual = idPeriodo ?? ObtenerPeriodoActual();
                int añoActual = año ?? DateTime.Now.Year;

                // Usar valores por defecto si no se proporcionan
                int grupoId = idGrupo ?? 1;
                int carreraId = idCarrera ?? 1;
                int gradoId = idGrado ?? 1;

                // Obtener información para encabezados
                var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == grupoId);
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == carreraId);
                var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == gradoId);
                var periodo = db.Periodos.FirstOrDefault(p => p.IdPeriodo == periodoActual);

                string nombreGrupo = grupo != null ? grupo.Nombre : grupoId.ToString();
                string nombreCarrera = carrera != null ? carrera.Nombre : "Carrera " + carreraId;
                string nombreGrado = grado != null ? grado.Nombre : "Grado " + gradoId;
                string nombrePeriodo = periodo != null ? periodo.Nombre : "Periodo " + periodoActual;

                // 1. OBTENER ALUMNOS
                var alumnosGrupo = db.DatosPersonales
                    .Where(dp => dp.IdGrupo == grupoId
                              && dp.IdCarrera == carreraId
                              && dp.IdGrado == gradoId
                              && dp.Estado == true
                              && dp.IdPeriodo == periodoActual
                              && dp.Año == añoActual)
                    .OrderBy(dp => dp.Nombre)
                    .ToList();

                if (!alumnosGrupo.Any())
                {
                    alumnosGrupo = new List<DatosPersonales>
            {
                new DatosPersonales { IdPersona = 1, Matricula = "000000", Nombre = "Sin alumnos en este grupo" }
            };
                }

                // =================================================================
                // ✅ CORRECCIÓN: USAR LÓGICA DE ID ESPECIALIDAD 
                // =================================================================

                // A. Buscar la configuración OFICIAL del grupo en TutoriaGrupals
                // Buscamos cualquier configuración que coincida con Grupo, Carrera, Grado, Periodo y Año
                // (Ignoramos el turno aquí porque las materias son las mismas para ambos turnos)
                var configuracionGrupo = db.TutoriaGrupals
                    .FirstOrDefault(t => t.IdGrupo == grupoId
                                      && t.IdCarrera == carreraId
                                      && t.IdGrado == gradoId
                                      && t.IdPeriodo == periodoActual
                                      && t.Año == añoActual);

                int? idEspecialidadOficial = configuracionGrupo?.IdEspecialidad;

                System.Diagnostics.Debug.WriteLine($"[Exportar] Estrategia ID: Especialidad Grupo = {idEspecialidadOficial ?? 0}");

                // B. Construir consulta SQL ROBUSTA
                // Trae materias que coincidan con la carrera, grado y:
                // 1. El ID de especialidad del grupo.
                // 2. O que sean materias de Tronco Común.
                // 3. O si no hay especialidad, las generales.
                var sqlQueryFinal = @"
            SELECT DISTINCT m.IdMateria, m.Nombre, m.NumeroUnidades
            FROM Materias m
            INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
            WHERE m.IdCarrera = @p0 
              AND m.IdGrado = @p1 
              AND m.Activo = 1
              AND (
                  -- 1. Coincidencia exacta por ID de Especialidad (Prioridad)
                  (@p2 IS NOT NULL AND m.IdEspecialidad = @p2)
                  OR
                  -- 2. Materias de Tronco Común (Por nombre)
                  (e.Nombre LIKE '%Común%' OR e.Nombre LIKE '%Comun%' OR e.Nombre LIKE '%Tronco%')
                  OR
                  -- 3. Fallback: Si el grupo no tiene especialidad (0 o null)
                  ((@p2 IS NULL OR @p2 = 0) AND e.IdCarrera = @p0 AND (e.Nombre = 'Sin Especialidad' OR e.Nombre = 'General'))
              )
            ORDER BY m.Nombre";

                // Parámetros: @p0=Carrera, @p1=Grado, @p2=IdEspecialidad
                var sqlParams = new object[] { carreraId, gradoId, idEspecialidadOficial };

                // C. Ejecutar Consulta
                var materiasDelGrado = db.Database.SqlQuery<MateriaExportConUnidades>(
                    sqlQueryFinal,
                    sqlParams
                ).ToList();

                // Validación si no encuentra materias
                if (!materiasDelGrado.Any())
                {
                    string infoEspecialidad = idEspecialidadOficial.HasValue ? $"ID: {idEspecialidadOficial}" : "General";
                    materiasDelGrado = new List<MateriaExportConUnidades>
            {
                new MateriaExportConUnidades { IdMateria = 0, Nombre = $"Sin materias configuradas ({infoEspecialidad})", NumeroUnidades = 1 }
            };
                }


                // ✅ GENERAR EXCEL CON INFORMACIÓN COMPLETA
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add($"Calificaciones {nombreGrupo}");

                    // ===== TÍTULO PRINCIPAL =====
                    worksheet.Cells[1, 1].Value = $"REPORTE COMPLETO DE CALIFICACIONES - {nombreGrupo.ToUpper()}";
                    int totalColumnas = 3; // Matrícula, Nombre, Promedio Final

                    // Calcular total de columnas dinámicamente
                    foreach (var materia in materiasDelGrado)
                    {
                        totalColumnas += materia.NumeroUnidades + 2; // Unidades + Calif Final + Estado
                    }

                    worksheet.Cells[1, 1, 1, totalColumnas].Merge = true;
                    var tituloRange = worksheet.Cells[1, 1];
                    tituloRange.Style.Font.Size = 18;
                    tituloRange.Style.Font.Bold = true;
                    tituloRange.Style.Font.Color.SetColor(Color.White);
                    tituloRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                    tituloRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // ===== INFORMACIÓN DEL GRUPO =====
                    worksheet.Cells[2, 1].Value = $"{nombreCarrera} - {nombreGrado} - {nombrePeriodo} {añoActual}";
                    worksheet.Cells[2, 1, 2, totalColumnas].Merge = true;
                    var infoRange = worksheet.Cells[2, 1];
                    infoRange.Style.Font.Size = 14;
                    infoRange.Style.Font.Bold = true;
                    infoRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    infoRange.Style.Font.Color.SetColor(Color.FromArgb(31, 73, 125));

                    worksheet.Cells[3, 1].Value = $"Generado el {DateTime.Now:dd/MM/yyyy HH:mm:ss} | Total Alumnos: {alumnosGrupo.Count}";
                    worksheet.Cells[3, 1, 3, totalColumnas].Merge = true;
                    var fechaRange = worksheet.Cells[3, 1];
                    fechaRange.Style.Font.Size = 11;
                    fechaRange.Style.Font.Italic = true;
                    fechaRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // ===== ENCABEZADOS DINÁMICOS =====
                    int col = 1;
                    int filaEncabezado1 = 5;
                    int filaEncabezado2 = 6;
                    int filaEncabezado3 = 7;

                    // Columnas fijas
                    worksheet.Cells[filaEncabezado1, col, filaEncabezado3, col].Merge = true;
                    worksheet.Cells[filaEncabezado1, col].Value = "Matrícula";
                    col++;

                    worksheet.Cells[filaEncabezado1, col, filaEncabezado3, col].Merge = true;
                    worksheet.Cells[filaEncabezado1, col].Value = "Nombre del Alumno";
                    col++;

                    // ✅ ENCABEZADOS POR MATERIA CON UNIDADES
                    foreach (var materia in materiasDelGrado)
                    {
                        int colInicioMateria = col;
                        int totalColumnasMateria = materia.NumeroUnidades + 2; // Unidades + Final + Estado

                        // Nivel 1: Nombre de la materia (merge sobre todas sus columnas)
                        worksheet.Cells[filaEncabezado1, colInicioMateria, filaEncabezado1, colInicioMateria + totalColumnasMateria - 1].Merge = true;
                        worksheet.Cells[filaEncabezado1, colInicioMateria].Value = materia.Nombre;

                        // Nivel 2: "Unidades" (merge sobre las columnas de unidades)
                        worksheet.Cells[filaEncabezado2, colInicioMateria, filaEncabezado2, colInicioMateria + materia.NumeroUnidades - 1].Merge = true;
                        worksheet.Cells[filaEncabezado2, colInicioMateria].Value = "Unidades";

                        // Nivel 2: "Calificación Final"
                        worksheet.Cells[filaEncabezado2, colInicioMateria + materia.NumeroUnidades, filaEncabezado3, colInicioMateria + materia.NumeroUnidades].Merge = true;
                        worksheet.Cells[filaEncabezado2, colInicioMateria + materia.NumeroUnidades].Value = "Calif. Final";

                        // Nivel 2: "Estado"
                        worksheet.Cells[filaEncabezado2, colInicioMateria + materia.NumeroUnidades + 1, filaEncabezado3, colInicioMateria + materia.NumeroUnidades + 1].Merge = true;
                        worksheet.Cells[filaEncabezado2, colInicioMateria + materia.NumeroUnidades + 1].Value = "Estado";

                        // Nivel 3: U1, U2, U3, etc.
                        for (int u = 1; u <= materia.NumeroUnidades; u++)
                        {
                            worksheet.Cells[filaEncabezado3, colInicioMateria + u - 1].Value = $"U{u}";
                        }

                        col += totalColumnasMateria;
                    }

                    // Columna de PROMEDIO FINAL DEL CUATRIMESTRE (al final)
                    worksheet.Cells[filaEncabezado1, col, filaEncabezado3, col].Merge = true;
                    worksheet.Cells[filaEncabezado1, col].Value = "PROMEDIO FINAL CUATRIMESTRE";

                    // ✅ ESTILO PARA ENCABEZADOS
                    var headerRange = worksheet.Cells[filaEncabezado1, 1, filaEncabezado3, col];
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Size = 10;
                    headerRange.Style.Font.Color.SetColor(Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.WrapText = true;

                    // ✅ LLENAR DATOS DE ALUMNOS
                    int fila = filaEncabezado3 + 1;

                    foreach (var alumno in alumnosGrupo)
                    {
                        col = 1;

                        // Matrícula y Nombre
                        worksheet.Cells[fila, col++].Value = alumno.Matricula;
                        worksheet.Cells[fila, col++].Value = alumno.Nombre;

                        var calificacionesCuatrimestre = new List<decimal>();

                        // ✅ DATOS POR MATERIA
                        foreach (var materia in materiasDelGrado)
                        {
                            // Obtener el Id de MateriasAlumno
                            var idMateriaAlumno = db.Database.SqlQuery<int?>(
                                @"SELECT Id FROM MateriasAlumno 
                          WHERE IdMateria = @p0 AND IdPersona = @p1",
                                materia.IdMateria, alumno.IdPersona
                            ).FirstOrDefault();

                            if (idMateriaAlumno.HasValue)
                            {
                                // Obtener calificaciones de unidades
                                var unidades = db.Database.SqlQuery<CalificacionUnidadDto>(
                                    @"SELECT NumeroUnidad, Calificacion
                              FROM CalificacionesUnidades
                              WHERE IdMateriaAlumno = @p0
                              ORDER BY NumeroUnidad",
                                    idMateriaAlumno.Value
                                ).ToList();

                                // Llenar unidades
                                for (int u = 1; u <= materia.NumeroUnidades; u++)
                                {
                                    var unidad = unidades.FirstOrDefault(un => un.NumeroUnidad == u);
                                    var celda = worksheet.Cells[fila, col++];

                                    if (unidad != null && unidad.Calificacion.HasValue)
                                    {
                                        celda.Value = unidad.Calificacion.Value;
                                        celda.Style.Numberformat.Format = "0.0";

                                        // Color según calificación
                                        if (unidad.Calificacion.Value >= 7)
                                        {
                                            celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                            celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(198, 239, 206)); // Verde claro
                                        }
                                        else
                                        {
                                            celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                            celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 199, 206)); // Rojo claro
                                        }
                                    }
                                    else
                                    {
                                        celda.Value = "-";
                                        celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 248, 248)); // Gris claro
                                    }

                                    celda.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                }

                                // Obtener calificación final y estado
                                var materiaAlumno = db.Database.SqlQuery<MateriaAlumnoDto>(
                                    @"SELECT Calificacion, Estado, IntentosExtraordinarios
                              FROM MateriasAlumno
                              WHERE IdMateria = @p0 AND IdPersona = @p1",
                                    materia.IdMateria, alumno.IdPersona
                                ).FirstOrDefault();

                                // Calificación Final de la materia
                                var celdaFinal = worksheet.Cells[fila, col++];
                                if (materiaAlumno != null && materiaAlumno.Calificacion.HasValue)
                                {
                                    celdaFinal.Value = materiaAlumno.Calificacion.Value;
                                    celdaFinal.Style.Numberformat.Format = "0.0";
                                    celdaFinal.Style.Font.Bold = true;

                                    // Agregar a promedio del cuatrimestre
                                    calificacionesCuatrimestre.Add(materiaAlumno.Calificacion.Value);

                                    // Color según estado
                                    if (materiaAlumno.Estado == "Acreditada")
                                    {
                                        celdaFinal.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celdaFinal.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(146, 208, 80)); // Verde
                                    }
                                    else if (materiaAlumno.Estado == "Extraordinario")
                                    {
                                        celdaFinal.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celdaFinal.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 217, 102)); // Amarillo
                                    }
                                    else if (materiaAlumno.Estado == "Reprobada")
                                    {
                                        celdaFinal.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celdaFinal.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 102, 102)); // Rojo
                                        celdaFinal.Style.Font.Color.SetColor(Color.White);
                                    }
                                }
                                else
                                {
                                    celdaFinal.Value = "-";
                                    celdaFinal.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    celdaFinal.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 217, 217)); // Gris
                                }
                                celdaFinal.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                                // Estado de la materia
                                var celdaEstado = worksheet.Cells[fila, col++];
                                if (materiaAlumno != null)
                                {
                                    string estadoTexto = materiaAlumno.Estado ?? "Pendiente";
                                    if (materiaAlumno.IntentosExtraordinarios > 0)
                                    {
                                        estadoTexto += $" ({materiaAlumno.IntentosExtraordinarios})";
                                    }
                                    celdaEstado.Value = estadoTexto;

                                    // Color según estado
                                    if (materiaAlumno.Estado == "Acreditada")
                                    {
                                        celdaEstado.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celdaEstado.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(146, 208, 80));
                                    }
                                    else if (materiaAlumno.Estado == "Extraordinario")
                                    {
                                        celdaEstado.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celdaEstado.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 217, 102));
                                    }
                                    else if (materiaAlumno.Estado == "Reprobada")
                                    {
                                        celdaEstado.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                        celdaEstado.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 102, 102));
                                        celdaEstado.Style.Font.Color.SetColor(Color.White);
                                    }
                                }
                                else
                                {
                                    celdaEstado.Value = "Pendiente";
                                    celdaEstado.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    celdaEstado.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 217, 217));
                                }
                                celdaEstado.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                celdaEstado.Style.Font.Size = 9;
                            }
                            else
                            {
                                // Sin datos para esta materia
                                for (int u = 0; u < materia.NumeroUnidades + 2; u++)
                                {
                                    var celda = worksheet.Cells[fila, col++];
                                    celda.Value = "-";
                                    celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 248, 248));
                                    celda.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                }
                            }
                        }

                        // ✅ PROMEDIO FINAL DEL CUATRIMESTRE
                        var celdaPromedioCuatrimestre = worksheet.Cells[fila, col];
                        if (calificacionesCuatrimestre.Any())
                        {
                            decimal promedioFinal = Math.Round(calificacionesCuatrimestre.Average(), 2);
                            celdaPromedioCuatrimestre.Value = promedioFinal;
                            celdaPromedioCuatrimestre.Style.Numberformat.Format = "0.00";
                            celdaPromedioCuatrimestre.Style.Font.Bold = true;
                            celdaPromedioCuatrimestre.Style.Font.Size = 12;

                            // Color según promedio
                            if (promedioFinal >= 9.0m)
                            {
                                celdaPromedioCuatrimestre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                celdaPromedioCuatrimestre.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 176, 80)); // Verde oscuro
                                celdaPromedioCuatrimestre.Style.Font.Color.SetColor(Color.White);
                            }
                            else if (promedioFinal >= 8.0m)
                            {
                                celdaPromedioCuatrimestre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                celdaPromedioCuatrimestre.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(146, 208, 80)); // Verde
                            }
                            else if (promedioFinal >= 7.0m)
                            {
                                celdaPromedioCuatrimestre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                celdaPromedioCuatrimestre.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 217, 102)); // Amarillo
                            }
                            else
                            {
                                celdaPromedioCuatrimestre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                celdaPromedioCuatrimestre.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 102, 102)); // Rojo
                                celdaPromedioCuatrimestre.Style.Font.Color.SetColor(Color.White);
                            }
                        }
                        else
                        {
                            celdaPromedioCuatrimestre.Value = "-";
                            celdaPromedioCuatrimestre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            celdaPromedioCuatrimestre.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 217, 217));
                        }
                        celdaPromedioCuatrimestre.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        fila++;
                    }

                    // ✅ AJUSTAR ANCHOS DE COLUMNAS
                    worksheet.Column(1).Width = 12;  // Matrícula
                    worksheet.Column(2).Width = 35;  // Nombre

                    col = 3;
                    foreach (var materia in materiasDelGrado)
                    {
                        for (int u = 0; u < materia.NumeroUnidades; u++)
                        {
                            worksheet.Column(col++).Width = 6; // Unidades
                        }
                        worksheet.Column(col++).Width = 10; // Calif Final
                        worksheet.Column(col++).Width = 15; // Estado
                    }
                    worksheet.Column(col).Width = 12; // Promedio Final Cuatrimestre

                    // ✅ LEYENDA
                    fila += 2;
                    worksheet.Cells[fila, 1].Value = "LEYENDA DE COLORES";
                    worksheet.Cells[fila, 1, fila, 4].Merge = true;
                    var leyendaTitulo = worksheet.Cells[fila, 1];
                    leyendaTitulo.Style.Font.Bold = true;
                    leyendaTitulo.Style.Font.Size = 11;
                    fila++;

                    worksheet.Cells[fila, 1].Value = "Verde:";
                    worksheet.Cells[fila, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[fila, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(146, 208, 80));
                    worksheet.Cells[fila, 2].Value = "Acreditada (≥ 7.0)";
                    fila++;

                    worksheet.Cells[fila, 1].Value = "Amarillo:";
                    worksheet.Cells[fila, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[fila, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 217, 102));
                    worksheet.Cells[fila, 2].Value = "Extraordinario";
                    fila++;

                    worksheet.Cells[fila, 1].Value = "Rojo:";
                    worksheet.Cells[fila, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[fila, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 102, 102));
                    worksheet.Cells[fila, 2].Value = "Reprobada";
                    fila++;

                    worksheet.Cells[fila, 1].Value = "Gris:";
                    worksheet.Cells[fila, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[fila, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 217, 217));
                    worksheet.Cells[fila, 2].Value = "Sin calificación";

                    // ✅ CONFIGURACIÓN
                    worksheet.View.FreezePanes(filaEncabezado3 + 1, 3);
                    worksheet.PrinterSettings.Orientation = eOrientation.Landscape;
                    worksheet.PrinterSettings.FitToPage = true;

                    // GENERAR ARCHIVO
                    var excelBytes = package.GetAsByteArray();
                    string nombreArchivo = $"Calificaciones_{nombreGrupo.Replace(" ", "_")}_Completo_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                    System.Diagnostics.Debug.WriteLine($"✅ Excel generado: {nombreArchivo}");

                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR: {ex.Message}");
                return Content($"Error al generar el reporte: {ex.Message}");
            }
        }


        // ✅ CLASE DTO AUXILIAR PARA CALIFICACIONES DE UNIDADES
        public class CalificacionUnidadDto
        {
            public int NumeroUnidad { get; set; }
            public decimal? Calificacion { get; set; }
        }

        // ✅ CLASE DTO AUXILIAR PARA MATERIA ALUMNO
        public class MateriaAlumnoDto
        {
            public decimal? Calificacion { get; set; }
            public string Estado { get; set; }
            public int IntentosExtraordinarios { get; set; }
        }


        // ============================================================
        // MÉTODO CORREGIDO (INTENTO 5 - LÓGICA DE ALUMNO DE MUESTRA)
        // ============================================================
        // PROPÓSITO: Descargar PLANTILLA VACÍA para llenar y luego importar
        // BOTÓN: "Descargar Plantilla" en el modal de importación
        public ActionResult DescargarPlantillaMateriasGrupo(int? idGrupo, int? idCarrera, int? idGrado, int? idTurno, int? idPeriodo, int? año)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📥 DESCARGANDO PLANTILLA CON FILTROS EXACTOS");

                int grupoId = idGrupo ?? 1;
                int carreraId = idCarrera ?? 1;
                int gradoId = idGrado ?? 1;

                int turnoId = idTurno ?? 1;
                int periodoId = idPeriodo ?? 1;
                int anioActual = año ?? DateTime.Now.Year;

                // Obtener nombre del grupo para el archivo
                var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == grupoId);
                string nombreGrupo = grupo != null ? grupo.Nombre : "Grupo " + grupoId;

                // =================================================================
                // PASO 1: OBTENER ALUMNOS
                // =================================================================
                var alumnosGrupo = db.DatosPersonales
                    .Where(dp => dp.IdGrupo == grupoId
                              && dp.IdCarrera == carreraId
                              && dp.IdGrado == gradoId
                              && dp.IdTurno == turnoId
                              && dp.IdPeriodo == periodoId
                              && dp.Año == anioActual
                              && dp.Estado == true)
                    .OrderBy(dp => dp.Matricula)
                    .ToList();

                if (!alumnosGrupo.Any())
                {
                    alumnosGrupo = new List<DatosPersonales>
            {
                new DatosPersonales { IdPersona = 1, Matricula = "Sin alumnos", Nombre = "Sin alumnos en este grupo" }
            };
                }

                // =================================================================
                // PASO 2: DETECTAR MATERIAS (LÓGICA ROBUSTA - ID ESPECIALIDAD)
                // =================================================================

                // 2.A. Buscar la configuración OFICIAL del grupo en TutoriaGrupals
                // Esto es lo que hace que funcione "solito" sin depender del primer alumno.
                var configuracionGrupo = db.TutoriaGrupals
                    .FirstOrDefault(t => t.IdGrupo == grupoId
                                      && t.IdCarrera == carreraId
                                      && t.IdGrado == gradoId
                                      && t.IdTurno == turnoId
                                      && t.IdPeriodo == periodoId
                                      && t.Año == anioActual);

                int? idEspecialidadOficial = configuracionGrupo?.IdEspecialidad;

                System.Diagnostics.Debug.WriteLine($"[Plantilla] Estrategia de Materias: ID Especialidad Grupo = {idEspecialidadOficial ?? 0}");

                // 2.B. Construir consulta SQL
                // Trae materias que coincidan con la carrera, grado y:
                // 1. El ID de especialidad del grupo.
                // 2. O que sean materias de Tronco Común (Inglés, Valores, etc).
                // 3. O si no hay especialidad, las que no tengan especialidad asignada.
                var sqlQueryFinal = @"
            SELECT DISTINCT m.IdMateria, m.Nombre, m.NumeroUnidades
            FROM Materias m
            INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
            WHERE m.IdCarrera = @p0 
              AND m.IdGrado = @p1 
              AND m.Activo = 1
              AND (
                  -- 1. Coincidencia exacta por ID de Especialidad (Lo más seguro)
                  (@p2 IS NOT NULL AND m.IdEspecialidad = @p2)
                  OR
                  -- 2. Materias de Tronco Común (Por nombre, para asegurar que salgan)
                  (e.Nombre LIKE '%Común%' OR e.Nombre LIKE '%Comun%' OR e.Nombre LIKE '%Tronco%')
                  OR
                  -- 3. Fallback: Si el grupo no tiene especialidad (0 o null), traer las materias generales de la carrera
                  ((@p2 IS NULL OR @p2 = 0) AND e.IdCarrera = @p0 AND (e.Nombre = 'Sin Especialidad' OR e.Nombre = 'General'))
              )
            ORDER BY m.Nombre";

                // Parámetros: @p0=Carrera, @p1=Grado, @p2=IdEspecialidad
                var sqlParams = new object[] { carreraId, gradoId, idEspecialidadOficial };

                // =================================================================
                // PASO 3: EJECUTAR Y GENERAR
                // =================================================================
                var materiasConUnidades = db.Database.SqlQuery<MateriaExportConUnidades>(
                    sqlQueryFinal,
                    sqlParams
                ).ToList();

                // AQUÍ ESTABA EL ERROR: Se eliminó la referencia a 'nombresUnicos'
                if (!materiasConUnidades.Any())
                {
                    string infoEspecialidad = idEspecialidadOficial.HasValue ? $"ID: {idEspecialidadOficial}" : "General/Tronco Común";

                    System.Diagnostics.Debug.WriteLine($"⚠️ No se encontraron materias para Carrera {carreraId}, Grado {gradoId}, Especialidad {infoEspecialidad}");

                    materiasConUnidades = new List<MateriaExportConUnidades>
            {
                new MateriaExportConUnidades {
                    IdMateria = 1,
                    Nombre = $"Sin materias encontradas (Esp: {infoEspecialidad})",
                    NumeroUnidades = 1
                }
            };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Materias encontradas: {materiasConUnidades.Count}");
                }

                return GenerarPaqueteExcel(materiasConUnidades, alumnosGrupo, nombreGrupo, grupoId, carreraId, gradoId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR en plantilla: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ STACK TRACE: {ex.StackTrace}");
                return Content($"Error al generar plantilla: {ex.Message}");
            }
        }
        // ============================================================
        // MÉTODO AUXILIAR PARA GENERAR EL PAQUETE EXCEL (ACTUALIZADO)
        // ============================================================
        private FileResult GenerarPaqueteExcel(List<MateriaExportConUnidades> materiasConUnidades, List<DatosPersonales> alumnosGrupo, string nombreGrupo, int grupoId, int carreraId, int gradoId)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Plantilla {nombreGrupo}");

                int col = 1;

                // ===== FILA 1: NOMBRES DE MATERIAS (CON CELDAS COMBINADAS) =====
                // ✅ REQUISITO 1: APLICADO
                worksheet.Cells[1, 1].Value = "Matrícula";
                worksheet.Cells[1, 2].Value = "Nombre";
                // Combinar celdas de Matrícula y Nombre para que abarquen 2 filas
                worksheet.Cells[1, 1, 2, 1].Merge = true;
                worksheet.Cells[1, 2, 2, 2].Merge = true;
                col = 3;

                foreach (var materia in materiasConUnidades)
                {
                    int colInicioMateria = col;
                    int colFinMateria = col + materia.NumeroUnidades - 1;

                    // Combinar celdas si la materia tiene más de 1 unidad
                    if (materia.NumeroUnidades > 1)
                    {
                        worksheet.Cells[1, colInicioMateria, 1, colFinMateria].Merge = true;
                    }

                    worksheet.Cells[1, colInicioMateria].Value = materia.Nombre;

                    col += materia.NumeroUnidades; // Mover el contador al final de esta materia
                }

                // ===== FILA 2: INDICADOR "UNIDADES" =====
                col = 3;
                foreach (var materia in materiasConUnidades)
                {
                    for (int u = 1; u <= materia.NumeroUnidades; u++)
                    {
                        worksheet.Cells[2, col].Value = $"U{u}";
                        col++;
                    }
                }

                // ===== ESTILO PARA ENCABEZADOS (FILAS 1-2) =====
                int totalColumnas = 2 + materiasConUnidades.Sum(m => m.NumeroUnidades);

                var headerRange = worksheet.Cells[1, 1, 2, totalColumnas];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Size = 11;
                headerRange.Style.Font.Color.SetColor(Color.White);
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                // ===== LLENAR DATOS DE ALUMNOS (DESDE FILA 3) =====
                // (Los alumnos ya vienen ordenados por Matrícula desde el método principal)
                int fila = 3;
                foreach (var alumno in alumnosGrupo)
                {
                    col = 1;

                    // Matrícula y Nombre (NO EDITABLES - fondo gris)
                    worksheet.Cells[fila, col].Value = alumno.Matricula;
                    worksheet.Cells[fila, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[fila, col].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 217, 217));
                    worksheet.Cells[fila, col].Style.Locked = true;
                    col++;

                    worksheet.Cells[fila, col].Value = alumno.Nombre;
                    worksheet.Cells[fila, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[fila, col].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 217, 217));
                    worksheet.Cells[fila, col].Style.Locked = true;
                    col++;

                    // Celdas vacías para unidades (EDITABLES - fondo blanco)
                    foreach (var materia in materiasConUnidades)
                    {
                        for (int u = 1; u <= materia.NumeroUnidades; u++)
                        {
                            var celda = worksheet.Cells[fila, col];
                            celda.Value = ""; // Vacío para llenar manualmente
                            celda.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            celda.Style.Numberformat.Format = "0.0";
                            celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            celda.Style.Fill.BackgroundColor.SetColor(Color.White);
                            celda.Style.Locked = false; // EDITABLE

                            // ✅ VALIDACIÓN DE DATOS: Solo números entre 0.0 y 10.0
                            var validacion = worksheet.DataValidations.AddDecimalValidation(
                                worksheet.Cells[fila, col].Address
                            );
                            validacion.ShowErrorMessage = true;
                            validacion.ErrorTitle = "Valor inválido";
                            validacion.Error = "Solo números entre 0.0 y 10.0";
                            validacion.Operator = ExcelDataValidationOperator.between;
                            validacion.Formula.Value = 0.0;
                            validacion.Formula2.Value = 10.0;
                            validacion.AllowBlank = true;

                            col++;
                        }
                    }

                    fila++;
                }

                // ===== AJUSTAR ANCHOS =====
                worksheet.Column(1).Width = 15; // Matrícula
                worksheet.Column(2).Width = 35; // Nombre
                for (int i = 3; i <= totalColumnas; i++)
                {
                    worksheet.Column(i).Width = 8; // Unidades
                }

                // ===== CONGELAR PANELES (Filas 1-2 y Columnas 1-2) =====
                worksheet.View.FreezePanes(3, 3);

                // ===== INSTRUCCIONES AL FINAL (FUERA DEL ÁREA DE DATOS) =====
                fila += 3;
                worksheet.Cells[fila, 1].Value = "📋 INSTRUCCIONES PARA IMPORTAR:";
                worksheet.Cells[fila, 1, fila, 5].Merge = true;
                worksheet.Cells[fila, 1].Style.Font.Bold = true;
                worksheet.Cells[fila, 1].Style.Font.Size = 12;
                worksheet.Cells[fila, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[fila, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 242, 204));
                fila++;

                worksheet.Cells[fila, 1].Value = "1. Complete SOLO las celdas de unidades con valores entre 0.0 y 10.0";
                fila++;
                worksheet.Cells[fila, 1].Value = "2. NO modifique las columnas de Matrícula y Nombre";
                fila++;
                worksheet.Cells[fila, 1].Value = "3. NO modifique los encabezados (filas 1-2)";
                fila++;
                worksheet.Cells[fila, 1].Value = "4. Puede dejar celdas vacías si aún no tiene la calificación";
                fila++;
                // ✅ CORRECCIÓN DE ERROR TIPOGRÁFICO
                worksheet.Cells[fila, 1].Value = "5. Una vez completado, importe este archivo usando el botón 'Importar Excel'";
                fila++;
                worksheet.Cells[fila, 1].Value = "6. El sistema calculará automáticamente la calificación final de cada materia";
                fila++;
                worksheet.Cells[fila, 1].Value = $"7. Grupo: {nombreGrupo} | Carrera: {carreraId} | Grado: {gradoId}";

                // INFORMACIÓN DEL SISTEMA
                fila += 2;
                worksheet.Cells[fila, 1].Value = $"Plantilla generada: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Cells[fila, 1].Style.Font.Italic = true;
                worksheet.Cells[fila, 1].Style.Font.Size = 9;
                worksheet.Cells[fila, 1].Style.Font.Color.SetColor(Color.Gray);

                // GENERAR ARCHIVO
                var excelBytes = package.GetAsByteArray();
                // ✅ CORRECCIÓN DE VARIABLE (grupoId ahora existe)
                string nombreArchivo = $"Plantilla_{nombreGrupo.Replace(" ", "_")}_G{grupoId}C{carreraId}Gd{gradoId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                System.Diagnostics.Debug.WriteLine($"✅ Plantilla generada (FORMATO CORRECTO): {nombreArchivo}");
                System.Diagnostics.Debug.WriteLine($"   - Alumnos: {alumnosGrupo.Count}");
                System.Diagnostics.Debug.WriteLine($"   - Materias: {materiasConUnidades.Count}");
                System.Diagnostics.Debug.WriteLine($"   - Total columnas: {totalColumnas}");

                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
            }
        }


        // MÉTODO PARA OBTENER PERIODO ACTUAL
        private int ObtenerPeriodoActual()
        {
            try
            {
                var tiempo = DateTime.Now;
                var pa = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1; // Enero-Abril = Periodo 1
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2; // Mayo-Agosto = Periodo 2  
                }
                else
                {
                    pa = 3; // Sept-Dic = Periodo 3
                }
                return pa;
            }
            catch
            {
                return 1;
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

    // CLASES AUXILIARES


    public class MateriaExport
    {
        public int IdMateria { get; set; }
        public string Nombre { get; set; }

        public int NumeroUnidades { get; set; }
    }

    public class MateriaExportConUnidades
    {
        public int IdMateria { get; set; }
        public string Nombre { get; set; }
        public int NumeroUnidades { get; set; }
    }

    public class CalificacionExport
    {
        public decimal Calificacion { get; set; }
        public string Estado { get; set; }
    }
}