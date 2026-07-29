using Plataforma_Web.Models;
using PlataformaWeb;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PlataformaWeb.Models;
using PlataformaWeb.Models.Psicologia;

namespace Plataforma_Web.Controllers
{
    [CustomAuthorize(Nivel = 2)]
    public class CanalizacionesController : Controller
    {
        private readonly ModeloPlataforma db = new ModeloPlataforma();

        public ActionResult HistorialCanalizaciones(int? idCarrera = null, int? idGrupo = null, int? mes = null, int? anio = null)
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null) return RedirectToAction("Login", "Account");

            if (usuario.IdNivel < 3)
            {
                return RedirectToAction("Grupo", "Entrevistas");
            }

            var model = new HistorialDashboardViewModel();
            model.Mes = mes;
            model.Anio = anio;

            var carrerasQuery = db.Carreras.OrderBy(c => c.Nombre).AsQueryable();

            if (usuario.IdNivel == 3)
            {
                carrerasQuery = carrerasQuery.Where(c => c.IdCarrera == usuario.IdCarrera);
                idCarrera = usuario.IdCarrera;
            }

            model.ListaCarreras = carrerasQuery.ToList()
                .Select(c => new SelectListItem { Value = c.IdCarrera.ToString(), Text = c.Nombre })
                .ToList();

            if (usuario.IdNivel == 4)
            {
                model.ListaCarreras.Insert(0, new SelectListItem { Value = "", Text = "Todas las carreras" });
            }

            model.IdCarrera = idCarrera;

            if (idCarrera.HasValue)
            {
                int periodo = ObtenerPeriodoActual();
                int year = DateTime.Now.Year;

                var grupos = db.TutoriaGrupals
                    .Where(t => t.IdCarrera == idCarrera.Value && t.IdPeriodo == periodo && t.Año == year)
                    .ToList()
                    .Select(t => new { t.IdTutoriaGrupal, Nombre = FormatearNombreGrupo(t.IdGrupo, t.IdGrado, t.IdTurno) })
                    .OrderBy(g => g.Nombre)
                    .ToList();

                model.ListaGrupos = grupos.Select(g => new SelectListItem { Value = g.IdTutoriaGrupal.ToString(), Text = g.Nombre }).ToList();
                model.ListaGrupos.Insert(0, new SelectListItem { Value = "", Text = "Todos los grupos" });
                model.IdGrupo = idGrupo;
            }

            var query = from c in db.Canalizaciones
                        join dp in db.DatosPersonales on c.IdPersona equals dp.IdPersona
                        join tc in db.TipoCanalizaciones on c.IdTipoCanalizacion equals tc.IdTipoCanalizacion into tcJoin
                        from tc in tcJoin.DefaultIfEmpty()
                        join ps in db.Psicologos on c.IdPsicologo equals ps.IdPsicologo into psJoin
                        from ps in psJoin.DefaultIfEmpty()
                        join car in db.Carreras on dp.IdCarrera equals car.IdCarrera

                        join gr in db.Gradoes on dp.IdGrado equals gr.IdGrado into grJoin
                        from gr in grJoin.DefaultIfEmpty()
                        join gp in db.Grupoes on dp.IdGrupo equals gp.IdGrupo into gpJoin
                        from gp in gpJoin.DefaultIfEmpty()
                        join tu in db.Turnoes on dp.IdTurno equals tu.IdTurno into tuJoin
                        from tu in tuJoin.DefaultIfEmpty()

                        join u in db.Usuarios on c.IdUsuario equals u.IdUsuario into uJoin
                        from u in uJoin.DefaultIfEmpty()

                        select new { c, dp, tc, ps, car, gr, gp, tu, u };

            if (idCarrera.HasValue) query = query.Where(x => x.dp.IdCarrera == idCarrera.Value);

            if (idGrupo.HasValue)
            {
                var grupoInfo = db.TutoriaGrupals.Find(idGrupo.Value);
                if (grupoInfo != null)
                {
                    query = query.Where(x => x.dp.IdGrado == grupoInfo.IdGrado &&
                                             x.dp.IdGrupo == grupoInfo.IdGrupo &&
                                             x.dp.IdTurno == grupoInfo.IdTurno);
                }
            }

            if (anio.HasValue) query = query.Where(x => x.c.Fecha.Year == anio.Value);
            if (mes.HasValue) query = query.Where(x => x.c.Fecha.Month == mes.Value);


            var dataList = query.ToList();
            model.TotalCanalizaciones = dataList.Count;

            model.TotalPersonal = dataList.Count(x => x.tc != null && x.tc.Descripcion.ToLower().Contains("personal"));

            model.TotalAcademica = dataList.Count(x => x.tc != null && x.tc.Descripcion.ToLower().Contains("acad"));

            model.TotalSocioeconomica = dataList.Count(x => x.tc != null && x.tc.Descripcion.ToLower().Contains("socioecon"));

            model.TotalIniciativa = dataList.Count(x => x.tc != null && x.tc.Descripcion.ToLower().Contains("iniciativa"));

