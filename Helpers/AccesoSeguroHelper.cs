using System;
using System.Web;

namespace PlataformaWeb.Helpers
{
    public static class AccesoSeguroHelper
    {
        private const string SESSION_KEY = "AccesoAlumnoToken";

        // Generar un token único para acceder a un alumno específico
        public static string GenerarToken(int idPersona, int idUsuario)
        {
            string token = Guid.NewGuid().ToString();
            var tokenData = new
            {
                IdPersona = idPersona,
                IdUsuario = idUsuario,
                FechaCreacion = DateTime.Now,
                Token = token
            };

            // Guardar en sesión
            if (HttpContext.Current.Session[SESSION_KEY] == null)
            {
                HttpContext.Current.Session[SESSION_KEY] = new System.Collections.Generic.Dictionary<string, object>();
            }

            var tokens = HttpContext.Current.Session[SESSION_KEY] as System.Collections.Generic.Dictionary<string, object>;
            tokens[token] = tokenData;

            return token;
        }

        // Validar que el token es válido para el idPersona solicitado
        public static bool ValidarToken(string token, int idPersona, int idUsuario)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            if (HttpContext.Current.Session[SESSION_KEY] == null)
                return false;

            var tokens = HttpContext.Current.Session[SESSION_KEY] as System.Collections.Generic.Dictionary<string, object>;

            if (!tokens.ContainsKey(token))
                return false;

            dynamic tokenData = tokens[token];

            // Verificar que el token no haya expirado (5 minutos)
            if ((DateTime.Now - tokenData.FechaCreacion).TotalMinutes > 5)
            {
                tokens.Remove(token);
                return false;
            }

            // Verificar que coincida el idPersona y el usuario
            return tokenData.IdPersona == idPersona && tokenData.IdUsuario == idUsuario;
        }

        // Limpiar tokens expirados
        public static void LimpiarTokens()
        {
            if (HttpContext.Current.Session[SESSION_KEY] != null)
            {
                var tokens = HttpContext.Current.Session[SESSION_KEY] as System.Collections.Generic.Dictionary<string, object>;
                var tokensAEliminar = new System.Collections.Generic.List<string>();

                foreach (var kvp in tokens)
                {
                    dynamic tokenData = kvp.Value;
                    if ((DateTime.Now - tokenData.FechaCreacion).TotalMinutes > 5)
                    {
                        tokensAEliminar.Add(kvp.Key);
                    }
                }

                foreach (var tokenKey in tokensAEliminar)
                {
                    tokens.Remove(tokenKey);
                }
            }
        }
    }
}