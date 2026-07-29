using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace PlataformaWeb.BecasTransporte.Models
{
    public class Estudiante
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Nombre")]
        public string Nombre { get; set; }

        [Required]
        [DisplayName("Apellido Paterno")]
        public string ApellidoP { get; set; }
        [Required]
        [DisplayName("Apellido Materno")]
        public string ApellidoM { get; set; }

        [Required]
        [DisplayName("Matricula del alumno")]
        public string Matricula { get; set; }

        [DisplayName("Sexo")]
        public string Sexo { get; set; }

        [Required(ErrorMessage ="Seleccione una ruta de transporte.")]
        [DisplayName("Ruta de transporte")]
        public int IdTransporte { get; set; }

        [DisplayName("Tipo de beca")]
        [Required(ErrorMessage = "Seleccione un tipo de beca.")]
        public int IdBeca { get; set; }

        [DisplayName("Monto")]
        [Required(ErrorMessage = "Ingrese el monto de la beca.")]
        public int MontoBeca { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public int IdCarrera { get; set; }

        [Required]
        [DisplayName("Grado")]
        public int IdGrado { get; set; }

        [Required]
        [DisplayName("Grupo")]
        public int IdGrupo { get; set; }

        [Required]
        [DisplayName("Turno")]
        public int IdTurno { get; set; }

        [Required]
        public string Direccion { get; set; }

        [DisplayName("Descripción de la beca")]
        [DataType(DataType.MultilineText)]
        public string DetallesBecaEstudiante { get; set; }

        //public string Latitud { get; set; }
        //public string Longitud { get; set; }

        [Required]
        [DisplayName("¿Cada cuántos meses recibe la beca el estudiante?")]
        public int MesesBeca { get; set; }


        [DisplayName("Ciudad")]
        public int? IdCiudad { get; set; }

        [DisplayName("Calle")]
        public string Calle { get; set; }

        [DisplayName("Numero")]
        public string NumeroDireccion { get; set; }

        [DisplayName("Colonia")]
        public int? IdColonia { get; set; }

        public int periodoActual { get; set; }

        public int Año { get; set; }

        public string Especialidad { get; set; }
    }
}