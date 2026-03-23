// ═══════════════════════════════════════════════════════
// AROKIS — apex-theme-patch.js
// Синхронизирует все экземпляры ApexCharts с текущей темой.
//
// Запускается в двух случаях:
//   1. При смене data-theme на <html> (MutationObserver)
//   2. При загрузке страницы — через window.__apexSyncTheme(),
//      которую вызывает theme-toggle.js после установки темы.
// ═══════════════════════════════════════════════════════

(function () {
    function getApexTheme() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function getApexColors(mode) {
        return mode === 'dark'
            ? { bg: 'transparent', text: '#8b949e', grid: 'rgba(47,184,196,0.10)', legend: '#e6edf3' }
            : { bg: 'transparent', text: '#6b7280', grid: 'rgba(47,184,196,0.15)', legend: '#374151' };
    }

    function updateAllCharts() {
        const mode   = getApexTheme();
        const colors = getApexColors(mode);

        if (typeof Apex === 'undefined' || !Apex._chartInstances) return;

        Object.values(Apex._chartInstances).forEach(chart => {
            try {
                chart.updateOptions({
                    theme:   { mode },
                    chart:   { background: colors.bg },
                    xaxis:   { labels: { style: { colors: colors.text } },
                               title: { style: { color: colors.text } } },
                    yaxis:   { labels: { style: { colors: colors.text } },
                               title: { style: { color: colors.text } } },
                    legend:  { labels: { colors: colors.legend } },
                    grid:    { borderColor: colors.grid },
                    tooltip: { theme: mode }
                }, false, false);
            } catch(e) { /* chart может быть уничтожен */ }
        });
    }

    // Публичный метод — вызывается из theme-toggle.js после applyTheme()
    window.__apexSyncTheme = updateAllCharts;

    // MutationObserver — реагирует на смену data-theme в реальном времени
    const observer = new MutationObserver(mutations => {
        mutations.forEach(m => {
            if (m.attributeName === 'data-theme') updateAllCharts();
        });
    });
    observer.observe(document.documentElement, { attributes: true });

    // Запуск при загрузке страницы — на случай если data-theme уже установлен
    // до создания графиков (theme-toggle.js работает раньше ApexCharts).
    // Используем небольшую задержку, чтобы все графики успели отрендериться.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => setTimeout(updateAllCharts, 300));
    } else {
        setTimeout(updateAllCharts, 300);
    }
})();
