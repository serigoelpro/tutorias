using Microsoft.ReportingServices.ReportProcessing.ReportObjectModel;
using Plataforma_Web.Models;
using PlataformaWeb.BecasTransporte.Models;
using ProyectoIntegracion.Functionalities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using static System.Net.Mime.MediaTypeNames;

namespace PlataformaWeb.Controllers
{
    [CustomAuthorize]
    public class HomeController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        public ActionResult Index()
        {
            return View();
        }

        [NonAction]
        [HttpPost]
        public ActionResult Registrarse(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                if (usuario.UserName.Contains("@") || char.IsLetter(usuario.UserName.First()) || char.IsLetter(usuario.UserName.Last()))
                {
                    ViewBag.Mensaje = "Favor de LEER atentamente lo que se pide en cada campo e introducir los datos correspondientes.";
                    return View(usuario);
                }
                if (usuario.NombreCompleto.Contains("@uttn.mx"))
                {
                    var user = db.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName);
                    if (user == null)
                    {
                        usuario.NombreCompleto = usuario.NombreCompleto.Replace(',', '.');
                        usuario.Password = Security.Encripta(usuario.Password);
                        usuario.IdNivel = 1;
                        usuario.Estado = true;
                        usuario.Tiempo = DateTime.Now;
                        db.Usuarios.Add(usuario);
                        db.SaveChanges();
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        ViewBag.Mensaje = "La matricula ya esta registrada. Favor de iniciar sesion.";
                        return View(usuario);
                    }
                }
                else
                {
                    ViewBag.Mensaje = "Favor de utilizar el correo institucional.";
                    return View(usuario);
                }

            }
            return View(usuario);
        }

        [AllowAnonymous]
        public ActionResult Login(string redirect = "")
        {
            Session.Clear();

            string token = Request.Cookies.Get("tkn")?.Value;
            Response.Cookies.Set(new HttpCookie("tkn")
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Domain = ConfigurationManager.AppSettings["CookieDomain"],
                Expires = DateTime.UtcNow.AddDays(-1)
            });

            if (!string.IsNullOrEmpty(token))
            {
                string strId = ExternalLoginToken.Consume(token);
                string[] sections = strId.Split('/');
                var oid = ((System.Security.Claims.ClaimsIdentity)User.Identity)
                      .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (int.TryParse(sections[0], out int id)
                    && sections[1] == oid
                    && sections[2] == ConfigurationManager.AppSettings["SubDomain"])
                {
                    var user = db.Usuarios.Find(id);

                    Session["Usuario"] = user;
                    Session["Nivel"] = user.Nivel;

                    Session.Timeout = 60;
                    Session["HoraFinSesion"] = DateTime.Now.AddHours(1);

                    user.Tiempo = DateTime.Now;
                    db.Entry(user).State = EntityState.Modified;
                    db.SaveChanges();

                    if (user.IdNivel == 3 && !user.EsDirector)
                    {
                        AgregarHistorialBT(user);
                    }

                    // Los directores reales de produccion existen como IdNivel=5 (catalogo Nivels),
                    // pero el resto del codigo solo conoce el modelo IdNivel=3 + EsDirector (hereda
                    // las pantallas del Coordinador con solo-lectura garantizada). Se mapea SOLO en
                    // el objeto de la sesion: la entidad ya se guardo arriba y se desacopla del
                    // contexto para que este cambio NO llegue a la BD.
                    if (user.IdNivel == 5)
                    {
                        db.Entry(user).State = EntityState.Detached;
                        user.IdNivel = 3;
                        user.EsDirector = true;
                    }

                    return Redirect(LinkGenerator.Create(ConfigurationManager.AppSettings["SubDomain"], redirect));
                }
                return Redirect(LinkGenerator.Create("login", "Home/Unauthorized"));
            }
            return RedirectToAction("Index");
        }

        public ActionResult Restringido()
        {
            return Redirect(LinkGenerator.Create("login", "Home/Unauthorized"));
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return Redirect(LinkGenerator.Create("login", "Usuarios/CerrarSesion"));
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult ClearSession()
        {
            Session.Clear();
            Session.Abandon();

            if (Request.Cookies["ASP.NET_SessionId"] != null)
            {
                var cookie = new HttpCookie("ASP.NET_SessionId", "")
                {
                    Expires = DateTime.Now.AddDays(-1),
                    Path = "/",
                    Domain = ConfigurationManager.AppSettings["SubDomain"] + ConfigurationManager.AppSettings["CookieDomain"]
                };
                Response.Cookies.Set(cookie);
            }

            return Content("Sesión cerrada en tutorías...");
        }

        public ActionResult ReturnHome()
        {
            Session.Clear();
            Session.Abandon();
            return Redirect(LinkGenerator.Create("login", "Home/Index"));
        }

        public void AgregarHistorialBT(Usuario user)
        {
            if (user.IdNivel == 3)
            {
                #region Periodo Actual
                var tiempo = DateTime.Now;
                var PeriodoActual = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    PeriodoActual = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    PeriodoActual = 2;
                }
                else
                {
                    PeriodoActual = 3;
                }
                #endregion
                using (ModeloPlataforma data = new ModeloPlataforma())
                {
                    var Turnos = data.TutoriaGrupals.Where(x => x.IdCarrera == user.IdCarrera && x.Año == tiempo.Year && x.IdPeriodo == PeriodoActual).Select(x => x.IdTurno);

                    Historial oHistorial = data.Historials.OrderByDescending(x => x.Id).FirstOrDefault(x => x.IdCarrera == user.IdCarrera);
                    if (oHistorial != null)
                    {
                        if (oHistorial.Fecha.Month != tiempo.Month)
                        {
                            if (tiempo.Day == 10)
                            {
                                Historial newHistorial = new Historial();

                                newHistorial.IdCarrera = user.IdCarrera;
                                newHistorial.Fecha = DateTime.Now;

                                newHistorial.CantidadBecados = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0);
                                newHistorial.CantidadUsanTransporte = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0);
                                newHistorial.CantidadTotalEstudiantes = data.DatosPersonales.Where(x => Turnos.Contains(x.IdTurno)).Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno > 0);
                                newHistorial.MontoTotal = montoTotalPorCuatrimestre(user.IdCarrera);

                                newHistorial.CantidadBecadosMatutino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0 && x.IdTurno == 1);
                                newHistorial.CantidadUsanTransporteMatutino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0 && x.IdTurno == 1);
                                newHistorial.CantidadTotalEstudiantesMatutino = data.DatosPersonales.Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno == 1);
                                newHistorial.MontoTotalMatutino = montoTotalPorCuatrimestre(user.IdCarrera, 1);

                                newHistorial.CantidadBecadosVespertino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0 && x.IdTurno == 2);
                                newHistorial.CantidadUsanTransporteVespertino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0 && x.IdTurno == 2);
                                newHistorial.CantidadTotalEstudiantesVespertino = data.DatosPersonales.Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno == 2);
                                newHistorial.MontoTotalVespertino = montoTotalPorCuatrimestre(user.IdCarrera, 2);

                                newHistorial.CantidadBecadosDespresurizado = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0 && x.IdTurno == 3);
                                newHistorial.CantidadUsanTransporteDespresurizado = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0 && x.IdTurno == 3);
                                newHistorial.CantidadTotalEstudiantesDespresurizado = data.DatosPersonales.Where(x => Turnos.Contains(x.IdTurno)).Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno == 3);
                                newHistorial.MontoTotalDespresurizado = montoTotalPorCuatrimestre(user.IdCarrera, 3);

                                newHistorial.PeriodoActual = PeriodoActual;

                                db.Historials.Add(newHistorial);
                                db.SaveChanges();

                            }
                        }
                    }
                    else
                    {
                        Historial newHistorial = new Historial();

                        newHistorial.IdCarrera = user.IdCarrera;
                        newHistorial.Fecha = DateTime.Now;

                        newHistorial.CantidadBecados = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0);
                        newHistorial.CantidadUsanTransporte = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0);
                        newHistorial.CantidadTotalEstudiantes = data.DatosPersonales.Where(x => Turnos.Contains(x.IdTurno)).Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno > 0);
                        newHistorial.MontoTotal = montoTotalPorCuatrimestre(user.IdCarrera);

                        newHistorial.CantidadBecadosMatutino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0 && x.IdTurno == 1);
                        newHistorial.CantidadUsanTransporteMatutino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0 && x.IdTurno == 1);
                        newHistorial.CantidadTotalEstudiantesMatutino = data.DatosPersonales.Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno == 1);
                        newHistorial.MontoTotalMatutino = montoTotalPorCuatrimestre(user.IdCarrera, 1);

                        newHistorial.CantidadBecadosVespertino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0 && x.IdTurno == 2);
                        newHistorial.CantidadUsanTransporteVespertino = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0 && x.IdTurno == 2);
                        newHistorial.CantidadTotalEstudiantesVespertino = data.DatosPersonales.Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno == 2);
                        newHistorial.MontoTotalVespertino = montoTotalPorCuatrimestre(user.IdCarrera, 2);

                        newHistorial.CantidadBecadosDespresurizado = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdBeca > 0 && x.IdTurno == 3);
                        newHistorial.CantidadUsanTransporteDespresurizado = data.Estudiantes.Count(x => x.IdCarrera == user.IdCarrera && x.IdTransporte != 0 && x.IdTurno == 3);
                        newHistorial.CantidadTotalEstudiantesDespresurizado = data.DatosPersonales.Where(x => Turnos.Contains(x.IdTurno)).Count(x => x.IdCarrera == user.IdCarrera && x.IdPeriodo == PeriodoActual && x.Año == DateTime.Now.Year && x.IdTurno == 3);
                        newHistorial.MontoTotalDespresurizado = montoTotalPorCuatrimestre(user.IdCarrera, 3);

                        newHistorial.PeriodoActual = PeriodoActual;

                        db.Historials.Add(newHistorial);
                        db.SaveChanges();
                    }
                }

            }
        }

        public int montoTotalPorCuatrimestre(int Id)
        {
            int montoTotal = 0;

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 1).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 2).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 4).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }
            return montoTotal;
        }

        public int montoTotalPorCuatrimestre(int Id, int Turno)
        {
            int montoTotal = 0;

            using (ModeloPlataforma data = new ModeloPlataforma())
            {
                List<int> becasPorMes = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 1 && x.IdTurno == Turno).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorMes = 0;
                foreach (int Monto in becasPorMes)
                {
                    sumaBecasPorMes = sumaBecasPorMes + Monto;
                }
                sumaBecasPorMes = sumaBecasPorMes * 4;
                List<int> becasPorBimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 2 && x.IdTurno == Turno).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorBimestre = 0;
                foreach (int Monto in becasPorBimestre)
                {
                    sumaBecasPorBimestre = sumaBecasPorBimestre + Monto;
                }
                sumaBecasPorBimestre = sumaBecasPorBimestre * 2;
                List<int> becasPorCuatrimestre = data.Estudiantes.Where(x => x.IdCarrera == Id && x.IdBeca > 0 && x.MesesBeca == 4 && x.IdTurno == Turno).Select(x => x.MontoBeca).ToList();
                int sumaBecasPorCuatrimestre = 0;
                foreach (int Monto in becasPorCuatrimestre)
                {
                    sumaBecasPorCuatrimestre = sumaBecasPorCuatrimestre + Monto;
                }
                montoTotal = sumaBecasPorMes + sumaBecasPorBimestre + sumaBecasPorCuatrimestre;
            }
            return montoTotal;
        }
    }
}