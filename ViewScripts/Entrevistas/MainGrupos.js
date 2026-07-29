let viewType = 0;
let idCarrera;
let idGrado;
let idGrupo;
let nombreGrupo;
let idTurno;
let idPeriodo;
let anio;

const currentDate = new Date();
let currentMonth = currentDate.getMonth();
let currentYear = currentDate.getFullYear();
let currentPeriod;

if (currentMonth < 4) {
    currentPeriod = 1;
}
else if (currentMonth < 8) {
    currentPeriod = 2;
}
else {
    currentPeriod = 3;
}

var nivel = parseInt(document.getElementById("nivelUsuario").dataset.nivel);
var elEsDirector = document.getElementById("esDirector");
var esDirector = !!(elEsDirector && elEsDirector.dataset && elEsDirector.dataset.esdirector === "1");
var userCarrera = document.getElementById("carreraUsuario").dataset.carrera;
const sessiongGroupData = JSON.parse(sessionStorage.getItem("groupData"));

$(document).ready(function () {
    const viewTypeData = sessionStorage.getItem("viewTypeData");
    const sessionGroupTitle = JSON.parse(sessionStorage.getItem("sessionGroupTitle"));

    if (sessionGroupTitle) {
        $("#MainBody").before(`<h4 id="groupTitleInfo" class="page-header-custom text-center">${sessionGroupTitle}</h4>`);
    }

    const sessiongidCarrera = JSON.parse(sessionStorage.getItem("idCarrera"));
    const sessiongidGrado = JSON.parse(sessionStorage.getItem("idGrado"));
    const sessiongidGrupo = JSON.parse(sessionStorage.getItem("idGrupo"));
    const sessiongnombreGrupo = JSON.parse(sessionStorage.getItem("nombreGrupo"));

    if (sessiongGroupData) {
        $.ajax({
            url: "/Main/GetGroup",
            type: "GET",
            data: { id: sessiongGroupData },
            dataType: "json",
            beforeSend: function () {
                $("#MainBody").children().not("#loadingOverlay").remove();
                $("#loadingOverlay").show();
            },
            success: function (response) {
                if (viewTypeData && viewTypeData == 1) {
                    generateGroupTable(response);
                    viewType = 1;
                } else {
                    generateGroupCards(response);
                }
            },
            error: function (xhr, status, error) {
                console.log(error);
            },
            complete: function () {
                $("#loadingOverlay").hide();
            },
        });

        if (
            sessiongidCarrera &&
            sessiongidGrado &&
            sessiongidGrupo &&
            sessiongnombreGrupo
        ) {
            idCarrera = sessiongidCarrera;
            idGrado = sessiongidGrado;
            idGrupo = sessiongidGrupo;
            nombreGrupo = sessiongnombreGrupo;

            $("#btnDescargarPlantilla").attr(
                "href",
                `/Exportar/MateriasGrupo/idGrupo=${idGrupo}idCarrera=${idCarrera}idGrado=${idGrado}`
            );
            $("#inputidGrupoActual").val(idGrupo);
            $("#inputidCarreraActual").val(idCarrera);
            $("#inputidGradoActual").val(idGrado);
            $("#groupInfoName").html(nombreGrupo);
            $("#inputidGrupoActual").text(
                `(Grupo: ${idGrupo}, Carrera: ${idCarrera}, Grado: ${idGrado})`
            );
            $("#infoGestionGrupoNombre").text(
                `Gestión de Calificaciones - Grupo ${nombreGrupo}`
            );
            $("#modalNombreGrupo").text(`${idGrado}`);
        }
    }
});

// =================================================================
// SECCIÓN 1: CREAR SELECTORES (ORDEN CORRECTO)
// =================================================================

if (nivel > 2) {
    // 1.1 - Crear selector de especialidad/carrera PRIMERO
    $("#groupSelectLable").before(`
        <label>Area</label>
        <select id="specialtyGroup" class="form-control" style="display: inline-block">
            <option selected>Selecciona una carrera...</option>
        </select>
    `);

    // 1.2 - Cargar datos en selector de especialidad
    if (nivel == 4) {
        $.ajax({
            url: "/Main/GetCarreras",
            type: "GET",
            data: { userId: 1 },
            dataType: "json",
            success: function (response) {
                const $specialtyButtonList = $("#specialtyGroup");
                response.forEach(function (carrera) {
                    $specialtyButtonList.append(
                        `<option class="carrera" value="${carrera.IdCarrera}">${carrera.Nombre}</option>`
                    );
                });
            },
            error: function (xhr, status, error) {
                console.error("Error cargando carreras:", error);
            }
        });

        $.ajax({
            url: "/Main/GetSpecialties",
            type: "GET",
            dataType: "json",
            success: function (response) {
                const $specialtyButtonList = $("#specialtyGroup");
                response.forEach(function (especialidad) {
                    $specialtyButtonList.append(
                        `<option value="${especialidad.Id}">${especialidad.Nombre}</option>`
                    );
                });
            },
            error: function (xhr, status, error) {
                console.error("Error cargando especialidades:", error);
            }
        });
    } else {
        $.ajax({
            url: "/Main/GetCarreras",
            type: "GET",
            data: { userCarrera: userCarrera },
            dataType: "json",
            success: function (response) {
                const $specialtyButtonList = $("#specialtyGroup");
                response.forEach(function (carrera) {
                    $specialtyButtonList.append(
                        `<option class="carrera" value="${carrera.IdCarrera}">${carrera.Nombre}</option>`
                    );
                });
            },
            error: function (xhr, status, error) {
                console.error("Error cargando carreras:", error);
            }
        });

        $.ajax({
            url: "/Main/GetSpecialties",
            type: "GET",
            data: { userCarrera: userCarrera },
            dataType: "json",
            success: function (response) {
                const $specialtyButtonList = $("#specialtyGroup");
                response.forEach(function (especialidad) {
                    $specialtyButtonList.append(
                        `<option value="${especialidad.Id}">${especialidad.Nombre}</option>`
                    );
                });
            },
            error: function (xhr, status, error) {
                console.error("Error cargando especialidades:", error);
            }
        });
    }

    // 1.3 - Crear selectores de año y período DESPUÉS
    $("#groupSelect").after(`
        <select id="year" class="form-control" style="width:100px; display: inline-block"></select>
        <select id="periodo" name="periodo" class="form-control" style="width:150px; display: inline-block">
            <option selected>Periodo...</option>
            <option value="1">Enero - Abril</option>
            <option value="2">Mayo - Agosto</option>
            <option value="3">Septiembre - Diciembre</option>
        </select> 
    `);

    // 1.4 - Llenar selector de años
    for (var y = 2000; y <= 2050; y++) {
        var option = document.createElement("option");
        option.value = y;
        option.text = y;
        if (y === new Date().getFullYear()) option.selected = true;
        $("#year").append(option);
    }

    $("#year").val(new Date().getFullYear());
    $("#periodo").val(currentPeriod);
}

// =================================================================
// SECCIÓN 2: CARGAR GRUPOS INICIALES
// =================================================================

if (nivel < 3) {
    $.ajax({
        url: "/Main/GetGroups",
        type: "GET",
        dataType: "json",
        success: function (response) {
            const $select = $("#groupSelect");
            response.forEach(function (group) {
                const $groupOption = $(`
                    <option value="${group.Value}">
                        ${group.Text}
                    </option>
                `);
                $select.append($groupOption);
            });
        },
        error: function (xhr, status, error) {
            console.error("Error cargando grupos:", error);
            alert("Error al cargar los grupos. Por favor recarga la página.");
        }
    });
}

if (nivel > 2) {
    const sessionSelectType = JSON.parse(sessionStorage.getItem("selectType"));
    const sessionSelectValue = JSON.parse(sessionStorage.getItem("selectGroupFilter"));

    if (sessionSelectType !== null && sessionSelectValue !== null) {
        $("#specialtyGroup").val(sessionSelectValue);

        if (sessionSelectType == 0) {
            getGroups(sessionSelectValue, currentPeriod, currentYear);
        } else {
            getGroupsByArea(sessionSelectValue, currentPeriod, currentYear);
        }
    } else if (userCarrera) {
        setTimeout(function () {
            $("#specialtyGroup").val(userCarrera);
            getGroups(userCarrera, currentPeriod, currentYear);
            sessionStorage.setItem("selectType", JSON.stringify(0));
            sessionStorage.setItem("selectGroupFilter", JSON.stringify(userCarrera));
        }, 500);
    }
}

// =================================================================
// SECCIÓN 3: EVENT LISTENERS
// =================================================================

if (nivel > 2) {
    $("#specialtyGroup").on("change", function () {
        var selectedOption = $(this).find("option:selected");
        var selectedYear = $("#year").find("option:selected").val();
        var selectedPeriod = $("#periodo").find("option:selected").val();
        const selectedValue = $(this).val();

        if (selectedValue === "" || selectedValue === "Selecciona una carrera...") {
            $("#groupSelect").empty().append("<option selected>Selecciona un grupo...</option>");
            return;
        }

        if ($(selectedOption).hasClass("carrera")) {
            getGroups(selectedValue, selectedPeriod, selectedYear);
            sessionStorage.setItem("selectType", JSON.stringify(0));
            sessionStorage.setItem("selectGroupFilter", JSON.stringify(selectedValue));
        } else {
            getGroupsByArea(selectedValue, selectedPeriod, selectedYear);
            sessionStorage.setItem("selectType", JSON.stringify(1));
            sessionStorage.setItem("selectGroupFilter", JSON.stringify(selectedValue));
        }
    });

    $("#periodo").on("change", function () {
        const selectedPeriod = $(this).val();
        var selectedYear = $("#year").find("option:selected").val();
        let selectType = JSON.parse(sessionStorage.getItem("selectType"));
        let selectValue = JSON.parse(sessionStorage.getItem("selectGroupFilter"));

        if (selectValue) {
            if (selectType == 0) {
                getGroups(selectValue, selectedPeriod, selectedYear);
            } else {
                getGroupsByArea(selectValue, selectedPeriod, selectedYear);
            }
        }
    });

    $("#year").on("change", function () {
        const selectedYear = $(this).val();
        var selectedPeriod = $("#periodo").find("option:selected");
        let period = selectedPeriod.val();
        let selectType = JSON.parse(sessionStorage.getItem("selectType"));
        let selectValue = JSON.parse(sessionStorage.getItem("selectGroupFilter"));

        if (selectValue) {
            if (selectType == 0) {
                getGroups(selectValue, period, selectedYear);
            } else {
                getGroupsByArea(selectValue, period, selectedYear);
            }
        }
    });
}

$("#groupSelect").on("change", function () {
    const selectedValue = $(this).val();

    if (selectedValue === "" || selectedValue === "Selecciona un grupo...") {
        return;
    }

    getGroup(selectedValue);

    groupTitle = $(this).find("option:selected").text();

    if (!$("#groupTitleInfo").length) {
        $("#MainBody").before(`<h4 id="groupTitleInfo" class="page-header-custom text-center"></h4>`);
    }
    $("#groupTitleInfo").text(`${groupTitle}`);
    sessionStorage.setItem("sessionGroupTitle", JSON.stringify(groupTitle));
});

// =================================================================
// SECCIÓN 4: FUNCIONES DE CARGA DE GRUPOS
// =================================================================

function getGroups(carrera_id, periodo, año) {
    $.ajax({
        url: `/Main/GetGroups/`,
        type: "GET",
        data: { carreraId: carrera_id, periodo: periodo, año: año },
        dataType: "json",
        success: function (response) {
            const $select = $("#groupSelect");
            $select.empty();
            $select.append("<option selected>Selecciona un grupo...</option>");

            if (response && response.length > 0) {
                response.forEach(function (group) {
                    const $groupOption = $(`
                        <option value="${group.Value}">
                            ${group.Text}
                        </option>
                    `);
                    $select.append($groupOption);
                });
            } else {
                $select.append("<option disabled>No hay grupos disponibles</option>");
            }
        },
        error: function (xhr, status, error) {
            console.error("Error cargando grupos:", error);
            const $select = $("#groupSelect");
            $select.empty();
            $select.append("<option selected>Error al cargar grupos</option>");
        }
    });
}

