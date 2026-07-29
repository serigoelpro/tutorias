using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    [Table("Usuario1")]
    public class UsuarioEstadias
    {
        [Key]
        public int IdUsuario { get; set; }

        [Display(Name = "Perfil")]
        public int IdPerfil { get; set; }

        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Display(Name = "Apellido Paterno")]
        public string Paterno { get; set; }

        [Display(Name = "Apellido Materno")]
        public string Materno { get; set; }

        [Display(Name = "Usuario")]
        public string Username { get; set; }

        public string Contraseña { get; set; }

        [Display(Name = "Correo Electrónico")]
        public string CorreoElectronico { get; set; }

        [Display(Name = "Área")]
        public int? IdArea { get; set; }

        [Display(Name = "Estado")]
        public bool? Estado { get; set; }

        [Display(Name = "Microsoft ID")]
        public string MicrosoftIdentifier { get; set; }

        [NotMapped]
        [Display(Name = "Nombre Completo")]
        public string NombreCompleto => $"{Nombre} {Paterno} {Materno}".Trim();
    }
}