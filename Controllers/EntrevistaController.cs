using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.BecasTransporte.Models;
using PlataformaWeb.Helpers;
using PlataformaWeb.Models;
using ProyectoIntegracion.Models.GestionUsuarios;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.PrimeraEntrevista
{
    [CustomAuthorize(Nivel = 1)]
    public class EntrevistaController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string pattern = @"[^a-zA-ZáéíóúÁÉÍÓÚñÑüÜ0-9\s.,;:¿?¡!()\-]";
            string cleaned = System.Text.RegularExpressions.Regex.Replace(input, pattern, "");

            return cleaned.ToUpper().Trim();
        }

        private bool IsSamePeriod(DateTime? lastUpdateDate)
        {
            if (!lastUpdateDate.HasValue) return false;

            var now = DateTime.Now;
            if (lastUpdateDate.Value.Year != now.Year) return false;

            int currentPeriod = 0;
            if (now.Month >= 1 && now.Month <= 4) currentPeriod = 1;
            else if (now.Month >= 5 && now.Month <= 8) currentPeriod = 2;
            else currentPeriod = 3;

            int updatePeriod = 0;
            if (lastUpdateDate.Value.Month >= 1 && lastUpdateDate.Value.Month <= 4) updatePeriod = 1;
            else if (lastUpdateDate.Value.Month >= 5 && lastUpdateDate.Value.Month <= 8) updatePeriod = 2;
            else updatePeriod = 3;

            return currentPeriod == updatePeriod;
        }

        public ActionResult Index(int? mensaje)
        {
            if (mensaje == 1)
            {
                ViewBag.Mensaje = "No existe una entrevista con la matricula del usuario, favor de realizar la entrevista inicial.";
            }
            else if (mensaje == 2)
            {
                ViewBag.Mensaje = "No se ha completado su entrevista inicial, favor de terminar la entrevista inicial.";
            }
            else if (mensaje == 3)
            {
                ViewBag.Mensaje = "Lo sentimos, ya realizaste esta entrevista, favor de actualizarla en caso de ser necesario.";
            }
            else if (mensaje == 4)
            {
                ViewBag.Mensaje = "Tu entrevista ya ha sido revisada por tu tutor. No puedes realizar cambios. Contacta a tu tutor si necesitas corregir algo.";
            }

            Usuario user = (Usuario)Session["Usuario"];
            DatosPersonales alumno = db.DatosPersonales.FirstOrDefault(x => x.Matricula == user.UserName);

            if (alumno != null)
            {
                var aspectosAcademicos = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == alumno.IdPersona);
                var aspectosEconomicos = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == alumno.IdPersona);
                var aspectosPersonales = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == alumno.IdPersona);
                if (aspectosAcademicos != null && aspectosEconomicos != null && aspectosPersonales != null)
                {
                    ViewBag.ExisteAlumno = alumno;
                }
            }
            else
            {
                ViewBag.ExisteAlumno = alumno;
            }

            return View();
        }

        public ActionResult Buscar()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            DatosPersonales alumno = new DatosPersonales();
            try
            {
                alumno = db.DatosPersonales.FirstOrDefault(x => x.Matricula == usuario.UserName);
            }
            catch
            {
                alumno = null;
            }

            if (alumno != null)
            {
                var aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == alumno.IdPersona);
                var ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == alumno.IdPersona);
                var ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == alumno.IdPersona);
                if (aa != null && ae != null && ap != null)
                {
                    return RedirectToAction("DatosPersonalesEdit", new { id = alumno.IdPersona });
                }
                else
                {
                    return RedirectToAction("Index", new { mensaje = 2 });
                }
            }
            else if (alumno == null)
            {
                return RedirectToAction("Index", new { mensaje = 1 });
            }
            return View();
        }

        [LecturaPermitida]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Buscar(DatosPersonales datos)
        {
            var alumno = db.DatosPersonales.FirstOrDefault(x => x.Matricula == datos.Matricula);
            if (alumno == null)
            {
                ViewBag.Mensaje = "Esta matricula no se encuentra en el sistema, Ingresa una matricula valida por favor";
                return View(datos);
            }
            else
            {
                return RedirectToAction("DatosPersonalesEdit", new { id = alumno.IdPersona });
            }
        }

        public ActionResult DatosPersonalesEdit(int? id)
        {

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            DatosPersonales datosPersonales = db.DatosPersonales.Find(id);

            if (datosPersonales == null)
            {
                return HttpNotFound();
            }

            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario.UserName != datosPersonales.Matricula)
            {
                return RedirectToAction("Index");
            }

            if (usuario.UserName != datosPersonales.Matricula)
            {
                return RedirectToAction("Index");
            }

            if (datosPersonales.Estado)
            {
                if (IsSamePeriod(datosPersonales.Fecha))
                {
                    return RedirectToAction("Index", new { mensaje = 4 });
                }
                else
                {
                    // Desbloqueo automático al cambiar de periodo/año
                    datosPersonales.Estado = false;
                    datosPersonales.Fecha = DateTime.Now;
                    db.Configuration.ValidateOnSaveEnabled = false;
                    db.Entry(datosPersonales).State = EntityState.Modified;
                    db.SaveChanges();
                    db.Configuration.ValidateOnSaveEnabled = true;
                }
            }

            Session["IdPersona"] = datosPersonales.IdPersona;

            string[] lista = new string[] { "Sin especialidad definida" };

            if (datosPersonales.IdCarrera == 1)
            {
                lista = new string[] { "Ingeniería en Tecnologías de la Información", "Entornos Virtuales y Negocios Digitales", "Infraestructura de Redes Digitales", "Desarrollo de Software Multiplataforma", "Ciencia de Datos", "Inteligencia Artificial", "Ingeniería en Desarrollo y Gestión de Software", "Ingeniería en Entornos Virtuales y Negocios Digitales" };
            }
            else if (datosPersonales.IdCarrera == 2)
            {
                lista = new string[] { "Ingeniería en Mantenimiento", "Ingeniería Petrolera", "Industrial", "Refrigeración ", "Petróleo", "Maquinaria Pesada" };
            }
            else if (datosPersonales.IdCarrera == 3)
            {
                lista = new string[] { "Ingeniería en Mecatronica", "Manufactura Flexible", "Automatización", "Instalaciones Eléctricas", "Robótica" };
            }
            else if (datosPersonales.IdCarrera == 4)
            {
                lista = new string[] { "Licenciatura en Gestión de Negocios y Proyectos", "Licenciatura en Gestión de Capital Humano", "Formulación y Evaluación de Proyectos", "Capital Humano", "Administración" };
            }
            else if (datosPersonales.IdCarrera == 5)
            {
                lista = new string[] { "Ingeniería en Procesos y Operaciones Industriales", "Manufactura", "Plástico", "Procesos Productivos", "Moldeo de Plástico", "Ingeniería Industrial", "Sistema de Gestión de Calidad" };
            }
            else if (datosPersonales.IdCarrera == 6)
            {
                lista = new string[] { "Ingeniería en Energías Renovables", "Calidad en el Ahorro de Energías", "Energía Turbo-Solar", "Ingeniería en Energía y Desarrollo Sostenible" };
            }
            else if (datosPersonales.IdCarrera == 7)
            {
                lista = new string[] { "Licenciatura en Diseño y Gestión en Redes Logísticas", "Cadena de Subministros", "Transporte Terrestre", "Logística" };
            }
            else if (datosPersonales.IdCarrera == 8)
            {
                lista = new string[] { "Comercio Exterior", "Ingeniería en Logística Internacional" };
            }
            else if (datosPersonales.IdCarrera == 9)
            {
                lista = new string[] { "Manufactura Aeronáutica", "Ingeniería Aeronáutica en Manufactura" };
            }
            else if (datosPersonales.IdCarrera == 10)
            {
                lista = new string[] { "Ingeniería en Microelectrónica y Semiconductores", "Manufactura de Semiconductores" };
            }
            else if (datosPersonales.IdCarrera == 11)
            {
                lista = new string[] { "Ingeniería en datos e Inteligencia Artificial", "Ciencia de Datos" };
            }

            var Esp = new List<SelectListItem>();
            if (lista != null)
            {
                Esp = lista.Select(p => new SelectListItem() { Value = p, Text = p }).ToList();
            }
            ViewBag.Especialidad = new SelectList(Esp, "Value", "Text", datosPersonales.Especialidad);

            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre", datosPersonales.IdTurno);
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre", datosPersonales.IdCarrera);
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre", datosPersonales.IdGrado);
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre", datosPersonales.IdGrupo);
            var temp = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
            System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit GET] IdPersona: {id}");
            if (temp != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit GET] AspectosAcademicos FOUND. Id: {temp.IdPersona}");
                ViewBag.Id = temp.IdPersona;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit GET] AspectosAcademicos NOT FOUND for IdPersona: {id}");
                ViewBag.Id = null;
            }
            ViewBag.Datos = datosPersonales;
            return View(datosPersonales);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DatosPersonalesEdit(DatosPersonales datosPersonales, int id)
        {
            System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit POST] Start. IdPersona: {id}, Matricula: {datosPersonales.Matricula}");
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario.UserName != datosPersonales.Matricula)
            {
                return RedirectToAction("Index");
            }

            // Check lock status
            var dpCheck = db.DatosPersonales.AsNoTracking().FirstOrDefault(x => x.IdPersona == id);
            if (dpCheck != null && dpCheck.Estado && IsSamePeriod(dpCheck.Fecha))
            {
                return RedirectToAction("Index", new { mensaje = 4 });
            }

            string[] lista = new string[] { "Sin especialidad definida" };

            if (datosPersonales.IdCarrera == 1)
            {
                lista = new string[] { "Ingeniería en Tecnologías de la Información", "Entornos Virtuales y Negocios Digitales", "Infraestructura de Redes Digitales", "Desarrollo de Software Multiplataforma", "Ciencia de Datos", "Inteligencia Artificial", "Ingeniería en Desarrollo y Gestión de Software", "Ingeniería en Entornos Virtuales y Negocios Digitales" };
            }
            else if (datosPersonales.IdCarrera == 2)
            {
                lista = new string[] { "Ingeniería en Mantenimiento", "Ingeniería Petrolera", "Industrial", "Refrigeración ", "Petróleo", "Maquinaria Pesada" };
            }
            else if (datosPersonales.IdCarrera == 3)
            {
                lista = new string[] { "Ingeniería en Mecatronica", "Manufactura Flexible", "Automatización", "Instalaciones Eléctricas", "Robótica" };
            }
            else if (datosPersonales.IdCarrera == 4)
            {
                lista = new string[] { "Licenciatura en Gestión de Negocios y Proyectos", "Licenciatura en Gestión de Capital Humano", "Formulación y Evaluación de Proyectos", "Capital Humano", "Administración" };
            }
            else if (datosPersonales.IdCarrera == 5)
            {
                lista = new string[] { "Ingeniería en Procesos y Operaciones Industriales", "Manufactura", "Plástico", "Procesos Productivos", "Moldeo de Plástico", "Ingeniería Industrial", "Sistema de Gestión de Calidad" };
            }
            else if (datosPersonales.IdCarrera == 6)
            {
                lista = new string[] { "Ingeniería en Energías Renovables", "Calidad en el Ahorro de Energías", "Energía Turbo-Solar", "Ingeniería en Energía y Desarrollo Sostenible" };
            }
            else if (datosPersonales.IdCarrera == 7)
            {
                lista = new string[] { "Licenciatura en Diseño y Gestión en Redes Logísticas", "Cadena de Subministros", "Transporte Terrestre", "Logística" };
            }
            else if (datosPersonales.IdCarrera == 8)
            {
                lista = new string[] { "Comercio Exterior", "Ingeniería en Logística Internacional" };
            }
            else if (datosPersonales.IdCarrera == 9)
            {
                lista = new string[] { "Manufactura Aeronáutica", "Ingeniería Aeronáutica en Manufactura" };
            }
            else if (datosPersonales.IdCarrera == 10)
            {
                lista = new string[] { "Ingeniería en Microelectrónica y Semiconductores", "Manufactura de Semiconductores" };
            }
            else if (datosPersonales.IdCarrera == 11)
            {
                lista = new string[] { "Ingeniería en datos e Inteligencia Artificial", "Ciencia de Datos" };
            }

            var Esp = new List<SelectListItem>();
            if (lista != null)
            {
                Esp = lista.Select(p => new SelectListItem() { Value = p, Text = p }).ToList();
            }
            ViewBag.Especialidad = new SelectList(Esp, "Value", "Text", datosPersonales.Especialidad);
            ViewBag.Datos = db.DatosPersonales.Find(datosPersonales.IdPersona);

            DatosPersonales datosPersonalesDatos = db.DatosPersonales.Find(id);

            if (datosPersonales.Nom == null || datosPersonales.Nom == "")
            {
                ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
                ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
                ViewBag.Mensaje = "Por favor, escriba su nombre en el campo correspondiente";
                return View(datosPersonales);
            }
            if (datosPersonales.Paterno == null || datosPersonales.Paterno == "")
            {
                ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
                ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
                ViewBag.Mensaje = "Por favor, escriba su apellido paterno en el campo correspondiente";
                return View(datosPersonales);
            }
            if (datosPersonales.Materno == null || datosPersonales.Materno == "")
            {
                ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
                ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
                ViewBag.Mensaje = "Por favor, escriba su apellido materno en el campo correspondiente";
                return View(datosPersonales);
            }

            datosPersonales.Nombre = datosPersonales.Paterno + " " + datosPersonales.Materno + " " + datosPersonales.Nom;
            datosPersonales.Email = usuario.CorreoElectronico;
            datosPersonales.Matricula = usuario.UserName;
            datosPersonales.Año = DateTime.Now.Year;
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

            datosPersonales.IdPeriodo = pa;

            // FIX 1: Ignore Email validation error from binding because we overwrite it from Session
            ModelState.Remove("Email");

            if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(datosPersonales.Foto))
                {
                    datosPersonales.Fecha = DateTime.Now;
                }
                else if (datosPersonales.FotoFile != null && (datosPersonales.FotoFile.FileName.EndsWith(".jpg") || datosPersonales.FotoFile.FileName.EndsWith(".JPG")))
                {
                    if (datosPersonales.FotoFile.ContentLength <= 20480)
                    {
                        using (var br = new BinaryReader(datosPersonales.FotoFile.InputStream))
                        {
                            var bytes = br.ReadBytes((int)datosPersonales.FotoFile.InputStream.Length);
                            datosPersonales.Foto = "data:image/jpg;base64," + Convert.ToBase64String(bytes);
                        }
                        datosPersonales.Fecha = DateTime.Now;
                    }
                    else
                    {
                        ViewBag.Mensaje = "La foto debe pesar menos de 20 KB.";
                        ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
                        ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
                        ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
                        ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
                        // FIX 2: Ensure ViewBag.Id is set so Siguiente button appears on error
                        var tempAspectos = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
                        ViewBag.Id = tempAspectos?.IdPersona;
                        ViewBag.Datos = db.DatosPersonales.Find(id); // Ensure Datos is also set
                        return View(datosPersonales);
                    }
                }

                datosPersonales.Estado = false; // Always unlock when updating
                datosPersonales.Fecha = DateTime.Now;
                datosPersonales.IdPersona = id;
                var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == datosPersonales.IdCarrera);
                datosPersonales.CarreraNom = c.Nombre;
                datosPersonales.Area = datosPersonales.Especialidad;
                using (ModeloPlataforma data = new ModeloPlataforma())
                {
                    data.Entry(datosPersonales).State = EntityState.Modified;
                    System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit POST] Saving changes to DatosPersonales...");
                    data.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit POST] Changes saved successfully.");

                    var gradoObj = data.Gradoes.Find(datosPersonales.IdGrado);
                    int nuevoCuatrimestre = 0;
                    if (gradoObj != null)
                    {
                        int.TryParse(gradoObj.Nombre, out nuevoCuatrimestre);
                    }

                    using (usuarios_model_db GestionUsuarios = new usuarios_model_db())
                    {
                        var alumnoGestion = GestionUsuarios.Alumnos.FirstOrDefault(x => x.Matricula == datosPersonales.Matricula);

                        if (alumnoGestion != null)
                        {
                            alumnoGestion.Cuatrimestre = nuevoCuatrimestre;
                            GestionUsuarios.Entry(alumnoGestion).State = EntityState.Modified;
                            GestionUsuarios.SaveChanges();
                        }
                    }

                    if (nuevoCuatrimestre == 5 || nuevoCuatrimestre == 6 || nuevoCuatrimestre == 10 || nuevoCuatrimestre == 11)
                    {
                        TempData["MensajeAlerta"] = "Tus datos se actualizaron y se han habilitado nuevos módulos. Por favor, cierra e inicia sesión nuevamente para verlos.";
                    }

                    var temp = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == datosPersonales.IdPersona);

                    // FIX: Si no existe el registro de AspectosAcademicos, crearlo para evitar error 400/500
                    if (temp == null)
                    {
                        temp = new AspectosAcademicos
                        {
                            IdPersona = datosPersonales.IdPersona,
                            Especialidad = datosPersonales.Especialidad // Pre-llenar con especialidad si es posible
                        };
                        db.AspectosAcademicos.Add(temp);
                        db.SaveChanges();
                    }

                    return RedirectToAction("AspectosAcademicosEdit", new { id = temp.IdPersona });
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                System.Diagnostics.Debug.WriteLine($"[DatosPersonalesEdit POST] ModelState INVALID. Errors: {string.Join(", ", errors)}");
            }

            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");

            // FIX 2: Ensure ViewBag.Id is set so Siguiente button appears on error
            var tempAspectosEnd = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
            ViewBag.Id = tempAspectosEnd?.IdPersona;
            ViewBag.Datos = db.DatosPersonales.Find(id);

            return View(datosPersonales);
        }

        public ActionResult AspectosAcademicosEdit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            AspectosAcademicos aspectosAcademicos = db.AspectosAcademicos
        .Include(a => a.Respuesta12)
        .Include(a => a.Respuesta13)
        .Include(a => a.Respuesta14)
        .Include(a => a.Respuesta15)
        .FirstOrDefault(x => x.IdPersona == id);
            DatosPersonales datosPersonales = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (aspectosAcademicos == null)
            {
                return HttpNotFound();
            }

            if (datosPersonales == null)
            {
                return HttpNotFound();
            }

            int idPersona = (int)Session["IdPersona"];
            if (idPersona != aspectosAcademicos.IdPersona)
            {
                return RedirectToAction("Index");
            }

            if (datosPersonales.Estado)
            {
                if (IsSamePeriod(datosPersonales.Fecha))
                {
                    return RedirectToAction("Index", new { mensaje = 4 });
                }
                else
                {
                    // Desbloqueo automático al cambiar de periodo/año
                    datosPersonales.Estado = false;
                    datosPersonales.Fecha = DateTime.Now;
                    db.Configuration.ValidateOnSaveEnabled = false;
                    db.Entry(datosPersonales).State = EntityState.Modified;
                    db.SaveChanges();
                    db.Configuration.ValidateOnSaveEnabled = true;
                }
            }

            ViewBag.IdListaBachillerato = new SelectList(db.Respuesta12, "IdListaBachillerato", "Nombre", aspectosAcademicos.IdListaBachillerato);
            ViewBag.IdEquipoComp = new SelectList(db.Respuesta13, "IdEquipoComp", "Nombre", aspectosAcademicos.IdEquipoComp);
            ViewBag.IdTipoDispositivo = new SelectList(db.Respuesta14, "IdTipoDispositivo", "Nombre", aspectosAcademicos.IdTipoDispositivo);
            ViewBag.IdAccesoInternet = new SelectList(db.Respuesta15, "IdAccesoInternet", "Nombre", aspectosAcademicos.IdAccesoInternet);

            // Obtener nombres directamente para la vista de solo lectura
            ViewBag.NombreDispositivo = db.Respuesta14.FirstOrDefault(r => r.IdTipoDispositivo == aspectosAcademicos.IdTipoDispositivo)?.Nombre ?? "Sin especificar";

            ViewBag.Id = id;
            ViewBag.Aspectos = aspectosAcademicos;
            ViewBag.Datos = datosPersonales;
            ViewBag.MateriasAcreditadas = SelectListHelper.SelectorSiNo(aspectosAcademicos.MateriasRepro);
            return View(aspectosAcademicos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AspectosAcademicosEdit(AspectosAcademicos aspectosAcademicos, string AcreditoTodo)
        {
            // DEBUG: Log incoming values
            System.Diagnostics.Debug.WriteLine($"POST AspectosAcademicosEdit - IdPersona: {aspectosAcademicos.IdPersona}");
            System.Diagnostics.Debug.WriteLine($"POST AspectosAcademicosEdit - IdEquipoComp: {aspectosAcademicos.IdEquipoComp}");
            System.Diagnostics.Debug.WriteLine($"POST AspectosAcademicosEdit - IdTipoDispositivo: {aspectosAcademicos.IdTipoDispositivo}");
            System.Diagnostics.Debug.WriteLine($"POST AspectosAcademicosEdit - IdAccesoInternet: {aspectosAcademicos.IdAccesoInternet}");
            System.Diagnostics.Debug.WriteLine($"POST AspectosAcademicosEdit - ModelState.IsValid: {ModelState.IsValid}");

            // DEBUG: Log ModelState errors if invalid
            if (!ModelState.IsValid)
            {
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state.Errors.Count > 0)
                    {
                        foreach (var error in state.Errors)
                        {
                            System.Diagnostics.Debug.WriteLine($"ModelState ERROR - Key: {key}, Error: {error.ErrorMessage}");
                        }
                    }
                }
            }

            // Obtener datos personales
            DatosPersonales datosPersonales = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == aspectosAcademicos.IdPersona);

            // Limpiar errores de validación para campos que no aplican según el grado
            if (datosPersonales != null && datosPersonales.IdGrado != 1)
            {
                // Para estudiantes que NO son de primer ingreso, estos campos no están en el formulario
                ModelState.Remove("Especialidad");
                ModelState.Remove("IdListaBachillerato");
                ModelState.Remove("Bachillerato");
            }

            if (ModelState.IsValid)
            {
                int idPersona = (int)Session["IdPersona"];
                if (idPersona != aspectosAcademicos.IdPersona)
                {
                    return RedirectToAction("Index");
                }

                if (datosPersonales != null && datosPersonales.Estado && IsSamePeriod(datosPersonales.Fecha))
                {
                    return RedirectToAction("Index", new { mensaje = 4 });
                }

                var aspectosActuales = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == aspectosAcademicos.IdPersona);
                bool isNew = false;
                if (aspectosActuales == null)
                {
                    isNew = true;
                    aspectosActuales = new AspectosAcademicos { IdPersona = aspectosAcademicos.IdPersona };
                    db.AspectosAcademicos.Add(aspectosActuales);
                }

                // Bloque para actualizar propiedades
                {
                    aspectosActuales.IdListaBachillerato = (aspectosAcademicos.IdListaBachillerato == 0) ? 1 : aspectosAcademicos.IdListaBachillerato;
                    aspectosActuales.Bachillerato = SanitizeString(aspectosAcademicos.Bachillerato) ?? "N/A";
                    aspectosActuales.Especialidad = SanitizeString(aspectosAcademicos.Especialidad) ?? "N/A";
                    aspectosActuales.Promedio = SanitizeString(aspectosAcademicos.Promedio) ?? "N/A";
                    aspectosActuales.MateriasDif = SanitizeString(aspectosAcademicos.MateriasDif) ?? "N/A";

                    // Handle AcreditoTodo dropdown: if "1" (Sí) then N/A, if "2" (No) then use MateriasRepro value
                    if (AcreditoTodo == "1")
                    {
                        aspectosActuales.MateriasRepro = "N/A";
                    }
                    else if (AcreditoTodo == "2")
                    {
                        aspectosActuales.MateriasRepro = SanitizeString(aspectosAcademicos.MateriasRepro) ?? "N/A";
                    }
                    else
                    {
                        aspectosActuales.MateriasRepro = SanitizeString(aspectosAcademicos.MateriasRepro) ?? "N/A";
                    }

                    aspectosActuales.RendimientoClase = SanitizeString(aspectosAcademicos.RendimientoClase) ?? "N/A";
                    aspectosActuales.ExperienciaProfe = SanitizeString(aspectosAcademicos.ExperienciaProfe) ?? "N/A";
                    aspectosActuales.IdEquipoComp = (aspectosAcademicos.IdEquipoComp == 0) ? 2 : aspectosAcademicos.IdEquipoComp;

                    // Actualizar IdTipoDispositivo basado en el NUEVO valor de IdEquipoComp
                    if (aspectosActuales.IdEquipoComp == 1) // Usuario tiene equipo de computo
                    {
                        // Si tiene equipo, guardar el tipo seleccionado (o LAPTOP como default si no selecciono)
                        // ID 1 = "NO CUENTO", ID 2 = "LAPTOP", ID 3 = "PC", ID 4 = "TABLETA"
                        aspectosActuales.IdTipoDispositivo = (aspectosAcademicos.IdTipoDispositivo == 0 || aspectosAcademicos.IdTipoDispositivo == 1) ? 2 : aspectosAcademicos.IdTipoDispositivo;
                    }
                    else // Usuario NO tiene equipo de computo
                    {
                        // Si no tiene equipo, usar ID 1 = "NO CUENTO CON EQUIPO DE TRABAJO"
                        aspectosActuales.IdTipoDispositivo = 1;
                    }

                    aspectosActuales.IdAccesoInternet = (aspectosAcademicos.IdAccesoInternet == 0) ? 2 : aspectosAcademicos.IdAccesoInternet;

                    // DEBUG: Log values before save
                    System.Diagnostics.Debug.WriteLine($"ANTES DE GUARDAR - IdEquipoComp: {aspectosActuales.IdEquipoComp}");
                    System.Diagnostics.Debug.WriteLine($"ANTES DE GUARDAR - IdTipoDispositivo: {aspectosActuales.IdTipoDispositivo}");
                    System.Diagnostics.Debug.WriteLine($"ANTES DE GUARDAR - IdAccesoInternet: {aspectosActuales.IdAccesoInternet}");

                    // Ensure unlock
                    var dpUpdate = db.DatosPersonales.Find(aspectosAcademicos.IdPersona);
                    if (dpUpdate != null)
                    {
                        dpUpdate.Estado = false;
                        dpUpdate.Fecha = DateTime.Now;
                        db.Entry(dpUpdate).State = EntityState.Modified;
                    }

                    if (!isNew)
                    {
                        db.Entry(aspectosActuales).State = EntityState.Modified;
                    }
                    db.SaveChanges();
                }

                return RedirectToAction("AspectosEconomicosEdit", new { id = aspectosAcademicos.IdPersona });
            }

            ViewBag.IdListaBachillerato = new SelectList(db.Respuesta12, "IdListaBachillerato", "Nombre", aspectosAcademicos.IdListaBachillerato);
            ViewBag.IdEquipoComp = new SelectList(db.Respuesta13, "IdEquipoComp", "Nombre", aspectosAcademicos.IdEquipoComp);
            ViewBag.IdTipoDispositivo = new SelectList(db.Respuesta14, "IdTipoDispositivo", "Nombre", aspectosAcademicos.IdTipoDispositivo);
            ViewBag.IdAccesoInternet = new SelectList(db.Respuesta15, "IdAccesoInternet", "Nombre", aspectosAcademicos.IdAccesoInternet);
            ViewBag.Aspectos = aspectosAcademicos;
            ViewBag.Datos = datosPersonales;
            ViewBag.MateriasAcreditadas = SelectListHelper.SelectorSiNo(aspectosAcademicos.MateriasRepro);
            ViewBag.Id = aspectosAcademicos.IdPersona;
            return View(aspectosAcademicos);
        }

        public ActionResult AspectosEconomicosEdit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            AspectosEconomicos aspectosEconomicos = db.AspectosEconomicos
                .Include(a => a.Respuesta1)
                .Include(a => a.Respuesta2)
                .Include(a => a.Respuesta16)
                .Include(a => a.Respuesta17)
                .Include(a => a.Respuesta18)
                .FirstOrDefault(x => x.IdPersona == id);
            DatosPersonales datosPersonales = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (aspectosEconomicos == null)
            {
                System.Diagnostics.Debug.WriteLine($"=== AspectosEconomicosEdit GET === IdPersona: {id} - REGISTRO NO ENCONTRADO (NULL)");
                return HttpNotFound();
            }

            // DEBUG: Log de datos cargados
            System.Diagnostics.Debug.WriteLine($"=== AspectosEconomicosEdit GET ===");
            System.Diagnostics.Debug.WriteLine($"IdPersona: {aspectosEconomicos.IdPersona}");
            System.Diagnostics.Debug.WriteLine($"IdCiudad: {aspectosEconomicos.IdCiudad} | Respuesta1: {(aspectosEconomicos.Respuesta1 != null ? aspectosEconomicos.Respuesta1.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdTrabajo: {aspectosEconomicos.IdTrabajo} | Respuesta2: {(aspectosEconomicos.Respuesta2 != null ? aspectosEconomicos.Respuesta2.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdTipoFamiliar: {aspectosEconomicos.IdTipoFamiliar} | Respuesta16: {(aspectosEconomicos.Respuesta16 != null ? aspectosEconomicos.Respuesta16.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdIngresoMes: {aspectosEconomicos.IdIngresoMes} | Respuesta17: {(aspectosEconomicos.Respuesta17 != null ? aspectosEconomicos.Respuesta17.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdSolicitarBeca: {aspectosEconomicos.IdSolicitarBeca} | Respuesta18: {(aspectosEconomicos.Respuesta18 != null ? aspectosEconomicos.Respuesta18.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"OcupacionPapa: {aspectosEconomicos.OcupacionPapa}");
            System.Diagnostics.Debug.WriteLine($"OcupacionMama: {aspectosEconomicos.OcupacionMama}");
            System.Diagnostics.Debug.WriteLine($"==================================");

            int idPersona = (int)Session["IdPersona"];
            if (idPersona != aspectosEconomicos.IdPersona)
            {
                return RedirectToAction("Index");
            }

            if (datosPersonales.Estado && IsSamePeriod(datosPersonales.Fecha))
            {
                return RedirectToAction("Index", new { mensaje = 4 });
            }

            ViewBag.IdCiudad = new SelectList(db.Respuesta1, "IdCiudad", "Nombre", aspectosEconomicos.IdCiudad);
            ViewBag.IdTrabajo = new SelectList(db.Respuesta2, "IdTrabajo", "Nombre", aspectosEconomicos.IdTrabajo);
            ViewBag.IdTipoFamiliar = new SelectList(db.Respuesta16, "IdTipoFamiliar", "Nombre", aspectosEconomicos.IdTipoFamiliar);
            ViewBag.IdIngresoMes = new SelectList(db.Respuesta17, "IdIngresoMes", "Nombre", aspectosEconomicos.IdIngresoMes);
            ViewBag.IdSolicitarBeca = new SelectList(db.Respuesta18, "IdSolicitarBeca", "Nombre", aspectosEconomicos.IdSolicitarBeca);

            ViewBag.Id = id;
            ViewBag.Aspectos = aspectosEconomicos;
            ViewBag.Datos = datosPersonales;
            ViewBag.ApoyoSolicitado = SelectListHelper.SelectorNoSi(aspectosEconomicos.SolicitadoBeca);

            return View(aspectosEconomicos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AspectosEconomicosEdit(AspectosEconomicos aspectosEconomicos)
        {
            int idPersona = (int)Session["IdPersona"];
            if (idPersona != aspectosEconomicos.IdPersona)
            {
                return RedirectToAction("Index");
            }

            if (idPersona != aspectosEconomicos.IdPersona)
            {
                return RedirectToAction("Index");
            }

            // Obtener datos personales
            DatosPersonales datosPersonales = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == aspectosEconomicos.IdPersona);

            if (datosPersonales != null && datosPersonales.Estado && IsSamePeriod(datosPersonales.Fecha))
            {
                return RedirectToAction("Index", new { mensaje = 4 });
            }

            // *** CRÍTICO: Limpiar ModelState para ignorar validaciones automáticas ***
            ModelState.Clear();

            // Establecer valores por defecto para campos condicionales
            if (aspectosEconomicos.IdCiudad != 2)
            {
                aspectosEconomicos.Ciudad = "N/A";
            }

            // Manejar campos de seguimiento según el grado
            if (datosPersonales != null && datosPersonales.IdGrado == 1)
            {
                // Primer cuatrimestre - no son obligatorios
                if (string.IsNullOrWhiteSpace(aspectosEconomicos.SolicitadoBeca))
                {
                    aspectosEconomicos.SolicitadoBeca = "N/A";
                }
                if (string.IsNullOrWhiteSpace(aspectosEconomicos.AfectacionEco))
                {
                    aspectosEconomicos.AfectacionEco = "N/A";
                }
            }
            else
            {
                // Seguimiento - establecer N/A si están vacíos
                if (string.IsNullOrWhiteSpace(aspectosEconomicos.SolicitadoBeca))
                {
                    aspectosEconomicos.SolicitadoBeca = "N/A";
                }
                if (string.IsNullOrWhiteSpace(aspectosEconomicos.AfectacionEco))
                {
                    aspectosEconomicos.AfectacionEco = "N/A";
                }
            }

            // *** Validación manual solo de campos realmente requeridos ***
            bool esValido = true;

            if (aspectosEconomicos.IdCiudad == 0)
            {
                ModelState.AddModelError("IdCiudad", "Selecciona si resides en esta ciudad");
                esValido = false;
            }

            if (aspectosEconomicos.IdTrabajo == 0)
            {
                ModelState.AddModelError("IdTrabajo", "Selecciona si trabajas");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.OcupacionPapa))
            {
                ModelState.AddModelError("OcupacionPapa", "Ocupación de tú papá");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.OcupacionMama))
            {
                ModelState.AddModelError("OcupacionMama", "Ocupación de tú mamá");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.CantidadHermano))
            {
                ModelState.AddModelError("CantidadHermano", "Cantidad de hermanos");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.CantidadPersonas))
            {
                ModelState.AddModelError("CantidadPersonas", "Cantidad de personas que viven en tú casa");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.CantidadTrabajan))
            {
                ModelState.AddModelError("CantidadTrabajan", "Cantidad de miembros que trabajan en tú casa");
                esValido = false;
            }

            if (aspectosEconomicos.IdTipoFamiliar == 0)
            {
                ModelState.AddModelError("IdFamiliar", "Selecciona con quien vives");
                esValido = false;
            }

            if (aspectosEconomicos.IdIngresoMes == 0)
            {
                ModelState.AddModelError("IdIngresoMes", "Selecciona tu ingreso mensual");
                esValido = false;
            }

            if (aspectosEconomicos.IdSolicitarBeca == 0)
            {
                ModelState.AddModelError("IdSolicitarBeca", "Selecciona si necesitas beca");
                esValido = false;
            }

            if (aspectosEconomicos.IdCiudad == 2 && string.IsNullOrWhiteSpace(aspectosEconomicos.Ciudad))
            {
                ModelState.AddModelError("Ciudad", "Especifica en qué ciudad resides");
                esValido = false;
            }

            if (esValido)
            {
                try
                {
                    var aspectosActuales = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == aspectosEconomicos.IdPersona);
                    bool isNew = false;
                    if (aspectosActuales == null)
                    {
                        isNew = true;
                        aspectosActuales = new AspectosEconomicos { IdPersona = aspectosEconomicos.IdPersona };
                        db.AspectosEconomicos.Add(aspectosActuales);
                    }

                    // Bloque para actualizar propiedades
                    {
                        aspectosActuales.IdCiudad = (aspectosEconomicos.IdCiudad == 0) ? 2 : aspectosEconomicos.IdCiudad;
                        aspectosActuales.Ciudad = SanitizeString(aspectosEconomicos.Ciudad) ?? "N/A";
                        aspectosActuales.Familiar = SanitizeString(aspectosEconomicos.Familiar) ?? "N/A";
                        aspectosActuales.IdTrabajo = (aspectosEconomicos.IdTrabajo == 0) ? 2 : aspectosEconomicos.IdTrabajo;
                        aspectosActuales.Trabaja = SanitizeString(aspectosEconomicos.Trabaja) ?? "N/A";
                        aspectosActuales.Dependiente = SanitizeString(aspectosEconomicos.Dependiente) ?? "N/A";
                        aspectosActuales.OcupacionPapa = SanitizeString(aspectosEconomicos.OcupacionPapa) ?? "N/A";
                        aspectosActuales.OcupacionMama = SanitizeString(aspectosEconomicos.OcupacionMama) ?? "N/A";
                        aspectosActuales.CantidadHermano = SanitizeString(aspectosEconomicos.CantidadHermano) ?? "N/A";
                        aspectosActuales.SolicitadoBeca = aspectosEconomicos.SolicitadoBeca ?? "N/A";
                        aspectosActuales.AfectacionEco = SanitizeString(aspectosEconomicos.AfectacionEco) ?? "N/A";
                        aspectosActuales.CantidadPersonas = SanitizeString(aspectosEconomicos.CantidadPersonas) ?? "N/A";
                        aspectosActuales.CantidadTrabajan = SanitizeString(aspectosEconomicos.CantidadTrabajan) ?? "N/A";
                        aspectosActuales.IdTipoFamiliar = (aspectosEconomicos.IdTipoFamiliar == 0) ? 2 : aspectosEconomicos.IdTipoFamiliar;
                        aspectosActuales.IdIngresoMes = (aspectosEconomicos.IdIngresoMes == 0) ? 2 : aspectosEconomicos.IdIngresoMes;
                        aspectosActuales.IdSolicitarBeca = (aspectosEconomicos.IdSolicitarBeca == 0) ? 2 : aspectosEconomicos.IdSolicitarBeca;

                        aspectosActuales.IdSolicitarBeca = (aspectosEconomicos.IdSolicitarBeca == 0) ? 2 : aspectosEconomicos.IdSolicitarBeca;

                        // Ensure unlock
                        var dpUpdate = db.DatosPersonales.Find(aspectosEconomicos.IdPersona);
                        if (dpUpdate != null)
                        {
                            dpUpdate.Estado = false;
                            dpUpdate.Fecha = DateTime.Now;
                            db.Entry(dpUpdate).State = EntityState.Modified;
                        }

                        if (!isNew)
                        {
                            db.Entry(aspectosActuales).State = EntityState.Modified;
                        }
                        db.SaveChanges();

                        return RedirectToAction("AspectosPersonalesEdit", new { id = aspectosEconomicos.IdPersona });
                    }
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                {
                    System.Diagnostics.Debug.WriteLine("=== DbEntityValidationException en AspectosEconomicosEdit POST ===");
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"CAMPO: {validationError.PropertyName} | ERROR: {validationError.ErrorMessage}");
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("===============================================================");
                    ViewBag.Mensaje = "Error de validacion al guardar. Ver consola para detalles.";
                }
                catch (Exception ex)
                {
                    ViewBag.Mensaje = "Error al guardar los datos: " + ex.Message;
                    System.Diagnostics.Debug.WriteLine("Error al guardar: " + ex.Message);
                }
            }

            // Si hay errores, mostrar mensajes
            var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            if (errores.Any())
            {
                ViewBag.Mensaje = "Por favor completa los siguientes campos:\n" + string.Join("\n", errores);
                System.Diagnostics.Debug.WriteLine("Errores de validación: " + string.Join(", ", errores));
            }

            ViewBag.IdCiudad = new SelectList(db.Respuesta1, "IdCiudad", "Nombre", aspectosEconomicos.IdCiudad);
            ViewBag.IdTrabajo = new SelectList(db.Respuesta2, "IdTrabajo", "Nombre", aspectosEconomicos.IdTrabajo);
            ViewBag.IdTipoFamiliar = new SelectList(db.Respuesta16, "IdTipoFamiliar", "Nombre", aspectosEconomicos.IdTipoFamiliar);
            ViewBag.IdIngresoMes = new SelectList(db.Respuesta17, "IdIngresoMes", "Nombre", aspectosEconomicos.IdIngresoMes);
            ViewBag.IdSolicitarBeca = new SelectList(db.Respuesta18, "IdSolicitarBeca", "Nombre", aspectosEconomicos.IdSolicitarBeca);
            ViewBag.Aspectos = aspectosEconomicos;
            ViewBag.Datos = datosPersonales;
            ViewBag.ApoyoSolicitado = SelectListHelper.SelectorNoSi(aspectosEconomicos.SolicitadoBeca);
            ViewBag.Id = aspectosEconomicos.IdPersona;

            return View(aspectosEconomicos);
        }

        public ActionResult AspectosPersonalesEdit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            AspectosPersonales aspectosPersonales = db.AspectosPersonales
                .Include(a => a.Respuesta4)
                .Include(a => a.Respuesta5)
                .Include(a => a.Respuesta6)
                .Include(a => a.Respuesta7)
                .Include(a => a.Respuesta8)
                .Include(a => a.Respuesta9)
                .Include(a => a.Observacio)
                .Include(a => a.Respuesta11)
                .FirstOrDefault(x => x.IdPersona == id);
            DatosPersonales datosPersonales = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);

            if (aspectosPersonales == null)
            {
                System.Diagnostics.Debug.WriteLine($"=== AspectosPersonalesEdit GET === IdPersona: {id} - REGISTRO NO ENCONTRADO (NULL)");
                return HttpNotFound();
            }

            // DEBUG: Log de datos cargados
            System.Diagnostics.Debug.WriteLine($"=== AspectosPersonalesEdit GET ===");
            System.Diagnostics.Debug.WriteLine($"IdPersona: {aspectosPersonales.IdPersona}");
            System.Diagnostics.Debug.WriteLine($"IdCasado: {aspectosPersonales.IdCasado} | Respuesta4: {(aspectosPersonales.Respuesta4 != null ? aspectosPersonales.Respuesta4.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdHijo: {aspectosPersonales.IdHijo} | Respuesta5: {(aspectosPersonales.Respuesta5 != null ? aspectosPersonales.Respuesta5.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdEnfermedad: {aspectosPersonales.IdEnfermedad} | Respuesta6: {(aspectosPersonales.Respuesta6 != null ? aspectosPersonales.Respuesta6.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdFuma: {aspectosPersonales.IdFuma} | Respuesta7: {(aspectosPersonales.Respuesta7 != null ? aspectosPersonales.Respuesta7.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdBebida: {aspectosPersonales.IdBebida} | Respuesta8: {(aspectosPersonales.Respuesta8 != null ? aspectosPersonales.Respuesta8.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdVidaSinSentido: {aspectosPersonales.IdVidaSinSentido} | Respuesta9: {(aspectosPersonales.Respuesta9 != null ? aspectosPersonales.Respuesta9.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdObservacionFamilia: {aspectosPersonales.IdObservacionFamilia} | Observacio: {(aspectosPersonales.Observacio != null ? aspectosPersonales.Observacio.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"IdEmbarazo: {aspectosPersonales.IdEmbarazo} | Respuesta11: {(aspectosPersonales.Respuesta11 != null ? aspectosPersonales.Respuesta11.Nombre : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"Especifica: {aspectosPersonales.Especifica}");
            System.Diagnostics.Debug.WriteLine($"==================================");

            int idPersona = (int)Session["IdPersona"];
            if (idPersona != aspectosPersonales.IdPersona)
            {
                return RedirectToAction("Index");
            }

            if (datosPersonales.Estado && IsSamePeriod(datosPersonales.Fecha))
            {
                return RedirectToAction("Index", new { mensaje = 4 });
            }

            ViewBag.IdCasado = new SelectList(db.Respuesta4, "IdCasado", "Nombre", aspectosPersonales.IdCasado);
            ViewBag.IdHijo = new SelectList(db.Respuesta5, "IdHijo", "Nombre", aspectosPersonales.IdHijo);
            ViewBag.IdEnfermedad = new SelectList(db.Respuesta6, "IdEnfermedad", "Nombre", aspectosPersonales.IdEnfermedad);
            ViewBag.IdFuma = new SelectList(db.Respuesta7, "IdFuma", "Nombre", aspectosPersonales.IdFuma);
            ViewBag.IdBebida = new SelectList(db.Respuesta8, "IdBebida", "Nombre", aspectosPersonales.IdBebida);
            ViewBag.IdVidaSinSentido = new SelectList(db.Respuesta9, "IdVidaSinSentido", "Nombre", aspectosPersonales.IdVidaSinSentido);
            ViewBag.IdObservacionFamilia = new SelectList(db.ObservacioFamilias, "IdObservacionFamilia", "Nombre", aspectosPersonales.IdObservacionFamilia);
            ViewBag.IdEmbarazo = new SelectList(db.Respuesta11, "IdEmbarazo", "Nombre", aspectosPersonales.IdEmbarazo);

            ViewBag.Id = id;
            ViewBag.Aspectos = aspectosPersonales;
            ViewBag.Datos = datosPersonales;
            ViewBag.SentidoMal = SelectListHelper.SelectorNoSi(aspectosPersonales.SentidoMal);
            ViewBag.SituacionDif = SelectListHelper.SelectorNoSi(aspectosPersonales.SituacionDif);
            ViewBag.CompartirAlgo = SelectListHelper.SelectorNoSi(aspectosPersonales.CompartirAlgo);
            ViewBag.Responsabilidades = SelectListHelper.SelectorNoSi(aspectosPersonales.Responsabilidades);

            return View(aspectosPersonales);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AspectosPersonalesEdit(AspectosPersonales aspectosPersonales)
        {
            // Limpiar validaciones automáticas para manejar campos condicionales manualmente
            ModelState.Clear();

            // if (ModelState.IsValid) // Ya no es necesario checkear estricto aquí
            {
                int idPersona = (int)Session["IdPersona"];
                if (idPersona != aspectosPersonales.IdPersona)
                {
                    return RedirectToAction("Index");
                }

                var dpCheck = db.DatosPersonales.AsNoTracking().FirstOrDefault(x => x.IdPersona == aspectosPersonales.IdPersona);
                if (dpCheck != null && dpCheck.Estado && IsSamePeriod(dpCheck.Fecha))
                {
                    return RedirectToAction("Index", new { mensaje = 4 });
                }

                var aspectosActuales = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == aspectosPersonales.IdPersona);
                bool isNew = false;
                if (aspectosActuales == null)
                {
                    isNew = true;
                    aspectosActuales = new AspectosPersonales { IdPersona = aspectosPersonales.IdPersona };
                    db.AspectosPersonales.Add(aspectosActuales);
                }

                // Bloque para actualizar propiedades
                {
                    aspectosActuales.IdCasado = (aspectosPersonales.IdCasado == 0) ? 2 : aspectosPersonales.IdCasado;
                    aspectosActuales.IdHijo = (aspectosPersonales.IdHijo == 0) ? 2 : aspectosPersonales.IdHijo;
                    aspectosActuales.CantidadHijo = aspectosPersonales.CantidadHijo ?? "N/A";
                    aspectosActuales.IdEnfermedad = (aspectosPersonales.IdEnfermedad == 0) ? 2 : aspectosPersonales.IdEnfermedad;
                    aspectosActuales.Especifica = SanitizeString(aspectosPersonales.Especifica) ?? "N/A";
                    aspectosActuales.IdFuma = (aspectosPersonales.IdFuma == 0) ? 2 : aspectosPersonales.IdFuma;
                    aspectosActuales.CantidadFuma = SanitizeString(aspectosPersonales.CantidadFuma) ?? "N/A";
                    aspectosActuales.IdBebida = (aspectosPersonales.IdBebida == 0) ? 2 : aspectosPersonales.IdBebida;
                    aspectosActuales.CantidadBedida = SanitizeString(aspectosPersonales.CantidadBedida) ?? "N/A";
                    aspectosActuales.IdVidaSinSentido = (aspectosPersonales.IdVidaSinSentido == 0) ? 2 : aspectosPersonales.IdVidaSinSentido;
                    aspectosActuales.Porque = SanitizeString(aspectosPersonales.Porque) ?? "N/A";
                    aspectosActuales.IdObservacionFamilia = (aspectosPersonales.IdObservacionFamilia == 0) ? 5 : aspectosPersonales.IdObservacionFamilia;
                    aspectosActuales.ApoyoFamiliaEnProblemas = SanitizeString(aspectosPersonales.ApoyoFamiliaEnProblemas) ?? "N/A";
                    aspectosActuales.ApoyoFamiliaEnProblemasPorque = SanitizeString(aspectosPersonales.ApoyoFamiliaEnProblemasPorque) ?? "N/A";
                    aspectosActuales.ProblemasEconomicosFamilia = SanitizeString(aspectosPersonales.ProblemasEconomicosFamilia) ?? "N/A";
                    aspectosActuales.ProblemasEconomicosFamiliaPorque = SanitizeString(aspectosPersonales.ProblemasEconomicosFamiliaPorque) ?? "N/A";
                    aspectosActuales.AmbienteFamiliar = SanitizeString(aspectosPersonales.AmbienteFamiliar) ?? "N/A";
                    aspectosActuales.Responsabilidades = SanitizeString(aspectosPersonales.Responsabilidades) ?? "N/A";
                    aspectosActuales.DiaComun = SanitizeString(aspectosPersonales.DiaComun) ?? "N/A";
                    aspectosActuales.GustoEscuela = SanitizeString(aspectosPersonales.GustoEscuela) ?? "N/A";
                    aspectosActuales.SentidoUltimamente = SanitizeString(aspectosPersonales.SentidoUltimamente) ?? "N/A";
                    aspectosActuales.CompartirAlgo = aspectosPersonales.CompartirAlgo ?? "N/A";
                    aspectosActuales.IdEmbarazo = (aspectosPersonales.IdEmbarazo == 0) ? 1 : aspectosPersonales.IdEmbarazo;

                    // Campos faltantes agregados
                    aspectosActuales.SituacionDif = SanitizeString(aspectosPersonales.SituacionDif) ?? "N/A";
                    aspectosActuales.AlguienHablar = SanitizeString(aspectosPersonales.AlguienHablar) ?? "N/A";
                    aspectosActuales.Servicios = SanitizeString(aspectosPersonales.Servicios) ?? "N/A";
                    aspectosActuales.AccionesMejorar = SanitizeString(aspectosPersonales.AccionesMejorar) ?? "N/A";
                    aspectosActuales.AyudaInstitucion = SanitizeString(aspectosPersonales.AyudaInstitucion) ?? "N/A";
                    aspectosActuales.SentidoMal = SanitizeString(aspectosPersonales.SentidoMal) ?? "N/A";


                    // Ensure unlock
                    var dpUpdate = db.DatosPersonales.Find(aspectosPersonales.IdPersona);
                    if (dpUpdate != null)
                    {
                        dpUpdate.Estado = false;
                        dpUpdate.Fecha = DateTime.Now;
                        db.Entry(dpUpdate).State = EntityState.Modified;
                    }

                    if (!isNew)
                    {
                        db.Entry(aspectosActuales).State = EntityState.Modified;
                    }

                    try
                    {
                        db.SaveChanges();
                    }
                    catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine("=== DbEntityValidationException en AspectosPersonalesEdit POST ===");
                        foreach (var validationErrors in dbEx.EntityValidationErrors)
                        {
                            foreach (var validationError in validationErrors.ValidationErrors)
                            {
                                System.Diagnostics.Debug.WriteLine($"CAMPO: {validationError.PropertyName} | ERROR: {validationError.ErrorMessage}");
                            }
                        }
                        System.Diagnostics.Debug.WriteLine("===============================================================");
                        ViewBag.Mensaje = "Error de validacion al guardar. Ver consola para detalles.";
                        // Re-populate ViewBags and return view
                        ViewBag.IdCasado = new SelectList(db.Respuesta4, "IdCasado", "Nombre");
                        ViewBag.IdHijo = new SelectList(db.Respuesta5, "IdHijo", "Nombre");
                        ViewBag.IdEnfermedad = new SelectList(db.Respuesta6, "IdEnfermedad", "Nombre");
                        ViewBag.IdFuma = new SelectList(db.Respuesta7, "IdFuma", "Nombre");
                        ViewBag.IdBebida = new SelectList(db.Respuesta8, "IdBebida", "Nombre");
                        ViewBag.IdVidaSinSentido = new SelectList(db.Respuesta9, "IdVidaSinSentido", "Nombre");
                        ViewBag.IdObservacionFamilia = new SelectList(db.ObservacioFamilias, "IdObservacionFamilia", "Nombre");
                        ViewBag.IdEmbarazo = new SelectList(db.Respuesta11, "IdEmbarazo", "Nombre");

                        // Recargar Selects de Helper
                        ViewBag.SentidoMal = SelectListHelper.SelectorNoSi(aspectosPersonales.SentidoMal);
                        ViewBag.SituacionDif = SelectListHelper.SelectorNoSi(aspectosPersonales.SituacionDif);
                        ViewBag.CompartirAlgo = SelectListHelper.SelectorNoSi(aspectosPersonales.CompartirAlgo);
                        ViewBag.Responsabilidades = SelectListHelper.SelectorNoSi(aspectosPersonales.Responsabilidades);

                        return View(aspectosPersonales);
                    }
                }

                ViewBag.Mensaje = "Tus datos fueron actualizados correctamente.";
                return RedirectToAction("fin", new { id = 2 });
            }

            // LOG DE ERRORES DE MODELSTATE
            if (!ModelState.IsValid)
            {
                System.Diagnostics.Debug.WriteLine("=== ModelState Invalid en AspectosPersonalesEdit POST ===");
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0).ToList();
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"CAMPO: {error.Key}");
                    foreach (var msg in error.Value.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - ERROR: {msg.ErrorMessage}");
                        if (msg.Exception != null) System.Diagnostics.Debug.WriteLine($"  - EXCEPTION: {msg.Exception.Message}");
                    }
                }
                System.Diagnostics.Debug.WriteLine("=========================================================");

                // Mostrar en viewbag para verlo en pantalla si es posible (temporal)
                ViewBag.Mensaje = "Error de validación en formulario. Revisa los campos marcados.";
            }

            ViewBag.IdCasado = new SelectList(db.Respuesta4, "IdCasado", "Nombre");
            ViewBag.IdHijo = new SelectList(db.Respuesta5, "IdHijo", "Nombre");
            ViewBag.IdEnfermedad = new SelectList(db.Respuesta6, "IdEnfermedad", "Nombre");
            ViewBag.IdFuma = new SelectList(db.Respuesta7, "IdFuma", "Nombre");
            ViewBag.IdBebida = new SelectList(db.Respuesta8, "IdBebida", "Nombre");
            ViewBag.IdVidaSinSentido = new SelectList(db.Respuesta9, "IdVidaSinSentido", "Nombre");
            ViewBag.IdObservacionFamilia = new SelectList(db.ObservacioFamilias, "IdObservacionFamilia", "Nombre");
            ViewBag.IdEmbarazo = new SelectList(db.Respuesta11, "IdEmbarazo", "Nombre");

            ViewBag.SentidoMal = SelectListHelper.SelectorNoSi(aspectosPersonales.SentidoMal);
            ViewBag.SituacionDif = SelectListHelper.SelectorNoSi(aspectosPersonales.SituacionDif);
            ViewBag.CompartirAlgo = SelectListHelper.SelectorNoSi(aspectosPersonales.CompartirAlgo);
            ViewBag.Responsabilidades = SelectListHelper.SelectorNoSi(aspectosPersonales.Responsabilidades);

            return View(aspectosPersonales);
        }

        public ActionResult Fin(int? id)
        {
            if (id == 1)
            {
                ViewBag.Mensaje = "La entrevista se realizo con exito. Entrevista registrada correctamente";
            }
            else
            {
                ViewBag.Mensaje = "La entrevista se actualizo con exito. Entrevista actualizada correctamente";
            }
            return View();
        }

        public IEnumerable<Especialidad> getListaEspecialidades(int idCarrera)
        {
            return db.Especialidads.Where(x => x.IdCarrera == idCarrera).ToList();
        }

        public ActionResult DatosPersonales()
        {
            ViewBag.Ciudades = db.Ciudads.ToList();
            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
            Usuario usuario = Session["Usuario"] as Usuario;

            var personal = db.DatosPersonales.FirstOrDefault(x => x.Matricula == usuario.UserName);
            if (personal == null)
            {
                return View();
            }
            else
            {
                var escolar = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == personal.IdPersona);
                var economicos = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == personal.IdPersona);
                var infPersonal = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == personal.IdPersona);
                if (escolar == null)
                {
                    return RedirectToAction("AspectosAcademicos", new { id = personal.IdPersona });
                }
                else if (economicos == null)
                {
                    return RedirectToAction("AspectosEconomicos", new { id = personal.IdPersona });
                }
                else if (infPersonal == null)
                {
                    return RedirectToAction("AspectosPersonales", new { id = personal.IdPersona });
                }
                else
                {
                    return RedirectToAction("Index", new { mensaje = 3 });
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DatosPersonales(DatosPersonales datosPersonales, string submitButton)
        {
            System.Diagnostics.Debug.WriteLine($"=== VALOR DEL BOTÓN RECIBIDO: '{submitButton}' ===");

            Usuario usuario = Session["Usuario"] as Usuario;

            // Log para debug
            System.Diagnostics.Debug.WriteLine($"=== DATOS PERSONALES POST ===");
            System.Diagnostics.Debug.WriteLine($"Botón presionado: {submitButton}");
            System.Diagnostics.Debug.WriteLine($"Usuario: {usuario?.UserName}");

            // Validaciones básicas comunes
            if (string.IsNullOrEmpty(datosPersonales.Nom))
            {
                ViewBag.Mensaje = "Por favor, escriba su nombre en el campo correspondiente";
                return ReturnViewWithSelectLists(datosPersonales);
            }
            if (string.IsNullOrEmpty(datosPersonales.Paterno))
            {
                ViewBag.Mensaje = "Por favor, escriba su apellido paterno en el campo correspondiente";
                return ReturnViewWithSelectLists(datosPersonales);
            }
            if (string.IsNullOrEmpty(datosPersonales.Materno))
            {
                ViewBag.Mensaje = "Por favor, escriba su apellido materno en el campo correspondiente";
                return ReturnViewWithSelectLists(datosPersonales);
            }

            // Validación de edad
            if (datosPersonales.Edad <= 0 || datosPersonales.Edad > 100)
            {
                ViewBag.Mensaje = "Por favor ingresa una edad válida";
                return ReturnViewWithSelectLists(datosPersonales);
            }

            // Validaciones de selects
            if (datosPersonales.IdTurno == 0)
            {
                ViewBag.Mensaje = "Por favor selecciona un turno";
                return ReturnViewWithSelectLists(datosPersonales);
            }
            if (datosPersonales.IdCarrera == 0)
            {
                ViewBag.Mensaje = "Por favor selecciona una carrera";
                return ReturnViewWithSelectLists(datosPersonales);
            }
            if (datosPersonales.IdGrado == 0)
            {
                ViewBag.Mensaje = "Por favor selecciona un grado";
                return ReturnViewWithSelectLists(datosPersonales);
            }
            if (datosPersonales.IdGrupo == 0)
            {
                ViewBag.Mensaje = "Por favor selecciona un grupo";
                return ReturnViewWithSelectLists(datosPersonales);
            }

            // Preparar datos comunes
            datosPersonales.Nombre = datosPersonales.Paterno + " " + datosPersonales.Materno + " " + datosPersonales.Nom;
            datosPersonales.Email = usuario.CorreoElectronico;
            datosPersonales.Matricula = usuario.UserName;
            datosPersonales.Año = DateTime.Now.Year;
            datosPersonales.Estado = false;

            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month >= 1 && tiempo.Month <= 4) pa = 1;
            else if (tiempo.Month >= 5 && tiempo.Month <= 8) pa = 2;
            else pa = 3;
            datosPersonales.IdPeriodo = pa;

            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == datosPersonales.IdCarrera);
            datosPersonales.CarreraNom = c?.Nombre ?? "";
            datosPersonales.Area = datosPersonales.Especialidad ?? "N/A";

            if (string.IsNullOrEmpty(datosPersonales.Sexo))
                datosPersonales.Sexo = "No especificado";

            // Procesar foto
            if (!string.IsNullOrEmpty(datosPersonales.Foto))
            {
                datosPersonales.Fecha = DateTime.Now;
                System.Diagnostics.Debug.WriteLine("Foto cargada desde campo hidden (cámara)");
            }
            else if (datosPersonales.FotoFile != null &&
                     (datosPersonales.FotoFile.FileName.EndsWith(".jpg") ||
                      datosPersonales.FotoFile.FileName.EndsWith(".JPG") ||
                      datosPersonales.FotoFile.FileName.EndsWith(".jpeg") ||
                      datosPersonales.FotoFile.FileName.EndsWith(".JPEG")))
            {
                if (datosPersonales.FotoFile.ContentLength <= 20480)
                {
                    try
                    {
                        datosPersonales.Fecha = DateTime.Now;
                        System.IO.Stream fs = datosPersonales.FotoFile.InputStream;
                        System.IO.BinaryReader br = new System.IO.BinaryReader(fs);
                        Byte[] bytes = br.ReadBytes((Int32)fs.Length);
                        datosPersonales.Foto = "data:image/jpg;base64," + Convert.ToBase64String(bytes, 0, bytes.Length);
                        System.Diagnostics.Debug.WriteLine("Foto procesada desde archivo");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al procesar imagen: {ex.Message}");
                        ViewBag.Mensaje = $"Error al procesar la imagen: {ex.Message}";
                        return ReturnViewWithSelectLists(datosPersonales);
                    }
                }
                else
                {
                    ViewBag.Mensaje = "La foto debe pesar menos de 20 KB.";
                    return ReturnViewWithSelectLists(datosPersonales);
                }
            }
            else
            {
                ViewBag.Mensaje = "Por favor proporciona una foto de perfil (tómala con la cámara o sube un archivo JPG).";
                return ReturnViewWithSelectLists(datosPersonales);
            }

            datosPersonales.Fecha = DateTime.Now;

            // DECISIÓN CRÍTICA: ¿Qué botón fue presionado?
            System.Diagnostics.Debug.WriteLine($"=== DECISIÓN DE FLUJO ===");
            System.Diagnostics.Debug.WriteLine($"submitButton value: '{submitButton}'");

            if (submitButton == "GuardarContinuar")
            {
                System.Diagnostics.Debug.WriteLine("✓ FLUJO: Tramitar Baja - Guardado completo con valores por defecto");
                return EntrevistaBaja(datosPersonales);
            }
            else // submitButton == "Siguiente"
            {
                System.Diagnostics.Debug.WriteLine("✓ FLUJO: Normal - Solo guardar DatosPersonales y continuar");
                try
                {
                    db.DatosPersonales.Add(datosPersonales);
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"DatosPersonales guardado con ID: {datosPersonales.IdPersona}");

                    var personal = db.DatosPersonales.FirstOrDefault(x => x.Matricula == datosPersonales.Matricula);
                    return RedirectToAction("AspectosAcademicos", new { id = personal.IdPersona });
                }
                catch (DbEntityValidationException ex)
                {
                    string errorMessage = "Errores de validación:\n";
                    foreach (var validationErrors in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            errorMessage += $"Campo: {validationError.PropertyName}, Error: {validationError.ErrorMessage}\n";
                            System.Diagnostics.Debug.WriteLine($"Error validación: {validationError.PropertyName} - {validationError.ErrorMessage}");
                        }
                    }

                    ViewBag.Mensaje = errorMessage;
                    return ReturnViewWithSelectLists(datosPersonales);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error general: {ex.Message}");
                    ViewBag.Mensaje = "Error al guardar: " + ex.Message;
                    return ReturnViewWithSelectLists(datosPersonales);
                }
            }
        }

        private ActionResult ReturnViewWithSelectLists(DatosPersonales datosPersonales)
        {
            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
            return View(datosPersonales);
        }

        private ActionResult EntrevistaBaja(DatosPersonales datosPersonales)
        {
            System.Diagnostics.Debug.WriteLine("=== INICIO EntrevistaBaja ===");

            try
            {
                // 1. Guardar Datos Personales
                System.Diagnostics.Debug.WriteLine("Paso 1: Guardando DatosPersonales...");
                db.DatosPersonales.Add(datosPersonales);
                db.SaveChanges();
                System.Diagnostics.Debug.WriteLine($"✓ DatosPersonales guardado con IdPersona: {datosPersonales.IdPersona}");

                int idPersona = datosPersonales.IdPersona;
                Session["IdPersona"] = idPersona;

                // 2. Crear Aspectos Académicos con valores por defecto
                System.Diagnostics.Debug.WriteLine("Paso 2: Creando AspectosAcademicos con valores por defecto...");
                AspectosAcademicos aa = new AspectosAcademicos
                {
                    IdPersona = idPersona,
                    IdListaBachillerato = 1,
                    Bachillerato = SanitizeString("N/A"),
                    Especialidad = SanitizeString("N/A"),
                    Promedio = SanitizeString("N/A"),
                    MateriasDif = SanitizeString("N/A"),
                    MateriasRepro = SanitizeString("Ninguna"),
                    RendimientoClase = SanitizeString("N/A"),
                    ExperienciaProfe = SanitizeString("N/A"),
                    IdEquipoComp = 2,
                    IdTipoDispositivo = 1,
                    IdAccesoInternet = 2,
                    TiempoOrg = "N/A",
                    ApoyoAca = "N/A",
                    IdTecnicaEst = 2
                };
                db.AspectosAcademicos.Add(aa);
                System.Diagnostics.Debug.WriteLine("✓ AspectosAcademicos creado");

                // 3. Crear Aspectos Económicos con valores por defecto
                System.Diagnostics.Debug.WriteLine("Paso 3: Creando AspectosEconomicos con valores por defecto...");
                AspectosEconomicos ae = new AspectosEconomicos
                {
                    IdPersona = idPersona,
                    IdCiudad = 2,
                    Ciudad = SanitizeString("N/A"),
                    IdTrabajo = 2,
                    Trabaja = "N/A",
                    Dependiente = "0",
                    OcupacionPapa = SanitizeString("N/A"),
                    OcupacionMama = SanitizeString("N/A"),
                    CantidadHermano = SanitizeString("0"),
                    CantidadPersonas = "0",
                    CantidadTrabajan = "0",
                    IdTipoFamiliar = 2,
                    Familiar = SanitizeString("N/A"),
                    IdIngresoMes = 2,
                    IngresoM = "N/A",
                    IdSolicitarBeca = 2,
                    SolicitadoBeca = "N/A",
                    AfectacionEco = "N/A"
                };
                db.AspectosEconomicos.Add(ae);
                System.Diagnostics.Debug.WriteLine("✓ AspectosEconomicos creado");

                // 4. Crear Aspectos Personales con valores por defecto
                System.Diagnostics.Debug.WriteLine("Paso 4: Creando AspectosPersonales con valores por defecto...");
                AspectosPersonales ap = new AspectosPersonales
                {
                    IdPersona = idPersona,
                    IdCasado = 2,
                    IdHijo = 2,
                    CantidadHijo = "0",
                    IdEnfermedad = 2,
                    Especifica = SanitizeString("N/A"),
                    IdFuma = 2,
                    CantidadFuma = SanitizeString("N/A"),
                    IdBebida = 2,
                    CantidadBedida = SanitizeString("N/A"),
                    IdVidaSinSentido = 2,
                    Porque = SanitizeString("N/A"),
                    IdObservacionFamilia = 5,
                    IdEmbarazo = 1,
                    ApoyoFamiliaEnProblemas = "N/A",
                    ApoyoFamiliaEnProblemasPorque = SanitizeString("N/A"),
                    ProblemasEconomicosFamilia = "N/A",
                    ProblemasEconomicosFamiliaPorque = SanitizeString("N/A"),
                    AmbienteFamiliar = SanitizeString("N/A"),
                    Responsabilidades = SanitizeString("N/A"),
                    SituacionDif = "N/A",
                    SentidoMal = "N/A",
                    AlguienHablar = "N/A",
                    Servicios = "N/A",
                    AccionesMejorar = "N/A",
                    AyudaInstitucion = "N/A",
                    SentidoUltimamente = SanitizeString("N/A"),
                    CompartirAlgo = "N/A",
                    DiaComun = SanitizeString("N/A"),
                    GustoEscuela = SanitizeString("N/A")
                };
                db.AspectosPersonales.Add(ap);
                System.Diagnostics.Debug.WriteLine("AspectosPersonales creado");

                // 5. Guardar todos los Aspectos
                System.Diagnostics.Debug.WriteLine("Paso 5: Guardando todos los Aspectos en BD...");
                db.SaveChanges();
                System.Diagnostics.Debug.WriteLine("Todos los Aspectos guardados exitosamente");

                // 6. Crear EntrevistaInicial
                System.Diagnostics.Debug.WriteLine("Paso 6: Creando registro EntrevistaInicial...");
                EntrevistaInicial nuevaEntrevista = new EntrevistaInicial
                {
                    IdPersona = idPersona,
                    Fecha = DateTime.Now,
                    Matricula = datosPersonales.Matricula,
                    Nombre = datosPersonales.Nombre,
                    Edad = datosPersonales.Edad,
                    IdTurno = datosPersonales.IdTurno,
                    IdCarrera = datosPersonales.IdCarrera,
                    IdGrupo = datosPersonales.IdGrupo,
                    IdGrado = datosPersonales.IdGrado,
                    Celular = datosPersonales.Celular ?? "",
                    Telefono = datosPersonales.Telefono ?? "",
                    TelEmergencia = datosPersonales.TelEmergencia ?? "",
                    Email = datosPersonales.Email ?? "",
                    Sexo = datosPersonales.Sexo ?? "No especificado",
                    Foto = datosPersonales.Foto ?? "",
                    CarreraNom = datosPersonales.CarreraNom ?? "",
                    Area = datosPersonales.Area ?? "",

                    // Aspectos Académicos
                    IdListaBachillerato = aa.IdListaBachillerato,
                    Bachillerato = aa.Bachillerato,
                    Especialidad = aa.Especialidad,
                    Promedio = aa.Promedio,
                    MateriasDif = aa.MateriasDif,
                    MateriasRepro = aa.MateriasRepro,
                    RendimientoClase = aa.RendimientoClase,
                    ExperienciaProfe = aa.ExperienciaProfe,
                    IdEquipoComp = aa.IdEquipoComp,
                    IdTipoDispositivo = aa.IdTipoDispositivo,
                    IdAccesoInternet = aa.IdAccesoInternet,

                    // Aspectos Económicos
                    IdCiudad = ae.IdCiudad,
                    LugarVive = ae.Ciudad,
                    Familiar = ae.Familiar,
                    IdTrabajo = ae.IdTrabajo,
                    OcupacionPapa = ae.OcupacionPapa,
                    OcupacionMama = ae.OcupacionMama,
                    CantidadHermano = ae.CantidadHermano,
                    CantidadPersonas = ae.CantidadPersonas,
                    CantidadTrabajan = ae.CantidadTrabajan,
                    IdTipoFamiliar = ae.IdTipoFamiliar,
                    IdIngresoMes = ae.IdIngresoMes,
                    IdSolicitarBeca = ae.IdSolicitarBeca,

                    // Aspectos Personales
                    IdCasado = ap.IdCasado,
                    IdHijo = ap.IdHijo,
                    IdEnfermedad = ap.IdEnfermedad,
                    Especifica = ap.Especifica,
                    IdFuma = ap.IdFuma,
                    CantidadFuma = ap.CantidadFuma,
                    IdBebida = ap.IdBebida,
                    CantidadBedida = ap.CantidadBedida,
                    IdVidaSinSentido = ap.IdVidaSinSentido,
                    Porque = ap.Porque,
                    IdObservacionFamilia = ap.IdObservacionFamilia,
                    ApoyoFamiliaEnProblemas = ap.ApoyoFamiliaEnProblemas,
                    ApoyoFamiliaEnProblemasPorque = ap.ApoyoFamiliaEnProblemasPorque,
                    ProblemasEconomicosFamilia = ap.ProblemasEconomicosFamilia,
                    ProblemasEconomicosFamiliaPorque = ap.ProblemasEconomicosFamiliaPorque,
                    AmbienteFamiliar = ap.AmbienteFamiliar,
                    Responsabilidades = ap.Responsabilidades,
                    SentidoUltimamente = ap.SentidoUltimamente,
                    IdEmbarazo = ap.IdEmbarazo,
                    DiaComun = ap.DiaComun,
                    GustoEscuela = ap.GustoEscuela,

                    IdVulnerable = 0,
                    IdEleccionVunerabilidad = 0
                };

                db.EntrevistaInicials.Add(nuevaEntrevista);
                db.SaveChanges();
                System.Diagnostics.Debug.WriteLine("EntrevistaInicial guardada exitosamente");

                System.Diagnostics.Debug.WriteLine("=== TRAMITE DE BAJA COMPLETADO EXITOSAMENTE ===");
                return RedirectToAction("Fin", new { id = 1 });
            }
            catch (DbEntityValidationException dbEx)
            {
                System.Diagnostics.Debug.WriteLine("ERROR DE VALIDACIÓN:");
                var errorDetails = string.Join("\n", dbEx.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"• {x.PropertyName}: {x.ErrorMessage}"));

                System.Diagnostics.Debug.WriteLine(errorDetails);
                ViewBag.Mensaje = "Error de validación al guardar: " + errorDetails;
                return ReturnViewWithSelectLists(datosPersonales);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR GENERAL: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                ViewBag.Mensaje = "Error inesperado al guardar la entrevista: " + ex.Message;
                return ReturnViewWithSelectLists(datosPersonales);
            }
        }

        public ActionResult AspectosAcademicos(int? id)
        {
            if (id == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: AspectosAcademicos GET - id es null");
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            System.Diagnostics.Debug.WriteLine($"AspectosAcademicos GET - ID recibido: {id}");

            ViewBag.Id = id;
            ViewBag.IdListaBachillerato = new SelectList(db.Respuesta12, "IdListaBachillerato", "Nombre");
            ViewBag.IdEquipoComp = new SelectList(db.Respuesta13, "IdEquipoComp", "Nombre");
            ViewBag.IdTipoDispositivo = new SelectList(db.Respuesta14, "IdTipoDispositivo", "Nombre");
            ViewBag.IdAccesoInternet = new SelectList(db.Respuesta15, "IdAccesoInternet", "Nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AspectosAcademicos(AspectosAcademicos aspectosAcademicos, int id)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIO AspectosAcademicos POST ===");
                System.Diagnostics.Debug.WriteLine($"ID Persona recibido: {id}");
                System.Diagnostics.Debug.WriteLine($"IdListaBachillerato: {aspectosAcademicos.IdListaBachillerato}");
                System.Diagnostics.Debug.WriteLine($"Bachillerato: {aspectosAcademicos.Bachillerato}");
                System.Diagnostics.Debug.WriteLine($"Especialidad: {aspectosAcademicos.Especialidad}");
                System.Diagnostics.Debug.WriteLine($"Promedio: {aspectosAcademicos.Promedio}");
                System.Diagnostics.Debug.WriteLine($"IdEquipoComp: {aspectosAcademicos.IdEquipoComp}");
                System.Diagnostics.Debug.WriteLine($"IdTipoDispositivo: {aspectosAcademicos.IdTipoDispositivo}");
                System.Diagnostics.Debug.WriteLine($"IdAccesoInternet: {aspectosAcademicos.IdAccesoInternet}");

                System.Diagnostics.Debug.WriteLine($"ModelState.IsValid ANTES de limpiar: {ModelState.IsValid}");
                if (!ModelState.IsValid)
                {
                    foreach (var key in ModelState.Keys)
                    {
                        var state = ModelState[key];
                        if (state.Errors.Count > 0)
                        {
                            foreach (var error in state.Errors)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error en {key}: {error.ErrorMessage}");
                            }
                        }
                    }
                }


                ModelState.Clear();

                aspectosAcademicos.IdPersona = id;

                bool esValido = true;

                if (aspectosAcademicos.IdListaBachillerato == 0)
                {
                    ModelState.AddModelError("IdListaBachillerato", "Debes seleccionar tu bachillerato");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: IdListaBachillerato es 0");
                }

                if (aspectosAcademicos.IdListaBachillerato == 1 && string.IsNullOrWhiteSpace(aspectosAcademicos.Bachillerato))
                {
                    ModelState.AddModelError("Bachillerato", "Debes especificar el nombre del bachillerato");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: Bachillerato vacío cuando IdListaBachillerato es 1");
                }

                if (string.IsNullOrWhiteSpace(aspectosAcademicos.Especialidad))
                {
                    ModelState.AddModelError("Especialidad", "Debes especificar la especialidad del bachillerato");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: Especialidad vacía");
                }

                if (string.IsNullOrWhiteSpace(aspectosAcademicos.Promedio))
                {
                    ModelState.AddModelError("Promedio", "Debes especificar tu promedio general");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: Promedio vacío");
                }

                if (aspectosAcademicos.IdEquipoComp == 0)
                {
                    ModelState.AddModelError("IdEquipoComp", "Debes seleccionar si cuentas con equipo de cómputo");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: IdEquipoComp es 0");
                }

                if (aspectosAcademicos.IdTipoDispositivo == 0)
                {
                    ModelState.AddModelError("IdTipoDispositivo", "Debes especificar el tipo de dispositivo");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: IdTipoDispositivo es 0");
                }

                if (aspectosAcademicos.IdAccesoInternet == 0)
                {
                    ModelState.AddModelError("IdAccesoInternet", "Debes seleccionar si cuentas con acceso a internet");
                    esValido = false;
                    System.Diagnostics.Debug.WriteLine("ERROR: IdAccesoInternet es 0");
                }

                System.Diagnostics.Debug.WriteLine($"Validación manual completada. esValido: {esValido}");


                aspectosAcademicos.Bachillerato = aspectosAcademicos.Bachillerato ?? "N/A";
                aspectosAcademicos.MateriasDif = "N/A";
                aspectosAcademicos.MateriasRepro = "Ninguna";
                aspectosAcademicos.RendimientoClase = "N/A";
                aspectosAcademicos.ExperienciaProfe = "N/A";
                aspectosAcademicos.TiempoOrg = "N/A";
                aspectosAcademicos.ApoyoAca = "N/A";
                aspectosAcademicos.IdTecnicaEst = 2;

                if (esValido)
                {
                    System.Diagnostics.Debug.WriteLine("Validación exitosa. Intentando guardar en base de datos...");

                    try
                    {
                        db.AspectosAcademicos.Add(aspectosAcademicos);
                        System.Diagnostics.Debug.WriteLine("Objeto agregado al contexto");

                        db.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("SaveChanges exitoso!");

                        return RedirectToAction("AspectosEconomicos", new { id = aspectosAcademicos.IdPersona });
                    }
                    catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR DE VALIDACIÓN DE ENTITY FRAMEWORK:");
                        string errorDetails = "";
                        foreach (var validationErrors in dbEx.EntityValidationErrors)
                        {
                            foreach (var validationError in validationErrors.ValidationErrors)
                            {
                                System.Diagnostics.Debug.WriteLine($"Propiedad: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                                errorDetails += $"Campo: {validationError.PropertyName} - {validationError.ErrorMessage}\n";
                            }
                        }
                        ViewBag.Mensaje = "Errores de validación:\n" + errorDetails;
                    }
                    catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine("=== ERROR DE ACTUALIZACIÓN DE BASE DE DATOS ===");
                        System.Diagnostics.Debug.WriteLine($"Message: {dbEx.Message}");

                        // Obtener el error más interno (SQL Exception)
                        var innerException = dbEx.InnerException;
                        int level = 1;
                        while (innerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"InnerException Level {level}: {innerException.Message}");

                            // Si es SqlException, obtener detalles específicos
                            if (innerException is System.Data.SqlClient.SqlException sqlEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"SQL Error Number: {sqlEx.Number}");
                                System.Diagnostics.Debug.WriteLine($"SQL Error: {sqlEx.Message}");
                                ViewBag.Mensaje = $"Error de base de datos (SQL {sqlEx.Number}): {sqlEx.Message}";
                            }

                            innerException = innerException.InnerException;
                            level++;
                        }

                        if (ViewBag.Mensaje == null)
                        {
                            ViewBag.Mensaje = "Error al guardar en la base de datos: " + dbEx.Message;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ERROR GENERAL: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        }
                        ViewBag.Mensaje = "Error inesperado: " + ex.Message;
                    }

                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Validación FALLÓ. Mostrando errores al usuario.");
                }

                var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                if (errores.Any())
                {
                    ViewBag.Mensaje = "Por favor completa los siguientes campos:\n" + string.Join("\n", errores);
                    System.Diagnostics.Debug.WriteLine("Errores para mostrar al usuario: " + string.Join(", ", errores));
                }

                ViewBag.IdListaBachillerato = new SelectList(db.Respuesta12, "IdListaBachillerato", "Nombre", aspectosAcademicos.IdListaBachillerato);
                ViewBag.IdEquipoComp = new SelectList(db.Respuesta13, "IdEquipoComp", "Nombre", aspectosAcademicos.IdEquipoComp);
                ViewBag.IdTipoDispositivo = new SelectList(db.Respuesta14, "IdTipoDispositivo", "Nombre", aspectosAcademicos.IdTipoDispositivo);
                ViewBag.IdAccesoInternet = new SelectList(db.Respuesta15, "IdAccesoInternet", "Nombre", aspectosAcademicos.IdAccesoInternet);

                System.Diagnostics.Debug.WriteLine("=== FIN AspectosAcademicos POST (con errores) ===");
                return View(aspectosAcademicos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXCEPCIÓN GENERAL: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                ViewBag.Mensaje = "Error inesperado: " + ex.Message;

                ViewBag.IdListaBachillerato = new SelectList(db.Respuesta12, "IdListaBachillerato", "Nombre");
                ViewBag.IdEquipoComp = new SelectList(db.Respuesta13, "IdEquipoComp", "Nombre");
                ViewBag.IdTipoDispositivo = new SelectList(db.Respuesta14, "IdTipoDispositivo", "Nombre");
                ViewBag.IdAccesoInternet = new SelectList(db.Respuesta15, "IdAccesoInternet", "Nombre");

                return View(aspectosAcademicos);
            }

        }

        public ActionResult AspectosEconomicos(int? id)
        {
            ViewBag.IdCiudad = new SelectList(db.Respuesta1, "IdCiudad", "Nombre");
            ViewBag.IdTrabajo = new SelectList(db.Respuesta2, "IdTrabajo", "Nombre");
            ViewBag.IdDependientes = new SelectList(db.Respuesta3, "IdDependientes", "Nombre");
            ViewBag.IdTipoFamiliar = new SelectList(db.Respuesta16, "IdTipoFamiliar", "Nombre");
            ViewBag.IdIngresoMes = new SelectList(db.Respuesta17, "IdIngresoMes", "Nombre");
            ViewBag.IdSolicitarBeca = new SelectList(db.Respuesta18, "IdSolicitarBeca", "Nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AspectosEconomicos(AspectosEconomicos aspectosEconomicos, int id)
        {
            System.Diagnostics.Debug.WriteLine("=== ASPECTOS ECONÓMICOS POST (ENTREVISTA INICIAL) ===");
            System.Diagnostics.Debug.WriteLine($"IdPersona: {id}");

            ModelState.Clear();

            bool esValido = true;

            if (aspectosEconomicos.IdCiudad == 0)
            {
                ModelState.AddModelError("IdCiudad", "Debes seleccionar si resides en esta ciudad");
                esValido = false;
            }

            if (aspectosEconomicos.IdTrabajo == 0)
            {
                ModelState.AddModelError("IdTrabajo", "Debes seleccionar si trabajas");
                esValido = false;
            }

            if (aspectosEconomicos.IdTipoFamiliar == 0)
            {
                ModelState.AddModelError("IdTipoFamiliar", "Debes seleccionar con quién vives");
                esValido = false;
            }

            if (aspectosEconomicos.IdIngresoMes == 0)
            {
                ModelState.AddModelError("IdIngresoMes", "Debes seleccionar el rango de ingreso mensual");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.OcupacionPapa))
            {
                ModelState.AddModelError("OcupacionPapa", "Debes especificar la ocupación de tu papá");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.OcupacionMama))
            {
                ModelState.AddModelError("OcupacionMama", "Debes especificar la ocupación de tu mamá");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.CantidadHermano))
            {
                ModelState.AddModelError("CantidadHermano", "Debes especificar la cantidad de hermanos (escribe 0 si no tienes)");
                esValido = false;
            }

            if (aspectosEconomicos.IdCiudad == 2 && string.IsNullOrWhiteSpace(aspectosEconomicos.Ciudad))
            {
                ModelState.AddModelError("Ciudad", "Debes especificar en qué ciudad resides");
                esValido = false;
            }

            if (aspectosEconomicos.IdTipoFamiliar == 1 && string.IsNullOrWhiteSpace(aspectosEconomicos.Familiar))
            {
                ModelState.AddModelError("Familiar", "Debes especificar con quién vives");
                esValido = false;
            }

            if (aspectosEconomicos.IdCiudad != 2)
            {
                aspectosEconomicos.Ciudad = "N/A";
            }

            if (string.IsNullOrWhiteSpace(aspectosEconomicos.Familiar))
            {
                aspectosEconomicos.Familiar = "N/A";
            }

            aspectosEconomicos.CantidadPersonas = "N/A";
            aspectosEconomicos.CantidadTrabajan = "N/A";
            aspectosEconomicos.IdSolicitarBeca = 2;
            aspectosEconomicos.SolicitadoBeca = "N/A";
            aspectosEconomicos.AfectacionEco = "N/A";
            aspectosEconomicos.Trabaja = "N/A";
            aspectosEconomicos.Dependiente = "N/A";
            aspectosEconomicos.IngresoM = "N/A";

            System.Diagnostics.Debug.WriteLine($"Valores asignados:");
            System.Diagnostics.Debug.WriteLine($"- CantidadPersonas: {aspectosEconomicos.CantidadPersonas}");
            System.Diagnostics.Debug.WriteLine($"- CantidadTrabajan: {aspectosEconomicos.CantidadTrabajan}");
            System.Diagnostics.Debug.WriteLine($"- IdSolicitarBeca: {aspectosEconomicos.IdSolicitarBeca}");

            if (esValido)
            {
                try
                {
                    aspectosEconomicos.IdPersona = id;

                    System.Diagnostics.Debug.WriteLine("Intentando guardar AspectosEconomicos...");

                    db.AspectosEconomicos.Add(aspectosEconomicos);
                    db.SaveChanges();

                    System.Diagnostics.Debug.WriteLine("¡AspectosEconomicos guardado exitosamente!");
                    return RedirectToAction("AspectosPersonales", new { id = aspectosEconomicos.IdPersona });
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR DE VALIDACIÓN EF:");
                    string errorMessage = "Errores de validación:\n";
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"- {validationError.PropertyName}: {validationError.ErrorMessage}");
                            errorMessage += $"• {validationError.PropertyName}: {validationError.ErrorMessage}\n";
                        }
                    }
                    ViewBag.Mensaje = errorMessage;
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR DE BASE DE DATOS:");
                    System.Diagnostics.Debug.WriteLine($"Message: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"InnerException: {dbEx.InnerException.Message}");
                        if (dbEx.InnerException.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"InnerException2: {dbEx.InnerException.InnerException.Message}");
                        }
                    }
                    ViewBag.Mensaje = "Error al guardar en la base de datos. Por favor revisa los datos ingresados.";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR GENERAL: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    ViewBag.Mensaje = "Error inesperado: " + ex.Message;
                }
            }
            else
            {
                var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ViewBag.Mensaje = "Por favor completa los siguientes campos:\n" + string.Join("\n", errores);
                System.Diagnostics.Debug.WriteLine("Errores de validación: " + string.Join(", ", errores));
            }

            ViewBag.IdCiudad = new SelectList(db.Respuesta1, "IdCiudad", "Nombre", aspectosEconomicos.IdCiudad);
            ViewBag.IdTrabajo = new SelectList(db.Respuesta2, "IdTrabajo", "Nombre", aspectosEconomicos.IdTrabajo);
            ViewBag.IdTipoFamiliar = new SelectList(db.Respuesta16, "IdTipoFamiliar", "Nombre", aspectosEconomicos.IdTipoFamiliar);
            ViewBag.IdIngresoMes = new SelectList(db.Respuesta17, "IdIngresoMes", "Nombre", aspectosEconomicos.IdIngresoMes);
            ViewBag.IdSolicitarBeca = new SelectList(db.Respuesta18, "IdSolicitarBeca", "Nombre");

            return View(aspectosEconomicos);

        }

        public ActionResult AspectosPersonales(int? id)
        {
            ViewBag.IdCasado = new SelectList(db.Respuesta4, "IdCasado", "Nombre");
            ViewBag.IdHijo = new SelectList(db.Respuesta5, "IdHijo", "Nombre");
            ViewBag.IdEnfermedad = new SelectList(db.Respuesta6, "IdEnfermedad", "Nombre");
            ViewBag.IdFuma = new SelectList(db.Respuesta7, "IdFuma", "Nombre");
            ViewBag.IdBebida = new SelectList(db.Respuesta8, "IdBebida", "Nombre");
            ViewBag.IdVidaSinSentido = new SelectList(db.Respuesta9, "IdVidaSinSentido", "Nombre");
            ViewBag.IdObservacionFamilia = new SelectList(db.ObservacioFamilias, "IdObservacionFamilia", "Nombre");
            ViewBag.IdEmbarazo = new SelectList(db.Respuesta11, "IdEmbarazo", "Nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AspectosPersonales(AspectosPersonales aspectosPersonales, int id)
        {
            System.Diagnostics.Debug.WriteLine("=== ASPECTOS PERSONALES POST (ENTREVISTA INICIAL) ===");
            System.Diagnostics.Debug.WriteLine($"IdPersona: {id}");

            // Limpiar ModelState para evitar validaciones automáticas
            ModelState.Clear();

            // *** CRÍTICO: Asignar valores por defecto ANTES de validar ***
            aspectosPersonales.CantidadHijo = aspectosPersonales.CantidadHijo ?? "N/A";
            aspectosPersonales.Especifica = SanitizeString(aspectosPersonales.Especifica) ?? "N/A";
            aspectosPersonales.CantidadFuma = SanitizeString(aspectosPersonales.CantidadFuma) ?? "N/A";
            aspectosPersonales.CantidadBedida = SanitizeString(aspectosPersonales.CantidadBedida) ?? "N/A";
            aspectosPersonales.Porque = SanitizeString(aspectosPersonales.Porque) ?? "N/A";
            aspectosPersonales.AmbienteFamiliar = SanitizeString(aspectosPersonales.AmbienteFamiliar) ?? "N/A";
            aspectosPersonales.Responsabilidades = SanitizeString(aspectosPersonales.Responsabilidades) ?? "N/A";
            aspectosPersonales.SituacionDif = aspectosPersonales.SituacionDif ?? "N/A";
            aspectosPersonales.SentidoMal = aspectosPersonales.SentidoMal ?? "N/A";
            aspectosPersonales.AlguienHablar = aspectosPersonales.AlguienHablar ?? "N/A";
            aspectosPersonales.Servicios = aspectosPersonales.Servicios ?? "N/A";
            aspectosPersonales.AccionesMejorar = aspectosPersonales.AccionesMejorar ?? "N/A";
            aspectosPersonales.AyudaInstitucion = aspectosPersonales.AyudaInstitucion ?? "N/A";
            aspectosPersonales.SentidoUltimamente = SanitizeString(aspectosPersonales.SentidoUltimamente) ?? "N/A";
            aspectosPersonales.CompartirAlgo = aspectosPersonales.CompartirAlgo ?? "N/A";
            aspectosPersonales.DiaComun = SanitizeString(aspectosPersonales.DiaComun) ?? "N/A";
            aspectosPersonales.GustoEscuela = SanitizeString(aspectosPersonales.GustoEscuela) ?? "N/A";

            // Asignar IdEmbarazo por defecto (No aplica/No)
            if (aspectosPersonales.IdEmbarazo == 0)
            {
                aspectosPersonales.IdEmbarazo = 1;
            }

            bool esValido = true;

            // Validaciones obligatorias
            if (aspectosPersonales.IdCasado == 0)
            {
                ModelState.AddModelError("IdCasado", "Debes seleccionar si estás casado");
                esValido = false;
            }

            if (aspectosPersonales.IdHijo == 0)
            {
                ModelState.AddModelError("IdHijo", "Debes seleccionar si tienes hijos");
                esValido = false;
            }

            if (aspectosPersonales.IdEnfermedad == 0)
            {
                ModelState.AddModelError("IdEnfermedad", "Debes seleccionar si padeces alguna enfermedad");
                esValido = false;
            }

            if (aspectosPersonales.IdFuma == 0)
            {
                ModelState.AddModelError("IdFuma", "Debes seleccionar si fumas");
                esValido = false;
            }

            if (aspectosPersonales.IdBebida == 0)
            {
                ModelState.AddModelError("IdBebida", "Debes seleccionar si ingieres bebidas alcohólicas");
                esValido = false;
            }

            if (aspectosPersonales.IdVidaSinSentido == 0)
            {
                ModelState.AddModelError("IdVidaSinSentido", "Debes seleccionar si has pensado que la vida no tiene sentido");
                esValido = false;
            }

            if (aspectosPersonales.IdObservacionFamilia == 0)
            {
                ModelState.AddModelError("IdObservacionFamilia", "Debes seleccionar la observación sobre tu familia");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosPersonales.ApoyoFamiliaEnProblemas))
            {
                ModelState.AddModelError("ApoyoFamiliaEnProblemas", "Debes especificar si el apoyo de tu familia es adecuado");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosPersonales.ApoyoFamiliaEnProblemasPorque))
            {
                ModelState.AddModelError("ApoyoFamiliaEnProblemasPorque", "Debes especificar por qué");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosPersonales.ProblemasEconomicosFamilia))
            {
                ModelState.AddModelError("ProblemasEconomicosFamilia", "Debes especificar si los problemas económicos te afectan");
                esValido = false;
            }

            if (string.IsNullOrWhiteSpace(aspectosPersonales.ProblemasEconomicosFamiliaPorque))
            {
                ModelState.AddModelError("ProblemasEconomicosFamiliaPorque", "Debes especificar por qué");
                esValido = false;
            }

            // Validaciones condicionales
            if (aspectosPersonales.IdEnfermedad == 1 && string.IsNullOrWhiteSpace(aspectosPersonales.Especifica))
            {
                aspectosPersonales.Especifica = "No especificado";
            }

            if (aspectosPersonales.IdFuma == 1 && string.IsNullOrWhiteSpace(aspectosPersonales.CantidadFuma))
            {
                aspectosPersonales.CantidadFuma = "No especificado";
            }

            if (aspectosPersonales.IdBebida == 1 && string.IsNullOrWhiteSpace(aspectosPersonales.CantidadBedida))
            {
                aspectosPersonales.CantidadBedida = "No especificado";
            }

            if (aspectosPersonales.IdVidaSinSentido == 1 && string.IsNullOrWhiteSpace(aspectosPersonales.Porque))
            {
                aspectosPersonales.Porque = "No especificado";
            }

            System.Diagnostics.Debug.WriteLine($"Validación completada. esValido: {esValido}");

            if (esValido)
            {
                try
                {
                    aspectosPersonales.IdPersona = id;

                    System.Diagnostics.Debug.WriteLine("=== DATOS A GUARDAR ===");
                    System.Diagnostics.Debug.WriteLine($"IdPersona: {aspectosPersonales.IdPersona}");
                    System.Diagnostics.Debug.WriteLine($"IdCasado: {aspectosPersonales.IdCasado}");
                    System.Diagnostics.Debug.WriteLine($"IdHijo: {aspectosPersonales.IdHijo}");
                    System.Diagnostics.Debug.WriteLine($"CantidadHijo: {aspectosPersonales.CantidadHijo}");
                    System.Diagnostics.Debug.WriteLine($"IdEnfermedad: {aspectosPersonales.IdEnfermedad}");
                    System.Diagnostics.Debug.WriteLine($"Especifica: {aspectosPersonales.Especifica}");
                    System.Diagnostics.Debug.WriteLine($"IdFuma: {aspectosPersonales.IdFuma}");
                    System.Diagnostics.Debug.WriteLine($"CantidadFuma: {aspectosPersonales.CantidadFuma}");
                    System.Diagnostics.Debug.WriteLine($"IdBebida: {aspectosPersonales.IdBebida}");
                    System.Diagnostics.Debug.WriteLine($"CantidadBedida: {aspectosPersonales.CantidadBedida}");
                    System.Diagnostics.Debug.WriteLine($"IdVidaSinSentido: {aspectosPersonales.IdVidaSinSentido}");
                    System.Diagnostics.Debug.WriteLine($"Porque: {aspectosPersonales.Porque}");
                    System.Diagnostics.Debug.WriteLine($"IdObservacionFamilia: {aspectosPersonales.IdObservacionFamilia}");
                    System.Diagnostics.Debug.WriteLine($"IdEmbarazo: {aspectosPersonales.IdEmbarazo}");

                    System.Diagnostics.Debug.WriteLine("Guardando AspectosPersonales...");
                    db.AspectosPersonales.Add(aspectosPersonales);
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine("AspectosPersonales guardado exitosamente");

                    // Obtener datos necesarios para EntrevistaInicial
                    var datosPersonales = db.DatosPersonales.Find(id);
                    if (datosPersonales == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: No se encontraron DatosPersonales");
                        ViewBag.Mensaje = "No se encontraron los datos personales del alumno.";
                        return ReturnAspectosPersonalesView(aspectosPersonales);
                    }

                    var aspectosAcademicos = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
                    if (aspectosAcademicos == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: No se encontraron AspectosAcademicos");
                        ViewBag.Mensaje = "No se encontraron los aspectos académicos del alumno.";
                        return ReturnAspectosPersonalesView(aspectosPersonales);
                    }

                    var aspectosEconomicos = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
                    if (aspectosEconomicos == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: No se encontraron AspectosEconomicos");
                        ViewBag.Mensaje = "No se encontraron los aspectos económicos del alumno.";
                        return ReturnAspectosPersonalesView(aspectosPersonales);
                    }

                    // Crear registro de EntrevistaInicial
                    System.Diagnostics.Debug.WriteLine("Creando EntrevistaInicial...");
                    EntrevistaInicial nuevaEntrevista = new EntrevistaInicial
                    {
                        IdPersona = id,
                        Fecha = DateTime.Now,
                        Matricula = datosPersonales.Matricula,
                        Nombre = datosPersonales.Nombre,
                        Edad = datosPersonales.Edad,
                        IdTurno = datosPersonales.IdTurno,
                        IdCarrera = datosPersonales.IdCarrera,
                        IdGrupo = datosPersonales.IdGrupo,
                        IdGrado = datosPersonales.IdGrado,
                        Celular = datosPersonales.Celular ?? "",
                        Telefono = datosPersonales.Telefono ?? "",
                        TelEmergencia = datosPersonales.TelEmergencia ?? "",
                        Email = datosPersonales.Email ?? "",
                        Sexo = datosPersonales.Sexo ?? "No especificado",
                        Foto = datosPersonales.Foto ?? "",

                        // Aspectos Académicos
                        IdListaBachillerato = aspectosAcademicos.IdListaBachillerato,
                        Bachillerato = aspectosAcademicos.Bachillerato ?? "N/A",
                        Especialidad = aspectosAcademicos.Especialidad ?? "N/A",
                        Promedio = aspectosAcademicos.Promedio ?? "N/A",
                        MateriasDif = aspectosAcademicos.MateriasDif ?? "N/A",
                        MateriasRepro = aspectosAcademicos.MateriasRepro ?? "N/A",
                        RendimientoClase = aspectosAcademicos.RendimientoClase ?? "N/A",
                        ExperienciaProfe = aspectosAcademicos.ExperienciaProfe ?? "N/A",
                        IdEquipoComp = aspectosAcademicos.IdEquipoComp,
                        IdTipoDispositivo = aspectosAcademicos.IdTipoDispositivo,
                        IdAccesoInternet = aspectosAcademicos.IdAccesoInternet,

                        // Aspectos Económicos
                        IdCiudad = aspectosEconomicos.IdCiudad,
                        LugarVive = aspectosEconomicos.Ciudad ?? "N/A",
                        Familiar = aspectosEconomicos.Familiar ?? "N/A",
                        IdTrabajo = aspectosEconomicos.IdTrabajo,
                        OcupacionPapa = aspectosEconomicos.OcupacionPapa ?? "N/A",
                        OcupacionMama = aspectosEconomicos.OcupacionMama ?? "N/A",
                        CantidadHermano = aspectosEconomicos.CantidadHermano ?? "N/A",
                        CantidadPersonas = aspectosEconomicos.CantidadPersonas ?? "N/A",
                        CantidadTrabajan = aspectosEconomicos.CantidadTrabajan ?? "N/A",
                        IdTipoFamiliar = aspectosEconomicos.IdTipoFamiliar,
                        IdIngresoMes = aspectosEconomicos.IdIngresoMes,
                        IdSolicitarBeca = aspectosEconomicos.IdSolicitarBeca,

                        // Aspectos Personales
                        IdCasado = aspectosPersonales.IdCasado,
                        IdHijo = aspectosPersonales.IdHijo,
                        IdEnfermedad = aspectosPersonales.IdEnfermedad,
                        Especifica = aspectosPersonales.Especifica,
                        IdFuma = aspectosPersonales.IdFuma,
                        CantidadFuma = aspectosPersonales.CantidadFuma,
                        IdBebida = aspectosPersonales.IdBebida,
                        CantidadBedida = aspectosPersonales.CantidadBedida,
                        IdVidaSinSentido = aspectosPersonales.IdVidaSinSentido,
                        Porque = aspectosPersonales.Porque,
                        IdObservacionFamilia = aspectosPersonales.IdObservacionFamilia,
                        ApoyoFamiliaEnProblemas = aspectosPersonales.ApoyoFamiliaEnProblemas,
                        ApoyoFamiliaEnProblemasPorque = aspectosPersonales.ApoyoFamiliaEnProblemasPorque,
                        ProblemasEconomicosFamilia = aspectosPersonales.ProblemasEconomicosFamilia,
                        ProblemasEconomicosFamiliaPorque = aspectosPersonales.ProblemasEconomicosFamiliaPorque,
                        AmbienteFamiliar = aspectosPersonales.AmbienteFamiliar ?? "N/A",
                        Responsabilidades = aspectosPersonales.Responsabilidades ?? "N/A",
                        SentidoUltimamente = aspectosPersonales.SentidoUltimamente ?? "N/A",
                        IdEmbarazo = aspectosPersonales.IdEmbarazo,
                        DiaComun = aspectosPersonales.DiaComun ?? "N/A",
                        GustoEscuela = aspectosPersonales.GustoEscuela ?? "N/A",

                        IdVulnerable = 0,
                        IdEleccionVunerabilidad = 0,

                        CarreraNom = datosPersonales.CarreraNom ?? "",
                        Area = datosPersonales.Area ?? ""
                    };

                    System.Diagnostics.Debug.WriteLine("Guardando EntrevistaInicial...");
                    db.EntrevistaInicials.Add(nuevaEntrevista);
                    db.SaveChanges();

                    // =================================================================
                    // --- NUEVO CÓDIGO: CREAR SEGUIMIENTO AUTOMÁTICO INMEDIATO ---
                    // =================================================================
                    try
                    {
                        CrearSeguimientoPostEntrevista(id, db);
                    }
                    catch (Exception ex)
                    {
                        // Si falla el seguimiento, no detenemos el flujo, solo lo registramos
                        System.Diagnostics.Debug.WriteLine("Error al crear seguimiento auto: " + ex.Message);
                    }
                    // =================================================================

                    System.Diagnostics.Debug.WriteLine("EntrevistaInicial guardada exitosamente");
                    return RedirectToAction("Fin", new { id = 1 });
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR DE VALIDACIÓN EF:");
                    string errorMessage = "Errores de validación:\n";
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Entidad: {validationErrors.Entry.Entity.GetType().Name}");
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            System.Diagnostics.Debug.WriteLine($"- {validationError.PropertyName}: {validationError.ErrorMessage}");
                            errorMessage += $"• {validationError.PropertyName}: {validationError.ErrorMessage}\n";
                        }
                    }
                    ViewBag.Mensaje = errorMessage;
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
                {
                    System.Diagnostics.Debug.WriteLine("=== ERROR DE BASE DE DATOS ===");
                    System.Diagnostics.Debug.WriteLine($"Message: {dbEx.Message}");

                    var innerEx = dbEx.InnerException;
                    int level = 1;
                    while (innerEx != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"InnerException Level {level}: {innerEx.Message}");
                        innerEx = innerEx.InnerException;
                        level++;
                    }

                    ViewBag.Mensaje = "Error de base de datos: " + dbEx.Message +
                        (dbEx.InnerException != null ? "\nDetalle: " + dbEx.InnerException.Message : "");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR GENERAL: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    ViewBag.Mensaje = "Error inesperado: " + ex.Message;
                }
            }
            else
            {
                var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ViewBag.Mensaje = "Por favor completa los siguientes campos:\n" + string.Join("\n", errores);
                System.Diagnostics.Debug.WriteLine("Errores de validación: " + string.Join(", ", errores));
            }

            return ReturnAspectosPersonalesView(aspectosPersonales);
        }

        // Método auxiliar mejorado para evitar repetir código
        private ActionResult ReturnAspectosPersonalesView(AspectosPersonales aspectosPersonales = null)
        {
            ViewBag.IdCasado = new SelectList(db.Respuesta4, "IdCasado", "Nombre", aspectosPersonales?.IdCasado);
            ViewBag.IdHijo = new SelectList(db.Respuesta5, "IdHijo", "Nombre", aspectosPersonales?.IdHijo);
            ViewBag.IdEnfermedad = new SelectList(db.Respuesta6, "IdEnfermedad", "Nombre", aspectosPersonales?.IdEnfermedad);
            ViewBag.IdFuma = new SelectList(db.Respuesta7, "IdFuma", "Nombre", aspectosPersonales?.IdFuma);
            ViewBag.IdBebida = new SelectList(db.Respuesta8, "IdBebida", "Nombre", aspectosPersonales?.IdBebida);
            ViewBag.IdVidaSinSentido = new SelectList(db.Respuesta9, "IdVidaSinSentido", "Nombre", aspectosPersonales?.IdVidaSinSentido);
            ViewBag.IdObservacionFamilia = new SelectList(db.ObservacioFamilias, "IdObservacionFamilia", "Nombre", aspectosPersonales?.IdObservacionFamilia);
            ViewBag.IdEmbarazo = new SelectList(db.Respuesta11, "IdEmbarazo", "Nombre", aspectosPersonales?.IdEmbarazo);

            return View(aspectosPersonales ?? new AspectosPersonales());
        }

        // -------------------------------------------------------------------------
        // MÉTODO NUEVO: Genera la hoja y el seguimiento "Ya realizó su primera entrevista"
        // -------------------------------------------------------------------------
        private void CrearSeguimientoPostEntrevista(int idPersona, ModeloPlataforma context)
        {
            var dp = context.DatosPersonales.Find(idPersona);
            if (dp == null) return;

            // 1. Calcular datos para la Hoja (Individual)
            var t = context.Turnoes.FirstOrDefault(a => a.IdTurno == dp.IdTurno);
            var c = context.Carreras.FirstOrDefault(a => a.IdCarrera == dp.IdCarrera);
            var grado = context.Gradoes.FirstOrDefault(a => a.IdGrado == dp.IdGrado);
            var grup = context.Grupoes.FirstOrDefault(a => a.IdGrupo == dp.IdGrupo);

            // Generar string del grupo (Ej: TI1A)
            string pref = t?.Nombre == "Matutino" ? "M" : t?.Nombre == "Vespertino" ? "I" : "D";
            // Nota: Ajusta esta línea según tu lógica de nomenclatura preferida, aquí uso la estándar
            string grupoStr = $"{c?.Nomenclatura}{grado?.Nombre}{grup?.Nombre}";

            // Calcular Cuatrimestre
            var mes = DateTime.Now.Month;
            string cuatriStr;
            if (mes <= 4) cuatriStr = context.Periodos.FirstOrDefault(x => x.IdPeriodo == 1)?.Nombre;
            else if (mes <= 8) cuatriStr = context.Periodos.FirstOrDefault(x => x.IdPeriodo == 2)?.Nombre;
            else cuatriStr = context.Periodos.FirstOrDefault(x => x.IdPeriodo == 3)?.Nombre;

            int anio = DateTime.Now.Year;

            // 2. Buscar si YA existe una hoja para este cuatrimestre
            var individual = context.Individuals.FirstOrDefault(x =>
                x.IdPersona == idPersona &&
                x.Grupo == grupoStr &&
                x.Cuatrimestre == cuatriStr &&
                x.Fecha.Year == anio);

            // Si NO existe la hoja, la creamos
            if (individual == null)
            {
                individual = new Individual
                {
                    IdPersona = idPersona,
                    Fecha = DateTime.Now,
                    Nombre = dp.Nombre,
                    Matricula = dp.Matricula,
                    Grupo = grupoStr,
                    Cuatrimestre = cuatriStr,
                    Especialidad = dp.Especialidad ?? "",
                    Area = dp.Area
                };

                // Lógica de carrera/area (copiada de tus otros controladores)
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

                context.Individuals.Add(individual);
                context.SaveChanges(); // Guardamos para generar el IdIndividual
            }

            // 3. Crear el Seguimiento "Ya realizó su primera entrevista"
            // Verificamos si ya existe este mensaje específico para no duplicarlo si el alumno recarga la página
            bool existeSeguimiento = context.Seguimientoes.Any(s =>
                s.IdIndividual == individual.IdIndividual &&
                s.Problematica == "Ya realizó su primera entrevista");

            if (!existeSeguimiento)
            {
                var seg = new Seguimiento
                {
                    IdIndividual = individual.IdIndividual,
                    Fecha = DateTime.Now,
                    Vulnerabilidad = "No vulnerable", // Valor por defecto
                    Problematica = "Ya realizó su primera entrevista",
                    Accion = "Se valida entrevista inicial en el sistema."
                };

                context.Seguimientoes.Add(seg);
                context.SaveChanges();
            }
        }
    }
}