using Plataforma_Web.Models;
using Plataforma_Web.Models.UsuarioAlumnos;
using Plataforma_Web.Models.UsuariosAlumnoMaster;
using ProyectoIntegracion.Models.GestionUsuarios;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.UsuarioAlumnos
{
    public class UsuarioAlumnosController : Controller
    {
        private PlataformaWebDbContext db = new PlataformaWebDbContext();
        private usuarios_model_db gestionDb = new usuarios_model_db();

        // Valida que solo coordinadores (IdNivel = 3) puedan acceder
        private bool ValidarAccesoCoordinador()
        {
            var usuario = Session["Usuario"] as Usuario;
            return usuario != null && (usuario.IdNivel == 3 || usuario.IdNivel == 4);
        }

        // GET: UsuarioAlumnos - Vista unificada con las 2 tablas
        public ActionResult Index()
        {
            if (!ValidarAccesoCoordinador())
            {
                return RedirectToAction("Index", "Home");
            }

            // Pasar información del usuario a la vista
            var usuario = Session["Usuario"] as Usuario;

            ViewBag.UserNivel = usuario.IdNivel;
            ViewBag.UserCarrera = usuario.IdCarrera;

            // Obtener nombre de la carrera
            try
            {
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                ViewBag.UserCarreraNombre = carrera != null ? carrera.Nombre : "Sin asignar";
            }
            catch
            {
                ViewBag.UserCarreraNombre = "Error al cargar";
            }

            return View();
        }

        // POST: Obtener datos para DataTables - Tabla Tutorías (Server-Side)
        [LecturaPermitida]
        [HttpPost]
        public ActionResult GetUsuariosTutorias()
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { error = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var usuario = Session["Usuario"] as Usuario;

                // 1. Obtener datos de referencia de Gestión
                var datosGestion = gestionDb.Alumnos
                                            .AsNoTracking()
                                            .Select(a => new {
                                                Matricula = a.Matricula,
                                                Correo = a.CorreoElectronico
                                            })
                                            .ToList();

                var matriculasGestion = new HashSet<string>(datosGestion.Select(x => x.Matricula.ToLower()));
                var correosGestion = new HashSet<string>(datosGestion
                                                        .Where(x => x.Correo != null)
                                                        .Select(x => x.Correo.ToLower()));

                // 2. Parámetros de DataTables
                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumn = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // 3. Query Base
                var usuariosQuery = from u in db.Usuarios.AsNoTracking()
                                    where u.IdNivel == 1
                                    join dp in db.DatosPersonales on u.UserName equals dp.Matricula into dpGroup
                                    from dp in dpGroup.DefaultIfEmpty()
                                    join c in db.Carreras on dp.IdCarrera equals c.IdCarrera into cGroup
                                    from c in cGroup.DefaultIfEmpty()
                                    select new
                                    {
                                        Usuario = u,
                                        IdCarrera = dp != null ? dp.IdCarrera : 0,
                                        CarreraNombre = c.Nombre ?? "Sin carrera asignada",
                                        UserNameLower = u.UserName.ToLower(),
                                        EmailLower = (u.CorreoElectronico ?? "").ToLower()
                                    };

                if (usuario.IdNivel == 3)
                {
                    usuariosQuery = usuariosQuery.Where(x => x.IdCarrera == usuario.IdCarrera);
                }

                if (!string.IsNullOrEmpty(searchValue))
                {
                    string search = searchValue.ToLower();
                    usuariosQuery = usuariosQuery.Where(x =>
                        x.Usuario.NombreCompleto.ToLower().Contains(search) ||
                        x.Usuario.UserName.Contains(search) ||
                        (x.Usuario.CorreoElectronico != null && x.Usuario.CorreoElectronico.ToLower().Contains(search)) ||
                        x.CarreraNombre.ToLower().Contains(search)
                    );
                }

                // Ejecutar query preliminar
                var resultadosPreliminares = usuariosQuery.ToList();

                // 4. Filtrar cruzando con listas de Gestión
                var datosFiltrados = resultadosPreliminares.Where(x =>
                    matriculasGestion.Contains(x.UserNameLower) ||
                    correosGestion.Contains(x.EmailLower)
                ).ToList();

                int recordsTotal = db.Usuarios.Count(u => u.IdNivel == 1);
                int recordsFiltered = datosFiltrados.Count;

                // 5. Ordenamiento CORREGIDO (Sin dynamic)
                // Aplicamos el OrderBy directamente para conservar el tipo anónimo
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDirection))
                {
                    bool asc = sortDirection == "asc";

                    switch (sortColumn)
                    {
                        case "0": // NombreCompleto
                            datosFiltrados = asc
                                ? datosFiltrados.OrderBy(x => x.Usuario.NombreCompleto).ToList()
                                : datosFiltrados.OrderByDescending(x => x.Usuario.NombreCompleto).ToList();
                            break;
                        case "1": // UserName
                            datosFiltrados = asc
                                ? datosFiltrados.OrderBy(x => x.Usuario.UserName).ToList()
                                : datosFiltrados.OrderByDescending(x => x.Usuario.UserName).ToList();
                            break;
                        case "2": // CorreoElectronico
                            datosFiltrados = asc
                                ? datosFiltrados.OrderBy(x => x.Usuario.CorreoElectronico).ToList()
                                : datosFiltrados.OrderByDescending(x => x.Usuario.CorreoElectronico).ToList();
                            break;
                        case "3": // CarreraNombre
                            datosFiltrados = asc
                                ? datosFiltrados.OrderBy(x => x.CarreraNombre).ToList()
                                : datosFiltrados.OrderByDescending(x => x.CarreraNombre).ToList();
                            break;
                        default:
                            datosFiltrados = datosFiltrados.OrderBy(x => x.Usuario.NombreCompleto).ToList();
                            break;
                    }
                }
                else
                {
                    datosFiltrados = datosFiltrados.OrderBy(x => x.Usuario.NombreCompleto).ToList();
                }

                // 6. Paginación
                var dataPage = datosFiltrados.Skip(skip).Take(pageSize).ToList();

                var dataResponse = dataPage.Select(item => new
                {
                    IdUsuario = item.Usuario.IdUsuario,
                    NombreCompleto = item.Usuario.NombreCompleto,
                    UserName = item.Usuario.UserName,
                    CorreoElectronico = item.Usuario.CorreoElectronico ?? "",
                    CarreraNombre = item.CarreraNombre,
                    Estado = item.Usuario.Estado
                }).ToList();

                return Json(new
                {
                    draw = int.Parse(draw),
                    recordsTotal = recordsTotal,
                    recordsFiltered = recordsFiltered,
                    data = dataResponse
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Sincronizar Correo Electrónico desde la base de datos de Gestión
        [HttpPost]
        public JsonResult SincronizarCorreo(int idUsuario)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                // 1. Encontrar el usuario en la BD de Tutorías
                var usuarioTutorias = db.Usuarios.Find(idUsuario);
                if (usuarioTutorias == null)
                {
                    return Json(new { success = false, message = "Usuario de Tutorías no encontrado." });
                }

                // El UserName (matrícula) es necesario para la búsqueda
                if (string.IsNullOrEmpty(usuarioTutorias.UserName))
                {
                    return Json(new { success = false, message = "El usuario no tiene un Nombre de Usuario (matrícula) para buscar." });
                }

                // 2. Conectarse a la BD de Gestión para buscar al alumno por matrícula
                using (var gestionDb = new usuarios_model_db()) // Asegúrate que este es el nombre de tu DbContext de Gestión
                {
                    var alumnoGestion = gestionDb.Alumnos
                        .AsNoTracking()
                        .FirstOrDefault(a => a.Matricula.ToLower() == usuarioTutorias.UserName.ToLower());

                    if (alumnoGestion == null)
                    {
                        return Json(new { success = false, message = "No se encontró un alumno en Gestión con la matrícula coincidente." });
                    }

                    if (string.IsNullOrEmpty(alumnoGestion.CorreoElectronico))
                    {
                        return Json(new { success = false, message = "El alumno encontrado en Gestión no tiene un correo electrónico asignado." });
                    }

                    // 3. Actualizar el correo del usuario de Tutorías y guardar
                    usuarioTutorias.CorreoElectronico = alumnoGestion.CorreoElectronico;
                    db.Entry(usuarioTutorias).State = EntityState.Modified;
                    db.SaveChanges();

                    // 4. Devolver éxito y el nuevo correo
                    return Json(new { success = true, message = "¡Correo electrónico sincronizado con éxito!", newEmail = usuarioTutorias.CorreoElectronico });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error en el servidor: " + ex.Message });
            }
        }

        // POST: Sincronizar Microsoft Identifier desde la base de datos de Gestión
        [HttpPost]
        public JsonResult SincronizarMicrosoftIdentifier(int idUsuario)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                // 1. Encontrar el usuario en la BD de Tutorías (DB 1: Tutorías)
                var usuarioTutorias = db.Usuarios.Find(idUsuario);
                if (usuarioTutorias == null)
                {
                    return Json(new { success = false, message = "Usuario de Tutorías no encontrado." });
                }

                if (string.IsNullOrEmpty(usuarioTutorias.CorreoElectronico))
                {
                    return Json(new { success = false, message = "El usuario no tiene un correo electrónico para buscar." });
                }

                string emailParaBuscar = usuarioTutorias.CorreoElectronico.ToLower();

                // 2. Conectarse a la BD de Gestión para buscar al alumno (DB 2: Gestión)
                using (var gestionDb = new usuarios_model_db())
                {
                    var alumnoGestion = gestionDb.Alumnos
                        .AsNoTracking()
                        .FirstOrDefault(a => a.CorreoElectronico.ToLower() == emailParaBuscar);

                    if (alumnoGestion == null)
                    {
                        return Json(new { success = false, message = "No se encontró un alumno coincidente en el Sistema de Gestión con ese correo." });
                    }

                    if (string.IsNullOrEmpty(alumnoGestion.MicrosoftIdentifier))
                    {
                        return Json(new { success = false, message = "El alumno en Gestión no tiene un Microsoft Identifier asignado." });
                    }

                    string newIdentifier = alumnoGestion.MicrosoftIdentifier;

                    // 3. Actualizar el usuario de Tutorías (DB 1) y guardar
                    usuarioTutorias.MicrosoftIdentifier = newIdentifier;
                    db.Entry(usuarioTutorias).State = EntityState.Modified;
                    db.SaveChanges();

                    // 4. Sincronizar con la base de datos de EstadiasUTTN (DB 3: Estadias)
                    try
                    {
                        // Usamos el DbContext y el Modelo que definimos
                        using (var estadiasDb = new EstadiasDbContext())
                        {
                            var usuarioEstadias = estadiasDb.Usuario1
                                .FirstOrDefault(u => u.CorreoElectronico.ToLower() == emailParaBuscar);

                            // Si se encontró, actualizamos y guardamos
                            if (usuarioEstadias != null)
                            {
                                usuarioEstadias.MicrosoftIdentifier = newIdentifier;
                                estadiasDb.SaveChanges();
                            }
                            // Si no se encuentra, no hacemos nada
                        }
                    }
                    catch (Exception exEstadias)
                    {
                        // Informamos si falla la sincronización con Estadias
                        System.Diagnostics.Debug.WriteLine("Error al sincronizar con EstadiasUTTN: " + exEstadias.Message);
                        return Json(new
                        {
                            success = true,
                            message = "¡Sincronizado en Tutorías! (Advertencia: no se pudo sincronizar con la BD Estadias).",
                            newIdentifier = newIdentifier
                        });
                    }

                    // 5. Devolver éxito total
                    return Json(new
                    {
                        success = true,
                        message = "¡Identificador sincronizado con éxito en todos los sistemas!",
                        newIdentifier = newIdentifier
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error en el servidor: " + ex.Message });
            }
        }

        // GET: Obtener un usuario por ID (para modal)
        [HttpGet]
        public JsonResult GetUsuarioById(int id)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var usuario = db.Usuarios
                    .Where(u => u.IdUsuario == id && u.IdNivel == 1)
                    .Select(u => new
                    {
                        u.IdUsuario,
                        u.NombreCompleto,
                        u.UserName,
                        u.CorreoElectronico,
                        u.MicrosoftIdentifier
                    })
                    .FirstOrDefault();

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var emailUsuario = usuario.CorreoElectronico;
                bool tieneEmailValido = !string.IsNullOrEmpty(emailUsuario);

                var datosPersonales = db.DatosPersonales
                    .Where(dp => dp.Matricula == usuario.UserName || (tieneEmailValido && dp.Email == emailUsuario))
                    .Select(dp => new
                    {
                        dp.IdPersona,
                        dp.Matricula,
                        dp.Email,
                        dp.Nombre,
                        dp.CarreraNom,
                        dp.IdCarrera,
                        dp.Especialidad
                    })
                    .FirstOrDefault();

                return Json(new
                {
                    success = true,
                    usuario = usuario,
                    datosPersonales = datosPersonales
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SincronizarDatosPersonales(int idUsuario)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                // 1. Obtener datos de Usuarios (Fuente de verdad)
                var usuario = db.Usuarios.Find(idUsuario);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado." });
                }

                // 2. Encontrar el registro en DatosPersonales
                var datosPersonales = db.DatosPersonales.FirstOrDefault(dp => dp.Matricula == usuario.UserName);
                if (datosPersonales == null)
                {
                    return Json(new { success = false, message = "No se encontró el registro en Datos Personales para sincronizar." });
                }

                // 3. Sincronizar y Guardar
                datosPersonales.Matricula = usuario.UserName;
                datosPersonales.Email = usuario.CorreoElectronico;
                db.Entry(datosPersonales).State = EntityState.Modified;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "¡Datos Personales sincronizados!",
                    newMatricula = datosPersonales.Matricula,
                    newEmail = datosPersonales.Email
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Editar usuario vía AJAX
        [HttpPost]
        public JsonResult EditAjax(int IdUsuario, string UserName, string NombreCompleto)
        {
            var sessionUser = Session["Usuario"] as Usuario;
            if (sessionUser == null || (sessionUser.IdNivel != 3 && sessionUser.IdNivel != 4))
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var usuario = db.Usuarios.Find(IdUsuario);
                if (usuario == null || usuario.IdNivel != 1)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                var existeUsername = db.Usuarios.Any(u => u.UserName == UserName && u.IdUsuario != IdUsuario);
                if (existeUsername)
                {
                    return Json(new { success = false, message = "Este nombre de usuario ya existe" });
                }

                string usernameAnterior = usuario.UserName;
                string nombreAnterior = usuario.NombreCompleto;
                string nuevoUsername = UserName;

                usuario.UserName = UserName;
                usuario.NombreCompleto = NombreCompleto;
                db.Entry(usuario).State = EntityState.Modified;

                string sqlDatosPersonales = @"
            UPDATE DatosPersonales 
            SET Matricula = @nuevoUsername 
            WHERE Matricula = @usernameAnterior 
               OR LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(Nombre, '  ', ' '), CHAR(9), ' '), CHAR(13), ' ')))) = 
                  LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(@nombreCompleto, '  ', ' '), CHAR(9), ' '), CHAR(13), ' '))))";

                db.Database.ExecuteSqlCommand(sqlDatosPersonales,
                    new System.Data.SqlClient.SqlParameter("@nuevoUsername", nuevoUsername),
                    new System.Data.SqlClient.SqlParameter("@usernameAnterior", usernameAnterior),
                    new System.Data.SqlClient.SqlParameter("@nombreCompleto", nombreAnterior));

                string sqlEntrevistaInicial = @"
            UPDATE EntrevistaInicials 
            SET Matricula = @nuevoUsername 
            WHERE Matricula = @usernameAnterior 
               OR LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(Nombre, '  ', ' '), CHAR(9), ' '), CHAR(13), ' ')))) = 
                  LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(@nombreCompleto, '  ', ' '), CHAR(9), ' '), CHAR(13), ' '))))";

                db.Database.ExecuteSqlCommand(sqlEntrevistaInicial,
                    new System.Data.SqlClient.SqlParameter("@nuevoUsername", nuevoUsername),
                    new System.Data.SqlClient.SqlParameter("@usernameAnterior", usernameAnterior),
                    new System.Data.SqlClient.SqlParameter("@nombreCompleto", nombreAnterior));

                db.SaveChanges();

                return Json(new { success = true, message = "Usuario actualizado exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // GET: UsuarioAlumnos/Edit/5 (OPCIONAL - se mantiene por compatibilidad)
        public ActionResult Edit(int? id)
        {
            if (!ValidarAccesoCoordinador())
            {
                return RedirectToAction("Index", "Home");
            }

            if (id == null)
            {
                return RedirectToAction("Index");
            }

            var usuario = db.Usuarios.Find(id);

            if (usuario == null || usuario.IdNivel != 1)
            {
                return RedirectToAction("Index");
            }

            var usuarioAlumno = new UsuarioAlumno
            {
                IdUsuario = usuario.IdUsuario,
                NombreCompleto = usuario.NombreCompleto,
                UserName = usuario.UserName,
                IdCarrera = usuario.IdCarrera,
                IdNivel = usuario.IdNivel,
                Estado = usuario.Estado,
                Tiempo = usuario.Tiempo,
                CorreoElectronico = usuario.CorreoElectronico,
                MicrosoftIdentifier = usuario.MicrosoftIdentifier
            };

            return View(usuarioAlumno);
        }

        // POST: UsuarioAlumnos/Edit/5 (OPCIONAL - se mantiene por compatibilidad)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(UsuarioAlumno usuarioAlumno)
        {
            if (!ValidarAccesoCoordinador())
            {
                return RedirectToAction("Index", "Home");
            }

            if (ModelState.IsValid)
            {
                var usuario = db.Usuarios.Find(usuarioAlumno.IdUsuario);

                if (usuario != null && usuario.IdNivel == 1)
                {
                    // Verifica que el nuevo username no exista
                    var existeUsername = db.Usuarios
                        .Any(u => u.UserName == usuarioAlumno.UserName && u.IdUsuario != usuarioAlumno.IdUsuario);

                    if (existeUsername)
                    {
                        ModelState.AddModelError("UserName", "Este nombre de usuario ya existe");
                        return View(usuarioAlumno);
                    }

                    string usernameAnterior = usuario.UserName;
                    string nuevoUsername = usuarioAlumno.UserName;
                    string nombreCompleto = usuario.NombreCompleto;

                    // Actualiza el Username
                    usuario.UserName = usuarioAlumno.UserName;
                    db.Entry(usuario).State = EntityState.Modified;

                    int registrosActualizados = 0;

                    try
                    {
                        // Actualiza la matrícula en DatosPersonales con lógica segura:
                        // Prioridad 1: Por matrícula (más seguro y preciso)
                        // Prioridad 2: Por nombre completo normalizado (solo si coincide exactamente)
                        // Normalización: LTRIM/RTRIM para espacios, REPLACE para espacios dobles/tabs, UPPER para case-insensitive
                        string sqlDatosPersonales = @"
                            UPDATE DatosPersonales 
                            SET Matricula = @nuevoUsername 
                            WHERE Matricula = @usernameAnterior 
                               OR LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(Nombre, '  ', ' '), CHAR(9), ' '), CHAR(13), ' ')))) = 
                                  LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(@nombreCompleto, '  ', ' '), CHAR(9), ' '), CHAR(13), ' '))))";

                        var parametrosDatosPersonales = new object[]
                        {
                            new System.Data.SqlClient.SqlParameter("@nuevoUsername", nuevoUsername),
                            new System.Data.SqlClient.SqlParameter("@usernameAnterior", usernameAnterior),
                            new System.Data.SqlClient.SqlParameter("@nombreCompleto", nombreCompleto)
                        };

                        registrosActualizados += db.Database.ExecuteSqlCommand(sqlDatosPersonales, parametrosDatosPersonales);

                        // Actualizar EntrevistaInicials con la misma lógica segura
                        string sqlEntrevistaInicial = @"
                            UPDATE EntrevistaInicials 
                            SET Matricula = @nuevoUsername 
                            WHERE Matricula = @usernameAnterior 
                               OR LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(Nombre, '  ', ' '), CHAR(9), ' '), CHAR(13), ' ')))) = 
                                  LTRIM(RTRIM(UPPER(REPLACE(REPLACE(REPLACE(@nombreCompleto, '  ', ' '), CHAR(9), ' '), CHAR(13), ' '))))";

                        var parametrosEntrevistaInicial = new object[]
                        {
                            new System.Data.SqlClient.SqlParameter("@nuevoUsername", nuevoUsername),
                            new System.Data.SqlClient.SqlParameter("@usernameAnterior", usernameAnterior),
                            new System.Data.SqlClient.SqlParameter("@nombreCompleto", nombreCompleto)
                        };

                        registrosActualizados += db.Database.ExecuteSqlCommand(sqlEntrevistaInicial, parametrosEntrevistaInicial);
                    }
                    catch (Exception)
                    {
                        TempData["Mensaje"] = "Nombre de usuario actualizado exitosamente. Advertencia: No se pudieron actualizar todas las tablas relacionadas.";
                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }

                    db.SaveChanges();

                    TempData["Mensaje"] = "Nombre de usuario actualizado exitosamente. Se actualizaron " +
                        registrosActualizados + " registros relacionados.";
                    return RedirectToAction("Index");
                }
            }

            return View(usuarioAlumno);
        }

        // GET: Obtener lista de Carreras para Dropdown
        [HttpGet]
        public JsonResult GetCarrerasList()
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }
            try
            {
                var carreras = db.Carreras
                                 .AsNoTracking()
                                 .Select(c => new { c.IdCarrera, c.Nombre })
                                 .OrderBy(c => c.Nombre)
                                 .ToList();

                return Json(new { success = true, data = carreras }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Obtener lista de Especialidades por IdCarrera
        [HttpGet]
        public JsonResult GetEspecialidadesList(int idCarrera)
        {
            if (!ValidarAccesoCoordinador())
            {
                return Json(new { success = false, message = "No autorizado" }, JsonRequestBehavior.AllowGet);
            }
            try
            {
                var especialidades = db.Especialidads
                                     .AsNoTracking()
                                     .Where(e => e.IdCarrera == idCarrera)
                                     .Select(e => new { Nombre = e.Nombre })
                                     .OrderBy(e => e.Nombre)
                                     .ToList();

                return Json(new { success = true, data = especialidades }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Editar DatosPersonales vía AJAX
        [HttpPost]
        public JsonResult EditDatosPersonalesAjax(DatosPersonalesEditViewModel model)
        {
            var sessionUser = Session["Usuario"] as Usuario;

            if (sessionUser == null || (sessionUser.IdNivel != 3 && sessionUser.IdNivel != 4))
            {
                return Json(new { success = false, message = "No autorizado para esta acción" });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Datos inválidos" });
            }

            try
            {
                var datosPersonales = db.DatosPersonales.Find(model.IdPersona);
                if (datosPersonales == null)
                {
                    return Json(new { success = false, message = "Registro de Datos Personales no encontrado." });
                }

                datosPersonales.Matricula = model.Matricula;
                datosPersonales.Nombre = model.Nombre;
                datosPersonales.Email = model.Email;
                datosPersonales.IdCarrera = model.IdCarrera;
                datosPersonales.Especialidad = model.Especialidad;

                var carrera = db.Carreras.Find(model.IdCarrera);
                datosPersonales.CarreraNom = carrera != null ? carrera.Nombre : "N/A";

                db.Entry(datosPersonales).State = EntityState.Modified;

                var usuario = db.Usuarios.FirstOrDefault(u => u.IdUsuario == model.IdUsuario);
                if (usuario != null)
                {
                    usuario.UserName = model.Matricula;
                    usuario.CorreoElectronico = model.Email;
                    db.Entry(usuario).State = EntityState.Modified;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Datos Personales actualizados exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar: " + ex.Message });
            }
        }

        public class DatosPersonalesEditViewModel
        {
            public int IdPersona { get; set; }
            public int IdUsuario { get; set; } // Lo necesitamos para la sincronización inversa
            public string Matricula { get; set; }
            public string Nombre { get; set; }
            public string Email { get; set; }
            public int IdCarrera { get; set; }
            public string Especialidad { get; set; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                gestionDb.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}