function getGroupsByArea(Area_id, periodo, año) {
    $.ajax({
        url: `/Main/GetGroups/`,
        type: "GET",
        data: { Area_id: Area_id, periodo: periodo, año: año },
        dataType: "json",
        success: function (response) {
            const $select = $("#groupSelect");
            $select.empty();
            $select.append("<option selected>Selecciona un grupo...</option>");

            if (response && response.length > 0) {
                response.forEach(function (group) {
                    const $groupOption = $(`
                        <option value="${group.Value}">
                            ${group.Text}
                        </option>
                    `);
                    $select.append($groupOption);
                });
            } else {
                $select.append("<option disabled>No hay grupos disponibles</option>");
            }
        },
        error: function (xhr, status, error) {
            console.error("Error cargando grupos por área:", error);
            const $select = $("#groupSelect");
            $select.empty();
            $select.append("<option selected>Error al cargar grupos</option>");
        }
    });
}

function getGroup(groupId, period, year) {
    $.ajax({
        url: "/Main/GetGroup",
        type: "GET",
        data: { id: groupId },
        dataType: "json",
        beforeSend: function () {
            $("#MainBody").children().not("#loadingOverlay").remove();
            $("#loadingOverlay").show();
        },
        success: function (response) {
            if (viewType == 0) {
                generateGroupCards(response);
            } else {
                generateGroupTable(response);
            }

            $.ajax({
                url: "/Entrevistas/GetGroupInfo/",
                data: { idGrupo: groupId },
                datatype: "json",
                success: function (group) {
                    idCarrera = group.IdCarrera;
                    idGrado = group.IdGrado;
                    idGrupo = group.IdGrupo;
                    nombreGrupo = group.IdGrupo;
                    idTurno = group.IdTurno;
                    idPeriodo = group.IdPeriodo;
                    anio = group.Año;

                    $("#btnDescargarPlantilla").attr(
                        "href",
                        `/Exportar/MateriasGrupo/idGrupo=${idGrupo}idCarrera=${idCarrera}idGrado=${idGrado}`
                    );
                    $("#inputidGrupoActual").val(idGrupo);
                    $("#inputidCarreraActual").val(idCarrera);
                    $("#inputidGradoActual").val(idGrado);
                    $("#inputidTurnoActual").val(idTurno);
                    $("#inputidPeriodoActual").val(idPeriodo);
                    $("#inputidAnioActual").val(anio);
                    $("#groupInfoName").html(nombreGrupo);
                    $("#inputidGrupoActual").text(
                        `(Grupo: ${idGrupo}, Carrera: ${idCarrera}, Grado: ${idGrado})`
                    );
                    $("#infoGestionGrupoNombre").text(
                        `Gestión de Calificaciones - Grupo ${nombreGrupo}`
                    );
                    $("#modalNombreGrupo").text(`${idGrado}`);

                    sessionStorage.setItem("idCarrera", JSON.stringify(idCarrera));
                    sessionStorage.setItem("idGrado", JSON.stringify(idGrado));
                    sessionStorage.setItem("idGrupo", JSON.stringify(idGrupo));
                    sessionStorage.setItem("nombreGrupo", JSON.stringify(nombreGrupo));
                },
            });
        },
        error: function (xhr, status, error) {
            console.log(error);
            alert("Error al cargar el grupo. Por favor intenta nuevamente.");
        },
        complete: function () {
            $("#loadingOverlay").hide();
        },
    });
    sessionStorage.setItem("groupData", JSON.stringify(groupId));
}

// =================================================================
// RESTO DEL CÓDIGO (Sin cambios - solo limpieza)
// =================================================================

$(document).on("click", (e) => {
    if ($("#studentPopUp").length) {
        if (
            !$(e.target).closest("#studentPopUp").length &&
            !$(e.target).closest(".profile").length
        ) {
            $("#studentPopUp").remove();
        }
    }
});

async function revisarBajasAsync(studentId) {
    try { return await $.getJSON('/Bajas/revisarBajas', { id: studentId }); }
    catch { return false; }
}

// Determina la inicial y clase CSS para la vulnerabilidad del alumno
function resolveVulnerabilidad(student) {
    // Lee la nueva propiedad 'Vulnerabilidad' (o 'vulnerabilidad' si el JSON la minúscula)
    // Esta propiedad ahora puede ser un NÚMERO (como '1') o TEXTO (como 'Economico')
    const rawValue = student.Vulnerabilidad || student.vulnerabilidad || '';
    // console.log(`Resolviendo para ID ${student.IdPersona}, Valor crudo:`, rawValue); 

    const valueStr = String(rawValue).trim().toLowerCase();
    const valueNum = parseInt(valueStr, 10);

    // Mapeo por ID (si viene de la entrevista inicial "1", "2", "3")
    if (valueNum === 1) return { initial: 'E', cls: 'vuln-economico', text: 'Económico' };
    if (valueNum === 2) return { initial: 'A', cls: 'vuln-academico', text: 'Académico' };
    if (valueNum === 3) return { initial: 'P', cls: 'vuln-personal', text: 'Personal' };

    // Mapeo por Texto (si viene de seguimientos "Economico", "Academico", etc.)
    if (valueStr.includes('econ')) return { initial: 'E', cls: 'vuln-economico', text: 'Económico' };
    if (valueStr.includes('acad')) return { initial: 'A', cls: 'vuln-academico', text: 'Académico' };
    if (valueStr.includes('pers')) return { initial: 'P', cls: 'vuln-personal', text: 'Personal' };

    // --- CAMBIO AQUÍ ---
    // ID 4 ("No vulnerable") o texto "no" AHORA SÍ MUESTRA 'N'
    if (valueNum === 4 || valueStr.includes('no')) {
        return { initial: 'N', cls: 'vuln-no', text: 'No vulnerable' };
    }
}

// Genera el HTML del badge
function createVulnBadgeHtml(student) {
    const vuln = resolveVulnerabilidad(student); // Llama a la lógica
    if (!vuln) {
        return ''; // Devuelve un string vacío y no hagas nada
    }

    const html = vuln.initial ? `<span class="vuln-badge ${vuln.cls}" title="Vulnerabilidad: ${vuln.text}">${vuln.initial}</span>` : '';
    // console.log(`Badge HTML para ID ${student.IdPersona}:`, html || '(vacío)');
    return html;
}

function generateGroupCards(group) {
    $("#MainBody").children().not("#loadingOverlay").remove();
    $("#MainBody").removeClass("sga-table-wrap").addClass("centerContent");

    const batchSize = 20;
    let index = 0;
    const fotoCache = new Map();

    function renderBatch() {
        const end = Math.min(index + batchSize, group.length);

        for (let i = index; i < end; i++) {
            const student = group[i];
            const $studentCard = $("#studentCard").prop("content");
            const $studentCardClone = $($studentCard).clone();

            if (student.Estado) {
                $studentCardClone.find(".list-group-item").html(
                    `<span class="glyphicon glyphicon-eye-open" style="color:green"></span> ${student.Matricula}`
                );
            } else {
                $studentCardClone.find(".list-group-item").html(
                    `<span class="glyphicon glyphicon-edit" style="color:red"></span> ${student.Matricula}`
                );
            }

            const $profile = $studentCardClone.find(".profile");
            $profile.attr("data-user-id", student.IdPersona);
            $profile.attr("data-user-status", student.Estado);
            $profile.attr("data-user-grade", student.GradoNombre || (student.Grado ? student.Grado.Nombre : ''));

            $studentCardClone.find(".student-name").text(`${i + 1}. ${student.Nombre}`);

            const $img = $studentCardClone.find(".StudentImage");
            $img.attr("src", "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='100' height='100'%3E%3Cdefs%3E%3Cstyle%3E .spinner-dot { animation: pulse 1.5s ease-in-out infinite; } @keyframes pulse { 0%, 100% { opacity: 0.3; } 50% { opacity: 1; } } %3C/style%3E%3C/defs%3E%3Crect fill='%23f9f9f9' width='100' height='100'/%3E%3Cg transform='translate(50,50)'%3E%3Ccircle cx='0' cy='-8' r='10' fill='%23555'/%3E%3Cellipse cx='0' cy='12' rx='16' ry='11' fill='%23555'/%3E%3C/g%3E%3Cg transform='translate(50,50)'%3E%3Ccircle cx='0' cy='-32' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='0s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='23' cy='-23' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='0.2s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='32' cy='0' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='0.4s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='23' cy='23' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='0.6s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='0' cy='32' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='0.8s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='-23' cy='23' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='1s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='-32' cy='0' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='1.2s' repeatCount='indefinite'/%3E%3C/circle%3E%3Ccircle cx='-23' cy='-23' r='3' fill='%231ab192'%3E%3Canimate attributeName='opacity' values='1;0.2;1' dur='1.5s' begin='1.4s' repeatCount='indefinite'/%3E%3C/circle%3E%3C/g%3E%3C/svg%3E");
            $img.attr("data-person-id", student.IdPersona);
            $img.addClass("lazy-foto");

            const vulnBadgeHtml = createVulnBadgeHtml(student);
            if (vulnBadgeHtml) {
                $studentCardClone.find(".student-name").append(vulnBadgeHtml);
            }

            $("#MainBody").append($studentCardClone);
        }

        index = end;

        if (index < group.length) {
            requestAnimationFrame(renderBatch);
        } else {
            cargarFotosProgresivo();
            marcarBajasEnPantalla();
            setTimeout(marcarBajasEnPantalla, 200);
        }
    }

    function cargarFotosProgresivo() {
        const $imagenes = $(".lazy-foto");
        let fotoIndex = 0;
        const fotosBatch = 5;

        function cargarSiguienteBatch() {
            const end = Math.min(fotoIndex + fotosBatch, $imagenes.length);

            for (let i = fotoIndex; i < end; i++) {
                const img = $imagenes[i];
                const $img = $(img);
                const personId = $img.attr("data-person-id");

                if (fotoCache.has(personId)) {
                    $img.attr("src", fotoCache.get(personId));
                } else {
                    const fotoUrl = `/Main/GetFoto?id=${personId}`;
                    $img.attr("src", fotoUrl);
                    fotoCache.set(personId, fotoUrl);
                }

                $img.removeClass("lazy-foto");
            }

            fotoIndex = end;

            if (fotoIndex < $imagenes.length) {
                setTimeout(cargarSiguienteBatch, 100);
            }
        }

        cargarSiguienteBatch();
    }

    renderBatch();

    $("#MainBody").off("click", ".profile").on("click", ".profile", function () {
        if ($("#studentPopUp").length) $("#studentPopUp").remove();

        const $card = $(this);
        const userId = $card.data("user-id");
        const $tpl = $("#studentPopUpMenu").prop("content");
        const $popup = $($tpl).clone();

        // --- ESTAS LÍNEAS SON LA SOLUCIÓN ---
        const $studentNameEl = $card.find(".student-name");
        const studentImg = $card.find(".StudentImage").attr("src");

        // 1. Clona el badge de vulnerabilidad (E, A, P, N) si existe
        const $vulnBadge = $studentNameEl.find(".vuln-badge").clone();

        // 2. Obtiene solo el texto del nombre (ignorando el texto del badge)
        const studentName = $studentNameEl.contents().filter(function () {
            return this.nodeType === 3; // Filtra solo nodos de texto
        }).text().trim();

        $popup.find("#studentPopUpImage").attr("src", studentImg);
        $popup.find(".fixedPopUp").attr("id", "studentPopUp");

        // 3. Inserta el nombre en negritas y AÑADE el badge clonado al final
        $popup.find(".studentName").html("<strong>" + studentName + "</strong>").append($vulnBadge);
        // --- FIN DE LA SOLUCIÓN ---

        var tokenGenerado = null;
        $.ajax({
            url: '/Entrevistas/GenerarTokenAcceso',
            type: 'POST',
            data: { idPersona: userId },
            success: function (response) {
                if (response.success) {
                    tokenGenerado = response.token;
                    console.log("✓ Token pre-generado para ID:", userId);
                }
            },
            error: function () {
                console.warn("⚠ No se pudo pre-generar token para ID:", userId);
            }
        });

        if ($card.data("user-grade") == "1") {
            $popup.find("#imprimirBtn").attr("href", `/Entrevistas/ReporteEntrevista?id=${userId}`);
        } else {
            $popup.find("#imprimirBtn").attr("href", `/Entrevistas/ReporteEntrevistaSegumiento?id=${userId}`);
        }

        var textoEditar = ($card.data("user-status") == 1) ? "Editar" : "Revisar";

        if (!esDirector && (nivel == 2 || nivel == 3 || nivel == 4)) {
            $popup.find("#editarBtn")
                .attr("href", "javascript:void(0)")
                .attr("data-id-persona", userId)
                .text(textoEditar)
                .off("click")
                .on("click", function (e) {
                    e.preventDefault();
                    editarDetallesConToken($(this).data("id-persona"), tokenGenerado, e);
                });
        } else {
            $popup.find("#editarBtn").remove();
        }

        if ($card.data("user-status") != 1) {
            $popup.find("#imprimirBtn").remove();
            $popup.find("#revisarBtn").remove();
        }

        $popup.find("#revisarBtn")
            .attr("href", "javascript:void(0)")
            .attr("data-id-persona", userId)
            .off("click")
            .on("click", function (e) {
                e.preventDefault();
                verDetallesConTokenRapido($(this).data("id-persona"), tokenGenerado, e);
            });

        $popup.find("#materiasBtn").attr("href", `/MateriasAlumno/Index?id=${userId}`);
        $popup.find("#seguimientoBtn").attr("href", `/Individuals/Index?id=${userId}`);
        $popup.find("#bajaBtn").attr("href", `/Bajas/Index?id=${userId}`);

        const esBaja = ($card.attr("data-user-baja") === "1") ||
            ($card.data("user-baja") === 1) ||
            ($card.data("user-baja") === true);

        const $bajaBtn = $popup.find("#bajaBtn");
        if (esBaja) {
            $bajaBtn.text("Ver Baja").removeClass("btn-primary btn-success").addClass("btn-danger");
        } else {
            $bajaBtn.text("Tramitar Baja").removeClass("btn-danger btn-success").addClass("btn-primary");
        }

        $popup.find("#removerBtn").attr("href", `/Reportar/Opcion1?id=${userId}`);
        $popup.find("#canalizacionBtn").attr("href", `/Canalizaciones/EscribirCanalizacion?id=${userId}`);

        if (esDirector || nivel < 3) {
            $popup.find("#eliminarBtn").remove();
        } else {
            $popup.find("#eliminarBtn").attr("href", `/Entrevistas/Eliminar?id=${userId}`);
        }

        function paintBajaBtn($popup, esBaja) {
            const $btn = $popup.find('#bajaBtn');
            $btn.removeClass('btn-primary btn-success btn-danger');
            if (esBaja) {
                $btn.text('Ver Baja').addClass('btn-danger');
            } else {
                $btn.text('Tramitar Baja').addClass('btn-primary');
            }
        }

        function computeEsBajaFromCard($card) {
            const a = $card.attr('data-user-baja');
            const d = $card.data('user-baja');
            return (a === '1') || (d === 1) || (d === true) || (a === 'true');
        }

        function aplicarPermisosBaja($popup, nivel, esBaja) {
            if (!esBaja) return;
            if (nivel === 2) {
                $popup.find('#revisarBtn,#imprimirBtn,#seguimientoBtn,#materiasBtn,#canalizacionBtn,#removerBtn,#eliminarBtn,#editarBtn').remove();
            } else if (nivel === 3) {
                $popup.find('#imprimirBtn,#canalizacionBtn,#removerBtn,#eliminarBtn,#editarBtn').remove();
            }
        }

        const esBajaAttr = computeEsBajaFromCard($card);
        paintBajaBtn($popup, esBajaAttr);
        aplicarPermisosBaja($popup, parseInt(nivel, 10), esBajaAttr);

        $("body").append($popup);

        if (esBajaAttr === false && (typeof $card.attr('data-user-baja') === 'undefined')) {
            $.getJSON('/Bajas/revisarBajas', { id: userId })
                .done(val => paintBajaBtn($popup, !!val))
                .fail(() => { });
        }

        $("#PopUpCloseBtn").on("click", () => $("#studentPopUp").remove());
    });
}

