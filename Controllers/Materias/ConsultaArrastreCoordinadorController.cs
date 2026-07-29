using OfficeOpenXml;
using OfficeOpenXml.Style;
using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models.Materias;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.Mvc;
using System.Text;

namespace PlataformaWeb.Controllers.Materias
{
    [CustomAuthorize(Nivel = 3)] // Coordinadores (nivel 3) y Master (nivel 4)
    public class ConsultaArrastreCoordinadorController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: ConsultaArrastreCoordinador/ArrastrePorCarrera
        public ActionResult ArrastrePorCarrera(int? idCarrera, bool? soloCriticos = false, int? idEspecialidad = null, int? idGrado = null, bool? soloExtraordinarios = false, bool? incluirBajas = false)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // ✅ VALIDAR ACCESO (COORDINADORES Y MASTER)
            if (!ValidarAccesoCoordinador(usuario))
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección. Solo coordinadores y administradores.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // CARGAR DROPDOWN PRIMERO
                CargarDropdownCarreras(usuario, idCarrera);

                // ✅ SANITIZAR idCarrera SEGÚN ROL ANTES DE CUALQUIER CHEQUEO
                if (usuario.IdNivel == 4)
                {
                    // Master: si no especifica carrera o es 0, mostrar vista global
                    if (idCarrera == null || idCarrera == 0)
                    {
                        idCarrera = 0;
                        System.Diagnostics.Debug.WriteLine($"🔑 MASTER sin carrera especificada, usando 0 (Todas)");
                    }
                }
                else
                {
                    // Coordinador (u otro rol): SIEMPRE forzar a su carrera asignada
                    // Esto previene idCarrera=0 por manipulación de URL, bookmarks antiguos o edge cases de JS
                    idCarrera = usuario.IdCarrera;
                    System.Diagnostics.Debug.WriteLine($"📋 COORDINADOR forzado a su carrera: {usuario.IdCarrera}");
                }

