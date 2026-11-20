// Live updates: a SignalR connection that shows a toast when something changes and refreshes the page you are looking at.
(function () {
    if (!window.signalR) return;

    // Which pages show data affected by each kind of change (and are safe to reload: no half-filled forms).
    var reloadOn = { orders: ['orders', 'events', 'products'], invoices: ['invoices', 'orders'] };
    var safeActions = ['index', 'details'];

    var box = document.createElement('div');
    box.className = 'live-toasts';
    box.setAttribute('aria-live', 'polite');
    document.body.appendChild(box);

    function toast(n) {
        var el = document.createElement('a');
        el.className = 'live-toast';
        el.href = n.url || '#';
        var t = document.createElement('strong'); t.textContent = n.title;
        var m = document.createElement('span'); m.textContent = n.message;
        el.appendChild(t); el.appendChild(m);
        box.appendChild(el);
        setTimeout(function () { el.classList.add('out'); setTimeout(function () { el.remove(); }, 400); }, 7000);
    }

    // A toast would vanish with the reload, so it is parked in sessionStorage and shown again by the next page.
    try {
        var parked = sessionStorage.getItem('live.toast');
        if (parked) { sessionStorage.removeItem('live.toast'); toast(JSON.parse(parked)); }
    } catch (e) { /* storage unavailable: no toast after reload, the page still refreshes */ }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/live')
        .withAutomaticReconnect()
        .build();

    connection.on('notify', function (n) {
        var page = document.body.dataset;
        var pages = reloadOn[n.kind] || [];
        if (pages.indexOf(page.controller) >= 0 && safeActions.indexOf(page.action) >= 0) {
            try { sessionStorage.setItem('live.toast', JSON.stringify(n)); } catch (e) { toast(n); }
            setTimeout(function () { window.location.reload(); }, 600);
        } else {
            toast(n);
        }
    });

    connection.start().catch(function () { /* live updates are a bonus; the page works without them */ });
})();
