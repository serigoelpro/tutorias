using System;

namespace ProyectoIntegracion.Functionalities
{
    /// <summary>
    /// Periodo calendario cuatrimestral de la institucion:
    /// P1 = Enero-Abril, P2 = Mayo-Agosto, P3 = Septiembre-Diciembre.
    /// Centraliza la logica que estaba repetida (~12 veces) en NuevasEstadisticasController.
    /// </summary>
    public class PeriodoInfo
    {
        public int NumPeriodo { get; set; }     // 1, 2, 3
        public int Anio { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }
        public string Nombre { get; set; }      // ej. "Mayo - Agosto del 2026"
    }

    public static class PeriodoHelper
    {
        public static int CalcularNumPeriodo(int mes)
        {
            return (mes >= 1 && mes <= 4) ? 1 : (mes <= 8 ? 2 : 3);
        }

        public static PeriodoInfo Obtener(DateTime fecha)
        {
            return Obtener(fecha.Year, CalcularNumPeriodo(fecha.Month));
        }

        public static PeriodoInfo Obtener(int anio, int numPeriodo)
        {
            DateTime inicio, fin;
            string nombre;
            switch (numPeriodo)
            {
                case 1:
                    inicio = new DateTime(anio, 1, 1);
                    fin = new DateTime(anio, 4, 30, 23, 59, 59, 999);
                    nombre = "Enero - Abril del " + anio;
                    break;
                case 2:
                    inicio = new DateTime(anio, 5, 1);
                    fin = new DateTime(anio, 8, 31, 23, 59, 59, 999);
                    nombre = "Mayo - Agosto del " + anio;
                    break;
                default:
                    inicio = new DateTime(anio, 9, 1);
                    fin = new DateTime(anio, 12, 31, 23, 59, 59, 999);
                    nombre = "Septiembre - Diciembre del " + anio;
                    break;
            }
            return new PeriodoInfo { NumPeriodo = numPeriodo, Anio = anio, Inicio = inicio, Fin = fin, Nombre = nombre };
        }
    }
}
