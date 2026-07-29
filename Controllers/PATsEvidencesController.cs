using Microsoft.Reporting.Common;
using Microsoft.Reporting.WebForms;
using Newtonsoft.Json;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesPAT;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Models.ClasesPAT;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using Plataforma_Web.Models.MongoDB;
using PlataformaWeb.Services;
using System.Threading.Tasks;


namespace Plataforma_Web.Controllers
{
    public class PATsEvidencesController : Controller
    {
        private readonly MongoDBService _mongoService = new MongoDBService();




        // --- INICIO: SOLICITUD 2 (APROBAR/RECHAZAR EVIDENCIA) ---

        // POST: PATsEvidences/AprobarEvidencia
        [HttpPost]
        public async Task<ActionResult> AprobarEvidencia(string evidenciaId)
        {
            try
            {
                // --- INICIO: VERIFICACIÓN DE ROL (¡NUEVO!) ---
                Usuario usuario = Session["Usuario"] as Usuario;
                // Solo Nivel 3 (Coordinador) y 4 (Master) pueden aprobar
                if (usuario == null || (usuario.IdNivel != 3 && usuario.IdNivel != 4))
                {
                    return Json(new { success = false, message = "Acceso no autorizado." });
                }
                // --- FIN: VERIFICACIÓN DE ROL ---

                // NOTA: Debes implementar "ActualizarEstadoAprobacionAsync" en tu MongoDBService.
                // Esta función debe buscar por evidenciaId y hacer un $set de { estadoAprobacion: 1 }
                var ok = await _mongoService.ActualizarEstadoAprobacionAsync(evidenciaId, 1); // 1 = Aprobado
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PATsEvidences/RechazarEvidencia
        [HttpPost]
        public async Task<ActionResult> RechazarEvidencia(string evidenciaId)
        {
            try
            {
                // --- INICIO: VERIFICACIÓN DE ROL (¡NUEVO!) ---
                Usuario usuario = Session["Usuario"] as Usuario;
                // Solo Nivel 3 (Coordinador) y 4 (Master) pueden rechazar
                if (usuario == null || (usuario.IdNivel != 3 && usuario.IdNivel != 4))
                {
                    return Json(new { success = false, message = "Acceso no autorizado." });
                }
                // --- FIN: VERIFICACIÓN DE ROL ---

                // NOTA: Debes implementar "ActualizarEstadoAprobacionAsync" en tu MongoDBService.
                // Esta función debe buscar por evidenciaId y hacer un $set de { estadoAprobacion: 2 }
                var ok = await _mongoService.ActualizarEstadoAprobacionAsync(evidenciaId, 2); // 2 = Rechazado
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        // --- FIN: SOLICITUD 2 ---
        // GET: PATsEvidences/Preview?patId=123&semana=5
        [HttpGet]
        public async Task<ActionResult> Preview(int patId, int semana)
        {
            // Buscar evidencia por PAT y semana usando metadata.semana
            string semanaStr = $"Semana {semana}";
            var evidencia = await _mongoService.ObtenerEvidenciaPorPATySemanaAsync(patId, semanaStr);
            if (evidencia != null && !string.IsNullOrEmpty(evidencia.RutaArchivo))
            {
                var url = Url.Content(evidencia.RutaArchivo);
                return Json(new { success = true, url }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { success = false }, JsonRequestBehavior.AllowGet);
        }

        // POST: PATsEvidences/Subir
        [HttpPost]
        public async Task<ActionResult> Subir(int patId, int ActividadId, int semana, int tutorId, string periodo, string ano, string grupo, string tipoTutoria, System.Web.HttpPostedFileBase pdf)
        {
            if (pdf == null || pdf.ContentLength == 0 || !pdf.FileName.ToLower().EndsWith(".pdf"))
                return Json(new { success = false, message = "Archivo inválido. Solo PDF." });

            try
            {
                // 1. Carpeta por PAT
                var evidenciasPath = $"/Content/EvidenciasPAT/PAT_{patId}/Semana_{semana}/";
                var serverPath = Server.MapPath(evidenciasPath);
                if (!System.IO.Directory.Exists(serverPath))
                    System.IO.Directory.CreateDirectory(serverPath);

                // 2. Limitar a 3 archivos por semana
                var archivosExistentes = Directory.GetFiles(serverPath, "*.pdf");
                if (archivosExistentes.Length >= 3)
                    return Json(new { success = false, message = "Ya existen 3 evidencias para esta semana. Elimine alguna para subir otra." });

                var fileName = $"PAT_{patId}_Semana_{semana}_{DateTime.Now.Ticks}.pdf";
                var filePath = Path.Combine(serverPath, fileName);
                pdf.SaveAs(filePath);

                // Guardar en MongoDB
                var evidencia = new EvidenciaPAT
                {
                    PatId = patId,
                    ActividadId = ActividadId,
                    NombreArchivo = fileName,
                    RutaArchivo = evidenciasPath + fileName,
                    TamanoArchivo = pdf.ContentLength,
                    Estado = "active",
                    FechaSubida = DateTime.UtcNow,
                    FechaCreacion = DateTime.UtcNow,
                    TutorId = tutorId,
                    TipoTutoria = tipoTutoria,
                    Metadata = new EvidenciaMetadata
                    {
                        Periodo = periodo,
                        Ano = ano,
                        Grupo = grupo,
                        Semana = $"Semana {semana}",
                        TipoTutoria = tipoTutoria
                    },
                    EstadoAprobacion = 0
                };
                await _mongoService.CrearEvidenciaAsync(evidencia);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: PATsEvidences/Listar?patId=123&semana=5
        [HttpGet]
        public async Task<ActionResult> Listar(int patId)
        {
            // Buscar todas las evidencias activas para ese PAT
            var evidencias = await _mongoService.ObtenerEvidenciasPorPATAsync(patId);
            System.Diagnostics.Debug.WriteLine($"[PATsEvidencesController.Listar] patId={patId} - Evidencias encontradas: {evidencias.Count}");
            foreach (var e in evidencias)
            {
                System.Diagnostics.Debug.WriteLine($"  - Evidencia: id={e.Id}, actividadId={e.ActividadId}, semana={(e.Metadata != null ? e.Metadata.Semana : "null")}, tipoTutoria={(string.IsNullOrEmpty(e.TipoTutoria) ? (e.Metadata != null ? e.Metadata.TipoTutoria : "null") : e.TipoTutoria)}, nombre={e.NombreArchivo}");
            }
            var lista = evidencias.Select(e => new {
                id = e.Id,
                nombre = e.NombreArchivo,
                url = Url.Content(e.RutaArchivo),
                fecha = e.FechaSubida,
                semana = (e.Metadata != null ? e.Metadata.Semana : null),
                tipoTutoria = !string.IsNullOrEmpty(e.TipoTutoria) ? e.TipoTutoria : (e.Metadata != null ? e.Metadata.TipoTutoria : null),
                actividadId = e.ActividadId,
                estadoAprobacion = e.EstadoAprobacion
            }).ToList();
            System.Diagnostics.Debug.WriteLine($"[PATsEvidencesController.Listar] Respuesta enviada: {lista.Count} evidencias");
            return Json(new { success = true, evidencias = lista }, JsonRequestBehavior.AllowGet);
        }

        // POST: PATsEvidences/Eliminar
        // POST: PATsEvidences/Eliminar
        [HttpPost]
        public async Task<ActionResult> Eliminar(string evidenciaId)
        {
            try
            {
                // 1. Buscar el registro en MongoDB para obtener la ruta del archivo
                var evidencia = await _mongoService.ObtenerEvidenciaPorIdAsync(evidenciaId);
                if (evidencia != null && !string.IsNullOrEmpty(evidencia.RutaArchivo))
                {
                    // 2. Borrar el archivo físico del servidor
                    var serverPath = Server.MapPath(evidencia.RutaArchivo);
                    if (System.IO.File.Exists(serverPath))
                    {
                        System.IO.File.Delete(serverPath);
                    }
                }

                // 3. Borrar el registro de MongoDB
                var ok = await _mongoService.EliminarEvidenciaAsync(evidenciaId);
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
