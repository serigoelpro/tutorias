using Microsoft.Reporting.WebForms;
using Plataforma_Web.Data;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using PlataformaWeb;
using ProyectoIntegracion.Models.GestionUsuarios;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.Asesorar
{
    public class GruposGeneralesController : Controller
    {
        private TutoriasContext tutoriasDb = new TutoriasContext();
        private GestionUsuariosContext usuariosDb = new GestionUsuariosContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = Session["Usuario"] as Usuario;

            // ❌ Bloquear si es estudiante (1) o tutor (2), solo permitir nivel 3 y 4
            if (usuario == null || usuario.IdNivel < 3)
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // GET: GruposGenerales - Vista general de todos los grupos con tutores asignados
        public ActionResult Index(int? idCarrera, int? año, int? periodo)
        {
            try
            {
                var user = Session["Usuario"] as Usuario;

                // Validación adicional de usuario
                if (user == null)
                {
                    ViewBag.Error = "Sesión expirada. Por favor, inicie sesión nuevamente.";
                    return View(new List<GrupoConTutorViewModel>());
                }

                // Base: todos los grupos con tutores asignados
                var gruposQuery = tutoriasDb.TutoriaGrupals
                    .Where(x => x.IdUsuario != null);

                // Filtro por carrera según el nivel del usuario
                if (user.IdNivel == 3)
                {
                    // Coordinador: solo grupos de su carrera
                    gruposQuery = gruposQuery.Where(x => x.IdCarrera == user.IdCarrera);
                }
                else if (user.IdNivel == 4)
                {
                    // Master: puede filtrar por carrera si se especifica
                    if (idCarrera.HasValue && idCarrera.Value > 0)
                    {
                        gruposQuery = gruposQuery.Where(x => x.IdCarrera == idCarrera.Value);
                    }
                }

                // Filtros por año y período (disponibles para nivel 3 y 4)
                if (año.HasValue && año.Value > 0)
                {
                    gruposQuery = gruposQuery.Where(x => x.Año == año.Value);
                }

                if (periodo.HasValue && periodo.Value > 0)
                {
                    gruposQuery = gruposQuery.Where(x => x.IdPeriodo == periodo.Value);
                }

                // JOIN con usuarios para obtener información del tutor
                var gruposConTutores = gruposQuery
                    .Join(tutoriasDb.Usuarios,
                        grupo => grupo.IdUsuario,
                        usuario => usuario.IdUsuario,
                        (grupo, usuario) => new GrupoConTutorViewModel
                        {
                            IdTutoriaGrupal = grupo.IdTutoriaGrupal,
                            IdGrado = grupo.IdGrado,
                            IdGrupo = grupo.IdGrupo,
                            IdCarrera = grupo.IdCarrera,
                            IdTurno = grupo.IdTurno,
                            IdPeriodo = grupo.IdPeriodo,
                            Año = grupo.Año,
                            IdUsuario = grupo.IdUsuario,
                            NombreTutor = usuario.NombreCompleto,
                            UserNameTutor = usuario.UserName,
                            EstadoTutor = usuario.Estado
                        })
                    .ToList();

                // Diccionarios Id -> Nombre para mostrar en la vista
                ViewBag.Grados = tutoriasDb.Gradoes
                    .AsNoTracking()
                    .ToDictionary(g => g.IdGrado, g => g.Nombre);

                ViewBag.Grupos = tutoriasDb.Grupoes
                    .AsNoTracking()
                    .ToDictionary(g => g.IdGrupo, g => g.Nombre);

                ViewBag.Carreras = tutoriasDb.Carreras
                    .AsNoTracking()
                    .ToDictionary(c => c.IdCarrera, c => c.Nombre);

                ViewBag.Turnos = tutoriasDb.Turnoes
                    .AsNoTracking()
                    .ToDictionary(t => t.IdTurno, t => t.Nombre);

                ViewBag.Periodos = new Dictionary<int, string>
                {
                    { 1, "Enero - Abril" },
                    { 2, "Mayo - Agosto" },
                    { 3, "Septiembre - Diciembre" }
                };

                // Para que la vista pueda decidir qué mostrar
                ViewBag.UserNivel = user.IdNivel;
                ViewBag.UserCarrera = user.IdCarrera;

                // SelectList de carreras solo para usuarios Master (nivel 4)
                if (user.IdNivel == 4)
                {
                    ViewBag.CarrerasSelectList = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", idCarrera);
                    ViewBag.CarreraSeleccionada = idCarrera;
                }

                // SelectList de años (disponible para nivel 3 y 4)
                var añosDisponibles = tutoriasDb.TutoriaGrupals
                    .Where(x => x.Año > 0)
                    .Select(x => x.Año)
                    .Distinct()
                    .OrderByDescending(x => x)
                    .ToList();
                ViewBag.AñosSelectList = new SelectList(añosDisponibles, año);

                // SelectList de períodos (disponible para nivel 3 y 4)
                ViewBag.PeriodosSelectList = new SelectList(new List<object>
                {
                    new { Id = 1, Nombre = "Enero - Abril" },
                    new { Id = 2, Nombre = "Mayo - Agosto" },
                    new { Id = 3, Nombre = "Septiembre - Diciembre" }
                }, "Id", "Nombre", periodo);

                // Valores seleccionados para la vista
                ViewBag.AñoSeleccionado = año;
                ViewBag.PeriodoSeleccionado = periodo;

                return View(gruposConTutores);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los grupos: " + ex.Message;
                return View(new List<GrupoConTutorViewModel>());
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tutoriasDb?.Dispose();
                usuariosDb?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}