function generateGroupTable(group) {
    window.nivel = typeof window.nivel !== 'undefined'
        ? window.nivel
        : (parseInt($('#nivelUsuario').data('nivel')) || 0);

    $("#MainBody").children().not("#loadingOverlay").remove();
    $("#MainBody")
        .removeClass("centerContent")
        .addClass("sga-table-wrap");

    const $table = $(
        `<div class="sga-scope no-cards">                                
            <div class="table-responsive" style="--sga-table-min: 1100px;">
                <table class="table table-striped tabla-texto-grandecito" id="groupTable">        
                    <thead>
                        <tr>
                            <th>#</th>
                            <th>Matrícula</th>
                            <th>Nombre</th>
                            <th>Estado</th>
                            ${nivel == 2 ? "<th>Editar</th>" : ""}
                            <th>Detalles</th>
                            <th>Imprimir</th>
                            <th>Seguimiento</th>
                            <th>Canalizar</th>
                            <th>Materias</th>
                            <th>Baja</th>
                            <th>Remover</th>
                            ${nivel > 2 ? "<th>Eliminar</th>" : ""}
                        </tr>
                    </thead>
                    <tbody id="tableBody"></tbody>
                </table>
            </div>
        </div>`
    );

    $("#MainBody").append($table);
    const $tableBody = $("#tableBody");

    group.forEach(function (student, index) {
        const gradoNombre = student.GradoNombre || (student.Grado ? student.Grado.Nombre : '');

        var estadoHtml = student.Estado
            ? '<span class="glyphicon glyphicon-eye-open" style="color:var(--primary)" data-toggle="tooltip" title="Alumno revisado"></span>'
            : '<span class="glyphicon glyphicon-edit" style="color:var(--danger)" data-toggle="tooltip" title="Falta revisar"></span>';

        var detalles = '';
        if (student.Estado) {
            detalles = `<a class="btn btn-success btn-sm" href="javascript:void(0)"
                    data-toggle="tooltip" title="Ver expediente completo"
                    onclick="verDetallesConToken(${student.IdPersona}, event)">
                    <span class="glyphicon glyphicon-eye-open"></span>
                </a>`;
        }

        var revisar = '';
        if (!esDirector && (nivel == 2 || nivel == 3 || nivel == 4)) {
            let texto = student.Estado ? "Editar" : "Revisar";
            revisar = `<a class="btn btn-success btn-sm" href="javascript:void(0)"
                        data-toggle="tooltip" title="${texto} información del alumno"
                        onclick="editarDetallesConTokenTabla(${student.IdPersona}, event)">
                    <span class="glyphicon glyphicon-pencil"></span> ${texto}
                </a>`;
        }

        var canalizacion = esDirector ? '' : `<a class="btn btn-info btn-sm" href="/Canalizaciones/EscribirCanalizacion?id=${student.IdPersona}"
                            data-toggle="tooltip" title="Canalizar a psicología/tutoría">
                        <span class="glyphicon glyphicon-user"></span>
                    </a>`;

        var imprimir = '';
        if (student.Estado) {
            if (gradoNombre == "1") {
                imprimir = `<a class="btn btn-utt-outline btn-sm" href="/Entrevistas/ReporteEntrevista?id=${student.IdPersona}"
                            data-toggle="tooltip" title="Imprimir entrevista inicial">
                        <span class="glyphicon glyphicon-folder-open"></span>
                    </a>`;
            } else {
                imprimir = `<a class="btn btn-utt-outline btn-sm" href="/Entrevistas/ReporteEntrevistaSegumiento?id=${student.IdPersona}"
                            data-toggle="tooltip" title="Imprimir reporte de seguimiento">
                        <span class="glyphicon glyphicon-print"></span> 
                    </a>`;
            }
        }

        var seguimiento = `<a class="btn btn-success btn-sm" href="/Individuals/Index?id=${student.IdPersona}"
                            data-toggle="tooltip" title="Ver historial de seguimiento">
                                   <span class="glyphicon glyphicon-file"></span>
                               </a>`;

        const baja = esDirector ? '' : `<a class="btn btn-danger btn-sm" href="/Bajas/Index?id=${student.IdPersona}"
                        data-toggle="tooltip" title="Gestionar baja del alumno">
            <span class="glyphicon glyphicon-remove-sign"></span>
        </a>`;

        var materias = `<a class="btn btn-success btn-sm" href="/MateriasAlumno/Index?id=${student.IdPersona}"
                        data-toggle="tooltip" title="Ver carga académica">
                            <span class="glyphicon glyphicon-file"></span>
                        </a>`;

        var remover = esDirector ? '' : (gradoNombre != null && gradoNombre != '')
            ? `<a class="btn btn-danger btn-sm" href="/Reportar/Opcion1?id=${student.IdPersona}"
                 data-toggle="tooltip" title="Remover del grupo actual">
                <span class="glyphicon glyphicon-exclamation-sign"></span>
            </a>`
            : "Removido";

        var eliminar = '';
        if (!esDirector && nivel > 2) {
            eliminar = `<a class="btn btn-danger btn-sm" href="/Entrevistas/Eliminar?id=${student.IdPersona}"
                        data-toggle="tooltip" title="Eliminar registro permanentemente">
                                <span class="glyphicon glyphicon-trash"></span>
                            </a>`;
        }

        const vulnBadgeHtml = createVulnBadgeHtml(student);

        const row = `
<tr data-user-id="${student.IdPersona}">
  <th>${index + 1}</th>
  <td class="col-matricula">${student.Matricula}</td>
  <td class="col-nombre">${student.Nombre} ${vulnBadgeHtml}</td>
  <td>${estadoHtml}</td>
  ${nivel == 2 ? `<td>${revisar}</td>` : ""}
  <td>${detalles}</td>
  <td>${imprimir}</td>
  <td>${seguimiento}</td>
  <td>${canalizacion}</td>
  <td>${materias}</td>
  <td>${baja}</td>
  <td>${remover}</td>
  ${nivel > 2 ? `<td>${eliminar}</td>` : ""}
</tr>`;

        $tableBody.append(row);
    });

    marcarBajasEnPantalla();
    setTimeout(marcarBajasEnPantalla, 200);

    $('[data-toggle="tooltip"]').tooltip();
}
async function getGroupForViewSwitch(groupId) {
    let result = [];

    await $.ajax({
        url: "/Main/GetGroup",
        type: "GET",
        data: { id: groupId },
        dataType: "json",
        success: function (response) {
            result = response;
            console.log(result);
        }
    })
    return result;
}

async function switchViewToTable() {
    let groupdId = JSON.parse(sessionStorage.getItem("groupData"));
    let groupList = await getGroupForViewSwitch(groupdId);
    console.log(getGroupForViewSwitch(groupdId));

    $("#MainBody").children().not("#loadingOverlay").remove();

    if (viewType == 0) {
        generateGroupTable(groupList);
    } else {
        generateGroupCards(groupList);
    }
    marcarBajasEnPantalla();

    if (viewType == 0) {
        viewType = 1;
    } else {
        viewType = 0;
    }
    sessionStorage.setItem("viewTypeData", viewType);
}

