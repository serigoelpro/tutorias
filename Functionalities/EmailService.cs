using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web.Configuration;

namespace ProyectoIntegracion.Functionalities
{
    // Helper centralizado de envio de correo. Lee la config SMTP de appSettings;
    // si falta una clave, cae a los valores de Gmail ya usados en el proyecto.
    public static class EmailService
    {
        private static string Cfg(string key, string def)
        {
            var v = WebConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? def : v;
        }

        public static void Enviar(string asunto, string cuerpoHtml, IEnumerable<string> destinatarios)
        {
            Enviar(asunto, cuerpoHtml, destinatarios, null, null);
        }

        public static void Enviar(string asunto, string cuerpoHtml, IEnumerable<string> destinatarios, byte[] adjunto, string nombreAdjunto)
        {
            // Kill-switch: en local Correo.Habilitado=false — no se abre conexion SMTP jamas.
            if (!bool.TryParse(Cfg("Correo.Habilitado", "false"), out var correoOn) || !correoOn) return;

            var dest = (destinatarios ?? Enumerable.Empty<string>())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (dest.Count == 0) return;

            var host = Cfg("Smtp.Host", "smtp.gmail.com");
            var port = int.Parse(Cfg("Smtp.Port", "587"));
            var user = Cfg("Smtp.User", "uttn.tutorias@gmail.com");
            var pass = Cfg("Smtp.Password", "xhxrfrfpxhaipohw");
            var from = Cfg("Smtp.From", user);
            var ssl  = bool.Parse(Cfg("Smtp.EnableSsl", "true"));

            using (var m = new MailMessage())
            {
                m.From = new MailAddress(from, "Tutorías UTTN");
                foreach (var d in dest) m.To.Add(d);
                m.Subject = asunto;
                m.Body = cuerpoHtml;
                m.IsBodyHtml = true;
                if (adjunto != null && adjunto.Length > 0)
                    m.Attachments.Add(new Attachment(new System.IO.MemoryStream(adjunto), nombreAdjunto ?? "adjunto.pdf", "application/pdf"));
                using (var smtp = new SmtpClient(host, port))
                {
                    smtp.Credentials = new NetworkCredential(user, pass);
                    smtp.EnableSsl = ssl;
                    smtp.Timeout = int.Parse(Cfg("Smtp.TimeoutMs", "30000"));
                    smtp.Send(m);
                }
            }
        }

        // Envio best-effort en segundo plano. Aisla al request del cuelgue de red SMTP:
        // en .NET Framework SmtpClient.Timeout NO acota DNS ni la conexion TCP, asi que sin
        // salida a internet Send() puede colgarse mucho mas alla del Timeout y congelar el
        // request (visto en local: EjecutarCorteProgramado dejo el worker sin responder).
        // Los errores se tragan: el llamador ya persistio su trabajo antes de notificar.
        public static void EnviarEnSegundoPlano(string asunto, string cuerpoHtml, IEnumerable<string> destinatarios, byte[] adjunto = null, string nombreAdjunto = null)
        {
            var dest = (destinatarios ?? Enumerable.Empty<string>()).ToList();
            System.Threading.Tasks.Task.Run(() =>
            {
                try { Enviar(asunto, cuerpoHtml, dest, adjunto, nombreAdjunto); }
                catch { }
            });
        }
    }
}
