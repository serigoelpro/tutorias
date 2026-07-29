using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PlataformaWeb.BecasTransporte.Models;
using System.Data.Entity;
using iTextSharp.text.pdf.security;

namespace PlataformaWeb.Controllers
{
    public class MainController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        public ActionResult Index()
        {
            return View();
        }

        // GET: Grupos
        [HttpGet]
        public JsonResult GetGroup(int id)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var grup = db.TutoriaGrupals
                .AsNoTracking()
                .FirstOrDefault(x => x.IdTutoriaGrupal == id);

            if (grup == null)
            {
                return Json(new { error = "Grupo no encontrado" }, JsonRequestBehavior.AllowGet);
            }
            var query = @"
SELECT
    dp.IdPersona, dp.Fecha, dp.Matricula, dp.Nombre, dp.IdCarrera, dp.IdGrupo, dp.IdGrado, dp.Especialidad,
    '' AS Foto, dp.Estado, g.Nombre AS GradoNombre, gr.Nombre AS GrupoNombre,
    c.Nombre AS CarreraNombre, t.Nombre AS TurnoNombre,
    
    COALESCE(segTop.Vulnerabilidad, CAST(eiTop.IdEleccionVunerabilidad AS VARCHAR(30))) AS Vulnerabilidad

FROM DatosPersonales dp
LEFT JOIN Gradoes g ON dp.IdGrado = g.IdGrado
LEFT JOIN Grupoes gr ON dp.IdGrupo = gr.IdGrupo
LEFT JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
LEFT JOIN Turnoes t ON dp.IdTurno = t.IdTurno

OUTER APPLY (
    SELECT TOP 1 ei.IdEleccionVunerabilidad
    FROM EntrevistaInicials ei
    WHERE ei.IdPersona = dp.IdPersona
    ORDER BY ei.IdEntrevistaInicial DESC 
) AS eiTop

OUTER APPLY (
    SELECT TOP 1 seg.Vulnerabilidad
    FROM Seguimientoes seg
    INNER JOIN Individuals ind ON seg.IdIndividual = ind.IdIndividual
    WHERE ind.IdPersona = dp.IdPersona
    ORDER BY seg.Fecha DESC
) AS segTop

WHERE
    dp.IdCarrera = @IdCarrera AND dp.IdGrado = @IdGrado AND dp.IdGrupo = @IdGrupo
    AND dp.IdTurno = @IdTurno AND dp.IdPeriodo = @IdPeriodo AND dp.Año = @Año
ORDER BY dp.Nombre";

            var datosPersonales = db.Database.SqlQuery<DatosPersonalesDTO>(
                query,
                new System.Data.SqlClient.SqlParameter("@IdCarrera", grup.IdCarrera),
                new System.Data.SqlClient.SqlParameter("@IdGrado", grup.IdGrado),
                new System.Data.SqlClient.SqlParameter("@IdGrupo", grup.IdGrupo),
                new System.Data.SqlClient.SqlParameter("@IdTurno", grup.IdTurno),
                new System.Data.SqlClient.SqlParameter("@IdPeriodo", grup.IdPeriodo),
                new System.Data.SqlClient.SqlParameter("@Año", grup.Año)

            ).ToList();

            var timeQuery = stopwatch.ElapsedMilliseconds;
            System.Diagnostics.Debug.WriteLine($"⚡ Query sin fotos: {timeQuery}ms - {datosPersonales.Count} alumnos");

            var jsonResult = Json(datosPersonales, JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;

            return jsonResult;
        }

