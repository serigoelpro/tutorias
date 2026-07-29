$(document).ready(function () {
    $('form').submit(function (e) {
        let camposFaltantes = false;
        let camposFaltantesNombres = [];

        // Establecer valores por defecto ANTES de validar
        // Campos condicionales de ciudad
        if ($('#Pregunta1').length && $('#Pregunta1').val() != 2) {
            $('input[name="Ciudad"]').val('N/A');
        }

        // Campos condicionales de trabajo
        if ($('#Pregunta2').length && $('#Pregunta2').val() != 1) {
            $('input[name="Trabaja"]').val('N/A');
        }

        // Campos condicionales de dependientes
        if ($('#Pregunta3').length && $('#Pregunta3').val() != 1) {
            $('input[name="Dependiente"]').val('0');
        }

        // Campos de seguimiento - establecer N/A si no aplican
        if ($('.seguimiento').first().hasClass('hidden')) {
            $('input[name="SolicitadoBeca"]').val('N/A');
            $('input[name="AfectacionEco"]').val('N/A');
        } else {
            // Si CuentaConApoyo es "No", establecer N/A
            if ($('#CuentaConApoyo').length && $('#CuentaConApoyo').val() != 1) {
                $('input[name="SolicitadoBeca"]').val('N/A');
            }
        }

        // Auto-fill hidden text fields and textareas with 'N/A' to pass server-side [Required] validation
        $(this).find('input[type="text"], textarea').each(function () {
            if ($(this).is(':hidden') || $(this).closest('.hidden').length > 0) {
                if (!$(this).val() || $(this).val().trim() === "") {
                    $(this).val('N/A');
                }
            }
        });

        // Validar solo campos visibles y que NO estén ocultos por la clase 'hidden'
        $(this).find('input:visible, select:visible, textarea:visible')
            .not('[type="file"]')
            .not('[type="hidden"]')
            .not('[id*="Foto"], [name*="Foto"], [id*="Imagen"], [name*="Imagen"]')
            .each(function () {
                // Verificar si el campo o su contenedor padre tiene la clase 'hidden' y 'display: none'
                if ($(this).closest('.hidden').length > 0 || $(this).css('display') === 'none') {
                    return true;
                }

                let valor = $(this).val();
                let esVacio = false;

                if ($(this).is('select')) {
                    esVacio = !valor || valor === '' || valor === '0' || valor === '-1';
                } else {
                    esVacio = !valor || valor.trim() === '';
                }

                // Verificar si es un campo que puede tener N/A como valor válido
                // En estricto rigor, si es visible debe tener valor.
                let nombreCampo = $(this).attr('name');
                let permitirNA = false;

                if (esVacio) {
                    camposFaltantes = true;

                    // Mejor detección de Label: Buscar en el contenedor col-* más cercano
                    let label = "";
                    let colContainer = $(this).closest('div[class*="col-"]');
                    if (colContainer.length > 0) {
                        label = colContainer.find('label').first().text();
                    }

                    // Fallback al form-group si no se encuentra en col-*
                    if (!label || label.trim() === "") {
                        label = $(this).closest('.form-group').find('label').first().text();
                    }

                    // Fallback al nombre del campo
                    if (!label || label.trim() === "") {
                        label = nombreCampo;
                    }

                    // Limpiar label (quitar dos puntos, espacios extra)
                    label = label.replace(':', '').trim();

                    camposFaltantesNombres.push(label);
                }
            });

        if (camposFaltantes) {
            e.preventDefault();
            console.log('Campos faltantes:', camposFaltantesNombres);
            $('#ModalCamposFaltantes').modal('show');
        }
    });
});