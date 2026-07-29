using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Usuario
    {
        [Key]

        public int IdUsuario { get; set; }


        [DisplayName("Nombre completo:")]
        [Required(ErrorMessage = "El correo es obligatorio")]
        public string NombreCompleto { get; set; }//correo para alumnos//nombre para tutores

        [Required(ErrorMessage = "La matricula es obligatoria")]
        [DisplayName("Usuario:")]
        public string UserName { get; set; }//matricula para alumnos//usuario para tutores

        [Required]
        [DisplayName("Contraseña:")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DisplayName("Correo Electrónico:")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        public string CorreoElectronico { get; set; }

        [DisplayName("Contraseña")]
        public int IdCarrera { get; set; }

        [DisplayName("Nivel de acceso")]
        [Required]
        public int IdNivel { get; set; }
        public virtual Nivel Nivel { get; set; }

        [DisplayName("Tiempo")]
        public DateTime Tiempo { get; set; }

        [NotMapped]
        [DisplayName("Nombre de Carrera")]
        public string NombreCarrera { get; set; }

        [DisplayName("Estado")]
        [Required]
        public bool Estado { get; set; }

        [ScaffoldColumn(false)]
        public string MicrosoftIdentifier { get; set; }

        [ScaffoldColumn(false)]
        [DisplayName("Director (solo lectura)")]
        public bool EsDirector { get; set; }
    }
}