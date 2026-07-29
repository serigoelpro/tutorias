
$(document).ready(function () {
    //================EntrevistaInicial, PAT, Tutoria Grupal=============
    //Pregunta1
    $("#Pregunta1").change(function (e) {
        console.log(e.target.checked);
        if (e.target.checked) {
            $("#1").addClass("hidden");
        } else {
            $("#1").removeClass("hidden");
        }
    });

    //Pregunta2
    $("#Pregunta2").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#2").addClass("hidden");
        } else {
            $("#2").removeClass("hidden");
        }
    });

    //Pregunta3
    $("#Pregunta3").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#3").addClass("hidden");
        } else {
            $("#3").removeClass("hidden");
        }
    });

    //Pregunta4
    $("#Pregunta4").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#4").addClass("hidden");
        } else {
            $("#4").removeClass("hidden");
        }
    });

    //Pregunta5
    $("#Pregunta5").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#5").addClass("hidden");
        } else {
            $("#5").removeClass("hidden");
        }
    });

    //Pregunta6
    $("#Pregunta6").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#6").addClass("hidden");
        } else {
            $("#6").removeClass("hidden");
        }
    });

    //Pregunta7
    $("#Pregunta7").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#7").addClass("hidden");
        } else {
            $("#7").removeClass("hidden");
        }
    });

    //Pregunta8
    $("#Pregunta8").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#8").addClass("hidden");
        } else {
            $("#8").removeClass("hidden");
        }
    });

    //Pregunta9
    $("#Pregunta9").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#9").addClass("hidden");
        } else {
            $("#9").removeClass("hidden");
        }
    });

    //=========Baja de alumno======
    //Pregunta10
    $("#Pregunta10").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#10").addClass("hidden");
        } else {
            $("#10").removeClass("hidden");
        }
    });



    //Pregunta12
    $("#Pregunta12").change(function (e) {
        console.log(e.target.checked);
        if (!e.target.checked) {
            $("#12").addClass("hidden");
        } else {
            $("#12").removeClass("hidden");
        }
    });

    //=========Tutoria grupal======
    //volvi a utilizar el uno este no funciono
    //Pregunta11
    $("#Pregunta11").change(function (e) {
        console.log(e.target.checked);
        if (e.targSet.checked) {
            $("#11").addClass("hidden");
        } else {
            $("#11").removeClass("hidden");
        }
    });

});
