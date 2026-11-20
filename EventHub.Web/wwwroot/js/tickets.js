// My tickets page: QR codes, holographic tilt, and "Add to calendar".
(function () {
    // 1. QR code for each ticket
    document.querySelectorAll('.qr').forEach(function (el) {
        if (window.QRCode) {
            new QRCode(el, { text: el.dataset.code, width: 132, height: 132, correctLevel: QRCode.CorrectLevel.M });
        } else {
            el.textContent = el.dataset.code; // CDN blocked: the code below still works at the door
        }
    });

    // 2. Tilt + holographic sheen that follows the pointer (skipped for reduced motion and touch)
    var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var finePointer = window.matchMedia('(pointer: fine)').matches;
    if (!reduce && finePointer) {
        document.querySelectorAll('.ticket-card').forEach(function (card) {
            card.addEventListener('pointermove', function (e) {
                var r = card.getBoundingClientRect();
                var x = (e.clientX - r.left) / r.width;
                var y = (e.clientY - r.top) / r.height;
                card.classList.add('is-tilting');
                card.style.setProperty('--mx', (x * 100).toFixed(1) + '%');
                card.style.setProperty('--my', (y * 100).toFixed(1) + '%');
                card.style.setProperty('--ry', ((x - 0.5) * 8).toFixed(2) + 'deg');
                card.style.setProperty('--rx', ((0.5 - y) * 6).toFixed(2) + 'deg');
            });
            card.addEventListener('pointerleave', function () {
                card.classList.remove('is-tilting');
                ['--mx', '--my', '--rx', '--ry'].forEach(function (p) { card.style.removeProperty(p); });
            });
        });
    }

    // 3. Add to calendar: downloads an .ics file (works with Google, Outlook, Apple Calendar)
    var cal = document.getElementById('add-to-calendar');
    if (cal) {
        cal.addEventListener('click', function () {
            var esc = function (s) { return String(s).replace(/\\/g, '\\\\').replace(/([,;])/g, '\\$1').replace(/\n/g, '\\n'); };
            var d = cal.dataset;
            var stamp = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
            var ics = [
                'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Event Hub//Tickets//EN', 'BEGIN:VEVENT',
                'UID:' + d.start + '-' + Math.random().toString(36).slice(2) + '@eventhub',
                'DTSTAMP:' + stamp, 'DTSTART:' + d.start, 'DTEND:' + d.end,
                'SUMMARY:' + esc(d.title), 'LOCATION:' + esc(d.venue),
                'DESCRIPTION:' + esc('Your tickets are in Event Hub > My tickets.'),
                'END:VEVENT', 'END:VCALENDAR'
            ].join('\r\n');
            var a = document.createElement('a');
            a.href = URL.createObjectURL(new Blob([ics], { type: 'text/calendar' }));
            a.download = (d.title || 'event').replace(/[^\w\- ]+/g, '').trim() + '.ics';
            document.body.appendChild(a); a.click(); a.remove();
        });
    }
})();
