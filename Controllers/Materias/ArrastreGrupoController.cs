using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models.Materias;
using System;
using System.Collections.Generic;
using System.Data.Entity; // ✅ IMPORTANTE: Asegúrate de tener este using para .Include()
using System.Linq;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.Materias
{
    [CustomAuthorize(Nivel = 2)] // Requisito base, la lógica interna maneja los roles
    public class ArrastreGrupoController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // ✅ MÉTODO PRINCIPAL CORREGIDO
        public ActionResult ArrastrePorGrupo(int? id)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Valida Nivel 2 (Tutor), 3 (Coord), 4 (Master)
            if (!ValidarAccesoUsuario(usuario))
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // ✅ --- INICIO DE CAMBIOS EN DROPDOWN ---
                // CARGAR DROPDOWN CON LÓGICA DE ROLES
                // (Llama al nuevo método que está más abajo)
                CargarDropdownGruposPorRol(usuario, id);
                // ✅ --- FIN DE CAMBIOS EN DROPDOWN ---

                var tutorias = (List<SelectListItem>)ViewBag.Grupos;


                if (id == null || id == 0)
                {
                    var primerGrupo = tutorias.FirstOrDefault(x => x.Value != "-1");
                    if (primerGrupo != null)
                    {
                        return RedirectToAction("ArrastrePorGrupo", new { id = primerGrupo.Value });
                    }
                    else
                    {
                        ViewBag.Error = "No se encontraron grupos disponibles para tu usuario."; // Mensaje actualizado
                        ViewBag.HayDatos = false;
                        ViewBag.NombreCarrera = "Sin Grupos";
                        ViewBag.NombreGrupo = "Sin Grupos";
                        ViewBag.NombreGrado = "Sin Grupos";
                        ViewBag.NombreEspecialidad = "";
                        ViewBag.IdTutoriaGrupal = -1;

                        return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", new List<ArrastreGrupoDto>());
                    }
                }

                if (id == -1)
                {
                    if (usuario.IdNivel < 3)
                    {
                        ViewBag.Error = "No tienes permisos para ver alumnos removidos.";
                        return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", new List<ArrastreGrupoDto>());
                    }

                    ViewBag.NombreCarrera = "Alumnos Removidos";
                    ViewBag.NombreGrupo = "";
                    ViewBag.NombreGrado = "";
                    ViewBag.NombreEspecialidad = "";
                    ViewBag.IdTutoriaGrupal = id;
                    ViewBag.HayDatos = false;

                    return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", new List<ArrastreGrupoDto>());
                }

                // Cargar relaciones para el chequeo de seguridad
                var grupo = db.TutoriaGrupals
                    .Include(g => g.Carrera)
                    .Include(g => g.Grado)
                    .Include(g => g.Grupo)
                    .Include(g => g.Turno)
                    .Include(g => g.Periodo)
                    .FirstOrDefault(x => x.IdTutoriaGrupal == id);

                if (grupo == null)
                {
                    ViewBag.Error = "No se encontró el grupo especificado.";
                    ViewBag.HayDatos = false;
                    ViewBag.NombreCarrera = "Grupo No Encontrado";
                    ViewBag.NombreGrupo = "";
                    ViewBag.NombreGrado = "";
                    ViewBag.NombreEspecialidad = "";
                    ViewBag.IdTutoriaGrupal = id;

                    return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", new List<ArrastreGrupoDto>());
                }

                // ✅ NUEVO: CHEQUEO DE SEGURIDAD DE ACCESO AL GRUPO
                if (!UsuarioTieneAccesoAlGrupo(usuario, grupo))
                {
                    ViewBag.Error = "Acceso denegado. No tienes permisos para ver el grupo solicitado.";
                    ViewBag.HayDatos = false;
                    EstablecerInfoGrupo(grupo); // Carga la info para que el usuario se ubique
                    ViewBag.IdTutoriaGrupal = id;
                    return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", new List<ArrastreGrupoDto>());
                }


                // ✅ LOGS DE DEPURACIÓN
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔍 DEBUG - PARÁMETROS DE CONSULTA");
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"IdCarrera: {grupo.IdCarrera}");
                System.Diagnostics.Debug.WriteLine($"IdGrado: {grupo.IdGrado}");
                System.Diagnostics.Debug.WriteLine($"IdGrupo: {grupo.IdGrupo}");
                System.Diagnostics.Debug.WriteLine($"IdTurno: {grupo.IdTurno}");
                System.Diagnostics.Debug.WriteLine($"IdPeriodo: {grupo.IdPeriodo}");
                System.Diagnostics.Debug.WriteLine($"Año: {grupo.Año}");

                // ✅ PASO 1: VERIFICAR QUE EXISTAN ALUMNOS CON MATERIAS EN PROBLEMA (SIN FILTROS ESTRICTOS)
                var verificacion = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(DISTINCT dp.IdPersona)
                    FROM DatosPersonales dp
                    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
                    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
                    WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
                      AND dp.IdCarrera = @p0
                      AND (e.Nombre = dp.Especialidad OR m.IdEspecialidad IS NULL OR m.IdEspecialidad = 0)",
                    grupo.IdCarrera).FirstOrDefault();

                System.Diagnostics.Debug.WriteLine($"📊 Alumnos con problemas en la carrera (sin filtros): {verificacion}");

                // ✅ PASO 2: VERIFICAR CON FILTROS BÁSICOS
                var verificacion2 = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(DISTINCT dp.IdPersona)
                    FROM DatosPersonales dp
                    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
                    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
                    WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
                      AND dp.IdCarrera = @p0
                      AND dp.IdGrado = @p1
                      AND (e.Nombre = dp.Especialidad OR m.IdEspecialidad IS NULL OR m.IdEspecialidad = 0)",
                    grupo.IdCarrera, grupo.IdGrado).FirstOrDefault();

                System.Diagnostics.Debug.WriteLine($"📊 Alumnos con problemas (Carrera + Grado): {verificacion2}");

                // ✅ PASO 3: VERIFICAR CON TODOS LOS FILTROS
                var verificacion3 = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(DISTINCT dp.IdPersona)
                    FROM DatosPersonales dp
                    INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
                    INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
                    LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
                    WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
                      AND dp.IdCarrera = @p0
                      AND dp.IdGrado = @p1
                      AND dp.IdGrupo = @p2
                      AND dp.IdTurno = @p3
                      AND dp.IdPeriodo = @p4
                      AND dp.Año = @p5
                      AND (e.Nombre = dp.Especialidad OR m.IdEspecialidad IS NULL OR m.IdEspecialidad = 0)",
                    grupo.IdCarrera, grupo.IdGrado, grupo.IdGrupo,
                    grupo.IdTurno, grupo.IdPeriodo, grupo.Año).FirstOrDefault();

                System.Diagnostics.Debug.WriteLine($"📊 Alumnos con problemas (TODOS los filtros): {verificacion3}");
                System.Diagnostics.Debug.WriteLine("========================================");

                // ✅ CONSULTA SIMPLIFICADA - SOLO FILTROS ESENCIALES
                var datosArrastre = CargarDatosArrastreSimplificado(grupo);
                var resumenArrastre = CalcularResumenArrastre(grupo);

                EstablecerInfoGrupo(grupo);
                ViewBag.ResumenArrastre = resumenArrastre;
                ViewBag.IdTutoriaGrupal = id;
                ViewBag.HayDatos = datosArrastre.Count > 0;

                // ✅ LOG FINAL
                System.Diagnostics.Debug.WriteLine($"✅ Registros finales cargados: {datosArrastre.Count}");

                return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", datosArrastre);
            }
            catch (Exception ex)
            {
                try
                {
                    // ✅ Intentar recargar el dropdown con la lógica de roles
                    CargarDropdownGruposPorRol(usuario, id);
                }
                catch
                {
                    ViewBag.Grupos = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "", Text = "Error al cargar grupos" }
                    };
                }

                ViewBag.Error = "Error al cargar los datos: " + ex.Message;
                ViewBag.HayDatos = false;
                ViewBag.NombreCarrera = "Error";
                ViewBag.NombreGrupo = "Error";
                ViewBag.NombreGrado = "Error";
                ViewBag.NombreEspecialidad = "";
                ViewBag.IdTutoriaGrupal = id ?? 1;

                System.Diagnostics.Debug.WriteLine($"❌ ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ STACK: {ex.StackTrace}");

                return View("~/Views/MateriasAlumno/ArrastrePorGrupo.cshtml", new List<ArrastreGrupoDto>());
            }
        }

        // ====================================================================
        // ✅ INICIO DE MÉTODOS PRIVADOS (INCLUYENDO LOS NUEVOS)
        // ====================================================================

        /// <summary>
        /// ✅ NUEVO: Carga el dropdown de grupos (ViewBag.Grupos) filtrado según el rol del usuario.
        /// </summary>
        private void CargarDropdownGruposPorRol(Usuario usuario, int? idSeleccionado = null)
        {
            // 1. Empezamos con la consulta base a los grupos tutoriales
            var gruposQuery = db.TutoriaGrupals.AsQueryable();

            // 2. APLICAMOS LA LÓGICA DE ROLES
            switch (usuario.IdNivel)
            {
                case 4: // Nivel 4 = Master (Ve todas las carreras)
                    // No aplicar filtro
                    break;
                case 3: // Nivel 3 = Coordinador (Ve todos los grupos de SU carrera)
                    gruposQuery = gruposQuery.Where(x => x.IdCarrera == usuario.IdCarrera);
                    break;
                default: // Nivel 2 o inferior = Tutor (Ve SOLO sus grupos asignados)
                    gruposQuery = gruposQuery.Where(x => x.IdUsuario == usuario.IdUsuario);
                    break;
            }

            // 3. Unimos con las otras tablas para crear el texto descriptivo
            try
            {
                var grupos = gruposQuery
                    .Include(g => g.Carrera)
                    .Include(g => g.Grado)
                    .Include(g => g.Grupo)
                    .Include(g => g.Turno)
                    .Include(g => g.Periodo)
                    .OrderByDescending(x => x.Año)
                    .ThenByDescending(x => x.IdPeriodo)
                    .ThenBy(x => x.Carrera.Nomenclatura)
                    .ThenBy(x => x.IdGrado)
                    .ThenBy(x => x.IdGrupo)
                    .ToList();

                // 4. Creamos la lista final para el ViewBag
                var listaSelect = grupos.Select(g => new SelectListItem
                {
                    Value = g.IdTutoriaGrupal.ToString(),
                    Text = $"{g.Carrera?.Nomenclatura ?? "S/C"}, " +
                           $"{g.Grado?.Nombre ?? "S/G"}{g.Grupo?.Nombre ?? "S/G"}, " +
                           $"{g.Turno?.Nombre ?? "S/T"}, " +
                           $"{g.Periodo?.Nombre ?? "S/P"}, " +
                           $"{g.Año}",
                    Selected = g.IdTutoriaGrupal == idSeleccionado
                }).ToList();

                // 5. Agregar "Alumnos Removidos" solo para Coord/Master
                if (usuario.IdNivel >= 3)
                {
                    listaSelect.Add(new SelectListItem()
                    {
                        Value = "-1",
                        Text = "Alumnos Removidos",
                        Selected = idSeleccionado == -1
                    });
                }

                ViewBag.Grupos = listaSelect;
            }
            catch (Exception ex)
            {
                // Si algo falla, mostramos un error en el dropdown
                System.Diagnostics.Debug.WriteLine("Error al cargar Dropdown de Grupos: " + ex.Message);
                ViewBag.Grupos = new List<SelectListItem> {
                    new SelectListItem { Value = "", Text = "Error al cargar grupos" }
                };
            }
        }

        /// <summary>
        /// ✅ NUEVO: Valida si el usuario tiene permiso para ver un grupo específico.
        /// </summary>
        private bool UsuarioTieneAccesoAlGrupo(Usuario usuario, TutoriaGrupal grupo)
        {
            if (usuario == null || grupo == null) return false;

            switch (usuario.IdNivel)
            {
                case 4: // Master
                    return true; // Siempre tiene acceso
                case 3: // Coordinador
                    return grupo.IdCarrera == usuario.IdCarrera; // Solo si es de su carrera
                default: // Tutor (Nivel 2 o inferior)
                    return grupo.IdUsuario == usuario.IdUsuario; // Solo si es SU grupo
            }
        }


        // ✅ NUEVO MÉTODO SIMPLIFICADO PARA CARGAR DATOS
        private List<ArrastreGrupoDto> CargarDatosArrastreSimplificado(TutoriaGrupal grupo)
        {
            var listaArrastre = new List<ArrastreGrupoDto>();

            try
            {
                // ✅ CONSULTA SIMPLIFICADA CON DATOS COMPLETOS
                var query = @"
            SELECT 
                dp.IdPersona,
                ISNULL(dp.Matricula, '') as Matricula,
                ISNULL(dp.Nombre, 'Sin nombre') as NombreAlumno,
                ISNULL(m.Nombre, 'Sin materia') as MateriaArrastre,
                ISNULL(m.IdGrado, 1) as CuatrimestreMateria,
                ISNULL(ma.IntentosExtraordinarios, 0) as IntentosExtraordinarios,
                ma.FechaInicioArrastre,
                ma.FechaExamenExtraordinario,
                ISNULL(ma.Observaciones, '') as Observaciones,
                ISNULL(gr.Nombre, 'Sin grado') as NombreGrado,
                ISNULL(g.Nombre, 'Sin grupo') as NombreGrupo,
                ma.IdMateria,
                ma.Estado,
                m.Activo as MateriaEstaActiva,
                CASE WHEN m.Activo = 1 THEN 'Materia Activa' ELSE 'Materia Desactivada' END as EstadoMateria
            FROM DatosPersonales dp
            INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
            INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
            INNER JOIN Grupoes g ON dp.IdGrupo = g.IdGrupo
            INNER JOIN Gradoes gr ON dp.IdGrado = gr.IdGrado
            LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
            WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
              AND dp.IdCarrera = @p0
              AND dp.IdGrado = @p1
              AND dp.IdGrupo = @p2
              AND m.IdCarrera = dp.IdCarrera
            ORDER BY
                CASE WHEN ma.Estado = 'Reprobada' THEN 1 ELSE 2 END,
                m.Activo DESC,
                m.IdGrado ASC,
                dp.Nombre ASC";

                var resultados = db.Database.SqlQuery<ArrastreRawResultDto>(query,
                    grupo.IdCarrera, grupo.IdGrado, grupo.IdGrupo).ToList();

                System.Diagnostics.Debug.WriteLine($"📊 Resultados de consulta: {resultados.Count}");

                int materiasActivas = 0;
                int materiasDesactivadas = 0;
                int materiasReprobadas = 0;
                int materiasExtraordinario = 0;

                foreach (var r in resultados)
                {
                    try
                    {
                        var cuatrimestre = r.CuatrimestreMateria;
                        bool esExtraordinario = r.Estado == "Extraordinario";

                        if (r.MateriaEstaActiva)
                            materiasActivas++;
                        else
                            materiasDesactivadas++;

                        if (esExtraordinario)
                            materiasExtraordinario++;
                        else
                            materiasReprobadas++;

                        var item = new ArrastreGrupoDto
                        {
                            IdPersona = r.IdPersona,
                            IdMateria = r.IdMateria,
                            Matricula = r.Matricula ?? "",
                            NombreAlumno = r.NombreAlumno ?? "Sin nombre",
                            GradoGrupo = string.Format("{0} - {1}", r.NombreGrado, r.NombreGrupo),
                            MateriaArrastre = r.MateriaArrastre ?? "Sin materia",
                            CuatrimestreMateria = cuatrimestre,
                            CuatrimestreTexto = ConvertirCuatrimestreTexto(cuatrimestre),
                            IntentosExtraordinarios = r.IntentosExtraordinarios,
                            Observaciones = r.Observaciones ?? "",
                            MateriaEstaActiva = r.MateriaEstaActiva,
                            EstadoMateria = r.EstadoMateria ?? "Materia Activa",

                            // ✅ NUEVO: TIPO DE PROBLEMA
                            TipoProblema = esExtraordinario ? "Extraordinario" : "Arrastre",
                            EsExtraordinario = esExtraordinario,
                        };

                        // ✅ CONFIGURACIÓN DIFERENCIADA SEGÚN TIPO
                        if (esExtraordinario)
                        {
                            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                            // EXTRAORDINARIO: No cuenta días, solo periodo actual
                            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                            item.FechaInicioArrastre = r.FechaExamenExtraordinario;
                            item.FechaExamenExtraordinario = r.FechaExamenExtraordinario;
                            item.PeriodoExtraordinario = "Cuatrimestre en curso para presentar examen";

                            // No aplican métricas de tiempo para extraordinario
                            item.DiasEnArrastre = 0;
                            item.FechaLimiteArrastre = null;
                            item.DiasRestantes = 0;
                            item.EstadoTiempo = "Extraordinario";

                            // Criticidad baja para extraordinario (se resuelve pronto)
                            item.NivelCriticidad = 5;
                            item.ClasificacionVisual = "info";
                            item.DescripcionCriticidad = "Extraordinario - Recuperable en cuatrimestre actual";
                            item.OrdenPrioridad = 10; // Menor prioridad que arrastre
                        }
                        else
                        {
                            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                            // ARRASTRE: Cuenta días y tiene límite de 8 meses
                            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                            item.FechaInicioArrastre = r.FechaInicioArrastre;
                            item.FechaExamenExtraordinario = null;
                            item.PeriodoExtraordinario = null;

                            // Cálculos de tiempo (8 meses límite)
                            if (r.FechaInicioArrastre.HasValue)
                            {
                                item.DiasEnArrastre = (DateTime.Now - r.FechaInicioArrastre.Value).Days;
                                item.FechaLimiteArrastre = r.FechaInicioArrastre.Value.AddMonths(8);

                                item.DiasRestantes = (item.FechaLimiteArrastre.Value.Date - DateTime.Today).Days;

                                item.EstadoTiempo = CalcularEstadoTiempo(r.FechaInicioArrastre);
                            }
                            else
                            {
                                item.DiasEnArrastre = 0;
                                item.FechaLimiteArrastre = null;
                                item.DiasRestantes = 0;
                                item.EstadoTiempo = "Sin fecha";
                            }

                            // Criticidad según cuatrimestre
                            item.NivelCriticidad = CalcularNivelCriticidad(cuatrimestre);
                            item.ClasificacionVisual = CalcularClasificacionVisual(cuatrimestre);
                            item.DescripcionCriticidad = CalcularDescripcionCriticidad(cuatrimestre);
                            item.OrdenPrioridad = item.NivelCriticidad; // Mayor prioridad que extraordinario
                        }

                        listaArrastre.Add(item);
                    }
                    catch (Exception exItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error procesando item: {exItem.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Items procesados: {listaArrastre.Count}");
                System.Diagnostics.Debug.WriteLine($"📊 Activas: {materiasActivas}, Desactivadas: {materiasDesactivadas}");
                System.Diagnostics.Debug.WriteLine($"📊 Reprobadas (Arrastre): {materiasReprobadas}, Extraordinario: {materiasExtraordinario}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR en CargarDatosArrastre: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack: {ex.StackTrace}");
            }

            return listaArrastre;
        }

        // Métodos auxiliares sin cambios
        private ResumenArrastreDto CalcularResumenArrastre(TutoriaGrupal grupo)
        {
            try
            {
                var query = @"
        SELECT 
            COUNT(DISTINCT dp.IdPersona) as TotalAlumnosConArrastre,
            COUNT(*) as TotalMateriasEnArrastre,
            
            -- ✅ SEPARACIÓN POR TIPO
            COUNT(CASE WHEN ma.Estado = 'Reprobada' THEN 1 END) as TotalMateriasArrastre,
            COUNT(CASE WHEN ma.Estado = 'Extraordinario' THEN 1 END) as TotalMateriasExtraordinario,
            
            COUNT(CASE WHEN m.Activo = 1 THEN 1 END) as MateriasActivasEnArrastre,
            COUNT(CASE WHEN m.Activo = 0 THEN 1 END) as MateriasDesactivadasEnArrastre,
            
            -- ✅ POR CUATRIMESTRE (SOLO ARRASTRE)
            COUNT(CASE WHEN ma.Estado = 'Reprobada' AND m.IdGrado = 1 THEN 1 END) as MateriasCriticas_1er,
            COUNT(CASE WHEN ma.Estado = 'Reprobada' AND m.IdGrado = 2 THEN 1 END) as MateriasAltas_2do,
            COUNT(CASE WHEN ma.Estado = 'Reprobada' AND m.IdGrado = 3 THEN 1 END) as MateriasMedias_3er,
            COUNT(CASE WHEN ma.Estado = 'Reprobada' AND m.IdGrado >= 4 THEN 1 END) as MateriasRecientes_4to_mas,
            
            -- ✅ POR TIEMPO (SOLO ARRASTRE - 8 MESES)
            COUNT(CASE 
                WHEN ma.Estado = 'Reprobada' 
                AND DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 0 
                THEN 1 END) as MateriasFueraDeTiempo,
                
            COUNT(CASE 
                WHEN ma.Estado = 'Reprobada' 
                AND DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) BETWEEN 1 AND 60 
                THEN 1 END) as MateriasCriticasTiempo,
                
            COUNT(CASE 
                WHEN ma.Estado = 'Reprobada' 
                AND DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) BETWEEN 61 AND 180 
                THEN 1 END) as MateriasEnRiesgo,
            
            ISNULL(AVG(CAST(ma.IntentosExtraordinarios AS FLOAT)), 0) as PromedioIntentos,
            ISNULL(AVG(CAST(
                CASE 
                    WHEN ma.Estado = 'Reprobada' AND ma.FechaInicioArrastre IS NOT NULL 
                    THEN DATEDIFF(DAY, ma.FechaInicioArrastre, GETDATE())
                    ELSE 0
                END AS FLOAT)), 0) as PromedioDiasEnArrastre
        FROM DatosPersonales dp
        INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
        INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
        LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
        WHERE (ma.Estado = 'Reprobada' OR ma.Estado = 'Extraordinario')
          AND dp.IdCarrera = @p0
          AND dp.IdGrado = @p1
          AND dp.IdGrupo = @p2
          AND m.IdCarrera = dp.IdCarrera";

                var resultado = db.Database.SqlQuery<ResumenArrastreDto>(query,
                    grupo.IdCarrera, grupo.IdGrado, grupo.IdGrupo).FirstOrDefault();

                return resultado ?? new ResumenArrastreDto();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CalcularResumenArrastre: {ex.Message}");
                return new ResumenArrastreDto();
            }
        }


        private void EstablecerInfoGrupo(TutoriaGrupal grupo)
        {
            try
            {
                ViewBag.NombreCarrera = grupo.Carrera?.Nombre ?? "Sin Carrera";
                ViewBag.NombreGrupo = grupo.Grupo?.Nombre ?? "Sin Grupo";
                ViewBag.NombreGrado = grupo.Grado?.Nombre ?? "Sin Grado";
                ViewBag.NombreTurno = grupo.Turno?.Nombre ?? "Sin Turno";
                ViewBag.NombrePeriodo = grupo.Periodo?.Nombre ?? "Sin Periodo";
                ViewBag.IdGrupo = grupo.IdGrupo;
                ViewBag.IdCarrera = grupo.IdCarrera;
                ViewBag.IdGrado = grupo.IdGrado;
                ViewBag.IdTurno = grupo.IdTurno;
                ViewBag.IdPeriodo = grupo.IdPeriodo;
                ViewBag.Año = grupo.Año;

                try
                {
                    // Trata de obtener la especialidad por la relación del grupo, si existe
                    var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == grupo.IdEspecialidad);
                    ViewBag.NombreEspecialidad = especialidad?.Nombre ?? "";
                }
                catch
                {
                    ViewBag.NombreEspecialidad = "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en EstablecerInfoGrupo: {ex.Message}");
                ViewBag.NombreCarrera = "Error";
                ViewBag.NombreGrupo = "Error";
                ViewBag.NombreGrado = "Error";
                ViewBag.NombreEspecialidad = "";
            }
        }

        private string ConvertirCuatrimestreTexto(int cuatrimestre)
        {
            switch (cuatrimestre)
            {
                case 1: return "1er Cuatrimestre";
                case 2: return "2do Cuatrimestre";
                case 3: return "3er Cuatrimestre";
                case 4: return "4to Cuatrimestre";
                case 5: return "5to Cuatrimestre";
                case 6: return "6to Cuatrimestre";
                case 7: return "7mo Cuatrimestre";
                case 8: return "8vo Cuatrimestre";
                case 9: return "9no Cuatrimestre";
                default: return cuatrimestre + " Cuatrimestre";
            }
        }

        private int CalcularNivelCriticidad(int cuatrimestre)
        {
            if (cuatrimestre == 1) return 1;
            if (cuatrimestre == 2) return 2;
            if (cuatrimestre == 3) return 3;
            if (cuatrimestre == 4) return 4;
            return 5;
        }

        private string CalcularClasificacionVisual(int cuatrimestre)
        {
            if (cuatrimestre <= 2) return "danger";
            if (cuatrimestre <= 4) return "warning";
            return "info";
        }

        private string CalcularDescripcionCriticidad(int cuatrimestre)
        {
            switch (cuatrimestre)
            {
                case 1: return "CRÍTICO - Materia de 1er cuatrimestre";
                case 2: return "ALTO - Materia de 2do cuatrimestre";
                case 3: return "MEDIO - Materia de 3er cuatrimestre";
                case 4: return "MEDIO - Materia de 4to cuatrimestre";
                default: return "BAJO - Materia reciente";
            }
        }

        private string CalcularEstadoTiempo(DateTime? fechaInicio)
        {
            if (!fechaInicio.HasValue) return "Sin fecha";

            var fechaLimite = fechaInicio.Value.AddMonths(8);
            var diasRestantes = (fechaLimite - DateTime.Now).Days;

            if (diasRestantes <= 0) return "Fuera de tiempo";      // Ya venció
            if (diasRestantes <= 60) return "Crítico";             // 2 meses o menos
            if (diasRestantes <= 180) return "Medio";              // 6 meses o menos
            return "En tiempo";                                     // 7-8 meses
        }

        private bool ValidarAccesoUsuario(Usuario usuario)
        {
            if (usuario == null) return false;
            // Permitir Nivel 2 (Tutor), 3 (Coordinador) y 4 (Master)
            return usuario.IdNivel >= 2 && usuario.IdNivel <= 4;
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

    public class ArrastreRawResultDto
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string MateriaArrastre { get; set; }
        public int CuatrimestreMateria { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public DateTime? FechaExamenExtraordinario { get; set; }
        public string Observaciones { get; set; }
        public string NombreGrado { get; set; }
        public string NombreGrupo { get; set; }
        public string Estado { get; set; }
        public bool MateriaEstaActiva { get; set; }
        public string EstadoMateria { get; set; }
    }
}