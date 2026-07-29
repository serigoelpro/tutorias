using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Alumno
    {
        //Datos del alumno 
        [Key]
        public int IdAlumno { get; set; }

        [Required]
        [DisplayName("Matricula")]
        public string Matricula { get; set; }

        [Required]
        [DisplayName("Nombre")]
        public string Nombre { get; set; }

        [Required]
        [DisplayName("Edad")]
        public int Edad { get; set; }

        //======Checar porque este valor se comunica con la clase de GRUPOS
        [Required]
        [DisplayName("grupo")]
        public int IdGrupo { get; set; }
        public virtual Grupo Grupo { get; set; }


        [Required]
        [DisplayName("Direccion")]
        public string Direccion { get; set; }

        [DisplayName("Celular")]
        [DataType(DataType.PhoneNumber)]
        [MinLength(7)]
        [MaxLength(10)]
        public string Celular { get; set; }

        [DisplayName("Telefono")]
        [DataType(DataType.PhoneNumber)]
        [MinLength(7)]
        [MaxLength(10)]
        public string Telefono { get; set; }

        [DisplayName("TelefonoEmergencia")]
        [DataType(DataType.PhoneNumber)]
        [MinLength(7)]
        [MaxLength(10)]
        public string TelEmergencia { get; set; }

        [Required]
        [DisplayName("Correo")]
        public string Email{ get; set; }

        //[DisplayName("Numero seguro social")]
        //[MinLength(11)]
        //public int NumSS { get; set; }

        [DisplayName("CURP")]
        [MinLength(18)]
        public string CURP { get; set; }

        [Required]
        [DisplayName("Usuario")]
        public int IdUsuario { get; set; }
        [NotMapped]
        public string Usuario { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public int IdCarrera { get; set; }
        [NotMapped]
        public string Carrera { get; set; }
    }
}