function toggleMenuAcciones(event) {
    event.stopPropagation();

    var menu = document.getElementById("menu-acciones");
    var trigger = event.currentTarget;
    var icon = trigger.querySelector(".menu-icon");

    if (menu.style.display === "none" || menu.style.display === "") {
        menu.style.display = "block";
        icon.textContent = "×";
        trigger.classList.add("active");

        setTimeout(function () {
            document.addEventListener("click", cerrarMenuAcciones);
        }, 10);
    } else {
        cerrarMenuAcciones();
    }
}

function cerrarMenuAcciones() {
    var menu = document.getElementById("menu-acciones");
    var trigger = document.querySelector(".btn-menu-trigger-green");
    var icon = trigger.querySelector(".menu-icon");

    menu.style.display = "none";
    document.removeEventListener("click", cerrarMenuAcciones);
}

function exportarMaterias() {
    console.log("=== Exportando REPORTE COMPLETO ===");
    cerrarMenuAcciones();

    var url = "/Exportar/MateriasGrupo?idGrupo=" + idGrupo +
        "&idCarrera=" + idCarrera +
        "&idGrado=" + idGrado;

    console.log("URL de REPORTE COMPLETO:", url);
    window.location.href = url;
    showNotificationCompact("Descargando reporte completo...", "success", 2000);
}

function verArrastrePorGrupo() {
    console.log("=== Abriendo consulta de arrastre por grupo ===");
    cerrarMenuAcciones();

    var grupoSeleccionado = $("#groupSelect").val();
    var grupoTexto = $("#groupSelect option:selected").text();

    console.log("Grupo seleccionado en dropdown:", grupoSeleccionado);
    console.log("Texto del grupo:", grupoTexto);

    if (
        !grupoSeleccionado ||
        grupoSeleccionado === "" ||
        grupoSeleccionado === "Selecciona un grupo..."
    ) {
        console.error("No hay grupo seleccionado en el dropdown");
        alert(
            "Por favor selecciona un grupo primero antes de consultar el arrastre."
        );
        return;
    }

    var url = `/ArrastreGrupo/ArrastrePorGrupo/${grupoSeleccionado}`;

    console.log("URL de arrastre por grupo:", url);
    console.log("Enviando a ArrastreGrupoController con ID:", grupoSeleccionado);

    showNotificationCompact("Cargando consulta de arrastre...", "info", 2000);

    window.location.href = url;
}

function abrirModalImportar() {
    console.log('=== Abriendo modal de importación ===');
    cerrarMenuAcciones();

    if (!idGrupo || !idCarrera || !idGrado) {
        alert('Error: Selecciona un grupo primero.');
        return;
    }

    // ✅ OBTENER VALORES ACTUALIZADOS (De las variables globales o los inputs)
    var turnoActual = idTurno || $("#inputidTurnoActual").val();
    var periodoActual = idPeriodo || $("#inputidPeriodoActual").val();
    var anioActual = anio || $("#inputidAnioActual").val();

    // ✅ URL ACTUALIZADA CON LOS 6 FILTROS
    var urlPlantilla = `/Exportar/DescargarPlantillaMateriasGrupo?idGrupo=${idGrupo}&idCarrera=${idCarrera}&idGrado=${idGrado}&idTurno=${turnoActual}&idPeriodo=${periodoActual}&año=${anioActual}`;

    console.log('URL de PLANTILLA FILTRADA:', urlPlantilla);

    $('#btnDescargarPlantillaModal').attr('href', urlPlantilla);

    // Buscar el botón dentro del modal y actualizar su href
    $('#modalImportarExcel').find('a.btn-info[target="_blank"]').attr('href', urlPlantilla);

    $('#inputidGrupoActual').val(idGrupo);
    $('#inputidCarreraActual').val(idCarrera);
    $('#inputidGradoActual').val(idGrado);

    $('#formImportarModal')[0].reset();
    $('#info-archivo-modal').hide();
    $('#progress-container').hide();
    $('#btn-procesar-modal').prop('disabled', true);

    $('#modalImportarExcel').modal('show');
    showNotificationCompact('Modal de importación abierto', 'info', 1500);
}

function validarArchivoModal(input) {
    console.log("=== Validando archivo en modal ===");
    var archivo = input.files[0];
    var infoDiv = $("#info-archivo-modal");
    var btnProcesar = $("#btn-procesar-modal");

    if (archivo) {
        console.log("Archivo seleccionado:", archivo.name);

        var extension = archivo.name.toLowerCase().split(".").pop();
        console.log("Extensión del archivo:", extension);

        if (extension !== "xlsx" && extension !== "xls") {
            console.error("Extensión inválida:", extension);
            alert("Error: El archivo debe ser un Excel (.xlsx o .xls)");
            input.value = "";
            infoDiv.hide();
            btnProcesar.prop("disabled", true);
            return;
        }

        var tamañoMB = (archivo.size / 1024 / 1024).toFixed(2);
        console.log("Tamaño del archivo:", tamañoMB, "MB");

        if (archivo.size > 10 * 1024 * 1024) {
            console.error("Archivo demasiado grande:", tamañoMB, "MB");
            alert("Error: El archivo es demasiado grande. Máximo 10MB permitido.");
            input.value = "";
            infoDiv.hide();
            btnProcesar.prop("disabled", true);
            return;
        }

        $("#nombre-archivo-modal").html("<strong>Nombre:</strong> " + archivo.name);
        $("#tamaño-archivo-modal").html(
            "<strong>Tamaño:</strong> " + tamañoMB + " MB"
        );
        infoDiv.show();
        btnProcesar.prop("disabled", false);

        console.log("Archivo Excel válido seleccionado:", archivo.name);
    } else {
        console.log("No hay archivo seleccionado");
        infoDiv.hide();
        btnProcesar.prop("disabled", true);
    }
}

function procesarExcelModal() {
    console.log("=== Procesando Excel desde modal ===");

    var archivo = $("#archivoExcelModal")[0].files[0];
    console.log("Archivo a procesar:", archivo.name);

    // ✅ OBTENER IdTutoriaGrupal del sessionStorage EN ESTE MOMENTO
    var idTutoriaGrupalActual = JSON.parse(sessionStorage.getItem("groupData"));

    // ✅ LOGGING para debug
    console.log("🔍 DEBUG - Valores actuales:");
    console.log("   sessiongGroupData (variable global):", sessiongGroupData);
    console.log("   groupData (sessionStorage):", idTutoriaGrupalActual);
    console.log("   idGrupo:", idGrupo);
    console.log("   idCarrera:", idCarrera);
    console.log("   idGrado:", idGrado);

    // ✅ VALIDACIÓN CRÍTICA
    if (!idTutoriaGrupalActual || idTutoriaGrupalActual === 0) {
        alert("❌ ERROR: No se pudo identificar el grupo.\n\nPor favor, selecciona el grupo nuevamente del dropdown.");
        console.error("❌ ERROR CRÍTICO: idTutoriaGrupal no está disponible");
        return;
    }

    if (!confirm(
        "¿Estás seguro de que deseas importar las calificaciones desde este archivo?\n\n" +
        "Archivo: " + archivo.name + "\n" +
        "Grupo: " + nombreGrupo + "\n" +
        "ID Grupo: " + idTutoriaGrupalActual + "\n\n" +
        "Las calificaciones se registrarán en el sistema."
    )) {
        console.log("Usuario canceló la importación");
        return;
    }

    console.log("Usuario confirmó, iniciando procesamiento...");
    console.log("✅ Usando IdTutoriaGrupal:", idTutoriaGrupalActual);

    $("#progress-container").show();
    $("#btn-procesar-modal").prop("disabled", true);

    actualizarProgreso(0, "Iniciando procesamiento...");

    var formData = new FormData();
    formData.append("archivoExcel", archivo);
    formData.append("idTutoriaGrupal", idTutoriaGrupalActual);  // ✅ USAR VALOR ACTUALIZADO
    formData.append("idGrupo", idGrupo);
    formData.append("idCarrera", idCarrera);
    formData.append("idGrado", idGrado);

    console.log("FormData creado con los siguientes valores:");
    console.log("  - idTutoriaGrupal:", idTutoriaGrupalActual);
    console.log("  - idGrupo:", idGrupo);
    console.log("  - idCarrera:", idCarrera);
    console.log("  - idGrado:", idGrado);
    console.log("Enviando AJAX...");

    $.ajax({
        url: '/Importar/ProcesarExcelAjax',
        type: "POST",
        data: formData,
        processData: false,
        contentType: false,
        timeout: 120000,
        xhr: function () {
            var xhr = new window.XMLHttpRequest();
            xhr.upload.addEventListener(
                "progress",
                function (evt) {
                    if (evt.lengthComputable) {
                        var percentComplete = (evt.loaded / evt.total) * 50;
                        actualizarProgreso(percentComplete, "Subiendo archivo...");
                    }
                },
                false
            );
            return xhr;
        },
        beforeSend: function () {
            console.log("Enviando petición AJAX...");
            actualizarProgreso(10, "Validando archivo...");
        },
        success: function (response) {
            console.log("AJAX exitoso, respuesta recibida");
            actualizarProgreso(80, "Procesando calificaciones...");

            setTimeout(function () {
                actualizarProgreso(100, "¡Completado!");

                setTimeout(function () {
                    console.log("Cerrando modal de importación...");
                    $("#modalImportarExcel").modal("hide");

                    mostrarResultadosEnModal(response);
                }, 500);
            }, 1000);
        },
        error: function (xhr, status, error) {
            console.error("Error en AJAX:", status, error);
            console.error("Respuesta del servidor:", xhr.responseText);
            actualizarProgreso(0, "Error en el procesamiento");

            var mensajeError = "Error al procesar el archivo";
            if (xhr.responseText) {
                try {
                    var errorResponse = JSON.parse(xhr.responseText);
                    mensajeError = errorResponse.mensaje || mensajeError;
                } catch (e) {
                    console.error("No se pudo parsear respuesta de error");
                }
            }

            alert(mensajeError);
            $("#btn-procesar-modal").prop("disabled", false);
            $("#progress-container").hide();
        },
    });
}

function actualizarProgreso(porcentaje, mensaje) {
    $("#progress-bar").css("width", porcentaje + "%");
    $("#progress-text").text(mensaje + " (" + Math.round(porcentaje) + "%)");

    if (porcentaje === 100) {
        $("#progress-bar")
            .removeClass("progress-bar-striped active")
            .addClass("progress-bar-success");
    }
}

function mostrarResultadosEnModal(htmlResultado) {
    console.log("=== Mostrando resultados en modal ===");
    console.log("Tipo de respuesta:", typeof htmlResultado);

    var contenido = "";
    var esExitoso = false;

    try {
        if (typeof htmlResultado === "object" && htmlResultado.html) {
            console.log("Respuesta es objeto JSON con HTML");
            contenido = htmlResultado.html;
            esExitoso = htmlResultado.exito || false;
        }
        else if (typeof htmlResultado === "string") {
            console.log("Respuesta es HTML string");
            var $response = $(htmlResultado);
            contenido = $response.find(".container-fluid").html();

            if (!contenido || contenido.trim() === "") {
                contenido = htmlResultado;
            }

            esExitoso =
                contenido.indexOf("panel-success") !== -1 ||
                contenido.indexOf("Importación Completada") !== -1 ||
                contenido.indexOf("glyphicon-ok-circle") !== -1;
        }
        else {
            console.log("Tipo de respuesta desconocido, usando como string");
            contenido = String(htmlResultado);
            esExitoso = false;
        }

        console.log(
            "Contenido extraído (primeros 200 chars):",
            contenido.substring(0, 200)
        );
        console.log("Importación exitosa:", esExitoso);

        if (!contenido || contenido.trim() === "") {
            throw new Error("No se pudo extraer contenido válido de la respuesta");
        }
    } catch (error) {
        console.error("Error procesando respuesta:", error);
        contenido = `
            <div class="alert alert-danger">
                <h4><span class="glyphicon glyphicon-exclamation-triangle"></span> Error al procesar resultados</h4>
                <p>Hubo un problema al mostrar los resultados de la importación.</p>
                <p><strong>Error:</strong> ${error.message}</p>
                <p>Por favor, verifica manualmente el estado de las calificaciones o intenta nuevamente.</p>
            </div>
        `;
        esExitoso = false;
    }

    if (esExitoso) {
        $("#icono-resultado")
            .removeClass()
            .addClass("glyphicon glyphicon-ok-circle text-success");
    } else {
        $("#icono-resultado")
            .removeClass()
            .addClass("glyphicon glyphicon-exclamation-triangle text-danger");
    }

    $("#contenido-resultados").html(contenido);

    $("#modalResultados").modal("show");

    $("#modalResultados").on("shown.bs.modal", function () {
        initializeCollapseInModal();
    });

    if (esExitoso) {
        showNotificationCompact(
            "Importación completada exitosamente",
            "success",
            3000
        );
    } else {
        showNotificationCompact(
            "Importación completada con errores",
            "warning",
            3000
        );
    }
}

