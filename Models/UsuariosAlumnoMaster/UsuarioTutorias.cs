using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    [Table("Usuarios")]
    public class UsuarioTutorias
    {
        [Key]
        public int IdUsuario { get; set; }

        [Display(Name = "Nombre Completo")]
        public string NombreCompleto { get; set; }

        [Display(Name = "Usuario")]
        public string UserName { get; set; }

        public string Password { get; set; }

        [Display(Name = "Carrera")]
        public int IdCarrera { get; set; }

        [Display(Name = "Nivel")]
        public int IdNivel { get; set; }

        [Display(Name = "Estado")]
        public bool Estado { get; set; }

        [Display(Name = "Fecha Registro")]
        public DateTime Tiempo { get; set; }

        [Display(Name = "Correo Electrónico")]
        public string CorreoElectronico { get; set; }

        [Display(Name = "Microsoft ID")]
        public string MicrosoftIdentifier { get; set; }
    }
}