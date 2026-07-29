// Estadísticas Master JavaScript
document.addEventListener("DOMContentLoaded", () => {
    initializeCharts()
    addAnimations()
})

function loadEstadisticasPorCarrera(carreraId) {
    showLoading()

    const xhr = new XMLHttpRequest()
    xhr.open("POST", "/Estudiantes/GetEstadisticasPorCarrera", true)
    xhr.setRequestHeader("Content-Type", "application/json")

    xhr.onreadystatechange = () => {
        if (xhr.readyState === 4 && xhr.status === 200) {
            try {
                const data = JSON.parse(xhr.responseText)
                showDetailedStats(data)
                hideLoading()
            } catch (error) {
                console.error("Error parsing estadísticas response:", error)
                hideLoading()
            }
        }
    }

    xhr.send(JSON.stringify({ idCarrera: Number.parseInt(carreraId) }))
}

function showDetailedStats(data) {
    const detailedStats = document.getElementById("detailedStats")
    const detailTitle = document.getElementById("detailTitle")

    detailTitle.textContent = "Estadísticas - " + data.nombreCarrera

    document.getElementById("detailTotalEstudiantes").textContent = data.totalEstudiantes
    document.getElementById("detailBecados").textContent = data.estudiantesBecados
    document.getElementById("detailTransporte").textContent = data.estudiantesTransporte
    document.getElementById("detailSinApoyo").textContent = data.estudiantesSinApoyo
    document.getElementById("detailMonto").textContent = data.montoTotal

    detailedStats.style.display = "block"
    detailedStats.classList.add("fade-in")
}

function showGlobalStats() {
    document.getElementById("totalCarreras").textContent = window.masterData.totalCarreras
    document.getElementById("totalBecados").textContent = window.masterData.totalBecados
    document.getElementById("totalTransporte").textContent = window.masterData.totalTransporte
    document.getElementById("totalSinApoyo").textContent = window.masterData.totalSinApoyo
    document.getElementById("montoTotal").textContent = window.masterData.montoTotal
}

function showLoading() {
    const detailedStats = document.getElementById("detailedStats")
    detailedStats.innerHTML =
        '<div class="loading-container"><span class="loading"></span> Cargando estadísticas...</div>'
    detailedStats.style.display = "block"
}

function hideLoading() {
    // Loading will be replaced by showDetailedStats
}

function initializeCharts() {
    if (window.Chart) Chart.defaults.animation = false;
    // Global Distribution Chart
    const globalCtx = document.getElementById("globalChart")
    if (globalCtx) {
        const globalChart = new Chart(globalCtx.getContext("2d"), {
            type: "pie",
            data: {
                labels: ["Con Beca", "Con Transporte", "Sin Apoyo"],
                datasets: [
                    {
                        data: [
                            window.masterData.totalBecados,
                            window.masterData.totalTransporte,
                            window.masterData.totalSinApoyo
                        ],
                        backgroundColor: ["#2ECC71", "#3498DB", "#E74C3C"],
                        borderWidth: 2,
                        borderColor: "#fff",
                    },
                ],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: {
                            padding: 15,
                            usePointStyle: true,
                        },
                    },
                },
                animation: {
                    duration: 2000,
                },
            },
        })
    }

    // Careers Comparison Chart - INICIALMENTE VACÍO, SE LLENARÁ CON DATOS DINÁMICOS
    const carrerasCtx = document.getElementById("carrerasChart")
    if (carrerasCtx) {
        window.carrerasChart = new Chart(carrerasCtx.getContext("2d"), {
            type: "bar",
            data: {
                labels: [], // Se llenará dinámicamente
                datasets: [
                    {
                        label: "Con Beca",
                        data: [], // Se llenará dinámicamente
                        backgroundColor: "#2ECC71",
                    },
                    {
                        label: "Con Transporte",
                        data: [], // Se llenará dinámicamente
                        backgroundColor: "#3498DB",
                    },
                ],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "top",
                    },
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: "rgba(0, 0, 0, 0.1)",
                        },
                    },
                    x: {
                        grid: {
                            display: false,
                        },
                    },
                },
                animation: {
                    duration: 2000,
                    easing: "easeOutBounce",
                },
            },
        })
    }

    // Trends Chart - INICIALMENTE VACÍO, SE LLENARÁ CON DATOS DINÁMICOS
    const trendsCtx = document.getElementById("trendsChart")
    if (trendsCtx) {
        window.trendsChart = new Chart(trendsCtx.getContext("2d"), {
            type: "line",
            data: {
                labels: [], // Se llenará dinámicamente
                datasets: [
                    {
                        label: "Becas Otorgadas",
                        data: [], // Se llenará dinámicamente
                        borderColor: "#2ECC71",
                        backgroundColor: "rgba(46, 204, 113, 0.1)",
                        tension: 0.4,
                        fill: true,
                    },
                    {
                        label: "Transporte Asignado",
                        data: [], // Se llenará dinámicamente
                        borderColor: "#3498DB",
                        backgroundColor: "rgba(52, 152, 219, 0.1)",
                        tension: 0.4,
                        fill: true,
                    },
                ],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "top",
                    },
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: "rgba(0, 0, 0, 0.1)",
                        },
                    },
                    x: {
                        grid: {
                            display: false,
                        },
                    },
                },
                animation: {
                    duration: 2000,
                },
            },
        })
    }
}

