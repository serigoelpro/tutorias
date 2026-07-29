// Global SGA object to avoid namespace conflicts
window.SGA = window.SGA || {}
    ; (($) => {
        // Configuration object
        SGA.config = {
            animationSpeed: 300,
            tableLanguage: {
                sProcessing: "Procesando...",
                sLengthMenu: "Mostrar _MENU_ registros",
                sZeroRecords: "No se encontraron resultados",
                sEmptyTable: "Ningn dato disponible en esta tabla",
                sInfo: "Mostrando registros del _START_ al _END_ de un total de _TOTAL_ registros",
                sInfoEmpty: "Mostrando registros del 0 al 0 de un total de 0 registros",
                //sInfoFiltered: "(filtrado de un total de _MAX_ registros)",
                sInfoPostFix: "",
                sSearch: "Buscar:",
                sUrl: "",
                sInfoThousands: ",",
                sLoadingRecords: "Cargando...",
                oPaginate: {
                    sFirst: "Primero",
                    sLast: "ltimo",
                    sNext: "Siguiente",
                    sPrevious: "Anterior",
                },
                oAria: {
                    sSortAscending: ": Activar para ordenar la columna de manera ascendente",
                    sSortDescending: ": Activar para ordenar la columna de manera descendente",
                },
            },
        }

        // Sidebar Navigation Management
        SGA.sidebar = {
            init: function () {
                this.bindEvents()
                this.setActiveMenuItem()
                this.handleResponsive()
            },

            bindEvents: () => {
                // Toggle sidebar on mobile
                $(document).on("click", ".sidebar-toggle", (e) => {
                    e.preventDefault()
                    $(".sidebar").toggleClass("show")
                    $("body").toggleClass("sidebar-open")
                })

                // Close sidebar when clicking outside on mobile
                $(document).on("click", (e) => {
                    if ($(window).width() <= 768) {
                        if (!$(e.target).closest(".sidebar, .sidebar-toggle").length) {
                            $(".sidebar").removeClass("show")
                            $("body").removeClass("sidebar-open")
                        }
                    }
                })
            },

            setActiveMenuItem: () => {
                var currentPath = window.location.pathname.toLowerCase()

                $(".sidebar .nav-link").each(function () {
                    var $link = $(this)
                    var href = $link.attr("href")

                    if (href && currentPath.indexOf(href.toLowerCase()) !== -1) {
                        $link.addClass("active")
                        $link.closest(".nav-item").addClass("active")
                    }
                })
            },

            handleResponsive: () => {
                $(window).on("resize", () => {
                    if ($(window).width() > 768) {
                        $(".sidebar").removeClass("show")
                        $("body").removeClass("sidebar-open")
                    }
                })
            },
        }

        // DataTables Enhancement
        SGA.dataTables = {
            init: function () {
                this.initializeTables()
                this.handleResponsive()
            },

            initializeTables: () => {
                $(".table, .sga-data-table").each(function () {
                    var $table = $(this)

                    // Skip if already initialized
                    if ($.fn.DataTable && $.fn.DataTable.isDataTable($table)) {
                        return
                    }

                    // Check if DataTables is available
                    if (!$.fn.DataTable) {
                        console.warn("DataTables not loaded, skipping table initialization")
                        return
                    }

                    // Check if table has data
                    if ($table.find("tbody tr").length > 0) {
                        try {
                            $table.DataTable({
                                language: {
                                    url: "//cdn.datatables.net/plug-ins/1.13.4/i18n/es-ES.json",
                                },
                                lengthChange: false,
                                pageLength: 10,
                                responsive: true,
                                info: true,
                                searching: true,
                                ordering: true,
                                dom: '<"row"<"col-sm-6"f><"col-sm-6">>rtip',
                                columnDefs: [
                                    {
                                        targets: "no-sort",
                                        orderable: false,
                                    },
                                ],
                                drawCallback: () => {
                                    // Re-initialize tooltips after table redraw
                                    SGA.ui.initTooltips()
                                },
                                initComplete: () => {
                                    console.log("DataTable initialized successfully")
                                },
                            })
                        } catch (error) {
                            console.error("Error initializing DataTable:", error)
                        }
                    }
                })
            },

            handleResponsive: () => {
                $(window).on("resize", () => {
                    if ($.fn.DataTable) {
                        $.fn.dataTable.tables({ visible: true, api: true }).columns.adjust()
                    }
                })
            },

            refresh: (tableId) => {
                if (tableId && $.fn.DataTable) {
                    try {
                        var table = $("#" + tableId).DataTable()
                        table.ajax.reload()
                    } catch (error) {
                        console.error("Error refreshing DataTable:", error)
                    }
                }
            },
        }

        // Form Enhancement
        SGA.forms = {
            init: function () {
                this.enhanceValidation()
                this.handleSubmissions()
                this.initializeSelects()
            },

            enhanceValidation: () => {
                // Add custom validation styling
                $("form").on("submit", function () {
                    var $form = $(this)
                    var isValid = true

                    // Check required fields
                    $form.find("[required]").each(function () {
                        var $field = $(this)
                        if (!$field.val()) {
                            $field.addClass("is-invalid")
                            isValid = false
                        } else {
                            $field.removeClass("is-invalid").addClass("is-valid")
                        }
                    })

                    if (!isValid) {
                        SGA.notifications.show("Por favor complete todos los campos requeridos", "warning")
                        return false
                    }
                })

                // Real-time validation
                $("input, select, textarea").on("blur change", function () {
                    var $field = $(this)
                    if ($field.attr("required") && !$field.val()) {
                        $field.addClass("is-invalid")
                    } else {
                        $field.removeClass("is-invalid").addClass("is-valid")
                    }
                })
            },

            handleSubmissions: () => {
                // Handle AJAX form submissions
                $('form[data-ajax="true"]').on("submit", function (e) {
                    e.preventDefault()
                    var $form = $(this)
                    var url = $form.attr("action")
                    var method = $form.attr("method") || "POST"
                    var data = $form.serialize()

                    $.ajax({
                        url: url,
                        type: method,
                        data: data,
                        beforeSend: () => {
                            SGA.ui.showLoading()
                        },
                        success: (response) => {
                            SGA.ui.hideLoading()
                            if (response.success) {
                                SGA.notifications.show(response.message || "Operacin exitosa", "success")
                                if (response.redirect) {
                                    window.location.href = response.redirect
                                }
                            } else {
                                SGA.notifications.show(response.message || "Error en la operacin", "error")
                            }
                        },
                        error: () => {
                            SGA.ui.hideLoading()
                            SGA.notifications.show("Error de conexin", "error")
                        },
                    })
                })
            },

            initializeSelects: () => {
                // Enhance select dropdowns
                $("select").each(function () {
                    var $select = $(this)
                    if (!$select.hasClass("no-enhance")) {
                        $select.addClass("enhanced-select")
                    }
                })
            },
        }

        // UI Enhancements
        SGA.ui = {
            init: function () {
                this.initTooltips()
                this.initModals()
                this.handleCardAnimations()
                this.initScrollEffects()
            },

            initTooltips: () => {
                // Initialize Bootstrap tooltips
                $('[data-toggle="tooltip"]').tooltip()

                // Add tooltips to action buttons
                $(".btn[title]").tooltip()
            },

            initModals: () => {
                // Handle confirmation modals
                $(document).on("click", "[data-confirm]", function (e) {
                    e.preventDefault()
                    var $btn = $(this)
                    var message = $btn.data("confirm") || "Est seguro de realizar esta accin?"
                    var href = $btn.attr("href")

                    SGA.ui.showConfirmModal(message, () => {
                        if (href) {
                            window.location.href = href
                        }
                    })
                })
            },

            handleCardAnimations: () => {
                // Animate cards on scroll
                $(window).on("scroll", () => {
                    $(".card, .panel").each(function () {
                        var $card = $(this)
                        var cardTop = $card.offset().top
                        var cardBottom = cardTop + $card.outerHeight()
                        var windowTop = $(window).scrollTop()
                        var windowBottom = windowTop + $(window).height()

                        if (cardBottom > windowTop && cardTop < windowBottom) {
                            $card.addClass("in-view")
                        }
                    })
                })
            },

            initScrollEffects: () => {
                // Smooth scroll for anchor links
                $('a[href^="#"]').on("click", function (e) {
                    e.preventDefault()
                    var target = $(this.getAttribute("href"))
                    if (target.length) {
                        $("html, body").animate(
                            {
                                scrollTop: target.offset().top - 70,
                            },
                            SGA.config.animationSpeed,
                        )
                    }
                })
            },

            showLoading: () => {
                if (!$("#loading-overlay").length) {
                    $("body").append('<div id="loading-overlay" class="loading-overlay"><div class="spinner"></div></div>')
                }
                $("#loading-overlay").fadeIn(200)
            },

            hideLoading: () => {
                $("#loading-overlay").fadeOut(200)
            },

            showConfirmModal: (message, callback) => {
                var modalHtml = `
                <div class="modal fade" id="confirmModal" tabindex="-1" role="dialog">
                    <div class="modal-dialog" role="document">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Confirmacin</h5>
                                <button type="button" class="close" data-dismiss="modal">
                                    <span>&times;</span>
                                </button>
                            </div>
                            <div class="modal-body">
                                <p>${message}</p>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                                <button type="button" class="btn btn-primary" id="confirmBtn">Confirmar</button>
                            </div>
                        </div>
                    </div>
                </div>
            `

                // Remove existing modal
                $("#confirmModal").remove()

                // Add new modal
                $("body").append(modalHtml)

                // Show modal
                $("#confirmModal").modal("show")

                // Handle confirmation
                $("#confirmBtn").on("click", () => {
                    $("#confirmModal").modal("hide")
                    if (callback) callback()
                })
            },
        }

        // Notifications System
        SGA.notifications = {
            show: function (message, type, duration) {
                type = type || "info"
                duration = duration || 5000

                var alertClass = "alert-" + (type === "error" ? "danger" : type)
                var iconClass = this.getIconClass(type)

                var notificationHtml = `
            <div class="alert ${alertClass} alert-dismissible fade show notification-alert" role="alert" style="margin-bottom: 10px;">
                <i class="${iconClass}" style="margin-right: 8px;"></i>
                ${message}
                <button type="button" class="close" data-dismiss="alert" aria-label="Close">
                    <span aria-hidden="true">&times;</span>
                </button>
            </div>
        `

                // Create notifications container if it doesn't exist
                if (!$("#notifications-container").length) {
                    $("body").append(
                        '<div id="notifications-container" style="position: fixed; top: 20px; right: 20px; z-index: 9999; max-width: 400px;"></div>',
                    )
                }

                var $notification = $(notificationHtml)
                $("#notifications-container").append($notification)

                // Auto-hide notification
                setTimeout(() => {
                    $notification.alert("close")
                }, duration)
            },

            success: function (message) {
                this.show(message, "success")
            },

            error: function (message) {
                this.show(message, "error")
            },

            info: function (message) {
                this.show(message, "info")
            },

            warning: function (message) {
                this.show(message, "warning")
            },

            getIconClass: (type) => {
                switch (type) {
                    case "success":
                        return "fas fa-check-circle"
                    case "error":
                        return "fas fa-exclamation-circle"
                    case "warning":
                        return "fas fa-exclamation-triangle"
                    default:
                        return "fas fa-info-circle"
                }
            },
        }

        // Statistics and Dashboard
        SGA.dashboard = {
            init: function () {
                this.animateCounters()
                this.initCharts()
            },

            animateCounters: () => {
                $(".stats-card h3").each(function () {
                    var $counter = $(this)
                    var target = Number.parseInt($counter.text().replace(/,/g, ""))

                    if (!isNaN(target)) {
                        $counter.text("0")
                        $({ count: 0 }).animate(
                            { count: target },
                            {
                                duration: 2000,
                                easing: "swing",
                                step: function () {
                                    $counter.text(Math.floor(this.count).toLocaleString())
                                },
                                complete: () => {
                                    $counter.text(target.toLocaleString())
                                },
                            },
                        )
                    }
                })
            },

            initCharts: () => {
                // Initialize any charts if Chart.js is available
                if (typeof Chart !== "undefined") {
                    // Chart initialization code would go here
                    console.log("Charts ready for initialization")
                }
            },
        }

        // Search and Filter functionality
        SGA.search = {
            init: function () {
                this.initGlobalSearch()
                this.initFilters()
            },

            initGlobalSearch: () => {
                // Global search functionality
                $("#globalSearch").on("keyup", function () {
                    var searchTerm = $(this).val().toLowerCase()

                    $(".searchable-item").each(function () {
                        var $item = $(this)
                        var text = $item.text().toLowerCase()

                        if (text.indexOf(searchTerm) === -1) {
                            $item.hide()
                        } else {
                            $item.show()
                        }
                    })
                })
            },

            initFilters: () => {
                // Filter dropdowns
                $("[data-filter]").on("change", function () {
                    var $filter = $(this)
                    var filterType = $filter.data("filter")
                    var filterValue = $filter.val()

                    $('[data-filter-target="' + filterType + '"]').each(function () {
                        var $item = $(this)
                        var itemValue = $item.data("filter-value")

                        if (filterValue === "" || itemValue === filterValue) {
                            $item.show()
                        } else {
                            $item.hide()
                        }
                    })
                })
            },
        }

        // Utility functions
        SGA.utils = {
            formatDate: (date) => new Date(date).toLocaleDateString("es-ES"),

            formatNumber: (number) => number.toLocaleString("es-ES"),

            debounce: (func, wait) => {
                var timeout
                return function executedFunction() {

                    var args = arguments
                    var later = () => {
                        timeout = null
                        func.apply(this, args)
                    }
                    clearTimeout(timeout)
                    timeout = setTimeout(later, wait)
                }
            },

            getCookie: (name) => {
                var value = "; " + document.cookie
                var parts = value.split("; " + name + "=")
                if (parts.length == 2) return parts.pop().split(";").shift()
            },

            setCookie: (name, value, days) => {
                var expires = ""
                if (days) {
                    var date = new Date()
                    date.setTime(date.getTime() + days * 24 * 60 * 60 * 1000)
                    expires = "; expires=" + date.toUTCString()
                }
                document.cookie = name + "=" + (value || "") + expires + "; path=/"
            },
        }

        // Charts functionality
        SGA.charts = {
            init: function () {
                this.setupChartDefaults()
                this.handleChartResize()
            },

            setupChartDefaults: () => {
                if (typeof Chart !== "undefined") {
                    Chart.defaults.font.family = "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif"
                    Chart.defaults.color = "#2e3d49"
                    Chart.defaults.plugins.legend.labels.usePointStyle = true
                }
            },

            handleChartResize: () => {
                $(window).on("resize", () => {
                    if (typeof Chart !== "undefined") {
                        Chart.helpers.each(Chart.instances, (instance) => {
                            instance.resize()
                        })
                    }
                })
            },

            createRadarChart: (canvasId, data, options) => {
                const ctx = document.getElementById(canvasId)
                if (!ctx) return null

                const defaultOptions = {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: "top",
                        },
                    },
                    scales: {
                        r: {
                            beginAtZero: true,
                            grid: {
                                color: "rgba(32, 178, 170, 0.1)",
                            },
                            pointLabels: {
                                color: "#20B2AA",
                                font: {
                                    size: 12,
                                    weight: "bold",
                                },
                            },
                        },
                    },
                }

                const mergedOptions = $.extend(true, {}, defaultOptions, options)

                return new Chart(ctx, {
                    type: "radar",
                    data: data,
                    options: mergedOptions,
                })
            },
        }

        // Initialize everything when document is ready
        $(document).ready(() => {
            // Initialize all modules
            SGA.sidebar.init()
            SGA.forms.init()
            SGA.ui.init()
            SGA.dashboard.init()
            SGA.search.init()
            SGA.charts.init()

            // Initialize DataTables with delay to ensure DOM is ready
            setTimeout(() => {
                SGA.dataTables.init()
            }, 100)

            // Handle ViewBag messages (for ASP.NET MVC)
            if (window.ViewBagMessage) {
                SGA.notifications.show(window.ViewBagMessage.text, window.ViewBagMessage.type)
            }

            // Initialize page-specific functionality
            SGA.pageInit()
        })

        // Page-specific initialization
        SGA.pageInit = () => {
            var page = $("body").data("page") || ""

            switch (page) {
                case "tutores":
                    // Tutores page specific code
                    break
                case "asesorados":
                    // Asesorados page specific code
                    break
                case "pat":
                    // PAT page specific code
                    break
                default:
                    // Default page code
                    break
            }
        }
    })(window.jQuery)

// Export SGA to global scope
window.SGA = SGA