function initializeCollapseInModal() {
    console.log(
        " Inicializando paneles colapsables en modal "
    );

    $("#contenido-resultados").off("click.panel-collapse-custom");

    $("#contenido-resultados").on(
        "click.panel-collapse-custom",
        '[data-toggle="collapse"]',
        function (e) {
            e.preventDefault();
            e.stopPropagation();

            var $trigger = $(this);
            var target = $trigger.attr("href");
            var $target = $(target);
            var $icon = $trigger.find(
                ".glyphicon-chevron-down, .glyphicon-chevron-up"
            );

            console.log("Click en panel:", target);

            if ($target.length === 0) {
                console.error("Target no encontrado:", target);
                return;
            }

            if ($target.is(":visible")) {
                $target.slideUp(300, function () {
                    console.log("Panel cerrado:", target);
                });
                $icon
                    .removeClass("glyphicon-chevron-up")
                    .addClass("glyphicon-chevron-down");
            } else {
                $target.slideDown(300, function () {
                    console.log("Panel abierto:", target);
                });
                $icon
                    .removeClass("glyphicon-chevron-down")
                    .addClass("glyphicon-chevron-up");
            }
        }
    );

    setTimeout(function () {
        $("#contenido-resultados .panel-collapse").each(function () {
            var $panel = $(this);
            var panelId = $panel.attr("id");
            var $trigger = $('a[href="#' + panelId + '"]');
            var $icon = $trigger.find(
                ".glyphicon-chevron-down, .glyphicon-chevron-up"
            );

            var debeEstarAbierto =
                $panel.hasClass("in") ||
                $panel.css("display") === "block" ||
                ($panel.attr("style") &&
                    $panel.attr("style").includes("display: block"));

            if (debeEstarAbierto) {
                $panel.show();
                $icon
                    .removeClass("glyphicon-chevron-down")
                    .addClass("glyphicon-chevron-up");
                console.log("Panel inicializado como abierto:", panelId);
            } else {
                $panel.hide();
                $icon
                    .removeClass("glyphicon-chevron-up")
                    .addClass("glyphicon-chevron-down");
                console.log("Panel inicializado como cerrado:", panelId);
            }
        });
    }, 300);

    console.log("Paneles colapsables inicializados con implementación manual");
}

function volverAImportar() {
    $("#modalResultados").modal("hide");
    setTimeout(function () {
        abrirModalImportar();
    }, 300);
}

function showNotificationCompact(message, type, duration) {
    $(".notification-compact").remove();

    var alertClass =
        type === "success"
            ? "alert-success"
            : type === "error"
                ? "alert-danger"
                : type === "warning"
                    ? "alert-warning"
                    : "alert-info";

    var notification = $(`
        <div class="alert ${alertClass} notification-compact">
            ${message}
        </div>
    `);

    $("body").append(notification);

    if (duration > 0) {
        setTimeout(
            () => notification.fadeOut(300, () => notification.remove()),
            duration
        );
    }
}

$(document).ready(function () {
    console.log("=== Documento listo ===");

    if (typeof $ === "undefined") {
        console.error("ERROR: jQuery no está cargado");
        alert(
            "Error: jQuery no está disponible. La funcionalidad del modal no funcionará."
        );
        return;
    }

    if (typeof $.fn.modal === "undefined") {
        console.error("ERROR: Bootstrap modal no está disponible");
        alert(
            "Error: Bootstrap no está disponible. La funcionalidad del modal no funcionará."
        );
        return;
    }

    console.log("jQuery y Bootstrap disponibles");

    if ($("#modalImportarExcel").length === 0) {
        console.error("ERROR: Modal #modalImportarExcel no encontrado en el DOM");
    } else {
        console.log("Modal #modalImportarExcel encontrado en el DOM");
    }

    $("#btn-importar-modal").on("click", function (e) {
        console.log("=== Click en botón importar ===");
        e.preventDefault();
        abrirModalImportar();
    });

    $(document).keydown(function (e) {
        if (e.ctrlKey && e.keyCode === 69) {
            e.preventDefault();
            exportarMaterias();
        }
        if (e.ctrlKey && e.keyCode === 73) {
            e.preventDefault();
            abrirModalImportar();
        }
        if (e.ctrlKey && e.keyCode === 82) {
            e.preventDefault();
            verArrastrePorGrupo();
        }
    });

    $("#modalImportarExcel").on("hidden.bs.modal", function () {
        console.log("Modal de importación cerrado, limpiando...");
        $("#formImportarModal")[0].reset();
        $("#info-archivo-modal").hide();
        $("#progress-container").hide();
        $("#btn-procesar-modal").prop("disabled", true);
    });

    console.log("=== Test de funcionalidades ===");
    console.log("Modal importación existe:", $("#modalImportarExcel").length > 0);
    console.log("Modal resultados existe:", $("#modalResultados").length > 0);
    console.log("Botón importar existe:", $("#btn-importar-modal").length > 0);

    console.log("=== Inicialización completa ===");
});

window.marcarBajasEnPantalla = async function () {
    const $items = $('.profile[data-user-id], tr[data-user-id]');
    if ($items.length === 0) return;

    const ids = $items.map((_, el) => $(el).data('user-id')).get().filter(Boolean);
    if (ids.length === 0) return;

    try {
        const res = await $.getJSON('/Bajas/revisarBajasLote', { ids: ids.join(',') });
        const conBaja = new Set((res?.idsConBaja || []).map(Number));
        $items.each((_, el) => {
            const $el = $(el);
            const id = Number($el.data('user-id'));
            window.aplicarEstiloBaja($el, conBaja.has(id));
        });
        return;
    } catch (e) {
        console.warn('Batch no disponible, usando fallback por alumno...', e);
    }

    const concurrency = 6;
    let index = 0;
    async function worker() {
        while (index < $items.length) {
            const el = $items.get(index++);
            const $el = $(el);
            const id = Number($el.data('user-id'));
            if (!id) continue;
            try {
                const esBaja = await $.getJSON('/Bajas/revisarBajas', { id });
                window.aplicarEstiloBaja($el, !!esBaja);
            } catch (e) {
                console.warn('Error revisando baja de', id, e);
            }
        }
    }
    await Promise.all(Array.from({ length: concurrency }, worker));
};

window.aplicarEstiloBaja = function ($item, esBaja) {
    $item.attr('data-user-baja', esBaja ? '1' : '0').data('user-baja', esBaja ? 1 : 0);

    // Cards
    if ($item.hasClass('profile')) {
        const $nombre = $item.find('.student-name, .studentName');
        const $li = $item.find('.list-group-item');
        const $icon = $li.find('.glyphicon');

        if (esBaja) {
            $item.addClass('is-baja');
            if ($nombre.find('.baja-badge').length === 0) {
                $nombre.append('<span class="baja-badge">BAJA</span>');
            }
            $li.addClass('icon-danger');
            $icon.addClass('icon-danger');
            // $item.find('.vuln-badge').remove();  <-- ¡ELIMINA O COMENTA ESTA LÍNEA!
        } else {
            $item.removeClass('is-baja');
            $nombre.find('.baja-badge').remove();
            $li.removeClass('icon-danger');
            $icon.removeClass('icon-danger');
        }
        return;
    }

    // Filas de tabla
    if ($item.is('tr')) {
        const $nombreTd = $item.find('.col-nombre');
        const $matriTd = $item.find('.col-matricula');
        const $estadoIcon = $item.find('.estado-icon');

        if (esBaja) {
            $item.addClass('is-baja');
            if ($nombreTd.find('.baja-badge').length === 0) {
                $nombreTd.append('<span class="baja-badge">(BAJA)</span>');
            }
            $matriTd.addClass('icon-danger');
            $estadoIcon.removeClass('icon-success').addClass('icon-danger');
            // $item.find('.vuln-badge').remove(); <-- ¡ELIMINA O COMENTA ESTA LÍNEA TAMBIÉN!
        } else {
            $item.removeClass('is-baja');
            $nombreTd.find('.baja-badge').remove();
            $matriTd.removeClass('icon-danger');
            $estadoIcon.removeClass('icon-danger');
        }
    }
};

console.log("=== Funcionalidad de arrastre por grupo agregada ===");

function verDetallesConTokenRapido(idPersona, tokenPreGenerado, event) {
    console.log("=== Accediendo a detalles (modo rápido) ===");
    console.log("ID Persona:", idPersona);
    console.log("Token pre-generado:", tokenPreGenerado ? "✓ Sí" : "✗ No");

    var loadingHtml = '<i class="glyphicon glyphicon-refresh spinning"></i> Cargando...';
    var $btn = null;
    var textoOriginal = '';

    if (event && event.target) {
        $btn = $(event.target).closest('a, button');
        textoOriginal = $btn.html();
        $btn.prop('disabled', true).html(loadingHtml);
    }

    if (tokenPreGenerado) {
        console.log("→ Usando token pre-generado, redirigiendo...");
        if ($("#studentPopUp").length) {
            $("#studentPopUp").remove();
        }
        window.location.href = '/Entrevistas/Detalles?idPersona=' + idPersona + '&token=' + tokenPreGenerado;
        return;
    }

    console.log("→ Generando token nuevo (fallback)...");
    $.ajax({
        url: '/Entrevistas/GenerarTokenAcceso',
        type: 'POST',
        data: { idPersona: idPersona },
        success: function (response) {
            if (response.success) {
                if ($("#studentPopUp").length) {
                    $("#studentPopUp").remove();
                }
                console.log("→ Token generado, redirigiendo...");
                window.location.href = '/Entrevistas/Detalles?idPersona=' + idPersona + '&token=' + response.token;
            } else {
                alert('❌ Error: ' + (response.message || 'No se pudo generar el token'));
                if ($btn) {
                    $btn.prop('disabled', false).html(textoOriginal);
                }
            }
        },
        error: function (xhr, status, error) {
            console.error('Error AJAX:', error);
            alert('Error al generar el token. Intente nuevamente.');
            if ($btn) {
                $btn.prop('disabled', false).html(textoOriginal);
            }
        }
    });
}

