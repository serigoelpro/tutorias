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
    public class AsignarAsesoresMasterController : Controller
    {
        private TutoriasContext tutoriasDb = new TutoriasContext();
        private GestionUsuariosContext usuariosDb = new GestionUsuariosContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = Session["Usuario"] as Usuario;

            // ❌ Bloquear si es estudiante (1) o tutor (2)
            if (usuario == null || usuario.IdNivel == 1 || usuario.IdNivel == 2)
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // GET: AsignarAsesores - Muestra tutores ya asignados
        public ActionResult Index(int? idCarrera)
        {
            try
            {
                var user = Session["Usuario"] as Usuario;

                // Validación adicional de usuario
                if (user == null)
                {
                    ViewBag.Error = "Sesión expirada. Por favor, inicie sesión nuevamente.";
                    ViewBag.UserNivel = 0; // Valor por defecto cuando no hay usuario
                    return View(new List<Usuario>());
                }

                // Base: todos los tutores (con o sin OID)
                var tutoresQuery = tutoriasDb.Usuarios.Where(x => x.IdNivel == 2);

                // Lógica de filtrado basada en el nivel del usuario
                if (user.IdNivel == 3)
                {
                    // Coordinador: ver solo su carrera y únicamente usuarios con OID asignado
                    tutoresQuery = tutoresQuery.Where(x => x.IdCarrera == user.IdCarrera &&
                                                           x.MicrosoftIdentifier != null &&
                                                           x.MicrosoftIdentifier != "");
                }
                else if (user.IdNivel >= 4)
                {
                    // Master: puede ver todos o filtrar si se pasa idCarrera
                    if (idCarrera.HasValue && idCarrera.Value > 0)
                    {
                        tutoresQuery = tutoresQuery.Where(x => x.IdCarrera == idCarrera.Value);
                    }
                }
                else if (user.IdNivel == 0)
                {
                    // Usuario no autenticado o con sesión expirada - no mostrar tutores
                    tutoresQuery = tutoresQuery.Where(x => false); // Query vacío
                }
                // Para otros niveles (1, 2), no aplicar filtros adicionales

                var tutores = tutoresQuery
                    .AsNoTracking()
                    .ToList()
                    .Select(usu => new Usuario
                    {
                        IdUsuario = usu.IdUsuario,
                        NombreCompleto = usu.NombreCompleto,
                        UserName = usu.UserName,
                        Password = "", // No desencriptar contraseñas para la vista
                        IdCarrera = usu.IdCarrera,
                        IdNivel = usu.IdNivel,
                        Estado = usu.Estado,
                        Tiempo = usu.Tiempo,
                        CorreoElectronico = usu.CorreoElectronico,
                        MicrosoftIdentifier = usu.MicrosoftIdentifier
                    })
                    .ToList();

                // Diccionario de carreras para mostrar nombres (nivel 3 y 4)
                ViewBag.Carreras = tutoriasDb.Carreras
                    .AsNoTracking()
                    .ToDictionary(c => c.IdCarrera, c => c.Nombre);

                // El selector de carreras SOLO para nivel >= 4
                if (user.IdNivel >= 4)
                {
                    ViewBag.CarrerasSelectList = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", idCarrera);
                    ViewBag.CarreraSeleccionada = idCarrera;
                }

                // Para que la vista pueda decidir qué mostrar
                ViewBag.UserNivel = user.IdNivel;
                ViewBag.UserCarrera = user.IdCarrera;

                return View(tutores);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los tutores: " + ex.Message;
                ViewBag.UserNivel = 0; // Valor por defecto en caso de error
                return View(new List<Usuario>());
            }
        }

        // GET: Mostrar docentes disponibles para asignar como tutores
        public ActionResult AsignarTutor()
        {
            try
            {
                Usuario user = Session["Usuario"] as Usuario;

                // ✅ FILTRO: Solo docentes habilitados, autorizados Y con OID asignado
                var docentesQuery = usuariosDb.Docentes.Where(d =>
                    d.Habilitado == true &&
                    d.Autorizado == true &&
                    d.MicrosoftIdentifier != null &&
                    d.MicrosoftIdentifier.Trim() != "");

                // Mostrar todos los docentes (incluso los que ya son tutores, para permitir asignación a múltiples carreras)
                var docentes = docentesQuery.ToList();

                // Obtener información de carreras ya asignadas para cada docente
                var carrerasDict = tutoriasDb.Carreras
                    .AsNoTracking()
                    .ToDictionary(c => c.IdCarrera, c => c.Nombre);

                // Crear diccionario de carreras asignadas por OID del docente
                var carrerasPorDocente = new Dictionary<string, List<string>>();
                foreach (var docente in docentes)
                {
                    // Primero obtener los datos de la base de datos (solo IdCarrera)
                    var carrerasAsignadasIds = tutoriasDb.Usuarios
                        .Where(u => u.MicrosoftIdentifier == docente.MicrosoftIdentifier &&
                                    u.IdNivel == 2)
                        .Select(u => u.IdCarrera)
                        .Distinct()
                        .ToList();

                    // Luego transformar en memoria usando el diccionario
                    var carrerasAsignadas = carrerasAsignadasIds
                        .Select(idCarrera => carrerasDict.ContainsKey(idCarrera) ? carrerasDict[idCarrera] : "Sin nombre")
                        .ToList();

                    carrerasPorDocente[docente.MicrosoftIdentifier] = carrerasAsignadas;
                }

                ViewBag.CarrerasPorDocente = carrerasPorDocente;
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre");
                ViewBag.UserNivel = user?.IdNivel ?? 0;
                ViewBag.UserCarrera = user?.IdCarrera ?? 0;

                return View(docentes);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los docentes: " + ex.Message;
                return View(new List<Docente>());
            }
        }

        // POST: Asignar docente como tutor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarTutor(int idDocente, int? idCarrera)
        {
            try
            {
                var docente = usuariosDb.Docentes.Find(idDocente);
                if (docente == null)
                {
                    TempData["Error"] = "Docente no encontrado.";
                    return RedirectToAction("AsignarTutor");
                }

                // ✅ VALIDACIÓN: Verificar que el docente tenga OID
                if (string.IsNullOrEmpty(docente.MicrosoftIdentifier) ||
                   string.IsNullOrWhiteSpace(docente.MicrosoftIdentifier))
                {
                    TempData["Error"] = "El docente seleccionado no tiene un Microsoft Identifier (OID) asignado.";
                    return RedirectToAction("AsignarTutor");
                }

                Usuario user = Session["Usuario"] as Usuario;
                if (user == null)
                {
                    TempData["Error"] = "Sesión expirada. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("AsignarTutor");
                }

                // Determinar la carrera a asignar
                int carreraAsignar;
                if (user.IdNivel >= 4 && idCarrera.HasValue && idCarrera.Value > 0)
                {
                    // Usuario Master puede seleccionar carrera
                    carreraAsignar = idCarrera.Value;
                }
                else
                {
                    // Coordinador o menor nivel: usar su propia carrera
                    carreraAsignar = user.IdCarrera;
                }

                // Verificar si ya existe un usuario con este Microsoft Identifier Y esta carrera específica
                var usuarioExistenteMismaCarrera = tutoriasDb.Usuarios.FirstOrDefault(u =>
                    u.MicrosoftIdentifier == docente.MicrosoftIdentifier &&
                    u.IdCarrera == carreraAsignar &&
                    u.IdNivel == 2);

                if (usuarioExistenteMismaCarrera != null)
                {
                    var carreraNombre = tutoriasDb.Carreras.Find(carreraAsignar)?.Nombre ?? "carrera desconocida";
                    TempData["Error"] = $"El docente {docente.Nombre} ya está asignado como tutor en la carrera {carreraNombre}.";
                    return RedirectToAction("AsignarTutor");
                }

                // Verificar si existe algún usuario con este OID (para actualizar información si es necesario)
                var usuarioExistenteOID = tutoriasDb.Usuarios.FirstOrDefault(u =>
                    u.MicrosoftIdentifier == docente.MicrosoftIdentifier);

                // Crear nuevo registro de Usuario para esta carrera específica
                var nuevoUsuario = new Usuario
                {
                    NombreCompleto = docente.Nombre,
                    UserName = docente.NumeroEmpleado,
                    Password = Security.Encripta("123456"), // Password temporal
                    CorreoElectronico = docente.CorreoElectronico,
                    MicrosoftIdentifier = docente.MicrosoftIdentifier,
                    IdCarrera = carreraAsignar,
                    IdNivel = 2,
                    Estado = true,
                    Tiempo = DateTime.Now
                };

                tutoriasDb.Usuarios.Add(nuevoUsuario);
                tutoriasDb.SaveChanges();

                var carreraNombreAsignada = tutoriasDb.Carreras.Find(carreraAsignar)?.Nombre ?? "carrera";
                TempData["Mensaje"] = $"Docente {docente.Nombre} asignado como tutor en la carrera {carreraNombreAsignada} exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al asignar tutor: {ex.Message}");
                TempData["Error"] = "Error al asignar tutor: " + ex.Message;
                return RedirectToAction("AsignarTutor");
            }
        }

        public ActionResult Crear()
        {
            Usuario user = Session["Usuario"] as Usuario;

            // Para coordinadores y masters (nivel >= 3)
            if (user.IdNivel >= 3)
            {
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Usuario usuario)
        {
            try
            {
                if (string.IsNullOrEmpty(usuario.NombreCompleto) ||
                   string.IsNullOrEmpty(usuario.UserName) ||
                   string.IsNullOrEmpty(usuario.Password))
                {
                    ViewBag.Mensaje = "Favor de llenar todos los campos obligatorios.";
                    return View(usuario);
                }

                if (ModelState.IsValid)
                {
                    var users = tutoriasDb.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName && x.IdNivel == 2);
                    var users2 = tutoriasDb.Usuarios.FirstOrDefault(x => x.NombreCompleto == usuario.NombreCompleto && x.IdNivel == 2);
                    var users3 = tutoriasDb.Usuarios.FirstOrDefault(x => x.CorreoElectronico == usuario.CorreoElectronico && x.IdNivel == 2);

                    if (users == null && users2 == null && users3 == null)
                    {
                        Usuario user = Session["Usuario"] as Usuario;

                        // Para coordinadores y masters (nivel >= 3), pueden seleccionar carrera
                        // Si no seleccionaron carrera, usar la del usuario actual
                        if (user.IdNivel >= 3)
                        {
                            if (usuario.IdCarrera == 0 || usuario.IdCarrera == null)
                            {
                                usuario.IdCarrera = user.IdCarrera;
                            }
                        }
                        else
                        {
                            usuario.IdCarrera = user.IdCarrera;
                        }

                        usuario.IdNivel = 2;
                        usuario.Estado = true;
                        usuario.Tiempo = DateTime.Now;
                        usuario.Password = Security.Encripta(usuario.Password);
                        tutoriasDb.Usuarios.Add(usuario);
                        tutoriasDb.SaveChanges();
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.Mensaje = "El nombre completo, usuario o correo electrónico ya está registrado. Favor de intentar con otros datos.";
                        return View(usuario);
                    }
                }
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al crear el tutor: " + ex.Message;
                return View(usuario);
            }
        }

        // GET: AsignarAsesores/EditarAsesor
        public ActionResult EditarAsesor(int? id)
        {
            try
            {
                if (id == null)
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

                var usuario = tutoriasDb.Usuarios.Find(id);
                if (usuario == null)
                    return HttpNotFound();

                // Usuario actual (para nivel en vista)
                var user = Session["Usuario"] as Usuario;
                ViewBag.UserNivel = user.IdNivel;
                ViewBag.UserCarrera = user.IdCarrera;

                var usuarioVista = new Usuario
                {
                    IdUsuario = usuario.IdUsuario,
                    NombreCompleto = usuario.NombreCompleto,
                    UserName = usuario.UserName,
                    Password = Security.Desencripta(usuario.Password), // Desencriptar contraseña para mostrar en la vista
                    IdCarrera = usuario.IdCarrera,
                    IdNivel = usuario.IdNivel,
                    Estado = usuario.Estado,
                    Tiempo = usuario.Tiempo,
                    CorreoElectronico = usuario.CorreoElectronico,
                    MicrosoftIdentifier = usuario.MicrosoftIdentifier
                };

                ViewBag.Carreras = new SelectList(
                    tutoriasDb.Carreras.AsNoTracking().ToList(), "IdCarrera", "Nombre", usuarioVista.IdCarrera);

                return View(usuarioVista);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar el tutor: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: AsignarAsesores/EditarAsesor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarAsesor(Usuario usuario)
        {
            try
            {
                var userSession = Session["Usuario"] as Usuario;
                ViewBag.UserNivel = userSession != null ? userSession.IdNivel : 0;
                ViewBag.UserCarrera = userSession != null ? userSession.IdCarrera : 0;
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", usuario.IdCarrera);

                if (string.IsNullOrEmpty(usuario.NombreCompleto) ||
                    string.IsNullOrEmpty(usuario.UserName) ||
                    string.IsNullOrEmpty(usuario.Password))
                {
                    ViewBag.Mensaje = "Favor de llenar todos los campos obligatorios.";
                    return View(usuario);
                }

                if (ModelState.IsValid)
                {
                    var usuarioOriginal = tutoriasDb.Usuarios.Find(usuario.IdUsuario);

                    if (usuarioOriginal == null)
                    {
                        ViewBag.Mensaje = "No se encontró el tutor a actualizar.";
                        return View(usuario);
                    }

                    // Actualización de datos
                    usuarioOriginal.NombreCompleto = usuario.NombreCompleto;
                    usuarioOriginal.UserName = usuario.UserName;
                    usuarioOriginal.CorreoElectronico = usuario.CorreoElectronico;
                    usuarioOriginal.Password = Security.Encripta(usuario.Password);

                    if (userSession.IdNivel >= 4)
                    {
                        usuarioOriginal.IdCarrera = (usuario.IdCarrera != 0 ? usuario.IdCarrera : usuarioOriginal.IdCarrera);
                    }
                    else
                    {
                        usuarioOriginal.IdCarrera = userSession.IdCarrera;
                    }

                    usuarioOriginal.IdNivel = 2;
                    usuarioOriginal.Estado = true;
                    usuarioOriginal.Tiempo = DateTime.Now;

                    tutoriasDb.Entry(usuarioOriginal).State = EntityState.Modified;
                    tutoriasDb.SaveChanges();

                    TempData["Mensaje"] = "Tutor actualizado correctamente.";

                    return RedirectToAction("EditarAsesor", new { id = usuario.IdUsuario });
                }

                return View(usuario);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al actualizar el tutor: " + ex.Message;
                return View(usuario);
            }
        }

        public ActionResult EliminarAsesor(int? id)
        {
            try
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                Usuario asesor = tutoriasDb.Usuarios.Find(id);
                if (asesor == null)
                {
                    return HttpNotFound();
                }

                // Crear una copia para la vista
                var asesorVista = new Usuario
                {
                    IdUsuario = asesor.IdUsuario,
                    NombreCompleto = asesor.NombreCompleto,
                    UserName = asesor.UserName,
                    Password = "", // No desencriptar contraseñas para la vista
                    IdCarrera = asesor.IdCarrera,
                    IdNivel = asesor.IdNivel,
                    Estado = asesor.Estado,
                    Tiempo = asesor.Tiempo,
                    CorreoElectronico = asesor.CorreoElectronico,
                    MicrosoftIdentifier = asesor.MicrosoftIdentifier
                };

                return View(asesorVista);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar el tutor: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarAsesor(int id)
        {
            try
            {
                Usuario asesor = tutoriasDb.Usuarios.Find(id);
                if (asesor != null)
                {
                    // En lugar de eliminar, cambiar el nivel para que ya no sea tutor
                    asesor.IdNivel = 1; // Cambiar a nivel básico
                    tutoriasDb.Entry(asesor).State = EntityState.Modified;
                    tutoriasDb.SaveChanges();
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al eliminar el tutor: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        public ActionResult Asesorados(int? id)
        {
            try
            {
                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == id);

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

                var grupos = tutoriasDb.TutoriaGrupals.Where(x => x.IdUsuario == id).ToList();

                // Pasar años en ViewData para evitar problemas de encoding
                foreach (var grupo in grupos)
                {
                    ViewData["Ano_" + grupo.IdTutoriaGrupal] = grupo.Año;
                }

                return View(grupos);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los grupos: " + ex.Message;
                return View(new List<TutoriaGrupal>());
            }
        }

        public ActionResult Asignar(int? id)
        {
            if (id == null)
            {
                TempData["Error"] = "No se ha especificado un tutor para asignar.";
                return RedirectToAction("Index");
            }

            try
            {
                var tutor = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == id);

                if (tutor == null)
                {
                    TempData["Error"] = "El tutor especificado (ID: " + id + ") no fue encontrado.";
                    return RedirectToAction("Index");
                }

                ViewBag.Usuario = tutor;

                ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre");
                ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre");

                var usuario = Session["Usuario"] as Plataforma_Web.Models.Usuario;
                var userNivel = usuario?.IdNivel ?? 0;

                var especialidadesLista = new List<PlataformaWeb.BecasTransporte.Models.Especialidad>();

                if (userNivel == 3 && usuario.IdCarrera != 0)
                {
                    especialidadesLista = tutoriasDb.Especialidads
                                              .Where(e => e.IdCarrera == usuario.IdCarrera)
                                              .OrderBy(e => e.Nombre)
                                              .ToList();
                }

                ViewBag.IdEspecialidad = new SelectList(especialidadesLista, "Id", "Nombre");

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los datos: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Asignar(TutoriaGrupal tutoriaGrupal, int tutorId)
        {
            var usuarioSesion = Session["Usuario"] as Plataforma_Web.Models.Usuario;
            var userNivel = usuarioSesion?.IdNivel ?? 0;

            try
            {
                if (tutoriaGrupal.Año <= 0)
                {
                    tutoriaGrupal.Año = DateTime.Now.Year;
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Mensaje = "Faltan campos obligatorios. Revise el formulario.";
                    goto RecargarVista;
                }

                var tiempo = DateTime.Now;
                if (tiempo.Month >= 1 && tiempo.Month <= 4)
                {
                    tutoriaGrupal.IdPeriodo = 1;
                }
                else if (tiempo.Month >= 5 && tiempo.Month <= 8)
                {
                    tutoriaGrupal.IdPeriodo = 2;
                }
                else
                {
                    tutoriaGrupal.IdPeriodo = 3;
                }

                tutoriaGrupal.IdUsuario = tutorId;

                if (userNivel < 4)
                {
                    tutoriaGrupal.IdCarrera = usuarioSesion.IdCarrera;
                }

                var grupo = tutoriasDb.TutoriaGrupals.FirstOrDefault(x => x.IdGrado == tutoriaGrupal.IdGrado &&
                    x.IdGrupo == tutoriaGrupal.IdGrupo && x.IdCarrera == tutoriaGrupal.IdCarrera &&
                    x.IdTurno == tutoriaGrupal.IdTurno && x.IdPeriodo == tutoriaGrupal.IdPeriodo &&
                    x.Año == tutoriaGrupal.Año && x.IdEspecialidad == tutoriaGrupal.IdEspecialidad);

                if (grupo == null)
                {
                    tutoriasDb.TutoriaGrupals.Add(tutoriaGrupal);
                    tutoriasDb.SaveChanges();
                    return RedirectToAction("Asesorados", new { id = tutoriaGrupal.IdUsuario });
                }
                else
                {
                    ViewBag.Mensaje = "El grupo que intenta crear ya está asignado a un tutor.";
                }

            RecargarVista:
                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == tutorId);
                ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre", tutoriaGrupal.IdGrado);
                ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre", tutoriaGrupal.IdGrupo);
                ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre", tutoriaGrupal.IdCarrera);
                ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre", tutoriaGrupal.IdTurno);

                var idCarreraParaRecargar = (userNivel >= 4) ? tutoriaGrupal.IdCarrera : usuarioSesion.IdCarrera;
                var especialidadesLista = tutoriasDb.Especialidads
                                              .Where(e => e.IdCarrera == idCarreraParaRecargar)
                                              .OrderBy(e => e.Nombre)
                                              .ToList();
                ViewBag.IdEspecialidad = new SelectList(especialidadesLista, "Id", "Nombre", tutoriaGrupal.IdEspecialidad);

                return View(tutoriaGrupal);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al asignar el grupo: " + ex.Message;

                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == tutorId);
                ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre", tutoriaGrupal.IdGrado);
                ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre", tutoriaGrupal.IdGrupo);
                ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre", tutoriaGrupal.IdCarrera);
                ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre", tutoriaGrupal.IdTurno);

                var idCarreraParaRecargar = (userNivel >= 4) ? tutoriaGrupal.IdCarrera : usuarioSesion.IdCarrera;
                var especialidadesLista = new List<PlataformaWeb.BecasTransporte.Models.Especialidad>();
                if (idCarreraParaRecargar > 0)
                {
                    especialidadesLista = tutoriasDb.Especialidads
                                              .Where(e => e.IdCarrera == idCarreraParaRecargar)
                                              .OrderBy(e => e.Nombre)
                                              .ToList();
                }
                ViewBag.IdEspecialidad = new SelectList(especialidadesLista, "Id", "Nombre", tutoriaGrupal.IdEspecialidad);

                return View(tutoriaGrupal);
            }
        }

        public ActionResult Eliminar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TutoriaGrupal tutoriaGrupal = tutoriasDb.TutoriaGrupals.Find(id);
            if (tutoriaGrupal == null)
            {
                return HttpNotFound();
            }
            return View(tutoriaGrupal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            TutoriaGrupal tutoriaGrupal = tutoriasDb.TutoriaGrupals.Find(id);
            tutoriasDb.TutoriaGrupals.Remove(tutoriaGrupal);
            tutoriasDb.SaveChanges();
            return RedirectToAction("Index");
        }

        public ActionResult Editar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TutoriaGrupal tutoriaGrupal = tutoriasDb.TutoriaGrupals.Find(id);
            if (tutoriaGrupal == null)
            {
                return HttpNotFound();
            }
            ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == tutoriaGrupal.IdUsuario);
            ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre", tutoriaGrupal.IdGrado);
            ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre", tutoriaGrupal.IdGrupo);
            ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre", tutoriaGrupal.IdCarrera);
            ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre", tutoriaGrupal.IdTurno);
            ViewBag.IdEspecialidad = new SelectList(tutoriasDb.Especialidads.Where(e => e.IdCarrera == tutoriaGrupal.IdCarrera).OrderBy(e => e.Nombre), "Id", "Nombre", tutoriaGrupal.IdEspecialidad);
            return View(tutoriaGrupal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(TutoriaGrupal tutoriaGrupal)
        {
            if (ModelState.IsValid)
            {
                tutoriasDb.Entry(tutoriaGrupal).State = EntityState.Modified;
                tutoriasDb.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre", tutoriaGrupal.IdGrado);
            ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre", tutoriaGrupal.IdGrupo);
            ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre", tutoriaGrupal.IdCarrera);
            ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre", tutoriaGrupal.IdTurno);
            ViewBag.IdEspecialidad = new SelectList(tutoriasDb.Especialidads.Where(e => e.IdCarrera == tutoriaGrupal.IdCarrera).OrderBy(e => e.Nombre), "Id", "Nombre", tutoriaGrupal.IdEspecialidad);
            return View(tutoriaGrupal);
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

        public JsonResult GetEspecialidadesPorCarrera(int idCarrera)
        {
            var especialidades = tutoriasDb.Especialidads
                .Where(e => e.IdCarrera == idCarrera)
                .Select(e => new {
                    Id = e.Id,
                    Nombre = e.Nombre
                })
                .OrderBy(e => e.Nombre)
                .ToList();

            return Json(especialidades, JsonRequestBehavior.AllowGet);
        }
    }
}