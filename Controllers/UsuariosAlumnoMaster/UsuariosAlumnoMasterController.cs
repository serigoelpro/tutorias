using System;
using System.Linq;
using System.Web.Mvc;
using Plataforma_Web.Models.UsuariosAlumnoMaster;
using Plataforma_Web.Data;
using Plataforma_Web.Models;
using Newtonsoft.Json;
using PlataformaWeb;

namespace Plataforma_Web.Controllers.UsuariosAlumnoMaster
{
    public class UsuariosAlumnoMasterController : Controller
    {
        private TutoriasContext tutoriasDb = new TutoriasContext();
        private EstadiasDbContext estadiasDb = new EstadiasDbContext();
        private GestionDbContext gestionDb = new GestionDbContext();

        // GET: UsuariosAlumnoMaster
        public ActionResult Index()
        {
            var usuario = Session["Usuario"] as Usuario;
            if (usuario == null || usuario.IdNivel != 4)
            {
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new UsuariosViewModel
            {
                TotalUsuariosTutorias = tutoriasDb.Usuarios.Count(),
                TotalUsuariosEstadias = estadiasDb.Usuario1.Count(),
                TotalAlumnosGestion = gestionDb.Alumnos.Count()
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult GetUsuariosTutorias()
        {
            try
            {
                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumn = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Query con join a DatosPersonales y Carreras como en el ejemplo
                var usuariosQuery = from u in tutoriasDb.Usuarios
                                    join dp in tutoriasDb.DatosPersonales on u.UserName equals dp.Matricula into dpGroup
                                    from dp in dpGroup.DefaultIfEmpty()
                                    join c in tutoriasDb.Carreras on dp.IdCarrera equals c.IdCarrera into cGroup
                                    from c in cGroup.DefaultIfEmpty()
                                    select new
                                    {
                                        Usuario = u,
                                        DatosPersonales = dp,
                                        CarreraNombre = c.Nombre ?? "Sin carrera asignada"
                                    };

                // Aplica filtro de búsqueda
                if (!string.IsNullOrEmpty(searchValue))
                {
                    usuariosQuery = usuariosQuery.Where(x =>
                        x.Usuario.NombreCompleto.Contains(searchValue) ||
                        x.Usuario.UserName.Contains(searchValue) ||
                        x.Usuario.Password.Contains(searchValue) ||
                        x.CarreraNombre.Contains(searchValue));
                }

                int recordsTotal = tutoriasDb.Usuarios.Count();
                int recordsFiltered = usuariosQuery.Count();

                // Aplica ordenamiento
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDirection))
                {
                    switch (sortColumn)
                    {
                        case "0":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.NombreCompleto) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.NombreCompleto);
                            break;
                        case "1":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.UserName) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.UserName);
                            break;
                        case "2":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.Password) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.Password);
                            break;
                        case "3":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.CarreraNombre) :
                                usuariosQuery.OrderByDescending(x => x.CarreraNombre);
                            break;
                        case "4":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.Estado) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.Estado);
                            break;
                    }
                }
                else
                {
                    usuariosQuery = usuariosQuery.OrderBy(x => x.Usuario.NombreCompleto);
                }

                var data = usuariosQuery.Skip(skip).Take(pageSize).ToList();

                // Crear objetos para la respuesta con carreras y contraseñas desencriptadas
                var dataResponse = data.Select(item => new
                {
                    IdUsuario = item.Usuario.IdUsuario,
                    NombreCompleto = item.Usuario.NombreCompleto,
                    UserName = item.Usuario.UserName,
                    Password = TryDesencriptar(item.Usuario.Password),
                    CorreoElectronico = item.Usuario.CorreoElectronico ?? "Sin correo",
                    CarreraNombre = item.CarreraNombre,
                    IdNivel = item.Usuario.IdNivel,
                    Estado = item.Usuario.Estado,
                    Tiempo = item.Usuario.Tiempo
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

        [HttpPost]
        public ActionResult GetUsuariosEstadias()
        {
            try
            {
                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumn = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Query con join a Carrera para obtener el nombre del área
                var usuariosQuery = from u in estadiasDb.Usuario1
                                    join c in estadiasDb.Carrera on u.IdArea equals c.IdArea into cGroup
                                    from c in cGroup.DefaultIfEmpty()
                                    select new
                                    {
                                        Usuario = u,
                                        AreaNombre = c.Area ?? "Sin área asignada"
                                    };

                // Aplica filtro de búsqueda
                if (!string.IsNullOrEmpty(searchValue))
                {
                    usuariosQuery = usuariosQuery.Where(x =>
                        x.Usuario.Nombre.Contains(searchValue) ||
                        x.Usuario.Paterno.Contains(searchValue) ||
                        x.Usuario.Materno.Contains(searchValue) ||
                        x.Usuario.Username.Contains(searchValue) ||
                        x.Usuario.Contraseña.Contains(searchValue) ||
                        x.Usuario.CorreoElectronico.Contains(searchValue) ||
                        x.AreaNombre.Contains(searchValue));
                }

                int recordsTotal = estadiasDb.Usuario1.Count();
                int recordsFiltered = usuariosQuery.Count();

                // Aplica ordenamiento
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDirection))
                {
                    switch (sortColumn)
                    {
                        case "0":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.Nombre) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.Nombre);
                            break;
                        case "1":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.Username) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.Username);
                            break;
                        case "2":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.Contraseña) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.Contraseña);
                            break;
                        case "3":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.CorreoElectronico) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.CorreoElectronico);
                            break;
                        case "4":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.AreaNombre) :
                                usuariosQuery.OrderByDescending(x => x.AreaNombre);
                            break;
                        case "5":
                            usuariosQuery = sortDirection == "asc" ?
                                usuariosQuery.OrderBy(x => x.Usuario.Estado) :
                                usuariosQuery.OrderByDescending(x => x.Usuario.Estado);
                            break;
                    }
                }
                else
                {
                    usuariosQuery = usuariosQuery.OrderBy(x => x.Usuario.Nombre);
                }

                var data = usuariosQuery.Skip(skip).Take(pageSize).ToList();

                // Crear objetos para la respuesta (las contraseñas en Estadías no están encriptadas)
                var dataResponse = data.Select(item => new
                {
                    IdUsuario = item.Usuario.IdUsuario,
                    Nombre = item.Usuario.Nombre,
                    Paterno = item.Usuario.Paterno,
                    Materno = item.Usuario.Materno,
                    Username = item.Usuario.Username,
                    Contraseña = item.Usuario.Contraseña ?? "[Sin contraseña]",
                    CorreoElectronico = item.Usuario.CorreoElectronico ?? "Sin correo",
                    AreaNombre = item.AreaNombre,
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

        [HttpPost]
        public ActionResult GetUsuariosGestion()
        {
            try
            {
                var draw = Request.Form.GetValues("draw")?.FirstOrDefault();
                var start = Request.Form.GetValues("start")?.FirstOrDefault();
                var length = Request.Form.GetValues("length")?.FirstOrDefault();
                var searchValue = Request.Form.GetValues("search[value]")?.FirstOrDefault();
                var sortColumn = Request.Form.GetValues("order[0][column]")?.FirstOrDefault();
                var sortDirection = Request.Form.GetValues("order[0][dir]")?.FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Cargar carreras desde ESTADIAS usando el DbSet directamente
                var carrerasList = estadiasDb.Carrera
                    .Select(c => new { c.IdArea, c.Area })
                    .ToList();
                var carrerasDictionary = carrerasList.ToDictionary(c => c.IdArea, c => c.Area);

                var alumnosQuery = gestionDb.Alumnos.AsQueryable();

                // Aplica filtro de búsqueda
                if (!string.IsNullOrEmpty(searchValue))
                {
                    alumnosQuery = alumnosQuery.Where(a =>
                        a.Nombre.Contains(searchValue) ||
                        a.ApellidoPaterno.Contains(searchValue) ||
                        a.ApellidoMaterno.Contains(searchValue) ||
                        a.Matricula.Contains(searchValue) ||
                        a.Contrasena.Contains(searchValue) ||
                        (a.CorreoElectronico != null && a.CorreoElectronico.Contains(searchValue)));
                }

                int recordsTotal = gestionDb.Alumnos.Count();
                int recordsFiltered = alumnosQuery.Count();

                // Aplica ordenamiento
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDirection))
                {
                    switch (sortColumn)
                    {
                        case "0":
                            alumnosQuery = sortDirection == "asc" ?
                                alumnosQuery.OrderBy(a => a.Nombre) :
                                alumnosQuery.OrderByDescending(a => a.Nombre);
                            break;
                        case "1":
                            alumnosQuery = sortDirection == "asc" ?
                                alumnosQuery.OrderBy(a => a.Matricula) :
                                alumnosQuery.OrderByDescending(a => a.Matricula);
                            break;
                        case "2":
                            alumnosQuery = sortDirection == "asc" ?
                                alumnosQuery.OrderBy(a => a.Contrasena) :
                                alumnosQuery.OrderByDescending(a => a.Contrasena);
                            break;
                        case "3":
                            alumnosQuery = sortDirection == "asc" ?
                                alumnosQuery.OrderBy(a => a.CorreoElectronico) :
                                alumnosQuery.OrderByDescending(a => a.CorreoElectronico);
                            break;
                        case "4":
                            alumnosQuery = sortDirection == "asc" ?
                                alumnosQuery.OrderBy(a => a.IdCarrera) :
                                alumnosQuery.OrderByDescending(a => a.IdCarrera);
                            break;
                        case "5":
                            alumnosQuery = sortDirection == "asc" ?
                                alumnosQuery.OrderBy(a => a.Habilitado) :
                                alumnosQuery.OrderByDescending(a => a.Habilitado);
                            break;
                    }
                }
                else
                {
                    alumnosQuery = alumnosQuery.OrderBy(a => a.Nombre);
                }

                var data = alumnosQuery.Skip(skip).Take(pageSize).ToList();

                // Crear objetos para la respuesta con contraseñas desencriptadas y nombres de carrera
                var dataResponse = data.Select(alumno => new
                {
                    IdAlumno = alumno.IdAlumno,
                    NombreCompleto = $"{alumno.Nombre} {alumno.ApellidoPaterno} {alumno.ApellidoMaterno}".Trim(),
                    Matricula = alumno.Matricula,
                    Contrasena = TryDesencriptar(alumno.Contrasena),
                    CorreoElectronico = alumno.CorreoElectronico ?? "Sin correo",
                    CarreraNombre = carrerasDictionary.ContainsKey(alumno.IdCarrera)
                        ? carrerasDictionary[alumno.IdCarrera]
                        : $"Carrera no encontrada (ID: {alumno.IdCarrera})",
                    Habilitado = alumno.Habilitado
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
                return Json(new { error = ex.Message + " | StackTrace: " + ex.StackTrace }, JsonRequestBehavior.AllowGet);
            }
        }

        private string TryDesencriptar(string passwordEncriptada)
        {
            try
            {
                if (string.IsNullOrEmpty(passwordEncriptada))
                {
                    return "[Sin contraseña]";
                }
                return Security.Desencripta(passwordEncriptada);
            }
            catch (Exception)
            {
                return "[Error de desencriptación]";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tutoriasDb?.Dispose();
                estadiasDb?.Dispose();
                gestionDb?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}