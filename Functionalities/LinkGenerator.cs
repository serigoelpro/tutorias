using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.WebPages;

namespace ProyectoIntegracion.Functionalities
{
    /// <summary>
    /// Static class used to create a url. Use method called `Create`.
    /// </summary>
    public static class LinkGenerator
    {
        public static string Domain = ConfigurationManager.AppSettings["CookieDomain"];
        public static string SubDomain = ConfigurationManager.AppSettings["SubDomain"];
        public static string Port = ConfigurationManager.AppSettings["Port"] ?? "";
        public static string Scheme = ConfigurationManager.AppSettings["Scheme"] ?? "https";

        /// <summary>
        /// Method used to create a link to a specific view of another sub domain in the same main domain.
        /// </summary>
        /// <param name="subDomain">It indicates the sub domain of the separate module.</param>
        /// <param name="viewPath">The path to the view in the sub domain to link. Use the structure "Model/View". No need of adding a "/" at the beginning.</param>
        /// <returns>A string with the url in the sub domain following the path.</returns>
        public static string Create(string subDomain, string viewPath)
        {
            string port = Port.IsEmpty() ? "" : String.Concat(":", Port);
            return String.Concat(Scheme, "://", subDomain, Domain, port, "/", viewPath);
        }

        /// <summary>
        /// Method used to create a link to a specific view of the actual sub domain.
        /// </summary>
        /// <param name="viewPath">The path to the view in the sub domain to link. Use the structure "Model/View". No need of adding a "/" at the beginning.</param>
        /// <returns>A string with the url in the actual sub domain following the path.</returns>
        public static string Create(string viewPath)
        {
            string port = Port.IsEmpty() ? "" : String.Concat(":", Port);
            return String.Concat(Scheme, "://", SubDomain, Domain, port, "/", viewPath);
        }
    }
}