function verDetallesConToken(idPersona, event) {
    console.log("=== Generando token para tabla ===");
    console.log("ID Persona:", idPersona);

    var loadingHtml = '<i class="glyphicon glyphicon-refresh spinning"></i> Cargando...';
    var $btn = null;
    var textoOriginal = '';

    if (event && event.target) {
        $btn = $(event.target).closest('a, button');
        textoOriginal = $btn.html();
        $btn.prop('disabled', true).html(loadingHtml);
    }

    $.ajax({
        url: '/Entrevistas/GenerarTokenAcceso',
        type: 'POST',
        data: { idPersona: idPersona },
        success: function (response) {
            if (response.success) {
                console.log("→ Token generado, redirigiendo...");
                window.location.href = '/Entrevistas/Detalles?idPersona=' + idPersona + '&token=' + response.token;
            } else {
                alert('Error: ' + (response.message || 'No se pudo generar el token'));
                if ($btn) {
                    $btn.prop('disabled', false).html(textoOriginal);
                }
            }
        },
        error: function (xhr, status, error) {
            console.error('Error AJAX:', error);
            alert('Error al generar el token. Intente nuevamente.');
            if ($btn) {
                $btn.prop('disabled', false).html(textoOriginal);
            }
        }
    });
}

window.verDetallesConToken = verDetallesConToken;
window.verDetallesConTokenRapido = verDetallesConTokenRapido;

console.log("Funciones de token optimizadas cargadas");

function editarDetallesConToken(idPersona, tokenPreGenerado, event) {
    var loadingHtml = '<i class="glyphicon glyphicon-refresh spinning"></i> Cargando...';
    var $btn = null, textoOriginal = '';
    if (event && event.target) {
        $btn = $(event.target).closest('a, button');
        textoOriginal = $btn.html();
        $btn.prop('disabled', true).html(loadingHtml);
    }
    if (tokenPreGenerado) {
        if ($("#studentPopUp").length) $("#studentPopUp").remove();
        window.location.href = '/Asesor/Detalles?id=' + idPersona + '&token=' + tokenPreGenerado;
        return;
    }
    $.ajax({
        url: '/Entrevistas/GenerarTokenAcceso',
        type: 'POST',
        data: { idPersona: idPersona },
        success: function (response) {
            if (response.success) {
                if ($("#studentPopUp").length) $("#studentPopUp").remove();
                window.location.href = '/Asesor/Detalles?id=' + idPersona + '&token=' + response.token;
            } else {
                alert('Error: ' + (response.message || 'No se pudo generar el token'));
                if ($btn) $btn.prop('disabled', false).html(textoOriginal);
            }
        },
        error: function () {
            alert('Error al generar el token. Intente nuevamente.');
            if ($btn) $btn.prop('disabled', false).html(textoOriginal);
        }
    });
}
window.editarDetallesConToken = editarDetallesConToken;

function editarDetallesConTokenTabla(idPersona, event) {
    var loadingHtml = '<i class="glyphicon glyphicon-refresh spinning"></i> Cargando...';
    var $btn = null, textoOriginal = '';
    if (event && event.target) {
        $btn = $(event.target).closest('a, button');
        textoOriginal = $btn.html();
        $btn.prop('disabled', true).html(loadingHtml);
    }
    $.ajax({
        url: '/Entrevistas/GenerarTokenAcceso',
        type: 'POST',
        data: { idPersona: idPersona },
        success: function (response) {
            if (response.success) {
                window.location.href = '/Asesor/Detalles?id=' + idPersona + '&token=' + response.token;
            } else {
                alert('Error: ' + (response.message || 'No se pudo generar el token'));
                if ($btn) $btn.prop('disabled', false).html(textoOriginal);
            }
        },
        error: function () {
            alert('Error al generar el token. Intente nuevamente.');
            if ($btn) $btn.prop('disabled', false).html(textoOriginal);
        }
    });
}
window.editarDetallesConTokenTabla = editarDetallesConTokenTabla;

// ====================================================================
// FUNCIONALIDAD: AGREGAR MATERIA DE ARRASTRE MANUAL
// ====================================================================

// Abrir modal y cargar alumnos del grupo
function abrirModalAgregarArrastre() {
    console.log('=== Abriendo modal para agregar arrastre manual ===');
    cerrarMenuAcciones();

    // Validar que hay un grupo seleccionado
    if (!idGrupo || !idCarrera || !idGrado) {
        alert('Error: Selecciona un grupo primero.');
        return;
    }

    // Limpiar formulario
    $('#formAgregarArrastre')[0].reset();
    $('#ddlCuatrimestreArrastre').prop('disabled', true).empty().append('<option value="">-- Primero seleccione un alumno --</option>');
    $('#ddlMateriaArrastre').prop('disabled', true).empty().append('<option value="">-- Primero seleccione un cuatrimestre --</option>');
    $('#infoPlanArrastre').hide();
    $('#previewFechaLimite').hide();
    $('#infoAlumnoArrastre').text('');

    // Cargar alumnos del grupo
    $.ajax({
        url: '/Main/GetGroup',
        type: 'GET',
        data: { id: JSON.parse(sessionStorage.getItem('groupData')) },
        dataType: 'json',
        beforeSend: function () {
            $('#ddlAlumnoArrastre').prop('disabled', true).empty().append('<option value="">Cargando alumnos...</option>');
        },
        success: function (alumnos) {
            $('#ddlAlumnoArrastre').empty().append('<option value="">-- Seleccionar Alumno --</option>');

            if (!alumnos || alumnos.length === 0) {
                alert('No hay alumnos en este grupo.');
                return;
            }

            alumnos.forEach(function (alumno) {
                $('#ddlAlumnoArrastre').append(
                    `<option value="${alumno.IdPersona}" 
                             data-grado="${alumno.IdGrado}" 
                             data-carrera="${alumno.IdCarrera}"
                             data-especialidad="${alumno.Especialidad || ''}">
                        ${alumno.Matricula} - ${alumno.Nombre}
                    </option>`
                );
            });

            $('#ddlAlumnoArrastre').prop('disabled', false);
        },
        error: function (xhr, status, error) {
            console.error('Error al cargar alumnos:', error);
            alert('Error al cargar la lista de alumnos.');
        }
    });

    // Establecer fecha máxima como hoy
    $('#txtFechaInicioArrastre').attr('max', new Date().toISOString().split('T')[0]);

    // Mostrar modal
    $('#modalAgregarArrastre').modal('show');
}

// Cuando se selecciona un alumno, cargar cuatrimestres disponibles
$('#ddlAlumnoArrastre').on('change', function () {
    var $selected = $(this).find('option:selected');
    var idPersona = $selected.val();
    var gradoAlumno = parseInt($selected.data('grado'));
    var carreraAlumno = $selected.data('carrera');

    // Limpiar selects dependientes
    $('#ddlCuatrimestreArrastre').prop('disabled', true).empty().append('<option value="">-- Seleccionar Cuatrimestre --</option>');
    $('#ddlMateriaArrastre').prop('disabled', true).empty().append('<option value="">-- Primero seleccione un cuatrimestre --</option>');
    $('#infoPlanArrastre').hide();

    if (!idPersona) {
        $('#infoAlumnoArrastre').text('');
        return;
    }

    // Mostrar información del alumno
    $('#infoAlumnoArrastre').html(
        `<strong>Cuatrimestre actual del alumno:</strong> ${gradoAlumno}° Cuatrimestre`
    );

    // Cargar cuatrimestres anteriores
    if (gradoAlumno > 1) {
        for (var i = 1; i < gradoAlumno; i++) {
            $('#ddlCuatrimestreArrastre').append(
                `<option value="${i}">${i}° Cuatrimestre</option>`
            );
        }
        $('#ddlCuatrimestreArrastre').prop('disabled', false);
    } else {
        $('#ddlCuatrimestreArrastre').append('<option value="">El alumno no tiene cuatrimestres anteriores</option>');
        $('#infoAlumnoArrastre').append('<br><span class="text-warning">⚠ Este alumno está en 1er cuatrimestre, no puede tener materias de arrastre.</span>');
    }
});

// Cuando se selecciona un cuatrimestre, cargar materias
$('#ddlCuatrimestreArrastre').on('change', function () {
    var cuatrimestreSeleccionado = $(this).val();

    // Obtener datos del alumno seleccionado
    var $alumnoSelected = $('#ddlAlumnoArrastre').find('option:selected');
    var carreraAlumno = $alumnoSelected.data('carrera');
    var idPersona = $alumnoSelected.val(); // ✅ OBTENER ID PERSONA

    // Limpiar select de materias
    $('#ddlMateriaArrastre').prop('disabled', true).empty().append('<option value="">Cargando materias...</option>');
    $('#infoPlanArrastre').hide();
    $('#seccionUnidadesArrastre').hide();

    if (!cuatrimestreSeleccionado || !idPersona) {
        $('#ddlMateriaArrastre').empty().append('<option value="">-- Primero seleccione un cuatrimestre --</option>');
        return;
    }

    console.log('Cargando materias para:', { carrera: carreraAlumno, grado: cuatrimestreSeleccionado, persona: idPersona });

    // ✅ LLAMADA AJAX ACTUALIZADA
    $.ajax({
        url: '/MateriasAlumno/ObtenerMateriasParaArrastre',
        type: 'GET',
        data: {
            idCarrera: carreraAlumno,
            idGrado: cuatrimestreSeleccionado,
            idPersona: idPersona // ✅ ENVIAMOS EL ID DEL ALUMNO
        },
        success: function (materias) {
            $('#ddlMateriaArrastre').empty().append('<option value="">-- Seleccionar Materia --</option>');

            if (!materias || materias.length === 0) {
                $('#ddlMateriaArrastre').append('<option value="">No hay materias disponibles para este cuatrimestre</option>');
                return;
            }

            materias.forEach(function (materia) {
                var estadoTexto = materia.Activo ? '' : ' [DESACTIVADA]';
                // Color gris si está desactivada para diferenciar visualmente
                var estilo = materia.Activo ? '' : 'style="color:#999;"';

                $('#ddlMateriaArrastre').append(
                    `<option value="${materia.IdMateria}" 
                             ${estilo}
                             data-plan-id="${materia.IdPlanEstudio}"
                             data-plan-nombre="${materia.NombrePlan}"
                             data-plan-minima="${materia.CalificacionMinima}"
                             data-plan-decimales="${materia.PermiteDecimales}"
                             data-activo="${materia.Activo}"
                             data-unidades="${materia.NumeroUnidades}">
                        ${materia.Nombre}${estadoTexto}
                    </option>`
                );
            });

            $('#ddlMateriaArrastre').prop('disabled', false);
        },
        error: function (xhr, status, error) {
            console.error('Error al cargar materias:', error);
            $('#ddlMateriaArrastre').empty().append('<option value="">Error al cargar materias</option>');
            alert('Error al cargar la lista de materias.');
        }
    });
});

// Cuando se selecciona una materia, mostrar información del plan
$('#ddlMateriaArrastre').on('change', function () {
    var $selected = $(this).find('option:selected');
    var planNombre = $selected.data('plan-nombre');
    var planMinima = $selected.data('plan-minima');
    var planDecimales = $selected.data('plan-decimales');
    var activo = $selected.data('activo');
    var numeroUnidades = parseInt($selected.data('unidades')) || 3; // AGREGAR

    if (!$selected.val()) {
        $('#infoPlanArrastre').hide();
        $('#seccionUnidadesArrastre').hide(); // AGREGAR
        return;
    }

    var estadoHtml = activo ?
        '<span class="label label-success">Materia Activa</span>' :
        '<span class="label label-warning">Materia Desactivada</span>';

    var tipoCalif = planDecimales ? 'Permite decimales' : 'Solo enteros';

    var html = `
        <strong>Plan:</strong> ${planNombre}<br>
        <strong>Calificación Mínima:</strong> ${planMinima}<br>
        <strong>Tipo:</strong> ${tipoCalif}<br>
        <strong>Unidades:</strong> ${numeroUnidades}<br>
        <strong>Estado:</strong> ${estadoHtml}<br>
        ${!activo ? '<br><span class="text-warning"> Esta materia está desactivada pero se puede agregar al historial del alumno.</span>' : ''}
    `;

    $('#detallesPlanArrastre').html(html);
    $('#infoPlanArrastre').show();

    // ✅ GENERAR INPUTS DE UNIDADES
    generarInputsUnidadesArrastre(numeroUnidades, planMinima, planDecimales, planNombre);
    $('#seccionUnidadesArrastre').show();
});