        [HttpGet]
        [OutputCache(Duration = 3600, VaryByParam = "id")]
        public ActionResult GetFoto(int id)
        {
            try
            {
                var foto = db.DatosPersonales
                    .AsNoTracking()
                    .Where(x => x.IdPersona == id)
                    .Select(x => x.Foto)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(foto))
                {
                    return File(Server.MapPath("~/Imagenes/default-avatar.png"), "image/png");
                }

                if (foto.StartsWith("data:image"))
                {
                    var base64Data = foto.Substring(foto.IndexOf(',') + 1);
                    var imageBytes = Convert.FromBase64String(base64Data);
                    var contentType = foto.Substring(5, foto.IndexOf(';') - 5);
                    return File(imageBytes, contentType);
                }

                if (System.IO.File.Exists(Server.MapPath(foto)))
                {
                    return File(foto, "image/jpeg");
                }

                return File(Server.MapPath("~/Imagenes/default-avatar.png"), "image/png");
            }
            catch
            {
                return File(Server.MapPath("~/Imagenes/default-avatar.png"), "image/png");
            }
        }

        [HttpGet]
        public JsonResult GetGrades()
        {
            var grades = db.Gradoes.ToList();
            return Json(grades, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult EliminarEntrevista(int? idEntrevista, int? idPersona)
        {
            try
            {
                var entrevista = db.EntrevistaInicials.FirstOrDefault(x => x.IdEntrevistaInicial == idEntrevista);
                var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);
                if (entrevista == null)
                {
                    return Json(new { success = false, error = "Record not found." }, JsonRequestBehavior.AllowGet);
                }

                if (entrevista.Grados.Nombre == datos.Grado.Nombre)
                {
                    datos.Estado = false;
                    db.Entry(datos).State = EntityState.Modified;
                }

                db.EntrevistaInicials.Remove(entrevista);
                db.SaveChanges();

                return Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetCarreras(int? userCarrera)
        {
            List<Carrera> careerList;

            if (userCarrera.HasValue)
            {
                careerList = db.Carreras.Where(x => x.IdCarrera == userCarrera).ToList();
            }
            else
            {
                careerList = db.Carreras.ToList();
            }
            return Json(careerList, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetSpecialties(int? userId, int? userCarrera)

        {

            List<Especialidad> especialtyList;

            if (userCarrera.HasValue)
            {
                especialtyList = db.Especialidads.OrderBy(X => X.IdCarrera).Where(x => x.IdCarrera == userCarrera).ToList();
            }
            else
            {
                especialtyList = db.Especialidads.OrderBy(X => X.IdCarrera).ToList();
            }
            return Json(especialtyList, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetGroups(int? carreraId, int? Area_id, int? periodo, int? año)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return Json(new { error = "Usuario no está en la sesión" }, JsonRequestBehavior.AllowGet);
            }
            List<TutoriasGrupalsByArea> tutoriasByArea;

            // Calcular año y período de la misma manera que en otros controladores
            int añoActual = año ?? DateTime.Now.Year;
            int pa;

            if (periodo.HasValue)
            {
                pa = periodo.Value;
            }
            else
            {
                // Calcular período basado en el mes actual (igual que en AsesorController)
                var tiempo = DateTime.Now;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    pa = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    pa = 2;
                }
                else
                {
                    pa = 3;
                }
            }

            // Construir la consulta SQL dinámicamente según el tipo de usuario
            string query;
            object[] sqlParameters;

            // Si el usuario es tutor (nivel 2), agregar filtro por IdUsuario en la consulta SQL
            if (usuario.IdNivel == 2)
            {
                query = @"
        SELECT
            tg.IdTutoriaGrupal, tg.IdUsuario, tg.IdPeriodo, pr.Nombre AS Periodo, tg.Año, tg.IdTurno, tg.IdCarrera,
            t.Nombre AS Turno, gr.Nombre AS Grado, g.Nombre AS Grupo, 
            c.Nombre AS Carrera, 
            COALESCE(e.Nombre, 'Sin especialidad') AS Especialidad, 
            tg.IdEspecialidad, 
            e.SistemaNuevo,
            0 AS AlumnosSoporte
        FROM TutoriaGrupals tg
        JOIN Gradoes gr ON gr.IdGrado = tg.IdGrado
        JOIN Periodoes pr ON pr.IdPeriodo = tg.IdPeriodo
        JOIN Grupoes g ON g.IdGrupo = tg.IdGrupo
        JOIN Carreras c ON c.IdCarrera = tg.IdCarrera
        LEFT JOIN Turnoes t ON t.IdTurno = tg.IdTurno
        LEFT JOIN Especialidads e ON e.Id = tg.IdEspecialidad
        
        WHERE 
            tg.IdPeriodo = @IdPeriodo AND tg.Año = @Año AND tg.IdUsuario = @IdUsuario";

                sqlParameters = new object[]
                {
                    new System.Data.SqlClient.SqlParameter("@IdPeriodo", System.Data.SqlDbType.Int) { Value = pa },
                    new System.Data.SqlClient.SqlParameter("@Año", System.Data.SqlDbType.Int) { Value = añoActual },
                    new System.Data.SqlClient.SqlParameter("@IdUsuario", System.Data.SqlDbType.Int) { Value = usuario.IdUsuario }
                };

                // Log para debugging
                System.Diagnostics.Debug.WriteLine($"[GetGroups] Tutor (Nivel 2) - IdUsuario: {usuario.IdUsuario}, Periodo: {pa}, Año: {añoActual}");
                System.Diagnostics.Debug.WriteLine($"[GetGroups] SQL Query ejecutada con parámetros: IdPeriodo={pa} (tipo: {pa.GetType()}), Año={añoActual} (tipo: {añoActual.GetType()}), IdUsuario={usuario.IdUsuario} (tipo: {usuario.IdUsuario.GetType()})");
            }
            else
            {
                query = @"
        SELECT
            tg.IdTutoriaGrupal, tg.IdUsuario, tg.IdPeriodo, pr.Nombre AS Periodo, tg.Año, tg.IdTurno, tg.IdCarrera,
            t.Nombre AS Turno, gr.Nombre AS Grado, g.Nombre AS Grupo, 
            c.Nombre AS Carrera, 
            COALESCE(e.Nombre, 'Sin especialidad') AS Especialidad, 
            tg.IdEspecialidad, 
            e.SistemaNuevo,
            0 AS AlumnosSoporte
        FROM TutoriaGrupals tg
        JOIN Gradoes gr ON gr.IdGrado = tg.IdGrado
        JOIN Periodoes pr ON pr.IdPeriodo = tg.IdPeriodo
        JOIN Grupoes g ON g.IdGrupo = tg.IdGrupo
        JOIN Carreras c ON c.IdCarrera = tg.IdCarrera
        LEFT JOIN Turnoes t ON t.IdTurno = tg.IdTurno
        LEFT JOIN Especialidads e ON e.Id = tg.IdEspecialidad
        
        WHERE 
            tg.IdPeriodo = @IdPeriodo AND tg.Año = @Año";

                sqlParameters = new object[]
                {
                    new System.Data.SqlClient.SqlParameter("@IdPeriodo", System.Data.SqlDbType.Int) { Value = pa },
                    new System.Data.SqlClient.SqlParameter("@Año", System.Data.SqlDbType.Int) { Value = añoActual }
                };
            }

            db.Database.CommandTimeout = 300;

            // Log de la consulta SQL antes de ejecutarla
            if (usuario.IdNivel == 2)
            {
                System.Diagnostics.Debug.WriteLine($"[GetGroups] Ejecutando consulta SQL para tutor:");
                System.Diagnostics.Debug.WriteLine($"Query: {query}");
                System.Diagnostics.Debug.WriteLine($"Parámetros: IdPeriodo={pa}, Año={añoActual}, IdUsuario={usuario.IdUsuario}");
            }

            var todosLosGrupos = db.Database.SqlQuery<TutoriasGrupalsByArea>(query, sqlParameters).ToList();

            // Log para debugging
            System.Diagnostics.Debug.WriteLine($"[GetGroups] Total grupos encontrados: {todosLosGrupos.Count}, Usuario Nivel: {usuario.IdNivel}, IdUsuario: {usuario.IdUsuario}");

            // Si es tutor y no hay grupos, verificar directamente en la base de datos
            if (usuario.IdNivel == 2 && todosLosGrupos.Count == 0)
            {
                var gruposDirectos = db.TutoriaGrupals
                    .Where(x => x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == pa && x.Año == añoActual)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"[GetGroups] Verificación directa con LINQ: {gruposDirectos.Count} grupos encontrados");
                if (gruposDirectos.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[GetGroups] ERROR: Hay {gruposDirectos.Count} grupos en la BD pero la consulta SQL devolvió 0. Posible problema con la consulta SQL.");
                }
            }

            Especialidad especialidadSeleccionada = null;

            // Si el usuario es tutor (nivel 2), ya está filtrado en la consulta SQL
            if (usuario.IdNivel == 2)
            {
                // Para tutores, los grupos ya están filtrados por IdUsuario en la consulta SQL
                tutoriasByArea = todosLosGrupos.ToList();
            }
            else if (carreraId.HasValue)
            {
                tutoriasByArea = todosLosGrupos.Where(x => x.IdCarrera == carreraId).ToList();
            }
            else if (Area_id.HasValue)
            {
                especialidadSeleccionada = db.Especialidads.AsNoTracking()
                                                .FirstOrDefault(e => e.Id == Area_id);

                if (especialidadSeleccionada == null)
                {
                    return Json(new List<SelectListItem>(), JsonRequestBehavior.AllowGet);
                }

                bool esNivelSuperior = especialidadSeleccionada.Nombre.ToUpper().Contains("INGENIERÍA") ||
                                        especialidadSeleccionada.Nombre.ToUpper().Contains("LICENCIATURA");

                tutoriasByArea = todosLosGrupos
                    .Where(x => x.IdCarrera == especialidadSeleccionada.IdCarrera)
                    .Where(x => {
                        int gradoNum = 0;
                        int.TryParse(x.Grado, out gradoNum);
                        bool grupoEsNivelSuperior = gradoNum > 6;

                        if (esNivelSuperior != grupoEsNivelSuperior)
                        {
                            return false;
                        }

                        return x.IdEspecialidad.HasValue && x.IdEspecialidad.Value == Area_id.Value;
                    })
                    .ToList();
            }
            else
            {
                tutoriasByArea = new List<TutoriasGrupalsByArea>();
            }

            tutoriasByArea = tutoriasByArea
                .OrderBy(x => { int result; return int.TryParse(x.Grado, out result) ? result : 0; })
                .ThenBy(x => x.Grupo)
                .ToList();

            foreach (var item in tutoriasByArea)
            {
                string nombrePrincipal;
                string especialidad = item.Especialidad.ToString();
                string carrera = item.Carrera.ToString();
                int gradoNum = 0;
                int.TryParse(item.Grado, out gradoNum);

                if (especialidad != "Sin especialidad")
                {
                    nombrePrincipal = especialidad;
                }
                else if (Area_id.HasValue && especialidadSeleccionada != null)
                {
                    nombrePrincipal = especialidadSeleccionada.Nombre;
                }
                else
                {
                    if (item.IdCarrera == 1 && gradoNum >= 7)
                    {
                        var especialidadForzada = db.Especialidads.Find(1043);
                        if (especialidadForzada != null)
                        {
                            nombrePrincipal = especialidadForzada.Nombre;
                        }
                        else
                        {
                            nombrePrincipal = carrera;
                        }
                    }
                    else
                    {
                        nombrePrincipal = carrera;
                    }
                }

                // --- BLOQUE DE CONVERSIÓN DE GRADO ELIMINADO ---

                var x = $"{nombrePrincipal}, {item.Grado}{item.Grupo}, {item.Turno}, {item.Periodo}, {item.Año}";
                item.Nomenclatura = x;
            }

            return Json(tutoriasByArea.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>(), JsonRequestBehavior.AllowGet);
        }
    }

    public class DatosPersonalesDTO
    {
        public int IdPersona { get; set; }
        public DateTime? Fecha { get; set; }
        public string Matricula { get; set; }
        public string Nombre { get; set; }
        public int IdCarrera { get; set; }
        public int IdGrupo { get; set; }
        public int IdGrado { get; set; }
        public string Foto { get; set; }
        public bool Estado { get; set; }
        public string GradoNombre { get; set; }
        public string GrupoNombre { get; set; }
        public string CarreraNombre { get; set; }
        public string TurnoNombre { get; set; }
        public string Vulnerabilidad { get; set; }

        public string Especialidad { get; set; }
    }

    public class TutoriasGrupalsByArea
    {
        public int IdTutoriaGrupal { get; set; }
        public int IdUsuario { get; set; }
        public int IdPeriodo { get; set; }
        public string Periodo { get; set; }
        public int? Año { get; set; }
        public int IdTurno { get; set; }
        public int IdCarrera { get; set; }
        public string Turno { get; set; }
        public string Grado { get; set; }
        public string Grupo { get; set; }
        public string Carrera { get; set; }
        public string Especialidad { get; set; }
        public int? IdEspecialidad { get; set; }
        public int AlumnosSoporte { get; set; }
        public bool? SistemaNuevo { get; set; }
        public string Nomenclatura { get; set; }
    }
}