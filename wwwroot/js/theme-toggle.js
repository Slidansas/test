/**
 * AROKIS — Theme Toggle
 * Вставляет кнопку переключения темы в .arokis-topbar,
 * сохраняет выбор в localStorage.
 *
 * Приоритет темы:
 *   1. Системная тема Windows (window.__arokisSetTheme из C#)
 *   2. Ручной выбор пользователя (localStorage)
 *   3. prefers-color-scheme (fallback)
 *
 * Подключать на всех страницах:
 *   <script src="js/theme-toggle.js"></script>
 */
(function () {
    'use strict';

    const STORAGE_KEY = 'arokis-theme';
    const MANUAL_KEY  = 'arokis-theme-manual';
    const DARK        = 'dark';
    const LIGHT       = 'light';

    // ── Иконки (inline SVG) ──────────────────────────────────────────────
    const SUN_SVG = `<svg class="theme-toggle__sun" viewBox="0 0 24 24" fill="none"
        stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="4"/>
        <line x1="12" y1="2"  x2="12" y2="5"/>
        <line x1="12" y1="19" x2="12" y2="22"/>
        <line x1="4.22"  y1="4.22"  x2="6.34"  y2="6.34"/>
        <line x1="17.66" y1="17.66" x2="19.78" y2="19.78"/>
        <line x1="2"  y1="12" x2="5"  y2="12"/>
        <line x1="19" y1="12" x2="22" y2="12"/>
        <line x1="4.22"  y1="19.78" x2="6.34"  y2="17.66"/>
        <line x1="17.66" y1="6.34"  x2="19.78" y2="4.22"/>
    </svg>`;

    const MOON_SVG = `<svg class="theme-toggle__moon" viewBox="0 0 24 24" fill="none"
        stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
    </svg>`;

    // ── Построить HTML переключателя ─────────────────────────────────────
    function buildToggle() {
        const btn = document.createElement('button');
        btn.className = 'theme-toggle';
        btn.setAttribute('aria-label', 'Переключить тему');
        btn.setAttribute('title', 'Светлая / Тёмная тема');
        btn.innerHTML = `
            <div class="theme-toggle__track">
                <div class="theme-toggle__glow"></div>
                <div class="theme-toggle__stars">
                    <span></span><span></span><span></span>
                </div>
                <div class="theme-toggle__thumb">
                    ${SUN_SVG}
                    ${MOON_SVG}
                </div>
            </div>
        `;
        btn.style.cssText = 'background:none;border:none;padding:0;cursor:pointer;';
        btn.addEventListener('click', toggleManual);
        return btn;
    }

    // ── Заморозить / разморозить анимации ────────────────────────────────
    // Пока на <html> висит класс .theme-switching — все transition отключены
    // (см. arokis.css блок «ЗАМОРОЗКА АНИМАЦИЙ»).
    let _thawTimer = null;
    function freezeTransitions() {
        document.documentElement.classList.add('theme-switching');
        if (_thawTimer) clearTimeout(_thawTimer);
        // Одного кадра достаточно, чтобы браузер применил новые CSS-переменные
        // без анимации; после этого разрешаем обычные hover-переходы.
        _thawTimer = setTimeout(() => {
            document.documentElement.classList.remove('theme-switching');
        }, 50);
    }

    // ── Применить тему ───────────────────────────────────────────────────
    function applyTheme(theme) {
        freezeTransitions();
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem(STORAGE_KEY, theme);
        // Синхронизируем ApexCharts — MutationObserver тоже сработает,
        // но явный вызов надёжнее при быстрой смене темы
        if (typeof window.__apexSyncTheme === 'function') window.__apexSyncTheme();
    }

    // ── Переключить вручную (клик по кнопке) ─────────────────────────────
    function toggleManual() {
        const current = document.documentElement.getAttribute('data-theme') || LIGHT;
        localStorage.setItem(MANUAL_KEY, '1');
        applyTheme(current === DARK ? LIGHT : DARK);
    }

    // ── Вставить в topbar ────────────────────────────────────────────────
    function injectToggle() {
        const topbar = document.querySelector('.arokis-topbar');
        if (!topbar) return;

        const rightDiv  = topbar.querySelector('div:last-child');
        const toggleBtn = buildToggle();

        if (rightDiv) {
            rightDiv.insertBefore(toggleBtn, rightDiv.firstChild);
        } else {
            topbar.appendChild(toggleBtn);
        }
    }

    // ── Публичный API — вызывается из C# (Window1.xaml.cs) ───────────────
    /**
     * @param {string}  theme      Системная тема Windows: 'dark' | 'light'
     * @param {boolean} fromSystem true  — реальная смена темы Windows
     *                                     (сбрасывает ручной выбор, применяет системную)
     *                             false — навигация между страницами
     *                                     (если есть ручной выбор — применяем его,
     *                                      иначе применяем системную тему)
     */
    window.__arokisSetTheme = function (theme, fromSystem) {
        if (fromSystem) {
            // Пользователь сменил тему Windows — сбрасываем ручной выбор
            localStorage.removeItem(MANUAL_KEY);
            applyTheme(theme);
        } else {
            // Навигация — уважаем ручной выбор пользователя
            const isManual = localStorage.getItem(MANUAL_KEY) === '1';
            const saved    = localStorage.getItem(STORAGE_KEY);
            applyTheme(isManual && saved ? saved : theme);
        }
    };

    // ── Инициализация ────────────────────────────────────────────────────
    function init() {
        // Тема уже установлена инлайн-скриптом в <head> ДО рендера страницы.
        // Здесь мы только:
        //   1. Убеждаемся что localStorage синхронизирован с текущим data-theme
        //   2. Вставляем кнопку переключения в топбар
        //   3. Синхронизируем ApexCharts (они могут создаться уже после init)

        const current = document.documentElement.getAttribute('data-theme') || LIGHT;
        // Сохраняем в localStorage чтобы следующая страница тоже знала тему
        localStorage.setItem(STORAGE_KEY, current);

        injectToggle();

        // Синхронизируем ApexCharts после рендера (они создаются асинхронно)
        if (typeof window.__apexSyncTheme === 'function') {
            setTimeout(window.__apexSyncTheme, 200);
        }

        // Fallback: следим за prefers-color-scheme если C# не прислал тему
        window.matchMedia('(prefers-color-scheme: dark)')
            .addEventListener('change', e => {
                if (localStorage.getItem(MANUAL_KEY) !== '1') {
                    applyTheme(e.matches ? DARK : LIGHT);
                }
            });
    }

    // Запускаем после DOMContentLoaded — к этому моменту топбар уже есть в DOM
    // Вспышки нет т.к. тема уже применена инлайн-скриптом в <head>
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
