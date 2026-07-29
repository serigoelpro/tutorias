using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;

public class SeguimientoTutorialController : Controller
{
    private ModeloPlataforma db = new ModeloPlataforma();

    // GET: SeguimientoTutorial/Index
    public ActionResult Index(int? seccion = 1, int? pagina = 1)
    {
        var usuario = Session["Usuario"] as Usuario;
        if (usuario == null)
        {
            return RedirectToAction("Login", "Account");
        }

        bool esTutor = usuario.IdNivel == 2;
        int idUsuarioActual = usuario.IdUsuario;

        try
        {
            // Verificar si hay datos en la base de datos (sin filtro de año)
            var datosDisponibles = VerificarDatosDisponibles();
            if (!datosDisponibles)
            {
                ViewBag.Error = "No hay datos disponibles en la base de datos. Verifique que existan registros en las tablas principales.";
                ViewBag.NivelUsuario = usuario.IdNivel;
                ViewBag.NombreUsuario = usuario.NombreCompleto;
                return View(new List<SeguimientoTutorialViewModel>());
            }

            // Parámetros de paginación por secciones
            int seccionActual = seccion ?? 1;
            int paginaActual = pagina ?? 1;
            int registrosPorPagina = 50;
            int registrosPorSeccion = 100; // 2 páginas × 50 registros (reducido de 200)
            int offset = (seccionActual - 1) * registrosPorSeccion + (paginaActual - 1) * registrosPorPagina;

            var seguimientoData = EjecutarConsultaSeguimiento(usuario, idUsuarioActual, offset, registrosPorPagina);
            var totalRegistros = ObtenerTotalRegistros(usuario, idUsuarioActual);

            // ENRIQUECER DATOS CON TOTALES (consultas individuales rápidas)
            if (seguimientoData.Any())
            {
                System.Diagnostics.Debug.WriteLine("Enriqueciendo datos con totales individuales...");
                System.Diagnostics.Debug.WriteLine($"Total de registros a enriquecer: {seguimientoData.Count}");
                
                // AGREGAR INFORMACIÓN DE DEBUGGING
                ViewBag.DebugInfo = $"Enriqueciendo {seguimientoData.Count} registros...";
                
                // VERIFICAR DATOS ANTES DEL ENRIQUECIMIENTO
                VerificarDatosEntrevistaInicials();
                
                EnriquecerDatosConTotales(seguimientoData, usuario, idUsuarioActual);
                System.Diagnostics.Debug.WriteLine("Datos enriquecidos exitosamente");
                
                // VERIFICAR RESULTADOS
                var totalAlumnos = seguimientoData.Sum(x => x.Total);
                var totalHombres = seguimientoData.Sum(x => x.H);
                var totalMujeres = seguimientoData.Sum(x => x.M);
                
                System.Diagnostics.Debug.WriteLine($"RESULTADOS FINALES: Total Alumnos={totalAlumnos}, H={totalHombres}, M={totalMujeres}");
                ViewBag.DebugInfo += $" | Resultados: Total={totalAlumnos}, H={totalHombres}, M={totalMujeres}";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No hay datos para enriquecer");
                ViewBag.DebugInfo = "No hay datos para enriquecer";
            }

            // Calcular información de paginación
            int totalSecciones = (int)Math.Ceiling((double)totalRegistros / registrosPorSeccion);
            int totalPaginasEnSeccion = Math.Min(4, (int)Math.Ceiling((double)Math.Min(registrosPorSeccion, totalRegistros - (seccionActual - 1) * registrosPorSeccion) / registrosPorPagina));

            // Preparar datos para la vista
            ViewBag.NivelUsuario = usuario.IdNivel;
            ViewBag.NombreUsuario = usuario.NombreCompleto;
            ViewBag.CarreraCoordinador = usuario.IdCarrera;
            ViewBag.TotalRegistros = totalRegistros;
            ViewBag.RegistrosMostrados = seguimientoData.Count;
            ViewBag.SeccionActual = seccionActual;
            ViewBag.PaginaActual = paginaActual;
            ViewBag.TotalSecciones = totalSecciones;
            ViewBag.TotalPaginasEnSeccion = totalPaginasEnSeccion;
            ViewBag.RegistrosPorPagina = registrosPorPagina;
            ViewBag.RegistrosPorSeccion = registrosPorSeccion;
            ViewBag.PrimerRegistro = offset + 1;
            ViewBag.UltimoRegistro = offset + seguimientoData.Count;

            return View(seguimientoData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en Index: {ex.Message}");
            ViewBag.Error = "Ocurrió un error al cargar los datos. Por favor, contacte al administrador.";
            ViewBag.ErrorDetalle = ex.Message; // Para debugging
            ViewBag.NivelUsuario = usuario?.IdNivel ?? 0;
            ViewBag.NombreUsuario = usuario?.NombreCompleto ?? "Usuario desconocido";
            return View(new List<SeguimientoTutorialViewModel>());
        }
    }

    private bool VerificarDatosDisponibles()
    {
        try
        {
            using (var connection = new SqlConnection(db.Database.Connection.ConnectionString))
            {
                connection.Open();
                
                // Verificar si hay datos en TutoriaGrupals (sin filtro de año)
                using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.TutoriaGrupals", connection))
                {
                    var countTutoria = Convert.ToInt32(command.ExecuteScalar());
                    System.Diagnostics.Debug.WriteLine($"Total de registros en TutoriaGrupals: {countTutoria}");
                    
                    if (countTutoria == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No hay registros en TutoriaGrupals");
                        return false;
                    }
                }
                
                // Verificar si hay datos en EntrevistaInicials (sin filtro de año)
                using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.EntrevistaInicials", connection))
                {
                    var countEntrevista = Convert.ToInt32(command.ExecuteScalar());
                    System.Diagnostics.Debug.WriteLine($"Total de registros en EntrevistaInicials: {countEntrevista}");
                    
                    if (countEntrevista == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No hay registros en EntrevistaInicials");
                        return false;
                    }
                }
                
                // Verificar si hay usuarios tutores
                using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Usuarios WHERE IdNivel = 2", connection))
                {
                    var countTutores = Convert.ToInt32(command.ExecuteScalar());
                    System.Diagnostics.Debug.WriteLine($"Total de tutores en el sistema: {countTutores}");
                    
                    if (countTutores == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No hay usuarios con nivel de tutor en el sistema");
                        return false;
                    }
                }
                
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al verificar datos disponibles: {ex.Message}");
            return false;
        }
    }

    private int ObtenerTotalRegistros(Usuario usuario, int idUsuarioActual)
    {
        try
        {
            string consultaCount = @"
                SELECT COUNT(DISTINCT CONCAT(tg.IdCarrera, '-', tg.IdGrado, '-', tg.IdGrupo, '-', tg.IdTurno, '-', tg.Año))
                FROM dbo.TutoriaGrupals tg
                INNER JOIN dbo.Usuarios u ON u.IdUsuario = tg.IdUsuario AND u.IdNivel = 2
                WHERE (@NivelUsuario = 4) -- Master ve todo
                   OR (@NivelUsuario = 3 AND tg.IdCarrera = @IdCarrera) -- Coordinador ve su carrera
                   OR (@NivelUsuario = 2 AND tg.IdUsuario = @IdUsuario) -- Tutor ve sus grupos";

            using (var connection = new SqlConnection(db.Database.Connection.ConnectionString))
            {
                using (var command = new SqlCommand(consultaCount, connection))
                {
                    command.Parameters.AddWithValue("@NivelUsuario", usuario.IdNivel);
                    command.Parameters.AddWithValue("@IdUsuario", idUsuarioActual);
                    command.Parameters.Add("@IdCarrera", SqlDbType.Int).Value = usuario?.IdCarrera ?? 0;

                    connection.Open();
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al obtener total de registros: {ex.Message}");
            return 0;
        }
    }

    private List<SeguimientoTutorialViewModel> EjecutarConsultaSeguimiento(Usuario usuario, int idUsuarioActual, int offset = 0, int limit = 100)
    {
        var resultado = new List<SeguimientoTutorialViewModel>();

        // CONSULTA SIMPLIFICADA Y OPTIMIZADA para evitar timeout
        string consultaSQL = @"
            SELECT 
                CONCAT(CAST(tg.IdGrado AS varchar(2)), 
                       ISNULL((SELECT TOP(1) g.Nombre FROM dbo.Grupoes g WHERE g.IdGrupo = tg.IdGrupo), '')) AS Grupo,
                u.NombreCompleto AS NombreTutor,
                ISNULL(p.Nombre, 'Sin período') AS NombrePeriodo,
                tg.Año,
                -- Contadores simplificados para evitar JOINs complejos
                0 AS Total,
                0 AS H,
                0 AS M,
                0 AS Vulnerable_Economico,
                0 AS Vulnerable_Academico,
                0 AS Vulnerable_Personal,
                0 AS No_Vulnerables,
                0 AS Padres_H,
                0 AS Madres_M,
                0 AS Trabajan_H,
                0 AS Trabajan_M,
                0 AS Becados_H,
                0 AS Becados_M,
                0 AS PAT_Activo,
                'Sin revisar' AS PAT_EstadoRevision
            FROM dbo.TutoriaGrupals tg
            INNER JOIN dbo.Usuarios u ON u.IdUsuario = tg.IdUsuario AND u.IdNivel = 2
            LEFT JOIN dbo.Periodoes p ON p.IdPeriodo = tg.IdPeriodo
            WHERE (@NivelUsuario = 4) -- Master ve todo
              OR (@NivelUsuario = 3 AND tg.IdCarrera = @IdCarrera) -- Coordinador ve su carrera
              OR (@NivelUsuario = 2 AND tg.IdUsuario = @IdUsuario) -- Tutor ve sus grupos
            ORDER BY tg.Año DESC, tg.IdPeriodo, tg.IdGrado, tg.IdGrupo
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

        try
        {
            using (var connection = new SqlConnection(db.Database.Connection.ConnectionString))
            {
                using (var command = new SqlCommand(consultaSQL, connection))
                {
                    command.Parameters.AddWithValue("@NivelUsuario", usuario.IdNivel);
                    command.Parameters.AddWithValue("@IdUsuario", idUsuarioActual);
                    command.Parameters.Add("@IdCarrera", SqlDbType.Int).Value = usuario?.IdCarrera ?? 0;
                    command.Parameters.AddWithValue("@Offset", offset);
                    command.Parameters.AddWithValue("@Limit", limit);

                    System.Diagnostics.Debug.WriteLine($"Ejecutando consulta SIMPLIFICADA con parámetros:");
                    System.Diagnostics.Debug.WriteLine($"NivelUsuario: {usuario.IdNivel}");
                    System.Diagnostics.Debug.WriteLine($"IdUsuario: {idUsuarioActual}");
                    System.Diagnostics.Debug.WriteLine($"IdCarrera: {usuario?.IdCarrera ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"Offset: {offset}");
                    System.Diagnostics.Debug.WriteLine($"Limit: {limit}");

                    connection.Open();
                    System.Diagnostics.Debug.WriteLine($"Conexión abierta exitosamente");
                    
                    using (var reader = command.ExecuteReader())
                    {
                        int contador = 0;
                        while (reader.Read())
                        {
                            contador++;
                            resultado.Add(new SeguimientoTutorialViewModel
                            {
                                Grupo = reader["Grupo"]?.ToString() ?? "",
                                NombreTutor = reader["NombreTutor"]?.ToString() ?? "",
                                NombrePeriodo = reader["NombrePeriodo"]?.ToString() ?? "",
                                Año = Convert.ToInt32(reader["Año"] ?? 0),
                                Total = Convert.ToInt32(reader["Total"] ?? 0),
                                H = Convert.ToInt32(reader["H"] ?? 0),
                                M = Convert.ToInt32(reader["M"] ?? 0),
                                Vulnerable_Economico = Convert.ToInt32(reader["Vulnerable_Economico"] ?? 0),
                                Vulnerable_Academico = Convert.ToInt32(reader["Vulnerable_Academico"] ?? 0),
                                Vulnerable_Personal = Convert.ToInt32(reader["Vulnerable_Personal"] ?? 0),
                                No_Vulnerables = Convert.ToInt32(reader["No_Vulnerables"] ?? 0),
                                Padres_H = Convert.ToInt32(reader["Padres_H"] ?? 0),
                                Madres_M = Convert.ToInt32(reader["Madres_M"] ?? 0),
                                Trabajan_H = Convert.ToInt32(reader["Trabajan_H"] ?? 0),
                                Trabajan_M = Convert.ToInt32(reader["Trabajan_M"] ?? 0),
                                Becados_H = Convert.ToInt32(reader["Becados_H"] ?? 0),
                                Becados_M = Convert.ToInt32(reader["Becados_M"] ?? 0),
                                PAT_Activo = Convert.ToInt32(reader["PAT_Activo"] ?? 0),
                                PAT_EstadoRevision = reader["PAT_EstadoRevision"]?.ToString() ?? "Sin revisar"
                            });
                        }
                        System.Diagnostics.Debug.WriteLine($"Se leyeron {contador} registros de la consulta SIMPLIFICADA");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en consulta SQL SIMPLIFICADA: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"Consulta SQL: {consultaSQL}");
            System.Diagnostics.Debug.WriteLine($"Parámetros: NivelUsuario={usuario.IdNivel}, IdUsuario={idUsuarioActual}, IdCarrera={usuario.IdCarrera}, Offset={offset}, Limit={limit}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            throw;
        }

        System.Diagnostics.Debug.WriteLine($"Total de registros retornados: {resultado.Count}");
        return resultado;
    }

    // NUEVO MÉTODO: Obtener totales por grupo de forma individual
    private void EnriquecerDatosConTotales(List<SeguimientoTutorialViewModel> datos, Usuario usuario, int idUsuarioActual)
    {
        if (datos == null || !datos.Any()) return;

        System.Diagnostics.Debug.WriteLine($"Iniciando enriquecimiento de {datos.Count} registros...");

        try
        {
            foreach (var item in datos)
            {
                System.Diagnostics.Debug.WriteLine($"Enriqueciendo grupo: {item.Grupo}, Año: {item.Año}");
                
                // Consulta individual para cada grupo (CORREGIDA para obtener totales reales)
                string consultaTotales = @"
                    SELECT 
                        COUNT(DISTINCT ei.Matricula) AS Total,
                        SUM(CASE WHEN UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS H,
                        SUM(CASE WHEN UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS M,
                        -- Vulnerables Económicos (H y M)
                        SUM(CASE WHEN ei.IdVulnerable = 1 AND ei.IdEleccionVunerabilidad = 1 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS Vulnerable_Economico_H,
                        SUM(CASE WHEN ei.IdVulnerable = 1 AND ei.IdEleccionVunerabilidad = 1 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS Vulnerable_Economico_M,
                        -- Vulnerables Personales (H y M)
                        SUM(CASE WHEN ei.IdVulnerable = 1 AND ei.IdEleccionVunerabilidad = 3 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS Vulnerable_Personal_H,
                        SUM(CASE WHEN ei.IdVulnerable = 1 AND ei.IdEleccionVunerabilidad = 3 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS Vulnerable_Personal_M,
                        -- Vulnerables Académicos (H y M)
                        SUM(CASE WHEN ei.IdVulnerable = 1 AND ei.IdEleccionVunerabilidad = 2 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS Vulnerable_Academico_H,
                        SUM(CASE WHEN ei.IdVulnerable = 1 AND ei.IdEleccionVunerabilidad = 2 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS Vulnerable_Academico_M,
                        -- No Vulnerables (H y M)
                        SUM(CASE WHEN (ei.IdVulnerable = 2 OR ei.IdEleccionVunerabilidad = 4) AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS No_Vulnerables_H,
                        SUM(CASE WHEN (ei.IdVulnerable = 2 OR ei.IdEleccionVunerabilidad = 4) AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS No_Vulnerables_M,
                        -- Padres de Familias (total, no separado por sexo)
                        SUM(CASE WHEN ei.IdHijo = 1 THEN 1 ELSE 0 END) AS Padres_Familias,
                        -- Alumnos Trabajando (H y M)
                        SUM(CASE WHEN ei.IdTrabajo = 1 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS Trabajan_H,
                        SUM(CASE WHEN ei.IdTrabajo = 1 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS Trabajan_M,
                        -- Alumnos Becados (H y M)
                        SUM(CASE WHEN e.IdBeca IS NOT NULL AND e.IdBeca > 0 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'H' THEN 1 ELSE 0 END) AS Becados_H,
                        SUM(CASE WHEN e.IdBeca IS NOT NULL AND e.IdBeca > 0 AND UPPER(LEFT(ISNULL(ei.Sexo,''),1)) = 'M' THEN 1 ELSE 0 END) AS Becados_M
                    FROM dbo.EntrevistaInicials ei
                    LEFT JOIN dbo.Estudiantes e ON e.Matricula = ei.Matricula AND e.Año = @Año
                    WHERE ei.IdCarrera = @IdCarrera 
                      AND ei.IdGrado = @IdGrado 
                      AND ei.IdGrupo = @IdGrupo 
                      AND ei.IdTurno = @IdTurno";

                using (var connection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    using (var command = new SqlCommand(consultaTotales, connection))
                    {
                        // Extraer información del grupo (formato: "1A", "2B", etc.)
                        var grupoInfo = ExtraerInfoGrupo(item.Grupo);
                        
                        System.Diagnostics.Debug.WriteLine($"Grupo extraído - Grado: {grupoInfo.IdGrado}, Grupo: {grupoInfo.IdGrupo}");
                        
                        // Usar la carrera del usuario actual o buscar en TutoriaGrupals
                        int idCarrera = usuario.IdCarrera;
                        if (idCarrera == 0)
                        {
                            // Si no hay carrera del usuario, buscar en TutoriaGrupals
                            string consultaCarrera = @"
                                SELECT TOP(1) IdCarrera 
                                FROM dbo.TutoriaGrupals 
                                WHERE IdGrado = @IdGrado AND IdGrupo = @IdGrupo AND Año = @Año";
                            
                            using (var cmdCarrera = new SqlCommand(consultaCarrera, connection))
                            {
                                cmdCarrera.Parameters.AddWithValue("@IdGrado", grupoInfo.IdGrado);
                                cmdCarrera.Parameters.AddWithValue("@IdGrupo", grupoInfo.IdGrupo);
                                cmdCarrera.Parameters.AddWithValue("@Año", item.Año);
                                
                                if (connection.State != ConnectionState.Open)
                                    connection.Open();
                                
                                var result = cmdCarrera.ExecuteScalar();
                                if (result != null)
                                {
                                    idCarrera = Convert.ToInt32(result);
                                    System.Diagnostics.Debug.WriteLine($"Carrera encontrada: {idCarrera}");
                                }
                            }
                        }
                        
                        command.Parameters.AddWithValue("@IdCarrera", idCarrera);
                        command.Parameters.AddWithValue("@IdGrado", grupoInfo.IdGrado);
                        command.Parameters.AddWithValue("@IdGrupo", grupoInfo.IdGrupo);
                        command.Parameters.AddWithValue("@IdTurno", 1); // Default
                        command.Parameters.AddWithValue("@Año", item.Año);

                        System.Diagnostics.Debug.WriteLine($"Ejecutando consulta de totales con parámetros: Carrera={idCarrera}, Grado={grupoInfo.IdGrado}, Grupo={grupoInfo.IdGrupo}, Turno=1, Año={item.Año}");

                        if (connection.State != ConnectionState.Open)
                            connection.Open();
                            
                        // EJECUTAR CONSULTA Y VER RESULTADOS
                        System.Diagnostics.Debug.WriteLine($"Ejecutando consulta SQL: {consultaTotales}");
                        
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var total = Convert.ToInt32(reader["Total"] ?? 0);
                                var h = Convert.ToInt32(reader["H"] ?? 0);
                                var m = Convert.ToInt32(reader["M"] ?? 0);
                                
                                System.Diagnostics.Debug.WriteLine($"DATOS OBTENIDOS para {item.Grupo}: Total={total}, H={h}, M={m}");
                                
                                item.Total = total;
                                item.H = h;
                                item.M = m;
                                
                                // Vulnerables Económicos (H y M)
                                item.Vulnerable_Economico_H = Convert.ToInt32(reader["Vulnerable_Economico_H"] ?? 0);
                                item.Vulnerable_Economico_M = Convert.ToInt32(reader["Vulnerable_Economico_M"] ?? 0);
                                
                                // Vulnerables Personales (H y M)
                                item.Vulnerable_Personal_H = Convert.ToInt32(reader["Vulnerable_Personal_H"] ?? 0);
                                item.Vulnerable_Personal_M = Convert.ToInt32(reader["Vulnerable_Personal_M"] ?? 0);
                                
                                // Vulnerables Académicos (H y M)
                                item.Vulnerable_Academico_H = Convert.ToInt32(reader["Vulnerable_Academico_H"] ?? 0);
                                item.Vulnerable_Academico_M = Convert.ToInt32(reader["Vulnerable_Academico_M"] ?? 0);
                                
                                // No Vulnerables (H y M)
                                item.No_Vulnerables_H = Convert.ToInt32(reader["No_Vulnerables_H"] ?? 0);
                                item.No_Vulnerables_M = Convert.ToInt32(reader["No_Vulnerables_M"] ?? 0);
                                
                                // Padres de Familias (total)
                                item.Padres_Familias = Convert.ToInt32(reader["Padres_Familias"] ?? 0);
                                
                                // Alumnos Trabajando (H y M)
                                item.Trabajan_H = Convert.ToInt32(reader["Trabajan_H"] ?? 0);
                                item.Trabajan_M = Convert.ToInt32(reader["Trabajan_M"] ?? 0);
                                
                                // Alumnos Becados (H y M)
                                item.Becados_H = Convert.ToInt32(reader["Becados_H"] ?? 0);
                                item.Becados_M = Convert.ToInt32(reader["Becados_M"] ?? 0);
                                
                                System.Diagnostics.Debug.WriteLine($"Totales obtenidos para {item.Grupo}: Total={item.Total}, H={item.H}, M={item.M}");
                                System.Diagnostics.Debug.WriteLine($"Vulnerables: Económico H={item.Vulnerable_Economico_H} M={item.Vulnerable_Economico_M}, Personal H={item.Vulnerable_Personal_H} M={item.Vulnerable_Personal_M}, Académico H={item.Vulnerable_Academico_H} M={item.Vulnerable_Academico_M}");
                                System.Diagnostics.Debug.WriteLine($"No Vulnerables: H={item.No_Vulnerables_H} M={item.No_Vulnerables_M}, Padres={item.Padres_Familias}, Trabajan H={item.Trabajan_H} M={item.Trabajan_M}, Becados H={item.Becados_H} M={item.Becados_M}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ NO SE ENCONTRARON DATOS para el grupo {item.Grupo} con parámetros: Carrera={idCarrera}, Grado={grupoInfo.IdGrado}, Grupo={grupoInfo.IdGrupo}, Turno=1, Año={item.Año}");
                            }
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine("Enriquecimiento de datos completado exitosamente");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al enriquecer datos con totales: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            // No lanzar excepción, continuar con datos básicos
        }
    }

    // Método auxiliar para extraer información del grupo
    private (int IdGrado, int IdGrupo) ExtraerInfoGrupo(string grupo)
    {
        try
        {
            if (string.IsNullOrEmpty(grupo)) return (1, 1);
            
            // El formato es "1A", "2B", etc.
            if (grupo.Length >= 2)
            {
                var gradoStr = grupo.Substring(0, 1);
                var grupoStr = grupo.Substring(1, 1);
                
                if (int.TryParse(gradoStr, out int grado))
                {
                    // Convertir letra a número (A=1, B=2, etc.)
                    int grupoNum = char.ToUpper(grupoStr[0]) - 'A' + 1;
                    return (grado, grupoNum);
                }
            }
        }
        catch
        {
            // En caso de error, valores por defecto
        }
        
        return (1, 1);
    }

    // MÉTODO DE PRUEBA: Verificar datos de EntrevistaInicials
    private void VerificarDatosEntrevistaInicials()
    {
        try
        {
            using (var connection = new SqlConnection(db.Database.Connection.ConnectionString))
            {
                connection.Open();
                
                // Consulta simple para ver qué datos hay
                string consultaPrueba = @"
                    SELECT TOP(10)
                        ei.IdCarrera,
                        ei.IdGrado,
                        ei.IdGrupo,
                        ei.IdTurno,
                        ei.Sexo,
                        ei.IdVulnerable,
                        ei.IdEleccionVunerabilidad,
                        ei.IdHijo,
                        ei.IdTrabajo,
                        COUNT(*) as TotalRegistros
                    FROM dbo.EntrevistaInicials ei
                    GROUP BY ei.IdCarrera, ei.IdGrado, ei.IdGrupo, ei.IdTurno, ei.Sexo, ei.IdVulnerable, ei.IdEleccionVunerabilidad, ei.IdHijo, ei.IdTrabajo
                    ORDER BY TotalRegistros DESC";
                
                using (var command = new SqlCommand(consultaPrueba, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        System.Diagnostics.Debug.WriteLine("=== DATOS DE PRUEBA EN EntrevistaInicials ===");
                        while (reader.Read())
                        {
                            System.Diagnostics.Debug.WriteLine($"Carrera: {reader["IdCarrera"]}, Grado: {reader["IdGrado"]}, Grupo: {reader["IdGrupo"]}, Turno: {reader["IdTurno"]}, Sexo: {reader["Sexo"]}, Vulnerable: {reader["IdVulnerable"]}, Elección: {reader["IdEleccionVunerabilidad"]}, Hijo: {reader["IdHijo"]}, Trabajo: {reader["IdTrabajo"]}, Total: {reader["TotalRegistros"]}");
                        }
                    }
                }
                
                // Verificar total general
                using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.EntrevistaInicials", connection))
                {
                    var total = Convert.ToInt32(command.ExecuteScalar());
                    System.Diagnostics.Debug.WriteLine($"TOTAL GENERAL de EntrevistaInicials: {total}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en verificación: {ex.Message}");
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