            model.Resultados = dataList.OrderByDescending(x => x.c.Fecha)
    .Select(x => new CanalizacionNotaViewModel
    {
        Id = x.c.IdCanalizacion,
        Fecha = x.c.Fecha,
        NombreEstudiante = HttpUtility.HtmlDecode(x.dp.Nombre),
        Carrera = HttpUtility.HtmlDecode(x.car.Nombre),
        Grupo = ((x.gr != null ? x.gr.Nombre : "") + (x.gp != null ? x.gp.Nombre : "") + " - " + (x.tu != null ? x.tu.Nombre : "")).Trim(),
        Tipo = HttpUtility.HtmlDecode(x.tc != null ? x.tc.Descripcion : "Sin tipo"),

        Matricula = x.dp.Matricula ?? "S/N",
        Turno = x.tu != null ? x.tu.Nombre : "No especificado",
        Email = x.dp.Email ?? "No registrado",
        Celular = x.dp.Celular ?? "No registrado",

        MotivoCanalizacion = HttpUtility.HtmlDecode(x.c.MotivoCanalizacion ?? ""),

        CorreoTutor = HttpUtility.HtmlDecode(x.c.CorreoTutor ?? ""),
        TutorFullName = HttpUtility.HtmlDecode(x.u != null ? x.u.NombreCompleto : "No identificado"),
        TutorUsername = x.u != null ? x.u.CorreoElectronico : (x.c.CorreoTutor ?? ""),
        Status = x.c.Status ?? "Tutor",

        NombrePsicologo = HttpUtility.HtmlDecode(x.ps != null ? x.ps.NombreCompleto : "No asignado")
    }).ToList();

            var mesesDict = new Dictionary<int, string> { { 1, "Enero" }, { 2, "Febrero" }, { 3, "Marzo" }, { 4, "Abril" }, { 5, "Mayo" }, { 6, "Junio" }, { 7, "Julio" }, { 8, "Agosto" }, { 9, "Septiembre" }, { 10, "Octubre" }, { 11, "Noviembre" }, { 12, "Diciembre" } };
            model.ListaMeses = mesesDict.Select(m => new SelectListItem { Value = m.Key.ToString(), Text = m.Value }).ToList();
            model.ListaAnios = Enumerable.Range(DateTime.Now.Year - 5, 6).OrderByDescending(y => y).Select(y => new SelectListItem { Value = y.ToString(), Text = y.ToString() }).ToList();

