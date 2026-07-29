using System.Collections.Generic;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    public class UsuariosViewModel
    {
        public List<UsuarioTutorias> UsuariosTutorias { get; set; }
        public List<UsuarioEstadias> UsuariosEstadias { get; set; }
        public List<AlumnoGestion> AlumnosGestion { get; set; }

        public int TotalUsuariosTutorias { get; set; }
        public int TotalUsuariosEstadias { get; set; }
        public int TotalAlumnosGestion { get; set; }

        public UsuariosViewModel()
        {
            UsuariosTutorias = new List<UsuarioTutorias>();
            UsuariosEstadias = new List<UsuarioEstadias>();
            AlumnosGestion = new List<AlumnoGestion>();
        }
    }

    public class DataTableResponse<T>
    {
        public int draw { get; set; }
        public int recordsTotal { get; set; }
        public int recordsFiltered { get; set; }
        public List<T> data { get; set; }
    }
}