// ====================================================================
// ✅ FUNCIONALIDAD: CALIFICACIÓN POR UNIDADES EN MODAL DE ARRASTRE
// ====================================================================

// Cuando se selecciona una materia, generar inputs de unidades
$('#ddlMateriaArrastre').on('change', function () {
    var $selected = $(this).find('option:selected');
    var planMinima = parseFloat($selected.data('plan-minima')) || 7.0;
    var planDecimales = $selected.data('plan-decimales') === 'true' || $selected.data('plan-decimales') === true;
    var planNombre = $selected.data('plan-nombre') || 'Plan Estándar';
    var numeroUnidades = parseInt($selected.data('unidades')) || 3;

    if (!$selected.val()) {
        $('#infoPlanArrastre').hide();
        $('#seccionUnidadesArrastre').hide();
        return;
    }

    var estadoHtml = $selected.data('activo') ?
        '<span class="label label-success">Materia Activa</span>' :
        '<span class="label label-warning">Materia Desactivada</span>';

    var tipoCalif = planDecimales ? 'Permite decimales' : 'Solo enteros';

    var html = `
        <strong>Plan:</strong> ${planNombre}<br>
        <strong>Calificación Mínima:</strong> ${planMinima}<br>
        <strong>Tipo:</strong> ${tipoCalif}<br>
        <strong>Unidades:</strong> ${numeroUnidades}<br>
        <strong>Estado:</strong> ${estadoHtml}<br>
        ${!$selected.data('activo') ? '<br><span class="text-warning">Esta materia está desactivada pero se puede agregar al historial del alumno.</span>' : ''}
    `;

    $('#detallesPlanArrastre').html(html);
    $('#infoPlanArrastre').show();

    // ✅ GENERAR INPUTS DE UNIDADES
    generarInputsUnidadesArrastre(numeroUnidades, planMinima, planDecimales, planNombre);
    $('#seccionUnidadesArrastre').show();
});

// Función para generar inputs de unidades
function generarInputsUnidadesArrastre(numeroUnidades, planMinima, planDecimales, planNombre) {
    console.log(`📝 Generando ${numeroUnidades} unidades - Plan: ${planNombre}`);

    // ✅ Generar en el contenedor del ORDINARIO (panel de historial)
    var $contenedor = $('#contenedorUnidadesOrdinario');
    $contenedor.empty();

    for (var i = 1; i <= numeroUnidades; i++) {
        var inputHtml = `
            <div class="unidad-item-arrastre">
                <label>Unidad ${i}:</label>
                <input type="number" 
                       class="form-control unidad-input-arrastre" 
                       data-unidad="${i}"
                       data-plan-minima="${planMinima}"
                       data-plan-decimales="${planDecimales}"
                       data-plan-nombre="${planNombre}"
                       min="0" 
                       max="10"
                       step="${planDecimales ? '0.01' : '1'}"
                       placeholder="0.00"
                       onchange="calcularCalificacionFinalArrastre()"
                       oninput="aplicarColorUnidadArrastre(this)" />
            </div>
        `;
        $contenedor.append(inputHtml);
    }
    // ✅ Resetear preview del ORDINARIO
    $('#previewCalifOrdinario').text('---').css('background-color', '#999');
    $('#previewEstadoOrdinario').removeClass('label-success label-danger label-warning').addClass('label-default').text('Pendiente');

    // ✅ Mostrar la sección del ordinario
    $('#seccionUnidadesOrdinario').show();
}

// Función para aplicar color según aprobación
function aplicarColorUnidadArrastre(input) {
    var $input = $(input);
    var valor = parseFloat($input.val());
    var minima = parseFloat($input.data('plan-minima')) || 7.0;

    if (!isNaN(valor)) {
        if (valor >= minima) {
            $input.removeClass('reprobada').addClass('aprobada');
        } else {
            $input.removeClass('aprobada').addClass('reprobada');
        }
    } else {
        $input.removeClass('aprobada reprobada');
    }
}

// Función para calcular calificación final
function calcularCalificacionFinalArrastre() {
    console.log('Calculando calificación final del ordinario...');

    //  Buscar inputs del ORDINARIO
    var $inputs = $('#contenedorUnidadesOrdinario .unidad-input-arrastre');
    var totalUnidades = $inputs.length;
    var unidadesCalificadas = 0;
    var sumaCalificaciones = 0;
    var todasValidas = true;
    var calificacionesArray = []; // Para verificar Plan 2020

    // Obtener información del plan
    var $primerInput = $inputs.first();
    var planMinima = parseFloat($primerInput.data('plan-minima')) || 7.0;
    var planDecimales = $primerInput.data('plan-decimales') === 'true' || $primerInput.data('plan-decimales') === true;
    var planNombre = $primerInput.data('plan-nombre') || 'Plan Estándar';

    // Recorrer todas las unidades
    $inputs.each(function () {
        var valor = $(this).val().trim();
        if (valor !== '') {
            var calif = parseFloat(valor);
            if (!isNaN(calif) && calif >= 0 && calif <= 10) {
                unidadesCalificadas++;
                sumaCalificaciones += calif;
                calificacionesArray.push(calif); // Guardar para Plan 2020
            } else {
                todasValidas = false;
            }
        }
    });

    // Si no están todas calificadas o hay inválidas
    if (!todasValidas || unidadesCalificadas < totalUnidades) {
        $('#previewCalifOrdinario').text('---').css('background-color', '#999');
        $('#previewEstadoOrdinario').removeClass('label-success label-danger').addClass('label-default').text('Pendiente');
        console.log('Faltan unidades o hay valores inválidos');
        return;
    }

    // Calcular promedio
    var promedio = sumaCalificaciones / totalUnidades;
    var calificacionFinal;
    var esAprobatoria = true;

    // APLICAR LÓGICA DEL PLAN 2020 (SI TIENE UNIDADES < 8, REPRUEBA)
    if (!planDecimales) {
        // Plan 2020: Verificar si tiene unidades menores a 8
        var tieneUnidadesReprobadas = calificacionesArray.some(c => c < 8.0);

        if (tieneUnidadesReprobadas) {
            // Si tiene unidades < 8, la materia es REPROBATORIA
            calificacionFinal = Math.min(Math.floor(promedio), 7.0);
            esAprobatoria = false;
            console.log(`Plan 2020: Tiene unidades < 8, forzando reprobatoria con ${calificacionFinal}`);
        } else {
            // Todas las unidades >= 8, aplicar redondeo normal
            if (promedio < 8.0) {
                calificacionFinal = Math.floor(promedio);
            } else {
                calificacionFinal = Math.round(promedio);
            }
            esAprobatoria = calificacionFinal >= planMinima;
        }
    } else {
        // Plan 2024: Solo importa el promedio final
        calificacionFinal = Math.round(promedio * 100) / 100;
        esAprobatoria = calificacionFinal >= planMinima;
    }

    console.log(`Calificación calculada: ${calificacionFinal} (Plan: ${planNombre}, Aprobatoria: ${esAprobatoria})`);

    // Mostrar en preview del ORDINARIO
    $('#previewCalifOrdinario').text(calificacionFinal.toFixed(2));

    // Determinar estado
    if (esAprobatoria) {
        $('#previewCalifOrdinario').css('background-color', '#5cb85c');
        $('#previewEstadoOrdinario').removeClass('label-default label-danger').addClass('label-success').text('APROBADO');
    } else {
        $('#previewCalifOrdinario').css('background-color', '#d9534f');
        $('#previewEstadoOrdinario').removeClass('label-default label-success').addClass('label-danger').text('REPROBADO');
    }
}

// ====================================================================
// ✅ MOSTRAR ALERTA CUANDO SE SELECCIONA EL ÚLTIMO INTENTO
// ====================================================================

$('#ddlIntentoArrastre').on('change', function () {
    var intentoSeleccionado = parseInt($(this).val());

    if (intentoSeleccionado === 3) {
        $('#alertaUltimoIntento').slideDown(300);
        console.log('Usuario seleccionó ÚLTIMO INTENTO de arrastre');
    } else {
        $('#alertaUltimoIntento').slideUp(300);
    }
});



// ====================================================================
// ✅ MANEJAR CHECKBOXES DE INTENTOS
// ====================================================================
$(document).on('change', '#chkTieneExtraordinario', function () {
    if ($(this).is(':checked')) {
        $('#seccionExtraordinario').slideDown();
        $('#panelArrastre1').show(); // Mostrar opción de arrastre 1
    } else {
        $('#seccionExtraordinario').slideUp();
        $('#txtCalifExtraordinario').val('');
        $('#previewEstadoExtraordinario').removeClass().addClass('label label-default').text('Pendiente');
        $('#chkTieneArrastre1').prop('checked', false).trigger('change');
        $('#panelArrastre1').hide();
    }
});

$(document).on('change', '#chkTieneArrastre1', function () {
    if ($(this).is(':checked')) {
        $('#seccionArrastre1').slideDown();
        $('#panelArrastre2').show(); // Mostrar opción de arrastre 2
    } else {
        $('#seccionArrastre1').slideUp();
        $('#txtCalifArrastre1').val('');
        $('#previewEstadoArrastre1').removeClass().addClass('label label-default').text('Pendiente');
        $('#chkTieneArrastre2').prop('checked', false).trigger('change');
        $('#panelArrastre2').hide();
    }
});

$(document).on('change', '#chkTieneArrastre2', function () {
    if ($(this).is(':checked')) {
        $('#seccionArrastre2').slideDown();
    } else {
        $('#seccionArrastre2').slideUp();
        $('#txtCalifArrastre2').val('');
        $('#previewEstadoArrastre2').removeClass().addClass('label label-default').text('Pendiente');
    }
});

// ====================================================================
// ✅ VALIDAR CALIFICACIONES DE INTENTOS
// ====================================================================
function validarCalificacionIntento(valor, planMinima, permiteDecimales) {
    var calif = parseFloat(valor);
    if (isNaN(calif) || calif < 0 || calif > 10) {
        return null;
    }

    // Aplicar reglas del plan
    var califAjustada;
    if (permiteDecimales) {
        // Plan 2024
        califAjustada = Math.round(calif * 100) / 100;
    } else {
        // Plan 2020
        if (calif < 8.0) {
            califAjustada = Math.floor(calif);
        } else {
            califAjustada = Math.round(calif);
        }
    }

    return {
        original: calif,
        ajustada: califAjustada,
        esAprobatoria: califAjustada >= planMinima
    };
}

// ====================================================================
// ✅ ACTUALIZAR PREVIEW DE EXTRAORDINARIO
// ====================================================================
$(document).on('input', '#txtCalifExtraordinario', function () {
    var planMinima = parseFloat($('#ddlMateriaArrastre option:selected').data('plan-minima')) || 7.0;
    var planDecimales = $('#ddlMateriaArrastre option:selected').data('plan-decimales') === true;

    var valor = $(this).val().trim();
    if (valor === '') {
        $('#previewEstadoExtraordinario').removeClass().addClass('label label-default').text('Pendiente');
        return;
    }

    var resultado = validarCalificacionIntento(valor, planMinima, planDecimales);
    if (resultado) {
        if (resultado.esAprobatoria) {
            $('#previewEstadoExtraordinario').removeClass().addClass('label label-success').text('APROBADO - ' + resultado.ajustada.toFixed(2));
        } else {
            $('#previewEstadoExtraordinario').removeClass().addClass('label label-danger').text('REPROBADO - ' + resultado.ajustada.toFixed(2));
        }
    }
});

