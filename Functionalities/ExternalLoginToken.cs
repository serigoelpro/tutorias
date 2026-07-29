using ProyectoIntegracion.Models.GestionUsuarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoIntegracion.Functionalities
{
    public class ExternalLoginToken
    {
        public static string Save(string data)
        {
            string token = Guid.NewGuid().ToString("N");

            // Save in database
            usuarios_model_db db = new usuarios_model_db();
            db.Sesiones.Add(new Sesion
            {
                Clave = token,
                Valor = data
            });
            db.SaveChanges();

            return token;
        }

        public static string Consume(string token)
        {
            // Get session corresponding to token
            usuarios_model_db db = new usuarios_model_db();
            var sesion = db.Sesiones.FirstOrDefault(x => x.Clave == token);

            // Invalid token
            if (sesion == null)
                return null;

            // Get value and delete from database
            string id = Cypher.Decrypt(sesion.Valor);
            db.Sesiones.Remove(sesion);
            db.SaveChanges();

            // Session is expired
            if (DateTime.Now > sesion.Caducidad)
                return null;

            // Success in session
            return id;
        }
    }
}