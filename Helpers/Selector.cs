using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PlataformaWeb.Helpers
{
    public static class SelectListHelper
    {
        public static SelectList SelectorSiNo(string valor)
        {
            string selectedValue;

            if (string.IsNullOrEmpty(valor))
            {
                selectedValue = "0";
            }
            else if (valor == "N/A")
            {
                selectedValue = "1";
            }
            else
            {
                selectedValue = "2";
            }

            return new SelectList(new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Selecciona una opción" },
                new SelectListItem { Value = "1", Text = "Sí" },
                new SelectListItem { Value = "2", Text = "No" }
            }, "Value", "Text", selectedValue);
        }

        public static SelectList SelectorNoSi(string valor)
        {
            string selectedValue;

            if (string.IsNullOrWhiteSpace(valor))
            {
                selectedValue = "0";
            }
            else if (valor == "N/A")
            {
                selectedValue = "2";
            }
            else
            {
                selectedValue = "1";
            }

            return new SelectList(new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Selecciona una opción" },
                new SelectListItem { Value = "1", Text = "Sí" },
                new SelectListItem { Value = "2", Text = "No" }
            }, "Value", "Text", selectedValue);
        }

    }
}