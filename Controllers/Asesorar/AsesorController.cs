using Microsoft.Reporting.WebForms;
using OfficeOpenXml.FormulaParsing.LexicalAnalysis;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using PlataformaWeb;
using PlataformaWeb.Helpers;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.Asesorar
{
    public class AsesorController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: Asesor
        public ActionResult Index()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            var tiempo = DateTime.Now;
            var pa = 0;
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
            List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == pa && x.Año == DateTime.Now.Year).ToList();
            foreach (var item in tutorias)
            {
                var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                item.Nomenclatura = x.ToString();
            }

            ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
            return View();
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(TutoriaGrupal tutoriaGrupal)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (tutoriaGrupal.IdTutoriaGrupal == 0)
            {
                ViewBag.Mensaje = "Seleccione un grupo por favor.";
            }
            else
            {
                return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal });
            }

            var tiempo = DateTime.Now;
            var pa = 0;
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
            List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == pa && x.Año == DateTime.Now.Year).ToList();
            foreach (var item in tutorias)
            {
                var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                item.Nomenclatura = x.ToString();
            }

            ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
            return View();
        }

        public ActionResult Grupo(int? id)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (id == null || id == 0)
            {
                return RedirectToAction("Index");
            }

            var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id);
            List<DatosPersonales> datosPersonales = db.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
            x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.Año == grupo.Año && x.IdPeriodo == grupo.IdPeriodo).OrderBy(x => x.Nombre).ToList();
            ViewBag.Alumnos = datosPersonales;
            return View();
        }

        public ActionResult Error()
        {
            ViewBag.Mensaje1 = "Ocurrió un error al procesar los datos del alumno, pida al alumno completar la entrevista y vuelva a intentar";
            return View();
        }

        [LecturaPermitida]
        [HttpPost]
        public JsonResult ObtenerNavegacionEntrevistas(int? idPersona, string token)
        {
            try
            {
                // 1. OBTENER USUARIO Y VALIDAR SESIÓN
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Sesión expirada." }, JsonRequestBehavior.AllowGet);
                }

                // 2. VALIDAR PARÁMETROS
                if (!idPersona.HasValue || idPersona.Value <= 0)
                {
                    return Json(new { success = false, message = "ID de persona inválido." }, JsonRequestBehavior.AllowGet);
                }

                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "Token no proporcionado." }, JsonRequestBehavior.AllowGet);
                }

                // 3. VALIDAR TOKEN
                if (!PlataformaWeb.Helpers.AccesoSeguroHelper.ValidarToken(token, idPersona.Value, usuario.IdUsuario))
                {
                    return Json(new { success = false, message = "Token inválido." }, JsonRequestBehavior.AllowGet);
                }

                // 4. OBTENER DETALLES DEL ALUMNO ACTUAL
                var datosActual = db.DatosPersonales
                    .AsNoTracking()
                    .FirstOrDefault(d => d.IdPersona == idPersona.Value);

                if (datosActual == null)
                {
                    return Json(new { success = false, message = "Alumno no encontrado." }, JsonRequestBehavior.AllowGet);
                }

                // 5. OBTENER LA LISTA ORDENADA DE ALUMNOS DEL GRUPO
                var alumnosGrupo = db.DatosPersonales
                    .AsNoTracking()
                    .Where(d => d.IdCarrera == datosActual.IdCarrera &&
                                d.IdGrado == datosActual.IdGrado &&
                                d.IdGrupo == datosActual.IdGrupo &&
                                d.IdPeriodo == datosActual.IdPeriodo &&
                                d.IdTurno == datosActual.IdTurno &&
                                d.Año == datosActual.Año)
                    .OrderBy(d => d.Paterno)
                    .ThenBy(d => d.Nombre)
                    .Select(d => new { d.IdPersona })
                    .ToList();

                // 6. ENCONTRAR POSICIÓN ACTUAL
                int indexActual = alumnosGrupo.FindIndex(d => d.IdPersona == idPersona.Value);

                if (indexActual == -1)
                {
                    return Json(new { success = false, message = "Alumno no encontrado en el grupo." }, JsonRequestBehavior.AllowGet);
                }

                int totalAlumnos = alumnosGrupo.Count;

                // 7. CALCULAR ID Y TOKEN ANTERIOR Y SIGUIENTE
                int? idAnterior = null;
                int? idSiguiente = null;
                string tokenAnterior = null;
                string tokenSiguiente = null;

                if (indexActual > 0)
                {
                    idAnterior = alumnosGrupo[indexActual - 1].IdPersona;
                    tokenAnterior = PlataformaWeb.Helpers.AccesoSeguroHelper.GenerarToken(idAnterior.Value, usuario.IdUsuario);
                }

                if (indexActual < totalAlumnos - 1)
                {
                    idSiguiente = alumnosGrupo[indexActual + 1].IdPersona;
                    tokenSiguiente = PlataformaWeb.Helpers.AccesoSeguroHelper.GenerarToken(idSiguiente.Value, usuario.IdUsuario);
                }

                // 8. RETORNAR RESULTADO
                return Json(new
                {
                    success = true,
                    posicion = indexActual + 1,
                    total = totalAlumnos,
                    idAnterior = idAnterior,
                    tokenAnterior = tokenAnterior,
                    idSiguiente = idSiguiente,
                    tokenSiguiente = tokenSiguiente
                }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Regenera un token de acceso para la navegación cuando el token original ha expirado
        /// (por ejemplo, al usar las flechas de historial del navegador).
        /// Valida permisos verificando la asignación de grupo del tutor en lugar de requerir un token previo.
        /// </summary>
        [LecturaPermitida]
        [HttpPost]
        public JsonResult RegenerarTokenNavegacion(int? idPersona)
        {
            try
            {
                // 1. OBTENER USUARIO Y VALIDAR SESIÓN
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Sesión expirada." });
                }

                // 2. VALIDAR PARÁMETROS
                if (!idPersona.HasValue || idPersona.Value <= 0)
                {
                    return Json(new { success = false, message = "ID de persona inválido." });
                }

                // 3. OBTENER DATOS DEL ALUMNO
                var datosAlumno = db.DatosPersonales
                    .AsNoTracking()
                    .FirstOrDefault(d => d.IdPersona == idPersona.Value);

                if (datosAlumno == null)
                {
                    return Json(new { success = false, message = "Alumno no encontrado." });
                }

                // 4. VALIDAR PERMISOS: Verificar que el tutor tiene asignado este grupo
                // (Solo para nivel 2, niveles 3 y 4 tienen acceso global)
                if (usuario.IdNivel == 2)
                {
                    var tiempo = DateTime.Now;
                    int periodoActual;
                    if (tiempo.Month >= 1 && tiempo.Month <= 4)
                        periodoActual = 1;
                    else if (tiempo.Month >= 5 && tiempo.Month <= 8)
                        periodoActual = 2;
                    else
                        periodoActual = 3;

                    var tutoriaAsignada = db.TutoriaGrupals.FirstOrDefault(t =>
                        t.IdCarrera == datosAlumno.IdCarrera &&
                        t.IdGrado == datosAlumno.IdGrado &&
                        t.IdGrupo == datosAlumno.IdGrupo &&
                        t.IdTurno == datosAlumno.IdTurno &&
                        t.IdUsuario == usuario.IdUsuario &&
                        t.IdPeriodo == periodoActual &&
                        t.Año == DateTime.Now.Year);

                    if (tutoriaAsignada == null)
                    {
                        return Json(new { success = false, message = "No tienes permisos para acceder a este alumno." });
                    }
                }
                else if (usuario.IdNivel != 3 && usuario.IdNivel != 4)
                {
                    return Json(new { success = false, message = "Tu nivel de usuario no tiene permisos para esta función." });
                }

                // 5. GENERAR NUEVO TOKEN
                string nuevoToken = PlataformaWeb.Helpers.AccesoSeguroHelper.GenerarToken(idPersona.Value, usuario.IdUsuario);

                // 6. RETORNAR RESULTADO
                return Json(new
                {
                    success = true,
                    token = nuevoToken,
                    idPersona = idPersona.Value
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        [HttpGet]
        public JsonResult GetEntrevistas(int? personaId)
        {
            var entrevistas = db.EntrevistaInicials.Where(x => x.IdPersona == personaId).Select(x => new
            {
                x.IdEntrevistaInicial,
                x.IdGrado,
                x.Fecha
            }).OrderByDescending(x => x.IdGrado).ToList();

            return Json(entrevistas, JsonRequestBehavior.AllowGet);
        }

        // GET: Detalles
        public ActionResult Detalles(int? id, string token = null)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                ViewBag.ErrorTitle = "Sesión requerida";
                ViewBag.ErrorMessage = "Debes iniciar sesión para acceder a esta información.";
                SetSelectLists(null);
                return View("ErrorPermiso");
            }
            if (usuario.IdNivel != 2 && usuario.IdNivel != 3 && usuario.IdNivel != 4)
            {
                ViewBag.ErrorTitle = "Permiso denegado";
                ViewBag.ErrorMessage = "Tu nivel de usuario no tiene permisos para acceder a esta función.";
                SetSelectLists(null);
                return View("ErrorPermiso");
            }
            if (id == null)
            {
                ViewBag.ErrorTitle = "Solicitud inválida";
                ViewBag.ErrorMessage = "No se proporcionó un identificador de alumno válido.";
                SetSelectLists(null);
                return View("ErrorPermiso");
            }

            if (string.IsNullOrEmpty(token) || !AccesoSeguroHelper.ValidarToken(token, id.Value, usuario.IdUsuario))
            {
                ViewBag.ErrorTitle = "Permiso denegado";
                ViewBag.ErrorMessage = "Acceso no autorizado. Por favor accede desde la lista.";
                SetSelectLists(null);
                return View("ErrorPermiso");
            }

            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            var aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
            var ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
            var ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (dp == null || aa == null || ae == null || ap == null)
            {
                ViewBag.ErrorTitle = "Entrevista no disponible";
                ViewBag.ErrorMessage = "El alumno no ha generado los registros básicos de la entrevista.";
                SetSelectLists(null);
                return View("ErrorPermiso");
            }

            if (usuario.IdNivel == 2)
            {
                var tiempo = DateTime.Now;
                var turn = 0;
                if (tiempo.Month >= 1 && tiempo.Month <= 4)
                    turn = 1;
                else if (tiempo.Month >= 5 && tiempo.Month <= 8)
                    turn = 2;
                else
                    turn = 3;

                var tutgrup = db.TutoriaGrupals.FirstOrDefault(x =>
                    x.IdCarrera == dp.IdCarrera &&
                    x.IdGrado == dp.IdGrado &&
                    x.IdGrupo == dp.IdGrupo &&
                    x.IdTurno == dp.IdTurno &&
                    x.IdUsuario == usuario.IdUsuario &&
                    x.IdPeriodo == turn &&
                    x.Año == DateTime.Now.Year);

                if (tutgrup == null)
                {
                    ViewBag.ErrorTitle = "Permiso denegado";
                    ViewBag.ErrorMessage = "No tienes permisos para editar este alumno.";
                    SetSelectLists(null);
                    return View("ErrorPermiso");
                }

                ViewBag.id = tutgrup.IdTutoriaGrupal;
            }
            else
            {
                ViewBag.id = 0;
            }

            EntrevistaInicial comp = db.EntrevistaInicials
                .AsNoTracking()
                .Where(x => x.IdPersona == id)
                .OrderByDescending(e => e.IdEntrevistaInicial)
                .FirstOrDefault();

            if (comp == null)
            {
                comp = new EntrevistaInicial();
                comp.Area = dp.Especialidad;
                comp.IdPersona = dp.IdPersona;
                comp.Nombre = dp.Nombre;
                comp.Matricula = dp.Matricula;
            }


            ViewBag.datos = dp;
            ViewBag.academicos = aa;
            ViewBag.economicos = ae;
            ViewBag.personales = ap;

            SetSelectLists(comp);

            return View(comp);
        }

        // POST: Detalles
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Detalles(EntrevistaInicial ei, int id, string token)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                ViewBag.Error = "Debes iniciar sesión.";
                return View("ErrorPermiso");
            }
            if (usuario.IdNivel != 2 && usuario.IdNivel != 3 && usuario.IdNivel != 4)
            {
                ViewBag.Error = "No tienes permisos para esta acción.";
                return View("ErrorPermiso");
            }
            // TOKEN VALIDATION
            if (string.IsNullOrEmpty(token) || !AccesoSeguroHelper.ValidarToken(token, id, usuario.IdUsuario))
            {
                ViewBag.Error = "Acceso no autorizado. Por favor accede desde la lista.";
                return View("ErrorPermiso");
            }

            try
            {
                var entrevistaExistente = db.EntrevistaInicials
                    .Where(x => x.IdPersona == id)
                    .OrderByDescending(e => e.IdEntrevistaInicial)
                    .FirstOrDefault();

                if (entrevistaExistente != null)
                {
                    var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                    var aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
                    var ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
                    var ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);

                    entrevistaExistente.Fecha = DateTime.Now;
                    entrevistaExistente.Area1 = ei.Area1;
                    entrevistaExistente.NivelDesempeño1 = ei.NivelDesempeño1;
                    entrevistaExistente.Area2 = ei.Area2;
                    entrevistaExistente.NivelDesempeño2 = ei.NivelDesempeño2;
                    entrevistaExistente.Area3 = ei.Area3;
                    entrevistaExistente.NivelDesempeño3 = ei.NivelDesempeño3;
                    entrevistaExistente.Area4 = ei.Area4;
                    entrevistaExistente.NivelDesempeño4 = ei.NivelDesempeño4;
                    entrevistaExistente.EvaluacionPsicometrica = ei.EvaluacionPsicometrica;
                    entrevistaExistente.IdVulnerable = ei.IdVulnerable;
                    entrevistaExistente.IdEleccionVunerabilidad = ei.IdEleccionVunerabilidad;

                    entrevistaExistente.IdNivelAutoestima = ei.IdNivelAutoestima.HasValue && ei.IdNivelAutoestima.Value > 0 ? ei.IdNivelAutoestima : null;
                    entrevistaExistente.IdNivelTamizaje = ei.IdNivelTamizaje.HasValue && ei.IdNivelTamizaje.Value > 0 ? ei.IdNivelTamizaje : null;
                    entrevistaExistente.IdNivelPensamientoAbstracto = ei.IdNivelPensamientoAbstracto.HasValue && ei.IdNivelPensamientoAbstracto.Value > 0 ? ei.IdNivelPensamientoAbstracto : null;
                    entrevistaExistente.IdAspectoPersonal = ei.IdAspectoPersonal.HasValue && ei.IdAspectoPersonal.Value > 0 ? ei.IdAspectoPersonal : null;
                    entrevistaExistente.IdAspectoAcademico = ei.IdAspectoAcademico.HasValue && ei.IdAspectoAcademico.Value > 0 ? ei.IdAspectoAcademico : null;
                    entrevistaExistente.IdAspectoEconomico = ei.IdAspectoEconomico.HasValue && ei.IdAspectoEconomico.Value > 0 ? ei.IdAspectoEconomico : null;

                    if (dp != null)
                    {
                        entrevistaExistente.Matricula = dp.Matricula;
                        entrevistaExistente.Nombre = dp.Nombre;
                        entrevistaExistente.Edad = dp.Edad;
                        entrevistaExistente.IdTurno = dp.IdTurno;
                        entrevistaExistente.IdCarrera = dp.IdCarrera;
                        entrevistaExistente.IdGrupo = dp.IdGrupo;
                        entrevistaExistente.IdGrado = dp.IdGrado;
                        entrevistaExistente.Direccion = dp.Direccion;
                        entrevistaExistente.Celular = dp.Celular;
                        entrevistaExistente.Telefono = dp.Telefono;
                        entrevistaExistente.TelEmergencia = dp.TelEmergencia;
                        entrevistaExistente.Email = dp.Email;
                        entrevistaExistente.Sexo = dp.Sexo;
                        entrevistaExistente.CarreraNom = dp.CarreraNom;
                        entrevistaExistente.Area = dp.Especialidad;
                    }

                    if (aa != null)
                    {
                        entrevistaExistente.IdListaBachillerato = aa.IdListaBachillerato;
                        entrevistaExistente.Bachillerato = aa.Bachillerato;
                        entrevistaExistente.Especialidad = aa.Especialidad;
                        entrevistaExistente.Promedio = aa.Promedio;
                        entrevistaExistente.MateriasDif = aa.MateriasDif;
                        entrevistaExistente.MateriasRepro = aa.MateriasRepro;
                        entrevistaExistente.TiempoOrg = aa.TiempoOrg;
                        entrevistaExistente.ApoyoAca = aa.ApoyoAca;
                        entrevistaExistente.RendimientoClase = aa.RendimientoClase;
                        entrevistaExistente.ExperienciaProfe = aa.ExperienciaProfe;
                        entrevistaExistente.IdEquipoComp = aa.IdEquipoComp;
                        entrevistaExistente.IdTipoDispositivo = aa.IdTipoDispositivo;
                        entrevistaExistente.IdAccesoInternet = aa.IdAccesoInternet;
                    }

                    if (ae != null)
                    {
                        entrevistaExistente.IdCiudad = ae.IdCiudad;
                        entrevistaExistente.Ciudad = ae.Ciudad;
                        entrevistaExistente.Familiar = ae.Familiar;
                        entrevistaExistente.IdTrabajo = ae.IdTrabajo;
                        entrevistaExistente.Trabaja = ae.Trabaja;
                        entrevistaExistente.Dependiente = ae.Dependiente;
                        entrevistaExistente.OcupacionPapa = ae.OcupacionPapa;
                        entrevistaExistente.OcupacionMama = ae.OcupacionMama;
                        entrevistaExistente.CantidadHermano = ae.CantidadHermano;
                        entrevistaExistente.IngresoM = ae.IngresoM;
                        entrevistaExistente.SolicitadoBeca = ae.SolicitadoBeca;
                        entrevistaExistente.AfectacionEco = ae.AfectacionEco;
                        entrevistaExistente.CantidadPersonas = ae.CantidadPersonas;
                        entrevistaExistente.CantidadTrabajan = ae.CantidadTrabajan;
                        entrevistaExistente.IdTipoFamiliar = ae.IdTipoFamiliar;
                        entrevistaExistente.IdIngresoMes = ae.IdIngresoMes;
                        entrevistaExistente.IdSolicitarBeca = ae.IdSolicitarBeca;

                    }

                    if (ap != null)
                    {
                        entrevistaExistente.IdCasado = ap.IdCasado;
                        entrevistaExistente.IdHijo = ap.IdHijo;
                        entrevistaExistente.CantidadHijo = ap.CantidadHijo;
                        entrevistaExistente.IdEnfermedad = ap.IdEnfermedad;
                        entrevistaExistente.Especifica = ap.Especifica;
                        entrevistaExistente.IdFuma = ap.IdFuma;
                        entrevistaExistente.CantidadFuma = ap.CantidadFuma;
                        entrevistaExistente.IdBebida = ap.IdBebida;
                        entrevistaExistente.CantidadBedida = ap.CantidadBedida;
                        entrevistaExistente.IdVidaSinSentido = ap.IdVidaSinSentido;
                        entrevistaExistente.Porque = ap.Porque;
                        entrevistaExistente.IdObservacionFamilia = ap.IdObservacionFamilia;
                        entrevistaExistente.ApoyoFamiliaEnProblemas = ap.ApoyoFamiliaEnProblemas;
                        entrevistaExistente.ApoyoFamiliaEnProblemasPorque = ap.ApoyoFamiliaEnProblemasPorque;
                        entrevistaExistente.ProblemasEconomicosFamilia = ap.ProblemasEconomicosFamilia;
                        entrevistaExistente.ProblemasEconomicosFamiliaPorque = ap.ProblemasEconomicosFamiliaPorque;
                        entrevistaExistente.AmbienteFamiliar = ap.AmbienteFamiliar;
                        entrevistaExistente.Responsabilidades = ap.Responsabilidades;
                        entrevistaExistente.SentidoUltimamente = ap.SentidoUltimamente;
                        entrevistaExistente.Servicios = ap.Servicios;
                        entrevistaExistente.AlguienHablar = ap.AlguienHablar;
                        entrevistaExistente.SituacionDif = ap.SituacionDif;
                        entrevistaExistente.SentidoMal = ap.SentidoMal;
                        entrevistaExistente.CompartirAlgo = ap.CompartirAlgo;
                        entrevistaExistente.AccionesMejorar = ap.AccionesMejorar;
                        entrevistaExistente.AyudaInstitucion = ap.AyudaInstitucion;
                        entrevistaExistente.IdEmbarazo = ap.IdEmbarazo;
                        entrevistaExistente.DiaComun = ap.DiaComun;
                        entrevistaExistente.GustoEscuela = ap.GustoEscuela;
                    }

                    db.Entry(entrevistaExistente).State = EntityState.Modified;
                    db.SaveChanges();

                    db.Entry(entrevistaExistente).State = EntityState.Detached;

                    foreach (var entry in db.ChangeTracker.Entries().ToList())
                    {
                        entry.State = EntityState.Detached;
                    }

                    var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                    if (alumno != null)
                    {
                        alumno.Estado = true;
                        db.Entry(alumno).State = EntityState.Modified;
                        db.SaveChanges();
                    }

                    // MODIFICADO: Se pasan los datos de vulnerabilidad
                    CrearPrimerSeguimientoSiEsNecesario(id, ei.IdVulnerable, ei.IdEleccionVunerabilidad);

                    TempData["Message"] = "Información actualizada correctamente.";
                }
                else
                {
                    var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                    var aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
                    var ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
                    var ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);

                    ei.IdPersona = dp.IdPersona;
                    ei.Fecha = DateTime.Now;
                    ei.Matricula = dp.Matricula;
                    ei.Nombre = dp.Nombre;
                    ei.Edad = dp.Edad;
                    ei.IdTurno = dp.IdTurno;
                    ei.IdCarrera = dp.IdCarrera;
                    ei.IdGrupo = dp.IdGrupo;
                    ei.IdGrado = dp.IdGrado;
                    ei.Direccion = dp.Direccion;
                    ei.Celular = dp.Celular;
                    ei.Telefono = dp.Telefono;
                    ei.TelEmergencia = dp.TelEmergencia;
                    ei.Email = dp.Email;
                    ei.Foto = "";
                    ei.Sexo = dp.Sexo;
                    ei.CarreraNom = dp.CarreraNom;
                    ei.Area = dp.Especialidad;

                    ei.IdListaBachillerato = aa.IdListaBachillerato;
                    ei.Bachillerato = aa.Bachillerato;
                    ei.Especialidad = aa.Especialidad;
                    ei.Promedio = aa.Promedio;
                    ei.MateriasDif = aa.MateriasDif;
                    ei.MateriasRepro = aa.MateriasRepro;
                    ei.TiempoOrg = aa.TiempoOrg;
                    ei.ApoyoAca = aa.ApoyoAca;
                    ei.RendimientoClase = aa.RendimientoClase;
                    ei.ExperienciaProfe = aa.ExperienciaProfe;
                    ei.IdEquipoComp = aa.IdEquipoComp;
                    ei.IdTipoDispositivo = aa.IdTipoDispositivo;
                    ei.IdAccesoInternet = aa.IdAccesoInternet;

                    ei.IdCiudad = ae.IdCiudad;
                    ei.Ciudad = ae.Ciudad;
                    ei.Familiar = ae.Familiar;
                    ei.IdTrabajo = ae.IdTrabajo;
                    ei.Trabaja = ae.Trabaja;
                    ei.Dependiente = ae.Dependiente;
                    ei.OcupacionPapa = ae.OcupacionPapa;
                    ei.OcupacionMama = ae.OcupacionMama;
                    ei.CantidadHermano = ae.CantidadHermano;
                    ei.IngresoM = ae.IngresoM;
                    ei.SolicitadoBeca = ae.SolicitadoBeca;
                    ei.AfectacionEco = ae.AfectacionEco;
                    ei.CantidadPersonas = ae.CantidadPersonas;
                    ei.CantidadTrabajan = ae.CantidadTrabajan;
                    ei.IdTipoFamiliar = ae.IdTipoFamiliar;
                    ei.IdIngresoMes = ae.IdIngresoMes;
                    ei.IdSolicitarBeca = ae.IdSolicitarBeca;

                    ei.IdCasado = ap.IdCasado;
                    ei.IdHijo = ap.IdHijo;
                    ei.CantidadHijo = ap.CantidadHijo;
                    ei.IdEnfermedad = ap.IdEnfermedad;
                    ei.Especifica = ap.Especifica;
                    ei.IdFuma = ap.IdFuma;
                    ei.CantidadFuma = ap.CantidadFuma;
                    ei.IdBebida = ap.IdBebida;
                    ei.CantidadBedida = ap.CantidadBedida;
                    ei.IdVidaSinSentido = ap.IdVidaSinSentido;
                    ei.Porque = ap.Porque;
                    ei.IdObservacionFamilia = ap.IdObservacionFamilia;
                    ei.ApoyoFamiliaEnProblemas = ap.ApoyoFamiliaEnProblemas;
                    ei.ApoyoFamiliaEnProblemasPorque = ap.ApoyoFamiliaEnProblemasPorque;
                    ei.ProblemasEconomicosFamilia = ap.ProblemasEconomicosFamilia;
                    ei.ProblemasEconomicosFamiliaPorque = ap.ProblemasEconomicosFamiliaPorque;
                    ei.AmbienteFamiliar = ap.AmbienteFamiliar;
                    ei.Responsabilidades = ap.Responsabilidades;
                    ei.SentidoUltimamente = ap.SentidoUltimamente;
                    ei.Servicios = ap.Servicios;
                    ei.AlguienHablar = ap.AlguienHablar;
                    ei.SituacionDif = ap.SituacionDif;
                    ei.SentidoMal = ap.SentidoMal;
                    ei.CompartirAlgo = ap.CompartirAlgo;
                    ei.AccionesMejorar = ap.AccionesMejorar;
                    ei.AyudaInstitucion = ap.AyudaInstitucion;
                    ei.IdEmbarazo = ap.IdEmbarazo;
                    ei.DiaComun = ap.DiaComun;
                    ei.GustoEscuela = ap.GustoEscuela;

                    ei.IdNivelAutoestima = ei.IdNivelAutoestima.HasValue && ei.IdNivelAutoestima.Value > 0 ? ei.IdNivelAutoestima : null;
                    ei.IdNivelTamizaje = ei.IdNivelTamizaje.HasValue && ei.IdNivelTamizaje.Value > 0 ? ei.IdNivelTamizaje : null;
                    ei.IdNivelPensamientoAbstracto = ei.IdNivelPensamientoAbstracto.HasValue && ei.IdNivelPensamientoAbstracto.Value > 0 ? ei.IdNivelPensamientoAbstracto : null;
                    ei.IdAspectoEconomico = ei.IdAspectoEconomico.HasValue && ei.IdAspectoEconomico.Value > 0 ? ei.IdAspectoEconomico : null;
                    ei.IdAspectoAcademico = ei.IdAspectoAcademico.HasValue && ei.IdAspectoAcademico.Value > 0 ? ei.IdAspectoAcademico : null;
                    ei.IdAspectoPersonal = ei.IdAspectoPersonal.HasValue && ei.IdAspectoPersonal.Value > 0 ? ei.IdAspectoPersonal : null;

                    db.EntrevistaInicials.Add(ei);
                    db.SaveChanges();

                    var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                    if (alumno != null)
                    {
                        alumno.Estado = true;
                        db.Entry(alumno).State = EntityState.Modified;
                        db.SaveChanges();
                    }

                    // MODIFICADO: Se pasan los datos de vulnerabilidad
                    CrearPrimerSeguimientoSiEsNecesario(id, ei.IdVulnerable, ei.IdEleccionVunerabilidad);

                    TempData["Message"] = "Nueva entrevista creada correctamente.";
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Error al guardar: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = id, token = token });
        }

        [HttpPost]
        public ActionResult Desbloquear(int id)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null || (usuario.IdNivel != 2 && usuario.IdNivel != 3 && usuario.IdNivel != 4))
            {
                return Json(new { success = false, message = "No tienes permisos." });
            }

            var alumno = db.DatosPersonales.Find(id);
            if (alumno != null)
            {
                alumno.Estado = false;
                db.Configuration.ValidateOnSaveEnabled = false; // Fix 500 Error: Disable validation for this specific action
                db.Entry(alumno).State = EntityState.Modified;
                db.SaveChanges();
                db.Configuration.ValidateOnSaveEnabled = true; // Re-enable validation
                return Json(new { success = true, message = "Entrevista desbloqueada correctamente." });
            }
            return Json(new { success = false, message = "Alumno no encontrado." });
        }

        public ActionResult ReporteEntrevista(int? id)
        {
            List<EntrevistaInicial> alumno = db.EntrevistaInicials.Where(x => x.IdPersona == id).ToList();
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
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
                EntrevistaInicial ei = db.EntrevistaInicials.FirstOrDefault(x => x.IdPersona == id);
                TutoriaGrupal tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == ei.IdCarrera && x.IdGrado == ei.IdGrado && x.IdGrupo == ei.IdGrupo && x.IdTurno == ei.IdTurno && x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);

                ViewBag.id = tutgrup.IdTutoriaGrupal;

                foreach (EntrevistaInicial v in alumno)
                {
                    string grupo = "";
                    var t = db.Turnoes.FirstOrDefault(x => x.IdTurno == v.IdTurno);
                    var c = db.Carreras.FirstOrDefault(x => x.IdCarrera == v.IdCarrera);
                    var grado = db.Gradoes.FirstOrDefault(x => x.IdGrado == v.IdGrado);
                    var grup = db.Grupoes.FirstOrDefault(x => x.IdGrupo == v.IdGrupo);

                    if (t.Nombre == "Matutino")
                    {
                        grupo += "M";
                    }
                    else if (t.Nombre == "Vespertino")
                    {
                        grupo += "I";
                    }
                    else if (t.Nombre == "Despresurizado")
                    {
                        grupo += "D";
                    }
                    grupo += c.Nomenclatura;
                    grupo += grado.Nombre;
                    grupo += grup.Nombre;

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
                    v.EleccionVulnerabilidad = db.Vulnerable.FirstOrDefault(x => x.IdEleccionVunerabilidad == v.IdEleccionVunerabilidad).Nombre;
                    v.Embarazo = db.Respuesta11.FirstOrDefault(x => x.IdEmbarazo == v.IdEmbarazo).Nombre;
                    v.ListaBachillerato = db.Respuesta12.FirstOrDefault(x => x.IdListaBachillerato == v.IdListaBachillerato).Nombre;
                    v.EquipoComp = db.Respuesta13.FirstOrDefault(x => x.IdEquipoComp == v.IdEquipoComp).Nombre;
                    v.TipoDispositivo = db.Respuesta14.FirstOrDefault(x => x.IdTipoDispositivo == v.IdTipoDispositivo).Nombre;
                    v.AccesoInternet = db.Respuesta15.FirstOrDefault(x => x.IdAccesoInternet == v.IdAccesoInternet).Nombre;
                    v.TipoFamiliar = db.Respuesta16.FirstOrDefault(x => x.IdTipoFamiliar == v.IdTipoFamiliar).Nombre;
                    v.IngresoMes = db.Respuesta17.FirstOrDefault(x => x.IdIngresoMes == v.IdIngresoMes).Nombre;
                    v.SolicitarBeca = db.Respuesta18.FirstOrDefault(x => x.IdSolicitarBeca == v.IdSolicitarBeca).Nombre;


                }

                ReportViewer report1 = new ReportViewer();
                ReportDataSource rds = new ReportDataSource();
                rds.Value = alumno;
                rds.Name = "EntrevistaInicial";
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

        // Métodos del controlador para manejar notificaciones
        public JsonResult GetUnreadNotifications()
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null) return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            var notificaciones = db.Notificaciones
                .Where(n => n.IdTutor == usuario.IdUsuario && !n.Leida)
                .OrderByDescending(n => n.FechaEnvio)
                .Select(n => new
                {
                    n.IdNotificacion,
                    n.IdEstudiante,
                    NombreEstudiante = n.Estudiante.Nombre,
                    n.Mensaje,
                    n.FechaEnvio
                })
                .ToList()
                .Select(n => new
                {
                    n.IdNotificacion,
                    n.IdEstudiante,
                    NombreEstudiante = HttpUtility.HtmlDecode(n.NombreEstudiante),
                    n.Mensaje,
                    FechaEnvio = n.FechaEnvio.ToString("o")
                })
                .ToList();

            return Json(notificaciones, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetUnreadNotificationCount()
        {
            Debug.WriteLine("GetUnreadNotificationCount: Iniciando...");
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null)
            {
                Debug.WriteLine("GetUnreadNotificationCount: Sesión nula.");
                return Json(0, JsonRequestBehavior.AllowGet);
            }

            var count = db.Notificaciones
                .Count(n => n.IdTutor == usuario.IdUsuario && !n.Leida);
            Debug.WriteLine($"GetUnreadNotificationCount: Encontradas {count} notificaciones.");

            return Json(count, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult MarkNotificationAsRead(int id)
        {
            var notificacion = db.Notificaciones.Find(id);
            if (notificacion != null)
            {
                notificacion.Leida = true;
                db.SaveChanges();
            }
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult DeleteNotification(int id)
        {
            var notificacion = db.Notificaciones.Find(id);
            if (notificacion != null)
            {
                db.Notificaciones.Remove(notificacion);
                db.SaveChanges();
            }
            return Json(new { success = true });
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
                    return File(alumno.Foto, "image/jpeg");
                }
            }

            Debug.WriteLine($"ObtenerFoto: Returning default image for IdPersona={id}.");
            byte[] imagenDefault = System.IO.File.ReadAllBytes(Server.MapPath("~/css/estudiantes/default.png"));
            return File(imagenDefault, "image/png");
        }

        // Método privado para evitar duplicar código y siempre llenar los SelectList
        private void SetSelectLists(EntrevistaInicial comp)
        {
            var nivelAutoestima = comp?.IdNivelAutoestima;
            var nivelTamizaje = comp?.IdNivelTamizaje;
            var nivelPensamientoAbstracto = comp?.IdNivelPensamientoAbstracto;
            var aspectoPersonal = comp?.IdAspectoPersonal;
            var aspectoAcademico = comp?.IdAspectoAcademico;
            var aspectoEconomico = comp?.IdAspectoEconomico;

            ViewBag.ListaAutoestima = new SelectList(
                db.NivelDesempenoPerfils.Where(x => x.Area == "Autoestima").ToList(),
                "IdNivelDesempeno",
                "NivelDescripcion",
                nivelAutoestima
            );

            ViewBag.ListaTamizaje = new SelectList(
                db.NivelDesempenoPerfils.Where(x => x.Area == "Tamizaje").ToList(),
                "IdNivelDesempeno",
                "NivelDescripcion",
                nivelTamizaje
            );

            ViewBag.ListaPensamientoAbstracto = new SelectList(
                db.NivelDesempenoPerfils.Where(x => x.Area == "Pensamiento Abstracto").ToList(),
                "IdNivelDesempeno",
                "NivelDescripcion",
                nivelPensamientoAbstracto
            );
            ViewBag.ListaPersonal = new SelectList(
                db.NivelDesempenoPerfils.Where(x => x.Area == "Personal").ToList(),
                "IdNivelDesempeno",
                "NivelDescripcion",
                aspectoPersonal
            );

            ViewBag.ListaAcademico = new SelectList(
                db.NivelDesempenoPerfils.Where(x => x.Area == "Academico").ToList(),
                "IdNivelDesempeno",
                "NivelDescripcion",
                aspectoAcademico
            );

            ViewBag.ListaEconomico = new SelectList(
                db.NivelDesempenoPerfils.Where(x => x.Area == "Economico").ToList(),
                "IdNivelDesempeno",
                "NivelDescripcion",
                aspectoEconomico
            );

            ViewBag.IdVulnerable = new SelectList(db.Respuesta10.ToList(), "IdVulnerable", "Nombre", comp?.IdVulnerable);
            ViewBag.IdEleccionVunerabilidad = new SelectList(db.Vulnerable.ToList(), "IdEleccionVunerabilidad", "Nombre", comp?.IdEleccionVunerabilidad);
        }

        // ===== MÉTODOS HELPER PARA CREAR PRIMER SEGUIMIENTO INDIVIDUAL =====

        /// <summary>
        /// Verifica si es la primera revisión del cuatrimestre y crea automáticamente
        /// el primer seguimiento individual si es necesario.
        /// </summary>
        private void CrearPrimerSeguimientoSiEsNecesario(int idPersona, int? idVulnerable, int? idTipoVulnerabilidad)
        {
            try
            {
                // Calcular periodo y año actuales
                var ahora = DateTime.Now;
                var periodoActual = GetPeriodoActual(ahora);
                var añoActual = ahora.Year;

                // Obtener datos del alumno
                var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);
                if (dp == null) return;

                // Calcular grupo y cuatrimestre actuales
                var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == dp.IdTurno);
                var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == dp.IdCarrera);
                var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == dp.IdGrado);
                var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == dp.IdGrupo);

                string grupoActual = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

                string cuatriActual;
                if (ahora.Month <= 4) cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
                else if (ahora.Month <= 8) cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
                else cuatriActual = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

                // Buscar si ya existe una hoja Individual para este periodo/año
                var hojaExistente = db.Individuals
                    .Where(i => i.IdPersona == idPersona &&
                                i.Grupo == grupoActual &&
                                i.Cuatrimestre == cuatriActual &&
                                i.Fecha.Year == añoActual)
                    .FirstOrDefault();

                // Si no existe hoja o si existe pero no tiene seguimientos, crear el primer seguimiento
                if (hojaExistente == null)
                {
                    // Crear nueva hoja Individual
                    hojaExistente = CrearHojaIndividualParaAlumno(idPersona);
                    if (hojaExistente == null) return;
                }

                // Verificar si ya tiene seguimientos
                var primerSeguimiento = db.Seguimientoes
                    .Where(s => s.IdIndividual == hojaExistente.IdIndividual)
                    .OrderBy(s => s.Fecha)
                    .ThenBy(s => s.IdSeguimiento)
                    .FirstOrDefault();

                if (primerSeguimiento != null)
                {
                    // CASO: YA EXISTE SEGUIMIENTO (Manual o Automático)
                    // Actualizamos SOLAMENTE la vulnerabilidad
                    primerSeguimiento.Vulnerabilidad = GetTextoVulnerabilidad(idVulnerable, idTipoVulnerabilidad);
                    db.SaveChanges();
                }
                else
                {
                    // CASO: NO TIENE SEGUIMIENTOS -> Crear el primero
                    CrearPrimerSeguimiento(hojaExistente.IdIndividual, idVulnerable, idTipoVulnerabilidad);
                }
            }
            catch (Exception ex)
            {
                // Log el error pero no interrumpir el flujo normal
                System.Diagnostics.Debug.WriteLine($"Error al gestionar primer seguimiento: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea una hoja Individual para el alumno con los datos actuales.
        /// </summary>
        private Individual CrearHojaIndividualParaAlumno(int idPersona)
        {
            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == idPersona);
            if (dp == null) return null;

            var individual = new Individual
            {
                IdPersona = idPersona,
                Fecha = DateTime.Now,
                Nombre = dp.Nombre,
                Matricula = dp.Matricula,
                Carrera = dp.CarreraNom,
                Especialidad = dp.Especialidad ?? "",
                Area = dp.Area
            };

            // Construcción de nomenclatura de grupo
            var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == dp.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == dp.IdCarrera);
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == dp.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == dp.IdGrupo);

            individual.Grupo = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

            // Cuatrimestre por mes
            var mes = DateTime.Now.Month;
            if (mes <= 4) individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
            else if (mes <= 8) individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
            else individual.Cuatrimestre = db.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

            // Lógica adicional: sobreescribir Carrera/Area si es TSU/Ing
            if (!string.IsNullOrEmpty(individual.Especialidad) &&
                (individual.Especialidad.StartsWith("Ingeniería") || individual.Especialidad.StartsWith("Licenciatura")))
            {
                individual.Carrera = individual.Especialidad;
                individual.Area = "";
            }
            else
            {
                individual.Carrera = "TSU en " + (c?.Nombre ?? "");
                individual.Area = individual.Especialidad ?? "";
            }

            db.Individuals.Add(individual);
            db.SaveChanges();
            return individual;
        }

        /// <summary>
        /// Crea el primer seguimiento individual tomando los datos reales seleccionados en la entrevista.
        /// </summary>
        private void CrearPrimerSeguimiento(int idIndividual, int? idVulnerable, int? idTipoVulnerabilidad)
        {
            // 1. Obtener texto de vulnerabilidad usando el helper
            string textoVulnerabilidad = GetTextoVulnerabilidad(idVulnerable, idTipoVulnerabilidad);

            // 2. Definir otros campos por defecto
            string problematica = "Semana 1. Entrevista Inicial";
            string accion = "Se revisa entrevista inicial del alumno en plataforma";

            // Si es vulnerable (el texto NO es "NO VULNERABLE"), ajustamos la acción si es necesario
            // Nota: La lógica original cambiaba 'accion' a "Por definir" si era vulnerable.
            if (textoVulnerabilidad != "NO VULNERABLE")
            {
                accion = "Por definir";
            }

            // 5. Crear el objeto Seguimiento con los datos dinámicos
            var seguimiento = new Seguimiento
            {
                IdIndividual = idIndividual,
                Fecha = DateTime.Now,
                Vulnerabilidad = textoVulnerabilidad,  // Columna: TIPO DE VULNERABILIDAD
                Problematica = problematica,           // Columna: ACCIÓN / PROBLEMÁTICA
                Accion = accion                        // Columna: ACCIONES Y/O CANALIZACIONES
            };

            db.Seguimientoes.Add(seguimiento);
            db.SaveChanges();
        }

        /// <summary>
        /// Obtiene el periodo actual basado en el mes.
        /// </summary>
        private int GetPeriodoActual(DateTime fecha)
        {
            if (fecha.Month >= 1 && fecha.Month <= 4) return 1;
            if (fecha.Month >= 5 && fecha.Month <= 8) return 2;
            return 3;
        }

        /// <summary>
        /// Helper para generar el texto de vulnerabilidad (ECONOMICO, ACADEMICO, NO VULNERABLE)
        /// basado en los IDs seleccionados.
        /// </summary>
        private string GetTextoVulnerabilidad(int? idVulnerable, int? idTipoVulnerabilidad)
        {
            try
            {
                var esVulnerableObj = db.Respuesta10.FirstOrDefault(x => x.IdVulnerable == idVulnerable);

                if (esVulnerableObj != null && esVulnerableObj.Nombre.ToUpper().Trim() == "SI")
                {
                    var tipoObj = db.Vulnerable.FirstOrDefault(x => x.IdEleccionVunerabilidad == idTipoVulnerabilidad);
                    return tipoObj != null ? tipoObj.Nombre.ToUpper() : "VULNERABLE (NO ESPECIFICADO)";
                }

                return "NO VULNERABLE";
            }
            catch
            {
                return "NO VULNERABLE";
            }
        }
    }
}