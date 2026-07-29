using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    [Table("Alumnos")]
    public class AlumnoGestion
    {
        [Key]
        public int IdAlumno { get; set; }

        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Display(Name = "Apellido Paterno")]
        public string ApellidoPaterno { get; set; }

        [Display(Name = "Apellido Materno")]
        public string ApellidoMaterno { get; set; }

        [Display(Name = "Matrícula")]
        public string Matricula { get; set; }

        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; }

        [Display(Name = "Correo Electrónico")]
        public string CorreoElectronico { get; set; }

        [Display(Name = "Carrera")]
        public int IdCarrera { get; set; }

        [Display(Name = "Cuatrimestre")]
        public int Cuatrimestre { get; set; }

        [Display(Name = "Registrado Estadías")]
        public bool RegistradoEstadias { get; set; }

        [Display(Name = "Habilitado")]
        public bool Habilitado { get; set; }

        [Display(Name = "Fecha Registro")]
        public DateTime? FechaRegistro { get; set; }

        [Display(Name = "Fecha Sesión")]
        public DateTime? FechaSesion { get; set; }

        [Display(Name = "Microsoft ID")]
        public string MicrosoftIdentifier { get; set; }

        public string TokenSesion { get; set; }

        [NotMapped]
        [Display(Name = "Nombre Completo")]
        public string NombreCompleto => $"{Nombre} {ApellidoPaterno} {ApellidoMaterno}".Trim();
    }
}