function addAnimations() {
    // Animate stat numbers
    const statNumbers = document.querySelectorAll(".stat-number")

    statNumbers.forEach((element) => {
        const text = element.textContent.replace(/[^0-9]/g, "")
        const finalValue = Number.parseInt(text)
        if (!isNaN(finalValue)) {
            animateNumber(element, 0, finalValue, 2000)
        }
    })

    // Add hover effects
    const statCards = document.querySelectorAll(".stat-card")
    statCards.forEach((card) => {
        card.addEventListener("mouseenter", function () {
            this.style.transform = "translateY(-5px) scale(1.02)"
        })

        card.addEventListener("mouseleave", function () {
            this.style.transform = "translateY(0) scale(1)"
        })
    })
}

function animateNumber(element, start, end, duration) {
    const startTime = performance.now()
    const originalText = element.textContent
    const isCurrency = originalText.includes("$")

    function updateNumber(currentTime) {
        const elapsed = currentTime - startTime
        const progress = Math.min(elapsed / duration, 1)

        const easeOutQuart = 1 - Math.pow(1 - progress, 4)
        const current = Math.floor(start + (end - start) * easeOutQuart)

        let displayValue = current.toLocaleString()
        if (isCurrency) displayValue = "$" + displayValue

        element.textContent = displayValue

        if (progress < 1) {
            requestAnimationFrame(updateNumber)
        }
    }

    requestAnimationFrame(updateNumber)
}

// Export functionality
document.getElementById('export-pdf-btn').addEventListener('click', function () {
    const exportBlock = document.getElementById('bloque-exportable');
    if (!exportBlock) return alert("No se encontró el bloque a exportar");

    // Clase de exportación para ajustar el ancho solo al exportar
    exportBlock.classList.add("exportar-pdf");

    html2canvas(exportBlock, {
        scale: 2,
        backgroundColor: '#fff',
        useCORS: true
    }).then(canvas => {
        const imgData = canvas.toDataURL('image/png');

        // Medidas A4 en mm
        const pdfWidth = 210; // mm
        const pdfHeight = 297; // mm

        const { jsPDF } = window.jspdf;
        const pdf = new jsPDF({
            orientation: 'portrait',
            unit: 'mm',
            format: 'a4'
        });

        // Que la imagen cubra todo el PDF
        pdf.addImage(imgData, 'PNG', 0, 0, pdfWidth, pdfHeight);
        pdf.save("EstadisticasMaster.pdf");

        // Remueve clase 
        exportBlock.classList.remove("exportar-pdf");
    });
});