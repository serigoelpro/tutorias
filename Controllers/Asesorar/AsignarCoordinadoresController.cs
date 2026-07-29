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
    public class AsignarCoordinadoresMasterController : Controller
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

        // GET: AsignarCoordinadores - Muestra coordinadores ya asignados
        public ActionResult Index(int? idCarrera)
        {
            try
            {
                Usuario user = Session["Usuario"] as Usuario;
                List<Usuario> coordinadoresAsignados = new List<Usuario>();

                // Obtener usuarios con nivel 3 (coordinadores)
                var coordinadoresQuery = tutoriasDb.Usuarios.Where(x => x.IdNivel == 3);

                // Aplicar filtro por carrera para coordinadores y masters (nivel >= 3)
                if (user.IdNivel >= 3)
                {
                    if (idCarrera.HasValue && idCarrera.Value > 0)
                    {
                        // Filtrar por carrera específica seleccionada
                        coordinadoresQuery = coordinadoresQuery.Where(x => x.IdCarrera == idCarrera.Value);
                    }
                    // Si no hay filtro específico, mostrar todos los coordinadores
                }

                var coordinadores = coordinadoresQuery.ToList();

                foreach (Usuario usu in coordinadores)
                {
                    // Crear una copia para no modificar el objeto original
                    var coordCopia = new Usuario
                    {
                        IdUsuario = usu.IdUsuario,
                        NombreCompleto = usu.NombreCompleto,
                        UserName = usu.UserName,
                        Password = Security.Desencripta(usu.Password ?? ""),
                        IdCarrera = usu.IdCarrera,
                        IdNivel = usu.IdNivel,
                        Estado = usu.Estado,
                        Tiempo = usu.Tiempo,
                        CorreoElectronico = usu.CorreoElectronico,
                        MicrosoftIdentifier = usu.MicrosoftIdentifier
                    };
                    coordinadoresAsignados.Add(coordCopia);
                }

                // Para usuarios nivel 3 y 4, pasar las carreras para el filtro
                if (user.IdNivel >= 3)
                {
                    ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", idCarrera);
                    ViewBag.CarreraSeleccionada = idCarrera;
                }

                return View("IndexCoordinadores", coordinadoresAsignados);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los coordinadores: " + ex.Message;
                return View("IndexCoordinadores", new List<Usuario>());
            }
        }

        // GET: Mostrar docentes disponibles para asignar como coordinadores
        public ActionResult AsignarCoordinador()
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

                var docentes = docentesQuery.ToList();

                // Filtrar docentes que ya son coordinadores
                var docentesNoCoordinadores = new List<Docente>();
                foreach (var docente in docentes)
                {
                    // Verificar si el docente ya está asignado como coordinador por OID
                    var usuarioExistente = tutoriasDb.Usuarios.FirstOrDefault(u =>
                        u.MicrosoftIdentifier == docente.MicrosoftIdentifier && u.IdNivel == 3);

                    if (usuarioExistente == null)
                    {
                        docentesNoCoordinadores.Add(docente);
                    }
                }

                return View("AsignarCoordinador", docentesNoCoordinadores);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los docentes: " + ex.Message;
                return View("AsignarCoordinador", new List<Docente>());
            }
        }

        // POST: Asignar docente como coordinador
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarCoordinador(int idDocente)
        {
            try
            {
                var docente = usuariosDb.Docentes.Find(idDocente);
                if (docente == null)
                {
                    TempData["Error"] = "Docente no encontrado.";
                    return RedirectToAction("AsignarCoordinador");
                }

                // ✅ VALIDACIÓN: Verificar que el docente tenga OID
                if (string.IsNullOrEmpty(docente.MicrosoftIdentifier) ||
                   string.IsNullOrWhiteSpace(docente.MicrosoftIdentifier))
                {
                    TempData["Error"] = "El docente seleccionado no tiene un Microsoft Identifier (OID) asignado.";
                    return RedirectToAction("AsignarCoordinador");
                }

                Usuario user = Session["Usuario"] as Usuario;

                // Verificar si ya existe un usuario con este Microsoft Identifier
                var usuarioExistente = tutoriasDb.Usuarios.FirstOrDefault(u =>
                    u.MicrosoftIdentifier == docente.MicrosoftIdentifier);

                if (usuarioExistente != null)
                {
                    // Si existe, actualizar su nivel a coordinador (3) y copiar información del docente
                    usuarioExistente.IdNivel = 3;
                    usuarioExistente.IdCarrera = user.IdCarrera;
                    usuarioExistente.Estado = true;
                    usuarioExistente.Tiempo = DateTime.Now;

                    // Actualizar información del docente
                    usuarioExistente.NombreCompleto = docente.Nombre;
                    usuarioExistente.CorreoElectronico = docente.CorreoElectronico;
                    usuarioExistente.UserName = docente.NumeroEmpleado;

                    tutoriasDb.Entry(usuarioExistente).State = EntityState.Modified;
                }
                else
                {
                    // Crear nuevo usuario basado en los datos del docente
                    var nuevoUsuario = new Usuario
                    {
                        NombreCompleto = docente.Nombre,
                        UserName = docente.NumeroEmpleado,
                        Password = Security.Encripta("123456"), // Password temporal
                        CorreoElectronico = docente.CorreoElectronico,
                        MicrosoftIdentifier = docente.MicrosoftIdentifier,
                        IdCarrera = user.IdCarrera,
                        IdNivel = 3,
                        Estado = true,
                        Tiempo = DateTime.Now
                    };
                    tutoriasDb.Usuarios.Add(nuevoUsuario);
                }

                tutoriasDb.SaveChanges();

                TempData["Mensaje"] = $"Docente {docente.Nombre} asignado como coordinador exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al asignar coordinador: {ex.Message}");
                TempData["Error"] = "Error al asignar coordinador: " + ex.Message;
                return RedirectToAction("AsignarCoordinador");
            }
        }

        public ActionResult EditarCoordinador(int? id)
        {
            try
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                Usuario usuario = tutoriasDb.Usuarios.Find(id);
                if (usuario == null)
                {
                    return HttpNotFound();
                }

                // Crear una copia para la vista con la contraseña desencriptada
                var usuarioVista = new Usuario
                {
                    IdUsuario = usuario.IdUsuario,
                    NombreCompleto = usuario.NombreCompleto,
                    UserName = usuario.UserName,
                    // Desencriptar la contraseña para mostrarla en la vista
                    Password = !string.IsNullOrEmpty(usuario.Password) ? Security.Desencripta(usuario.Password) : "",
                    IdCarrera = usuario.IdCarrera,
                    IdNivel = usuario.IdNivel,
                    Estado = usuario.Estado,
                    Tiempo = usuario.Tiempo,
                    CorreoElectronico = usuario.CorreoElectronico,
                    MicrosoftIdentifier = usuario.MicrosoftIdentifier
                };

                // Poblar carreras para permitir cambio
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", usuarioVista.IdCarrera);

                return View("EditarCoordinador", usuarioVista);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar el coordinador: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarCoordinador(Usuario usuario)
        {
            try
            {
                if (string.IsNullOrEmpty(usuario.NombreCompleto) ||
                    string.IsNullOrEmpty(usuario.UserName) ||
                    string.IsNullOrEmpty(usuario.Password))
                {
                    ViewBag.Mensaje = "Favor de llenar todos los campos obligatorios.";
                    return View("EditarCoordinador", usuario);
                }

                if (ModelState.IsValid)
                {
                    // Verificar duplicados excluyendo el usuario actual
                    var users = tutoriasDb.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName &&
                        x.IdUsuario != usuario.IdUsuario && x.IdNivel == 3);
                    var users2 = tutoriasDb.Usuarios.FirstOrDefault(x => x.NombreCompleto == usuario.NombreCompleto &&
                        x.IdUsuario != usuario.IdUsuario && x.IdNivel == 3);
                    var users3 = tutoriasDb.Usuarios.FirstOrDefault(x => x.CorreoElectronico == usuario.CorreoElectronico &&
                        x.IdUsuario != usuario.IdUsuario && x.IdNivel == 3);

                    if (users == null && users2 == null && users3 == null)
                    {
                        // Obtener el usuario actual de la sesión
                        Usuario user = Session["Usuario"] as Usuario;

                        // Obtener el usuario original de la base de datos
                        var usuarioOriginal = tutoriasDb.Usuarios.Find(usuario.IdUsuario);
                        if (usuarioOriginal != null)
                        {
                            // Actualizar los campos editables
                            usuarioOriginal.NombreCompleto = usuario.NombreCompleto;
                            usuarioOriginal.UserName = usuario.UserName;
                            usuarioOriginal.CorreoElectronico = usuario.CorreoElectronico;

                            // Encriptar la nueva contraseña
                            usuarioOriginal.Password = Security.Encripta(usuario.Password);

                            // Mantener otros campos importantes
                            usuarioOriginal.IdCarrera = (usuario.IdCarrera != 0 ? usuario.IdCarrera : user.IdCarrera);
                            usuarioOriginal.IdNivel = 3;
                            usuarioOriginal.Estado = true;
                            usuarioOriginal.Tiempo = DateTime.Now;
                            // No modificar MicrosoftIdentifier - se mantiene el original

                            tutoriasDb.Entry(usuarioOriginal).State = EntityState.Modified;
                            tutoriasDb.SaveChanges();

                            TempData["Mensaje"] = "Coordinador actualizado correctamente.";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            ViewBag.Mensaje = "No se encontró el coordinador a actualizar.";
                            ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", usuario.IdCarrera);
                            return View("EditarCoordinador", usuario);
                        }
                    }
                    else
                    {
                        ViewBag.Mensaje = "El nombre completo, usuario o correo electrónico ya está registrado. Favor de intentar con otros datos.";
                        ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", usuario.IdCarrera);
                        return View("EditarCoordinador", usuario);
                    }
                }
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", usuario.IdCarrera);
                return View("EditarCoordinador", usuario);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al actualizar el coordinador: " + ex.Message;
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", usuario.IdCarrera);
                return View("EditarCoordinador", usuario);
            }
        }

        public ActionResult EliminarCoordinador(int? id)
        {
            try
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                Usuario coord = tutoriasDb.Usuarios.Find(id);
                if (coord == null)
                {
                    return HttpNotFound();
                }

                // Crear una copia para la vista
                var coordVista = new Usuario
                {
                    IdUsuario = coord.IdUsuario,
                    NombreCompleto = coord.NombreCompleto,
                    UserName = coord.UserName,
                    Password = Security.Desencripta(coord.Password ?? ""),
                    IdCarrera = coord.IdCarrera,
                    IdNivel = coord.IdNivel,
                    Estado = coord.Estado,
                    Tiempo = coord.Tiempo,
                    CorreoElectronico = coord.CorreoElectronico,
                    MicrosoftIdentifier = coord.MicrosoftIdentifier
                };

                return View("EliminarCoordinador", coordVista);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar el coordinador: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarCoordinador(int id)
        {
            try
            {
                Usuario coord = tutoriasDb.Usuarios.Find(id);
                if (coord != null)
                {
                    // En lugar de eliminar, cambiar el nivel para que ya no sea coordinador
                    coord.IdNivel = 1; // Cambiar a nivel básico
                    tutoriasDb.Entry(coord).State = EntityState.Modified;
                    tutoriasDb.SaveChanges();
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al eliminar el coordinador: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        public ActionResult Asesorados(int? id)
        {
            try
            {
                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
                return View("AsesoradosCoordinador", tutoriasDb.TutoriaGrupals.Where(x => x.IdUsuario == id).ToList());
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los grupos: " + ex.Message;
                return View("AsesoradosCoordinador", new List<TutoriaGrupal>());
            }
        }

        public ActionResult Asignar(int? id)
        {
            try
            {
                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
                ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre");
                ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre");
                return View("AsignarCoordinadorGrupo");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar los datos: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Asignar(TutoriaGrupal tutoriaGrupal, int id)
        {
            try
            {
                tutoriaGrupal.Año = DateTime.Now.Year;
                if (ModelState.IsValid)
                {
                    Usuario user = Session["Usuario"] as Usuario;
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

                    tutoriaGrupal.IdUsuario = id;
                    tutoriaGrupal.IdCarrera = user.IdCarrera;

                    var grupo = tutoriasDb.TutoriaGrupals.FirstOrDefault(x => x.IdGrado == tutoriaGrupal.IdGrado &&
                        x.IdGrupo == tutoriaGrupal.IdGrupo && x.IdCarrera == tutoriaGrupal.IdCarrera &&
                        x.IdTurno == tutoriaGrupal.IdTurno && x.IdPeriodo == tutoriaGrupal.IdPeriodo &&
                        x.Año == tutoriaGrupal.Año);

                    if (grupo == null)
                    {
                        tutoriasDb.TutoriaGrupals.Add(tutoriaGrupal);
                        tutoriasDb.SaveChanges();
                        return RedirectToAction("Asesorados", new { id = tutoriaGrupal.IdUsuario });
                    }
                    else
                    {
                        ViewBag.Mensaje = "El grupo que intenta crear ya está asignado a un coordinador.";
                    }
                }

                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
                ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre");
                ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre");
                return View("AsignarCoordinadorGrupo", tutoriaGrupal);
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al asignar el grupo: " + ex.Message;
                ViewBag.Usuario = tutoriasDb.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
                ViewBag.IdGrado = new SelectList(tutoriasDb.Gradoes, "IdGrado", "Nombre");
                ViewBag.IdGrupo = new SelectList(tutoriasDb.Grupoes, "IdGrupo", "Nombre");
                ViewBag.IdCarrera = new SelectList(tutoriasDb.Carreras, "IdCarrera", "Nombre");
                ViewBag.IdTurno = new SelectList(tutoriasDb.Turnoes, "IdTurno", "Nombre");
                return View("AsignarCoordinadorGrupo", tutoriaGrupal);
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
            return View("EliminarCoordinadorGrupo", tutoriaGrupal);
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
            return View("EditarCoordinadorGrupo", tutoriaGrupal);
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
            return View("EditarCoordinadorGrupo", tutoriaGrupal);
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


