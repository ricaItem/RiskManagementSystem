(function () {
    var mapEl = document.getElementById('monitoring-map');
    if (mapEl && typeof L !== 'undefined') {
        var lat = parseFloat(mapEl.getAttribute('data-lat')) || 7.0707;
        var lon = parseFloat(mapEl.getAttribute('data-lon')) || 125.6083;
        var map = L.map('monitoring-map').setView([lat, lon], 14);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(map);
        L.marker([lat, lon]).addTo(map);
        var riskRadiusM = 500;
        L.circle([lat, lon], { radius: riskRadiusM, color: '#ef4444', fillColor: '#ef4444', fillOpacity: 0.15, weight: 2 }).addTo(map);
    }

    // Auto-sync every 10 minutes (ON/OFF toggle)
    var AUTO_SYNC_KEY = 'monitoringAutoSync';
    var INTERVAL_MS = 10 * 60 * 1000; // 10 minutes
    var form = document.getElementById('monitoring-sync-form');
    var siteSelect = document.getElementById('monitoring-site-select');
    var btnOff = document.getElementById('auto-sync-off');
    var btnOn = document.getElementById('auto-sync-on');
    var statusEl = document.getElementById('auto-sync-status');
    var countdownEl = document.getElementById('auto-sync-countdown');
    var autoSyncTimer = null;
    var countdownTimer = null;

    function getToken() {
        if (!form) return '';
        var input = form.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function getSiteId() {
        if (siteSelect) return siteSelect.value || form.querySelector('input[name="siteId"]').value;
        return form ? (form.querySelector('input[name="siteId"]') || {}).value : '';
    }

    function runAutoSync() {
        if (!form || !getToken()) return;
        var action = form.getAttribute('action') || form.action;
        var body = new FormData();
        body.append('__RequestVerificationToken', getToken());
        body.append('siteId', getSiteId());
        fetch(action, { method: 'POST', body: body, redirect: 'follow' })
            .then(function () { window.location.reload(); })
            .catch(function () {});
    }

    function setUiOn(on) {
        if (!btnOff || !btnOn) return;
        if (on) {
            btnOff.classList.remove('bg-slate-200', 'dark:bg-slate-600', 'border-slate-300', 'dark:border-slate-500');
            btnOff.classList.add('bg-slate-100', 'dark:bg-slate-700', 'border-transparent');
            btnOn.classList.remove('bg-slate-100', 'dark:bg-slate-700', 'border-transparent', 'text-slate-500', 'dark:text-slate-400');
            btnOn.classList.add('bg-emerald-600', 'dark:bg-emerald-600', 'text-white', 'border-emerald-500');
            if (statusEl) statusEl.classList.remove('hidden');
            startCountdown();
        } else {
            btnOn.classList.remove('bg-emerald-600', 'dark:bg-emerald-600', 'text-white', 'border-emerald-500');
            btnOn.classList.add('bg-slate-100', 'dark:bg-slate-700', 'text-slate-500', 'dark:text-slate-400', 'border-transparent');
            btnOff.classList.remove('bg-slate-100', 'dark:bg-slate-700', 'border-transparent');
            btnOff.classList.add('bg-slate-200', 'dark:bg-slate-600', 'border-slate-300', 'dark:border-slate-500');
            if (statusEl) statusEl.classList.add('hidden');
            if (countdownTimer) clearInterval(countdownTimer);
        }
    }

    var nextSyncAt = null;
    function startCountdown() {
        nextSyncAt = Date.now() + INTERVAL_MS;
        function tick() {
            if (!countdownEl || !nextSyncAt) return;
            var left = Math.max(0, Math.ceil((nextSyncAt - Date.now()) / 1000));
            var m = Math.floor(left / 60);
            var s = left % 60;
            countdownEl.textContent = m + ':' + (s < 10 ? '0' : '') + s;
            if (left <= 0) nextSyncAt = null;
        }
        tick();
        if (countdownTimer) clearInterval(countdownTimer);
        countdownTimer = setInterval(tick, 1000);
    }

    function startAutoSync() {
        if (autoSyncTimer) clearInterval(autoSyncTimer);
        autoSyncTimer = setInterval(runAutoSync, INTERVAL_MS);
        nextSyncAt = Date.now() + INTERVAL_MS;
        setUiOn(true);
        try { localStorage.setItem(AUTO_SYNC_KEY, 'true'); } catch (e) {}
    }

    function stopAutoSync() {
        if (autoSyncTimer) clearInterval(autoSyncTimer);
        autoSyncTimer = null;
        nextSyncAt = null;
        setUiOn(false);
        try { localStorage.setItem(AUTO_SYNC_KEY, 'false'); } catch (e) {}
    }

    if (btnOff) btnOff.addEventListener('click', function () { stopAutoSync(); });
    if (btnOn) btnOn.addEventListener('click', function () { startAutoSync(); });

    try {
        if (localStorage.getItem(AUTO_SYNC_KEY) === 'true' && form) startAutoSync();
    } catch (e) {}
})();
