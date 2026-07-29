using Microsoft.Reporting.WebForms;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Data.SqlClient;

namespace PlataformaWeb.Controllers
{
    [CustomAuthorize(Nivel = 2)]
    public class BajasController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        [HttpGet]
        public JsonResult GetDatosAlumno(string matricula)
        {
            // Solo el Máster (Nivel 4) puede usar esta función
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null || usuario.IdNivel != 4)
            {
                return Json(new { Status = "Error", Mensaje = "Acceso denegado." }, JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrWhiteSpace(matricula))
            {
                return Json(new { Status = "Error", Mensaje = "La matrícula no puede estar vacía." }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var param = new SqlParameter("@Matricula", matricula);

                var datos = db.Database.SqlQuery<DatosAlumnoAjaxDto>("sp_GetDatosAlumnoPorMatricula @Matricula", param).FirstOrDefault();

                if (datos == null)
                {
                    return Json(new DatosAlumnoAjaxDto { Status = "Error", Mensaje = "Alumno no encontrado (datos nulos)." }, JsonRequestBehavior.AllowGet);
                }

                if (datos.Status == "Error")
                {
                    return Json(datos, JsonRequestBehavior.AllowGet);
                }

                return Json(datos, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = "Error", Mensaje = "Error en el servidor: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        // GET: Bajas
        public ActionResult Index(int id, bool fromReporte = false)
        {
            Usuario user = Session["Usuario"] as Usuario;
            if (user == null) return RedirectToAction("Login", "Account");

            // 1. Buscamos al alumno y la lista de bajas
            DatosPersonales datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (datos == null)
            {
                return HttpNotFound();
            }

            // CASO 1: ALUMNO (Nivel 1) - Solo puede verse a sí mismo
            if (user.IdNivel == 1)
            {
                // Comparamos la matrícula de la sesión con la matrícula del ID solicitado
                if (user.UserName != datos.Matricula)
                {
                    return Content("<script>alert('ACCESO DENEGADO: No puedes ver la información de otros alumnos.'); window.location.href = '/Home/Index';</script>");
                }
            }

            // CASO 2: COORDINADOR (Nivel 3) - Puede VER el historial de cualquier alumno.
            // La restricción de edición se aplica en la vista y en cada acción de modificación.
            else if (user.IdNivel == 3)
            {
                // Sin bloqueo en la vista; ViewBag.UserCarreraNombre controla los botones de edición.
            }

            // CASO 3: TUTOR (Nivel 2) - Solo alumnos de sus grupos asignados ACTIVOS
            else if (user.IdNivel == 2)
            {
                // Calculamos el periodo actual para validar (Usamos tu lógica de meses)
                var hoy = DateTime.Now;
                var periodoActual = 0;
                if (hoy.Month >= 1 && hoy.Month <= 4) periodoActual = 1;
                else if (hoy.Month >= 5 && hoy.Month <= 8) periodoActual = 2;
                else periodoActual = 3;

                // Verificamos si existe la relación Tutor-Grupo-Alumno
                bool esSuAlumno = db.TutoriaGrupals.Any(tg =>
                    tg.IdUsuario == user.IdUsuario &&       // Es este tutor
                    tg.IdCarrera == datos.IdCarrera &&      // Misma carrera
                    tg.IdGrado == datos.IdGrado &&          // Mismo grado
                    tg.IdGrupo == datos.IdGrupo &&          // Mismo grupo
                    tg.IdTurno == datos.IdTurno &&          // Mismo turno
                    tg.IdPeriodo == periodoActual &&        // Periodo actual
                    tg.Año == hoy.Year                      // Año actual
                );

                if (!esSuAlumno)
                {
                    return Content("<script>alert('ACCESO DENEGADO: Este alumno no pertenece a tus grupos de tutoría actuales.'); window.location.href = '/Home/Index';</script>");
                }
            }
            List<Baja> lista = db.Bajas.Where(x => x.IdPersona == id).ToList();
            ViewBag.Alumno = datos;

            var tiempo = DateTime.Now;
            var turn = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                turn = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                turn = 2;
            }
            else
            {
                turn = 3;
            }

            TutoriaGrupal tutgrup = new TutoriaGrupal();

            // Lógica original para obtener el ID del grupo para el botón "Regresar"
            if (user.IdNivel == 3)
            {
                tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == datos.IdPeriodo && x.Año == datos.Año);
            }
            else if (user.IdNivel == 2) // Nivel 2 (Tutor): busca el grupo del periodo actual en sus asignaciones
            {
                tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == turn && x.Año == DateTime.Now.Year && x.IdUsuario == user.IdUsuario);
            }

            int idtutgrup;
            if (tutgrup == null)
            {
                idtutgrup = -1;
            }
            else
            {
                idtutgrup = tutgrup.IdTutoriaGrupal;
            }

            ViewBag.FromReporte = fromReporte;
            ViewBag.Grupo = idtutgrup;
            ViewBag.Nivel = user.IdNivel;

            // Nombre de la carrera del coordinador para controlar botones de edición en la vista.
            // Los coordinadores solo pueden editar bajas de su propia carrera.
            if (user.IdNivel == 3)
            {
                string userCarreraNom = db.Carreras
                    .Where(c => c.IdCarrera == user.IdCarrera)
                    .Select(c => c.Nombre)
                    .FirstOrDefault() ?? "";
                ViewBag.UserCarreraNombre = userCarreraNom;
                ViewBag.EsMismaCarreraActual = (datos.IdCarrera == user.IdCarrera);
            }
            else
            {
                ViewBag.UserCarreraNombre = null;
                ViewBag.EsMismaCarreraActual = true;
            }

            return View(lista);
        }

        // GET: Bajas/Details/5
        public ActionResult Details(int? id, bool? sello)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var baja = db.Bajas.FirstOrDefault(x => x.IdBaja == id);
            if (baja == null) return HttpNotFound();

            Usuario userDet = Session["Usuario"] as Usuario;
            if (userDet == null) return RedirectToAction("Login", "Account");

            if (userDet.IdNivel == 3)
            {
                // Coordinador: puede ver la baja si su carrera coincide con la carrera actual
                // del alumno O con la carrera histórica guardada en el registro de baja.
                bool esMismaCarreraActual = db.DatosPersonales
                    .Any(p => p.IdPersona == baja.IdPersona && p.IdCarrera == userDet.IdCarrera);

                bool esMismaCarreraHistorica = !string.IsNullOrEmpty(baja.Carrera)
                    && db.Carreras.Any(c => c.IdCarrera == userDet.IdCarrera
                                         && c.Nombre.Trim() == baja.Carrera.Trim());

                if (!esMismaCarreraActual && !esMismaCarreraHistorica)
                    return Content("<script>alert('ACCESO DENEGADO: No tienes permisos para ver esta baja.'); window.location.href='/Bajas/ReporteBajas';</script>");

                // Solo lectura cuando el usuario es de la carrera nueva y la baja es de la carrera vieja.
                ViewBag.EsSoloLectura = baja.Reingreso == 1
                                        && esMismaCarreraActual
                                        && !esMismaCarreraHistorica;
            }
            else if (userDet.IdNivel == 2)
            {
                // Tutor: puede ver la baja si en algún momento fue tutor de ese alumno
                // (cualquier periodo, para permitir lectura histórica).
                bool esRelacionado = db.DatosPersonales
                    .Where(p => p.IdPersona == baja.IdPersona)
                    .Join(db.TutoriaGrupals,
                          p => new { p.IdCarrera, p.IdGrado, p.IdGrupo, p.IdTurno },
                          tg => new { tg.IdCarrera, tg.IdGrado, tg.IdGrupo, tg.IdTurno },
                          (p, tg) => tg.IdUsuario)
                    .Any(idU => idU == userDet.IdUsuario);

                if (!esRelacionado)
                    return Content("<script>alert('ACCESO DENEGADO: Este alumno no está asignado a tus grupos.'); window.location.href='/Bajas/ReporteBajas';</script>");
            }

            // Nivel 4 (Master) y Nivel 2 (Tutor): nunca solo lectura.
            if (ViewBag.EsSoloLectura == null)
                ViewBag.EsSoloLectura = false;

            if (!(bool)ViewBag.EsSoloLectura)
            {
                try
                {
                    var lista = new List<Baja> { baja };

                    var rv = new ReportViewer();
                    rv.ProcessingMode = ProcessingMode.Local;
                    rv.LocalReport.ReportPath = Server.MapPath("~/Reporte/rptBajaAlumno.rdlc");
                    rv.LocalReport.DataSources.Clear();
                    rv.LocalReport.DataSources.Add(new ReportDataSource("BajaAlumno", lista));
                    rv.LocalReport.EnableExternalImages = true;

                    rv.SizeToReportContent = true;
                    rv.Width = Unit.Percentage(100);
                    rv.Height = Unit.Pixel(900);
                    rv.ZoomMode = ZoomMode.PageWidth;
                    rv.AsyncRendering = false;

                    bool showSello = sello ?? false;
                    ViewBag.ShowSello = showSello;

                    try
                    {
                        string carreraNom = db.DatosPersonales
                                              .Where(p => p.IdPersona == baja.IdPersona)
                                              .Select(p => p.CarreraNom)
                                              .FirstOrDefault();

                        // Construye la URI de la imagen de sello para el reporte
                        string selloFile = GetSelloFileNameByNombreCarrera(carreraNom);
                        var selloPhysicalPath = Server.MapPath("~/Imagenes/" + selloFile);
                        string selloUrl = new Uri(selloPhysicalPath).AbsoluteUri;

                        // Construye la URI de la imagen de firma del director
                        var directorInfo = GetDirectorInfoPorCarrera(carreraNom);
                        string firmaFile = directorInfo.Item1;      // Nombre del archivo de la firma
                        string directorNombre = directorInfo.Item2; // Nombre completo del director

                        // La firma reside en ~/Imagenes/Firmas/
                        var firmaPhysicalPath = Server.MapPath("~/Imagenes/Firmas/" + firmaFile);
                        string firmaUrl = new Uri(firmaPhysicalPath).AbsoluteUri;

                        ViewBag.DebugSelloUrl = selloUrl;
                        ViewBag.DebugFirmaUrl = firmaUrl;

                        // Parámetros del reporte: visibilidad del sello, rutas de imágenes y nombre del director
                        rv.LocalReport.SetParameters(new[]
                        {
                            new ReportParameter("ShowSello", showSello ? "true" : "false"),
                            new ReportParameter("SelloPath", selloUrl),
                            new ReportParameter("FirmaPath", firmaUrl),
                            new ReportParameter("DirectorNombre", directorNombre)
                        });
                    }
                    catch (Exception pex)
                    {
                        ViewBag.Mensaje = "Aviso: el sello o la firma no se pudieron aplicar. " + pex.Message;
                        try
                        {
                            rv.LocalReport.SetParameters(new[] {
                                new ReportParameter("ShowSello", "false")
                            });
                        }
                        catch { /* ignora */ }
                    }

                    ViewBag.ReportViewer = rv;
                }
                catch (Exception ex)
                {
                    ViewBag.Mensaje = "Error al preparar el reporte: " + ex.Message;
                }
            }

            return View(baja);
        }