            return View(model);
        }

        [HttpPost]
        public JsonResult EliminarCanalizacion(int id)
        {
            try
            {
                var canalizacion = db.Canalizaciones.Find(id);
                if (canalizacion == null)
                {
                    return Json(new { success = false, message = "La canalización no existe." });
                }

                db.Canalizaciones.Remove(canalizacion);
                db.SaveChanges();

                return Json(new { success = true, message = "Canalización eliminada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetGruposPorCarrera(int idCarrera)
        {
            int periodo = ObtenerPeriodoActual();
            int year = DateTime.Now.Year;

            var grupos = db.TutoriaGrupals
                .Where(t => t.IdCarrera == idCarrera && t.IdPeriodo == periodo && t.Año == year)
                .ToList()
                .Select(t => new {
                    Value = t.IdTutoriaGrupal,
                    Text = FormatearNombreGrupo(t.IdGrupo, t.IdGrado, t.IdTurno)
                })
                .OrderBy(g => g.Text)
                .ToList();

            return Json(grupos, JsonRequestBehavior.AllowGet);
        }

        public ActionResult HistorialGrupo(int? idGrupo = null)
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int periodoActual = ObtenerPeriodoActual();
            int añoActual = DateTime.Now.Year;

            var tutoriasQuery = db.TutoriaGrupals
                .Where(x => x.IdPeriodo == periodoActual && x.Año == añoActual);

            if (usuario.IdNivel == 2)
            {
                tutoriasQuery = tutoriasQuery.Where(x => x.IdUsuario == usuario.IdUsuario);
            }

            var tutorias = tutoriasQuery.ToList();

            if (!tutorias.Any())
            {
                ViewBag.Mensaje = "No se encontraron grupos asignados en el periodo actual.";
                return View("HistorialGrupo", new List<CanalizacionNotaViewModel>());
            }

            var gruposDropdown = tutorias.Select(t => new {
                t.IdGrupo,
                NombreGrupo = FormatearNombreGrupo(t.IdGrupo, t.IdGrado, t.IdTurno)
            }).Distinct().OrderBy(g => g.NombreGrupo).ToList();

            ViewBag.Grupos = new SelectList(gruposDropdown, "IdGrupo", "NombreGrupo", idGrupo ?? tutorias.First().IdGrupo);

            var selectedGroupInfo = idGrupo.HasValue
                ? tutorias.FirstOrDefault(t => t.IdGrupo == idGrupo)
                : tutorias.First();

            if (selectedGroupInfo == null)
            {
                if (idGrupo.HasValue && usuario.IdNivel > 2)
                {
                    selectedGroupInfo = db.TutoriaGrupals.Where(t => t.IdTutoriaGrupal == idGrupo.Value).FirstOrDefault();
                    if (selectedGroupInfo == null)
                    {
                        ViewBag.Mensaje = "El grupo seleccionado no es válido o no existe.";
                        return View("HistorialGrupo", new List<CanalizacionNotaViewModel>());
                    }
                }
                else
                {
                    ViewBag.Mensaje = "El grupo seleccionado no es válido.";
                    return View("HistorialGrupo", new List<CanalizacionNotaViewModel>());
                }
            }

            ViewBag.GrupoNombre = FormatearNombreGrupo(selectedGroupInfo.IdGrupo, selectedGroupInfo.IdGrado, selectedGroupInfo.IdTurno);

            var studentIds = db.DatosPersonales
                .Where(x => x.IdCarrera == selectedGroupInfo.IdCarrera && x.IdGrado == selectedGroupInfo.IdGrado &&
                            x.IdGrupo == selectedGroupInfo.IdGrupo && x.IdTurno == selectedGroupInfo.IdTurno &&
                            x.IdPeriodo == selectedGroupInfo.IdPeriodo && x.Año == selectedGroupInfo.Año)
                .Select(s => s.IdPersona)
                .ToList();

            var historialQuery =
                from c in db.Canalizaciones
                join dp in db.DatosPersonales on c.IdPersona equals dp.IdPersona
                join car in db.Carreras on dp.IdCarrera equals car.IdCarrera
                join u in db.Usuarios on c.IdUsuario equals u.IdUsuario into tutorJoin
                from tj in tutorJoin.DefaultIfEmpty()
                join tc in db.TipoCanalizaciones on c.IdTipoCanalizacion equals tc.IdTipoCanalizacion into tcJoin
                from tcn in tcJoin.DefaultIfEmpty()
                join ps in db.Psicologos on c.IdPsicologo equals ps.IdPsicologo into psJoin
                from ps in psJoin.DefaultIfEmpty()
                join gr in db.Gradoes on dp.IdGrado equals gr.IdGrado into grJoin
                from g in grJoin.DefaultIfEmpty()
                join gp in db.Grupoes on dp.IdGrupo equals gp.IdGrupo into gpJoin
                from p in gpJoin.DefaultIfEmpty()
                join tu in db.Turnoes on dp.IdTurno equals tu.IdTurno into tuJoin
                from t in tuJoin.DefaultIfEmpty()
                where studentIds.Contains(c.IdPersona)
                select new CanalizacionNotaViewModel
                {
                    Id = c.IdCanalizacion,
                    Fecha = c.Fecha,
                    MotivoCanalizacion = c.MotivoCanalizacion ?? "Sin motivo",
                    Tipo = tcn != null ? tcn.Descripcion : "Sin tipo",
                    NombrePsicologo = ps != null ? ps.NombreCompleto : "No asignado",
                    CorreoTutor = c.CorreoTutor ?? "No identificado",
                    TutorFullName = tj != null ? tj.NombreCompleto : "Tutor no disponible",
                    NombreEstudiante = dp.Nombre,
                    Carrera = car.Nombre,
                    Matricula = dp.Matricula,
                    Grupo = (g != null ? g.Nombre : "") + " " + (p != null ? p.Nombre : ""),
                    Especialidad = dp.Especialidad,
                    Turno = t != null ? t.Nombre : "No especificado",
                    Email = dp.Email,
                    Celular = dp.Celular
                };

            var historial = historialQuery.OrderByDescending(c => c.Fecha).ToList();

            historial.ForEach(item => {
                item.NombreEstudiante = HttpUtility.HtmlDecode(item.NombreEstudiante);
                item.Carrera = HttpUtility.HtmlDecode(item.Carrera);
                item.NombrePsicologo = HttpUtility.HtmlDecode(item.NombrePsicologo);
            });

            return View("HistorialGrupo", historial);
        }

        public ActionResult IndexC(int? idGrupo = null)
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                Debug.WriteLine("IndexC: Session expired, redirecting to login.");
                return RedirectToAction("Login", "Account");
            }

            int periodoActual = ObtenerPeriodoActual();
            int añoActual = DateTime.Now.Year;

            var tutoriasQuery = db.TutoriaGrupals
                .Where(x => x.IdPeriodo == periodoActual && x.Año == añoActual);

            if (usuario.IdNivel == 2)
            {
                tutoriasQuery = tutoriasQuery.Where(x => x.IdUsuario == usuario.IdUsuario);
            }

            var tutorias = tutoriasQuery
                .Select(x => new
                {
                    x.IdTutoriaGrupal,
                    x.IdGrupo,
                    x.IdCarrera,
                    x.IdGrado,
                    x.IdTurno,
                    x.IdPeriodo,
                    x.Año
                })
                .ToList();

            if (!tutorias.Any())
            {
                Debug.WriteLine($"IndexC: No tutoria found for user {usuario.IdUsuario} (Nivel {usuario.IdNivel}).");
                ViewBag.Mensaje = "No se encontraron grupos asignados en el periodo actual.";
                ViewBag.Grupos = new SelectList(new List<SelectListItem>(), "Value", "Text");
                return View(new List<EstudianteCanalizacionViewModel>());
            }

            var grupos = tutorias.Select(t => new
            {
                IdGrupo = t.IdTutoriaGrupal,
                NombreGrupo = FormatearNombreGrupo(t.IdGrupo, t.IdGrado, t.IdTurno)
            }).Distinct().OrderBy(g => g.NombreGrupo).ToList();

            ViewBag.Grupos = new SelectList(
                grupos.Select(g => new SelectListItem
                {
                    Value = g.IdGrupo.ToString(),
                    Text = g.NombreGrupo
                }),
                "Value",
                "Text",
                idGrupo ?? tutorias.First().IdTutoriaGrupal
            );

            var selectedGroup = idGrupo.HasValue
                ? tutorias.FirstOrDefault(t => t.IdTutoriaGrupal == idGrupo)
                : tutorias.First();

            if (selectedGroup == null)
            {
                if (idGrupo.HasValue && usuario.IdNivel > 2)
                {
                    selectedGroup = db.TutoriaGrupals.Where(t => t.IdTutoriaGrupal == idGrupo.Value)
                                    .Select(x => new {
                                        x.IdTutoriaGrupal,
                                        x.IdGrupo,
                                        x.IdCarrera,
                                        x.IdGrado,
                                        x.IdTurno,
                                        x.IdPeriodo,
                                        x.Año
                                    }).FirstOrDefault();
                    if (selectedGroup == null)
                    {
                        Debug.WriteLine($"IndexC: No group found for IdGrupo={idGrupo}.");
                        ViewBag.Mensaje = "El grupo seleccionado no es válido.";
                        return View(new List<EstudianteCanalizacionViewModel>());
                    }
                }
                else
                {
                    Debug.WriteLine($"IndexC: No group found for IdGrupo={idGrupo}.");
                    ViewBag.Mensaje = "El grupo seleccionado no es válido.";
                    return View(new List<EstudianteCanalizacionViewModel>());
                }
            }

            var estudiantes = db.DatosPersonales
                .Where(x =>
                    x.IdCarrera == selectedGroup.IdCarrera &&
                    x.IdGrado == selectedGroup.IdGrado &&
                    x.IdGrupo == selectedGroup.IdGrupo &&
                    x.IdTurno == selectedGroup.IdTurno &&
                    x.IdPeriodo == selectedGroup.IdPeriodo &&
                    x.Año == selectedGroup.Año)
                .OrderBy(x => x.Nombre)
                .Select(d => new
                {
                    d.IdPersona,
                    d.Matricula,
                    Nombre = d.Nombre,
                    CanalizacionCompleta = db.EntrevistaInicials.Any(e => e.IdPersona == d.IdPersona),
                    Vulnerable = db.EntrevistaInicials.Any(e => e.IdPersona == d.IdPersona && e.IdVulnerable == 1),
                    TieneFoto = d.Foto != null
                })
                .ToList()
                .Select(d => new EstudianteCanalizacionViewModel
                {
                    IdPersona = d.IdPersona,
                    Matricula = d.Matricula,
                    Nombre = HttpUtility.HtmlDecode(d.Nombre),
                    CanalizacionCompleta = d.CanalizacionCompleta,
                    Vulnerable = d.Vulnerable,
                    TieneFoto = d.TieneFoto
                })
                .ToList();

            ViewBag.NombreGrupo = FormatearNombreGrupo(selectedGroup.IdGrupo, selectedGroup.IdGrado, selectedGroup.IdTurno);
            Debug.WriteLine($"IndexC: Loaded {estudiantes.Count} students for group {ViewBag.NombreGrupo}.");
            return View(estudiantes);
        }

        public ActionResult Vulnerabilidades(int id)
        {
            var alumno = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == id);
            if (alumno == null)
            {
                return HttpNotFound();
            }

            var historial = (from c in db.Canalizaciones
                             join u in db.Usuarios on c.IdUsuario equals u.IdUsuario into tutorJoin
                             from tj in tutorJoin.DefaultIfEmpty()
                             join ps in db.Psicologos on c.IdPsicologo equals ps.IdPsicologo into psJoin
                             from ps in psJoin.DefaultIfEmpty()
                             where c.IdPersona == id
                             orderby c.Fecha descending
                             select new
                             {
                                 MotivoCanalizacion = c.MotivoCanalizacion ?? "Sin motivo",
                                 VulnerabilidadesPasadas = c.VulnerabilidadesPasadas,
                                 Tipo = c.TipoCanalizaciones != null ? c.TipoCanalizaciones.Descripcion : "Sin tipo",
                                 CorreoTutor = c.CorreoTutor ?? "No identificado",
                                 TutorFullName = tj != null ? tj.NombreCompleto : "Tutor no disponible",
                                 NombrePsicologo = ps != null ? ps.NombreCompleto : "No asignado",
                                 c.Fecha
                             })
                                .ToList()
                                .Select(c => new CanalizacionNotaViewModel
                                {
                                    MotivoCanalizacion = HttpUtility.HtmlDecode(c.MotivoCanalizacion),
                                    VulnerabilidadesPasadas = HttpUtility.HtmlDecode(c.VulnerabilidadesPasadas),
                                    Tipo = HttpUtility.HtmlDecode(c.Tipo),
                                    CorreoTutor = HttpUtility.HtmlDecode(c.CorreoTutor),
                                    TutorFullName = HttpUtility.HtmlDecode(c.TutorFullName),
                                    NombrePsicologo = HttpUtility.HtmlDecode(c.NombrePsicologo),
                                    Fecha = c.Fecha
                                })
                                .ToList();

            var tipos = db.TipoCanalizaciones.OrderBy(t => t.Descripcion)
                .ToList()
                .Select(t => new SelectListItem { Value = t.IdTipoCanalizacion.ToString(), Text = HttpUtility.HtmlDecode(t.Descripcion) })
                .ToList();

            var psicologos = db.Psicologos
                .Where(p => p.Activo)
                .OrderBy(p => p.NombreCompleto)
                .ToList()
                .Select(p => new SelectListItem
                {
                    Value = p.IdPsicologo.ToString(),
                    Text = HttpUtility.HtmlDecode(p.NombreCompleto)
                })
                .ToList();

            var modelo = new VulnerabilidadViewModel
            {
                IdPersona = alumno.IdPersona,
                Nombre = HttpUtility.HtmlDecode(alumno.Nombre),
                Matricula = alumno.Matricula,
                Grupo = $"{(db.Gradoes.Find(alumno.IdGrado)?.Nombre ?? "")} {(db.Grupoes.Find(alumno.IdGrupo)?.Nombre ?? "")}",
                Carrera = HttpUtility.HtmlDecode(db.Carreras.Find(alumno.IdCarrera)?.Nombre ?? ""),
                Especialidad = HttpUtility.HtmlDecode(alumno.Especialidad ?? "No disponible"),
                Turno = HttpUtility.HtmlDecode(db.Turnoes.Find(alumno.IdTurno)?.Nombre ?? "No especificado"),
                Email = HttpUtility.HtmlDecode(alumno.Email ?? "No disponible"),
                Celular = HttpUtility.HtmlDecode(alumno.Celular ?? "No disponible"),
                Historial = historial,
                TiposCanalizacion = tipos,
                Psicologos = psicologos
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarVulnerabilidad(VulnerabilidadViewModel modelo)
        {
            if (modelo == null || !ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Datos del formulario inválidos";
                return RedirectToAction("Vulnerabilidades", new { id = modelo?.IdPersona ?? 0 });
            }

            var usuarioSesion = Session["Usuario"] as Usuario;
            if (usuarioSesion == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var tutorDelAlumno = ObtenerTutorDelAlumno(modelo.IdPersona);
                if (tutorDelAlumno == null)
                {
                    TempData["ErrorMessage"] = "Error: El alumno no tiene un tutor asignado en el periodo actual. No se pudo guardar la canalización.";
                    return RedirectToAction("Vulnerabilidades", new { id = modelo.IdPersona });
                }

                int newCanalizacionId = 0;

                using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["ModeloPlataforma"].ConnectionString))
                {
                    connection.Open();

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO Canalizaciones 
                        (IdPersona, IdUsuario, CorreoTutor, MotivoCanalizacion, VulnerabilidadesPasadas, IdTipoCanalizacion, Fecha, IdPsicologo, Status) 
                        VALUES 
                        (@IdPersona, @IdUsuario, @CorreoTutor, @MotivoCanalizacion, @VulnerabilidadesPasadas, @IdTipoCanalizacion, @Fecha, @IdPsicologo, 'Tutor');
                        
                        SELECT SCOPE_IDENTITY();", connection))
                    {
                        cmd.Parameters.AddWithValue("@IdPersona", modelo.IdPersona);

                        cmd.Parameters.AddWithValue("@IdUsuario", tutorDelAlumno.IdUsuario);
                        cmd.Parameters.AddWithValue("@CorreoTutor", tutorDelAlumno.CorreoElectronico ?? (object)DBNull.Value);

                        cmd.Parameters.AddWithValue("@MotivoCanalizacion", modelo.MotivoCanalizacion ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdTipoCanalizacion", modelo.IdTipoCanalizacion);
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);


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
                        using (var cmd = new SqlCommand("TransferirCanalizacionesToCitas", connection) { CommandType = CommandType.StoredProcedure })
                        {
                            cmd.Parameters.AddWithValue("@IdCanalizacion", newCanalizacionId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        throw new Exception("No se pudo obtener el ID de la nueva canalización.");
                    }
                }
                TempData["SuccessMessage"] = "Canalización enviada correctamente";
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error inesperado: {ex.Message}";
                errorMessage = errorMessage.Replace("'", "").Replace("\"", "").Replace("\r", " ").Replace("\n", " ");

                TempData["ErrorMessage"] = errorMessage;
            }

            return RedirectToAction("Vulnerabilidades", new { id = modelo.IdPersona });
        }

        public ActionResult ObtenerFoto(int id)
        {
            Debug.WriteLine($"ObtenerFoto: Loading photo for IdPersona={id}.");
            var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (alumno?.Foto != null)
            {
                if (alumno.Foto.StartsWith("data:image"))
                {
                    var base64Data = alumno.Foto.Split(',')[1];
                    var bytes = Convert.FromBase64String(base64Data);
                    Debug.WriteLine($"ObtenerFoto: Returning base64 image for IdPersona={id}.");
                    return File(bytes, "image/jpeg");
                }
                else if (alumno.Foto.Length > 0)
                {
                    Debug.WriteLine($"ObtenerFoto: Returning raw image for IdPersona={id}.");
                    return File(System.Text.Encoding.Default.GetBytes(alumno.Foto), "image/jpeg");
                }
            }

            Debug.WriteLine($"ObtenerFoto: Returning default image for IdPersona={id}.");
            byte[] imagenDefault = System.IO.File.ReadAllBytes(Server.MapPath("~/css/estudiantes/default.png"));
            return File(imagenDefault, "image/png");
        }

        public ActionResult EscribirCanalizacion(int id)
        {
            var alumno = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == id);
            if (alumno == null)
            {
                return HttpNotFound();
            }

            var usuarioSesion = Session["Usuario"] as Usuario;
            if (usuarioSesion == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var tutorDelAlumno = ObtenerTutorDelAlumno(id);
            if (tutorDelAlumno == null)
            {
                TempData["ErrorMessage"] = "Error: El alumno no tiene un tutor asignado en el periodo actual. No se puede canalizar.";
                return RedirectToAction("Vulnerabilidades", new { id = id });
            }

            var vulnerabilidadesPasadas = db.Database.SqlQuery<SelectListItemRaw>(
                    @"WITH RankedCanalizaciones AS (
                        SELECT IdCanalizacion, IdTipoCanalizacion, Fecha, CAST(MotivoCanalizacion AS NVARCHAR(MAX)) AS Motivo,
                                ROW_NUMBER() OVER (PARTITION BY CAST(MotivoCanalizacion AS NVARCHAR(MAX)) ORDER BY Fecha DESC) AS RN
                        FROM Canalizaciones WHERE IdPersona = @IdPersona
                    )
                    SELECT c.IdCanalizacion AS Value, CAST(COALESCE((SELECT Descripcion FROM TipoCanalizaciones WHERE IdTipoCanalizacion = c.IdTipoCanalizacion), 'Sin tipo') + ' - ' + CONVERT(VARCHAR, c.Fecha, 103) + ' - ' +
                            CASE WHEN c.Motivo IS NULL THEN 'Sin motivo' WHEN LEN(c.Motivo) > 30 THEN LEFT(c.Motivo, 30) + '...' ELSE c.Motivo END AS NVARCHAR(MAX)) AS Text
                    FROM RankedCanalizaciones c WHERE c.RN = 1", new SqlParameter("@IdPersona", id))
                    .ToList()
                    .Select(r => new SelectListItem { Value = r.Value.ToString(), Text = HttpUtility.HtmlDecode(r.Text) }).ToList();

            var tipos = db.TipoCanalizaciones.OrderBy(t => t.Descripcion)
                .ToList()
                .Select(t => new SelectListItem { Value = t.IdTipoCanalizacion.ToString(), Text = HttpUtility.HtmlDecode(t.Descripcion) })
                .ToList();

            var psicologos = db.Psicologos
                .Where(p => p.Activo)
                .OrderBy(p => p.NombreCompleto)
                .ToList()
                .Select(p => new SelectListItem
                {
                    Value = p.IdPsicologo.ToString(),
                    Text = HttpUtility.HtmlDecode(p.NombreCompleto)
                })
                .ToList();

            var carrera = db.Carreras.Find(alumno.IdCarrera)?.Nombre ?? "No disponible";
            var grado = db.Gradoes.Find(alumno.IdGrado)?.Nombre ?? "";
            var grupo = db.Grupoes.Find(alumno.IdGrupo)?.Nombre ?? "";
            var turno = db.Turnoes.Find(alumno.IdTurno)?.Nombre ?? "No especificado";

            var modelo = new CanalizacionViewModel
            {
                IdPersona = alumno.IdPersona,
                Nombre = HttpUtility.HtmlDecode(alumno.Nombre ?? "No disponible"),
                Matricula = alumno.Matricula ?? "No disponible",
                Carrera = HttpUtility.HtmlDecode(carrera),
                Especialidad = HttpUtility.HtmlDecode(alumno.Especialidad ?? "No disponible"),
                Grupo = $"{grado} {grupo}".Trim(),
                Turno = turno,
                IdTurnoAlumno = alumno.IdTurno,
                Email = alumno.Email ?? "No disponible",
                Celular = alumno.Celular ?? "No disponible",

                TutorFullName = HttpUtility.HtmlDecode(tutorDelAlumno.NombreCompleto ?? "No disponible"),
                TutorUsername = tutorDelAlumno.CorreoElectronico ?? "No disponible",

                IdTutorAsignado = tutorDelAlumno.IdUsuario,
                CorreoTutorAsignado = tutorDelAlumno.CorreoElectronico,

                TiposCanalizacion = tipos,
                Psicologos = psicologos,
                PastVulnerabilities = vulnerabilidadesPasadas,
                MotivoCanalizacion = "",
                IdTipoCanalizacion = 0
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EscribirCanalizacion(CanalizacionViewModel modelo)
        {
            if (modelo == null || !ModelState.IsValid)
            {
                modelo.TiposCanalizacion = db.TipoCanalizaciones.OrderBy(t => t.Descripcion)
                    .ToList()
                    .Select(t => new SelectListItem { Value = t.IdTipoCanalizacion.ToString(), Text = HttpUtility.HtmlDecode(t.Descripcion) })
                    .ToList();
                return Json(new { success = false, message = "Datos del formulario inválidos." });
            }

            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return Json(new { success = false, message = "Sesión expirada." });
            }

            try
            {
                string fechaStr = Request.Form["fechaRegistro"];
                if (!DateTime.TryParseExact(fechaStr, "dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaRegistro))
                {
                    return Json(new { success = false, message = "Formato de fecha inválido." });
                }

                bool yaExisteHoy = db.Canalizaciones.Any(c =>
                    c.IdPersona == modelo.IdPersona &&
                    c.Fecha.Year == fechaRegistro.Year &&
                    c.Fecha.Month == fechaRegistro.Month &&
                    c.Fecha.Day == fechaRegistro.Day);

                if (yaExisteHoy)
                {
                    return Json(new { success = false, message = "Aviso: Ya se ha registrado una canalización para este alumno el día de hoy. Solo se permite una diaria." });
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

                        cmd.Parameters.AddWithValue("@IdUsuario", modelo.IdTutorAsignado);
                        cmd.Parameters.AddWithValue("@CorreoTutor", (object)modelo.CorreoTutorAsignado ?? DBNull.Value);

                        cmd.Parameters.AddWithValue("@MotivoCanalizacion", (object)modelo.MotivoCanalizacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@VulnerabilidadesPasadas", (object)textoVulnerabilidadPasada ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdTipoCanalizacion", modelo.IdTipoCanalizacion);
                        cmd.Parameters.AddWithValue("@Fecha", fechaRegistro);
                        cmd.Parameters.AddWithValue("@Status", "Tutor");

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
                        using (var cmd = new SqlCommand("TransferirCanalizacionesToCitas", connection) { CommandType = CommandType.StoredProcedure })
                        {
                            cmd.Parameters.AddWithValue("@IdCanalizacion", newCanalizacionId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        throw new Exception("No se pudo obtener el ID de la nueva canalización.");
                    }
                }

                return Json(new { success = true, message = "Canalización enviada correctamente." });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error al guardar: {ex.Message}";
                errorMessage = errorMessage.Replace("'", "").Replace("\"", "").Replace("\r", " ").Replace("\n", " ");

                return Json(new
                {
                    success = false,
                    message = errorMessage
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult GetCanalizacionDetails(int id)
        {
            var canalizacion = db.Canalizaciones
                .Where(c => c.IdCanalizacion == id)
                .Select(c => new
                {
                    c.IdTipoCanalizacion,
                    c.IdPsicologo,
                    Motivo = c.MotivoCanalizacion
                })
                .FirstOrDefault();

            if (canalizacion == null)
            {
                return Json(new { error = "Canalización no encontrada" }, JsonRequestBehavior.AllowGet);
            }
            var result = new
            {
                canalizacion.IdTipoCanalizacion,
                canalizacion.IdPsicologo,
                Notas = HttpUtility.HtmlDecode(canalizacion.Motivo)
            };
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetPsicologosPorTipo(int tipoId)
        {
            try
            {
                var psicologos = db.Psicologos
                    .Where(p => p.Activo)
                    .Include(p => p.PsicologoTurno)
                    .Include(p => p.Psicologo_PsiDetalles.Select(pd => pd.PsiDetalleAtencion.PsiAreaAtencion))
                    .ToList();

                var viewModel = psicologos.Select(p => new PsicologoCardViewModel
                {
                    IdPsicologo = p.IdPsicologo,
                    NombreCompleto = HttpUtility.HtmlDecode(p.NombreCompleto),
                    Horario = p.PsicologoTurno != null
                ? (p.PsicologoTurno.Nombre.Trim().Equals("Mixto", StringComparison.OrdinalIgnoreCase)
                    ? "Matutino y Vespertino" // Reemplazo
                    : p.PsicologoTurno.Nombre) // Original (Matutino/Vespertino)
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


        private string ObtenerNombreTutor(int idUsuario)
        {
            try
            {
                using (var db = new ModeloPlataforma())
                {
                    var tutor = db.Usuarios.Find(idUsuario);
                    Debug.WriteLine($"ObtenerNombreTutor: Found tutor {tutor?.NombreCompleto} for IdUsuario={idUsuario}.");
                    return tutor?.NombreCompleto ?? "Tutor no identificado";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ObtenerNombreTutor: Error - {ex.Message}, IdUsuario={idUsuario}.");
                return "Tutor no identificado";
            }
        }
        private int ObtenerPeriodoActual()
        {
            int mesActual = DateTime.Now.Month;
            if (mesActual >= 1 && mesActual <= 4) return 1;
            if (mesActual >= 5 && mesActual <= 8) return 2;
            return 3;
        }
        private string FormatearNombreGrupo(int idGrupo, int idGrado, int idTurno)
        {
            var turno = db.Turnoes.Find(idTurno)?.Nombre ?? "Sin turno";
            var grado = db.Gradoes.Find(idGrado)?.Nombre ?? "";
            var grupo = db.Grupoes.Find(idGrupo)?.Nombre ?? "";
            return $"{grado}{grupo} - {turno}";
        }

        public class EstudianteCanalizacionViewModel
        {
            public int IdPersona { get; set; }
            public string Matricula { get; set; }
            public string Nombre { get; set; }
            public bool CanalizacionCompleta { get; set; }
            public bool Vulnerable { get; set; }
            public bool TieneFoto { get; set; }
        }

        public class VulnerabilidadViewModel
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; }
            public string Matricula { get; set; }
            public string Grupo { get; set; }
            public string Carrera { get; set; }
            public string Especialidad { get; set; }
            public string Turno { get; set; }
            public string Email { get; set; }
            public string Celular { get; set; }

            [Required(ErrorMessage = "El motivo es requerido")]
            public string MotivoCanalizacion { get; set; }
            [Required(ErrorMessage = "Debe seleccionar un tipo")]
            public int IdTipoCanalizacion { get; set; }
            public List<SelectListItem> TiposCanalizacion { get; set; }
            public List<CanalizacionNotaViewModel> Historial { get; set; }

            public int? IdPsicologo { get; set; }
            public List<SelectListItem> Psicologos { get; set; }
        }

        public class CanalizacionNotaViewModel
        {
            public int Id { get; set; }
            public string MotivoCanalizacion { get; set; }
            public string VulnerabilidadesPasadas { get; set; }
            public string Tipo { get; set; }
            public DateTime Fecha { get; set; }
            public string NombrePsicologo { get; set; }
            public string CorreoTutor { get; set; }
            public string TutorFullName { get; set; }
            public string Status { get; set; }
            public string TutorUsername { get; set; }
            public int IdCarrera { get; set; }
            public string NombreEstudiante { get; set; }
            public string Carrera { get; set; }
            public string Matricula { get; set; }
            public string Grupo { get; set; }
            public string Especialidad { get; set; }
            public string Turno { get; set; }
            public string Email { get; set; }
            public string Celular { get; set; }
        }

        public class CanalizacionViewModel
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; }
            public string Matricula { get; set; }
            public string Grupo { get; set; }
            public string Especialidad { get; set; }
            public string TutorUsername { get; set; }
            public string TutorFullName { get; set; }
            public string Titulo { get; set; }
            [Required(ErrorMessage = "El motivo es requerido.")]
            public string MotivoCanalizacion { get; set; }
            [Required(ErrorMessage = "Debe seleccionar un tipo de canalización.")]
            [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tipo válido.")]
            public int IdTipoCanalizacion { get; set; }

            public int? IdPsicologo { get; set; }
            public List<SelectListItem> Psicologos { get; set; }

            public List<SelectListItem> TiposCanalizacion { get; set; }
            public List<SelectListItem> PastVulnerabilities { get; set; }
            public int? SelectedPastVulnerabilityId { get; set; }
            public string Turno { get; set; }
            public int IdTurnoAlumno { get; set; }
            public string Email { get; set; }
            public string Celular { get; set; }
            public string Carrera { get; internal set; }

            public int IdTutorAsignado { get; set; }
            public string CorreoTutorAsignado { get; set; }
        }

        private class SelectListItemRaw
        {
            public int Value { get; set; }
            public string Text { get; set; }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private Usuario ObtenerTutorDelAlumno(int idPersona)
        {
            var alumno = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == idPersona);
            if (alumno == null)
            {
                return null;
            }

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

            if (tutoria == null)
            {
                return null;
            }

            return db.Usuarios.Find(tutoria.IdUsuario);
        }

        public class HistorialDashboardViewModel
        {
            public int? IdCarrera { get; set; }
            public int? IdGrupo { get; set; }
            public int? Mes { get; set; }
            public int? Anio { get; set; }

            public List<SelectListItem> ListaCarreras { get; set; }
            public List<SelectListItem> ListaGrupos { get; set; }
            public List<SelectListItem> ListaMeses { get; set; }
            public List<SelectListItem> ListaAnios { get; set; }

            public int TotalCanalizaciones { get; set; }
            public int TotalPersonal { get; set; }
            public int TotalAcademica { get; set; }
            public int TotalSocioeconomica { get; set; }
            public int TotalIniciativa { get; set; }

            public List<CanalizacionNotaViewModel> Resultados { get; set; }

            public HistorialDashboardViewModel()
            {
                ListaCarreras = new List<SelectListItem>();
                ListaGrupos = new List<SelectListItem>();
                Resultados = new List<CanalizacionNotaViewModel>();
            }
        }


    }
}