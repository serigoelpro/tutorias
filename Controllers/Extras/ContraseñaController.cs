using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.Extras
{
    [CustomAuthorize(Nivel = 99)]
    public class ContraseñaController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        // GET: Contraseña
        public ActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index(Usuario usuario, string CorreoAlt)
        {
            if(usuario.UserName != null)
            {

                var datos = db.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName && x.NombreCompleto.ToLower().Trim()==usuario.NombreCompleto.ToLower().Trim());
                DatosPersonales personal = new DatosPersonales();
                if (datos != null)
                {
                    datos.Password = Security.Desencripta(datos.Password);
                    personal = db.DatosPersonales.FirstOrDefault(x => x.Matricula == datos.UserName);
                }
                else
                {
                    ViewBag.Mensaje = "La matricula y correo proporcionados no coinciden con ningun usuario, favor de revisar los datos proporcionados o registrarse en caso de no haberlo hecho.";
                    return View();
                }
                string asunto = "Recuperación de contraseña";
                string titulo = "Credenciales";
                try { 
                    System.Net.Mail.MailMessage m = new System.Net.Mail.MailMessage(
                    new System.Net.Mail.MailAddress("uttn.tutorias@gmail.com", asunto),
                    //new System.Net.Mail.MailAddress("renato.barreras@uttn.mx"));
                    new System.Net.Mail.MailAddress(datos.NombreCompleto));
                    m.To.Add(CorreoAlt);
                    if(personal!=null)
                    {
                        m.To.Add("direccionti@uttn.mx");
                        if (personal.IdCarrera == 1)
                        {
                            m.To.Add("myriam.benitez@uttn.mx");
                        }
                        else if (personal.IdCarrera == 2)
                        {
                            m.To.Add("Claudia.delarosa@uttn.mx");
                        }
                        else if (personal.IdCarrera == 3)
                        {
                            m.To.Add("alma.lechuga@uttn.mx");
                        }
                        else if (personal.IdCarrera == 4)
                        {
                            m.To.Add("carmen.garcia@uttn.mx");
                        }
                        else if (personal.IdCarrera == 5)
                        {
                            m.To.Add("silvia.dorado@uttn.mx");
                        }
                        else if (personal.IdCarrera == 6)
                        {
                            m.To.Add("alma.lechuga@uttn.mx");
                        }
                        else if (personal.IdCarrera == 7)
                        {
                            m.To.Add("myriam.benitez@uttn.mx");
                        }
                        else if (personal.IdCarrera == 8)
                        {
                            m.To.Add("carmen.garcia@uttn.mx");
                        }
                    }
                    
                    
                    m.Subject = titulo;
                    string errorMessage = "Lo sentimos, No existe un usuario registrado con el correo proporcionado.";
                    if (datos == null)
                    {
                        m.Body = string.Format("<h3>Estimado: Alumno</h3> <br/><br/> A decidido recuperar sus credenciales para acceder a la pagina de tutorias" +
                        " a continuación se muestran las credenciales que corresponden de acuerdo a la informacion proporcionada anteriormente: {0}", errorMessage);

                    }
                    else
                    {
                        m.Body = string.Format("<h3>Estimado: Alumno</h3> <br/><br/> A decidido recuperar sus credenciales para acceder a la pagina de tutorias" +
                       " a continuación se muestran las credenciales que corresponden de acuerdo a la informacion proporcionada anteriormente. <br/>" +
                       "Su usuario es: {0}. <br/>" +
                       "Su contraseña es: {1}<br/><br/> <h4>Tu contraseña no puede ser cambiada por el momento, te sugerminos guerdes estos datos para futuros inicios de sesión</h4>", datos.UserName, datos.Password);

                    }
                    m.IsBodyHtml = true;

                    System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com");
                    smtp.Credentials = new System.Net.NetworkCredential("uttn.tutorias@gmail.com", "xhxrfrfpxhaipohw");// "hyzmsyrgdzlshjkl"); // "Uttn@Tutorias");

                    //Este solo funciona para el uso del servicio del smtp de Gmail
                    smtp.Port = 587;
                    smtp.EnableSsl = true;
                    smtp.Send(m);

                    // Uncomment to debug locally 
                    // TempData["ViewBagLink"] = callbackUrl;
                }
                catch (Exception e) { 
                    ViewBag.Mensaje = "Favor de introducir un correo valido. En caso de no registrarse con un correo valido, favor de volver a registrarse";
                    return View();
                }
                return RedirectToAction("Login", "Home");             
            }
            return View(usuario);
        }
        
    }
}