using Plataforma_Web.Models.ClasesExtras;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.PrimeraEntrevista
{
    public class DatosPersonales
    {
        [Key]
        public int IdPersona { get; set; }



        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }

        [Required]
        [DisplayName("Matricula")]
        public string Matricula { get; set; }


        [DisplayName("Nombre")]
        public string Nombre { get; set; }
        [Required]
        [DisplayName("Nombre")]
        public virtual string Nom { get; set; }
        [Required]
        [DisplayName("Apellido paterno")]
        public virtual string Paterno { get; set; }
        [Required]
        [DisplayName("Apellido materno")]
        public virtual string Materno { get; set; }

        [Required]
        [DisplayName("Edad")]
        public int Edad { get; set; }

        [Required]
        [DisplayName("Turno")]
        public int IdTurno { get; set; }
        public virtual Turno Turno { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public int IdCarrera { get; set; }
        public virtual Carrera Carrera { get; set; }

        [Required]
        [DisplayName("Grupo")]
        public int IdGrupo { get; set; }
        public virtual Grupo Grupo { get; set; }

        [Required]
        [DisplayName("Grado")]
        public int IdGrado { get; set; }
        public virtual Grado Grado { get; set; }

        [DisplayName("Direccion")]
        public string Direccion { get; set; }

        [Required]
        [DisplayName("Teléfono celular")]
        public string Celular { get; set; }

        [Required]
        [DisplayName("Teléfono de casa")]
        public string Telefono { get; set; }

        [Required]
        [DisplayName("Teléfono de Emergencia")]
        public string TelEmergencia { get; set; }

        [Required]
        [EmailAddress]
        [DisplayName("Correo Electrónico")]
        public string Email { get; set; }

        [Display(Name = "Foto")]
        public string Foto { get; set; }

        //private string foto { get; set; }
        //[Display(Name = "Foto")]
        //public string Foto { get {
        //        return foto.Replace("../../", "http://201.174.6.168/Tutorias/");
        //    } set {foto = value;} }


        [NotMapped]
        public HttpPostedFileBase FotoFile { get; set; }


        [DisplayName("Estatus")]
        public bool Estado { get; set; }

        [DisplayName("Periodo")]
        public int IdPeriodo { get; set; }

        [DisplayName("Año")]
        public int Año { get; set; }

        [Required]
        [DisplayName("Sexo")]
        public string Sexo { get; set; }

        [DisplayName("Carrera")]
        public string CarreraNom { get; set; }//concatenacion de nombre de carrera completo


        [DisplayName("Area")]
        public string Area { get; set; }//especialidad a mostrar

        [Required]
        [DisplayName("Especialidad")]
        public string Especialidad { get; set; }//opcion del combobox seleccionada

        [DisplayName("Ciudad")]
        public int? IdCiudad { get; set; }

        [DisplayName("Calle")]
        public string Calle { get; set; }

        [DisplayName("Numero")]
        public string NumeroDireccion { get; set; }

        [DisplayName("Colonia")]
        public int? IdColonia { get; set; }



    }
}