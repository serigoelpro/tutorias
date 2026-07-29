using Plataforma_Web.Models;
using PlataformaWeb;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PlataformaWeb.Models;
using PlataformaWeb.Models.Psicologia;

namespace Plataforma_Web.Controllers
{
    [CustomAuthorize(Nivel = 1)]
    public class CanalizacionesEController : Controller
    {
        private readonly ModeloPlataforma db = new ModeloPlataforma();

        #region Acción IndexE
        public ActionResult IndexE()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Home");
            }

            var estudiante = db.DatosPersonales.FirstOrDefault(p => p.Matricula == usuario.UserName);
            if (estudiante == null)
            {
                TempData["ErrorMessage"] = "No se encontraron datos del estudiante.";
                return RedirectToAction("Login", "Account");
            }

            ViewBag.IdPersona = estudiante.IdPersona;
            ViewBag.Nombre = $"{estudiante.Nombre} ".Trim();
            ViewBag.Matricula = estudiante.Matricula;
            ViewBag.Carrera = db.Carreras.Find(estudiante.IdCarrera)?.Nombre ?? "";
            ViewBag.Grupo = $"{db.Gradoes.Find(estudiante.IdGrado)?.Nombre ?? ""} {db.Grupoes.Find(estudiante.IdGrupo)?.Nombre ?? ""}";

