using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Administrativo
    {
        [Key]
        public int IdAdministrativo { get; set; }

        [Required]
        [DisplayName("Nombre")]
        public string Nombre { get; set; }

        [Required]
        [DisplayName("Edad")]
        public int Edad { get; set; }

        [Required]
        [DisplayName("Direccion")]
        public string Direccion { get; set; }

        [DisplayName("Celular")]
        [DataType(DataType.PhoneNumber)]
        [MinLength(7)]
        [MaxLength(10)]
        public string Celular { get; set; }

        [Required]
        [DisplayName("Numero de Empleado")]
        public string NumeroEmpleado { get; set; }

        [Required]
        [DisplayName("Usuario")]
        public int IdUsuario { get; set; }
        public string  Usuario{ get; set; }

    }
}