// Shared Chart.js setup (same file in EventHub.Web and SupplierSystem.Web).
// Doughnuts use the poster inks; bars use the app's accent. Text, grid lines and bars
// follow light/dark mode: ui.js fires "themechange" and every chart repaints.
(function () {
    if (!window.Chart) return;
    const palette = ['#0B5CAD', '#FF48B0', '#00A95C', '#FF6C2F', '#ACA3E8', '#FFE800', '#00838A', '#1D1D1B'];
    const charts = [];

    function css(name) { return getComputedStyle(document.body).getPropertyValue(name).trim(); }

    function applyColors() {
        Chart.defaults.color = css('--muted');
        Chart.defaults.borderColor = css('--row-border');
        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        charts.forEach(function (ch) {
            const ds = ch.data.datasets[0];
            if (ch.config.type === 'doughnut') ds.borderColor = css('--surface');
            else ds.backgroundColor = css('--accent');
            ch.update('none');
        });
    }

    /** Draws a bar or doughnut chart into the canvas with this id. */
    window.themedChart = function (id, type, labels, data, label) {
        const el = document.getElementById(id);
        if (!el) return;
        const circular = type === 'doughnut';
        charts.push(new Chart(el, {
            type: type,
            data: { labels: labels, datasets: [{ label: label, data: data,
                backgroundColor: circular ? palette : css('--accent'),
                borderColor: circular ? css('--surface') : undefined,
                borderRadius: circular ? 0 : 3, maxBarThickness: 36 }] },
            options: { maintainAspectRatio: false,
                plugins: { legend: { display: circular, position: 'bottom', labels: { boxWidth: 12, boxHeight: 12 } } },
                scales: circular ? {} : { y: { beginAtZero: true, ticks: { precision: 0 } }, x: { grid: { display: false } } } }
        }));
    };

    applyColors();
    document.addEventListener('themechange', applyColors);
})();
