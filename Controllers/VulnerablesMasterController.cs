using Plataforma_Web.Models;
using PlataformaWeb;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

public class VulnerablesMasterController : Controller
{
    private ModeloPlataforma db = new ModeloPlataforma();

    // GET: Vulnerables/Index
    public ActionResult Index(
        string search = "",
        int? carrera = null,
        int? grupo = null,
        int? cuatrimestre = null,
        int? turno = null,
        int? tipoVulnerabilidad = null,
        int page = 1, int pageSize = 50)
    {
        var usuario = Session["Usuario"] as Usuario;

        // Validar acceso - Los estudiantes (IdNivel = 1) no pueden acceder
        if (usuario == null || usuario.IdNivel == 1)
        {
            return new HttpStatusCodeResult(403, "Acceso denegado");
        }

        bool esTutor = usuario.IdNivel == 2;
        bool esCoordinador = usuario.IdNivel == 3;
        bool esMaster = usuario.IdNivel == 4;
        int idUsuarioActual = usuario.IdUsuario;

        try
        {
            // 1. Obtener todas las entrevistas con vulnerabilidad
            var entrevistasVulnerables = db.EntrevistaInicials
                .Where(e => e.IdVulnerable == 1
                    && e.IdEleccionVunerabilidad != 4
                    && e.IdEleccionVunerabilidad != 0)
                .ToList();

            // 2. Obtener todas las asignaciones de tutoría según el nivel
            var tutoriaAsignaciones = db.TutoriaGrupals.AsQueryable();

            if (esTutor)
            {
                // Tutor: solo sus grupos asignados
                tutoriaAsignaciones = tutoriaAsignaciones.Where(tg => tg.IdUsuario == idUsuarioActual);
            }
            else if (esCoordinador)
            {
                // Coordinador: usar directamente su IdCarrera de la tabla Usuarios
                var carreraCoordinador = usuario.IdCarrera;

                if (carreraCoordinador > 0)
                {
                    // Filtrar solo por LA carrera del coordinador
                    tutoriaAsignaciones = tutoriaAsignaciones.Where(tg => tg.IdCarrera == carreraCoordinador);

                    // Guardar la carrera del coordinador para mostrar en la vista
                    var nombreCarrera = db.Carreras
                        .Where(c => c.IdCarrera == carreraCoordinador)
                        .Select(c => c.Nombre)
                        .FirstOrDefault();
                    ViewBag.CarreraCoordinador = nombreCarrera ?? "Carrera no encontrada";
                }
                else
                {
                    // Si no tiene carrera asignada, no mostrar nada
                    tutoriaAsignaciones = tutoriaAsignaciones.Where(tg => false);
                    ViewBag.CarreraCoordinador = "Sin carrera asignada";
                }
            }
            // Master: ve todo, no se filtra

            var asignaciones = tutoriaAsignaciones.ToList();

            // 3. Hacer el join en memoria y obtener solo los años más recientes
            var vulnerablesConAsignacion = (
                from e in entrevistasVulnerables
                join tg in asignaciones
                    on new { e.IdGrupo, e.IdGrado, e.IdCarrera, e.IdTurno }
                    equals new { tg.IdGrupo, tg.IdGrado, tg.IdCarrera, tg.IdTurno }
                select new
                {
                    Entrevista = e,
                    Tutoria = tg
                }
            ).ToList();

            // 4. Obtener el año más reciente por matrícula
            var ultimosAniosPorMatricula = vulnerablesConAsignacion
                .GroupBy(x => x.Entrevista.Matricula)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Tutoria.Año));

            // 5. Filtrar solo los registros del año más reciente
            var vulnerablesFiltrados = vulnerablesConAsignacion
                .Where(x => ultimosAniosPorMatricula.ContainsKey(x.Entrevista.Matricula) &&
                           ultimosAniosPorMatricula[x.Entrevista.Matricula] == x.Tutoria.Año)
                .ToList();