                // VALIDAR ACCESO A LA CARRERA
                if (!UsuarioTieneAccesoCarrera(usuario, idCarrera.Value))
                {
                    // ✅ Coordinadores: redirigir silenciosamente a su carrera (defensa en profundidad)
                    if (usuario.IdNivel == 3)
                    {
                        return RedirectToAction("ArrastrePorCarrera", new { idCarrera = usuario.IdCarrera });
                    }

                    // Otros roles: error genérico sin exponer "VISTA GLOBAL"
                    ViewBag.Error = "No tienes acceso a la carrera solicitada.";
                    ViewBag.HayDatos = false;
                    ViewBag.NombreCarrera = "Acceso Denegado";
                    ViewBag.IdCarrera = usuario.IdCarrera;
                    ViewBag.SoloCriticos = soloCriticos ?? false;
                    CargarDropdownCarreras(usuario, usuario.IdCarrera);
                    CargarFiltrosAdicionales(usuario.IdCarrera);
                    return View(new List<ArrastreCarreraDto>());
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Usuario: {usuario.NombreCompleto} (Nivel {usuario.IdNivel})");
                System.Diagnostics.Debug.WriteLine($"🔍 Consultando carrera: {idCarrera}, SoloCriticos={soloCriticos}");

                // CARGAR DATOS USANDO MÉTODO MEJORADO
                var datosArrastre = CargarDatosArrastreCarreraMejorado(idCarrera.Value, soloCriticos ?? false, idEspecialidad, idGrado, soloExtraordinarios ?? false, incluirBajas ?? false);
                var resumenArrastre = CalcularResumenArrastreCarrera(idCarrera.Value, soloCriticos ?? false);

                // ESTABLECER VIEWBAG
                EstablecerInfoCarrera(idCarrera.Value, usuario);
                ViewBag.ResumenArrastre = resumenArrastre;
                ViewBag.IdCarrera = idCarrera;
                ViewBag.SoloCriticos = soloCriticos ?? false;
                ViewBag.IdEspecialidad = idEspecialidad;
                ViewBag.IdGrado = idGrado;
                ViewBag.SoloExtraordinarios = soloExtraordinarios ?? false;
                ViewBag.IncluirBajas = incluirBajas ?? false;
                ViewBag.HayDatos = datosArrastre.Count > 0;

                // INFO DE DEBUG MEJORADA
                System.Diagnostics.Debug.WriteLine($"✅ RESULTADO - Datos encontrados: {datosArrastre.Count}");
                if (datosArrastre.Count > 0)
                {
                    var muestra = datosArrastre.Take(3);
                    foreach (var item in muestra)
                    {
                        System.Diagnostics.Debug.WriteLine($"   - {item.Matricula}: {item.NombreAlumno} | {item.MateriaArrastre} | Especialidad: {item.NombreEspecialidad}");
                    }
                }

                CargarFiltrosAdicionales(idCarrera.Value);

                return View(datosArrastre);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR EN CONTROLADOR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ STACK TRACE: {ex.StackTrace}");

                // ASEGURAR DROPDOWN SIEMPRE DISPONIBLE
                try
                {
                    CargarDropdownCarreras(usuario, idCarrera);
                    CargarFiltrosAdicionales(idCarrera ?? usuario.IdCarrera);
                }
                catch
                {
                    ViewBag.CarrerasDropdown = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "", Text = "Error al cargar carreras" }
                    };
                }

                ViewBag.Error = "Error al cargar los datos: " + ex.Message;
                ViewBag.HayDatos = false;
                ViewBag.NombreCarrera = "Error";
                ViewBag.IdCarrera = idCarrera ?? usuario.IdCarrera;
                ViewBag.SoloCriticos = soloCriticos ?? false;

                return View(new List<ArrastreCarreraDto>());
            }
        }

        // MÉTODO QUE MAPEA TODOS LOS CAMPOS
        private List<ArrastreCarreraDto> CargarDatosArrastreCarreraMejorado(int idCarrera, bool soloCriticos = false, int? idEspecialidad = null, int? idGrado = null, bool soloExtraordinarios = false, bool incluirBajas = false)
        {
            var listaArrastre = new List<ArrastreCarreraDto>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔧 CargarDatosArrastreCarreraMejorado INICIADO");
                System.Diagnostics.Debug.WriteLine($"🔧 Parámetros: IdCarrera={idCarrera}, SoloCriticos={soloCriticos}, IdEspecialidad={idEspecialidad}, IdGrado={idGrado}");

                // VERIFICAR SI LA VISTA EXISTE
                var vistaExiste = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM sys.views WHERE name = 'vw_ArrastreCarreraCoordinador'").FirstOrDefault();
                if (vistaExiste == 0)
                {
                    System.Diagnostics.Debug.WriteLine("❌ ERROR: La vista vw_ArrastreCarreraCoordinador no existe");
                    // FALLBACK: usar consulta directa
                    return CargarDatosDirectos(idCarrera, soloCriticos, idEspecialidad, idGrado, soloExtraordinarios, incluirBajas);
                }

                // Calcular periodo academico actual de forma dinamica
                int anioActual = DateTime.Now.Year;
                int mesActual = DateTime.Now.Month;
                int periodoActual = (mesActual >= 1 && mesActual <= 4) ? 1
                                  : (mesActual >= 5 && mesActual <= 8) ? 2 : 3;

                // CONSTRUIR CONSULTA DINÁMICAMENTE
                var consulta = new StringBuilder();
                consulta.AppendLine("SELECT * FROM vw_ArrastreCarreraCoordinador");

                var parametros = new List<object>();
                int paramIndex = 0;

                if (idCarrera > 0)
                {
                    consulta.AppendLine($"WHERE IdCarrera = @p{paramIndex}");
                    parametros.Add(idCarrera);
                    paramIndex++;
                }
                else
                {
                    consulta.AppendLine("WHERE 1=1"); // Para encadenar ANDs
                }

                // AGREGAR FILTROS OPCIONALES
                if (idEspecialidad.HasValue && idEspecialidad.Value > 0)
                {
                    consulta.AppendLine($"  AND IdEspecialidad = @p{paramIndex}");
                    parametros.Add(idEspecialidad.Value);
                    paramIndex++;
                }

                if (idGrado.HasValue && idGrado.Value > 0)
                {
                    consulta.AppendLine($"  AND IdGrado = @p{paramIndex}");
                    parametros.Add(idGrado.Value);
                    paramIndex++;
                }

                if (soloCriticos)
                {
                    consulta.AppendLine("  AND CuatrimestreMateria <= 2"); // Solo 1° y 2° cuatrimestre
                }

                if (soloExtraordinarios)
                {
                    consulta.AppendLine("  AND EstadoMateriaAlumno = 'Extraordinario'");
                    consulta.AppendLine("  AND MateriaEstaActiva = 1");
                    consulta.AppendLine("  AND CuatrimestreMateria <= IdGrado");
                    consulta.AppendLine($"  AND Año = @p{paramIndex}");
                    parametros.Add(anioActual);
                    paramIndex++;
                    consulta.AppendLine($"  AND IdPeriodo = @p{paramIndex}");
                    parametros.Add(periodoActual);
                    paramIndex++;
                }
                else
                {
                    // ARRASTRE ABSOLUTO: 
                    // 1. Es de un cuatrimestre anterior AL ACTUAL
                    // 2. O BIEN, es del cuatrimestre actual pero YA esta en fase Arrastre (Intentos CONSUMIDOS >= 1)
                    consulta.AppendLine("  AND EstadoMateriaAlumno IN ('Extraordinario', 'Reprobada')");
                    consulta.AppendLine("  AND (CuatrimestreMateria < IdGrado OR (CuatrimestreMateria = IdGrado AND IntentosExtraordinarios >= 1))");
                }

                if (!incluirBajas)
                {
                    consulta.AppendLine("  AND EstadoAlumno = 1");
                }

                consulta.AppendLine("ORDER BY CuatrimestreMateria ASC, NivelCriticidad ASC, NombreAlumno ASC");

                var consultaFinal = consulta.ToString();
                System.Diagnostics.Debug.WriteLine($"🔧 CONSULTA CONSTRUIDA:\n{consultaFinal}");

                // EJECUTAR CONSULTA CON MANEJO DE ERRORES
                try
                {
                    var resultadosVista = db.Database.SqlQuery<ArrastreVistaResultado>(consultaFinal, parametros.ToArray()).ToList();
                    System.Diagnostics.Debug.WriteLine($"✅ RESULTADOS OBTENIDOS DE LA VISTA: {resultadosVista.Count}");

                    // MAPEAR RESULTADOS CORRECTAMENTE
                    foreach (var resultado in resultadosVista)
                    {
                        try
                        {
                            var dto = MapearVistaADto(resultado);
                            listaArrastre.Add(dto);
                        }
                        catch (Exception exMapeo)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Error mapeando item: {exMapeo.Message}");
                        }
                    }
                }
                catch (Exception exVista)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error ejecutando vista, usando fallback: {exVista.Message}");
                    return CargarDatosDirectos(idCarrera, soloCriticos, idEspecialidad, idGrado, soloExtraordinarios, incluirBajas);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Items procesados correctamente: {listaArrastre.Count}");
                return listaArrastre;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR en CargarDatosArrastreCarreraMejorado: {ex.Message}");
                // FALLBACK FINAL
                return CargarDatosDirectos(idCarrera, soloCriticos, idEspecialidad, idGrado, soloExtraordinarios, incluirBajas);
            }
        }

        // MÉTODO DE FALLBACK PARA CARGAR DATOS DIRECTAMENTE
        private List<ArrastreCarreraDto> CargarDatosDirectos(int idCarrera, bool soloCriticos = false, int? idEspecialidad = null, int? idGrado = null, bool soloExtraordinarios = false, bool incluirBajas = false)
        {
            System.Diagnostics.Debug.WriteLine("🔄 Usando consulta directa como fallback");

            var listaArrastre = new List<ArrastreCarreraDto>();

            try
            {
                var consulta = @"
        SELECT 
            dp.IdPersona,
            ma.IdMateria,
            dp.Matricula,
            dp.Nombre as NombreAlumno,
            c.Nombre as NombreCarrera,
            ISNULL(e.Nombre, 'Sin especialidad') as NombreEspecialidad,
            CONCAT(gr.Nombre, ' - ', g.Nombre) as GradoGrupo,
            m.Nombre as MateriaArrastre,
            m.IdGrado as CuatrimestreMateria,
            
            -- ⭐ TEXTO DEL CUATRIMESTRE CORREGIDO (solo número)
            CASE m.IdGrado 
                WHEN 1 THEN '1°'
                WHEN 2 THEN '2°' 
                WHEN 3 THEN '3°'
                WHEN 4 THEN '4°'
                WHEN 5 THEN '5°'
                WHEN 6 THEN '6°'
                WHEN 7 THEN '7°'
                WHEN 8 THEN '8°'
                WHEN 9 THEN '9°'
                ELSE CAST(m.IdGrado AS VARCHAR) + '°'
            END as CuatrimestreTexto,
            
            ma.IntentosExtraordinarios,
            ma.FechaInicioArrastre,
            ma.Observaciones,
            t.Nombre as NombreTurno,
            p.Nombre as NombrePeriodo,
            dp.Año,
            ISNULL(u.NombreCompleto, 'Sin tutor') as TutorAsignado,
            dp.IdGrupo, 
            dp.IdCarrera, 
            dp.IdGrado, 
            dp.IdTurno, 
            dp.IdPeriodo,
            ISNULL(e.Id, 0) as IdEspecialidad,
            m.Activo as MateriaEstaActiva,
            CASE WHEN m.Activo = 1 THEN 'Materia Activa' ELSE 'Materia Desactivada' END as EstadoMateria,
            
            -- ⭐ NIVEL DE CRITICIDAD BASADO EN TIEMPO RESTANTE (CORREGIDO)
            CASE 
                WHEN ma.FechaInicioArrastre IS NULL THEN 4
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 0 THEN 1  -- Fuera de tiempo
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 60 THEN 2  -- Crítico (≤2 meses)
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 180 THEN 3  -- Medio (≤6 meses)
                ELSE 4  -- En tiempo
            END as NivelCriticidad,
            
            -- ⭐ CLASIFICACIÓN VISUAL (CORREGIDA)
            CASE 
                WHEN ma.FechaInicioArrastre IS NULL THEN 'info'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 0 THEN 'danger'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 60 THEN 'danger'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 180 THEN 'warning'
                ELSE 'success'
            END as ClasificacionVisual,
            
            -- ⭐ DESCRIPCIÓN DE CRITICIDAD (CORREGIDA)
            CASE 
                WHEN ma.FechaInicioArrastre IS NULL THEN 'Sin fecha'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 0 THEN 'FUERA DE TIEMPO'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 60 THEN 'CRÍTICO'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 180 THEN 'MEDIO'
                ELSE 'EN TIEMPO'
            END as DescripcionCriticidad,
            
            ISNULL(DATEDIFF(DAY, ma.FechaInicioArrastre, GETDATE()), 0) as DiasEnArrastre,
            
            CASE 
                WHEN ma.FechaInicioArrastre IS NOT NULL 
                THEN DATEADD(MONTH, 8, ma.FechaInicioArrastre)
                ELSE NULL
            END as FechaLimiteArrastre,
            
            CASE 
                WHEN ma.FechaInicioArrastre IS NOT NULL 
                THEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre))
                ELSE 999
            END as DiasRestantes,
            
            -- ⭐ ESTADO TIEMPO (CORREGIDO - 4 estados específicos)
            CASE 
                WHEN ma.FechaInicioArrastre IS NULL THEN 'Sin fecha'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 0 THEN 'Fuera de Tiempo'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 60 THEN 'Crítico'
                WHEN DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 180 THEN 'Medio'
                ELSE 'En Tiempo'
            END as EstadoTiempo,
            
            -- Deteccion real de baja: consulta BajasAlumnos (dp.Estado es flag de entrevista, NO de baja)
            CASE WHEN EXISTS(
                SELECT 1 FROM BajasAlumnos ba 
                WHERE ba.IdPersona = dp.IdPersona 
                AND (ba.Activo = 1 OR ba.Activo IS NULL) 
                AND (ba.Reingreso = 0 OR ba.Reingreso IS NULL)
            ) THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END as EstadoAlumno,
            ma.Estado as EstadoMateriaAlumno
            
        FROM DatosPersonales dp
        INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona
        INNER JOIN Materias m ON ma.IdMateria = m.IdMateria
        INNER JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
        LEFT JOIN Especialidads e ON m.IdEspecialidad = e.Id
        LEFT JOIN Grupoes g ON dp.IdGrupo = g.IdGrupo
        LEFT JOIN Gradoes gr ON dp.IdGrado = gr.IdGrado
        LEFT JOIN Turnoes t ON dp.IdTurno = t.IdTurno
        LEFT JOIN Periodoes p ON dp.IdPeriodo = p.IdPeriodo
        LEFT JOIN TutoriaGrupals tg ON (
            dp.IdGrupo = tg.IdGrupo AND 
            dp.IdCarrera = tg.IdCarrera AND
            dp.IdGrado = tg.IdGrado AND 
            dp.IdTurno = tg.IdTurno AND
            dp.IdPeriodo = tg.IdPeriodo AND 
            dp.Año = tg.Año
        )
        LEFT JOIN Usuarios u ON tg.IdUsuario = u.IdUsuario
        WHERE 1=1
          -- Filtro de carrera para evitar arrastres de carreras anteriores.
          -- Sin filtro de especialidad: se muestran todos los registros para que
          -- el usuario pueda identificar y eliminar duplicados manualmente.
          AND m.IdCarrera = dp.IdCarrera";

                var parametros = new List<object>();
                int paramIndex = 0;

                if (idCarrera > 0)
                {
                    consulta += $" AND dp.IdCarrera = @p{paramIndex}";
                    parametros.Add(idCarrera);
                    paramIndex++;
                }

                if (idEspecialidad.HasValue && idEspecialidad.Value > 0)
                {
                    consulta += $" AND e.Id = @p{paramIndex}";
                    parametros.Add(idEspecialidad.Value);
                    paramIndex++;
                }

                // Calcular periodo academico actual de forma dinamica
                int anioActual = DateTime.Now.Year;
                int mesActual = DateTime.Now.Month;
                int periodoActual = (mesActual >= 1 && mesActual <= 4) ? 1
                                  : (mesActual >= 5 && mesActual <= 8) ? 2 : 3;

                if (soloExtraordinarios)
                {
                    consulta += $" AND ma.Estado = 'Extraordinario' AND m.Activo = 1 AND m.IdGrado <= dp.IdGrado AND dp.Año = @p{paramIndex} AND dp.IdPeriodo = @p{paramIndex + 1}";
                    parametros.Add(anioActual);
                    paramIndex++;
                    parametros.Add(periodoActual);
                    paramIndex++;
                }
                else
                {
                    consulta += " AND ma.Estado IN ('Extraordinario', 'Reprobada') AND (m.IdGrado < dp.IdGrado OR (m.IdGrado = dp.IdGrado AND ma.IntentosExtraordinarios >= 1))";
                }

                if (!incluirBajas)
                {
                    // Deteccion real de baja: consulta BajasAlumnos (dp.Estado es flag de entrevista, NO de baja)
                    consulta += " AND NOT EXISTS(SELECT 1 FROM BajasAlumnos ba WHERE ba.IdPersona = dp.IdPersona AND (ba.Activo = 1 OR ba.Activo IS NULL) AND (ba.Reingreso = 0 OR ba.Reingreso IS NULL))";
                }

                if (idGrado.HasValue && idGrado.Value > 0)
                {
                    consulta += $" AND dp.IdGrado = @p{paramIndex}";
                    parametros.Add(idGrado.Value);
                    paramIndex++;
                }

                // ⭐ FILTRO SOLO CRÍTICOS ACTUALIZADO (basado en tiempo, no en cuatrimestre)
                if (soloCriticos)
                {
                    // Solo materias con NivelCriticidad 1 o 2 (Fuera de tiempo o Crítico)
                    consulta += @" AND (
                ma.FechaInicioArrastre IS NULL OR 
                DATEDIFF(DAY, GETDATE(), DATEADD(MONTH, 8, ma.FechaInicioArrastre)) <= 60
            )";
                }

                // Ordenar por nivel de criticidad (tiempo)
                consulta += " ORDER BY m.IdGrado ASC, NivelCriticidad ASC, dp.Nombre ASC";

                var resultados = db.Database.SqlQuery<ArrastreDirectoResultado>(consulta, parametros.ToArray()).ToList();

                foreach (var resultado in resultados)
                {
                    var dto = MapearDirectoADto(resultado);
                    listaArrastre.Add(dto);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Consulta directa completada: {listaArrastre.Count} registros");

                return listaArrastre;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en consulta directa: {ex.Message}");
                return new List<ArrastreCarreraDto>();
            }
        }

        // MÉTODO PARA MAPEAR DESDE LA VISTA
        private ArrastreCarreraDto MapearVistaADto(ArrastreVistaResultado resultado)
        {
            return new ArrastreCarreraDto
            {
                IdPersona = resultado.IdPersona,
                IdMateria = resultado.IdMateria,
                Matricula = resultado.Matricula ?? "",
                NombreAlumno = resultado.NombreAlumno ?? "Sin nombre",
                NombreCarrera = resultado.NombreCarrera ?? "Sin carrera",
                NombreEspecialidad = resultado.NombreEspecialidad ?? "Sin especialidad",
                GradoGrupo = resultado.GradoGrupo ?? "Sin grupo",
                MateriaArrastre = resultado.MateriaArrastre ?? "Sin materia",
                CuatrimestreMateria = resultado.CuatrimestreMateria,
                CuatrimestreTexto = resultado.CuatrimestreTexto ?? "",
                IntentosExtraordinarios = resultado.IntentosExtraordinarios,
                FechaInicioArrastre = resultado.FechaInicioArrastre,
                Observaciones = resultado.Observaciones ?? "",
                NombreTurno = resultado.NombreTurno ?? "Sin turno",
                NombrePeriodo = resultado.NombrePeriodo ?? "Sin periodo",
                Año = resultado.Año,
                TutorAsignado = resultado.TutorAsignado ?? "Sin tutor",
                NivelCriticidad = resultado.NivelCriticidad,
                ClasificacionVisual = resultado.ClasificacionVisual ?? "info",
                DescripcionCriticidad = resultado.DescripcionCriticidad ?? "",
                DiasEnArrastre = resultado.DiasEnArrastre,
                FechaLimiteArrastre = resultado.FechaLimiteArrastre,
                DiasRestantes = resultado.DiasRestantes,
                EstadoTiempo = resultado.EstadoTiempo ?? "Sin fecha",
                OrdenPrioridad = (int)resultado.OrdenPrioridad,
                IdCarrera = resultado.IdCarrera,
                IdEspecialidad = resultado.IdEspecialidad,
                IdGrado = resultado.IdGrado,
                IdGrupo = resultado.IdGrupo,
                IdTurno = resultado.IdTurno,
                IdPeriodo = resultado.IdPeriodo,

                MateriaEstaActiva = resultado.MateriaEstaActiva,
                EstadoMateria = resultado.EstadoMateria ?? "Desconocido",
                EstadoAlumno = resultado.EstadoAlumno,
                EstadoMateriaAlumno = resultado.EstadoMateriaAlumno ?? "Desconocido"
            };
        }

        // MÉTODO PARA MAPEAR DESDE CONSULTA DIRECTA
        private ArrastreCarreraDto MapearDirectoADto(ArrastreDirectoResultado resultado)
        {
            return new ArrastreCarreraDto
            {
                IdPersona = resultado.IdPersona,
                IdMateria = resultado.IdMateria,
                Matricula = resultado.Matricula ?? "",
                NombreAlumno = resultado.NombreAlumno ?? "Sin nombre",
                NombreCarrera = resultado.NombreCarrera ?? "Sin carrera",
                NombreEspecialidad = resultado.NombreEspecialidad ?? "Sin especialidad",
                GradoGrupo = resultado.GradoGrupo ?? "Sin grupo",
                MateriaArrastre = resultado.MateriaArrastre ?? "Sin materia",
                CuatrimestreMateria = resultado.CuatrimestreMateria,
                CuatrimestreTexto = resultado.CuatrimestreTexto ?? "",
                IntentosExtraordinarios = resultado.IntentosExtraordinarios,
                FechaInicioArrastre = resultado.FechaInicioArrastre,
                Observaciones = resultado.Observaciones ?? "",
                NombreTurno = resultado.NombreTurno ?? "Sin turno",
                NombrePeriodo = resultado.NombrePeriodo ?? "Sin periodo",
                Año = resultado.Año,
                TutorAsignado = resultado.TutorAsignado ?? "Sin tutor",
                NivelCriticidad = resultado.NivelCriticidad,
                ClasificacionVisual = resultado.ClasificacionVisual ?? "info",
                DescripcionCriticidad = resultado.DescripcionCriticidad ?? "",
                DiasEnArrastre = resultado.DiasEnArrastre,
                FechaLimiteArrastre = resultado.FechaLimiteArrastre,
                DiasRestantes = resultado.DiasRestantes,
                EstadoTiempo = resultado.EstadoTiempo ?? "Sin fecha",
                OrdenPrioridad = 0,
                IdCarrera = resultado.IdCarrera,
                IdEspecialidad = resultado.IdEspecialidad,
                IdGrado = resultado.IdGrado,
                IdGrupo = resultado.IdGrupo,
                IdTurno = resultado.IdTurno,
                IdPeriodo = resultado.IdPeriodo,

                MateriaEstaActiva = resultado.MateriaEstaActiva,
                EstadoMateria = resultado.EstadoMateria ?? "Desconocido",
                EstadoAlumno = resultado.EstadoAlumno,
                EstadoMateriaAlumno = resultado.EstadoMateriaAlumno ?? "Desconocido"
            };
        }

        // GET: Exportar Excel por Carrera - MEJORADO
        // MÉTODO CORREGIDO: ExportarArrastreCarreraExcel
        public ActionResult ExportarArrastreCarreraExcel(int idCarrera, bool soloCriticos = false, int? idEspecialidad = null, int? idGrado = null, bool soloExtraordinarios = false, bool incluirBajas = false)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return RedirectToAction("Login", "Home");
                }

                // ✅ SANITIZAR: Forzar coordinador a su carrera
                if (usuario.IdNivel == 3 && (idCarrera <= 0 || idCarrera != usuario.IdCarrera))
                {
                    idCarrera = usuario.IdCarrera;
                }

                // Validar permisos
                if (!ValidarAccesoCoordinador(usuario) || !UsuarioTieneAccesoCarrera(usuario, idCarrera))
                {
                    return Content("ERROR: No tienes permisos para exportar esta carrera.");
                }

                // USAR EL MÉTODO MEJORADO (igual que la vista)
                var materiasArrastre = CargarDatosArrastreCarreraMejorado(idCarrera, soloCriticos, idEspecialidad, idGrado, soloExtraordinarios, incluirBajas);

                System.Diagnostics.Debug.WriteLine($"📊 Excel - Datos obtenidos: {materiasArrastre.Count}");

                // Obtener información de la carrera
                string nombreCarrera = "Carrera " + idCarrera;
                string nombreEspecialidad = "";

                try
                {
                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == idCarrera);
                    if (carrera != null) nombreCarrera = carrera.Nombre;

                    if (idEspecialidad.HasValue && idEspecialidad.Value > 0)
                    {
                        var especialidad = db.Especialidads.FirstOrDefault(e => e.Id == idEspecialidad.Value);
                        if (especialidad != null) nombreEspecialidad = especialidad.Nombre;
                    }
                }
                catch (Exception infoEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error obteniendo nombres: {infoEx.Message}");
                }

                if (materiasArrastre.Count == 0)
                {
                    return GenerarExcelSinDatos(idCarrera, usuario, soloCriticos);
                }

                // ✅ CREAR EXCEL CON DISEÑO ACTUALIZADO
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Arrastre por Carrera");

                    // TÍTULO PRINCIPAL
                    worksheet.Cells[1, 1].Value = $"REPORTE DE ARRASTRE POR CARRERA - {nombreCarrera.ToUpper()}";
                    worksheet.Cells[1, 1, 1, 10].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 18;
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

                    // INFORMACIÓN DE LA CARRERA
                    string infoCarrera = nombreCarrera.ToUpper();
                    if (!string.IsNullOrEmpty(nombreEspecialidad))
                    {
                        infoCarrera += " - " + nombreEspecialidad.ToUpper();
                    }

                    worksheet.Cells[2, 1].Value = infoCarrera;
                    worksheet.Cells[2, 1, 2, 10].Merge = true;
                    worksheet.Cells[2, 1].Style.Font.Size = 14;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(31, 73, 125));

                    // FECHA Y FILTROS
                    string filtrosTexto = $"GENERADO EL {DateTime.Now:dd/MM/yyyy} A LAS {DateTime.Now:HH:mm:ss} | USUARIO: {usuario.NombreCompleto.ToUpper()}";

                    if (soloCriticos)
                        filtrosTexto += " - SOLO MATERIAS CRÍTICAS (≤60 DÍAS RESTANTES)";
                    if (idEspecialidad.HasValue && idEspecialidad.Value > 0)
                        filtrosTexto += " - ESPECIALIDAD: " + nombreEspecialidad.ToUpper();
                    if (idGrado.HasValue && idGrado.Value > 0)
                        filtrosTexto += " - GRADO: " + idGrado.Value;

                    worksheet.Cells[3, 1].Value = filtrosTexto;
                    worksheet.Cells[3, 1, 3, 10].Merge = true;
                    worksheet.Cells[3, 1].Style.Font.Size = 11;
                    worksheet.Cells[3, 1].Style.Font.Italic = true;
                    worksheet.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[3, 1].Style.Font.Color.SetColor(Color.Gray);

                    // ESTADÍSTICAS
                    int filaStats = 5;
                    var totalAlumnos = materiasArrastre.Select(m => m.IdPersona).Distinct().Count();
                    var totalMaterias = materiasArrastre.Count;

                    var fueraDeTiempo = materiasArrastre.Count(m => m.DiasRestantes <= 0);
                    var criticos = materiasArrastre.Count(m => m.DiasRestantes > 0 && m.DiasRestantes <= 60);
                    var medios = materiasArrastre.Count(m => m.DiasRestantes > 60 && m.DiasRestantes <= 180);
                    var enTiempo = materiasArrastre.Count(m => m.DiasRestantes > 180);

                    worksheet.Cells[filaStats, 1].Value = "RESUMEN ESTADÍSTICO";
                    worksheet.Cells[filaStats, 1, filaStats, 6].Merge = true;
                    worksheet.Cells[filaStats, 1].Style.Font.Bold = true;
                    worksheet.Cells[filaStats, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[filaStats, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 230, 241));
                    worksheet.Cells[filaStats, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    filaStats++;

                    worksheet.Cells[filaStats, 1].Value = "TOTAL ALUMNOS:";
                    worksheet.Cells[filaStats, 2].Value = totalAlumnos;
                    worksheet.Cells[filaStats, 3].Value = "TOTAL MATERIAS:";
                    worksheet.Cells[filaStats, 4].Value = totalMaterias;
                    filaStats++;

                    worksheet.Cells[filaStats, 1].Value = "FUERA TIEMPO:";
                    worksheet.Cells[filaStats, 2].Value = fueraDeTiempo;
                    worksheet.Cells[filaStats, 2].Style.Font.Color.SetColor(Color.Red);
                    worksheet.Cells[filaStats, 3].Value = "CRÍTICOS:";
                    worksheet.Cells[filaStats, 4].Value = criticos;
                    worksheet.Cells[filaStats, 4].Style.Font.Color.SetColor(Color.Orange);
                    filaStats++;

                    worksheet.Cells[filaStats, 1].Value = "MEDIOS:";
                    worksheet.Cells[filaStats, 2].Value = medios;
                    worksheet.Cells[filaStats, 2].Style.Font.Color.SetColor(Color.FromArgb(241, 196, 15));
                    worksheet.Cells[filaStats, 3].Value = "EN TIEMPO:";
                    worksheet.Cells[filaStats, 4].Value = enTiempo;
                    worksheet.Cells[filaStats, 4].Style.Font.Color.SetColor(Color.Green);
                    filaStats += 2;

                    // ENCABEZADOS - TODO EN MAYÚSCULAS
                    string[] encabezados = {
    "MATRÍCULA",
    "NOMBRE DEL ALUMNO",
    "ESPECIALIDAD",
    "GRADO Y GRUPO",
    "MATERIA DE ARRASTRE",
    "CUATRIMESTRE",
    "INTENTOS",
    "ESTADO",
    "DÍAS RESTANTES",
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

                    // DATOS - TODO EN MAYÚSCULAS
                    int fila = filaStats + 1;
                    foreach (var item in materiasArrastre)
                    {
                        Color colorFondo = Color.White;
                        if (item.DiasRestantes <= 0)
                            colorFondo = Color.FromArgb(248, 215, 218);
                        else if (item.DiasRestantes <= 60)
                            colorFondo = Color.FromArgb(255, 243, 205);
                        else if (item.DiasRestantes <= 180)
                            colorFondo = Color.FromArgb(255, 250, 220);
                        else
                            colorFondo = Color.FromArgb(217, 237, 247);

                        worksheet.Cells[fila, 1].Value = item.Matricula?.ToUpper() ?? "";
                        worksheet.Cells[fila, 2].Value = item.NombreAlumno?.ToUpper() ?? "";
                        worksheet.Cells[fila, 3].Value = item.NombreEspecialidad?.ToUpper() ?? "";
                        worksheet.Cells[fila, 4].Value = item.GradoGrupo?.ToUpper() ?? "";
                        worksheet.Cells[fila, 5].Value = item.MateriaArrastre?.ToUpper() ?? "";
                        worksheet.Cells[fila, 6].Value = item.CuatrimestreTexto?.ToUpper() ?? "";
                        worksheet.Cells[fila, 7].Value = item.IntentosExtraordinarios;
                        worksheet.Cells[fila, 8].Value = item.EstadoTiempo?.ToUpper() ?? "";

                        if (item.DiasRestantes > 0)
                        {
                            worksheet.Cells[fila, 9].Value = $"{item.DiasRestantes} DÍAS";
                            if (item.FechaLimiteArrastre.HasValue)
                                worksheet.Cells[fila, 9].Value += $"\nLÍMITE: {item.FechaLimiteArrastre.Value:dd/MM/yyyy}";
                        }
                        else
                        {
                            worksheet.Cells[fila, 9].Value = $"VENCIDO ({Math.Abs(item.DiasRestantes)} DÍAS)";
                            worksheet.Cells[fila, 9].Style.Font.Color.SetColor(Color.Red);
                            worksheet.Cells[fila, 9].Style.Font.Bold = true;
                        }
                        worksheet.Cells[fila, 9].Style.WrapText = true;

                        worksheet.Cells[fila, 10].Value = (item.Observaciones ?? "").ToUpper();
                        worksheet.Cells[fila, 10].Style.WrapText = true;

                        var rangoFila = worksheet.Cells[fila, 1, fila, 10];
                        rangoFila.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rangoFila.Style.Fill.BackgroundColor.SetColor(colorFondo);

                        if (!item.MateriaEstaActiva)
                        {
                            rangoFila.Style.Border.Left.Style = ExcelBorderStyle.Medium;
                            rangoFila.Style.Border.Left.Color.SetColor(Color.Orange);
                            worksheet.Cells[fila, 5].Style.Font.Italic = true;
                            worksheet.Cells[fila, 5].Style.Font.Color.SetColor(Color.FromArgb(133, 100, 4));
                        }

                        fila++;
                    }

                    // AJUSTAR COLUMNAS
                    worksheet.Column(1).Width = 12;
                    worksheet.Column(2).Width = 30;
                    worksheet.Column(3).Width = 20;
                    worksheet.Column(4).Width = 15;
                    worksheet.Column(5).Width = 35;
                    worksheet.Column(6).Width = 12;
                    worksheet.Column(7).Width = 10;
                    worksheet.Column(8).Width = 15;
                    worksheet.Column(9).Width = 18;
                    worksheet.Column(10).Width = 30;

                    // PIE DE PÁGINA
                    fila += 2;
                    worksheet.Cells[fila, 1].Value = $"SISTEMA DE CONTROL ACADÉMICO - GENERADO EL {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Cells[fila, 1, fila, 10].Merge = true;
                    worksheet.Cells[fila, 1].Style.Font.Size = 9;
                    worksheet.Cells[fila, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.View.ShowGridLines = true;
                    worksheet.PrinterSettings.Orientation = eOrientation.Landscape;

                    var excelBytes = package.GetAsByteArray();

                    string tipoReporte = soloCriticos ? "CRITICOS" : "COMPLETO";
                    string nombreArchivo = $"ARRASTRE_CARRERA_{tipoReporte}_{nombreCarrera.Replace(" ", "_").ToUpper()}_C{idCarrera}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                    System.Diagnostics.Debug.WriteLine($"✅ Excel generado exitosamente. Tamaño: {excelBytes.Length} bytes");

                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR EN EXCEL: {ex.Message}");
                return Content($"ERROR: {ex.Message}");
            }
        }

        // MÉTODO DE DIAGNÓSTICO
        public ActionResult DiagnosticarVista(int idCarrera)
        {
            var diagnostico = new StringBuilder();

            try
            {
                diagnostico.AppendLine("=== DIAGNÓSTICO COMPLETO DE VISTA vw_ArrastreCarreraCoordinador ===");
                diagnostico.AppendLine($"Carrera ID: {idCarrera}");
                diagnostico.AppendLine($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                diagnostico.AppendLine();

                // 1. Verificar que la vista existe
                var vistaExiste = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM sys.views WHERE name = 'vw_ArrastreCarreraCoordinador'").FirstOrDefault();
                diagnostico.AppendLine($"1. VISTA EXISTE: {(vistaExiste > 0 ? "✅ SÍ" : "❌ NO")}");

                if (vistaExiste > 0)
                {
                    // 2. Total de registros en la vista
                    var totalVista = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM vw_ArrastreCarreraCoordinador").FirstOrDefault();
                    diagnostico.AppendLine($"2. TOTAL REGISTROS EN VISTA: {totalVista}");

                    // 3. Registros para la carrera específica
                    var totalCarrera = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM vw_ArrastreCarreraCoordinador WHERE IdCarrera = @p0", idCarrera).FirstOrDefault();
                    diagnostico.AppendLine($"3. REGISTROS PARA CARRERA {idCarrera}: {totalCarrera}");

                    // 4. Muestra de datos
                    var muestra = db.Database.SqlQuery<dynamic>(
                        @"SELECT TOP 5 Matricula, NombreAlumno, MateriaArrastre, NombreEspecialidad 
                          FROM vw_ArrastreCarreraCoordinador 
                          WHERE IdCarrera = @p0", idCarrera).ToList();

                    diagnostico.AppendLine("4. MUESTRA DE DATOS:");
                    foreach (var item in muestra)
                    {
                        diagnostico.AppendLine($"   - {item.Matricula} | {item.NombreAlumno} | {item.MateriaArrastre} | {item.NombreEspecialidad}");
                    }

                    // 5. Verificar especialidades
                    var especialidades = db.Database.SqlQuery<dynamic>(
                        @"SELECT DISTINCT NombreEspecialidad, COUNT(*) as Cantidad 
                          FROM vw_ArrastreCarreraCoordinador 
                          WHERE IdCarrera = @p0 
                          GROUP BY NombreEspecialidad", idCarrera).ToList();

                    diagnostico.AppendLine($"5. ESPECIALIDADES ENCONTRADAS: {especialidades.Count}");
                    foreach (var esp in especialidades)
                    {
                        diagnostico.AppendLine($"   - {esp.NombreEspecialidad}: {esp.Cantidad} casos");
                    }
                }

                // 6. Verificar datos base
                var datosBase = db.Database.SqlQuery<int>(
                    @"SELECT COUNT(*) FROM DatosPersonales dp 
                      INNER JOIN MateriasAlumno ma ON dp.IdPersona = ma.IdPersona 
                      WHERE ma.Estado = 'Reprobada' AND dp.IdCarrera = @p0 AND dp.Estado = 1", idCarrera).FirstOrDefault();

                diagnostico.AppendLine($"6. MATERIAS REPROBADAS DIRECTAS: {datosBase}");

                // 7. Probar consulta directa
                diagnostico.AppendLine("7. PROBANDO CONSULTA DIRECTA:");
                var resultadoDirecto = CargarDatosDirectos(idCarrera, false, null, null);
                diagnostico.AppendLine($"   - Resultados consulta directa: {resultadoDirecto.Count}");

                if (resultadoDirecto.Count > 0)
                {
                    var ejemplo = resultadoDirecto.FirstOrDefault();
                    diagnostico.AppendLine($"   - Ejemplo: {ejemplo.Matricula} - {ejemplo.NombreAlumno} - {ejemplo.MateriaArrastre}");
                }

            }
            catch (Exception ex)
            {
                diagnostico.AppendLine($"❌ ERROR EN DIAGNÓSTICO: {ex.Message}");
                diagnostico.AppendLine($"❌ STACK TRACE: {ex.StackTrace}");
            }

            return Content(diagnostico.ToString(), "text/plain");
        }

        #region Métodos Privados Existentes

        private bool ValidarAccesoCoordinador(Usuario usuario)
        {
            // ✅ PERMITIR ACCESO A COORDINADORES (nivel 3) Y MASTER (nivel 4)
            return usuario != null && (usuario.IdNivel == 3 || usuario.IdNivel == 4);
        }

        private bool UsuarioTieneAccesoCarrera(Usuario usuario, int idCarrera)
        {
            if (usuario == null) return false;

            // ✅ MASTER TIENE ACCESO A TODAS LAS CARRERAS
            if (usuario.IdNivel == 4)
            {
                return true;
            }

            // ✅ COORDINADORES SOLO A SU CARRERA
            return usuario.IdCarrera == idCarrera;
        }
        private void CargarDropdownCarreras(Usuario usuario, int? idSeleccionado = null)
        {
            try
            {
                var carreras = new List<CarreraSeleccionDto>();

                // ✅ SI ES MASTER, CARGAR TODAS LAS CARRERAS
                if (usuario.IdNivel == 4)
                {
                    System.Diagnostics.Debug.WriteLine("🔑 Usuario MASTER detectado - Cargando TODAS las carreras");

                    var todasLasCarreras = db.Carreras.ToList();

                    foreach (var carrera in todasLasCarreras)
                    {
                        var totalArrastre = db.Database.SqlQuery<int>(
                            "SELECT COUNT(DISTINCT IdPersona) FROM vw_ArrastreCarreraCoordinador WHERE IdCarrera = @p0",
                            carrera.IdCarrera).FirstOrDefault();

                        carreras.Add(new CarreraSeleccionDto
                        {
                            IdCarrera = carrera.IdCarrera,
                            NombreCarrera = carrera.Nombre,
                            Nomenclatura = carrera.Nomenclatura ?? "",
                            TotalAlumnosConArrastre = totalArrastre,
                            DisplayText = $"{carrera.Nombre} ({totalArrastre} casos)"
                        });
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Cargadas {carreras.Count} carreras para MASTER");
                }
                // ✅ SI ES COORDINADOR, SOLO SU CARRERA
                else if (usuario.IdNivel == 3)
                {
                    System.Diagnostics.Debug.WriteLine($"📋 Usuario COORDINADOR - Cargando solo carrera {usuario.IdCarrera}");

                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                    if (carrera != null)
                    {
                        var totalArrastre = db.Database.SqlQuery<int>(
                            "SELECT COUNT(DISTINCT IdPersona) FROM vw_ArrastreCarreraCoordinador WHERE IdCarrera = @p0",
                            carrera.IdCarrera).FirstOrDefault();

                        carreras.Add(new CarreraSeleccionDto
                        {
                            IdCarrera = carrera.IdCarrera,
                            NombreCarrera = carrera.Nombre,
                            Nomenclatura = carrera.Nomenclatura ?? "",
                            TotalAlumnosConArrastre = totalArrastre,
                            DisplayText = $"{carrera.Nombre} ({totalArrastre} casos)"
                        });
                    }
                }

                ViewBag.CarrerasDropdown = carreras.Select(c => new SelectListItem
                {
                    Value = c.IdCarrera.ToString(),
                    Text = c.DisplayText,
                    Selected = c.IdCarrera == idSeleccionado
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"✅ Dropdown de carreras cargado: {carreras.Count} opciones");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CargarDropdownCarreras: {ex.Message}");
                ViewBag.CarrerasDropdown = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "Error: " + ex.Message }
        };
            }
        }

        private ResumenArrastreCarreraDto CalcularResumenArrastreCarrera(int idCarrera, bool soloCriticos = false)
        {
            try
            {
                // Usar la función auxiliar creada en la base de datos
                var resultado = db.Database.SqlQuery<ResumenArrastreCarreraDto>(
                    "SELECT * FROM fn_ResumenArrastreCarrera(@p0, @p1)", idCarrera, soloCriticos).FirstOrDefault();

                return resultado ?? new ResumenArrastreCarreraDto();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en resumen: {ex.Message}");
                return new ResumenArrastreCarreraDto();
            }
        }

        private void EstablecerInfoCarrera(int idCarrera, Usuario usuario = null)
        {
            try
            {
                if (idCarrera == 0)
                {
                    // ✅ Solo mostrar "VISTA GLOBAL" para usuarios Master
                    ViewBag.NombreCarrera = (usuario != null && usuario.IdNivel == 4)
                        ? "VISTA GLOBAL (TODAS LAS CARRERAS)"
                        : "Todas las carreras";
                }
                else
                {
                    var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == idCarrera);
                    ViewBag.NombreCarrera = carrera?.Nombre ?? "Carrera no encontrada";
                }
                ViewBag.IdCarrera = idCarrera;
                ViewBag.FechaConsulta = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en EstablecerInfoCarrera: {ex.Message}");
                ViewBag.NombreCarrera = "Error";
                ViewBag.IdCarrera = idCarrera;
            }
        }

        private void CargarFiltrosAdicionales(int idCarrera)
        {
            try
            {
                var especialidadesQuery = db.Especialidads.AsQueryable();

                if (idCarrera > 0)
                {
                    especialidadesQuery = especialidadesQuery.Where(e => e.IdCarrera == idCarrera);
                }

                var especialidades = especialidadesQuery
                    .Select(e => new SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = e.Nombre
                    }).ToList();

                especialidades.Insert(0, new SelectListItem { Value = "", Text = "Todas las especialidades" });
                ViewBag.EspecialidadesDropdown = especialidades;

                var grados = db.Gradoes
                    .Select(g => new SelectListItem
                    {
                        Value = g.IdGrado.ToString(),
                        Text = g.Nombre
                    }).ToList();

                grados.Insert(0, new SelectListItem { Value = "", Text = "Todos los grados" });
                ViewBag.GradosDropdown = grados;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando filtros: {ex.Message}");
                ViewBag.EspecialidadesDropdown = new List<SelectListItem>();
                ViewBag.GradosDropdown = new List<SelectListItem>();
            }
        }

        private ActionResult GenerarExcelSinDatos(int idCarrera, Usuario usuario, bool soloCriticos)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sin Datos de Arrastre");

                worksheet.Cells[1, 1].Value = "🔍 NO HAY MATERIAS EN ARRASTRE EN ESTA CARRERA";
                worksheet.Cells[1, 1, 1, 6].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);

                int fila = 3;
                worksheet.Cells[fila, 1].Value = "✅ ¡EXCELENTE! Todos los estudiantes de esta carrera están al corriente.";

                var excelBytes = package.GetAsByteArray();
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           $"SinArrastre_Carrera{idCarrera}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region DTOs para mapear resultados

    // DTO para resultados de la vista
    public class ArrastreVistaResultado
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreEspecialidad { get; set; }
        public string GradoGrupo { get; set; }
        public string MateriaArrastre { get; set; }
        public int CuatrimestreMateria { get; set; }
        public string CuatrimestreTexto { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
        public string NombreTurno { get; set; }
        public string NombrePeriodo { get; set; }
        public int Año { get; set; }
        public string TutorAsignado { get; set; }
        public int IdGrupo { get; set; }
        public int IdCarrera { get; set; }
        public int IdGrado { get; set; }
        public int IdTurno { get; set; }
        public int IdPeriodo { get; set; }
        public int IdEspecialidad { get; set; }
        public int NivelCriticidad { get; set; }
        public string ClasificacionVisual { get; set; }
        public string DescripcionCriticidad { get; set; }
        public int DiasEnArrastre { get; set; }
        public DateTime? FechaLimiteArrastre { get; set; }
        public int DiasRestantes { get; set; }
        public string EstadoTiempo { get; set; }
        public long OrdenPrioridad { get; set; }

        public bool MateriaEstaActiva { get; set; }
        public string EstadoMateria { get; set; }
        public bool EstadoAlumno { get; set; }
        public string EstadoMateriaAlumno { get; set; }
    }

    // DTO para consulta directa (fallback)
    public class ArrastreDirectoResultado
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreEspecialidad { get; set; }
        public string GradoGrupo { get; set; }
        public string MateriaArrastre { get; set; }
        public int CuatrimestreMateria { get; set; }
        public string CuatrimestreTexto { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
        public string NombreTurno { get; set; }
        public string NombrePeriodo { get; set; }
        public int Año { get; set; }
        public string TutorAsignado { get; set; }
        public int IdGrupo { get; set; }
        public int IdCarrera { get; set; }
        public int IdGrado { get; set; }
        public int IdTurno { get; set; }
        public int IdPeriodo { get; set; }
        public int IdEspecialidad { get; set; }
        public int NivelCriticidad { get; set; }
        public string ClasificacionVisual { get; set; }
        public string DescripcionCriticidad { get; set; }
        public int DiasEnArrastre { get; set; }
        public DateTime? FechaLimiteArrastre { get; set; }
        public int DiasRestantes { get; set; }
        public string EstadoTiempo { get; set; }

        public bool MateriaEstaActiva { get; set; }
        public string EstadoMateria { get; set; }
        public bool EstadoAlumno { get; set; }
        public string EstadoMateriaAlumno { get; set; }
    }

    #endregion
}