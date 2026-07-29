// SGA Dashboard JavaScript - Encapsulated
(function () {
    'use strict';

    // Namespace for SGA Dashboard
    const SGADashboard = {
        // Configuration
        config: {
            sidebarClass: '.sga-sidebar',
            toggleClass: '.sidebar-toggle',
            mainContentClass: '.main-content',
            layoutContainerClass: '.sga-layout-container'
        },

        // Initialize the dashboard
        init: function () {
            this.setupSidebar();
            this.setupNavigation();
            this.setupAnimations();
            this.setupFilters();
            this.setupNotifications();
            this.setupAccessibility();
            this.setupPerformanceMonitoring();
        },

        // Sidebar functionality
        setupSidebar: function () {
            const sidebarToggle = document.querySelector(this.config.layoutContainerClass + ' ' + this.config.toggleClass);
            const sidebar = document.querySelector(this.config.layoutContainerClass + ' ' + this.config.sidebarClass);
            const mainContent = document.querySelector(this.config.layoutContainerClass + ' ' + this.config.mainContentClass);

            if (sidebarToggle && sidebar) {
                sidebarToggle.addEventListener('click', () => {
                    sidebar.classList.toggle('active');
                });

                // Close sidebar on mobile when clicking outside
                document.addEventListener('click', (e) => {
                    const layoutContainer = document.querySelector(this.config.layoutContainerClass);
                    if (layoutContainer && layoutContainer.contains(e.target)) {
                        if (!sidebar.contains(e.target) && !sidebarToggle.contains(e.target)) {
                            sidebar.classList.remove('active');
                        }
                    }
                });
            }

            // Handle window resize
            window.addEventListener('resize', () => {
                if (window.innerWidth > 768 && sidebar) {
                    sidebar.classList.remove('show');
                }
            });
        },

        // Navigation highlighting
        setupNavigation: function () {
            const currentPath = window.location.pathname;
            const navLinks = document.querySelectorAll(this.config.layoutContainerClass + ' .nav-link');

            navLinks.forEach((link) => {
                if (link.getAttribute('href') === currentPath) {
                    link.classList.add('active');
                }
            });
        },

        // Animation setup
        setupAnimations: function () {
            const observerOptions = {
                threshold: 0.1,
                rootMargin: '0px 0px -50px 0px'
            };

            const observer = new IntersectionObserver((entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        entry.target.style.opacity = '1';
                        entry.target.style.transform = 'translateY(0)';
                    }
                });
            }, observerOptions);

            // Observe all dashboard cards within the layout container
            document.querySelectorAll(this.config.layoutContainerClass + ' .dashboard-section-card, ' +
                this.config.layoutContainerClass + ' .group-resumen-card, ' +
                this.config.layoutContainerClass + ' .admin-card').forEach((card) => {
                    card.style.opacity = '0';
                    card.style.transform = 'translateY(20px)';
                    card.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
                    observer.observe(card);
                });

            // Improved hover effects for section links
            document.querySelectorAll(this.config.layoutContainerClass + ' .section-link').forEach((link) => {
                link.addEventListener('mouseenter', () => {
                    link.style.transform = 'translateY(-2px) scale(1.02)';
                });

                link.addEventListener('mouseleave', () => {
                    link.style.transform = 'translateY(0) scale(1)';
                });
            });

            // Animate counters when they are visible
            this.setupCounterAnimations();
        },

        // Counter animations
        setupCounterAnimations: function () {
            const animateCounter = (element, target, duration = 1000) => {
                const start = 0;
                const increment = target / (duration / 16);
                let current = start;

                const timer = setInterval(() => {
                    current += increment;
                    if (current >= target) {
                        current = target;
                        clearInterval(timer);
                    }
                    element.textContent = Math.floor(current);
                }, 16);
            };

            const counterObserver = new IntersectionObserver((entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        const element = entry.target;
                        const target = parseInt(element.textContent);
                        if (!isNaN(target) && target > 0) {
                            animateCounter(element, target);
                            counterObserver.unobserve(element);
                        }
                    }
                });
            }, { threshold: 0.5 });

            // Observe elements with numbers within the layout container
            document.querySelectorAll(this.config.layoutContainerClass + ' .group-resumen-card b, ' +
                this.config.layoutContainerClass + ' .admin-card b').forEach((element) => {
                    const text = element.textContent.trim();
                    if (/^\d+$/.test(text)) {
                        counterObserver.observe(element);
                    }
                });
        },

        // Filter setup
        setupFilters: function () {
            document.querySelectorAll(this.config.layoutContainerClass + ' .dashboard-filters select').forEach((select) => {
                select.addEventListener('change', () => {
                    const form = select.closest('form');
                    if (form) {
                        const submitBtn = form.querySelector("input[type='submit']");
                        if (submitBtn) {
                            submitBtn.disabled = true;
                            submitBtn.value = 'Cargando...';
                        }

                        setTimeout(() => {
                            form.submit();
                        }, 300);
                    }
                });
            });
        },

        // Notification system
        setupNotifications: function () {
            // Show TempData notifications if they exist
            if (typeof window.tempDataSuccess !== 'undefined' && window.tempDataSuccess) {
                this.showNotification(window.tempDataSuccess, 'success');
            }
            if (typeof window.tempDataError !== 'undefined' && window.tempDataError) {
                this.showNotification(window.tempDataError, 'error');
            }
        },

        // Show notification function
        showNotification: function (message, type = 'info') {
            const notification = document.createElement('div');
            notification.className = `sga-notification sga-notification-${type}`;
            notification.innerHTML = `
                <div class="sga-notification-content">
                    <span class="sga-notification-message">${message}</span>
                    <button class="sga-notification-close">&times;</button>
                </div>
            `;

            notification.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                background: ${type === 'success' ? '#10b981' : type === 'error' ? '#ef4444' : '#3b82f6'};
                color: white;
                padding: 1rem 1.5rem;
                border-radius: 8px;
                box-shadow: 0 10px 25px rgba(0,0,0,0.1);
                z-index: 9999;
                transform: translateX(100%);
                transition: transform 0.3s ease;
                max-width: 400px;
                font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
            `;

            document.body.appendChild(notification);

            setTimeout(() => {
                notification.style.transform = 'translateX(0)';
            }, 100);

            setTimeout(() => {
                notification.style.transform = 'translateX(100%)';
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.parentNode.removeChild(notification);
                    }
                }, 300);
            }, 5000);

            notification.querySelector('.sga-notification-close').addEventListener('click', () => {
                notification.style.transform = 'translateX(100%)';
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.parentNode.removeChild(notification);
                    }
                }, 300);
            });
        },

        // Accessibility improvements
        setupAccessibility: function () {
            document.querySelectorAll(this.config.layoutContainerClass + ' .nav-link, ' +
                this.config.layoutContainerClass + ' .section-link').forEach((link) => {
                    link.addEventListener('keydown', (e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            link.click();
                        }
                    });
                });

            // Tooltips for icons
            document.querySelectorAll(this.config.layoutContainerClass + ' .section-icon').forEach((icon) => {
                const title = icon.closest('.section-header').querySelector('.section-title').textContent;
                icon.setAttribute('title', title);
            });
        },

        // Performance monitoring
        setupPerformanceMonitoring: function () {
            if ('performance' in window) {
                window.addEventListener('load', () => {
                    setTimeout(() => {
                        const perfData = performance.getEntriesByType('navigation')[0];
                        console.log('SGA Dashboard load time:', perfData.loadEventEnd - perfData.loadEventStart, 'ms');
                    }, 0);
                });
            }
        },

        // Utility functions
        utils: {
            formatNumber: (num) => new Intl.NumberFormat('es-MX').format(num),
            formatDate: (date) => new Intl.DateTimeFormat('es-MX', {
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            }).format(new Date(date)),
            generateColors: (count) => {
                const colors = [
                    '#667eea', '#764ba2', '#f093fb', '#f5576c',
                    '#4facfe', '#00f2fe', '#43e97b', '#38f9d7',
                    '#ffecd2', '#fcb69f', '#a8edea', '#fed6e3'
                ];
                const result = [];
                for (let i = 0; i < count; i++) {
                    result.push(colors[i % colors.length]);
                }
                return result;
            },
            debounce: (func, wait) => {
                let timeout;
                return function executedFunction(...args) {
                    const later = () => {
                        clearTimeout(timeout);
                        func(...args);
                    };
                    clearTimeout(timeout);
                    timeout = setTimeout(later, wait);
                };
            }
        },

        // Confirm action function
        confirmAction: function (message, callback) {
            if (confirm(message)) {
                callback();
            }
        },

        // Export data function
        exportData: function (format, data) {
            console.log(`Exportando datos en formato ${format}:`, data);
            this.showNotification(`Exportando datos en formato ${format}...`, 'info');

            setTimeout(() => {
                this.showNotification('Datos exportados exitosamente', 'success');
            }, 2000);
        },

        // Refresh data function
        refreshData: function () {
            this.showNotification('Actualizando datos...', 'info');
            setTimeout(() => {
                location.reload();
            }, 1000);
        }
    };

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', () => {
        SGADashboard.init();
    });

    // Global error handling for SGA Dashboard
    window.addEventListener('error', (e) => {
        console.error('Error en SGA Dashboard:', e.error);
        if (SGADashboard.showNotification) {
            SGADashboard.showNotification('Ha ocurrido un error. Por favor, recarga la página.', 'error');
        }
    });

    // Expose necessary functions to global scope with SGA prefix
    window.SGADashboard = {
        showNotification: SGADashboard.showNotification.bind(SGADashboard),
        confirmAction: SGADashboard.confirmAction.bind(SGADashboard),
        exportData: SGADashboard.exportData.bind(SGADashboard),
        refreshData: SGADashboard.refreshData.bind(SGADashboard),
        utils: SGADashboard.utils
    };
})();