            // 6. Obtener datos adicionales para la vista
            var carreras = db.Carreras.ToDictionary(c => c.IdCarrera, c => c.Nombre);
            var grupos = db.Grupoes.ToDictionary(g => g.IdGrupo, g => g.Nombre);
            var grados = db.Gradoes.ToDictionary(gr => gr.IdGrado, gr => gr.Nombre);
            var turnos = db.Turnoes.ToDictionary(t => t.IdTurno, t => t.Nombre);
            var vulnerabilidades = db.Vulnerable.ToDictionary(v => v.IdEleccionVunerabilidad, v => v.Nombre);

            // 7. Crear el ViewModel
            var vulnerablesViewModel = vulnerablesFiltrados.Select(x => new
            {
                IdEntrevistaInicial = x.Entrevista.IdEntrevistaInicial,
                Matricula = x.Entrevista.Matricula,
                Nombre = x.Entrevista.Nombre,
                Carrera = carreras.ContainsKey(x.Entrevista.IdCarrera) ? carreras[x.Entrevista.IdCarrera] : "No registrado",
                Grupo = grupos.ContainsKey(x.Entrevista.IdGrupo) ? grupos[x.Entrevista.IdGrupo] : "No registrado",
                Cuatrimestre = grados.ContainsKey(x.Entrevista.IdGrado) ? grados[x.Entrevista.IdGrado] : "No registrado",
                Turno = turnos.ContainsKey(x.Entrevista.IdTurno) ? turnos[x.Entrevista.IdTurno] : "No registrado",
                IdVulnerable = x.Entrevista.IdVulnerable,
                NombreVulnerabilidad = vulnerabilidades.ContainsKey(x.Entrevista.IdEleccionVunerabilidad)
                    ? vulnerabilidades[x.Entrevista.IdEleccionVunerabilidad] : "No registrado",
                Año = x.Tutoria.Año,
                IdCarrera = x.Entrevista.IdCarrera,
                IdGrupo = x.Entrevista.IdGrupo,
                IdGrado = x.Entrevista.IdGrado,
                IdTurno = x.Entrevista.IdTurno
            }).ToList();

