using Microsoft.Reporting.WebForms;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using PlataformaWeb;
using PlataformaWeb.BecasTransporte.Models;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using PlataformaWeb.Helpers;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace PlataformaWeb.Controllers.Asesorar
{
    [CustomAuthorize(Nivel = 2)]
    public class EntrevistasController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Entrevistas
        public ActionResult Index()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdCarrera == usuario.IdCarrera).OrderByDescending(x => x.IdTutoriaGrupal).ToList();
            foreach (var item in tutorias)
            {
                var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                item.Nomenclatura = x.ToString();
            }
            TutoriaGrupal rem = new TutoriaGrupal();


            rem.IdTutoriaGrupal = -1;
            rem.Nomenclatura = "Alumnos Removidos";
            tutorias.Add(rem);

            ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
            return View();
        }
        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(TutoriaGrupal tutoriaGrupal)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (tutoriaGrupal.IdTutoriaGrupal == -1)
            {
                return RedirectToAction("Grupo", new { id = -1 });
            }

            if (tutoriaGrupal.IdTutoriaGrupal == 0)
            {
                ViewBag.Mensaje = "Seleccione un grupo por favor.";
            }
            else
            {
                return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal, nom = tutoriaGrupal.Nomenclatura });
                //TempGrupo alum = new TempGrupo();
                //var alu = db.TempGrupos.FirstOrDefault(x => x.IdUsuario == usuario.IdUsuario);
                //if (alu == null)
                //{
                //    alum.IdUsuario = usuario.IdUsuario;
                //    alum.Grupo = tutoriaGrupal.IdTutoriaGrupal;

                //    db.TempGrupos.Add(alum);
                //    db.SaveChanges();
                //    return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal });
                //}
                //else
                //{
                //    alum = alu;
                //    alum.IdUsuario = usuario.IdUsuario;
                //    alum.Grupo = tutoriaGrupal.IdTutoriaGrupal;
                //    db.Entry(alum).State = EntityState.Modified;
                //    db.SaveChanges();
                //    return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal });
                //}


            }

            List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdCarrera == usuario.IdCarrera).ToList();
            foreach (var item in tutorias)
            {
                var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                item.Nomenclatura = x.ToString();
            }
            TutoriaGrupal rem = new TutoriaGrupal();
            rem.IdTutoriaGrupal = -1;
            rem.Nomenclatura = "Alumnos removidos";

            tutorias.Add(rem);
            ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
            return View();
        }
        public ActionResult Grupo()
        {
            // Limpiar tokens expirados
            AccesoSeguroHelper.LimpiarTokens();
            return View();
        }

        public JsonResult GetGroupInfo(int idGrupo)
        {
            var Grupo = db.TutoriaGrupals.Find(idGrupo);
            return Json(Grupo, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Error()
        {
            ViewBag.Mensaje1 = "Ocurrió un error al procesar los datos del alumno, pida al alumno completar la entrevista y vuelva a intentar";
            return View();
        }

        [LecturaPermitida]
        [HttpPost]
        public JsonResult ObtenerNavegacionEntrevistas(int? idPersona, int? idGrupo)
        {

            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return Json(new { success = false, message = "Sesión expirada o usuario no encontrado." });
            }

            // Buscamos la información del grupo al que pertenece la persona actual
            var alumnoActual = db.DatosPersonales
                .Include(dp => dp.Carrera)
                .Include(dp => dp.Grado)
                .Include(dp => dp.Grupo)
                // Solo obtener las propiedades necesarias para la búsqueda del grupo
                .FirstOrDefault(x => x.IdPersona == idPersona);

            if (alumnoActual == null)
            {
                return Json(new { success = false, message = "Alumno actual no encontrado." });
            }

            // 2. CONSTRUIR LISTA COMPLETA DEL GRUPO
            // Obtener todos los alumnos que comparten los mismos filtros de grupo
            var alumnosDelGrupo = db.DatosPersonales
            .AsNoTracking()
            .Where(x => x.IdCarrera == alumnoActual.IdCarrera
                     && x.IdGrado == alumnoActual.IdGrado
                     && x.IdGrupo == alumnoActual.IdGrupo
                     && x.IdTurno == alumnoActual.IdTurno
                     && x.IdPeriodo == alumnoActual.IdPeriodo
                     && x.Año == alumnoActual.Año)
            // CRUCIAL: Ordenar por un criterio estable (e.g., Nombre o Matrícula)
            .OrderBy(x => x.Nombre)
            .ThenBy(x => x.Matricula)
            .Select(x => new { x.IdPersona })
            .ToList();

            // 3. ENCONTRAR POSICIÓN Y CALCULAR IDS
            int indiceActual = alumnosDelGrupo.FindIndex(x => x.IdPersona == idPersona);

            int? idAnterior = indiceActual > 0 ?
                alumnosDelGrupo[indiceActual - 1].IdPersona : (int?)null;

            int? idSiguiente = indiceActual < alumnosDelGrupo.Count - 1 ?
                alumnosDelGrupo[indiceActual + 1].IdPersona : (int?)null;

            // 4. GENERAR TOKENS DE SEGURIDAD (Asumiendo que existe un helper)
            // ESTO ES CLAVE PARA LA SEGURIDAD
            string tokenAnterior = idAnterior.HasValue ?
                AccesoSeguroHelper.GenerarToken(idAnterior.Value, usuario.IdUsuario) : null;

            string tokenSiguiente = idSiguiente.HasValue ?
                AccesoSeguroHelper.GenerarToken(idSiguiente.Value, usuario.IdUsuario) : null;

            // 5. DEVOLVER RESULTADO
            return Json(new
            {
                success = true,
                idAnterior = idAnterior,
                idSiguiente = idSiguiente,
                tokenAnterior = tokenAnterior,
                tokenSiguiente = tokenSiguiente,
                posicion = indiceActual + 1,
                total = alumnosDelGrupo.Count
            });
        }

        public ActionResult Eliminar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            List<DatosPersonales> us = new List<DatosPersonales>();
            DatosPersonales datos = db.DatosPersonales.Find(id);
            if (datos == null)
            {
                return HttpNotFound();
            }

            DatosPersonales ei = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            TutoriaGrupal tutgrupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == ei.IdCarrera && x.IdGrado == ei.IdGrado && x.IdGrupo == ei.IdGrupo && x.IdTurno == ei.IdTurno && x.IdPeriodo == ei.IdPeriodo && x.Año == ei.Año);
            int idtutgrup;
            if (tutgrupo == null)
            {
                idtutgrup = -1;
            }
            else
            {
                idtutgrup = tutgrupo.IdTutoriaGrupal;
            }
            ViewBag.id = idtutgrup;
            return View(datos);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            DatosPersonales da = db.DatosPersonales.Find(id);
            AspectosAcademicos aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
            AspectosEconomicos ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
            AspectosPersonales ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);
            EntrevistaInicial ei = db.EntrevistaInicials.FirstOrDefault(x => x.IdPersona == id);

            Estudiante becaTransporte = db.Estudiantes.FirstOrDefault(x => x.Matricula == da.Matricula && x.Nombre == da.Nombre);
            Usuario usuarioAlumno = db.Usuarios.FirstOrDefault(x => x.UserName == da.Matricula && x.NombreCompleto == da.Email);

            TutoriaGrupal tutgrupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == da.IdCarrera &&
            x.IdGrado == da.IdGrado && x.IdGrupo == da.IdGrupo && x.IdTurno == da.IdTurno &&
            x.IdPeriodo == da.IdPeriodo && x.Año == da.Año);
            int idtutgrup;
            if (tutgrupo == null)
            {
                idtutgrup = -1;
            }
            else
            {
                idtutgrup = tutgrupo.IdTutoriaGrupal;
            }
            int rid = idtutgrup;

            List<Baja> datos6 = db.Bajas.Where(x => x.IdPersona == id).ToList();
            List<Individual> datos7 = db.Individuals.Where(x => x.IdPersona == id).ToList();

            if (datos7 != null)
            {
                foreach (Individual i in datos7)
                {
                    List<Seguimiento> datos8 = db.Seguimientoes.Where(x => x.IdIndividual == i.IdIndividual).ToList();
                    if (datos8 != null)
                    {
                        foreach (Seguimiento s in datos8)
                        {
                            db.Seguimientoes.Remove(s);
                            db.SaveChanges();
                        }
                    }
                    db.Individuals.Remove(i);
                    db.SaveChanges();
                }
            }
            if (datos6 != null)
            {
                foreach (Baja b in datos6)
                {
                    db.Bajas.Remove(b);
                    db.SaveChanges();
                }
            }
            if (becaTransporte != null)
            {
                db.Estudiantes.Remove(becaTransporte);
            }
            if (usuarioAlumno != null)
            {
                db.Usuarios.Remove(usuarioAlumno);
            }
            if (ei != null)
            {
                db.EntrevistaInicials.Remove(ei);
            }
            if (ap != null)
            {
                db.AspectosPersonales.Remove(ap);
            }
            if (ae != null)
            {
                db.AspectosEconomicos.Remove(ae);
            }
            if (aa != null)
            {
                db.AspectosAcademicos.Remove(aa);
            }
            if (da != null)
            {
                db.DatosPersonales.Remove(da);
            }

            db.SaveChanges();
            return RedirectToAction("Grupo");
        }

        // **MÉTODO AUXILIAR PARA VALIDAR ACCESO**
        private bool TieneAccesoAlAlumno(Usuario usuario, DatosPersonales datosAlumno)
        {
            if (usuario == null || datosAlumno == null)
            {
                return false;
            }

            // Nivel 4 (Master) - Acceso total a todo
            if (usuario.IdNivel == 4)
            {
                return true;
            }

            // Nivel 3 (Coordinador de carrera) - Solo puede ver alumnos de su carrera
            if (usuario.IdNivel == 3)
            {
                return datosAlumno.IdCarrera == usuario.IdCarrera;
            }

            // Nivel 2 (Tutor/Docente) - Solo puede ver alumnos de su carrera
            if (usuario.IdNivel == 2)
            {
                return datosAlumno.IdCarrera == usuario.IdCarrera;
            }

            // Nivel 1 (Alumno) - Solo puede ver su propia información
            if (usuario.IdNivel == 1)
            {
                return datosAlumno.Matricula == usuario.UserName;
            }

            // Por defecto, denegar acceso
            return false;
        }

        public ActionResult Detalles(int? idPersona, int? id, string token)
        {
            if (!idPersona.HasValue)
            {
                TempData["Error"] = "Parámetros inválidos";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Validar token
            if (string.IsNullOrEmpty(token) || !AccesoSeguroHelper.ValidarToken(token, idPersona.Value, usuario.IdUsuario))
            {
                TempData["Error"] = "Token inválido o expirado. Por favor acceda desde la lista de alumnos.";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            DatosPersonales datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);

            if (datos == null)
            {
                // Manejo de error: la persona no existe
                return RedirectToAction("Grupo", new { error = "El alumno no existe." });
            }

            if (!TieneAccesoAlAlumno(usuario, datos))
            {
                TempData["Error"] = "No tiene permisos para ver esta información";
                return RedirectToAction("Index", "Entrevistas");
            }

            // Determinar el grupo
            TutoriaGrupal tutgrupo = db.TutoriaGrupals.FirstOrDefault(x =>
                x.IdCarrera == datos.IdCarrera &&
                x.IdGrado == datos.IdGrado &&
                x.IdGrupo == datos.IdGrupo &&
                x.IdTurno == datos.IdTurno &&
                x.IdPeriodo == datos.IdPeriodo &&
                x.Año == datos.Año);

            int idtutgrup = tutgrupo == null ? -1 : tutgrupo.IdTutoriaGrupal;

            // Obtener entrevista
            EntrevistaInicial entrevista;
            if (id.HasValue)
            {
                entrevista = db.EntrevistaInicials.FirstOrDefault(x => x.IdEntrevistaInicial == id);
            }
            else
            {
                entrevista = db.EntrevistaInicials
                    .Where(x => x.IdPersona == idPersona && x.Grados.Nombre == datos.Grado.Nombre)
                    .OrderByDescending(x => x.Fecha)
                    .FirstOrDefault();
            }

            // Pasar datos necesarios al ViewBag para la navegación
            //ViewBag.IdPersona = idPersona.Value;
            ViewBag.id = idtutgrup;
            ViewBag.Token = token;
            ViewBag.datos = datos;
            /* ViewBag.IdGrupo = idtutgrup;*/ // Alternativa por si usas este nombre

            // Cargar datos adicionales para la vista
            if (entrevista != null)
            {
                // Cargar relaciones y datos necesarios
                // ... tu código existente para cargar los datos de la entrevista
            }

            return View(entrevista);
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

        public ActionResult ReporteEntrevista(int? id, int? entrevistaId, string token)
        {
            if (!id.HasValue)
            {
                TempData["Error"] = "Parámetros inválidos";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // ❌ VALIDAR TOKEN
            if (string.IsNullOrEmpty(token) || !AccesoSeguroHelper.ValidarToken(token, id.Value, usuario.IdUsuario))
            {
                TempData["Error"] = "Token inválido o expirado. Por favor acceda desde la lista de alumnos.";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            DatosPersonales dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (dp == null)
            {
                TempData["Error"] = "Alumno no encontrado";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            if (!TieneAccesoAlAlumno(usuario, dp))
            {
                TempData["Error"] = "No tiene permisos para generar este reporte";
                return RedirectToAction("Index", "Entrevistas");
            }

            List<EntrevistaInicial> alumno;

            if (entrevistaId.HasValue)
            {
                alumno = db.EntrevistaInicials.Where(x => x.IdEntrevistaInicial == entrevistaId).ToList();
            }
            else
            {
                alumno = db.EntrevistaInicials.Where(x => x.Grados.Nombre == dp.Grado.Nombre && x.IdPersona == id).ToList();
            }

            try
            {
                TutoriaGrupal tutgrupo = db.TutoriaGrupals.FirstOrDefault(x =>
                    x.IdCarrera == dp.IdCarrera &&
                    x.IdGrado == dp.IdGrado &&
                    x.IdGrupo == dp.IdGrupo &&
                    x.IdTurno == dp.IdTurno &&
                    x.IdPeriodo == dp.IdPeriodo &&
                    x.Año == dp.Año);

                int idtutgrup = tutgrupo == null ? -1 : tutgrupo.IdTutoriaGrupal;
                ViewBag.id = idtutgrup;

                foreach (EntrevistaInicial v in alumno)
                {
                    string grupo = "";
                    var t = db.Turnoes.FirstOrDefault(x => x.IdTurno == v.IdTurno);
                    var c = db.Carreras.FirstOrDefault(x => x.IdCarrera == v.IdCarrera);
                    var grado = db.Gradoes.FirstOrDefault(x => x.IdGrado == v.IdGrado);
                    var grup = db.Grupoes.FirstOrDefault(x => x.IdGrupo == v.IdGrupo);

                    if (t.Nombre == "Matutino")
                        grupo += "M";
                    else if (t.Nombre == "Vespertino")
                        grupo += "I";
                    else if (t.Nombre == "Despresurizado")
                        grupo += "D";

                    grupo += c.Nomenclatura + grado.Nombre + grup.Nombre;
                    v.Grupo = grupo;
                    v.Ciudad = db.Respuesta1.FirstOrDefault(x => x.IdCiudad == v.IdCiudad).Nombre;
                    v.PTrabajas = db.Respuesta2.FirstOrDefault(x => x.IdTrabajo == v.IdTrabajo).Nombre;
                    //v.TDependientes = db.Respuesta3.FirstOrDefault(x => x.IdDependientes == v.IdDependientes).Nombre;
                    //v.TecnicaEstSiNo = db.Respuesta0.FirstOrDefault(x => x.IdTecnicaEst == v.IdTecnicaEst).Nombre;
                    v.Casado = db.Respuesta4.FirstOrDefault(x => x.IdCasado == v.IdCasado).Nombre;
                    v.Hijos = db.Respuesta5.FirstOrDefault(x => x.IdHijo == v.IdHijo).Nombre;
                    v.Enfermedad = db.Respuesta6.FirstOrDefault(x => x.IdEnfermedad == v.IdEnfermedad).Nombre;
                    v.Fuma = db.Respuesta7.FirstOrDefault(x => x.IdFuma == v.IdFuma).Nombre;
                    v.Bebida = db.Respuesta8.FirstOrDefault(x => x.IdBebida == v.IdBebida).Nombre;
                    v.VidaSinSentido = db.Respuesta9.FirstOrDefault(x => x.IdVidaSinSentido == v.IdVidaSinSentido).Nombre;
                    v.Observaciones = db.ObservacioFamilias.FirstOrDefault(x => x.IdObservacionFamilia == v.IdObservacionFamilia).Nombre;
                    v.Vulnerable = db.Respuesta10.FirstOrDefault(x => x.IdVulnerable == v.IdVulnerable).Nombre;
                    v.Embarazo = db.Respuesta11.FirstOrDefault(x => x.IdEmbarazo == v.IdEmbarazo).Nombre;
                    v.EleccionVulnerabilidad = db.Vulnerable.FirstOrDefault(x => x.IdEleccionVunerabilidad == v.IdEleccionVunerabilidad).Nombre;
                    v.ListaBachillerato = db.Respuesta12.FirstOrDefault(x => x.IdListaBachillerato == v.IdListaBachillerato).Nombre;
                    v.EquipoComp = db.Respuesta13.FirstOrDefault(x => x.IdEquipoComp == v.IdEquipoComp).Nombre;
                    v.TipoDispositivo = db.Respuesta14.FirstOrDefault(x => x.IdTipoDispositivo == v.IdTipoDispositivo).Nombre;
                    v.AccesoInternet = db.Respuesta15.FirstOrDefault(x => x.IdAccesoInternet == v.IdAccesoInternet).Nombre;
                    v.TipoFamiliar = db.Respuesta16.FirstOrDefault(x => x.IdTipoFamiliar == v.IdTipoFamiliar).Nombre;
                    v.IngresoMes = db.Respuesta17.FirstOrDefault(x => x.IdIngresoMes == v.IdIngresoMes).Nombre;
                    v.SolicitarBeca = db.Respuesta18.FirstOrDefault(x => x.IdSolicitarBeca == v.IdSolicitarBeca).Nombre;
                    v.Foto = dp.Foto.Replace("data:image/jpg;base64,", "").Replace("data:image/jpeg;base64,", "");
                }

                ReportViewer report1 = new ReportViewer();
                ReportDataSource rds = new ReportDataSource();
                rds.Value = alumno;
                rds.Name = "Entrevistas";
                report1.LocalReport.EnableExternalImages = true;
                report1.LocalReport.DataSources.Add(rds);
                report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/EntrevistaInicialAlumno.rdlc");
                ViewBag.ReportViewer = report1;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = ex.Message;
            }

            return View();
        }

        public ActionResult ReporteEntrevistaSegumiento(int? id, int? entrevistaId, string token)
        {
            if (!id.HasValue)
            {
                TempData["Error"] = "Parámetros inválidos";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // ❌ VALIDAR TOKEN
            if (string.IsNullOrEmpty(token) || !AccesoSeguroHelper.ValidarToken(token, id.Value, usuario.IdUsuario))
            {
                TempData["Error"] = "Token inválido o expirado. Por favor acceda desde la lista de alumnos.";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            DatosPersonales dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (dp == null)
            {
                TempData["Error"] = "Alumno no encontrado";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            if (!TieneAccesoAlAlumno(usuario, dp))
            {
                TempData["Error"] = "No tiene permisos para generar este reporte";
                return RedirectToAction("Index", "Entrevistas");
            }

            List<EntrevistaInicial> alumno;

            if (entrevistaId.HasValue)
            {
                alumno = db.EntrevistaInicials.Where(x => x.IdEntrevistaInicial == entrevistaId).ToList();
            }
            else
            {
                alumno = db.EntrevistaInicials.Where(x => x.Grados.Nombre == dp.Grado.Nombre && x.IdPersona == id).ToList();
            }

            try
            {
                TutoriaGrupal tutgrupo = db.TutoriaGrupals.FirstOrDefault(x =>
                    x.IdCarrera == dp.IdCarrera &&
                    x.IdGrado == dp.IdGrado &&
                    x.IdGrupo == dp.IdGrupo &&
                    x.IdTurno == dp.IdTurno &&
                    x.IdPeriodo == dp.IdPeriodo &&
                    x.Año == dp.Año);

                int idtutgrup = tutgrupo == null ? -1 : tutgrupo.IdTutoriaGrupal;
                ViewBag.id = idtutgrup;

                foreach (EntrevistaInicial v in alumno)
                {
                    string grupo = "";
                    var t = db.Turnoes.FirstOrDefault(x => x.IdTurno == v.IdTurno);
                    var c = db.Carreras.FirstOrDefault(x => x.IdCarrera == v.IdCarrera);
                    var grado = db.Gradoes.FirstOrDefault(x => x.IdGrado == v.IdGrado);
                    var grup = db.Grupoes.FirstOrDefault(x => x.IdGrupo == v.IdGrupo);

                    if (t.Nombre == "Matutino")
                        grupo += "M";
                    else if (t.Nombre == "Vespertino")
                        grupo += "I";
                    else if (t.Nombre == "Despresurizado")
                        grupo += "D";

                    grupo += c.Nomenclatura + grado.Nombre + grup.Nombre;
                    v.Grupo = grupo;
                    v.Ciudad = db.Respuesta1.FirstOrDefault(x => x.IdCiudad == v.IdCiudad).Nombre;
                    v.PTrabajas = db.Respuesta2.FirstOrDefault(x => x.IdTrabajo == v.IdTrabajo).Nombre;
                    //v.TDependientes = db.Respuesta3.FirstOrDefault(x => x.IdDependientes == v.IdDependientes).Nombre;
                    //v.TecnicaEstSiNo = db.Respuesta0.FirstOrDefault(x => x.IdTecnicaEst == v.IdTecnicaEst).Nombre;
                    v.Casado = db.Respuesta4.FirstOrDefault(x => x.IdCasado == v.IdCasado).Nombre;
                    v.Hijos = db.Respuesta5.FirstOrDefault(x => x.IdHijo == v.IdHijo).Nombre;
                    v.Enfermedad = db.Respuesta6.FirstOrDefault(x => x.IdEnfermedad == v.IdEnfermedad).Nombre;
                    v.Fuma = db.Respuesta7.FirstOrDefault(x => x.IdFuma == v.IdFuma).Nombre;
                    v.Bebida = db.Respuesta8.FirstOrDefault(x => x.IdBebida == v.IdBebida).Nombre;
                    v.VidaSinSentido = db.Respuesta9.FirstOrDefault(x => x.IdVidaSinSentido == v.IdVidaSinSentido).Nombre;
                    v.Observaciones = db.ObservacioFamilias.FirstOrDefault(x => x.IdObservacionFamilia == v.IdObservacionFamilia).Nombre;
                    v.Vulnerable = db.Respuesta10.FirstOrDefault(x => x.IdVulnerable == v.IdVulnerable).Nombre;
                    v.Embarazo = db.Respuesta11.FirstOrDefault(x => x.IdEmbarazo == v.IdEmbarazo).Nombre;
                    v.EleccionVulnerabilidad = db.Vulnerable.FirstOrDefault(x => x.IdEleccionVunerabilidad == v.IdEleccionVunerabilidad).Nombre;
                    v.ListaBachillerato = db.Respuesta12.FirstOrDefault(x => x.IdListaBachillerato == v.IdListaBachillerato).Nombre;
                    v.EquipoComp = db.Respuesta13.FirstOrDefault(x => x.IdEquipoComp == v.IdEquipoComp).Nombre;
                    v.TipoDispositivo = db.Respuesta14.FirstOrDefault(x => x.IdTipoDispositivo == v.IdTipoDispositivo).Nombre;
                    v.AccesoInternet = db.Respuesta15.FirstOrDefault(x => x.IdAccesoInternet == v.IdAccesoInternet).Nombre;
                    v.TipoFamiliar = db.Respuesta16.FirstOrDefault(x => x.IdTipoFamiliar == v.IdTipoFamiliar).Nombre;
                    v.IngresoMes = db.Respuesta17.FirstOrDefault(x => x.IdIngresoMes == v.IdIngresoMes).Nombre;
                    v.SolicitarBeca = db.Respuesta18.FirstOrDefault(x => x.IdSolicitarBeca == v.IdSolicitarBeca).Nombre;
                    v.Foto = dp.Foto.Replace("data:image/jpg;base64,", "").Replace("data:image/jpeg;base64,", "");
                }

                ReportViewer report1 = new ReportViewer();
                ReportDataSource rds = new ReportDataSource();
                rds.Value = alumno;
                rds.Name = "Entrevistas";
                report1.LocalReport.EnableExternalImages = true;
                report1.LocalReport.DataSources.Add(rds);
                report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/EntrevistaSeguimientoAlumno.rdlc");
                ViewBag.ReportViewer = report1;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = ex.Message;
            }

            return View();
        }

        [LecturaPermitida]
        [HttpPost]
        public JsonResult GenerarTokenAcceso(int idPersona)
        {
            Usuario usuario = Session["Usuario"] as Usuario;

            if (usuario == null)
            {
                return Json(new { success = false, message = "Sesión expirada" });
            }

            // Validar que el usuario tiene acceso al alumno
            var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);

            if (datos == null || !TieneAccesoAlAlumno(usuario, datos))
            {
                return Json(new { success = false, message = "No tiene permisos" });
            }

            string token = AccesoSeguroHelper.GenerarToken(idPersona, usuario.IdUsuario);

            return Json(new { success = true, token = token });
        }

        public ActionResult ExportarDetallesExcel(int? id, int? idPersona, string token)
        {
            Usuario usuario = Session["Usuario"] as Usuario;

            if (usuario == null)
            {
                TempData["Error"] = "Sesión expirada. Por favor inicie sesión nuevamente.";
                return RedirectToAction("Index", "Home");
            }

            // Validar que solo nivel 3 y 4 pueden exportar
            if (usuario.IdNivel != 3 && usuario.IdNivel != 4)
            {
                TempData["Error"] = "No tiene permisos para exportar información.";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            // Validar token
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Acceso no autorizado. Debe acceder desde la lista de alumnos.";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            EntrevistaInicial comp = null;
            DatosPersonales datos = null;

            try
            {
                if (idPersona.HasValue)
                {
                    if (!AccesoSeguroHelper.ValidarToken(token, idPersona.Value, usuario.IdUsuario))
                    {
                        TempData["Error"] = "Token inválido o expirado.";
                        return RedirectToAction("Grupo", "Entrevistas");
                    }

                    datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);

                    if (datos == null)
                    {
                        TempData["Error"] = "Alumno no encontrado";
                        return RedirectToAction("Grupo", "Entrevistas");
                    }

                    if (!TieneAccesoAlAlumno(usuario, datos))
                    {
                        TempData["Error"] = "No tiene permisos para exportar la información de este alumno";
                        return RedirectToAction("Index", "Entrevistas");
                    }

                    comp = db.EntrevistaInicials
                        .Where(x => x.Grados.Nombre == datos.Grado.Nombre && x.IdPersona == idPersona)
                        .FirstOrDefault();

                    if (comp == null)
                    {
                        comp = db.EntrevistaInicials
                            .Where(x => x.IdPersona == idPersona)
                            .OrderByDescending(e => e.Fecha)
                            .FirstOrDefault();
                    }
                }
                else if (id.HasValue)
                {
                    comp = db.EntrevistaInicials.FirstOrDefault(x => x.IdEntrevistaInicial == id);

                    if (comp == null)
                    {
                        TempData["Error"] = "Entrevista no encontrada";
                        return RedirectToAction("Grupo", "Entrevistas");
                    }

                    if (!AccesoSeguroHelper.ValidarToken(token, comp.IdPersona, usuario.IdUsuario))
                    {
                        TempData["Error"] = "Token inválido o expirado.";
                        return RedirectToAction("Grupo", "Entrevistas");
                    }

                    datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == comp.IdPersona);

                    if (datos == null || !TieneAccesoAlAlumno(usuario, datos))
                    {
                        TempData["Error"] = "No tiene permisos para exportar la información de este alumno";
                        return RedirectToAction("Index", "Entrevistas");
                    }
                }
                else
                {
                    TempData["Error"] = "Parámetros inválidos";
                    return RedirectToAction("Grupo", "Entrevistas");
                }

                if (comp == null)
                {
                    TempData["Error"] = "No se encontraron datos de entrevista para exportar";
                    return RedirectToAction("Grupo", "Entrevistas");
                }

                // Obtener información de vulnerabilidad
                string esVulnerable = "No";
                string tipoVulnerabilidad = "N/A";

                if (comp.IdVulnerable == 1)
                {
                    esVulnerable = "Sí";
                    var vulnerabilidad = db.Vulnerable.FirstOrDefault(x => x.IdEleccionVunerabilidad == comp.IdEleccionVunerabilidad);
                    if (vulnerabilidad != null)
                    {
                        tipoVulnerabilidad = vulnerabilidad.Nombre;
                    }
                }

                var respuestaVulnerable = db.Respuesta10.FirstOrDefault(x => x.IdVulnerable == comp.IdVulnerable);
                string respuestaVulnerableNombre = respuestaVulnerable?.Nombre ?? "N/A";

                // Crear archivo Excel con EPPlus 4.5.3
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Detalles Entrevista");

                    int row = 1;
                    int col = 1;

                    // Título principal
                    string nombreGrado = comp.Grados?.Nombre ?? "1";
                    worksheet.Cells[row, col].Value = nombreGrado == "1" ? "ENTREVISTA INICIAL" : $"ENTREVISTA DE SEGUIMIENTO - {nombreGrado}°";
                    worksheet.Cells[row, col, row, 2].Merge = true;
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Font.Size = 16;
                    worksheet.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    row += 2;

                    // DATOS PERSONALES
                    worksheet.Cells[row, col].Value = "DATOS PERSONALES";
                    worksheet.Cells[row, col, row, 2].Merge = true;
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Font.Size = 14;
                    worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    row++;

                    worksheet.Cells[row, col].Value = "Fecha:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Fecha.ToString("dd/MM/yyyy");
                    row++;

                    worksheet.Cells[row, col].Value = "Matrícula:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Matricula ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Nombre:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Edad:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Edad > 0 ? $"{comp.Edad} años" : "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Sexo:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Sexo ?? "Sin actualizar";
                    row++;

                    worksheet.Cells[row, col].Value = "Grado:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Grados?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Grupo:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Grupos?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Carrera:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = !string.IsNullOrEmpty(comp.CarreraNom) ? comp.CarreraNom : (comp.Carreras?.Nombre ?? "N/A");
                    row++;

                    worksheet.Cells[row, col].Value = "Especialidad:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = !string.IsNullOrEmpty(comp.Area) ? comp.Area : "Sin actualizar";
                    row++;

                    worksheet.Cells[row, col].Value = "Turno:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Turnos?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Dirección:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Direccion ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Teléfono celular:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Celular ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Teléfono de casa:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Telefono ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Teléfono de emergencia:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.TelEmergencia ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Correo electrónico:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Email ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = comp.Sexo == "HOMBRE" ? "¿Está casado?:" : "¿Está casada?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta4?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Tiene hijos?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta5?.Nombre ?? "N/A";
                    row += 2;

                    // ASPECTOS ECONÓMICOS
                    worksheet.Cells[row, col].Value = "ASPECTOS ECONÓMICOS";
                    worksheet.Cells[row, col, row, 2].Merge = true;
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Font.Size = 14;
                    worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    row++;

                    worksheet.Cells[row, col].Value = "¿Reside en esta ciudad?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta1?.Nombre ?? "N/A";
                    row++;

                    if (comp.IdCiudad == 2)
                    {
                        worksheet.Cells[row, col].Value = "Especifique en qué ciudad reside:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Ciudad ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿Con quién vive actualmente?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta16?.Nombre ?? "N/A";
                    row++;

                    if (!string.IsNullOrEmpty(comp.Familiar))
                    {
                        worksheet.Cells[row, col].Value = "Especifique con quién vive:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Familiar;
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿Trabaja?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta2?.Nombre ?? "N/A";
                    row++;

                    if (comp.IdTrabajo == 1)
                    {
                        worksheet.Cells[row, col].Value = "Lugar de trabajo:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Trabaja ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿A qué se dedica el papá?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.OcupacionPapa ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿A qué se dedica la mamá?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.OcupacionMama ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Cantidad de hermanos:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.CantidadHermano ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Ingreso mensual aproximado por miembro:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta17?.Nombre ?? "N/A";
                    row++;

                    if (nombreGrado != "1")
                    {
                        worksheet.Cells[row, col].Value = "¿Cuántas personas viven en casa? (Incluyéndole):";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.CantidadPersonas ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Cuántas personas trabajan? (Incluyéndole):";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.CantidadTrabajan ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Considera que necesita beca o algún tipo de apoyo?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Respuesta18?.Nombre ?? "N/A";
                        row++;
                    }

                    row += 2;

                    // ASPECTOS ACADÉMICOS
                    worksheet.Cells[row, col].Value = "ASPECTOS ACADÉMICOS";
                    worksheet.Cells[row, col, row, 2].Merge = true;
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Font.Size = 14;
                    worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                    row++;

                    if (nombreGrado == "1")
                    {
                        worksheet.Cells[row, col].Value = "¿De qué bachillerato egresaste?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Respuesta12?.Nombre ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "Bachillerato específico:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Bachillerato ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "Especialidad en el bachillerato:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Especialidad ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = nombreGrado == "1" ? "Promedio general (Bachillerato):" : "Promedio general (Universidad):";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Promedio ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Cuenta con equipo de cómputo para estudiar?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta13?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "Tipo de dispositivo específico:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta14?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Cuenta con servicio de conexión a internet?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta15?.Nombre ?? "N/A";
                    row++;

                    if (nombreGrado != "1")
                    {
                        worksheet.Cells[row, col].Value = "¿Cómo es el rendimiento en clase?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.RendimientoClase ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Cómo es la experiencia con los profesores?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.ExperienciaProfe ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Qué materias reprobó el periodo anterior?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.MateriasRepro ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Qué materia se le dificulta más actualmente?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.MateriasDif ?? "N/A";
                        row++;
                    }
                    row += 2;

                    // ASPECTOS PERSONALES
                    worksheet.Cells[row, col].Value = "ASPECTOS PERSONALES";
                    worksheet.Cells[row, col, row, 2].Merge = true;
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Font.Size = 14;
                    worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                    row++;

                    if (comp.IdHijo == 1)
                    {
                        worksheet.Cells[row, col].Value = "Cantidad de hijos:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.CantidadHijo ?? "N/A";
                        row++;
                    }

                    if (comp.Sexo == "MUJER")
                    {
                        worksheet.Cells[row, col].Value = "¿Está embarazada?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Respuesta11?.Nombre ?? "N/A";
                        row++;
                    }

                    if (comp.IdHijo == 1)
                    {
                        worksheet.Cells[row, col].Value = "Cantidad de hijos:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.CantidadHijo ?? "N/A";
                        row++;
                    }

                    if (comp.Sexo == "MUJER")
                    {
                        worksheet.Cells[row, col].Value = "¿Está embarazada?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Respuesta11?.Nombre ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿Padece alguna enfermedad?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta6?.Nombre ?? "N/A";
                    row++;

                    if (comp.IdEnfermedad == 1)
                    {
                        worksheet.Cells[row, col].Value = "Enfermedad o alergia específica:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Especifica ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿Fuma?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta7?.Nombre ?? "N/A";
                    row++;

                    if (comp.IdFuma == 1)
                    {
                        worksheet.Cells[row, col].Value = "Cantidad y frecuencia (fuma):";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.CantidadFuma ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿Ingiere bebidas alcohólicas?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta8?.Nombre ?? "N/A";
                    row++;

                    if (comp.IdBebida == 1)
                    {
                        worksheet.Cells[row, col].Value = "Cantidad y frecuencia (bebidas):";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.CantidadBedida ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿En la familia ha observado que hay violencia?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Observacio?.Nombre ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Ha pensado que la vida no tiene sentido?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.Respuesta9?.Nombre ?? "N/A";
                    row++;

                    if (comp.IdVidaSinSentido == 1)
                    {
                        worksheet.Cells[row, col].Value = "¿Por qué (vida sin sentido)?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Porque ?? "N/A";
                        row++;
                    }

                    worksheet.Cells[row, col].Value = "¿Considera que el apoyo de la familia es el adecuado cuando tiene un problema?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.ApoyoFamiliaEnProblemas ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Por qué (apoyo familiar)?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.ApoyoFamiliaEnProblemasPorque ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Los problemas económicos de la familia le desconcentran?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.ProblemasEconomicosFamilia ?? "N/A";
                    row++;

                    worksheet.Cells[row, col].Value = "¿Por qué (problemas económicos)?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = comp.ProblemasEconomicosFamiliaPorque ?? "N/A";
                    row++;

                    if (nombreGrado != "1")
                    {
                        worksheet.Cells[row, col].Value = "¿Qué hace en un día común?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.DiaComun ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Qué es lo que más le gusta de la escuela?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.GustoEscuela ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Cómo se lleva con los padres / hermanos?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.AmbienteFamiliar ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Tiene algún pasatiempo?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.Responsabilidades ?? "N/A";
                        row++;

                        worksheet.Cells[row, col].Value = "¿Cómo es su estado de salúd en general?:";
                        worksheet.Cells[row, col].Style.Font.Bold = true;
                        worksheet.Cells[row, col + 1].Value = comp.SentidoUltimamente ?? "N/A";
                        row++;

                        // Estos campos NO están en la vista, se eliminan:
                        // Servicios, SituacionDif, AlguienHablar, AccionesMejorar, AyudaInstitucion
                    }
                    row += 2;

                    // VULNERABILIDAD
                    worksheet.Cells[row, col].Value = "VULNERABILIDAD";
                    worksheet.Cells[row, col, row, 2].Merge = true;
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col].Style.Font.Size = 14;
                    worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Orange);
                    row++;

                    worksheet.Cells[row, col].Value = "¿Es vulnerable?:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = respuestaVulnerableNombre;
                    worksheet.Cells[row, col + 1].Style.Font.Bold = true;
                    if (comp.IdVulnerable == 1)
                    {
                        worksheet.Cells[row, col + 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    }
                    row++;

                    worksheet.Cells[row, col].Value = "Tipo de vulnerabilidad:";
                    worksheet.Cells[row, col].Style.Font.Bold = true;
                    worksheet.Cells[row, col + 1].Value = tipoVulnerabilidad;
                    if (comp.IdVulnerable == 1 && comp.IdEleccionVunerabilidad == 1)
                    {
                        worksheet.Cells[row, col + 1].Style.Font.Color.SetColor(System.Drawing.Color.DarkRed);
                        worksheet.Cells[row, col + 1].Style.Font.Bold = true;
                    }
                    else if (comp.IdVulnerable == 1 && comp.IdEleccionVunerabilidad == 2)
                    {
                        worksheet.Cells[row, col + 1].Style.Font.Color.SetColor(System.Drawing.Color.DarkOrange);
                        worksheet.Cells[row, col + 1].Style.Font.Bold = true;
                    }
                    else if (comp.IdVulnerable == 1 && comp.IdEleccionVunerabilidad == 3)
                    {
                        worksheet.Cells[row, col + 1].Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue);
                        worksheet.Cells[row, col + 1].Style.Font.Bold = true;
                    }
                    row++;

                    // Ajustar ancho de columnas
                    worksheet.Column(1).Width = 50;
                    worksheet.Column(2).Width = 60;

                    // Aplicar bordes a todas las celdas con datos
                    var dataRange = worksheet.Cells[1, 1, row - 1, 2];
                    dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.WrapText = true; // Permitir ajuste de texto para evitar cortes

                    // Guardar el archivo
                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    string fileName = $"DetallesEntrevista_{comp.Matricula ?? "Alumno"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al exportar los datos: " + ex.Message;
                return RedirectToAction("Detalles", "Entrevistas", new { idPersona = idPersona, id = id, token = token });
            }
        }

        public ActionResult ExportarGrupoDetallesExcel(int idGrupo)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Validar que solo nivel 3 y 4 pueden exportar
            if (usuario.IdNivel != 3 && usuario.IdNivel != 4)
            {
                TempData["Error"] = "No tiene permisos para exportar información.";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == idGrupo);
            if (grupo == null)
            {
                TempData["Error"] = "Grupo no encontrado";
                return RedirectToAction("Grupo", "Entrevistas");
            }

            // Alumnos del grupo - Usar consulta SQL directa para evitar que EF resuelva Especialidad como navegación
            var query = @"
SELECT
    dp.IdPersona, dp.Matricula, dp.Nombre, dp.Sexo, dp.Edad,
    dp.Direccion, dp.Celular, dp.Telefono, dp.Email, dp.TelEmergencia,
    dp.Especialidad,
    g.Nombre AS GradoNombre,
    gr.Nombre AS GrupoNombre,
    c.Nombre AS CarreraNombre,
    t.Nombre AS TurnoNombre
FROM DatosPersonales dp
LEFT JOIN Gradoes g ON dp.IdGrado = g.IdGrado
LEFT JOIN Grupoes gr ON dp.IdGrupo = gr.IdGrupo
LEFT JOIN Carreras c ON dp.IdCarrera = c.IdCarrera
LEFT JOIN Turnoes t ON dp.IdTurno = t.IdTurno
WHERE
    dp.IdCarrera = @IdCarrera AND dp.IdGrado = @IdGrado AND dp.IdGrupo = @IdGrupo
    AND dp.IdTurno = @IdTurno AND dp.IdPeriodo = @IdPeriodo AND dp.Año = @Año
ORDER BY dp.Nombre, dp.Matricula";

            var alumnos = db.Database.SqlQuery<DatosPersonalesExportDTO>(
                query,
                new System.Data.SqlClient.SqlParameter("@IdCarrera", grupo.IdCarrera),
                new System.Data.SqlClient.SqlParameter("@IdGrado", grupo.IdGrado),
                new System.Data.SqlClient.SqlParameter("@IdGrupo", grupo.IdGrupo),
                new System.Data.SqlClient.SqlParameter("@IdTurno", grupo.IdTurno),
                new System.Data.SqlClient.SqlParameter("@IdPeriodo", grupo.IdPeriodo),
                new System.Data.SqlClient.SqlParameter("@Año", grupo.Año)
            ).ToList();

            // Preparar Excel
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Grupo");

                int row = 1;
                // Título y datos del grupo
                ws.Cells[row, 1].Value = "REPORTE DE ENTREVISTAS - GRUPO"; ws.Cells[row, 1, row, 12].Merge = true; ws.Cells[row, 1].Style.Font.Bold = true; ws.Cells[row, 1].Style.Font.Size = 16; row += 2;
                ws.Cells[row, 1].Value = "Carrera"; ws.Cells[row, 2].Value = grupo.Carrera?.Nombre; row++;
                ws.Cells[row, 1].Value = "Grado"; ws.Cells[row, 2].Value = grupo.Grado?.Nombre; row++;
                ws.Cells[row, 1].Value = "Grupo"; ws.Cells[row, 2].Value = grupo.Grupo?.Nombre; row++;
                ws.Cells[row, 1].Value = "Turno"; ws.Cells[row, 2].Value = grupo.Turno?.Nombre; row++;
                ws.Cells[row, 1].Value = "Periodo"; ws.Cells[row, 2].Value = grupo.Periodo?.Nombre; row++;
                ws.Cells[row, 1].Value = "Año"; ws.Cells[row, 2].Value = grupo.Año; row += 2;

                // Encabezados
                int h = 1; int r = row;
                string[] headers = new string[] {
                    // Datos Personales
                    "Matrícula","Nombre","Sexo","Edad","Grado","Grupo","Carrera","Especialidad","Turno",
                    "Teléfono celular","Teléfono de casa","Teléfono de emergencia","Correo electrónico",
                    "¿Está casado?","¿Tiene hijos?","Cantidad de hijos","¿Está embarazada?",
                    
                    // Aspectos Económicos
                    "¿Reside en esta ciudad?","Especifique ciudad","¿Con quién vive?","Especifique con quién","¿Trabaja?","Lugar de trabajo",
                    "Ocupación papá","Ocupación mamá","Cantidad de hermanos","Ingreso mensual",
                    "Personas en casa","Personas trabajan","Solicitó beca",
                    
                    // Aspectos Académicos
                    "Bachillerato","Bachillerato específico","Especialidad Bach.","Promedio general",
                    "Equipo de cómputo","Dispositivo","Internet",
                    "Rendimiento en clase","Experiencia profesores","Materias reprobadas","Materias difíciles",
                    
                    // Aspectos Personales
                    "Enfermedad","Especifica enfermedad","Fuma","Cantidad fuma","Bebidas","Cantidad bebidas",
                    "Violencia intrafamiliar","Vida sin sentido","Por qué (vida)",
                    "Apoyo familiar","Por qué (apoyo)","Problemas económicos","Por qué (económicos)",
                    "Día común","Gusta de escuela","Relación familiar","Pasatiempo","Estado de salud",
                    
                    // Vulnerabilidad
                    "Es vulnerable","Tipo vulnerabilidad"
                };
                foreach (var head in headers) { ws.Cells[r, h++].Value = head; }
                ws.Cells[r, 1, r, headers.Length].Style.Font.Bold = true; r++;

                // Filas por alumno
                foreach (var dp in alumnos)
                {
                    // Entrevista relevante: misma etiqueta de grado si existe; si no, última por fecha
                    var gradoNombre = dp.GradoNombre;
                    var ent = db.EntrevistaInicials
                        .AsNoTracking()
                        .Where(e => e.IdPersona == dp.IdPersona && ((e.Grados != null && e.Grados.Nombre == gradoNombre) || gradoNombre == null))
                        .OrderByDescending(e => e.Fecha)
                        .FirstOrDefault();

                    // Si no hay entrevista, sigue pero con celdas N/A
                    int c = 1;
                    // Basicos
                    ws.Cells[r, c++].Value = dp.Matricula; ws.Cells[r, c++].Value = dp.Nombre; ws.Cells[r, c++].Value = dp.Sexo; ws.Cells[r, c++].Value = dp.Edad;
                    ws.Cells[r, c++].Value = ent?.Grados?.Nombre ?? dp.GradoNombre; ws.Cells[r, c++].Value = ent?.Grupos?.Nombre ?? dp.GrupoNombre; ws.Cells[r, c++].Value = ent?.Carreras?.Nombre ?? dp.CarreraNombre;
                    ws.Cells[r, c++].Value = ent?.Area ?? dp.Especialidad ?? "N/A"; ws.Cells[r, c++].Value = ent?.Turnos?.Nombre ?? dp.TurnoNombre;
                    ws.Cells[r, c++].Value = ent?.Celular ?? dp.Celular; ws.Cells[r, c++].Value = ent?.Telefono ?? dp.Telefono; ws.Cells[r, c++].Value = ent?.TelEmergencia ?? dp.TelEmergencia;
                    ws.Cells[r, c++].Value = ent?.Email ?? dp.Email;

                    ws.Cells[r, c++].Value = ent?.Respuesta4?.Nombre; // Casado
                    ws.Cells[r, c++].Value = ent?.Respuesta5?.Nombre; // Hijos
                    ws.Cells[r, c++].Value = (ent?.IdHijo == 1 ? ent?.CantidadHijo : "N/A"); // Cantidad Hijos


                    int entIdEmb = ent != null ? ent.IdEmbarazo : 0;
                    var embarazo = db.Respuesta11.Where(x => x.IdEmbarazo == entIdEmb).Select(x => x.Nombre).FirstOrDefault();
                    if (dp.Sexo != null && (dp.Sexo.Equals("Masculino", StringComparison.OrdinalIgnoreCase) || dp.Sexo.Equals("Hombre", StringComparison.OrdinalIgnoreCase)))
                    {
                        embarazo = "N/A";
                    }
                    ws.Cells[r, c++].Value = embarazo; // Embarazo

                    // Económicos
                    ws.Cells[r, c++].Value = ent?.Respuesta1?.Nombre; // Reside
                    ws.Cells[r, c++].Value = (ent?.IdCiudad == 2 ? ent?.Ciudad : "N/A"); // Ciudad Especifica
                    ws.Cells[r, c++].Value = ent?.Respuesta16?.Nombre; // Con quien vive
                    ws.Cells[r, c++].Value = ent?.Familiar; // Especifica con quien vive
                    ws.Cells[r, c++].Value = ent?.Respuesta2?.Nombre; // Trabaja
                    ws.Cells[r, c++].Value = (ent?.IdTrabajo == 1 ? ent?.Trabaja : "N/A"); // Lugar trabajo

                    ws.Cells[r, c++].Value = ent?.OcupacionPapa; ws.Cells[r, c++].Value = ent?.OcupacionMama; ws.Cells[r, c++].Value = ent?.CantidadHermano;
                    ws.Cells[r, c++].Value = ent?.Respuesta17?.Nombre; // Ingreso Mensual (Antes IngresoM, pero en individual es Respuesta17) - Validar si es IngresoM o Respuesta17. En Individual es Respuesta17?.Nombre.
                                                                       // Nota: En base de datos original usaba IngresoM, pero en individual usa Respuesta17. Vamos a usar Respuesta17 para coincidir.

                    // Condicionales Económicos (Personas Casa, Trabajan, Beca)
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.CantidadPersonas : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.CantidadTrabajan : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.Respuesta18?.Nombre : null; // Solicitó Beca (Respuesta18 en individual)

                    // Académicos
                    ws.Cells[r, c++].Value = ent?.Respuesta12?.Nombre ?? "Sin especificar"; // Bachillerato (Respuesta12)
                    ws.Cells[r, c++].Value = ent?.Bachillerato; // Bachillerato Especifico
                    ws.Cells[r, c++].Value = ent?.Especialidad; // Especialidad Bach
                    ws.Cells[r, c++].Value = ent?.Promedio; // Promedio

                    ws.Cells[r, c++].Value = ent?.Respuesta13?.Nombre; // Equipo Computo
                    ws.Cells[r, c++].Value = ent?.Respuesta14?.Nombre; // Dispositivo
                    ws.Cells[r, c++].Value = ent?.Respuesta15?.Nombre; // Internet

                    // Condicionales Académicos
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.RendimientoClase : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.ExperienciaProfe : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.MateriasRepro : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.MateriasDif : null;

                    // Personales
                    ws.Cells[r, c++].Value = ent?.Respuesta6?.Nombre; // Enfermedad
                    ws.Cells[r, c++].Value = (ent?.IdEnfermedad == 1 ? ent?.Especifica : "N/A"); // Especifica Enfermedad
                    ws.Cells[r, c++].Value = ent?.Respuesta7?.Nombre; // Fuma
                    ws.Cells[r, c++].Value = (ent?.IdFuma == 1 ? ent?.CantidadFuma : "N/A"); // Cantidad Fuma
                    ws.Cells[r, c++].Value = ent?.Respuesta8?.Nombre; // Bebidas
                    ws.Cells[r, c++].Value = (ent?.IdBebida == 1 ? ent?.CantidadBedida : "N/A"); // Cantidad Bebidas

                    ws.Cells[r, c++].Value = ent?.Observacio?.Nombre; // Violencia
                    ws.Cells[r, c++].Value = ent?.Respuesta9?.Nombre; // Vida sin sentido
                    ws.Cells[r, c++].Value = (ent?.IdVidaSinSentido == 1 ? ent?.Porque : "N/A"); // Por qué vida

                    ws.Cells[r, c++].Value = ent?.ApoyoFamiliaEnProblemas; // Apoyo Familiar
                    ws.Cells[r, c++].Value = ent?.ApoyoFamiliaEnProblemasPorque; // Por qué apoyo
                    ws.Cells[r, c++].Value = ent?.ProblemasEconomicosFamilia; // Problemas Económicos
                    ws.Cells[r, c++].Value = ent?.ProblemasEconomicosFamiliaPorque; // Por qué económicos

                    // Condicionales Personales
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.DiaComun : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.GustoEscuela : null;
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.AmbienteFamiliar : null; // Relación familiar (AmbienteFamiliar)
                    // Nota: 'Pasatiempo' en individual era 'Responsabilidades' renombrado. Validemos si Responsabilidades es el campo correcto.
                    ws.Cells[r, c++].Value = ent?.Responsabilidades; // Pasatiempo (Mapped from Responsabilidades in individual)
                    ws.Cells[r, c++].Value = (ent != null && ent.Grados?.Nombre != "1") ? ent?.SentidoUltimamente : null; // Estado de salud



                    // Vulnerabilidad
                    int entIdVul = ent != null ? ent.IdVulnerable : 0;
                    int entIdElec = ent != null ? ent.IdEleccionVunerabilidad : 0;
                    var esVul = db.Respuesta10
                        .Where(x => x.IdVulnerable == entIdVul)
                        .Select(x => x.Nombre)
                        .FirstOrDefault();
                    var tipoVul = db.Vulnerable
                        .Where(x => x.IdEleccionVunerabilidad == entIdElec)
                        .Select(x => x.Nombre)
                        .FirstOrDefault();
                    ws.Cells[r, c++].Value = esVul; ws.Cells[r, c++].Value = tipoVul;

                    r++;
                }

                // AutoFit
                ws.Cells[1, 1, r, headers.Length].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;
                string nombreArchivo = $"Grupo_{grupo.Carrera?.Nomenclatura}{grupo.Grado?.Nombre}{grupo.Grupo?.Nombre}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
            }
        }

    }

    // DTO para exportación de datos personales usando consulta SQL directa
    public class DatosPersonalesExportDTO
    {
        public int IdPersona { get; set; }
        public string Matricula { get; set; }
        public string Nombre { get; set; }
        public string Sexo { get; set; }
        public int? Edad { get; set; }
        public string Direccion { get; set; }
        public string Celular { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string TelEmergencia { get; set; }
        public string Especialidad { get; set; }
        public string GradoNombre { get; set; }
        public string GrupoNombre { get; set; }
        public string CarreraNombre { get; set; }
        public string TurnoNombre { get; set; }
    }
}