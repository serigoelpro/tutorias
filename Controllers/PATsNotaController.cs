using Plataforma_Web.Models;
using Plataforma_Web.Models.MongoDB;
using PlataformaWeb.Services;
using PlataformaWeb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers
{
    public class PATsNotaController : Controller
    {
        private readonly MongoDBService _mongoService = new MongoDBService();
        private ModeloPlataforma db = new ModeloPlataforma();

        // POST: PATsNota/AgregarNota
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AgregarNota(int idPat, string nota)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nota))
                {
                    return Json(new { success = false, message = "La nota no puede estar vacía." });
                }

                if (nota.Length > 500)
                {
                    return Json(new { success = false, message = "La nota no puede exceder 500 caracteres." });
                }

                // Obtener información del usuario actual
                var usuarioSesion = Session["Usuario"] as Usuario;
                if (usuarioSesion == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado." });
                }

                var usuarioActual = usuarioSesion.NombreCompleto ?? usuarioSesion.UserName ?? "Usuario desconocido";
                // Aseguramos que UsuarioId sea igual a User.Identity.Name
                var usuarioId = User.Identity.Name ?? usuarioSesion.UserName ?? "unknown";

                // Obtener información adicional del PAT para metadata
                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == idPat);
                if (pat == null)
                {
                    return Json(new { success = false, message = "PAT no encontrado." });
                }

                // Obtener información adicional para metadata
                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                var periodo = db.Periodos.FirstOrDefault(p => p.IdPeriodo == pat.IdPeriodo);

                // Obtener información del usuario para metadata
                var roles = new List<string>();
                if (User.IsInRole("Master")) roles.Add("Master");
                if (User.IsInRole("Coordinador")) roles.Add("Coordinador");
                if (User.IsInRole("Tutor")) roles.Add("Tutor");

                var rolActual = roles.FirstOrDefault() ?? "Usuario";

                // Crear la nota
                var nuevaNota = new NotaPAT
                {
                    PatId = idPat,
                    UsuarioId = usuarioId,
                    Usuario = usuarioActual,
                    Comentario = nota.Trim(),
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "activo",
                    Metadata = new ComentarioMetadata
                    {
                        Periodo = periodo?.Nombre ?? "Sin período",
                        Ano = pat.Fecha.Year.ToString(),
                        Grupo = pat.Grupo ?? "Sin grupo",
                        Rol = rolActual,
                        IpAddress = Request.UserHostAddress
                    }
                };

                // Guardar en MongoDB
                var notaId = await _mongoService.CrearNotaAsync(nuevaNota);

                return Json(new { success = true, message = "Nota agregada correctamente.", notaId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno del servidor: " + ex.Message });
            }
        }

        // GET: PATsNota/ObtenerNotas
        [HttpGet]
        public async Task<ActionResult> ObtenerNotas(int idPat)
        {
            try
            {
                var notas = await _mongoService.ObtenerNotasPorPATAsync(idPat);

                var notasResponse = notas.Select(c => new
                {
                    Id = c.Id,
                    Usuario = c.Usuario,
                    UsuarioId = c.UsuarioId,
                    Comentario = c.Comentario,
                    FechaCreacion = c.FechaCreacion.ToString("yyyy-MM-ddTHH:mm:ssZ"), // Formato ISO para JavaScript
                    Rol = c.Metadata?.Rol
                }).ToList();

                return Json(new { success = true, notas = notasResponse }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al obtener notas: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: PATsNota/EliminarNota
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EliminarNota(string notaId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(notaId))
                {
                    return Json(new { success = false, message = "ID de nota inválido." });
                }

                // Verificar que la nota existe y pertenece al usuario actual
                var nota = await _mongoService.ObtenerNotaPorIdAsync(notaId);
                if (nota == null)
                {
                    return Json(new { success = false, message = "Nota no encontrada." });
                }

                // Verificar permisos: solo el autor o un Master puede eliminar
                if (nota.UsuarioId != User.Identity.Name && !User.IsInRole("Master"))
                {
                    return Json(new { success = false, message = "No tiene permisos para eliminar esta nota." });
                }

                var resultado = await _mongoService.EliminarNotaAsync(notaId);

                if (resultado)
                {
                    return Json(new { success = true, message = "Nota eliminada correctamente." });
                }
                else
                {
                    return Json(new { success = false, message = "No se pudo eliminar la nota." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar nota: " + ex.Message });
            }
        }

        // POST: PATsNota/EditarNota
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditarNota(string notaId, string nuevaNota)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(notaId) || string.IsNullOrWhiteSpace(nuevaNota))
                {
                    return Json(new { success = false, message = "Parámetros inválidos." });
                }

                if (nuevaNota.Length > 500)
                {
                    return Json(new { success = false, message = "La nota no puede exceder 500 caracteres." });
                }

                // Verificar que la nota existe y pertenece al usuario actual
                var nota = await _mongoService.ObtenerNotaPorIdAsync(notaId);
                if (nota == null)
                {
                    return Json(new { success = false, message = "Nota no encontrada." });
                }

                // Verificar permisos: solo el autor puede editar
                if (nota.UsuarioId != User.Identity.Name)
                {
                    return Json(new { success = false, message = "No tiene permisos para editar esta nota." });
                }

                var resultado = await _mongoService.ActualizarNotaAsync(notaId, nuevaNota.Trim());

                if (resultado)
                {
                    return Json(new { success = true, message = "Nota actualizada correctamente." });
                }
                else
                {
                    return Json(new { success = false, message = "No se pudo actualizar la nota." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al editar nota: " + ex.Message });
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
}