// ====================================================================
// ✅ ACTUALIZAR PREVIEW DE ARRASTRE 1
// ====================================================================
$(document).on('input', '#txtCalifArrastre1', function () {
    var planMinima = parseFloat($('#ddlMateriaArrastre option:selected').data('plan-minima')) || 7.0;
    var planDecimales = $('#ddlMateriaArrastre option:selected').data('plan-decimales') === true;

    var valor = $(this).val().trim();
    if (valor === '') {
        $('#previewEstadoArrastre1').removeClass().addClass('label label-default').text('Pendiente');
        return;
    }

    var resultado = validarCalificacionIntento(valor, planMinima, planDecimales);
    if (resultado) {
        if (resultado.esAprobatoria) {
            $('#previewEstadoArrastre1').removeClass().addClass('label label-success').text('APROBADO - ' + resultado.ajustada.toFixed(2));
        } else {
            $('#previewEstadoArrastre1').removeClass().addClass('label label-danger').text('REPROBADO - ' + resultado.ajustada.toFixed(2));
        }
    }
});

// ====================================================================
// ✅ ACTUALIZAR PREVIEW DE ARRASTRE 2
// ====================================================================
$(document).on('input', '#txtCalifArrastre2', function () {
    var planMinima = parseFloat($('#ddlMateriaArrastre option:selected').data('plan-minima')) || 7.0;
    var planDecimales = $('#ddlMateriaArrastre option:selected').data('plan-decimales') === true;

    var valor = $(this).val().trim();
    if (valor === '') {
        $('#previewEstadoArrastre2').removeClass().addClass('label label-default').text('Pendiente');
        return;
    }

    var resultado = validarCalificacionIntento(valor, planMinima, planDecimales);
    if (resultado) {
        if (resultado.esAprobatoria) {
            $('#previewEstadoArrastre2').removeClass().addClass('label label-success').text('APROBADO - ' + resultado.ajustada.toFixed(2));
        } else {
            $('#previewEstadoArrastre2').removeClass().addClass('label label-danger').text('REPROBADO - DEFINITIVA');
        }
    }
});

// ====================================================================
// ✅ FUNCIÓN MEJORADA: Guardar materia con SELECTOR DE INTENTO
// ====================================================================


var payloadTemporal = null;

// ====================================================================
// 1. LÓGICA Y CÁLCULOS
// ====================================================================
function prepararDatosCalculados() {
    console.log('Calculando datos...');

    // --- (VALIDACIONES BÁSICAS) ---
    var idPersona = $('#ddlAlumnoArrastre').val();
    var idMateria = $('#ddlMateriaArrastre').val();
    var cuatrimestre = $('#ddlCuatrimestreArrastre').val();
    var fechaInicio = $('#txtFechaInicioArrastre').val();
    var observaciones = $('#txtObservacionesArrastre').val();

    if (!idPersona || !idMateria || !cuatrimestre || !fechaInicio) {
        alert('Por favor complete todos los campos obligatorios (*)');
        return null;
    }

    // --- (PLAN Y OPCIONES) ---
    var $opcionMateria = $('#ddlMateriaArrastre option:selected');
    var planMinima = parseFloat($opcionMateria.data('plan-minima')) || 7.0;
    var planDecimales = $opcionMateria.data('plan-decimales') === true;
    var planNombre = $opcionMateria.data('plan-nombre') || 'Plan Estándar';

    // --- (CÁLCULO DE INTENTOS - LÓGICA ORIGINAL) ---
    var intentos = [];

    // 1. Ordinario
    var $inputsOrdinario = $('#contenedorUnidadesOrdinario .unidad-input-arrastre');
    var califsOrdinario = [];
    var todasOrdinarioCalificadas = true;

    $inputsOrdinario.each(function () {
        var valor = $(this).val().trim();
        if (valor === '') { todasOrdinarioCalificadas = false; return false; }
        califsOrdinario.push(parseFloat(valor));
    });

    if (!todasOrdinarioCalificadas || califsOrdinario.length === 0) {
        alert('Debe calificar todas las unidades del intento ordinario');
        return null;
    }

    var promedioOrdinario = califsOrdinario.reduce((a, b) => a + b, 0) / califsOrdinario.length;
    var califOrdinarioAjustada, esAprobatoriaOrdinario;

    if (!planDecimales) {
        var tieneUnidadesReprobadas = califsOrdinario.some(c => c < 8.0);
        if (tieneUnidadesReprobadas) {
            califOrdinarioAjustada = Math.min(Math.floor(promedioOrdinario), 7.0);
            esAprobatoriaOrdinario = false;
        } else {
            califOrdinarioAjustada = (promedioOrdinario < 8.0) ? Math.floor(promedioOrdinario) : Math.round(promedioOrdinario);
            esAprobatoriaOrdinario = califOrdinarioAjustada >= planMinima;
        }
    } else {
        califOrdinarioAjustada = Math.round(promedioOrdinario * 100) / 100;
        esAprobatoriaOrdinario = califOrdinarioAjustada >= planMinima;
    }

    intentos.push({
        numeroIntento: 1,
        tipoIntento: 'Ordinario',
        calificacion: promedioOrdinario,
        calificacionAjustada: califOrdinarioAjustada,
        esAprobatoria: esAprobatoriaOrdinario,
        observaciones: 'Calificación final por unidades',
        calificacionesUnidades: califsOrdinario
    });

    // 2. Extraordinario
    if ($('#chkTieneExtraordinario').is(':checked')) {
        var califExtra = parseFloat($('#txtCalifExtraordinario').val());
        if (isNaN(califExtra)) { alert('Calif. Extraordinario inválida'); return null; }

        var resExtra = validarCalificacionIntento(califExtra, planMinima, planDecimales);
        intentos.push({
            numeroIntento: 2, tipoIntento: 'Extraordinario',
            calificacion: resExtra.original, calificacionAjustada: resExtra.ajustada,
            esAprobatoria: resExtra.esAprobatoria, observaciones: 'Examen extraordinario'
        });

        if (resExtra.esAprobatoria && ($('#chkTieneArrastre1').is(':checked') || $('#chkTieneArrastre2').is(':checked'))) {
            alert('Aprobó en Extraordinario, quite los arrastres.'); return null;
        }
    }

    // 3. Arrastre 1
    if ($('#chkTieneArrastre1').is(':checked')) {
        var califArr1 = parseFloat($('#txtCalifArrastre1').val());
        if (isNaN(califArr1)) { alert('Calif. Arrastre 1 inválida'); return null; }

        var resArr1 = validarCalificacionIntento(califArr1, planMinima, planDecimales);
        intentos.push({
            numeroIntento: 3, tipoIntento: 'Arrastre (Intento 1)',
            calificacion: resArr1.original, calificacionAjustada: resArr1.ajustada,
            esAprobatoria: resArr1.esAprobatoria, observaciones: 'Primer intento de arrastre'
        });

        if (resArr1.esAprobatoria && $('#chkTieneArrastre2').is(':checked')) {
            alert('Aprobó en Arrastre 1, quite el 2do intento.'); return null;
        }
    }

    // 4. Arrastre 2
    if ($('#chkTieneArrastre2').is(':checked')) {
        var califArr2 = parseFloat($('#txtCalifArrastre2').val());
        if (isNaN(califArr2)) { alert('Calif. Arrastre 2 inválida'); return null; }

        var resArr2 = validarCalificacionIntento(califArr2, planMinima, planDecimales);
        intentos.push({
            numeroIntento: 4, tipoIntento: 'Arrastre (Intento 2)',
            calificacion: resArr2.original, calificacionAjustada: resArr2.ajustada,
            esAprobatoria: resArr2.esAprobatoria, observaciones: 'Segundo y último intento de arrastre'
        });
    }

    // --- (ESTADO FINAL) ---
    var ultimo = intentos[intentos.length - 1];
    var estadoFinal, intentosExtra, califFinal;

    if (ultimo.esAprobatoria) {
        estadoFinal = 'Acreditada';
        intentosExtra = 0;
        califFinal = ultimo.calificacionAjustada;
    } else {
        califFinal = ultimo.calificacionAjustada;
        if (ultimo.numeroIntento === 4) { estadoFinal = 'Reprobada'; intentosExtra = 3; }
        else if (ultimo.numeroIntento === 3) { estadoFinal = 'Reprobada'; intentosExtra = 2; }
        else if (ultimo.numeroIntento === 2) { estadoFinal = 'Reprobada'; intentosExtra = 1; }
        else { estadoFinal = 'Extraordinario'; intentosExtra = 1; }
    }

    // --- RETORNAR OBJETO CON DOS PARTES: DATOS PARA ENVIAR Y DATOS PARA MOSTRAR ---
    return {
        payload: {
            idPersona: idPersona,
            idMateria: idMateria,
            fechaInicioArrastre: fechaInicio,
            estadoFinal: estadoFinal,
            calificacionFinal: califFinal,
            intentosExtraordinarios: intentosExtra,
            intentos: intentos,
            observaciones: observaciones
        },
        // Datos visuales para el Modal
        ui: {
            nombreAlumno: $('#ddlAlumnoArrastre option:selected').text(),
            nombreMateria: $('#ddlMateriaArrastre option:selected').text(),
            cuatrimestre: cuatrimestre,
            plan: planNombre,
            totalIntentos: intentos.length,
            estado: estadoFinal,
            calif: califFinal,
            esDefinitiva: (ultimo.numeroIntento === 4 && !ultimo.esAprobatoria)
        }
    };
}

// 2. CONTROLADOR (El click del botón Guardar principal)
function guardarMateriaArrastreConUnidades() {
    var datos = prepararDatosCalculados();
    if (!datos) return; // Hubo error de validación

    // 1. Guardamos los datos en la variable global temporal
    payloadTemporal = datos.payload;

    // 2. Llenamos el Modal Bonito con la info
    $('#lblConfAlumno').text(datos.ui.nombreAlumno);
    $('#lblConfMateria').text(datos.ui.nombreMateria);
    $('#lblConfCuatri').text(datos.ui.cuatrimestre + '°');
    $('#lblConfPlan').text(datos.ui.plan);
    $('#lblConfIntentos').text(datos.ui.totalIntentos);
    $('#lblConfEstado').text(datos.ui.estado);
    $('#lblConfCalif').text(datos.ui.calif.toFixed(2));

    // Si es reprobada definitiva, mostramos la alerta roja dentro del modal
    if (datos.ui.esDefinitiva) {
        $('#alertaReprobadaDefinitiva').show();
    } else {
        $('#alertaReprobadaDefinitiva').hide();
    }

    // 3. Abrimos el Modal
    $('#modalConfirmacionArrastre').modal('show');
}

// 3. EL CLICK DEL MODAL ("Sí, Guardar")
$('#btnConfirmarFinal').click(function () {
    // Ocultamos el modal
    $('#modalConfirmacionArrastre').modal('hide');

    // Enviamos los datos que guardamos en la variable temporal
    if (payloadTemporal) {
        enviarDatosAlServidor(payloadTemporal);
    }
});

// 4. AJAX
function enviarDatosAlServidor(dataPayload) {
    var $btn = $('#btnGuardarArrastre'); // El botón del modal original
    var textoOriginal = $btn.html();
    $btn.prop('disabled', true).html('<span class="glyphicon glyphicon-refresh spinning"></span> Guardando...');

    $.ajax({
        url: '/MateriasAlumno/AgregarMateriaArrastreConHistorialCompleto',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(dataPayload),
        success: function (result) {
            if (result.success) {
                $('#modalAgregarArrastre').modal('hide'); // Cierra el modal de formulario
                alert(result.message); // Mensaje final de éxito
                setTimeout(function () { location.reload(); }, 1000);
            } else {
                alert('Error: ' + (result.message || 'No se pudo agregar'));
                $btn.prop('disabled', false).html(textoOriginal);
            }
        },
        error: function (xhr, status, error) {
            console.error('Error:', error);
            alert('Error de conexión');
            $btn.prop('disabled', false).html(textoOriginal);
        }
    });
}
console.log('=== Selector de intento de arrastre cargado correctamente ===');