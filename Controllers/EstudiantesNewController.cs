using Microsoft.Reporting.WebForms;
using Plataforma_Web.Data;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using PlataformaWeb;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity.Validation;
using OfficeOpenXml;
using System.Drawing;

namespace Plataforma_Web.Controllers.Estudiantes
{
    public class EstudiantesNewController : Controller
    {
        private TutoriasContext tutoriasDb = new TutoriasContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var usuario = Session["Usuario"] as Usuario;

            if (usuario == null || usuario.IdNivel == 1 || usuario.IdNivel == 2)
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Acceso denegado");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // GET: EstudiantesNew - Vista principal (simplificada para DataTables)
        public ActionResult Index()
        {
            try
            {
                Usuario user = Session["Usuario"] as Usuario;

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Solo pasamos información básica a la vista
                ViewBag.UserNivel = user.IdNivel;
                ViewBag.UserCarrera = user.IdCarrera;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar la vista: " + ex.Message;
                return View();
            }
        }

        // POST: EstudiantesNew/GetEstudiantes - Endpoint para DataTables Server-Side
        [LecturaPermitida]
        [HttpPost]
        public ActionResult GetEstudiantes()
        {
            try
            {
                // Leer parámetros de DataTables
                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumnIndex = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                // Convertir a enteros
                int pageSize = length != null ? Convert.ToInt32(length) : 25;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Usuario en sesión
                Usuario user = Session["Usuario"] as Usuario;
                if (user == null)
                {
                    return Json(new { error = "Sesión no válida" }, JsonRequestBehavior.AllowGet);
                }

                // Query base optimizada con JOIN
                var query = from u in tutoriasDb.Usuarios
                            where u.IdNivel == 1
                            join dp in tutoriasDb.DatosPersonales on u.UserName equals dp.Matricula into dpGroup
                            from dp in dpGroup.DefaultIfEmpty()
                            join c in tutoriasDb.Carreras on dp.IdCarrera equals c.IdCarrera into cGroup
                            from c in cGroup.DefaultIfEmpty()
                            select new
                            {
                                Usuario = u,
                                CarreraNombre = c != null ? c.Nombre : "No registrado",
                                IdCarrera = dp != null ? dp.IdCarrera : 0
                            };

                // Aplicar filtro según nivel de usuario
                if (user.IdNivel == 3)
                {
                    // Coordinador: solo estudiantes de su carrera (excluir no registrados)
                    query = query.Where(x => x.IdCarrera == user.IdCarrera && x.IdCarrera > 0);
                }
                // Nivel 4 o superior: mostrar todos

                // Total de registros SIN filtro de búsqueda
                int recordsTotal = query.Count();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(searchValue))
                {
                    searchValue = searchValue.ToLower();
                    query = query.Where(x =>
                        x.Usuario.NombreCompleto.ToLower().Contains(searchValue) ||
                        x.Usuario.UserName.ToLower().Contains(searchValue) ||
                        x.CarreraNombre.ToLower().Contains(searchValue)
                    );
                }

                // Total de registros CON filtro de búsqueda
                int recordsFiltered = query.Count();

                // Aplicar ordenamiento
                if (!string.IsNullOrEmpty(sortColumnIndex) && !string.IsNullOrEmpty(sortDirection))
                {
                    switch (sortColumnIndex)
                    {
                        case "0": // Nombre Completo
                            query = sortDirection == "asc" ?
                                query.OrderBy(x => x.Usuario.NombreCompleto) :
                                query.OrderByDescending(x => x.Usuario.NombreCompleto);
                            break;
                        case "1": // Usuario
                            query = sortDirection == "asc" ?
                                query.OrderBy(x => x.Usuario.UserName) :
                                query.OrderByDescending(x => x.Usuario.UserName);
                            break;
                        case "2": // Carrera
                            query = sortDirection == "asc" ?
                                query.OrderBy(x => x.CarreraNombre) :
                                query.OrderByDescending(x => x.CarreraNombre);
                            break;
                        case "3": // Estado
                            query = sortDirection == "asc" ?
                                query.OrderBy(x => x.Usuario.Estado) :
                                query.OrderByDescending(x => x.Usuario.Estado);
                            break;
                        default:
                            query = query.OrderBy(x => x.Usuario.NombreCompleto);
                            break;
                    }
                }
                else
                {
                    // Ordenamiento por defecto
                    query = query.OrderBy(x => x.Usuario.NombreCompleto);
                }

                // Aplicar paginación
                var data = query.Skip(skip).Take(pageSize).ToList();

                // Preparar datos para respuesta (desencriptar SOLO los de la página actual)
                var dataResponse = data.Select(item => new
                {
                    NombreCompleto = item.Usuario.NombreCompleto,
                    UserName = item.Usuario.UserName,
                    CarreraNombre = item.CarreraNombre,
                    Estado = item.Usuario.Estado,
                    Password = TryDesencriptar(item.Usuario.Password)
                }).ToList();

                // Retornar en formato DataTables
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
                System.Diagnostics.Debug.WriteLine($"Error en GetEstudiantes: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: EstudiantesNew/Create
        public ActionResult Create()
        {
            Usuario user = Session["Usuario"] as Usuario;

            if (user.IdNivel >= 4)
            {
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre");
            }
            return View();
        }

        // POST: EstudiantesNew/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Usuario usuario)
        {
            try
            {
                if (string.IsNullOrEmpty(usuario.NombreCompleto) ||
                   string.IsNullOrEmpty(usuario.UserName) ||
                   string.IsNullOrEmpty(usuario.Password) ||
                   string.IsNullOrEmpty(usuario.CorreoElectronico))
                {
                    ViewBag.Mensaje = "Favor de llenar todos los campos obligatorios.";
                    return View(usuario);
                }

                if (ModelState.IsValid)
                {
                    var users = tutoriasDb.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName && x.IdNivel == 1);
                    var users2 = tutoriasDb.Usuarios.FirstOrDefault(x => x.NombreCompleto == usuario.NombreCompleto && x.IdNivel == 1);
                    var users3 = tutoriasDb.Usuarios.FirstOrDefault(x => x.CorreoElectronico == usuario.CorreoElectronico && x.IdNivel == 1);

                    if (users == null && users2 == null && users3 == null)
                    {
                        Usuario user = Session["Usuario"] as Usuario;

                        if (user.IdNivel >= 4)
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

                        usuario.IdNivel = 1;
                        usuario.Estado = true;
                        usuario.Tiempo = DateTime.Now;
                        usuario.Password = Security.Encripta(usuario.Password);
                        usuario.MicrosoftIdentifier = null;

                        tutoriasDb.Usuarios.Add(usuario);
                        tutoriasDb.SaveChanges();

                        TempData["Mensaje"] = "Estudiante creado correctamente.";
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
                ViewBag.Mensaje = "Error al crear el estudiante: " + ex.Message;
                return View(usuario);
            }
        }

        // GET: EstudiantesNew/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var usuarioOriginal = tutoriasDb.Usuarios
                .AsNoTracking()
                .FirstOrDefault(u => u.IdUsuario == id && u.IdNivel == 1);

            if (usuarioOriginal == null) return HttpNotFound();

            var sesion = Session["Usuario"] as Usuario;
            if (sesion != null && sesion.IdNivel == 3 && usuarioOriginal.IdCarrera != sesion.IdCarrera)
                return new HttpStatusCodeResult(403, "No tiene permisos para editar este estudiante");

            string passwordDesencriptada = "";
            try
            {
                if (!string.IsNullOrEmpty(usuarioOriginal.Password))
                {
                    passwordDesencriptada = Security.Desencripta(usuarioOriginal.Password);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error al desencriptar la contraseña actual. Puede establecer una nueva contraseña.";
                ViewBag.MensajeTipo = "warning";
                passwordDesencriptada = "";
            }

            var modelo = new Usuario
            {
                IdUsuario = usuarioOriginal.IdUsuario,
                NombreCompleto = usuarioOriginal.NombreCompleto,
                UserName = usuarioOriginal.UserName,
                CorreoElectronico = usuarioOriginal.CorreoElectronico,
                IdCarrera = usuarioOriginal.IdCarrera,
                Password = passwordDesencriptada,
                Estado = usuarioOriginal.Estado
            };

            if (sesion?.IdNivel >= 4)
                ViewBag.Carreras = new SelectList(tutoriasDb.Carreras.ToList(), "IdCarrera", "Nombre", modelo.IdCarrera);

            if (TempData["Mensaje"] != null)
            {
                ViewBag.Mensaje = TempData["Mensaje"];
                ViewBag.MensajeTipo = TempData["MensajeTipo"] ?? "success";
            }

            return View(modelo);
        }

        // POST: EstudiantesNew/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "IdUsuario,NombreCompleto,UserName,Password,Estado,CorreoElectronico")] Usuario form)
        {
            var usuarioOriginal = tutoriasDb.Usuarios.Find(form.IdUsuario);
            if (usuarioOriginal == null || usuarioOriginal.IdNivel != 1)
            {
                TempData["Mensaje"] = "Usuario no encontrado.";
                TempData["MensajeTipo"] = "error";
                return RedirectToAction("Index");
            }

            try
            {
                // Actualizar campos básicos
                usuarioOriginal.NombreCompleto = form.NombreCompleto;
                usuarioOriginal.UserName = form.UserName;
                usuarioOriginal.Estado = form.Estado;
                usuarioOriginal.CorreoElectronico = form.CorreoElectronico;

                // Solo actualizar contraseña si se proporcionó una nueva
                if (!string.IsNullOrWhiteSpace(form.Password))
                {
                    var passActualPlano = "";
                    try
                    {
                        passActualPlano = Security.Desencripta(usuarioOriginal.Password ?? string.Empty);
                    }
                    catch (Exception)
                    {
                        passActualPlano = "";
                    }

                    if (string.IsNullOrEmpty(passActualPlano) ||
                        !string.Equals(passActualPlano, form.Password, StringComparison.Ordinal))
                    {
                        usuarioOriginal.Password = Security.Encripta(form.Password);
                    }
                }

                usuarioOriginal.Tiempo = DateTime.Now;

                var prev = tutoriasDb.Configuration.ValidateOnSaveEnabled;
                tutoriasDb.Configuration.ValidateOnSaveEnabled = false;
                try
                {
                    tutoriasDb.SaveChanges();
                    TempData["Mensaje"] = "Usuario actualizado correctamente.";
                    TempData["MensajeTipo"] = "success";
                }
                finally
                {
                    tutoriasDb.Configuration.ValidateOnSaveEnabled = prev;
                }
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al actualizar el usuario: " + ex.Message;
                TempData["MensajeTipo"] = "error";
            }

            return RedirectToAction("Edit", new { id = form.IdUsuario });
        }

        // GET: EstudiantesNew/Delete/5
        public ActionResult Delete(int? id)
        {
            try
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                Usuario estudiante = tutoriasDb.Usuarios.Find(id);
                if (estudiante == null || estudiante.IdNivel != 1)
                {
                    return HttpNotFound();
                }

                Usuario user = Session["Usuario"] as Usuario;
                if (user.IdNivel == 3 && estudiante.IdCarrera != user.IdCarrera)
                {
                    return new HttpStatusCodeResult(403, "No tiene permisos para eliminar este estudiante");
                }

                string passwordDesencriptada = "[Error de desencriptación]";
                try
                {
                    if (!string.IsNullOrEmpty(estudiante.Password))
                    {
                        passwordDesencriptada = Security.Desencripta(estudiante.Password);
                    }
                }
                catch (Exception)
                {
                    passwordDesencriptada = "[No se pudo desencriptar]";
                }

                var estudianteVista = new Usuario
                {
                    IdUsuario = estudiante.IdUsuario,
                    NombreCompleto = estudiante.NombreCompleto,
                    UserName = estudiante.UserName,
                    Password = passwordDesencriptada,
                    Estado = estudiante.Estado,
                    Tiempo = estudiante.Tiempo,
                    CorreoElectronico = estudiante.CorreoElectronico ?? "Sin correo"
                };

                return View(estudianteVista);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error al cargar el estudiante: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: EstudiantesNew/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                Usuario estudiante = tutoriasDb.Usuarios.Find(id);
                if (estudiante == null || estudiante.IdNivel != 1)
                {
                    TempData["Error"] = "Estudiante no encontrado.";
                    return RedirectToAction("Index");
                }

                Usuario user = Session["Usuario"] as Usuario;
                if (user.IdNivel == 3 && estudiante.IdCarrera != user.IdCarrera)
                {
                    TempData["Error"] = "No tiene permisos para eliminar este estudiante.";
                    return RedirectToAction("Index");
                }

                string nombreEstudiante = estudiante.NombreCompleto;
                string matricula = estudiante.UserName;

                tutoriasDb.Usuarios.Remove(estudiante);
                tutoriasDb.SaveChanges();

                TempData["Mensaje"] = $"El estudiante {nombreEstudiante} (Usuario: {matricula}) ha sido eliminado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar el estudiante: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        public ActionResult ExportarEstudiantesExcel()
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null || usuario.IdNivel != 4)
            {
                return new HttpStatusCodeResult(403, "Acceso denegado");
            }

            // Query base sin métodos C# en el select
            var query = from u in tutoriasDb.Usuarios
                        where u.IdNivel == 1
                        join dp in tutoriasDb.DatosPersonales on u.UserName equals dp.Matricula into dpGroup
                        from dp in dpGroup.DefaultIfEmpty()
                        join c in tutoriasDb.Carreras on dp.IdCarrera equals c.IdCarrera into cGroup
                        from c in cGroup.DefaultIfEmpty()
                        select new
                        {
                            Matricula = u.UserName,
                            NombreCompleto = u.NombreCompleto,
                            Carrera = c != null ? c.Nombre : "No registrado",
                            CorreoElectronico = u.CorreoElectronico ?? ""
                        };

            var lista = query.OrderBy(x => x.Carrera).ThenBy(x => x.NombreCompleto).ToList();

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Estudiantes");
                // Header
                ws.Cells[1, 1].Value = "Matrícula";
                ws.Cells[1, 2].Value = "Nombre Completo";
                ws.Cells[1, 3].Value = "Carrera";
                ws.Cells[1, 4].Value = "Correo Electrónico";

                using (var header = ws.Cells[1, 1, 1, 4])
                {
                    header.Style.Font.Bold = true;
                    header.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                    header.Style.Font.Color.SetColor(Color.White);
                }

                int row = 2;
                foreach (var est in lista)
                {
                    ws.Cells[row, 1].Value = est.Matricula;
                    ws.Cells[row, 2].Value = est.NombreCompleto;
                    ws.Cells[row, 3].Value = est.Carrera;
                    ws.Cells[row, 4].Value = est.CorreoElectronico;
                    row++;
                }

                ws.Cells.AutoFitColumns();

                string nombreArchivo = $"Estudiantes_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var stream = new System.IO.MemoryStream(package.GetAsByteArray());
                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    nombreArchivo);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tutoriasDb?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Método auxiliar para desencriptar contraseñas de forma segura
        /// </summary>
        private string TryDesencriptar(string passwordEncriptada)
        {
            try
            {
                if (string.IsNullOrEmpty(passwordEncriptada))
                    return "[Sin contraseña]";

                return Security.Desencripta(passwordEncriptada);
            }
            catch (Exception)
            {
                return "[Error de desencriptación]";
            }
        }
    }
}