            // 8. Aplicar filtros adicionales
            var vulnerablesParaFiltrar = vulnerablesViewModel.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                vulnerablesParaFiltrar = vulnerablesParaFiltrar.Where(e =>
                    (e.Matricula ?? "").ToLower().Contains(search.ToLower()) ||
                    (e.Nombre ?? "").ToLower().Contains(search.ToLower())
                );
            }

            if (carrera.HasValue && carrera > 0)
                vulnerablesParaFiltrar = vulnerablesParaFiltrar.Where(e => e.IdCarrera == carrera);

            if (grupo.HasValue && grupo > 0)
                vulnerablesParaFiltrar = vulnerablesParaFiltrar.Where(e => e.IdGrupo == grupo);

            if (cuatrimestre.HasValue && cuatrimestre > 0)
                vulnerablesParaFiltrar = vulnerablesParaFiltrar.Where(e => e.IdGrado == cuatrimestre);

            if (turno.HasValue && turno > 0)
                vulnerablesParaFiltrar = vulnerablesParaFiltrar.Where(e => e.IdTurno == turno);

            if (tipoVulnerabilidad.HasValue && tipoVulnerabilidad > 0)
            {
                var nombreVulnerabilidad = vulnerabilidades.ContainsKey(tipoVulnerabilidad.Value)
                    ? vulnerabilidades[tipoVulnerabilidad.Value] : "";
                vulnerablesParaFiltrar = vulnerablesParaFiltrar.Where(e => e.NombreVulnerabilidad == nombreVulnerabilidad);
            }

            // 9. Ordenar y paginar
            var vulnerablesFiltradosList = vulnerablesParaFiltrar.ToList();
            var totalRegistros = vulnerablesFiltradosList.Count();
            var vulnerables = vulnerablesFiltradosList
                .OrderBy(e => e.Nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AlumnoVulnerableViewModel
                {
                    IdEntrevistaInicial = x.IdEntrevistaInicial,
                    Matricula = x.Matricula,
                    Nombre = x.Nombre,
                    Carrera = x.Carrera,
                    Grupo = x.Grupo,
                    Cuatrimestre = x.Cuatrimestre,
                    Turno = x.Turno,
                    IdVulnerable = x.IdVulnerable,
                    NombreVulnerabilidad = x.NombreVulnerabilidad,
                    Anio = x.Año,
                })
                .ToList();

            // 10. Preparar dropdowns según el nivel del usuario
            PrepararDropdowns(usuario, idUsuarioActual);

            // 11. Mantener valores de filtros actuales
            ViewBag.CurrentCarrera = carrera;
            ViewBag.CurrentGrupo = grupo;
            ViewBag.CurrentCuatrimestre = cuatrimestre;
            ViewBag.CurrentTurno = turno;
            ViewBag.CurrentTipoVulnerabilidad = tipoVulnerabilidad;

            // 12. Información de paginación
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRegistros / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.TotalRegistros = totalRegistros;

            // 13. Información del usuario para la vista
            ViewBag.NivelUsuario = usuario.IdNivel;
            ViewBag.NombreUsuario = usuario.NombreCompleto;

            return View(vulnerables);
        }
        catch (Exception ex)
        {
            // Log del error para debugging
            System.Diagnostics.Debug.WriteLine($"Error en Index: {ex.Message}");

            // Retornar vista vacía en caso de error
            ViewBag.Error = "Ocurrió un error al cargar los datos. Por favor, contacte al administrador.";
            ViewBag.TotalRegistros = 0;
            ViewBag.TotalPages = 1;
            ViewBag.CurrentPage = 1;
            ViewBag.PageSize = pageSize;
            ViewBag.NivelUsuario = usuario?.IdNivel ?? 0;
            ViewBag.NombreUsuario = usuario?.NombreCompleto ?? "Usuario desconocido";

            PrepararDropdowns(usuario, idUsuarioActual);

            return View(new List<AlumnoVulnerableViewModel>());
        }
    }

    private void PrepararDropdowns(Usuario usuario, int idUsuarioActual)
    {
        if (usuario == null) return;

        bool esTutor = usuario.IdNivel == 2;
        bool esCoordinador = usuario.IdNivel == 3;

        if (esTutor)
        {
            // Tutor: solo sus carreras, grupos, cuatrimestres y turnos asignados
            var asignacionesTutor = db.TutoriaGrupals.Where(tg => tg.IdUsuario == idUsuarioActual).ToList();

            var carrerasIds = asignacionesTutor.Select(tg => tg.IdCarrera).Distinct().ToList();
            var gruposIds = asignacionesTutor.Select(tg => tg.IdGrupo).Distinct().ToList();
            var cuatrimestresIds = asignacionesTutor.Select(tg => tg.IdGrado).Distinct().ToList();
            var turnosIds = asignacionesTutor.Select(tg => tg.IdTurno).Distinct().ToList();

            ViewBag.Carreras = new SelectList(
                db.Carreras.Where(c => carrerasIds.Contains(c.IdCarrera)).OrderBy(c => c.Nombre),
                "IdCarrera", "Nombre");
            ViewBag.Grupos = new SelectList(
                db.Grupoes.Where(g => gruposIds.Contains(g.IdGrupo)).OrderBy(g => g.Nombre),
                "IdGrupo", "Nombre");
            ViewBag.Cuatrimestres = new SelectList(
                db.Gradoes.Where(gr => cuatrimestresIds.Contains(gr.IdGrado)).OrderBy(gr => gr.Nombre),
                "IdGrado", "Nombre");
            ViewBag.Turnos = new SelectList(
                db.Turnoes.Where(t => turnosIds.Contains(t.IdTurno)).OrderBy(t => t.Nombre),
                "IdTurno", "Nombre");
        }
        else if (esCoordinador)
        {
            // Coordinador: usar directamente su IdCarrera de la tabla Usuarios
            var carreraCoordinador = usuario.IdCarrera;

            if (carreraCoordinador > 0)
            {
                // Solo mostrar SU carrera (una de las 9 carreras de la tabla)
                ViewBag.Carreras = new SelectList(
                    db.Carreras.Where(c => c.IdCarrera == carreraCoordinador).OrderBy(c => c.Nombre),
                    "IdCarrera", "Nombre");

                // Obtener todos los grupos, cuatrimestres y turnos de SU carrera
                var asignacionesCarrera = db.TutoriaGrupals.Where(tg => tg.IdCarrera == carreraCoordinador).ToList();

                var gruposIds = asignacionesCarrera.Select(tg => tg.IdGrupo).Distinct().ToList();
                var cuatrimestresIds = asignacionesCarrera.Select(tg => tg.IdGrado).Distinct().ToList();
                var turnosIds = asignacionesCarrera.Select(tg => tg.IdTurno).Distinct().ToList();

                ViewBag.Grupos = new SelectList(
                    db.Grupoes.Where(g => gruposIds.Contains(g.IdGrupo)).OrderBy(g => g.Nombre),
                    "IdGrupo", "Nombre");
                ViewBag.Cuatrimestres = new SelectList(
                    db.Gradoes.Where(gr => cuatrimestresIds.Contains(gr.IdGrado)).OrderBy(gr => gr.Nombre),
                    "IdGrado", "Nombre");
                ViewBag.Turnos = new SelectList(
                    db.Turnoes.Where(t => turnosIds.Contains(t.IdTurno)).OrderBy(t => t.Nombre),
                    "IdTurno", "Nombre");
            }
            else
            {
                // Si no tiene carrera asignada, listas vacías
                ViewBag.Carreras = new SelectList(new List<object>(), "IdCarrera", "Nombre");
                ViewBag.Grupos = new SelectList(new List<object>(), "IdGrupo", "Nombre");
                ViewBag.Cuatrimestres = new SelectList(new List<object>(), "IdGrado", "Nombre");
                ViewBag.Turnos = new SelectList(new List<object>(), "IdTurno", "Nombre");
            }
        }
        else
        {
            // Master: todas las opciones (las 9 carreras completas)
            ViewBag.Carreras = new SelectList(db.Carreras.OrderBy(c => c.Nombre), "IdCarrera", "Nombre");
            ViewBag.Grupos = new SelectList(db.Grupoes.OrderBy(g => g.Nombre), "IdGrupo", "Nombre");
            ViewBag.Cuatrimestres = new SelectList(db.Gradoes.OrderBy(g => g.Nombre), "IdGrado", "Nombre");
            ViewBag.Turnos = new SelectList(db.Turnoes.OrderBy(t => t.Nombre), "IdTurno", "Nombre");
        }

        // Tipos de vulnerabilidad para todos los niveles
        ViewBag.TiposVulnerabilidad = new SelectList(
            db.Vulnerable.OrderBy(v => v.Nombre),
            "IdEleccionVunerabilidad", "Nombre");
    }

    // POST: Vulnerables/QuitarVulnerabilidad
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult QuitarVulnerabilidad(int id)
    {
        var usuario = Session["Usuario"] as Usuario;
        if (usuario == null || usuario.IdNivel == 1)
        {
            return new HttpStatusCodeResult(403, "Acceso denegado");
        }

        var alumno = db.EntrevistaInicials.Find(id);
        if (alumno != null)
        {
            alumno.IdVulnerable = 2;
            alumno.IdEleccionVunerabilidad = 4;
            alumno.EleccionVulnerabilidad = "No vulnerable";
            db.SaveChanges();
        }
        return RedirectToAction("Index");
    }

    // GET: Vulnerables/Edit/5
    public ActionResult Edit(int id)
    {
        var usuario = Session["Usuario"] as Usuario;
        if (usuario == null || usuario.IdNivel == 1)
        {
            return new HttpStatusCodeResult(403, "Acceso denegado");
        }

        var alumno = db.EntrevistaInicials.Find(id);
        if (alumno == null)
            return HttpNotFound();

        var opcionesVulnerable = db.Vulnerabilidads
            .Select(v => new SelectListItem
            {
                Value = v.IdVulnerabilidad.ToString(),
                Text = v.TipoVulnerabilidad
            }).ToList();

        var tiposVulnerabilidad = db.Vulnerable
            .Select(v => new SelectListItem
            {
                Value = v.IdEleccionVunerabilidad.ToString(),
                Text = v.Nombre
            }).ToList();

        var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == alumno.IdCarrera)?.Nombre ?? "No registrado";
        var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == alumno.IdGrupo)?.Nombre ?? "No registrado";
        var grado = db.Gradoes.FirstOrDefault(gr => gr.IdGrado == alumno.IdGrado)?.Nombre ?? "No registrado";
        var turno = db.Turnoes.FirstOrDefault(t => t.IdTurno == alumno.IdTurno)?.Nombre ?? "No registrado";

        var viewModel = new AlumnoVulnerableViewModel
        {
            IdEntrevistaInicial = alumno.IdEntrevistaInicial,
            Matricula = alumno.Matricula,
            Nombre = alumno.Nombre,
            Carrera = carrera,
            Grupo = grupo,
            Cuatrimestre = grado,
            Turno = turno,
            IdVulnerable = alumno.IdVulnerable,
            OpcionesVulnerable = opcionesVulnerable,
            IdEleccionVunerabilidad = alumno.IdEleccionVunerabilidad,
            TiposVulnerabilidad = tiposVulnerabilidad,
            NombreVulnerabilidad = db.Vulnerable
                .Where(v => v.IdEleccionVunerabilidad == alumno.IdEleccionVunerabilidad)
                .Select(v => v.Nombre)
                .FirstOrDefault() ?? "No definido"
        };

        return View(viewModel);
    }

    // POST: Vulnerables/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Edit(AlumnoVulnerableViewModel model)
    {
        var usuario = Session["Usuario"] as Usuario;
        if (usuario == null || usuario.IdNivel == 1)
        {
            return new HttpStatusCodeResult(403, "Acceso denegado");
        }

        if (!ModelState.IsValid)
        {
            model.OpcionesVulnerable = db.Vulnerabilidads
                .Select(v => new SelectListItem
                {
                    Value = v.IdVulnerabilidad.ToString(),
                    Text = v.TipoVulnerabilidad
                }).ToList();

            model.TiposVulnerabilidad = db.Vulnerable
                .Select(v => new SelectListItem
                {
                    Value = v.IdEleccionVunerabilidad.ToString(),
                    Text = v.Nombre
                }).ToList();

            return View(model);
        }

        var alumno = db.EntrevistaInicials.Find(model.IdEntrevistaInicial);
        if (alumno == null)
            return HttpNotFound();

        alumno.IdVulnerable = model.IdVulnerable;

        if (model.IdVulnerable == 2)
        {
            alumno.IdEleccionVunerabilidad = 4;
            alumno.EleccionVulnerabilidad = "No vulnerable";
        }
        else
        {
            alumno.IdEleccionVunerabilidad = model.IdEleccionVunerabilidad;
            alumno.EleccionVulnerabilidad = db.Vulnerable
                .Where(v => v.IdEleccionVunerabilidad == model.IdEleccionVunerabilidad)
                .Select(v => v.Nombre)
                .FirstOrDefault();
        }

        db.SaveChanges();
        return RedirectToAction("Index");
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
