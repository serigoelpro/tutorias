using System;
using System.ComponentModel.DataAnnotations;

namespace Plataforma_Web.Models.UsuarioAlumnos
{
    public class AlumnoGestion
    {
        public int IdAlumno { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El apellido paterno es requerido")]
        [Display(Name = "Apellido Paterno")]
        public string ApellidoPaterno { get; set; }

        [Required(ErrorMessage = "El apellido materno es requerido")]
        [Display(Name = "Apellido Materno")]
        public string ApellidoMaterno { get; set; }

        [Required(ErrorMessage = "La matrícula es requerida")]
        [Display(Name = "Matrícula")]
        public string Matricula { get; set; }

        [Display(Name = "Correo Electrónico")]
        public string CorreoElectronico { get; set; }

        public string Contrasena { get; set; }
        public int IdCarrera { get; set; }
        public int Cuatrimestre { get; set; }
        public bool RegistradoEstadias { get; set; }
        public bool Habilitado { get; set; }
        public DateTime? FechaRegistro { get; set; }
        public DateTime? FechaSesion { get; set; }
        public string MicrosoftIdentifier { get; set; }
        public string TokenSesion { get; set; }

        // NUEVA PROPIEDAD PARA EL NOMBRE DE LA CARRERA
        [Display(Name = "Carrera")]
        public string CarreraNombre { get; set; }
    }
}