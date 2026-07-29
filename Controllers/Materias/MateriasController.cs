using OfficeOpenXml;
using OfficeOpenXml.Style;
using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Materia = PlataformaWeb.Models.Materia;

namespace Plataforma_Web.Controllers
{
    // DTO para mapear resultados de SQL query
    public class MateriaConRelacionesDto
    {
        public int IdMateria { get; set; }
        public string Nombre { get; set; }
        public int IdCarrera { get; set; }
        public int IdEspecialidad { get; set; }
        public int IdGrado { get; set; }
        public int IdPlanEstudio { get; set; }
        public int NumeroUnidades { get; set; }
        public bool Activo { get; set; }

        // Propiedades de navegación que SÍ se mapearán automáticamente
        public string NombreCarrera { get; set; }
        public string NombreEspecialidad { get; set; }
        public string NombreGrado { get; set; }
        public string NombrePlanEstudio { get; set; }
        public int? AñoPlan { get; set; }
        public decimal? CalificacionMinima { get; set; }
        public bool? PermiteDecimales { get; set; }
    }

    [CustomAuthorize(Nivel = 3)]  // ← AGREGAR ESTA LÍNEA
    public class MateriasController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Materias/Index - ACTUALIZADO CON SOLUCIÓN
        public ActionResult Index()
        {
            // ✅ VALIDAR SESIÓN
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // ✅ VALIDAR ACCESO (Solo Coordinadores y Master)
            if (!ValidarAccesoCoordinador(usuario))
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección. Solo coordinadores y administradores.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // 🔑 OBTENER CARRERA PERMITIDA PARA FILTRAR
                var carreraPermitida = ObtenerCarreraPermitida(usuario);

                // Usar el mismo método FiltrarMaterias pero con la carrera del coordinador
                var result = FiltrarMaterias(carreraPermitida, null, null, null);

                if (result is PartialViewResult partialResult)
                {
                    var materias = partialResult.Model as IEnumerable<Materia>;

                    // 🔑 CARGAR SOLO CARRERAS ACCESIBLES
                    ViewBag.Carreras = new SelectList(ObtenerCarrerasAccesibles(usuario), "IdCarrera", "Nombre");
                    ViewBag.Grados = new SelectList(db.Gradoes.ToList(), "IdGrado", "Nombre");
                    ViewBag.PlanesEstudio = new SelectList(ObtenerPlanesEstudioActivos(), "IdPlanEstudio", "Nombre");

                    return View(materias);
                }

                return View(new List<Materia>());
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error: " + ex.Message;
                return View(new List<Materia>());
            }
        }

        // POST: Filtrar materias - SOLUCIÓN COMPLETA
        [LecturaPermitida]
        [HttpPost]
        public ActionResult FiltrarMaterias(int? carreraId = null, int? especialidadId = null, int? gradoId = null, int? planEstudioId = null)
        {
            try
            {
                // 🔑 VALIDAR ACCESO DEL USUARIO
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                // Consulta actualizada para incluir número de unidades
                var query = @"
        SELECT m.IdMateria, m.Nombre, m.IdCarrera, m.IdEspecialidad, m.IdGrado, m.IdPlanEstudio, m.NumeroUnidades, m.Activo,
               c.Nombre as NombreCarrera, e.Nombre as NombreEspecialidad, g.Nombre as NombreGrado, 
               p.Nombre as NombrePlanEstudio, p.Año as AñoPlan, p.CalificacionMinima, p.PermiteDecimales
        FROM Materias m
        INNER JOIN Carreras c ON m.IdCarrera = c.IdCarrera
        INNER JOIN Especialidads e ON m.IdEspecialidad = e.Id
        INNER JOIN Gradoes g ON m.IdGrado = g.IdGrado
        INNER JOIN PlanesEstudio p ON m.IdPlanEstudio = p.IdPlanEstudio
        WHERE 1=1";

                var parametros = new List<object>();
                int paramIndex = 0;

                // 🔑 FILTRAR POR CARRERA DEL COORDINADOR (SI NO ES MASTER)
                if (usuario.IdNivel == 3) // Coordinador
                {
                    if (usuario.IdCarrera == null || usuario.IdCarrera == 0)
                    {
                        return PartialView("_TablaMaterias", new List<Materia>());
                    }
                    query += $" AND m.IdCarrera = @p{paramIndex}";
                    parametros.Add(usuario.IdCarrera);
                    paramIndex++;
                }
                else if (carreraId.HasValue && carreraId.Value > 0) // Master con filtro
                {
                    query += $" AND m.IdCarrera = @p{paramIndex}";
                    parametros.Add(carreraId.Value);
                    paramIndex++;
                }

                // Aplicar filtros según los parámetros recibidos
                if (especialidadId.HasValue && especialidadId.Value > 0)
                {
                    query += $" AND m.IdEspecialidad = @p{paramIndex}";
                    parametros.Add(especialidadId.Value);
                    paramIndex++;
                }

                if (gradoId.HasValue && gradoId.Value > 0)
                {
                    query += $" AND m.IdGrado = @p{paramIndex}";
                    parametros.Add(gradoId.Value);
                    paramIndex++;
                }

                if (planEstudioId.HasValue && planEstudioId.Value > 0)
                {
                    query += $" AND m.IdPlanEstudio = @p{paramIndex}";
                    parametros.Add(planEstudioId.Value);
                    paramIndex++;
                }

                query += " ORDER BY m.Nombre";

                // Usar el DTO para mapear automáticamente los resultados
                var materiasDto = db.Database.SqlQuery<MateriaConRelacionesDto>(query, parametros.ToArray()).ToList();

                // Convertir DTO a objetos Materia con todas las propiedades mapeadas
                var materiasFiltradas = materiasDto.Select(dto => new Materia
                {
                    IdMateria = dto.IdMateria,
                    Nombre = dto.Nombre,
                    IdCarrera = dto.IdCarrera,
                    IdEspecialidad = dto.IdEspecialidad,
                    IdGrado = dto.IdGrado,
                    IdPlanEstudio = dto.IdPlanEstudio,
                    NumeroUnidades = dto.NumeroUnidades,
                    Activo = dto.Activo,

                    // Mapear las propiedades de navegación
                    NombreCarrera = dto.NombreCarrera,
                    NombreEspecialidad = dto.NombreEspecialidad,
                    NombreGrado = dto.NombreGrado,
                    NombrePlanEstudio = dto.NombrePlanEstudio,
                    AñoPlan = dto.AñoPlan,
                    CalificacionMinima = dto.CalificacionMinima,
                    PermiteDecimales = dto.PermiteDecimales
                }).ToList();

                // Retornar la vista parcial con los datos correctamente mapeados
                return PartialView("_TablaMaterias", materiasFiltradas);
            }
            catch (Exception ex)
            {
                // Log del error para debugging
                System.Diagnostics.Debug.WriteLine("Error en FiltrarMaterias: " + ex.Message);

                // Fallback básico con mapeo manual
                return FallbackConMapeoManual(carreraId, especialidadId, gradoId, planEstudioId);
            }
        }

        // Método fallback con mapeo manual
        private ActionResult FallbackConMapeoManual(int? carreraId, int? especialidadId, int? gradoId, int? planEstudioId)
        {
            var materias = db.Materias.ToList();

            // Aplicar filtros básicos
            if (carreraId.HasValue && carreraId.Value > 0)
                materias = materias.Where(m => m.IdCarrera == carreraId.Value).ToList();

            if (especialidadId.HasValue && especialidadId.Value > 0)
                materias = materias.Where(m => m.IdEspecialidad == especialidadId.Value).ToList();

            if (gradoId.HasValue && gradoId.Value > 0)
                materias = materias.Where(m => m.IdGrado == gradoId.Value).ToList();

            if (planEstudioId.HasValue && planEstudioId.Value > 0)
                materias = materias.Where(m => m.IdPlanEstudio == planEstudioId.Value).ToList();

            // Mapear manualmente las propiedades de navegación
            foreach (var materia in materias)
            {
                try
                {
                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == materia.IdCarrera);
                    var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == materia.IdEspecialidad);
                    var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == materia.IdGrado);
                    var plan = ObtenerPlanEstudioPorId(materia.IdPlanEstudio);

                    materia.NombreCarrera = carrera?.Nombre ?? "N/A";
                    materia.NombreEspecialidad = especialidad?.Nombre ?? "N/A";
                    materia.NombreGrado = grado?.Nombre ?? "N/A";
                    materia.NombrePlanEstudio = plan?.Nombre ?? "Plan No Encontrado";
                    materia.AñoPlan = plan?.Año;
                    materia.CalificacionMinima = plan?.CalificacionMinima;
                    materia.PermiteDecimales = plan?.PermiteDecimales;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error mapeando materia {materia.IdMateria}: {ex.Message}");
                    // Valores por defecto en caso de error
                    materia.NombreCarrera = "Error";
                    materia.NombreEspecialidad = "Error";
                    materia.NombreGrado = "Error";
                    materia.NombrePlanEstudio = "Error";
                }
            }

            return PartialView("_TablaMaterias", materias.OrderBy(m => m.Nombre).ToList());
        }

        // POST: Crear materia via AJAX (para el modal) - ACTUALIZADO
        [HttpPost]
        public JsonResult CreateAjax(string Nombre, int IdCarrera, int IdEspecialidad, int IdGrado, int IdPlanEstudio, int NumeroUnidades)
        {
            // ✅ AGREGAR ESTA VALIDACIÓN AL INICIO
            if (!ValidarAccesoCoordinadorOMaster())
            {
                return Json(new { success = false, message = "No tienes permisos para realizar esta acción." });
            }
            try
            {
                Nombre = Nombre?.Trim().ToUpper(); // <--- AÑADIR ESTO

                // Validar datos de entrada
                if (string.IsNullOrEmpty(Nombre) || IdCarrera <= 0 || IdEspecialidad <= 0 || IdGrado <= 0 || IdPlanEstudio <= 0)
                {
                    return Json(new { success = false, message = "Por favor complete todos los campos" });
                }

                // Validar número de unidades
                if (NumeroUnidades < 1 || NumeroUnidades > 6)
                {
                    return Json(new { success = false, message = "El número de unidades debe estar entre 1 y 6" });
                }

                // Verificar que existan las referencias
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == IdCarrera);
                if (carrera == null)
                {
                    return Json(new { success = false, message = "La carrera seleccionada no existe" });
                }

                var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == IdEspecialidad);
                if (especialidad == null)
                {
                    return Json(new { success = false, message = "La especialidad seleccionada no existe" });
                }

                var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == IdGrado);
                if (grado == null)
                {
                    return Json(new { success = false, message = "El grado seleccionado no existe" });
                }

                var planEstudio = ObtenerPlanEstudioPorId(IdPlanEstudio);
                if (planEstudio == null)
                {
                    return Json(new { success = false, message = "El plan de estudio seleccionado no existe" });
                }

                // Verificar que no exista la misma materia
                var materiaExistente = db.Materias.FirstOrDefault(m =>
                    m.Nombre.Trim().ToUpper() == Nombre.Trim().ToUpper() &&
                    m.IdCarrera == IdCarrera &&
                    m.IdEspecialidad == IdEspecialidad &&
                    m.IdGrado == IdGrado &&
                    m.IdPlanEstudio == IdPlanEstudio);

                if (materiaExistente != null)
                {
                    return Json(new { success = false, message = "Ya existe una materia con el mismo nombre, carrera, especialidad, grado y plan de estudio" });
                }

                // Intentar inserción con SQL directo (más confiable)
                try
                {
                    var sqlInsert = @"INSERT INTO Materias (Nombre, IdCarrera, IdEspecialidad, IdGrado, IdPlanEstudio, NumeroUnidades, Activo) 
                             VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)";

                    var result = db.Database.ExecuteSqlCommand(sqlInsert,
                        Nombre.Trim(), IdCarrera, IdEspecialidad, IdGrado, IdPlanEstudio, NumeroUnidades, true);

                    if (result > 0)
                    {
                        return Json(new
                        {
                            success = true,
                            message = $"Materia guardada correctamente en el {planEstudio.Nombre} con {NumeroUnidades} {(NumeroUnidades == 1 ? "unidad" : "unidades")}",
                            planInfo = new
                            {
                                nombre = planEstudio.Nombre,
                                año = planEstudio.Año,
                                calificacionMinima = planEstudio.CalificacionMinima,
                                permiteDecimales = planEstudio.PermiteDecimales
                            },
                            numeroUnidades = NumeroUnidades
                        });
                    }
                }
                catch
                {
                    // Si falla SQL directo, intentar con Entity Framework
                    var materia = new Materia
                    {
                        Nombre = Nombre.Trim(),
                        IdCarrera = IdCarrera,
                        IdEspecialidad = IdEspecialidad,
                        IdGrado = IdGrado,
                        IdPlanEstudio = IdPlanEstudio,
                        NumeroUnidades = NumeroUnidades,
                        Activo = true
                    };

                    db.Materias.Add(materia);
                    db.SaveChanges();

                    return Json(new
                    {
                        success = true,
                        message = $"Materia guardada correctamente en el {planEstudio.Nombre} con {NumeroUnidades} {(NumeroUnidades == 1 ? "unidad" : "unidades")}",
                        planInfo = new
                        {
                            nombre = planEstudio.Nombre,
                            año = planEstudio.Año,
                            calificacionMinima = planEstudio.CalificacionMinima,
                            permiteDecimales = planEstudio.PermiteDecimales
                        },
                        numeroUnidades = NumeroUnidades
                    });
                }

                return Json(new { success = false, message = "No se pudo guardar la materia" });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Error al guardar: " + innerMessage });
            }
        }

        // POST: Editar materia via AJAX (para el modal) - ACTUALIZADO
        [HttpPost]
        public JsonResult EditAjax(int IdMateria, string Nombre, int IdCarrera, int IdEspecialidad, int IdGrado, int IdPlanEstudio, int NumeroUnidades, bool Activo)
        {
            // ✅ AGREGAR ESTA VALIDACIÓN AL INICIO
            if (!ValidarAccesoCoordinadorOMaster())
            {
                return Json(new { success = false, message = "No tienes permisos para realizar esta acción." });
            }

            try
            {
                Nombre = Nombre?.Trim().ToUpper(); // <--- AÑADIR ESTO

                // Validar datos de entrada
                if (IdMateria <= 0 || string.IsNullOrEmpty(Nombre) || IdCarrera <= 0 || IdEspecialidad <= 0 || IdGrado <= 0 || IdPlanEstudio <= 0)
                {
                    return Json(new { success = false, message = "Por favor complete todos los campos" });
                }

                // Validar número de unidades
                if (NumeroUnidades < 1 || NumeroUnidades > 6)
                {
                    return Json(new { success = false, message = "El número de unidades debe estar entre 1 y 6" });
                }

                // Buscar la materia existente
                var materia = db.Materias.Find(IdMateria);
                if (materia == null)
                {
                    return Json(new { success = false, message = "La materia no existe" });
                }

                // Verificar que existan las referencias
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == IdCarrera);
                if (carrera == null)
                {
                    return Json(new { success = false, message = "La carrera seleccionada no existe" });
                }

                var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == IdEspecialidad);
                if (especialidad == null)
                {
                    return Json(new { success = false, message = "La especialidad seleccionada no existe" });
                }

                var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == IdGrado);
                if (grado == null)
                {
                    return Json(new { success = false, message = "El grado seleccionado no existe" });
                }

                var planEstudio = ObtenerPlanEstudioPorId(IdPlanEstudio);
                if (planEstudio == null)
                {
                    return Json(new { success = false, message = "El plan de estudio seleccionado no existe" });
                }

                // Verificar duplicados (excluyendo la materia actual)
                var materiaExistente = db.Materias.FirstOrDefault(m =>
                    m.IdMateria != IdMateria &&
                    m.Nombre.Trim().ToUpper() == Nombre.Trim().ToUpper() &&
                    m.IdCarrera == IdCarrera &&
                    m.IdEspecialidad == IdEspecialidad &&
                    m.IdGrado == IdGrado &&
                    m.IdPlanEstudio == IdPlanEstudio);

                if (materiaExistente != null)
                {
                    return Json(new { success = false, message = "Ya existe otra materia con el mismo nombre, carrera, especialidad, grado y plan de estudio" });
                }

                // Actualizar los datos
                materia.Nombre = Nombre.Trim();
                materia.IdCarrera = IdCarrera;
                materia.IdEspecialidad = IdEspecialidad;
                materia.IdGrado = IdGrado;
                materia.IdPlanEstudio = IdPlanEstudio;
                materia.NumeroUnidades = NumeroUnidades;  // ACTUALIZAR NÚMERO DE UNIDADES
                materia.Activo = Activo;

                // Guardar cambios
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Materia actualizada correctamente. Plan: {planEstudio.Nombre}, Unidades: {NumeroUnidades}",
                    planInfo = new
                    {
                        nombre = planEstudio.Nombre,
                        año = planEstudio.Año,
                        calificacionMinima = planEstudio.CalificacionMinima,
                        permiteDecimales = planEstudio.PermiteDecimales
                    },
                    numeroUnidades = NumeroUnidades
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Error al actualizar: " + innerMessage });
            }
        }

        // POST: Eliminar materia via AJAX (para el modal) - SIN CAMBIOS
        [HttpPost]
        public JsonResult DeleteAjax(int id)
        {
            // ✅ AGREGAR ESTA VALIDACIÓN AL INICIO
            if (!ValidarAccesoCoordinadorOMaster())
            {
                return Json(new { success = false, message = "No tienes permisos para realizar esta acción." });
            }

            try
            {
                // Buscar la materia
                var materia = db.Materias.Find(id);
                if (materia == null)
                {
                    return Json(new { success = false, message = "La materia no existe" });
                }

                // Eliminar la materia
                db.Materias.Remove(materia);
                db.SaveChanges();

                return Json(new { success = true, message = "Materia eliminada correctamente" });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Error al eliminar: " + innerMessage });
            }
        }

        // MÉTODOS PARA MANEJO DE PLANES DE ESTUDIO

        // Método para obtener planes de estudio activos
        private List<PlanEstudio> ObtenerPlanesEstudioActivos()
        {
            try
            {
                var query = @"
                    SELECT IdPlanEstudio, CAST(Año AS NVARCHAR(100)) as Nombre, Año, CalificacionMinima, PermiteDecimales, Descripcion, Activo, FechaCreacion
                    FROM PlanesEstudio 
                    WHERE Activo = 1 
                    ORDER BY Año DESC";

                return db.Database.SqlQuery<PlanEstudio>(query).ToList();
            }
            catch
            {
                // Fallback: crear planes básicos si no existen
                return new List<PlanEstudio>
                {
                    new PlanEstudio { IdPlanEstudio = 1, Nombre = "2020", Año = 2020, CalificacionMinima = 8.0m, PermiteDecimales = false },
                    new PlanEstudio { IdPlanEstudio = 2, Nombre = "2024", Año = 2024, CalificacionMinima = 7.0m, PermiteDecimales = true }
                };
            }
        }

        // Método para obtener un plan de estudio específico
        private PlanEstudio ObtenerPlanEstudioPorId(int idPlanEstudio)
        {
            try
            {
                var query = @"
                    SELECT IdPlanEstudio, CAST(Año AS NVARCHAR(100)) as Nombre, Año, CalificacionMinima, PermiteDecimales, Descripcion, Activo, FechaCreacion
                    FROM PlanesEstudio 
                    WHERE IdPlanEstudio = @p0 AND Activo = 1";

                return db.Database.SqlQuery<PlanEstudio>(query, idPlanEstudio).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        // AJAX method para obtener información del plan de estudio
        public JsonResult GetPlanEstudioInfo(int planEstudioId)
        {
            try
            {
                var plan = ObtenerPlanEstudioPorId(planEstudioId);
                if (plan == null)
                {
                    return Json(new { success = false, message = "Plan de estudio no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    plan = new
                    {
                        id = plan.IdPlanEstudio,
                        nombre = plan.Nombre,
                        año = plan.Año,
                        calificacionMinima = plan.CalificacionMinima,
                        permiteDecimales = plan.PermiteDecimales,
                        descripcion = plan.Descripcion,
                        tipoCalificacion = plan.TipoCalificacion,
                        requisitoAprobacion = plan.RequisitoAprobacion,
                        labelClass = plan.LabelClass
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Materias/ImportarExcel
        public ActionResult ImportarExcel()
        {
            // ✅ VALIDAR SESIÓN Y ACCESO
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            if (!ValidarAccesoCoordinadorOMaster())
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // POST: Materias/ImportarExcel
        [HttpPost]
        public JsonResult ImportarExcel(HttpPostedFileBase archivo)
        {
            try
            {
                if (archivo == null || archivo.ContentLength == 0)
                {
                    return Json(new { success = false, message = "Por favor seleccione un archivo Excel" });
                }

                // Validar extensión del archivo
                var extension = Path.GetExtension(archivo.FileName).ToLower();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    return Json(new { success = false, message = "Solo se permiten archivos Excel (.xlsx, .xls)" });
                }

                var errores = new List<string>();
                var exitosos = 0;
                var fallidos = 0;

                using (var stream = archivo.InputStream)
                {
                    using (var package = new ExcelPackage(stream))
                    {
                        // Buscar la hoja "Datos"
                        var worksheet = package.Workbook.Worksheets["Datos"];

                        // Si no existe la hoja "Datos", usar la primera hoja
                        if (worksheet == null)
                        {
                            worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        }

                        if (worksheet == null)
                        {
                            return Json(new { success = false, message = "El archivo Excel no contiene hojas de trabajo" });
                        }

                        // Buscar la fila de encabezados (puede estar en fila 1 o fila 3)
                        int filaEncabezado = 0;
                        int filaInicioDatos = 0;

                        // Verificar si la fila 1 tiene los encabezados (formato antiguo)
                        var encabezadosFila1 = new string[]
                        {
                    worksheet.Cells[1, 1].Text.Trim(),
                    worksheet.Cells[1, 2].Text.Trim(),
                    worksheet.Cells[1, 3].Text.Trim(),
                    worksheet.Cells[1, 4].Text.Trim(),
                    worksheet.Cells[1, 5].Text.Trim(),
                    worksheet.Cells[1, 6].Text.Trim()
                        };

                        // Verificar si la fila 3 tiene los encabezados (formato nuevo)
                        var encabezadosFila3 = new string[]
                        {
                    worksheet.Cells[3, 1].Text.Trim(),
                    worksheet.Cells[3, 2].Text.Trim(),
                    worksheet.Cells[3, 3].Text.Trim(),
                    worksheet.Cells[3, 4].Text.Trim(),
                    worksheet.Cells[3, 5].Text.Trim(),
                    worksheet.Cells[3, 6].Text.Trim()
                        };

                        var encabezadosEsperados = new[] { "Asignatura", "Programa Educativo", "Área", "Cuatrimestre", "Plan de Estudio", "Unidades" };

                        // Determinar qué fila tiene los encabezados correctos
                        bool encabezadosEnFila1 = true;
                        for (int i = 0; i < encabezadosEsperados.Length; i++)
                        {
                            if (!encabezadosFila1[i].Equals(encabezadosEsperados[i], StringComparison.OrdinalIgnoreCase))
                            {
                                encabezadosEnFila1 = false;
                                break;
                            }
                        }

                        bool encabezadosEnFila3 = true;
                        for (int i = 0; i < encabezadosEsperados.Length; i++)
                        {
                            if (!encabezadosFila3[i].Equals(encabezadosEsperados[i], StringComparison.OrdinalIgnoreCase))
                            {
                                encabezadosEnFila3 = false;
                                break;
                            }
                        }

                        if (encabezadosEnFila1)
                        {
                            filaEncabezado = 1;
                            filaInicioDatos = 2;
                        }
                        else if (encabezadosEnFila3)
                        {
                            filaEncabezado = 3;
                            filaInicioDatos = 4;
                        }
                        else
                        {
                            return Json(new
                            {
                                success = false,
                                message = "No se encontraron los encabezados correctos. Asegúrese de usar la plantilla proporcionada."
                            });
                        }

                        // Procesar solo filas que realmente tienen datos
                        var totalFilas = worksheet.Dimension?.Rows ?? 0;

                        for (int fila = filaInicioDatos; fila <= totalFilas; fila++)
                        {
                            try
                            {
                                var asignatura = worksheet.Cells[fila, 1].Text.Trim().ToUpper();
                                var programaEducativo = worksheet.Cells[fila, 2].Text.Trim();
                                var area = worksheet.Cells[fila, 3].Text.Trim();
                                var cuatrimestre = worksheet.Cells[fila, 4].Text.Trim();
                                var planEstudio = worksheet.Cells[fila, 5].Text.Trim();
                                var unidades = worksheet.Cells[fila, 6].Text.Trim();

                                // Saltar filas completamente vacías
                                if (string.IsNullOrEmpty(asignatura) &&
                                    string.IsNullOrEmpty(programaEducativo) &&
                                    string.IsNullOrEmpty(area) &&
                                    string.IsNullOrEmpty(cuatrimestre) &&
                                    string.IsNullOrEmpty(planEstudio) &&
                                    string.IsNullOrEmpty(unidades))
                                {
                                    continue;
                                }

                                // Validar que no estén vacíos los campos obligatorios
                                if (string.IsNullOrEmpty(asignatura) || string.IsNullOrEmpty(programaEducativo) ||
                                    string.IsNullOrEmpty(area) || string.IsNullOrEmpty(cuatrimestre) ||
                                    string.IsNullOrEmpty(planEstudio))
                                {
                                    errores.Add($"Fila {fila}: Todos los campos son obligatorios excepto Unidades. Falta: " +
                                        (string.IsNullOrEmpty(asignatura) ? "Asignatura " : "") +
                                        (string.IsNullOrEmpty(programaEducativo) ? "Programa " : "") +
                                        (string.IsNullOrEmpty(area) ? "Área " : "") +
                                        (string.IsNullOrEmpty(cuatrimestre) ? "Cuatrimestre " : "") +
                                        (string.IsNullOrEmpty(planEstudio) ? "Plan de Estudio " : ""));
                                    fallidos++;
                                    continue;
                                }

                                // Buscar carrera
                                var carrera = db.Carreras.FirstOrDefault(c =>
                                    c.Nombre.Trim().Equals(programaEducativo.Trim(), StringComparison.OrdinalIgnoreCase));
                                if (carrera == null)
                                {
                                    errores.Add($"Fila {fila}: No se encontró el programa educativo '{programaEducativo}'");
                                    fallidos++;
                                    continue;
                                }

                                // 🔑 VALIDAR PERMISOS SOBRE LA CARRERA
                                Usuario usuarioActual = Session["Usuario"] as Usuario;
                                if (!UsuarioTieneAccesoCarrera(usuarioActual, carrera.IdCarrera))
                                {
                                    errores.Add($"Fila {fila}: ⛔ ACCESO DENEGADO. No tienes permisos para importar materias del programa '{programaEducativo}'. Solo puedes importar materias de tu carrera asignada.");
                                    fallidos++;
                                    continue;
                                }

                                // Buscar especialidad
                                var especialidad = db.Especialidads.FirstOrDefault(e =>
                                    e.Nombre.Trim().Equals(area.Trim(), StringComparison.OrdinalIgnoreCase) &&
                                    e.IdCarrera == carrera.IdCarrera);
                                if (especialidad == null)
                                {
                                    errores.Add($"Fila {fila}: No se encontró el área '{area}' para el programa '{programaEducativo}'");
                                    fallidos++;
                                    continue;
                                }

                                // Buscar grado
                                var grado = db.Gradoes.FirstOrDefault(g =>
                                    g.Nombre.Trim().Equals(cuatrimestre.Trim(), StringComparison.OrdinalIgnoreCase));
                                if (grado == null)
                                {
                                    errores.Add($"Fila {fila}: No se encontró el cuatrimestre '{cuatrimestre}'");
                                    fallidos++;
                                    continue;
                                }

                                // Buscar plan de estudio
                                var planEstudioBuscado = ObtenerPlanesEstudioActivos().FirstOrDefault(p =>
                                    p.Nombre.Trim().Equals(planEstudio.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                    p.Año.ToString().Equals(planEstudio.Trim(), StringComparison.OrdinalIgnoreCase));

                                if (planEstudioBuscado == null)
                                {
                                    errores.Add($"Fila {fila}: No se encontró el plan de estudio '{planEstudio}'. Use '2020' o '2024'");
                                    fallidos++;
                                    continue;
                                }

                                // Procesar número de unidades
                                int numeroUnidades = 3; // Valor por defecto
                                if (!string.IsNullOrEmpty(unidades))
                                {
                                    if (!int.TryParse(unidades, out numeroUnidades))
                                    {
                                        errores.Add($"Fila {fila}: El número de unidades '{unidades}' no es válido. Use un número entre 1 y 6");
                                        fallidos++;
                                        continue;
                                    }

                                    if (numeroUnidades < 1 || numeroUnidades > 6)
                                    {
                                        errores.Add($"Fila {fila}: El número de unidades debe estar entre 1 y 6. Valor proporcionado: {numeroUnidades}");
                                        fallidos++;
                                        continue;
                                    }
                                }

                                // Verificar si ya existe la materia
                                var materiaExistente = db.Materias.FirstOrDefault(m =>
                                    m.Nombre.Trim().ToUpper() == asignatura.Trim().ToUpper() &&
                                    m.IdCarrera == carrera.IdCarrera &&
                                    m.IdEspecialidad == especialidad.Id &&
                                    m.IdGrado == grado.IdGrado &&
                                    m.IdPlanEstudio == planEstudioBuscado.IdPlanEstudio);

                                if (materiaExistente != null)
                                {
                                    // Ya existe, omitir
                                    continue;
                                }

                                // Insertar la nueva materia
                                bool insertado = false;
                                try
                                {
                                    var sqlInsert = @"INSERT INTO Materias (Nombre, IdCarrera, IdEspecialidad, IdGrado, IdPlanEstudio, NumeroUnidades, Activo) 
                                             VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)";

                                    db.Database.ExecuteSqlCommand(sqlInsert,
                                        asignatura.Trim(),
                                        carrera.IdCarrera,
                                        especialidad.Id,
                                        grado.IdGrado,
                                        planEstudioBuscado.IdPlanEstudio,
                                        numeroUnidades,
                                        true);

                                    exitosos++;
                                    insertado = true;
                                }
                                catch (System.Data.Entity.Infrastructure.DbUpdateException efEx)
                                {
                                    if (!insertado)
                                    {
                                        errores.Add($"Fila {fila}: Error al guardar - {efEx.InnerException?.Message ?? efEx.Message}");
                                        fallidos++;
                                    }
                                }

                                if (!insertado && !errores.Any(e => e.Contains($"Fila {fila}:")))
                                {
                                    errores.Add($"Fila {fila}: No se pudo insertar por razón desconocida");
                                    fallidos++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errores.Add($"Fila {fila}: Error inesperado - {ex.Message}");
                                fallidos++;
                            }
                        }
                    }
                }

                // Preparar respuesta
                var mensaje = exitosos > 0
                    ? $"Importación exitosa: {exitosos} asignaturas importadas" + (fallidos > 0 ? $", {fallidos} con problemas" : "")
                    : fallidos > 0
                        ? $"No se importaron asignaturas: {fallidos} registros con problemas"
                        : "No se encontraron datos para importar";

                return Json(new
                {
                    success = exitosos > 0,
                    message = mensaje,
                    exitosos = exitosos,
                    fallidos = fallidos,
                    errores = errores.Take(20).ToList(),
                    totalErrores = errores.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al procesar el archivo: " + ex.Message
                });
            }
        }
        // Método para descargar plantilla Excel
        public ActionResult DescargarPlantilla()
        {
            // 🔑 OBTENER USUARIO Y VALIDAR PERMISOS
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                using (var package = new ExcelPackage())
                {
                    // ===== HOJA 1: DATOS PARA IMPORTAR (LIMPIA) =====
                    var worksheetDatos = package.Workbook.Worksheets.Add("DATOS");

                    // Fila 1: Nota sobre instrucciones
                    worksheetDatos.Cells["A1"].Value = "→ Ver hoja 'INSTRUCCIONES' para información detallada sobre cómo completar esta plantilla ←";
                    worksheetDatos.Cells["A1:F1"].Merge = true;
                    var notaInstrucciones = worksheetDatos.Cells["A1:F1"];
                    notaInstrucciones.Style.Font.Bold = true;
                    notaInstrucciones.Style.Font.Size = 11;
                    notaInstrucciones.Style.Font.Color.SetColor(Color.White);
                    notaInstrucciones.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    notaInstrucciones.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(52, 152, 219)); // Azul
                    notaInstrucciones.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    notaInstrucciones.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                    // Fila 2: Espacio en blanco
                    worksheetDatos.Row(2).Height = 5;

                    // Fila 3: Encabezados para importación
                    worksheetDatos.Cells["A3"].Value = "Asignatura";
                    worksheetDatos.Cells["B3"].Value = "Programa Educativo";
                    worksheetDatos.Cells["C3"].Value = "Área";
                    worksheetDatos.Cells["D3"].Value = "Cuatrimestre";
                    worksheetDatos.Cells["E3"].Value = "Plan de Estudio";
                    worksheetDatos.Cells["F3"].Value = "Unidades";

                    // Estilo para encabezados
                    var headerRange = worksheetDatos.Cells["A3:F3"];
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.Size = 12;
                    headerRange.Style.Font.Color.SetColor(Color.White);
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125)); // Azul oscuro
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);

                    // Ajustar anchos de columnas
                    worksheetDatos.Column(1).Width = 45; // Asignatura
                    worksheetDatos.Column(2).Width = 35; // Programa Educativo
                    worksheetDatos.Column(3).Width = 45; // Área
                    worksheetDatos.Column(4).Width = 15; // Cuatrimestre
                    worksheetDatos.Column(5).Width = 20; // Plan de Estudio
                    worksheetDatos.Column(6).Width = 12; // Unidades

                    // Congelar primera fila
                    worksheetDatos.View.FreezePanes(4, 1);

                    // ===== HOJA 2: INSTRUCCIONES (COMPLETAS) =====
                    var worksheetInstrucciones = package.Workbook.Worksheets.Add("INSTRUCCIONES");

                    int filaActual = 1;

                    // Título principal
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "GUÍA COMPLETA PARA IMPORTAR MATERIAS";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var tituloPrincipal = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    tituloPrincipal.Style.Font.Bold = true;
                    tituloPrincipal.Style.Font.Size = 16;
                    tituloPrincipal.Style.Font.Color.SetColor(Color.White);
                    tituloPrincipal.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloPrincipal.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                    tituloPrincipal.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    tituloPrincipal.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    worksheetInstrucciones.Row(filaActual).Height = 30;
                    filaActual += 2;

                    // ===== INSTRUCCIONES DE USO =====
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "📋 INSTRUCCIONES DE USO";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var tituloInstrucciones = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    tituloInstrucciones.Style.Font.Bold = true;
                    tituloInstrucciones.Style.Font.Size = 14;
                    tituloInstrucciones.Style.Font.Color.SetColor(Color.White);
                    tituloInstrucciones.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloInstrucciones.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                    tituloInstrucciones.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaActual += 1;

                    // Instrucciones detalladas
                    var instrucciones = new List<string>
            {
                "1. Vaya a la hoja 'Datos' de este archivo Excel",
                "2. Complete sus materias desde la FILA 4 en adelante",
                "3. Copie y pegue los valores EXACTOS de las listas de abajo",
                "4. Todos los campos son obligatorios (6 columnas)",
                "5. Verifique que el Área corresponda al Programa Educativo seleccionado",
                "6. El número de Unidades debe ser entre 1 y 6 (deje vacío para usar 3 por defecto)",
                "7. Guarde el archivo y súbalo al sistema"
            };

                    foreach (var instruccion in instrucciones)
                    {
                        worksheetInstrucciones.Cells[filaActual, 1].Value = instruccion;
                        worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                        var celdaInstruccion = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                        celdaInstruccion.Style.Font.Size = 11;
                        celdaInstruccion.Style.WrapText = true;
                        celdaInstruccion.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        celdaInstruccion.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 237, 247));
                        celdaInstruccion.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        worksheetInstrucciones.Row(filaActual).Height = 25;
                        filaActual++;
                    }

                    filaActual += 2;

                    // 🔑 OBTENER SOLO CARRERAS ACCESIBLES SEGÚN PERMISOS
                    var carreras = ObtenerCarrerasAccesibles(usuario);

                    if (carreras.Count == 0)
                    {
                        return Content("Error: No tienes carreras asignadas. Contacta al administrador.");
                    }

                    // ===== PROGRAMAS EDUCATIVOS Y ÁREAS =====
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "📚 PROGRAMAS EDUCATIVOS Y ÁREAS DISPONIBLES";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var tituloAreas = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    tituloAreas.Style.Font.Bold = true;
                    tituloAreas.Style.Font.Size = 14;
                    tituloAreas.Style.Font.Color.SetColor(Color.White);
                    tituloAreas.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloAreas.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(46, 204, 113));
                    tituloAreas.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaActual++;

                    worksheetInstrucciones.Cells[filaActual, 1].Value = "Copie estos valores exactamente como aparecen:";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    worksheetInstrucciones.Cells[filaActual, 1].Style.Font.Italic = true;
                    worksheetInstrucciones.Cells[filaActual, 1].Style.Font.Size = 10;
                    filaActual++;

                    // Encabezados para áreas
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "Programa Educativo";
                    worksheetInstrucciones.Cells[filaActual, 2].Value = "Área";
                    var headerAreas = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 2];
                    headerAreas.Style.Font.Bold = true;
                    headerAreas.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerAreas.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(200, 200, 200));
                    headerAreas.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerAreas.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                    filaActual++;

                    // LISTAR SOLO CARRERAS ACCESIBLES POR EL USUARIO
                    try
                    {
                        foreach (var carrera in carreras)
                        {
                            var especialidades = db.Especialidads
                                .Where(e => e.IdCarrera == carrera.IdCarrera)
                                .OrderBy(e => e.Nombre)
                                .Select(e => e.Nombre)
                                .ToList();

                            foreach (var especialidad in especialidades)
                            {
                                worksheetInstrucciones.Cells[filaActual, 1].Value = carrera.Nombre;
                                worksheetInstrucciones.Cells[filaActual, 2].Value = especialidad;
                                worksheetInstrucciones.Cells[filaActual, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheetInstrucciones.Cells[filaActual, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(240, 240, 240));
                                worksheetInstrucciones.Cells[filaActual, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheetInstrucciones.Cells[filaActual, 2].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 237, 247));
                                worksheetInstrucciones.Cells[filaActual, 1, filaActual, 2].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                                filaActual++;
                            }
                        }
                    }
                    catch (Exception exEspecialidades)
                    {
                        return Content("Error al obtener Áreas: " + exEspecialidades.Message);
                    }

                    filaActual += 2;

                    // ===== CUATRIMESTRES DISPONIBLES =====
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "📅 CUATRIMESTRES DISPONIBLES";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var tituloCuatrimestres = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    tituloCuatrimestres.Style.Font.Bold = true;
                    tituloCuatrimestres.Style.Font.Size = 14;
                    tituloCuatrimestres.Style.Font.Color.SetColor(Color.White);
                    tituloCuatrimestres.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloCuatrimestres.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(230, 126, 34));
                    tituloCuatrimestres.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaActual++;

                    // Encabezado
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "Cuatrimestre";
                    var headerCuatrimestre = worksheetInstrucciones.Cells[filaActual, 1];
                    headerCuatrimestre.Style.Font.Bold = true;
                    headerCuatrimestre.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerCuatrimestre.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(200, 200, 200));
                    headerCuatrimestre.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerCuatrimestre.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                    filaActual++;

                    // Listar cuatrimestres
                    try
                    {
                        var grados = db.Gradoes.OrderBy(g => g.Nombre).ToList();
                        foreach (var grado in grados)
                        {
                            worksheetInstrucciones.Cells[filaActual, 1].Value = grado.Nombre;
                            worksheetInstrucciones.Cells[filaActual, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheetInstrucciones.Cells[filaActual, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 224));
                            worksheetInstrucciones.Cells[filaActual, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            worksheetInstrucciones.Cells[filaActual, 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                            filaActual++;
                        }
                    }
                    catch (Exception exGrados)
                    {
                        return Content("Error al obtener Cuatrimestres: " + exGrados.Message);
                    }

                    filaActual += 2;

                    // ===== PLANES DE ESTUDIO DISPONIBLES =====
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "📖 PLANES DE ESTUDIO DISPONIBLES";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var tituloPlanes = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    tituloPlanes.Style.Font.Bold = true;
                    tituloPlanes.Style.Font.Size = 14;
                    tituloPlanes.Style.Font.Color.SetColor(Color.White);
                    tituloPlanes.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloPlanes.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(155, 89, 182));
                    tituloPlanes.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaActual++;

                    // Encabezados para planes
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "Plan de Estudio";
                    worksheetInstrucciones.Cells[filaActual, 2].Value = "Calificación Mínima";
                    worksheetInstrucciones.Cells[filaActual, 3].Value = "Permite Decimales";
                    var headerPlanes = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 3];
                    headerPlanes.Style.Font.Bold = true;
                    headerPlanes.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerPlanes.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(200, 200, 200));
                    headerPlanes.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerPlanes.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                    filaActual++;

                    // Obtener planes de estudio
                    try
                    {
                        var planesData = db.Database.SqlQuery<PlanEstudioDto>(
                            @"SELECT IdPlanEstudio, CAST([Año] AS NVARCHAR(100)) as Nombre, [Año] as Anio, CalificacionMinima, PermiteDecimales, Activo 
                      FROM PlanesEstudio 
                      WHERE Activo = 1 
                      ORDER BY [Año]"
                        ).ToList();

                        foreach (var plan in planesData)
                        {
                            worksheetInstrucciones.Cells[filaActual, 1].Value = plan.Nombre;
                            worksheetInstrucciones.Cells[filaActual, 2].Value = plan.CalificacionMinima;
                            worksheetInstrucciones.Cells[filaActual, 3].Value = plan.PermiteDecimales ? "Sí" : "No";

                            var planRow = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 3];
                            planRow.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            planRow.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(235, 222, 240));
                            planRow.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            planRow.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                            filaActual++;
                        }
                    }
                    catch (Exception exPlanes)
                    {
                        return Content("Error al obtener Planes de Estudio: " + exPlanes.Message);
                    }

                    filaActual += 2;

                    // ===== NÚMERO DE UNIDADES =====
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "🔢 NÚMERO DE UNIDADES";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var tituloUnidades = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    tituloUnidades.Style.Font.Bold = true;
                    tituloUnidades.Style.Font.Size = 14;
                    tituloUnidades.Style.Font.Color.SetColor(Color.White);
                    tituloUnidades.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    tituloUnidades.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(52, 152, 219));
                    tituloUnidades.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaActual++;

                    worksheetInstrucciones.Cells[filaActual, 1].Value = "Valores válidos: 1, 2, 3, 4, 5 o 6";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var unidadesInfo1 = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    unidadesInfo1.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    unidadesInfo1.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(214, 234, 248));
                    unidadesInfo1.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    unidadesInfo1.Style.Font.Bold = true;
                    unidadesInfo1.Style.Font.Size = 12;
                    filaActual++;

                    worksheetInstrucciones.Cells[filaActual, 1].Value = "Si deja la celda vacía, se usarán 3 unidades por defecto";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var unidadesInfo2 = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    unidadesInfo2.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    unidadesInfo2.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(214, 234, 248));
                    unidadesInfo2.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    unidadesInfo2.Style.Font.Italic = true;
                    filaActual += 3;

                    // ===== NOTA FINAL =====
                    worksheetInstrucciones.Cells[filaActual, 1].Value = "⚠️ IMPORTANTE: Copie y pegue los valores exactos de las listas de arriba para evitar errores durante la importación";
                    worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6].Merge = true;
                    var notaFinal = worksheetInstrucciones.Cells[filaActual, 1, filaActual, 6];
                    notaFinal.Style.Font.Bold = true;
                    notaFinal.Style.Font.Size = 12;
                    notaFinal.Style.Font.Color.SetColor(Color.Red);
                    notaFinal.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    notaFinal.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 255, 200));
                    notaFinal.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    notaFinal.Style.WrapText = true;
                    worksheetInstrucciones.Row(filaActual).Height = 30;

                    // Ajustar anchos de columnas en la hoja de instrucciones
                    worksheetInstrucciones.Column(1).Width = 45;
                    worksheetInstrucciones.Column(2).Width = 45;
                    worksheetInstrucciones.Column(3).Width = 20;
                    worksheetInstrucciones.Column(4).Width = 15;
                    worksheetInstrucciones.Column(5).Width = 20;
                    worksheetInstrucciones.Column(6).Width = 12;

                    // Configuración de impresión
                    worksheetDatos.PrinterSettings.Orientation = eOrientation.Portrait;
                    worksheetDatos.PrinterSettings.FitToPage = true;
                    worksheetDatos.PrinterSettings.FitToWidth = 1;

                    worksheetInstrucciones.PrinterSettings.Orientation = eOrientation.Portrait;
                    worksheetInstrucciones.PrinterSettings.FitToPage = true;
                    worksheetInstrucciones.PrinterSettings.FitToWidth = 1;

                    // Generar archivo
                    var excelBytes = package.GetAsByteArray();
                    string nombreArchivo = string.Format("Plantilla_Importar_Materias_{0}.xlsx", DateTime.Now.ToString("yyyyMMdd"));

                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                }
            }
            catch (Exception ex)
            {
                return Content("Error general al generar la plantilla: " + ex.Message + " - Inner: " + (ex.InnerException?.Message ?? "No hay inner exception"));
            }
        }


        public class PlanEstudioDto
        {
            public int IdPlanEstudio { get; set; }
            public string Nombre { get; set; }
            public int Anio { get; set; }
            public decimal CalificacionMinima { get; set; }
            public bool PermiteDecimales { get; set; }
            public bool Activo { get; set; }
        }

        public class CarreraDto
        {
            public int IdCarrera { get; set; }
            public string Nombre { get; set; }
        }

        // OTROS MÉTODOS

        // GET: Materias/Create
        public ActionResult Create()
        {
            // ✅ VALIDAR ACCESO
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            if (!ValidarAccesoCoordinador(usuario))
            {
                TempData["Error"] = "No tienes permisos para realizar esta acción.";
                return RedirectToAction("Index", "Materias");
            }

            // 🔑 CARGAR SOLO CARRERAS ACCESIBLES
            ViewBag.IdCarrera = new SelectList(ObtenerCarrerasAccesibles(usuario), "IdCarrera", "Nombre");
            ViewBag.IdEspecialidad = new SelectList(db.Especialidads.ToList(), "Id", "Nombre");
            ViewBag.IdGrado = new SelectList(db.Gradoes.ToList(), "IdGrado", "Nombre");
            ViewBag.IdPlanEstudio = new SelectList(ObtenerPlanesEstudioActivos(), "IdPlanEstudio", "Nombre");

            return View(new Materia());
        }

        // POST: Materias/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Materia materia)
        {
            // ✅ VALIDAR ACCESO
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            if (!ValidarAccesoCoordinador(usuario))
            {
                TempData["Error"] = "No tienes permisos para realizar esta acción.";
                return RedirectToAction("Index", "Materias");
            }

            // 🔑 VALIDAR QUE EL COORDINADOR SOLO CREE MATERIAS DE SU CARRERA
            if (!UsuarioTieneAccesoCarrera(usuario, materia.IdCarrera))
            {
                TempData["Error"] = "No tienes permisos para crear materias en esta carrera.";
                ViewBag.IdCarrera = new SelectList(ObtenerCarrerasAccesibles(usuario), "IdCarrera", "Nombre", materia.IdCarrera);
                ViewBag.IdEspecialidad = new SelectList(db.Especialidads.ToList(), "Id", "Nombre", materia.IdEspecialidad);
                ViewBag.IdGrado = new SelectList(db.Gradoes.ToList(), "IdGrado", "Nombre", materia.IdGrado);
                ViewBag.IdPlanEstudio = new SelectList(ObtenerPlanesEstudioActivos(), "IdPlanEstudio", "Nombre", materia.IdPlanEstudio);
                return View(materia);
            }

            try
            {
                if (!string.IsNullOrEmpty(materia.Nombre) && materia.IdCarrera > 0 &&
                    materia.IdEspecialidad > 0 && materia.IdGrado > 0 && materia.IdPlanEstudio > 0)
                {
                    materia.Activo = true; // Siempre activo por defecto

                    // Intentar con SQL directo primero
                    try
                    {
                        var sqlInsert = @"INSERT INTO Materias (Nombre, IdCarrera, IdEspecialidad, IdGrado, IdPlanEstudio, NumeroUnidades, Activo) 
                                 VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)";

                        db.Database.ExecuteSqlCommand(sqlInsert,
                            materia.Nombre.Trim(), materia.IdCarrera, materia.IdEspecialidad,
                            materia.IdGrado, materia.IdPlanEstudio, materia.NumeroUnidades, true);
                    }
                    catch
                    {
                        // Fallback a Entity Framework
                        db.Materias.Add(materia);
                        db.SaveChanges();
                    }

                    TempData["Success"] = "Materia creada exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al guardar: " + ex.Message;
            }

            ViewBag.IdCarrera = new SelectList(ObtenerCarrerasAccesibles(usuario), "IdCarrera", "Nombre", materia.IdCarrera);
            ViewBag.IdEspecialidad = new SelectList(db.Especialidads.ToList(), "Id", "Nombre", materia.IdEspecialidad);
            ViewBag.IdGrado = new SelectList(db.Gradoes.ToList(), "IdGrado", "Nombre", materia.IdGrado);
            ViewBag.IdPlanEstudio = new SelectList(ObtenerPlanesEstudioActivos(), "IdPlanEstudio", "Nombre", materia.IdPlanEstudio);
            return View(materia);
        }

        // AJAX method to get especialidades by carrera (sin cambios)
        public JsonResult GetEspecialidades(int carreraId)
        {
            try
            {
                var especialidades = db.Especialidads.ToList();
                var filtered = especialidades.Where(e => e.IdCarrera == carreraId).ToList();
                var result = filtered.Select(e => new { Id = e.Id, Nombre = e.Nombre }).ToList();
                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult ToggleActivo(int id)
        {
            // ✅ AGREGAR ESTA VALIDACIÓN AL INICIO
            if (!ValidarAccesoCoordinadorOMaster())
            {
                return Json(new { success = false, message = "No tienes permisos para realizar esta acción." });
            }

            try
            {
                var materia = db.Materias.Find(id);
                if (materia != null)
                {
                    Usuario usuario = Session["Usuario"] as Usuario;
                    // 🔑 VALIDAR ACCESO A LA CARRERA
                    if (!UsuarioTieneAccesoCarrera(usuario, materia.IdCarrera))
                    {
                        return Json(new { success = false, message = "No tienes permisos para modificar materias de esta carrera." });
                    }

                    materia.Activo = !materia.Activo;
                    db.SaveChanges();
                    return Json(new { success = true, activo = materia.Activo });
                }
                return Json(new { success = false });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        private bool ValidarAccesoCoordinadorOMaster()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
                return false;

            return usuario.IdNivel == 3 || usuario.IdNivel == 4;
        }

        // MÉTODOS AUXILIARES PARA CONTROL DE ACCESO POR CARRERA
        private bool ValidarAccesoCoordinador(Usuario usuario)
        {
            if (usuario == null) return false;
            return usuario.IdNivel == 3 || usuario.IdNivel == 4; // Coordinador o Master
        }

        private bool UsuarioTieneAccesoCarrera(Usuario usuario, int idCarrera)
        {
            if (usuario == null) return false;

            // Master tiene acceso a todas las carreras
            if (usuario.IdNivel == 4) return true;

            // Coordinador solo a su carrera asignada
            if (usuario.IdNivel == 3)
            {
                return usuario.IdCarrera == idCarrera;
            }

            return false;
        }

        private int? ObtenerCarreraPermitida(Usuario usuario)
        {
            if (usuario == null) return null;

            // Master: puede ver todas (retornamos null para indicar "sin filtro")
            if (usuario.IdNivel == 4) return null;

            // Coordinador: solo su carrera
            if (usuario.IdNivel == 3) return usuario.IdCarrera;

            return null;
        }

        private List<Carrera> ObtenerCarrerasAccesibles(Usuario usuario)
        {
            if (usuario == null) return new List<Carrera>();

            // Master: todas las carreras
            if (usuario.IdNivel == 4)
            {
                return db.Carreras.OrderBy(c => c.Nombre).ToList();
            }

            // Coordinador: solo su carrera
            if (usuario.IdNivel == 3 && usuario.IdCarrera != null && usuario.IdCarrera > 0)
            {
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                return carrera != null ? new List<Carrera> { carrera } : new List<Carrera>();
            }

            return new List<Carrera>();
        }


        protected override void Dispose(bool disposing)
        {
            {
                if (disposing)
                {
                    db.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}