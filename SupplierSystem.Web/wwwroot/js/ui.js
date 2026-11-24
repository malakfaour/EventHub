// Shared UI behaviour (same file in EventHub.Web and SupplierSystem.Web):
// - theme toggle: light / dark, remembered in localStorage (the <head> applies it before paint, so there is no flash)
// - off-canvas sidebar on phones and tablets: open with the menu button, close with the backdrop, the X or Escape
(function () {
    var root = document.documentElement;
    var KEY = 'ui.theme';

    function applyTheme(theme) {
        root.setAttribute('data-theme', theme);
        root.setAttribute('data-bs-theme', theme);
        document.querySelectorAll('[data-theme-toggle]').forEach(function (b) {
            b.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
            b.setAttribute('title', theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
        });
        document.dispatchEvent(new CustomEvent('themechange', { detail: theme }));
    }

    applyTheme(root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light');

    var app = document.querySelector('.app');
    function setNav(open) {
        if (!app) return;
        app.classList.toggle('nav-open', open);
        document.querySelectorAll('[data-nav-open]').forEach(function (b) { b.setAttribute('aria-expanded', open ? 'true' : 'false'); });
        if (open) { var first = app.querySelector('.sidebar .side-link'); if (first) first.focus({ preventScroll: true }); }
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-theme-toggle]')) {
            var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            try { localStorage.setItem(KEY, next); } catch (err) { /* private mode: the choice lasts for this page only */ }
            applyTheme(next);
            return;
        }
        if (e.target.closest('[data-nav-open]')) { setNav(true); return; }
        if (e.target.closest('[data-nav-close]')) { setNav(false); }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && app && app.classList.contains('nav-open')) setNav(false);
    });
})();
