using System;
using System.ComponentModel.DataAnnotations;

namespace Plataforma_Web.Models.UsuarioAlumnos
{
    public class UsuarioAlumno
    {
        public int IdUsuario { get; set; }

        [Display(Name = "Nombre Completo")]
        public string NombreCompleto { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [Display(Name = "Nombre de Usuario")]
        public string UserName { get; set; }

        public int IdCarrera { get; set; }
        public int IdNivel { get; set; }
        public bool Estado { get; set; }
        public DateTime Tiempo { get; set; }
        public string CorreoElectronico { get; set; }
        public string MicrosoftIdentifier { get; set; }
    }
}