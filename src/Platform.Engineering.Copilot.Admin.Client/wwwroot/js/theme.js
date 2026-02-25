// Theme management for Platform Engineering Admin Dashboard
// Provides JS interop for theme switching (Dark/Light/Auto)

let themeWatcherCallback = null;
let mediaQuery = null;

window.themeInterop = {
    setTheme: function (theme) {
        if (theme === 'Auto') {
            const systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
            document.documentElement.setAttribute('data-bs-theme', systemTheme);
        } else {
            document.documentElement.setAttribute('data-bs-theme', theme.toLowerCase());
        }
        localStorage.setItem('platform_engineering_theme', theme);
    },

    getSystemTheme: function () {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'Dark' : 'Light';
    },

    watchSystemTheme: function (dotNetRef) {
        mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        themeWatcherCallback = function (e) {
            const newTheme = e.matches ? 'Dark' : 'Light';
            dotNetRef.invokeMethodAsync('OnSystemThemeChanged', newTheme);
        };
        mediaQuery.addEventListener('change', themeWatcherCallback);
    },

    disposeThemeWatcher: function () {
        if (mediaQuery && themeWatcherCallback) {
            mediaQuery.removeEventListener('change', themeWatcherCallback);
            themeWatcherCallback = null;
            mediaQuery = null;
        }
    }
};