        // GET: Bajas/Create
        public ActionResult Create(string matricula)
        {
            if (string.IsNullOrEmpty(matricula))
            {
                return RedirectToAction("Index");
            }

            var datos = db.Database.SqlQuery<BajaViewModel>(
                "EXEC sp_VerDatosParaBaja @p0", matricula).FirstOrDefault();

            // --- VALIDACIÓN 1: Matrícula no existe en absoluto ---
            if (datos == null)
            {
                return Content("<script>alert('La matrícula no existe en la base de datos.'); window.history.back();</script>");
            }

            // --- VALIDACIÓN 2: Faltan datos lógicos (Entrevista o Seguimiento) ---
            if (!string.IsNullOrEmpty(datos.ErrorLogico))
            {
                // Si el SP reporta error, verificar si es un alumno reingresado con baja histórica activa
                bool esReingreso = false;
                try
                {
                    // Verificamos si existe la columna Reingreso
                    bool tieneColReingreso = db.Database
                        .SqlQuery<int>("SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Bajas') AND name = 'Reingreso'")
                        .FirstOrDefault() > 0;

                    if (tieneColReingreso)
                    {
                        // Verificar si tiene baja con Reingreso=1
                        esReingreso = db.Bajas.Any(b => b.IdPersona == datos.IdPersona && b.Activo == true && b.Reingreso == 1);
                    }
                }
                catch { }

                if (esReingreso)
                {
                    datos.ErrorLogico = null; // Limpiamos el error para permitir continuar
                }
                else
                {
                    string mensajeAlerta = "";

                    if (datos.ErrorLogico == "SIN_ENTREVISTA")
                    {
                        mensajeAlerta = "¡El Alumno No Tiene Entrevista Inicial Registrada! No se puede procesar la baja.";
                    }
                    else if (datos.ErrorLogico == "SIN_SEGUIMIENTO")
                    {
                        mensajeAlerta = "¡El Alumno No Tiene Seguimientos (Individuales) Registrados! No se puede procesar la baja.";
                    }
                    else
                    {
                        // Si es otro error (ej. TIENE_BAJA_ACTIVA) y no es reingreso, mostramos mensaje genérico o el que venga
                        mensajeAlerta = "El alumno ya cuenta con una baja activa o existe un impedimento lógico: " + datos.ErrorLogico;
                    }

                    return Content($"<script>alert('{mensajeAlerta}'); window.history.back();</script>");
                }
            }

            // --- Calcular el próximo folio por carrera + periodo + año (solo para mostrar) ---
            // 1) Obtener IdCarrera del alumno por matrícula
            var pInfo = db.DatosPersonales
                .Where(p => p.Matricula == matricula)
                .Select(p => new { p.IdCarrera, p.IdPersona })
                .FirstOrDefault();

            int idCarrera = pInfo?.IdCarrera ?? 0;

            // sp_VerDatosParaBaja lee Grupo desde individuals.Grupo (string), que puede quedar
            // con el grupo de una carrera anterior. Se reconstruye la nomenclatura completa
            // desde DatosPersonales: Carrera.Nomenclatura + Grado.Nombre + Grupo.Nombre.
            if (pInfo != null)
            {
                var partes = db.DatosPersonales
                    .Where(d => d.IdPersona == pInfo.IdPersona)
                    .Select(d => new { d.Carrera.Nomenclatura, GradoNombre = d.Grado.Nombre, GrupoNombre = d.Grupo.Nombre })
                    .FirstOrDefault();
                if (partes != null)
                    datos.Grupo = partes.Nomenclatura + partes.GradoNombre + partes.GrupoNombre;
            }

            // Determinar periodo y año actuales (para la nueva baja)
            var ahora = DateTime.Now;
            var periodoActual = GetPeriodoFromDate(ahora);
            var añoActual = ahora.Year;

            // --- Lógica de Reutilización de Folio (Para Reingresos del mismo periodo) ---
            // Pasamos el IdPersona para ver si tiene un folio reutilizable
            datos.Folio = CalcularSiguienteFolio(idCarrera, null, pInfo?.IdPersona);

            ViewBag.Alumno = datos;
            ViewBag.Categorias = GetCategoriasBaja();
            ViewBag.Vulnerabilidades = GetListaVulnerabilidades();

            // ViewBags para desercion no inscrito
            var usuario = Session["Usuario"] as Usuario;
            ViewBag.NivelUsuario = usuario?.IdNivel ?? 0;

            // Carreras (para Master)
            ViewBag.Carreras = db.Carreras.OrderBy(c => c.Nombre)
                .Select(c => new SelectListItem
                {
                    Value = c.IdCarrera.ToString(),
                    Text = c.Nombre,
                    Selected = c.IdCarrera == idCarrera
                }).ToList();

            // Áreas (filtradas por carrera del alumno)
            ViewBag.Areas = db.Especialidads
                .Where(a => a.IdCarrera == idCarrera)
                .OrderBy(a => a.Nombre)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = a.Nombre
                }).ToList();

            // Cuatrimestres (actual + 3 del año anterior)
            ViewBag.Cuatrimestres = GenerarCuatrimestresValidos();

            // Turnos
            ViewBag.Turnos = new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Matutino" },
                new SelectListItem { Value = "2", Text = "Vespertino" },
                new SelectListItem { Value = "3", Text = "Despresurizado" }
            };

            return View(datos);
        }

        // POST: Bajas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BajaViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var usuario = Session["Usuario"] as Usuario;
                    var realizadoPor = string.IsNullOrWhiteSpace(usuario?.NombreCompleto) ? "Sistema" : usuario.NombreCompleto;

                    // Calcular folio por carrera, periodo y año antes de crear la baja
                    var pInfo = db.DatosPersonales.Where(p => p.Matricula == model.Matricula).Select(p => new { p.IdCarrera, p.IdPersona }).FirstOrDefault();
                    int idCarrera = pInfo?.IdCarrera ?? 0;

                    var ahora = DateTime.Now;
                    var periodoActual = GetPeriodoFromDate(ahora);
                    var añoActual = ahora.Year;

                    // Pasamos IdPersona para reutilizar folio si aplica
                    string nuevoFolio = CalcularSiguienteFolio(idCarrera, null, pInfo?.IdPersona);

                    // Verificar si es desercion no inscrito (sin folio)
                    bool esDesercionNoInscrito = model.Otra == "Deserción: No inscrito.";

                    // Solo asignar folio si NO es deserción no inscrito
                    if (esDesercionNoInscrito)
                    {
                        nuevoFolio = "N/A"; // Asignar N/A en lugar de null para cumplir validación [Required]
                    }

                    var resultado = db.Database.SqlQuery<ResultadoSP>(
                        "EXEC sp_RegistrarBajaAlumno_Auto @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7",
                        model.Matricula,
                        model.Causa,
                        model.Tipo,
                        model.Observacion,
                        model.Vulnerable,
                        model.Vulnerabilidad,
                        model.Otra ?? "N/A",
                        realizadoPor
                    ).FirstOrDefault();

                    if (resultado != null && resultado.IdPersona != 0)
                    {
                        // Buscar la baja recién creada y actualizar su folio
                        var bajaRecienCreada = db.Bajas
                            .Where(b => b.IdPersona == resultado.IdPersona)
                            .OrderByDescending(b => b.Fecha)
                            .ThenByDescending(b => b.IdBaja) // Por si hay misma fecha, usamos IdBaja
                            .FirstOrDefault();

                        if (bajaRecienCreada != null)
                        {
                            // Actualizar folio calculado
                            bajaRecienCreada.Folio = nuevoFolio; // "N/A" si es deserción no inscrito

                            // Actualizar campos editables si es deserción no inscrito
                            if (esDesercionNoInscrito)
                            {
                                bajaRecienCreada.Grupo = model.Grupo;
                                bajaRecienCreada.Carrera = model.Carrera;
                                bajaRecienCreada.Area = model.Area;
                                bajaRecienCreada.Cuatrimestre = model.Cuatrimestre;
                                bajaRecienCreada.Turno = model.Turno;
                            }

                            // Capturar el nombre del tutor como dato histórico inmutable.
                            // DatosPersonales todavía refleja la carrera del alumno antes de cualquier reingreso futuro.
                            var datosAlumno = db.DatosPersonales
                                .Where(p => p.IdPersona == resultado.IdPersona)
                                .FirstOrDefault();
                            if (datosAlumno != null)
                            {
                                var nombreTutorActual = db.TutoriaGrupals
                                    .Where(tg => tg.IdCarrera == datosAlumno.IdCarrera
                                              && tg.IdGrado == datosAlumno.IdGrado
                                              && tg.IdGrupo == datosAlumno.IdGrupo
                                              && tg.IdTurno == datosAlumno.IdTurno
                                              && tg.IdPeriodo == datosAlumno.IdPeriodo
                                              && tg.Año == datosAlumno.Año)
                                    .Join(db.Usuarios,
                                          tg => tg.IdUsuario,
                                          u => u.IdUsuario,
                                          (tg, u) => u.NombreCompleto)
                                    .FirstOrDefault();
                                bajaRecienCreada.NombreTutor = nombreTutorActual ?? "Sin Tutor Asignado";
                            }
                            else
                            {
                                // datosAlumno no encontrado: NombreTutor queda marcado explícitamente
                                // para que el reporte no use el join dinámico, que podría devolver
                                // el tutor de otra carrera si el alumno cambia de carrera en el futuro.
                                bajaRecienCreada.NombreTutor = "Sin Tutor Asignado";
                            }

                            if (datosAlumno != null)
                            {
                                // Garantizar que Carrera (texto) sea coherente con DatosPersonales,
                                // independientemente de lo que haya guardado el SP.
                                string nombreCarreraActual = db.Carreras
                                    .Where(c => c.IdCarrera == datosAlumno.IdCarrera)
                                    .Select(c => c.Nombre)
                                    .FirstOrDefault();
                                if (!string.IsNullOrEmpty(nombreCarreraActual))
                                    bajaRecienCreada.Carrera = nombreCarreraActual;

                                // Corregir Grupo almacenado por el SP, que lo toma de individuals.Grupo.
                                // No aplica para deserción no inscrito, donde el coordinador lo captura manualmente.
                                if (!esDesercionNoInscrito)
                                {
                                    var partesGrupo = db.DatosPersonales
                                        .Where(d => d.IdPersona == resultado.IdPersona)
                                        .Select(d => new { d.Carrera.Nomenclatura, GradoNombre = d.Grado.Nombre, GrupoNombre = d.Grupo.Nombre })
                                        .FirstOrDefault();
                                    if (partesGrupo != null)
                                        bajaRecienCreada.Grupo = partesGrupo.Nomenclatura + partesGrupo.GradoNombre + partesGrupo.GrupoNombre;
                                }
                            }

                            db.SaveChanges();
                        }

                        return RedirectToAction("Index", new { id = (int)resultado.IdPersona });
                    }

                    ModelState.AddModelError("", "No se obtuvo IdPersona válido del SP.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al registrar la baja: " + ex.Message);
                }
            }

            // Error de validación: recargar datos necesarios para la vista
            var datos = db.Database.SqlQuery<BajaViewModel>("EXEC sp_VerDatosParaBaja @p0", model.Matricula).FirstOrDefault();
            if (datos != null)
            {
                // Recalcular folio solo para mostrar (resetea por periodo y año)
                var pInfo = db.DatosPersonales.Where(p => p.Matricula == model.Matricula).Select(p => new { p.IdCarrera, p.IdPersona }).FirstOrDefault();
                datos.Folio = CalcularSiguienteFolio(pInfo?.IdCarrera ?? 0, null, pInfo?.IdPersona);
            }
            ViewBag.Alumno = datos;

            // Recargar catálogos para la vista
            ViewBag.Categorias = GetCategoriasBaja();
            ViewBag.Vulnerabilidades = GetListaVulnerabilidades();
            return View(model);
        }





        // GET: Bajas/Edit/5
        public ActionResult Edit(int? id)
        {

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Baja baja = db.Bajas.Find(id);
            if (baja == null)
            {
                return HttpNotFound();
            }

            if (!CoordinadorPuedeModificarBaja(baja))
                return Content("<script>alert('ACCESO DENEGADO: Solo puedes modificar bajas de tu carrera.'); history.back();</script>");

            if (baja.Fecha == default(DateTime) || baja.Fecha == DateTime.MinValue)
            {
                baja.Fecha = DateTime.Today;
            }


            int idp = baja.IdPersona;
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idp);
            ViewBag.Categorias = new SelectList(GetCategoriasBaja(), "Value", "Text", baja.Causa);

            // ViewBags para Desercion No Inscrito (dropdowns)
            var usuario = Session["Usuario"] as Usuario;
            ViewBag.NivelUsuario = usuario?.IdNivel ?? 0;

            // Obtener IdCarrera del alumno para filtrar areas
            var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idp);
            int idCarrera = alumno?.IdCarrera ?? 0;

            // Carreras
            ViewBag.Carreras = db.Carreras.OrderBy(c => c.Nombre)
                .Select(c => new SelectListItem
                {
                    Value = c.IdCarrera.ToString(),
                    Text = c.Nombre,
                    Selected = c.Nombre == baja.Carrera // Pre-seleccionar la actual
                }).ToList();

            // Areas (filtradas por carrera del alumno)
            ViewBag.Areas = db.Especialidads
                .Where(a => a.IdCarrera == idCarrera)
                .OrderBy(a => a.Nombre)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = a.Nombre,
                    Selected = a.Nombre == baja.Area // Pre-seleccionar la actual
                }).ToList();

            // Cuatrimestres
            ViewBag.Cuatrimestres = GenerarCuatrimestresValidos();

            // Turnos
            ViewBag.Turnos = new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Matutino", Selected = baja.Turno == "Matutino" },
                new SelectListItem { Value = "2", Text = "Vespertino", Selected = baja.Turno == "Vespertino" },
                new SelectListItem { Value = "3", Text = "Despresurizado", Selected = baja.Turno == "Despresurizado" }
            };

            return View(baja);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Baja baja)
        {
            // 1. Obtener usuario y nivel
            Usuario usuario = Session["Usuario"] as Usuario;
            bool isMaster = (usuario != null && usuario.IdNivel == 4);

            // 2. Recuperar datos originales de la BD (sin tracking) para proteger campos clave
            var bajaOriginal = db.Bajas.AsNoTracking().FirstOrDefault(b => b.IdBaja == baja.IdBaja);
            if (bajaOriginal == null)
            {
                return HttpNotFound();
            }

            if (!CoordinadorPuedeModificarBaja(bajaOriginal))
                return Content("<script>alert('ACCESO DENEGADO: Solo puedes modificar bajas de tu carrera.'); history.back();</script>");

            // 3. Detectar si es Deserción No Inscrito
            bool esDesercionNoInscrito = (baja.Otra ?? "").ToLower().Contains("no inscrito");

            // 4. Lógica de reasignación de campos según nivel de usuario
            if (isMaster)
            {
                // Máster puede modificar datos del formulario; se protegen campos de auditoría
                baja.RealizadoPor = bajaOriginal.RealizadoPor;
            }
            else
            {
                bool eraDesercionOriginal = (bajaOriginal.Otra ?? "").ToLower().Contains("no inscrito");
                // NO ES MÁSTER: (Es Coordinador/Tutor)
                // Protegemos IdPersona, Matricula, Nombre, Folio, Fecha (solo Master puede cambiarlos)
                baja.IdPersona = bajaOriginal.IdPersona;
                baja.IdEntrevistaInicial = bajaOriginal.IdEntrevistaInicial;
                baja.Nombre = bajaOriginal.Nombre;
                baja.Matricula = bajaOriginal.Matricula;


                // Si el folio original existía, lo protegemos, A MENOS QUE estemos cambiando a Deserción (que requiere quitar folio).
                // Si era NA (Deserción), permitimos que se guarde el nuevo (calculado en vista).
                if (!string.IsNullOrEmpty(bajaOriginal.Folio) && bajaOriginal.Folio != "NA" && bajaOriginal.Folio != "N/A" && !esDesercionNoInscrito)
                {
                    baja.Folio = bajaOriginal.Folio;
                }

                baja.RealizadoPor = bajaOriginal.RealizadoPor;
                baja.Fecha = bajaOriginal.Fecha;

                // Para Deserción No Inscrito: Tutores y Coordinadores PUEDEN editar estos campos
                // Permitimos guardar los cambios SI la baja original ERA de tipo "No inscrito" (porque no tenía datos fijos)
                // O SI la nueva causa ES "No inscrito" (porque estamos editando esos datos ahora)

                if (!esDesercionNoInscrito && !eraDesercionOriginal)
                {
                    // NO es Desercion actual Y NO era Desercion original: Es una baja normal -> Usar valores originales protegidos
                    baja.Grupo = bajaOriginal.Grupo;
                    baja.Carrera = bajaOriginal.Carrera;
                    baja.Area = bajaOriginal.Area;
                    baja.Cuatrimestre = bajaOriginal.Cuatrimestre;
                    baja.Turno = bajaOriginal.Turno;
                    baja.Especialidad = bajaOriginal.Especialidad;
                }
                // Si ES Desercion (actual o original), permitimos que los valores del formulario sobrescriban.
            }

            // Lógica global de fecha y estado
            // Restauramos el estado Activo original (para evitar que se ponga en false si no viene del form)
            baja.Activo = bajaOriginal.Activo;

            // PRESERVAR REINGRESO: Si la baja original era reingreso, mantenemos el flag.
            // A MENOS que sea revocada/reactivada explícitamente, pero Edit normal no debería cambiar esto.
            baja.Reingreso = bajaOriginal.Reingreso;

            // Aplicar a TODOS los usuarios (Master o Tutor):
            // Si venimos de Deserción a una baja con folio, actualizamos la FECHA a HOY.
            // Para asegurar que el Folio (que pertenece al periodo actual) coincida con la Fecha.
            bool eraDesercionOriginalGlobal = (bajaOriginal.Otra ?? "").ToLower().Contains("no inscrito");
            bool esDesercionNoInscritoGlobal = (baja.Otra ?? "").ToLower().Contains("no inscrito");

            if (eraDesercionOriginalGlobal && !esDesercionNoInscritoGlobal)
            {
                baja.Fecha = DateTime.Now;
            }

            // Limpieza de campos nulos antes de validar el modelo
            if (baja.Vulnerabilidad == null)
            {
                baja.Vulnerabilidad = "";
            }
            if (baja.Otra == null)
            {
                baja.Otra = "";
            }

            // Solo validamos el modelo DESPUÉS de haber aplicado nuestra lógica
            if (ModelState.IsValid)
            {
                try
                {
                    db.Entry(baja).State = EntityState.Modified;
                    db.SaveChanges();
                    return RedirectToAction("Index", new { id = baja.IdPersona });
                }
                catch (Exception ex)
                {
                    // Error de restricción única en la constraint UQ_BajasAlumnos_Carrera_Folio
                    var mensajeError = ex.InnerException?.InnerException?.Message ?? ex.Message;

                    if (mensajeError.Contains("UQ_BajasAlumnos_Carrera_Folio") || mensajeError.Contains("duplicate key"))
                    {
                        // Asignar error al campo Folio para que la vista lo muestre junto al control
                        ModelState.AddModelError("Folio", "Este número de Folio ya está ocupado en esta carrera. Por favor escribe otro.");
                    }
                    else
                    {
                        // Error inesperado: mostrarlo en el resumen de errores del modelo
                        ModelState.AddModelError("", "Error inesperado al guardar: " + mensajeError);
                    }

                    // No redirigir: dejar que el flujo continúe para recargar la vista
                }
            }

            // Modelo inválido: recargar los ViewBags para la vista
            System.Diagnostics.Debug.WriteLine(">>> MODELO NO VÁLIDO al editar baja.");
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == baja.IdPersona);
            ViewBag.Categorias = new SelectList(GetCategoriasBaja(), "Value", "Text", baja.Causa);
            return View(baja);
        }

        // GET: Bajas/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Baja baja = db.Bajas.Find(id);
            if (baja == null)
            {
                return HttpNotFound();
            }
            int idp = baja.IdPersona;
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idp);
            return View(baja);
        }

        // POST: Bajas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Baja baja = db.Bajas.Find(id);
            if (baja != null)
            {
                int idp = baja.IdPersona;

                // Verificar ANTES de eliminar si hay otras bajas activas
                bool hayOtrasBajasActivas = db.Bajas.Any(b =>
                    b.IdPersona == idp &&
                    b.IdBaja != id &&
                    b.Activo == true
                );

                db.Bajas.Remove(baja);
                db.SaveChanges();

                // Si NO hay otras bajas activas, reactivar usuario
                if (!hayOtrasBajasActivas)
                {
                    ReactivarUsuarioEnTodasLasBD(idp);
                }

                return RedirectToAction("Index", new { id = idp });
            }
            return RedirectToAction("Index", "Home");
        }


        // GET: Bajas/Revocar/5
        public ActionResult Revocar(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Baja baja = db.Bajas.Find(id);
            if (baja == null) return HttpNotFound();

            if (!CoordinadorPuedeModificarBaja(baja))
                return Content("<script>alert('ACCESO DENEGADO: Solo puedes modificar bajas de tu carrera.'); history.back();</script>");

            // Validar si ya está revocada
            if (baja.Activo == false)
            {
                return RedirectToAction("Index", new { id = baja.IdPersona });
            }

            // Obtener datos del alumno para mostrar en la vista de confirmación
            ViewBag.Alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == baja.IdPersona);
            return View(baja);
        }

        // POST: Bajas/Revocar/5
        [HttpPost, ActionName("Revocar")]
        [ValidateAntiForgeryToken]
        public ActionResult RevocarConfirmed(int id)
        {
            Baja baja = db.Bajas.Find(id);
            if (baja != null)
            {
                if (!CoordinadorPuedeModificarBaja(baja))
                    return Content("<script>alert('ACCESO DENEGADO: Solo puedes modificar bajas de tu carrera.'); history.back();</script>");

                int idPersona = baja.IdPersona;

                // 1. Desactivar la baja
                baja.Activo = false;

                // 2. LIBERAR EL FOLIO PARA QUE SE PUEDA REUTILIZAR
                // Le agregamos "-REV" y el ID para asegurar que sea único y no choque
                // Ejemplo: De "010" pasa a "010-REV-45"
                baja.Folio = $"{baja.Folio}-REV-{baja.IdBaja}";

                // 3. Marcar explícitamente como no-reingreso para distinguirlo de una reincorporación
                baja.Reingreso = 0;

                // 4. Agregar nota en observación
                Usuario usuario = Session["Usuario"] as Usuario;
                string quien = usuario?.NombreCompleto ?? "Sistema";
                baja.Observacion = $"[REVOCADA por {quien} el {DateTime.Now:dd/MM/yyyy}] " + baja.Observacion;

                db.SaveChanges();

                // Verificar si hay otras bajas activas para este alumno
                bool hayOtrasBajasActivas = db.Bajas.Any(b =>
                    b.IdPersona == idPersona &&
                    b.IdBaja != id &&
                    b.Activo == true
                );

                // Si NO hay otras bajas activas, reactivar usuario
                if (!hayOtrasBajasActivas)
                {
                    ReactivarUsuarioEnTodasLasBD(idPersona);
                }

                return RedirectToAction("Index", new { id = baja.IdPersona });
            }
            return RedirectToAction("Index", "Home"); // O manejo de error
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }


        // GET: Bajas/ReporteBajas
        public ActionResult ReporteBajas(string orden, int? carreraId, string periodo, int page = 1, int pageSize = 20, bool imprimir = false)
        {
            Usuario user = Session["Usuario"] as Usuario;
            if (user == null) return RedirectToAction("Login", "Account");

            if (user.IdNivel == 3) carreraId = user.IdCarrera; // Filtro coordinador

            if (string.IsNullOrEmpty(orden)) orden = "fecha_desc";

            // Consulta de bajas con join a TutoriaGrupal para obtener el nombre del tutor
            var q = from b in db.Bajas
                    join p in db.DatosPersonales on b.IdPersona equals p.IdPersona

                    // Join para buscar el grupo y su tutor
                    join tg in db.TutoriaGrupals on
                        new { p.IdCarrera, p.IdGrado, p.IdGrupo, p.IdTurno, p.IdPeriodo, p.Año }
                        equals
                        new { tg.IdCarrera, tg.IdGrado, tg.IdGrupo, tg.IdTurno, tg.IdPeriodo, tg.Año } into tgGroup
                    from tg in tgGroup.DefaultIfEmpty()

                        // Join para sacar el nombre del usuario tutor
                    join u in db.Usuarios on tg.IdUsuario equals u.IdUsuario into uGroup
                    from u in uGroup.DefaultIfEmpty()

                    select new BajaReporteItem
                    {
                        Baja = b,
                        IdCarrera = p.IdCarrera,
                        NombreTutorReal = u != null ? u.NombreCompleto : "Sin Tutor Asignado"
                    };

            if (carreraId.HasValue)
            {
                // Obtiene el nombre de la carrera para permitir el filtrado basado en la carrera historica guardada en texto.
                string nombreCarreraFiltro = db.Carreras
                    .Where(c => c.IdCarrera == carreraId.Value)
                    .Select(c => c.Nombre)
                    .FirstOrDefault() ?? "";

                q = q.Where(x =>
                    // Para alumnos con Reingreso activo, el IdCarrera actual difiere de la carrera donde se realizo la baja.
                    // Se utiliza el valor de texto almacenado en Baja.Carrera como pivote de comparacion para estos casos aislados.
                    (x.Baja.Reingreso == 1 && x.Baja.Carrera != null && x.Baja.Carrera != "")
                        ? (x.Baja.Carrera.Trim() == nombreCarreraFiltro.Trim())
                        // Para bajas regulares (no reingreso) o registros donde el campo de texto sea nulo, se utiliza el IdCarrera actual como fallback.
                        : (x.IdCarrera == carreraId.Value)
                );
            }

            // Filtrado por periodo
            if (!string.IsNullOrEmpty(periodo))
            {
                var partes = periodo.Split('-');
                if (partes.Length == 2 &&
                    int.TryParse(partes[0], out int anio) &&
                    int.TryParse(partes[1], out int periodoNum))
                {
                    q = q.Where(x =>
                        x.Baja.Fecha.Year == anio &&
                        ((x.Baja.Fecha.Month >= 1 && x.Baja.Fecha.Month <= 4 && periodoNum == 1) ||
                         (x.Baja.Fecha.Month >= 5 && x.Baja.Fecha.Month <= 8 && periodoNum == 2) ||
                         (x.Baja.Fecha.Month >= 9 && x.Baja.Fecha.Month <= 12 && periodoNum == 3))
                    );
                }
            }

            // Ordenamiento actualizado
            switch (orden)
            {
                case "fecha_asc": q = q.OrderBy(x => x.Baja.Fecha); break;
                case "fecha_desc": q = q.OrderByDescending(x => x.Baja.Fecha); break;
                case "nombre_asc": q = q.OrderBy(x => x.Baja.Nombre); break;
                case "nombre_desc": q = q.OrderByDescending(x => x.Baja.Nombre); break;
                case "grupo_asc": q = q.OrderBy(x => x.Baja.Grupo); break;
                case "grupo_desc": q = q.OrderByDescending(x => x.Baja.Grupo); break;
                case "tutor_asc": q = q.OrderBy(x => x.NombreTutorReal); break;
                case "tutor_desc": q = q.OrderByDescending(x => x.NombreTutorReal); break;
                case "folio_asc": q = q.OrderBy(x => x.Baja.Folio); break;
                case "folio_desc": q = q.OrderByDescending(x => x.Baja.Folio); break;
                default: q = q.OrderByDescending(x => x.Baja.Fecha); break;
            }

            var total = q.Count();
            List<BajaReporteItem> lista;

            // CARGAR TODOS LOS REGISTROS para que DataTables maneje la paginación del lado del cliente
            // Esto permite que el buscador funcione en toda la tabla
            lista = q.ToList();

            ViewBag.PaginaActual = 1;
            ViewBag.TotalPaginas = 1;
            ViewBag.PageSize = total;
            ViewBag.Imprimir = imprimir;

            // --- CÁLCULO DE TOTALES POR CARRERA ---
            // Pre-carga de diccionarios en memoria para reducir operaciones a base de datos y permitir el cruce de datos historicos.
            var dictCarreras = db.Carreras.ToDictionary(c => c.Nombre.Trim().ToLower(), c => c.IdCarrera);

            // Para bajas con reingreso, el nombre del tutor histórico fue capturado al registrar la baja.
            // Se sobreescribe NombreTutorReal con ese dato inmutable en lugar del tutor derivado del join.
            foreach (var item in lista.Where(x => x.Baja.Reingreso == 1
                                               && !string.IsNullOrEmpty(x.Baja.NombreTutor)))
            {
                item.NombreTutorReal = item.Baja.NombreTutor;
            }

            var personasDict = db.DatosPersonales.Select(p => new { p.IdPersona, p.IdCarrera })
                                                 .ToDictionary(x => x.IdPersona, x => x.IdCarrera);

            // Obtención de la proyección de bajas para el cálculo de totales del dropdown.
            // Se aplica el mismo filtro de periodo activo para que los conteos sean coherentes con los resultados visibles.
            var bajasRawQuery = db.Bajas.Select(b => new { b.IdPersona, b.Reingreso, b.Carrera, b.Fecha });

            if (!string.IsNullOrEmpty(periodo))
            {
                var partesPeriodo = periodo.Split('-');
                if (partesPeriodo.Length == 2
                    && int.TryParse(partesPeriodo[0], out int anioFiltro)
                    && int.TryParse(partesPeriodo[1], out int perFiltro))
                {
                    bajasRawQuery = bajasRawQuery.Where(b =>
                        b.Fecha.Year == anioFiltro && (
                            perFiltro == 1 ? (b.Fecha.Month >= 1 && b.Fecha.Month <= 4) :
                            perFiltro == 2 ? (b.Fecha.Month >= 5 && b.Fecha.Month <= 8) :
                                             (b.Fecha.Month >= 9 && b.Fecha.Month <= 12)));
                }
            }

            var bajasRaw = bajasRawQuery.ToList();
            var totales = new Dictionary<int, int>();

            foreach (var b in bajasRaw)
            {
                // Verifica la correspondencia del registro actual de DatosPersonales.
                if (personasDict.TryGetValue(b.IdPersona, out int idActual))
                {
                    int idSumar = idActual; // Incremento dirigido a la carrera actual por defecto.

                    // Para casos de reingreso, se redirige el contador hacia el IdCarrera de la carrera historica usando el registro de texto.
                    if (b.Reingreso == 1 && !string.IsNullOrWhiteSpace(b.Carrera))
                    {
                        string claveBuscar = b.Carrera.Trim().ToLower();
                        if (dictCarreras.ContainsKey(claveBuscar))
                        {
                            idSumar = dictCarreras[claveBuscar];
                        }
                    }

                    if (!totales.ContainsKey(idSumar)) totales[idSumar] = 0;
                    totales[idSumar]++;
                }
            }

            var carreras = db.Carreras.OrderBy(c => c.Nombre).ToList();
            if (user.IdNivel == 3) carreras = carreras.Where(c => c.IdCarrera == user.IdCarrera).ToList();

            var ddCarreras = carreras.Select(c => new SelectListItem
            {
                Value = c.IdCarrera.ToString(),
                Text = $"{c.Nombre} ({(totales.ContainsKey(c.IdCarrera) ? totales[c.IdCarrera] : 0)})",
                Selected = carreraId.HasValue && c.IdCarrera == carreraId.Value
            }).ToList();

            if (user.IdNivel != 3) ddCarreras.Insert(0, new SelectListItem { Value = "", Text = "TODAS LAS CARRERAS" });

            ViewBag.Carreras = ddCarreras;
            ViewBag.CarreraId = carreraId;
            ViewBag.Orden = orden;
            ViewBag.Total = total;
            ViewBag.NivelUsuario = user.IdNivel;
            ViewBag.Periodo = periodo;
            ViewBag.Periodos = GenerarListaPeriodos();

            return View(lista);
        }

        private List<SelectListItem> GenerarListaPeriodos()
        {
            try
            {
                var periodosData = db.Bajas
                    .Where(b => b.Fecha != default(DateTime))
                    .Select(b => new
                    {
                        Anio = b.Fecha.Year,
                        Mes = b.Fecha.Month
                    })
                    .ToList()
                    .Select(x => new
                    {
                        x.Anio,
                        Periodo = x.Mes >= 1 && x.Mes <= 4 ? 1 :
                                  x.Mes >= 5 && x.Mes <= 8 ? 2 : 3
                    })
                    .Distinct()
                    .OrderByDescending(x => x.Anio)
                    .ThenByDescending(x => x.Periodo)
                    .ToList();

                var lista = periodosData.Select(p => new SelectListItem
                {
                    Value = $"{p.Anio}-{p.Periodo}",
                    Text = ObtenerNombrePeriodo(p.Periodo, p.Anio)
                }).ToList();

                return lista;
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }

        /// <summary>
        /// Verifica si el coordinador en sesión tiene permiso para modificar la baja indicada.
        /// Solo aplica a Nivel 3 (Coordinador); otros niveles siempre reciben true.
        /// </summary>
        private bool CoordinadorPuedeModificarBaja(Baja baja)
        {
            Usuario user = Session["Usuario"] as Usuario;
            if (user == null || user.IdNivel != 3) return true;

            string nombreCarreraUser = db.Carreras
                .Where(c => c.IdCarrera == user.IdCarrera)
                .Select(c => c.Nombre)
                .FirstOrDefault() ?? "";

            return !string.IsNullOrEmpty(baja.Carrera)
                && baja.Carrera.Trim() == nombreCarreraUser.Trim();
        }

        private string ObtenerNombrePeriodo(int periodo, int anio)
        {
            switch (periodo)
            {
                case 1: return $"ENERO - ABRIL {anio}";
                case 2: return $"MAYO - AGOSTO {anio}";
                case 3: return $"SEPTIEMBRE - DICIEMBRE {anio}";
                default: return $"PERIODO {periodo} {anio}";
            }
        }

        private static List<SelectListItem> GetCategoriasBaja()
        {
            return new List<SelectListItem>
    {
        new SelectListItem { Text = "Académica", Value = "Académica" },
        new SelectListItem { Text = "Deserción", Value = "Deserción" },
        new SelectListItem { Text = "Reprobación", Value = "Reprobación" },
        new SelectListItem { Text = "Problemas Económicos", Value = "Problemas Económicos" },
        new SelectListItem { Text = "Motivos Personales", Value = "Motivos Personales" },
        new SelectListItem { Text = "Cambio de UTT", Value = "Cambio de UTT" },
        new SelectListItem { Text = "Cambio de carrera", Value = "Cambio de carrera" },
        new SelectListItem { Text = "Faltas al Reglamento Escolar", Value = "Faltas al Reglamento Escolar" },
        new SelectListItem { Text = "Otras", Value = "Otras" }
    };
        }





        // NO static porque usa 'db'
        private List<SelectListItem> GetListaVulnerabilidades()
        {
            try
            {
                // Lee directo por SQL (no requiere DbSet)
                var nombres = db.Database
                    .SqlQuery<string>("SELECT Nombre FROM Vulnerables ORDER BY Nombre")
                    .ToList(); // Count es propiedad, no método, al operar sobre la lista materializada

                if (nombres == null || nombres.Count == 0)
                    throw new Exception();

                return nombres.Select(n => new SelectListItem { Text = n, Value = n }).ToList();
            }
            catch
            {
                // Fallback fijo
                return new List<SelectListItem>
        {
            new SelectListItem{ Text = "Económico",     Value = "Económico" },
            new SelectListItem{ Text = "Académico",     Value = "Académico" },
            new SelectListItem{ Text = "Personal",      Value = "Personal" },
            new SelectListItem{ Text = "No vulnerable", Value = "No vulnerable" }
        };
            }
        }




        // Determina el periodo (cuatrimestre) a partir de la fecha:
        // 1: Enero-Abril, 2: Mayo-Agosto, 3: Septiembre-Diciembre
        private static int GetPeriodoFromDate(DateTime fecha)
        {
            if (fecha.Month >= 1 && fecha.Month <= 4) return 1;
            if (fecha.Month >= 5 && fecha.Month <= 8) return 2;
            return 3;
        }


        // Quita acentos y normaliza
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // Mapea por nombre de carrera a un archivo dentro de ~/Imagenes/
        private static string GetSelloFileNameByNombreCarrera(string carreraNom)
        {
            var n = RemoveDiacritics(carreraNom ?? "").ToLowerInvariant();

            if (n.Contains("tecnologias") && n.Contains("informacion")) return "sello_TI.png";
            if (n.Contains("administr")) return "sello_ADMIN.png";
            if (n.Contains("aeronaut") || n.Contains("manufact")) return "sello_AERONAUTICA.png";
            if (n.Contains("ciencia") && n.Contains("datos")) return "sello_TI.png";
            if (n.Contains("inteligencia artificial")) return "sello_CDATIA.png";
            if (n.Contains("energia")) return "sello_ENERGIAS.png";

            // "Mantenimiento Industrial" requiere sello específico para no colisionar con la regla genérica de "industrial"
            if (n.Contains("mantenimiento") && n.Contains("industrial")) return "sello_MANT.png";

            // Carrera "Industrial" o "Ingeniería Industrial" sin "mantenimiento" -> sello industrial genérico
            if (n.Equals("industrial") || n.Contains("industrial") && !n.Contains("mantenimiento")) return "sello_INDUSTRIAL.png";

            if (n.Contains("logistica internacional")) return "sello_LOG_INT.png";
            if (n.Contains("logistica")) return "sello_LOGISTICA.png";
            if (n.Contains("mecatron")) return "sello_MECA.png";
            if (n.Contains("microelectron") || n.Contains("semiconduct")) return "sello_MICROSEMI.png";

            return "sello_default.png"; // respaldo
        }

        // Devuelve el nombre del archivo de la firma y el nombre completo del director
        private Tuple<string, string> GetDirectorInfoPorCarrera(string carreraNom)
        {
            // Normalizamos el nombre de la carrera para hacer la comparación más robusta
            var n = RemoveDiacritics(carreraNom ?? "").ToLowerInvariant();

            string firmaArchivo = "firma_default.png"; // Un respaldo por si no se encuentra coincidencia
            string directorNombre = "DIRECTOR DE CARRERA";

            // IMPORTANTE: Las condiciones más específicas deben ir primero para evitar conflictos.
            // Por ejemplo, "logistica internacional" debe revisarse antes que "logistica".

            if (n.Contains("logistica internacional"))
            {
                firmaArchivo = "firma_soledad_ocanas.png";
                directorNombre = "Maria Soledad Ocañas Martinez";
            }
            else if (n.Contains("administracion")) // 'administr' cubre 'Administración'
            {
                firmaArchivo = "firma_soledad_ocanas.png";
                directorNombre = "Maria Soledad Ocañas Martinez";
            }
            else if (n.Contains("industrial") || n.Contains("mantenimiento") || n.Contains("maestria") || n.Contains("aeronautica"))
            {
                firmaArchivo = "firma_celia_velarde.png";
                directorNombre = "Celia Esther Velarde Gaytan";
            }
            else if (n.Contains("tecnologias") || n.Contains("logistica") || n.Contains("semiconductores") || n.Contains("mecatronica") || n.Contains("energias") || n.Contains("ciencia"))
            {
                firmaArchivo = "firma_myriam_benitez.png";
                directorNombre = "Myriam Benitez Cortes";
            }

            return new Tuple<string, string>(firmaArchivo, directorNombre);
        }


        // === Endpoints para revisar bajas ===

        [HttpGet]
        public JsonResult revisarBajas(int id)
        {
            try
            {
                // Verificamos si existe la columna Activo primero (por seguridad)
                bool tieneColActivo = db.Database
                    .SqlQuery<int>("SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BajasAlumnos') AND name = 'Activo'")
                    .FirstOrDefault() > 0;

                bool tieneBaja;

                if (tieneColActivo)
                {
                    // Filtrar solo bajas activas que no sean reingresos
                    // Verificamos si existe la columna Reingreso
                    bool tieneColReingreso = db.Database
                        .SqlQuery<int>("SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BajasAlumnos') AND name = 'Reingreso'")
                        .FirstOrDefault() > 0;

                    string query = "";
                    if (tieneColReingreso)
                    {
                        // Si es reingreso (1), NO se considera baja activa para efectos visuales/bloqueo
                        query = "IF EXISTS(SELECT 1 FROM dbo.BajasAlumnos WHERE IdPersona = @p0 AND (Activo = 1 OR Activo IS NULL) AND (Reingreso = 0 OR Reingreso IS NULL)) SELECT 1 ELSE SELECT 0";
                    }
                    else
                    {
                        query = "IF EXISTS(SELECT 1 FROM dbo.BajasAlumnos WHERE IdPersona = @p0 AND (Activo = 1 OR Activo IS NULL)) SELECT 1 ELSE SELECT 0";
                    }

                    int r = db.Database.SqlQuery<int>(query, id).FirstOrDefault();
                    tieneBaja = (r == 1);
                }
                else
                {
                    // Fallback: si la columna Activo no existe, se asume que toda baja registrada está activa
                    tieneBaja = db.Bajas.Any(b => b.IdPersona == id);
                }

                return Json(tieneBaja, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult revisarBajasLote(string ids)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ids))
                    return Json(new { idsConBaja = new int[0] }, JsonRequestBehavior.AllowGet);

                var arr = ids.Split(',')
                             .Select(s => { int v; return int.TryParse(s, out v) ? (int?)v : null; })
                             .Where(v => v.HasValue)
                             .Select(v => v.Value)
                             .ToList();

                if (arr.Count == 0) return Json(new { idsConBaja = new int[0] }, JsonRequestBehavior.AllowGet);

                bool tieneColActivo = db.Database
                    .SqlQuery<int>("SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BajasAlumnos') AND name = 'Activo'")
                    .FirstOrDefault() > 0;

                List<int> idsConBaja;

                if (tieneColActivo)
                {
                    // Filtrar solo bajas activas que no sean reingresos
                    bool tieneColReingreso = db.Database
                        .SqlQuery<int>("SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BajasAlumnos') AND name = 'Reingreso'")
                        .FirstOrDefault() > 0;

                    string inList = string.Join(",", arr);
                    string sql = "";

                    if (tieneColReingreso)
                    {
                        sql = $"SELECT DISTINCT IdPersona FROM dbo.BajasAlumnos WHERE IdPersona IN ({inList}) AND (Activo = 1 OR Activo IS NULL) AND (Reingreso = 0 OR Reingreso IS NULL)";
                    }
                    else
                    {
                        sql = $"SELECT DISTINCT IdPersona FROM dbo.BajasAlumnos WHERE IdPersona IN ({inList}) AND (Activo = 1 OR Activo IS NULL)";
                    }

                    idsConBaja = db.Database.SqlQuery<int>(sql).ToList();
                }
                else
                {
                    idsConBaja = db.Bajas
                                   .Where(b => arr.Contains(b.IdPersona))
                                   .Select(b => b.IdPersona)
                                   .Distinct()
                                   .ToList();
                }

                return Json(new { idsConBaja }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult RevisarBajasLote(string ids)
        {
            try
            {
                if (string.IsNullOrEmpty(ids))
                {
                    return Json(new { idsConBaja = new List<int>() }, JsonRequestBehavior.AllowGet);
                }

                var idsList = ids.Split(',')
                    .Select(id => {
                        int result;
                        return int.TryParse(id, out result) ? result : 0;
                    })
                    .Where(id => id > 0)
                    .ToList();

                if (!idsList.Any())
                {
                    return Json(new { idsConBaja = new List<int>() }, JsonRequestBehavior.AllowGet);
                }

                // Query optimizada en una sola consulta
                var idsConBaja = db.Bajas
                    .AsNoTracking()
                    .Where(b => idsList.Contains(b.IdPersona))
                    .Select(b => b.IdPersona)
                    .Distinct()
                    .ToList();

                return Json(new { idsConBaja }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en RevisarBajasLote: {ex.Message}");
                return Json(new { error = ex.Message, idsConBaja = new List<int>() }, JsonRequestBehavior.AllowGet);
            }



        }

        /// <summary>
        /// Reactiva al usuario en todas las bases de datos (GestionUsuarios y Tutorias)
        /// cuando se revoca o elimina una baja y no hay otras bajas activas.
        /// </summary>
        /// <param name="idPersona">ID de la persona/alumno a reactivar</param>
        private void ReactivarUsuarioEnTodasLasBD(int idPersona)
        {
            try
            {
                // Obtener la matricula del alumno
                var alumno = db.DatosPersonales.FirstOrDefault(p => p.IdPersona == idPersona);
                if (alumno == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: No se encontro alumno con IdPersona: {idPersona}");
                    return;
                }

                string matricula = alumno.Matricula;
                System.Diagnostics.Debug.WriteLine($"Reactivando usuario: {matricula} (IdPersona: {idPersona})");

                // 1. Reactivar en BD Tutorias (tabla Usuarios)
                var usuarioTutoria = db.Usuarios.FirstOrDefault(u => u.UserName == matricula);
                if (usuarioTutoria != null)
                {
                    usuarioTutoria.Estado = true;
                    System.Diagnostics.Debug.WriteLine($"  Usuario reactivado en Tutorias.Usuarios");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  Advertencia: Usuario no encontrado en Tutorias.Usuarios");
                }

                // 2. Reactivar en BD GestionUsuarios (tabla Alumnos)
                try
                {
                    int rowsAffected = db.Database.ExecuteSqlCommand(
                        @"UPDATE GestionUsuarios.dbo.Alumnos 
                          SET habilitado = 1 
                          WHERE LTRIM(RTRIM(matricula)) = @p0",
                        matricula
                    );

                    if (rowsAffected > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Usuario reactivado en GestionUsuarios.Alumnos ({rowsAffected} filas)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  Advertencia: No se encontro usuario en GestionUsuarios.Alumnos");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"  Error actualizando GestionUsuarios: {ex.Message}");
                }

                // 3. Reactivar en BD EstadiasUTTN (tabla Usuario1) - Opcional
                try
                {
                    int rowsAffected = db.Database.ExecuteSqlCommand(
                        @"UPDATE EstadiasUTTN.dbo.Usuario1 
                          SET Estado = 1 
                          WHERE LTRIM(RTRIM(UserName)) = @p0",
                        matricula
                    );

                    if (rowsAffected > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Usuario reactivado en EstadiasUTTN.Usuario1 ({rowsAffected} filas)");
                    }
                }
                catch (Exception ex)
                {
                    // Si falla, no es critico, puede que la BD no exista o no tenga permisos
                    System.Diagnostics.Debug.WriteLine($"  Advertencia: No se pudo actualizar EstadiasUTTN: {ex.Message}");
                }

                // Guardar cambios en Tutorias
                db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"Usuario {matricula} reactivado exitosamente en todas las BD");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error general al reactivar usuario: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        // ======================================================================
        // MÉTODOS AUXILIARES PARA DESERCIÓN NO INSCRITO
        // ======================================================================

        /// <summary>
        /// Genera lista de cuatrimestres válidos: periodo actual + 3 del año anterior
        /// </summary>
        private List<SelectListItem> GenerarCuatrimestresValidos()
        {
            var ahora = DateTime.Now;
            var periodoActual = GetPeriodoFromDate(ahora);
            var añoActual = ahora.Year;
            var añoAnterior = añoActual - 1;
            var lista = new List<SelectListItem>();

            // 1. Agregar el cuatrimestre ACTUAL
            lista.Add(new SelectListItem
            {
                Value = $"{añoActual}-{periodoActual}",
                Text = GetNombrePeriodo(periodoActual, añoActual)
            });

            // 2. Agregar los 3 cuatrimestres del AÑO ANTERIOR
            for (int periodo = 1; periodo <= 3; periodo++)
            {
                lista.Add(new SelectListItem
                {
                    Value = $"{añoAnterior}-{periodo}",
                    Text = GetNombrePeriodo(periodo, añoAnterior)
                });
            }

            // El resultado siempre será 4 opciones:
            // - 1 del año actual (el periodo en curso)
            // - 3 del año anterior (todos los periodos)

            return lista; // No ordenar, mantener orden: actual primero, luego los 3 anteriores
        }

        /// <summary>
        /// Obtiene el nombre descriptivo de un periodo con su año
        /// </summary>
        private string GetNombrePeriodo(int periodo, int año)
        {
            string nombre = periodo == 1 ? "Enero-Abril" :
                            periodo == 2 ? "Mayo-Agosto" :
                            "Septiembre-Diciembre";
            return $"{nombre} del {año}";
        }

        /// <summary>
        /// Método AJAX para obtener áreas filtradas por carrera
        /// </summary>
        [HttpGet]
        public JsonResult GetAreasPorCarrera(int idCarrera)
        {
            try
            {
                var areas = db.Especialidads
                    .Where(a => a.IdCarrera == idCarrera)
                    .OrderBy(a => a.Nombre)
                    .Select(a => new
                    {
                        Value = a.Id.ToString(),
                        Text = a.Nombre
                    })
                    .ToList();

                return Json(areas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }




        [HttpPost]
        public ActionResult Reactivar(int id)
        {
            try
            {
                Baja baja = db.Bajas.Find(id);
                if (baja != null)
                {
                    // 1. Marcar baja como inactiva (sin modificar datos históricos)
                    baja.Activo = false;

                    // 2. Marcar como reingreso (para demostrar que fue reincorporado)
                    baja.Reingreso = 1;

                    // 3. Reactivar USUARIO en TODAS las BDs (Gestion, Estadias, Tutorias)
                    ReactivarUsuarioEnTodasLasBD(baja.IdPersona);

                    db.SaveChanges();

                    System.Diagnostics.Debug.WriteLine($"Alumno reincorporado - IdPersona: {baja.IdPersona}, Matricula: {baja.Matricula}, Activo=false");
                }
                return RedirectToAction("ReporteBajas");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al reactivar alumno: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return RedirectToAction("ReporteBajas");
            }
        }


        // Endpoint AJAX para obtener el siguiente folio disponible (lógica de gaps)
        [HttpPost]
        public JsonResult GetSiguienteFolioAjax(int idCarrera, int? idBaja = null)
        {
            try
            {
                string nuevoFolio = CalcularSiguienteFolio(idCarrera, idBaja);
                return Json(new { Status = "OK", Folio = nuevoFolio });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "Error", Mensaje = ex.Message });
            }
        }

        // Lógica reutilizable para calcular folio con huecos
        private string CalcularSiguienteFolio(int idCarrera, int? idBajaExcluir = null, int? idPersonaParaReuso = null)
        {
            var ahora = DateTime.Now;
            var periodoActual = GetPeriodoFromDate(ahora);
            var añoActual = ahora.Year;

            // NOTA: Para el calculo de folios ocupados, debemos considerar TODAS las bajas
            // del periodo actual, independientemente de su estado (Activo/Reingreso).
            // Esto incluye bajas historicas de reingreso que aun ocupan su folio.
            var query = db.Bajas.AsQueryable();

            // Excluir la baja actual si se proporciona (para no contar nuestro propio folio como ocupado al editar)
            if (idBajaExcluir.HasValue)
            {
                query = query.Where(b => b.IdBaja != idBajaExcluir.Value);
            }

            // Para bajas de reingreso, DatosPersonales.IdCarrera ya fue actualizado a la nueva carrera,
            // por lo que no es fiable. Usamos el campo de texto Baja.Carrera (historico) como pivote,
            // igual que hace el reporte en ReporteBajas.
            string nombreCarrera = db.Carreras
                .Where(c => c.IdCarrera == idCarrera)
                .Select(c => c.Nombre)
                .FirstOrDefault() ?? "";

            var foliosCarrera = query
                .Join(db.DatosPersonales, b => b.IdPersona, p => p.IdPersona, (b, p) => new { b.Folio, b.Fecha, b.Carrera, b.Reingreso, p.IdCarrera })
                .Where(x => x.Folio != null && x.Fecha != default(DateTime) &&
                            ((x.Reingreso != 1 && x.IdCarrera == idCarrera) ||
                             (x.Reingreso == 1 && x.Carrera != null && x.Carrera.Trim() == nombreCarrera.Trim())))
                .ToList();

            var foliosOcupados = foliosCarrera
                .Where(x => x.Fecha != default(DateTime) &&
                            GetPeriodoFromDate(x.Fecha) == periodoActual &&
                            x.Fecha.Year == añoActual)
                .Select(x => int.TryParse(x.Folio, out var n) ? n : 0)
                .Where(n => n > 0)
                .OrderBy(n => n)
                .ToList();

            // Lógica de "Huecos"
            int siguienteFolio = 1;
            foreach (var num in foliosOcupados)
            {
                if (num == siguienteFolio)
                {
                    siguienteFolio++;
                }
                else if (num > siguienteFolio)
                {
                    break;
                }
            }

            return siguienteFolio.ToString("000");
        }


    }
}

public class DatosAlumnoAjaxDto
{
    public int? IdEntrevistaInicial { get; set; }
    public int? IdPersona { get; set; }
    public string Nombre { get; set; }
    public string Grupo { get; set; }
    public string Carrera { get; set; }
    public string Area { get; set; }
    public string Turno { get; set; }
    public string Especialidad { get; set; }
    public string Cuatrimestre { get; set; }
    public string Vulnerable { get; set; }
    public string Vulnerabilidad { get; set; }
    public string Status { get; set; }
    public string Mensaje { get; set; }
}

public class BajaReporteItem
{
    public Baja Baja { get; set; }
    public string NombreTutorReal { get; set; }
    public int IdCarrera { get; set; }
}