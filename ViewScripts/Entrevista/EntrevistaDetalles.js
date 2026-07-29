
const studentId = $('#studentId').attr('data-id');
const entrevistaId = $('#entrevistaId').attr('data-id');

$.ajax({
    url: '/Asesor/GetEntrevistas',
    type: 'GET',
    data: { personaId: studentId },
    datatype: 'json',
    success: function (response) {
        response.forEach(function (entrevista) {

            let timestamp = parseInt(entrevista.Fecha.replace(/\/Date\((\d+)\)\//, "$1"));

            // Convert to JS Date
            let dt = new Date(timestamp);

            if (entrevista.IdGrado == 1) {
                $('#selectEntrevista').append(`
        <option value="${entrevista.IdEntrevistaInicial}">Entrevista inicial - 1&deg;</option>`)
            }
            else {
                $('#selectEntrevista').append(`
        <option value="${entrevista.IdEntrevistaInicial}">Entrevista de seguimiento - ${entrevista.IdGrado}&deg;- ${dt.toISOString().split("T")[0] }</option>`)
            }
        })

    }
})

$('#selectEntrevista').on('change', function () {
    const selectedEntrevistaId = $(this).val();
    window.location.href = `/Entrevistas/Detalles/${selectedEntrevistaId}`;
})

$('#yesBtn').on('click', function () {
    $('#yesNoModal').modal('hide');
    $.ajax({
        url: '/Entrevistas/EliminarEntrevista/',
        type: 'POST',
        data: { idEntrevista: entrevistaId, idPersona: studentId },
        datatype: 'json',
        success: function (response) {
            if (response.success) {
                alert('Entrevista eliminada correctamente');
            } else {
                alert('Error: ' + response.error);
            }
        }
    })
});