            var canalizaciones = db.Database.SqlQuery<SelectListItemRaw>(
                @"WITH RankedCanalizaciones AS (
                    SELECT 
                        IdCanalizacion, 
                        IdTipoCanalizacion, 
                        Fecha, 
                        CAST(MotivoCanalizacion AS NVARCHAR(MAX)) AS Motivo,
                        ROW_NUMBER() OVER (PARTITION BY CAST(MotivoCanalizacion AS NVARCHAR(MAX)) ORDER BY Fecha DESC) AS RN
                    FROM Canalizaciones
                    WHERE IdPersona = @IdPersona
                )
                SELECT 
                    c.IdCanalizacion AS Value,
                    CAST(
                        COALESCE(
                            (SELECT Descripcion FROM TipoCanalizaciones WHERE IdTipoCanalizacion = c.IdTipoCanalizacion),
                            'Sin tipo'
                        ) + ' - ' + 
                        CONVERT(VARCHAR, c.Fecha, 103) + ' - ' +
                        CASE 
                            WHEN c.Motivo IS NULL THEN 'Sin motivo'
                            WHEN LEN(c.Motivo) > 30 THEN LEFT(c.Motivo, 30) + '...'
                            ELSE c.Motivo
                        END AS NVARCHAR(MAX)
                    ) AS Text
                FROM RankedCanalizaciones c
                WHERE c.RN = 1",
                new SqlParameter("@IdPersona", estudiante.IdPersona)
            )
            .ToList()
            .Select(r => new SelectListItem
            {
                Value = r.Value.ToString(),
                Text = r.Text
            })
            .ToList();

            ViewBag.PastVulnerabilities = canalizaciones;

            var tiposCanalizacion = db.TipoCanalizaciones
                                      .Select(t => new SelectListItem
                                      {
                                          Value = t.IdTipoCanalizacion.ToString(),
                                          Text = t.Descripcion
                                      }).ToList();
            ViewBag.TiposDeCanalizacion = tiposCanalizacion;

            return View();
        }
        #endregion

        #region Notificación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NotificarTutor(int idEstudiante, string mensaje)
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null) { return Json(new { success = false, message = "Sesión expirada." }); }
            if (idEstudiante <= 0 || string.IsNullOrWhiteSpace(mensaje)) { return Json(new { success = false, message = "Datos inválidos." }); }
            try
            {
                var estudiante = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == idEstudiante);
                if (estudiante == null) { return Json(new { success = false, message = "Estudiante no encontrado." }); }
                int periodoActual = ObtenerPeriodoActual();
                int añoActual = DateTime.Now.Year;
                var tutoria = db.TutoriaGrupals.FirstOrDefault(t => t.IdCarrera == estudiante.IdCarrera && t.IdGrado == estudiante.IdGrado && t.IdGrupo == estudiante.IdGrupo && t.IdTurno == estudiante.IdTurno && t.IdPeriodo == periodoActual && t.Año == añoActual);
                int? idTutor = tutoria?.IdUsuario;
                using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["ModeloPlataforma"].ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand("INSERT INTO Notificaciones (IdEstudiante, IdTutor, Mensaje, FechaEnvio) VALUES (@IdEstudiante, @IdTutor, @Mensaje, @FechaEnvio)", connection))
                    {
                        cmd.Parameters.AddWithValue("@IdEstudiante", idEstudiante);
                        cmd.Parameters.AddWithValue("@IdTutor", (object)idTutor ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Mensaje", mensaje);
                        cmd.Parameters.AddWithValue("@FechaEnvio", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { success = true, message = "Notificación enviada correctamente." });
            }
            catch (Exception ex) { return Json(new { success = false, message = $"Error: {ex.Message}" }); }
        }
        #endregion

        #region SolicitarCanalizacionIndividual
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SolicitarCanalizacionIndividual(CanalizacionViewModel modelo)
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return Json(new { success = false, message = "Sesión expirada. Por favor inicie sesión nuevamente." });
            }

            if (modelo == null || modelo.IdPersona <= 0 || string.IsNullOrWhiteSpace(modelo.MotivoCanalizacion) || modelo.IdTipoCanalizacion <= 0)
            {
                return Json(new { success = false, message = "Datos del formulario inválidos. Asegúrese de seleccionar un tipo y escribir el motivo." });
            }

            try
            {
                DateTime fechaActual = DateTime.Now;

                bool yaExisteHoy = db.Canalizaciones.Any(c =>
                    c.IdPersona == modelo.IdPersona &&
                    c.Fecha.Year == fechaActual.Year &&
                    c.Fecha.Month == fechaActual.Month &&
                    c.Fecha.Day == fechaActual.Day);

                if (yaExisteHoy)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Aviso: Ya has realizado una solicitud de canalización el día de hoy. Solo se permite una diaria."
                    });
                }

                var estudiante = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == modelo.IdPersona);
                if (estudiante == null)
                {
                    return Json(new { success = false, message = "No se encontraron datos del estudiante." });
                }

                var tutorReal = ObtenerTutorDelAlumno(estudiante.IdPersona);
                int idUsuarioParaGuardar;
                string correoTutorParaGuardar;

                if (tutorReal != null)
                {
                    idUsuarioParaGuardar = tutorReal.IdUsuario;
                    correoTutorParaGuardar = tutorReal.CorreoElectronico;
                }
                else
                {
                    idUsuarioParaGuardar = 0;
                    correoTutorParaGuardar = "Sin tutor asignado";
                }

                string textoVulnerabilidadPasada = null;
                if (modelo.SelectedPastVulnerabilityId.HasValue && modelo.SelectedPastVulnerabilityId > 0)
                {
                    var canalizacionPasada = db.Canalizaciones.Find(modelo.SelectedPastVulnerabilityId.Value);
                    if (canalizacionPasada != null)
                    {
                        textoVulnerabilidadPasada = canalizacionPasada.MotivoCanalizacion;
                    }
                }

                int newCanalizacionId = 0;

                using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["ModeloPlataforma"].ConnectionString))
                {
                    connection.Open();

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO Canalizaciones 
                        (IdPersona, IdUsuario, CorreoTutor, MotivoCanalizacion, VulnerabilidadesPasadas, IdTipoCanalizacion, Fecha, IdPsicologo, Status) 
                        VALUES 
                        (@IdPersona, @IdUsuario, @CorreoTutor, @MotivoCanalizacion, @VulnerabilidadesPasadas, @IdTipoCanalizacion, @Fecha, @IdPsicologo, @Status);
                        
                        SELECT SCOPE_IDENTITY();", connection))
                    {
                        cmd.Parameters.AddWithValue("@IdPersona", modelo.IdPersona);
                        cmd.Parameters.AddWithValue("@IdUsuario", idUsuarioParaGuardar);
                        cmd.Parameters.AddWithValue("@CorreoTutor", correoTutorParaGuardar);
                        cmd.Parameters.AddWithValue("@MotivoCanalizacion", modelo.MotivoCanalizacion);
                        cmd.Parameters.AddWithValue("@VulnerabilidadesPasadas", (object)textoVulnerabilidadPasada ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdTipoCanalizacion", modelo.IdTipoCanalizacion);
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Status", "Alumno");

                        if (modelo.IdPsicologo.HasValue && modelo.IdPsicologo > 0)
                        {
                            cmd.Parameters.AddWithValue("@IdPsicologo", modelo.IdPsicologo.Value);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@IdPsicologo", DBNull.Value);
                        }

                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            newCanalizacionId = Convert.ToInt32(result);
                        }
                    }

                    if (newCanalizacionId > 0)
                    {
                        using (var cmd = new SqlCommand("TransferirCanalizacionesToCitas", connection)
                        { CommandType = CommandType.StoredProcedure })
                        {
                            cmd.Parameters.AddWithValue("@IdCanalizacion", newCanalizacionId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                return Json(new { success = true, message = "Canalización enviada correctamente." });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error al guardar: {ex.Message}";
                errorMessage = errorMessage.Replace("'", "").Replace("\"", "").Replace("\r", " ").Replace("\n", " ");
                return Json(new { success = false, message = errorMessage });
            }
        }
        #endregion

        #region GetCanalizacionDetails
        public JsonResult GetCanalizacionDetails(int id)
        {
            var canalizacion = db.Canalizaciones
                .Where(c => c.IdCanalizacion == id)
                .Select(c => new
                {
                    c.IdTipoCanalizacion,
                    Motivo = c.MotivoCanalizacion
                })
                .FirstOrDefault();

            return canalizacion == null
                ? Json(new { error = "Canalización no encontrada" }, JsonRequestBehavior.AllowGet)
                : Json(canalizacion, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region ObtenerPeriodoActual
        private int ObtenerPeriodoActual()
        {
            int mesActual = DateTime.Now.Month;
            if (mesActual >= 1 && mesActual <= 4) return 1;
            if (mesActual >= 5 && mesActual <= 8) return 2;
            return 3;
        }
        #endregion

        #region Clases Auxiliares
        private class SelectListItemRaw
        {
            public int Value { get; set; }
            public string Text { get; set; }
        }

        public class CanalizacionViewModel
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; }
            public string Matricula { get; set; }
            public string Carrera { get; set; }
            public string Grupo { get; set; }
            public string TutorUsername { get; set; }
            public string TutorFullName { get; set; }
            public List<SelectListItem> PastVulnerabilities { get; set; }
            public int? SelectedPastVulnerabilityId { get; set; }
            public int IdTipoCanalizacion { get; set; }
            public string MotivoCanalizacion { get; set; }
            public int? IdPsicologo { get; set; }
        }
        #endregion

        [HttpGet]
        public JsonResult GetPsicologosPorTipo(int tipoId)
        {
            try
            {
                var psicologos = db.Psicologos
                    .Where(p => p.Activo)
                    .Include(p => p.PsicologoTurno) // <--- NECESARIO para leer el turno
                    .Include(p => p.Psicologo_PsiDetalles.Select(pd => pd.PsiDetalleAtencion.PsiAreaAtencion))
                    .ToList();

                var viewModel = psicologos.Select(p => new PsicologoCardViewModel
                {
                    IdPsicologo = p.IdPsicologo,
                    NombreCompleto = HttpUtility.HtmlDecode(p.NombreCompleto),
                    Horario = p.PsicologoTurno != null
                        ? (p.PsicologoTurno.Nombre.Trim().Equals("Mixto", StringComparison.OrdinalIgnoreCase)
                            ? "Matutino y Vespertino"
                            : p.PsicologoTurno.Nombre)
                        : "Horario no especificado",

                    EsRecomendado = (p.IdTipoCanalizacion == tipoId && tipoId > 0),
                    Areas = p.Psicologo_PsiDetalles
                        .Select(pd => pd.PsiDetalleAtencion)
                        .GroupBy(d => d.PsiAreaAtencion)
                        .Select(g => new AreaAtencionViewModel
                        {
                            NombreArea = HttpUtility.HtmlDecode(g.Key.NombreArea),
                            Detalles = g.Select(detalle => HttpUtility.HtmlDecode(detalle.DescripcionDetalle)).ToList()
                        })
                        .OrderBy(a => a.NombreArea)
                        .ToList()
                })
                .OrderBy(p => !p.EsRecomendado)
                .ThenBy(p => p.NombreCompleto)
                .ToList();

                return Json(viewModel, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private Usuario ObtenerTutorDelAlumno(int idPersona)
        {
            var alumno = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == idPersona);
            if (alumno == null) return null;

            int periodoActual = ObtenerPeriodoActual();
            int añoActual = DateTime.Now.Year;

            var tutoria = db.TutoriaGrupals.FirstOrDefault(t =>
                t.IdCarrera == alumno.IdCarrera &&
                t.IdGrado == alumno.IdGrado &&
                t.IdGrupo == alumno.IdGrupo &&
                t.IdTurno == alumno.IdTurno &&
                t.IdPeriodo == periodoActual &&
                t.Año == añoActual
            );

            if (tutoria == null) return null;

            return db.Usuarios.Find(tutoria.IdUsuario);
        }
    }

    public class PsicologoCardViewModel
    {
        public int IdPsicologo { get; set; }
        public string NombreCompleto { get; set; }
        public bool EsRecomendado { get; set; }

        public string Horario { get; set; }
        public List<AreaAtencionViewModel> Areas { get; set; }
    }

    public class AreaAtencionViewModel
    {
        public string NombreArea { get; set; }
        public List<string> Detalles { get; set; }
    }
}