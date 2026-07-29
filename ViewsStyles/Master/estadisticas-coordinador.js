// Estadísticas Coordinador JavaScript
document.addEventListener("DOMContentLoaded", () => {
    initializeCharts()
    addAnimations()
})

function initializeCharts() {
    // Distribution Chart
    const distributionCtx = document.getElementById("distributionChart")
    if (distributionCtx) {
        const distributionChart = new Chart(distributionCtx.getContext("2d"), {
            type: "doughnut",
            data: {
                labels: ["Con Beca", "Con Transporte", "Sin Apoyo"],
                datasets: [
                    {
                        data: [
                            window.estadisticasData.conBeca,
                            window.estadisticasData.conTransporte,
                            window.estadisticasData.sinApoyo,
                        ],
                        backgroundColor: ["#27AE60", "#3498DB", "#E74C3C"],
                        borderWidth: 0,
                        hoverOffset: 10,
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
                            padding: 20,
                            usePointStyle: true,
                            font: {
                                size: 14,
                            },
                        },
                    },
                    tooltip: {
                        callbacks: {
                            label: (context) => {
                                const total = context.dataset.data.reduce((a, b) => a + b, 0)
                                const percentage = ((context.parsed / total) * 100).toFixed(1)
                                return context.label + ": " + context.parsed + " (" + percentage + "%)"
                            },
                        },
                    },
                },
                animation: {
                    animateRotate: true,
                    duration: 2000,
                },
            },
        })
    }

    // Support Chart
    const supportCtx = document.getElementById("supportChart")
    if (supportCtx) {
        const supportChart = new Chart(supportCtx.getContext("2d"), {
            type: "bar",
            data: {
                labels: ["Total", "Con Beca", "Con Transporte"],
                datasets: [
                    {
                        label: "Estudiantes",
                        data: [
                            window.estadisticasData.totalEstudiantes,
                            window.estadisticasData.conBeca,
                            window.estadisticasData.conTransporte,
                        ],
                        backgroundColor: ["#2ECC71", "#27AE60", "#3498DB"],
                        borderRadius: 8,
                        borderSkipped: false,
                    },
                ],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false,
                    },
                    tooltip: {
                        backgroundColor: "rgba(0, 0, 0, 0.8)",
                        titleColor: "#fff",
                        bodyColor: "#fff",
                        borderColor: "#2ECC71",
                        borderWidth: 1,
                    },
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: "rgba(0, 0, 0, 0.1)",
                        },
                        ticks: {
                            font: {
                                size: 12,
                            },
                        },
                    },
                    x: {
                        grid: {
                            display: false,
                        },
                        ticks: {
                            font: {
                                size: 12,
                            },
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
}

function addAnimations() {
    // Animate stat numbers
    const statNumbers = document.querySelectorAll(".stat-number")

    statNumbers.forEach((element) => {
        const finalValue = Number.parseInt(element.textContent.replace(/[^0-9]/g, ""))
        if (!isNaN(finalValue)) {
            animateNumber(element, 0, finalValue, 2000)
        }
    })

    // Add hover effects to stat cards
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
    const isPercentage = originalText.includes("%")
    const isCurrency = originalText.includes("$")

    function updateNumber(currentTime) {
        const elapsed = currentTime - startTime
        const progress = Math.min(elapsed / duration, 1)

        // Easing function
        const easeOutQuart = 1 - Math.pow(1 - progress, 4)
        const current = Math.floor(start + (end - start) * easeOutQuart)

        let displayValue = current.toLocaleString()
        if (isPercentage) displayValue += "%"
        if (isCurrency) displayValue = "$" + displayValue

        element.textContent = displayValue

        if (progress < 1) {
            requestAnimationFrame(updateNumber)
        }
    }

    requestAnimationFrame(updateNumber)
}

// Add loading states for buttons
document.querySelectorAll(".btn").forEach((button) => {
    button.addEventListener("click", function () {
        if (this.href) return // Skip for links

        const originalText = this.innerHTML
        this.innerHTML = '<span class="loading"></span> Cargando...'
        this.disabled = true

        setTimeout(() => {
            button.innerHTML = originalText
            button.disabled = false
        }, 2000)
    })
})

// Add fade-in animation to containers
const containers = document.querySelectorAll(".stat-card, .chart-container")
const observer = new IntersectionObserver(
    (entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add("fade-in")
            }
        })
    },
    { threshold: 0.1 },
)

containers.forEach((container) => {
    observer.observe(container)
})

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
        pdf.save("EstadisticasCoordinador.pdf");

        // Remueve clase 
        exportBlock.classList.remove("exportar-pdf");
    });
});
