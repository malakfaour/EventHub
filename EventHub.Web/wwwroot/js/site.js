// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Rows with data-href act like links: click anywhere on the row to open it (links and buttons inside still work normally).
document.addEventListener('click', function (e) {
    var row = e.target.closest('tr[data-href]');
    if (!row || e.target.closest('a, button, input, select, textarea, label, form')) return;
    if (window.getSelection && String(window.getSelection())) return; // user is selecting text
    window.location.href = row.dataset.href;
});
