using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PlataformaWeb.Controllers.Extras
{
    [CustomAuthorize(Nivel = 2)]
    public class ReportarController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        // GET: Reporte
        public ActionResult Index(int? id)
        {
            ViewBag.id= id;
            return View();
        }
        public ActionResult Opcion1(int? id)
        {
            Usuario user = Session["Usuario"] as Usuario;
            DatosPersonales datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
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
            TutoriaGrupal tutgrup;
            if (user.IdNivel >= 3)
            {
                tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == datos.IdPeriodo && x.Año == datos.Año);
            }
            else
            {
                tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == datos.IdPeriodo && x.Año == datos.Año && x.IdUsuario == user.IdUsuario);
            }
            ViewBag.grup = tutgrup?.IdTutoriaGrupal ?? 0;
            ViewBag.id = id;
            return View();
        }
        public ActionResult Opcion2(int? id)
        {
            ViewBag.id = id;
            return View();
        }
        public ActionResult Editar(int? id)
        {
            if (id != null)
            {
                Usuario user = Session["Usuario"] as Usuario;
                var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);                
                if (datos == null)
                {
                    ViewBag.Mensaje = "El alumno no se encontro, favor de actualizar la pagina y volver a intentarlo";
                    return View();
                }
                try
                {
                    string asunto = "Entrevista Inical";
                    string titulo = "Universidad Tecnologica";
                    System.Net.Mail.MailMessage m = new System.Net.Mail.MailMessage(
                    new System.Net.Mail.MailAddress("uttn.tutorias@gmail.com", asunto),
                    new System.Net.Mail.MailAddress(datos.Email));
                    
                    if (datos.IdCarrera == 1)
                    {
                        m.To.Add("myriam.benitez@uttn.mx");
                    }
                    else if (datos.IdCarrera == 2)
                    {
                        m.To.Add("Claudia.delarosa@uttn.mx");
                    }
                    else if (datos.IdCarrera == 3)
                    {
                        m.To.Add("alma.lechuga@uttn.mx");
                    }
                    else if (datos.IdCarrera == 4)
                    {
                        m.To.Add("rosalinda.sanchez@uttn.mx");
                    }
                    else if (datos.IdCarrera == 5)
                    {
                        m.To.Add("silvia.dorado@uttn.mx");
                    }
                    else if (datos.IdCarrera == 6)
                    {
                        m.To.Add("alma.lechuga@uttn.mx");
                    }
                    m.Subject = titulo;


                    m.Body = string.Format("<h3>Estimado: {0}</h3> <br/><br/> Hay un problema con tu entrevista inicial, te pedimos que la actualices a la brevedad" +
                   " si tienes duda sobre el problema, consulta con su asesor, el nombre de tu tutor es: {1}" +
                   "<br/>" +
                   "Tu asesor sabra decirte cual es el problema<br/><br/>" +
                   "Ingrese a la página: 201.174.6.168/Tutorias.", datos.Nombre, user.NombreCompleto);


                    m.IsBodyHtml = true;

                    System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com");
                    smtp.Credentials = new System.Net.NetworkCredential("uttn.tutorias@gmail.com", "Uttn@Tutorias");

                    //Este solo funciona para el uso del servicio del smtp de Gmail
                    smtp.Port = 587;
                    smtp.EnableSsl = true;
                    smtp.Send(m);
                    ViewBag.Mensaje = "El reporte se realizo con exito, el mensaje fue enviado EXITOSAMENTE";
                    // Uncomment to debug locally 
                    // TempData["ViewBagLink"] = callbackUrl;
                }
                catch
                {
                    ViewBag.Mensaje = "El alumno escribio un correo no valido en sus datos personales, favor de contactar al alumno por otros medios para la correccion de su correo.";
                    
                }
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
                TutoriaGrupal tutgrup;
                if (user.IdNivel >= 3)
                {
                    tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);
                }
                else
                {
                    tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdUsuario == user.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);
                }
                ViewBag.id = tutgrup?.IdTutoriaGrupal ?? 0;
                return View();
            }
            ViewBag.Mensaje = "Ocurrio un error al intentar recuperar los datos del alumno, actualice la pagina e intente de nuevo por favor";
            return View();
        }
        public ActionResult Terminar(int? id)
        {

            if (id != null)
            {
                Usuario user = Session["Usuario"] as Usuario;
                var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                if (datos == null)
                {
                    ViewBag.Mensaje = "El alumno no se encontro, favor de actualizar la pagina y volver a intentarlo";
                    return View();
                }
                try
                {
                    string asunto = "Entrevista Inical";
                    string titulo = "Universidad Tecnologica";
                    System.Net.Mail.MailMessage m = new System.Net.Mail.MailMessage(
                    new System.Net.Mail.MailAddress("uttn.tutorias@gmail.com", asunto),
                    new System.Net.Mail.MailAddress(datos.Email));
                    
                    if (datos.IdCarrera == 1)
                    {
                        m.To.Add("myriam.benitez@uttn.mx");
                    }
                    else if (datos.IdCarrera == 2)
                    {
                        m.To.Add("Claudia.delarosa@uttn.mx");
                    }
                    else if (datos.IdCarrera == 3)
                    {
                        m.To.Add("alma.lechuga@uttn.mx");
                    }
                    else if (datos.IdCarrera == 4)
                    {
                        m.To.Add("rosalinda.sanchez@uttn.mx");
                    }
                    else if (datos.IdCarrera == 5)
                    {
                        m.To.Add("silvia.dorado@uttn.mx");
                    }
                    else if (datos.IdCarrera == 6)
                    {
                        m.To.Add("alma.lechuga@uttn.mx");
                    }
                    m.Subject = titulo;


                    m.Body = string.Format("<h3>Estimado: {0}</h3> <br/><br/> <h4>Hay un problema con tu entrevista inicial</h4>" +
                   " <br/>Al parecer tu entrevista esta incompleta, te pedimos que la termines a la brevedad." +
                   "<br/>" +
                   "A continuacion se muestran una serie de pasos para que termines de ingresar los datos faltantes<br/><br/> <h3>Pasos a seguir</h3><br/>" +
                   "<h4>1.- Ingrese a la página: 201.174.6.168/Tutorias.</h4><br/>" +
                   "<h4>2.- Inicie sesion en la pagina, si no recuerda la contraseña puede recuperarla.</h4><br/>" +
                   "<h4>3.- Seleccione Solicitud de entrevista inicial en la lista desplegable de formatos.</h4><br/>" +
                   "<h4>4.- Seleccione la opcion de inicial.</h4><br/>" +
                   "<h4>5.- LLene todo el formulario, no olvides tu matricula.</h4><br/>" +
                   "<h4>6.- Seras redireccionado hasta donde te quedaste, continua hasta que veas un mensaje de entrevsta completa.</h4><br/>" +
                   "", datos.Nombre);


                    m.IsBodyHtml = true;

                    System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com");
                    smtp.Credentials = new System.Net.NetworkCredential("uttn.tutorias@gmail.com", "Uttn@Tutorias");

                    //Este solo funciona para el uso del servicio del smtp de Gmail
                    smtp.Port = 587;
                    smtp.EnableSsl = true;
                    smtp.Send(m);
                    ViewBag.Mensaje = "El reporte se realizo con exito, el mensaje fue enviado EXITOSAMENTE";
                    // Uncomment to debug locally 
                    // TempData["ViewBagLink"] = callbackUrl;
                }
                catch
                {
                    ViewBag.Mensaje = "El alumno escribio un correo no valido en sus datos personales, favor de contactar al alumno por otros medios para la correccion de su correo.";
                  
                }
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
                TutoriaGrupal tutgrup;
                if (user.IdNivel >= 3)
                {
                    tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);
                }
                else
                {
                    tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdUsuario == user.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);
                }
                ViewBag.id = tutgrup?.IdTutoriaGrupal ?? 0;
                return View();
            }
            ViewBag.Mensaje = "Ocurrio un error al intentar recuperar los datos del alumno, actualice la pagina e intente de nuevo por favor";
            return View();
        }
        public ActionResult Eliminar(int? id)
        {

            if (id != null)
            {
                Usuario user = Session["Usuario"] as Usuario;
                var datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                if (datos == null)
                {
                    ViewBag.Mensaje = "El alumno no se encontro, favor de actualizar la pagina y volver a intentarlo";
                    return View();
                }

                // Capturar el grupo original antes de limpiar los IDs para mantener la navegación de regreso.
                TutoriaGrupal tutgrupOriginal;
                if (user.IdNivel >= 3)
                {
                    tutgrupOriginal = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == datos.IdPeriodo && x.Año == datos.Año);
                }
                else
                {
                    tutgrupOriginal = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == datos.IdCarrera && x.IdGrado == datos.IdGrado && x.IdGrupo == datos.IdGrupo && x.IdTurno == datos.IdTurno && x.IdPeriodo == datos.IdPeriodo && x.Año == datos.Año && x.IdUsuario == user.IdUsuario);
                }
                ViewBag.id = tutgrupOriginal?.IdTutoriaGrupal ?? 0;

                // Quitar del grupo primero — el envío de correo es best-effort y no debe bloquear la baja.
                datos.IdGrado = 0;
                datos.IdGrupo = 0;
                datos.IdTurno = 0;
                db.Entry(datos).State = EntityState.Modified;
                db.SaveChanges();

                try
                {
                    string asunto = "Entrevista Inical";
                    string titulo = "Universidad Tecnologica";
                    System.Net.Mail.MailMessage m = new System.Net.Mail.MailMessage(
                    new System.Net.Mail.MailAddress("uttn.tutorias@gmail.com", asunto),
                    new System.Net.Mail.MailAddress(datos.Email));

                    if (datos.IdCarrera == 1)
                    {
                        m.To.Add("myriam.benitez@uttn.mx");
                    }
                    else if (datos.IdCarrera == 2)
                    {
                        m.To.Add("Claudia.delarosa@uttn.mx");
                    }
                    else if (datos.IdCarrera == 3)
                    {
                        m.To.Add("alma.lechuga@uttn.mx");
                    }
                    else if (datos.IdCarrera == 4)
                    {
                        m.To.Add("rosalinda.sanchez@uttn.mx");
                    }
                    else if (datos.IdCarrera == 5)
                    {
                        m.To.Add("silvia.dorado@uttn.mx");
                    }
                    else if (datos.IdCarrera == 6)
                    {
                        m.To.Add("alma.lechuga@uttn.mx");
                    }
                    m.Subject = titulo;
                    m.Body = string.Format("<h3>Estimado alumno: {0}</h3> con matricula: {1}<br/><br/> Hubo un problema con tu entrevista" +
                   " <br/>Al parecer tu entrevista no fue llenada correctamente o contiene datos no actualizados" +
                   "<br/>" +
                   "Es necesario que ingreses en la pagina 201.174.6.168/Tutorias y actualices o corrijas tus datos a la brevedad posible.<br/>" +
                   "En caso de tener dudas sobre este mensaje, favor de ponerte en contacto con tu tutor academico.<br/>" +
                   "", datos.Nombre,datos.Matricula);
                    m.IsBodyHtml = true;

                    System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com");
                    smtp.Credentials = new System.Net.NetworkCredential("uttn.tutorias@gmail.com", "Uttn@Tutorias");

                    //Este solo funciona para el uso del servicio del smtp de Gmail
                    smtp.Port = 587;
                    smtp.EnableSsl = true;
                    smtp.Send(m);
                    ViewBag.Mensaje = "El reporte se realizo con exito, el mensaje fue enviado y el alumno removido del grupo EXITOSAMENTE";
                }
                catch
                {
                    ViewBag.Mensaje = "No fue posible notificar al alumno por correo, pero el alumno fue removido del grupo EXITOSAMENTE.";
                }
                //if (datos7!=null)
                //{
                //    foreach(Individual i in datos7)
                //    {
                //        List<Seguimiento> datos8 = db.Seguimientoes.Where(x => x.IdIndividual == i.IdIndividual).ToList();
                //        if(datos8!=null)
                //        {
                //            foreach(Seguimiento s in datos8)
                //            {
                //                db.Seguimientoes.Remove(s);
                //                db.SaveChanges();
                //            }
                //        }

                //        db.Individuals.Remove(i);
                //        db.SaveChanges();
                //    }
                //}
                //if (datos6!=null)
                //{
                //    foreach(Baja b in datos6)
                //    {
                //        db.Bajas.Remove(b);
                //        db.SaveChanges();
                //    }                    
                //}
                //if (datos5 != null)
                //{
                //    db.EntrevistaInicials.Remove(datos5);
                //    db.SaveChanges();
                //}
                //if (datos4 != null)
                //{
                //    db.AspectosPersonales.Remove(datos4);
                //    db.SaveChanges();
                //}
                //if (datos3 != null)
                //{
                //    db.AspectosEconomicos.Remove(datos3);
                //    db.SaveChanges();
                //}
                //if (datos2 != null)
                //{
                //    db.AspectosAcademicos.Remove(datos2);
                //    db.SaveChanges();
                //}
                //if (datos1 != null)
                //{
                //    db.DatosPersonales.Remove(datos1);
                //    db.SaveChanges();
                //}

                return View();
            }
            ViewBag.Mensaje = "Ocurrio un error al intentar recuperar los datos del alumno, favor de actualizar la pagina e intente de nuevo por favor";
            return View();
        }
    }
}