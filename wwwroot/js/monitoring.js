(function () {
    var mapEl = document.getElementById('monitoring-map');
    var map = null;
    var markers = {};
    var circles = {};
    var activeSiteId = null;
    var mapData = [];
    var layerSites = true;
    var layerGeofence = true;
    var layerRisksOnly = false;

    var prefersReducedMotion = typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var transitionClass = prefersReducedMotion ? '' : 'transition-all duration-300';
    var pulseClass = prefersReducedMotion ? '' : 'monitoring-marker-pulse';

    function getSitesFromData() {
        try {
            var json = mapEl && mapEl.getAttribute('data-sites');
            if (json) return JSON.parse(json);
        } catch (e) {}
        return [];
    }

    function getSelectedSiteId() {
        if (!mapEl) return null;
        var id = mapEl.getAttribute('data-selected-site-id');
        return id != null && id !== '' ? parseInt(id, 10) : null;
    }

    // Initialize Map
    if (mapEl && typeof L !== 'undefined') {
        var defaultLat = parseFloat(mapEl.getAttribute('data-lat')) || 7.0707;
        var defaultLon = parseFloat(mapEl.getAttribute('data-lon')) || 125.6083;

        map = L.map('monitoring-map', {
            zoomControl: false,
            attributionControl: false
        }).setView([defaultLat, defaultLon], 13);

        L.control.zoom({ position: 'bottomright' }).addTo(map);

        L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
            subdomains: 'abcd',
            maxZoom: 20
        }).addTo(map);

        fetchMapData();
    }

    function fetchMapData() {
        if (!map) return;

        fetch('/Client/Risks/GetMapData')
            .then(function (response) { return response.json(); })
            .then(function (data) {
                mapData = data || [];
                updateMapMarkers(mapData);
                var selectedId = getSelectedSiteId();
                if (selectedId && mapData.length) {
                    var site = mapData.find(function (s) { return s.siteId === selectedId; });
                    if (site) {
                        map.setView([site.latitude, site.longitude], 14);
                    }
                }
            })
            .catch(function (err) { console.error('Error loading map data', err); });
    }

    function applyLayerVisibility() {
        var showSites = layerSites && (!layerRisksOnly || true);
        var showCircles = layerGeofence;
        mapData.forEach(function (site) {
            var show = !layerRisksOnly || (site.activeRiskCount != null && site.activeRiskCount > 0);
            if (markers[site.siteId]) {
                markers[site.siteId].setOpacity(showSites && show ? 1 : 0);
                markers[site.siteId]._icon.style.pointerEvents = showSites && show ? 'auto' : 'none';
            }
            if (circles[site.siteId]) {
                circles[site.siteId].setStyle({ opacity: showCircles && show ? 1 : 0, fillOpacity: showCircles && show ? 0.1 : 0 });
                circles[site.siteId].bringToFront();
            }
        });
    }

    function updateMapMarkers(sites) {
        if (!map) return;

        function createSiteIcon(severity, hasAlerts) {
            var sev = (severity || 'None').toLowerCase();
            var severityClass = sev === 'critical'
                ? 'severity-critical'
                : sev === 'high'
                    ? 'severity-high'
                    : sev === 'medium'
                        ? 'severity-medium'
                        : 'severity-normal';
            var alertsClass = hasAlerts ? ' has-alerts' : '';
            var html =
                '<div class=\"site-marker ' + severityClass + alertsClass + '\">' +
                    '<div class=\"pin\"></div>' +
                    '<div class=\"pulse\"></div>' +
                '</div>';
            return L.divIcon({
                className: 'monitoring-site-icon ' + transitionClass,
                html: html,
                iconSize: [32, 32],
                iconAnchor: [16, 16],
                popupAnchor: [0, -16]
            });
        }

        sites.forEach(function (site) {
            var severity = site.maxSeverity || 'None';
            var show = !layerRisksOnly || (site.activeRiskCount != null && site.activeRiskCount > 0);
            var hasAlerts = site.activeAlertCount != null && site.activeAlertCount > 0;
            var icon = createSiteIcon(severity, hasAlerts);

            if (markers[site.siteId]) {
                markers[site.siteId].setLatLng([site.latitude, site.longitude]);
                markers[site.siteId].setIcon(icon);
                markers[site.siteId].setOpacity(layerSites && show ? 1 : 0);
            } else {
                var marker = L.marker([site.latitude, site.longitude], { icon: icon }).addTo(map);
                marker.on('click', function () {
                    openDrawer(site.siteId);
                    highlightGeofence(site.siteId, true);
                });
                marker._siteId = site.siteId;
                markers[site.siteId] = marker;
                if (!layerSites || !show) marker.setOpacity(0);
            }

            var color = severity === 'Critical' ? '#ef4444' : severity === 'High' ? '#f59e0b' : '#10b981';
            if (circles[site.siteId]) {
                circles[site.siteId].setLatLng([site.latitude, site.longitude]);
                circles[site.siteId].setStyle({ color: color, fillColor: color, fillOpacity: 0.1, weight: 1 });
                circles[site.siteId].setOpacity(layerGeofence && show ? 1 : 0);
            } else {
                var circle = L.circle([site.latitude, site.longitude], {
                    radius: 500,
                    color: color,
                    fillColor: color,
                    fillOpacity: 0.1,
                    weight: 1
                }).addTo(map);
                circle.on('click', function () {
                    openDrawer(site.siteId);
                    highlightGeofence(site.siteId, true);
                });
                circle.on('mouseover', function () {
                    if (!activeSiteId || circle._siteId !== activeSiteId) this.setStyle({ fillOpacity: 0.2, weight: 2 });
                });
                circle.on('mouseout', function () {
                    if (circle._siteId !== activeSiteId) this.setStyle({ fillOpacity: 0.1, weight: 1 });
                });
                circle._siteId = site.siteId;
                circles[site.siteId] = circle;
                if (!layerGeofence || !show) circle.setStyle({ opacity: 0, fillOpacity: 0 });
            }
        });

        if (sites.length > 0 && !activeSiteId) {
            var group = new L.featureGroup(Object.values(markers).filter(Boolean));
            if (group.getBounds().isValid()) map.fitBounds(group.getBounds().pad(0.2));
        }
        applyLayerVisibility();
    }

    function highlightGeofence(siteId, selected) {
        mapData.forEach(function (s) {
            var c = circles[s.siteId];
            if (!c) return;
            if (s.siteId === siteId && selected) {
                c.setStyle({ fillOpacity: 0.25, weight: 2 });
                c.bringToFront();
            } else {
                var sev = s.maxSeverity || 'None';
                c.setStyle({ fillOpacity: 0.1, weight: 1 });
            }
        });
    }

    function pulseMarker(siteId) {
        var marker = markers[siteId];
        if (!marker || !marker._icon) return;
        if (prefersReducedMotion) return;
        marker._icon.classList.add(pulseClass);
        setTimeout(function () {
            if (marker._icon) marker._icon.classList.remove(pulseClass);
        }, 1500);
    }

    // Site alerts modal state
    var drawerAlerts = [];
    var drawerAlertFilter = 'today';
    var drawerShowResolved = false;
    var drawerFiltersInitialized = false;

    function applyAlertFiltersAndRender() {
        var list = document.getElementById('drawer-alerts-list');
        if (!list) return;

        list.innerHTML = '';
        if (!drawerAlerts || drawerAlerts.length === 0) {
            list.innerHTML = '<div class="text-xs text-slate-400 italic text-center p-4">No active alerts</div>';
            return;
        }

        var now = new Date();
        var startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        var sevenDaysAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

        var filtered = drawerAlerts.filter(function (alert) {
            try {
                var t = new Date(alert.triggeredAt);
                if (drawerAlertFilter === 'today' && (t < startOfToday || t > now)) return false;
                if (drawerAlertFilter === '7d' && (t < sevenDaysAgo || t > now)) return false;
            } catch (e) { }
            if (!drawerShowResolved && alert.status === 'Resolved') return false;
            return true;
        });

        if (filtered.length === 0) {
            list.innerHTML = '<div class="text-xs text-slate-400 italic text-center p-4">No alerts in this range</div>';
        }

        filtered.forEach(function (alert) {
            var div = document.createElement('div');
            var isCritical = alert.severity === 'Critical';
            var bgClass = isCritical ? 'bg-rose-50 border-rose-200 dark:bg-rose-900/20 dark:border-rose-800' : 'bg-amber-50 border-amber-200 dark:bg-amber-900/20 dark:border-amber-800';
            var textClass = isCritical ? 'text-rose-700 dark:text-rose-300' : 'text-amber-700 dark:text-amber-300';
            var resolvedClass = alert.status === 'Resolved' ? ' opacity-70' : '';
            div.className = 'p-3 rounded-lg border ' + bgClass + ' ' + textClass + ' hover:bg-opacity-90 ' + transitionClass + resolvedClass;
            div.setAttribute('data-alert-id', alert.alertId);
            div.setAttribute('data-site-id', alert.siteId || 0);
            div.setAttribute('role', 'button');
            div.setAttribute('tabIndex', 0);

            var triggeredText = '';
            try {
                triggeredText = new Date(alert.triggeredAt).toLocaleString();
            } catch (e) {
                triggeredText = alert.triggeredAt || '';
            }
            var statusText = alert.status === 'Resolved' && alert.resolvedAtUtc ? ' · Resolved' : (alert.status === 'Resolved' ? ' · Resolved' : '');

            div.innerHTML =
                '<div class="flex justify-between items-start mb-1">' +
                    '<span class="font-bold text-xs uppercase tracking-wider">' + (alert.ruleName || '') + '</span>' +
                    '<span class="text-[10px] font-black px-1.5 py-0.5 rounded-full bg-white/60 text-slate-700 dark:bg-slate-900/70 dark:text-slate-100">' + (alert.severity || '') + '</span>' +
                '</div>' +
                '<div class="text-xs opacity-90 mb-2">' + (alert.measuredValues || '') + '</div>' +
                '<div class="flex items-center justify-between gap-2 text-[10px] opacity-75 mb-2">' +
                    '<span>' + triggeredText + statusText + '</span>' +
                '</div>' +
                '<div class="mt-2 flex flex-wrap gap-2">' +
                    '<button type="button" class="drawer-create-plan inline-flex items-center rounded-lg bg-slate-900 px-3 py-1.5 text-[10px] font-black uppercase tracking-wider text-white shadow-sm hover:bg-indigo-600 dark:bg-white dark:text-slate-900 dark:hover:bg-indigo-200">Create Plan</button>' +
                    '<button type="button" class="drawer-open-risk inline-flex items-center rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-[10px] font-black uppercase tracking-wider text-indigo-600 shadow-sm hover:bg-indigo-50 dark:border-slate-600 dark:bg-slate-800 dark:text-indigo-400 dark:hover:bg-slate-700">Open Risk</button>' +
                '</div>';

            div.addEventListener('click', function (ev) {
                if (ev.target.closest('.drawer-create-plan') || ev.target.closest('.drawer-open-risk')) return;
                var sid = parseInt(div.getAttribute('data-site-id') || '0', 10);
                if (sid) {
                    pulseMarker(sid);
                    highlightGeofence(sid, true);
                }
            });
            div.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    div.click();
                }
            });

            var createBtn = div.querySelector('.drawer-create-plan');
            var openBtn = div.querySelector('.drawer-open-risk');
            var alertId = alert.alertId;

            function postAlertAction(actionName) {
                var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                var token = tokenInput ? tokenInput.value : null;
                var form = document.createElement('form');
                form.method = 'post';
                form.action = '/Client/Risks/' + actionName;
                var inputId = document.createElement('input');
                inputId.type = 'hidden';
                inputId.name = 'alertId';
                inputId.value = alertId;
                form.appendChild(inputId);
                if (token) {
                    var tokenField = document.createElement('input');
                    tokenField.type = 'hidden';
                    tokenField.name = '__RequestVerificationToken';
                    tokenField.value = token;
                    form.appendChild(tokenField);
                }
                document.body.appendChild(form);
                form.submit();
            }

            if (createBtn) {
                createBtn.addEventListener('click', function (e) {
                    e.stopPropagation();
                    postAlertAction('CreateMitigationPlanFromAlert');
                });
            }
            if (openBtn) {
                openBtn.addEventListener('click', function (e) {
                    e.stopPropagation();
                    postAlertAction('OpenRiskFromAlert');
                });
            }

            list.appendChild(div);
        });

        var tabs = document.querySelectorAll('.drawer-filter-tab');
        tabs.forEach(function (tab) {
            var key = tab.getAttribute('data-alert-filter') || 'today';
            if (key === drawerAlertFilter) {
                tab.classList.add('bg-white', 'text-slate-900', 'shadow-sm', 'dark:bg-slate-900', 'dark:text-white');
            } else {
                tab.classList.remove('bg-white', 'text-slate-900', 'shadow-sm', 'dark:bg-slate-900', 'dark:text-white');
            }
        });
    }

    function ensureAlertFiltersInitialized() {
        if (drawerFiltersInitialized) return;
        drawerFiltersInitialized = true;
        var tabs = document.querySelectorAll('.drawer-filter-tab');
        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                var key = tab.getAttribute('data-alert-filter') || 'today';
                drawerAlertFilter = key;
                applyAlertFiltersAndRender();
            });
        });
        var showResolvedToggle = document.getElementById('drawer-show-resolved');
        if (showResolvedToggle) {
            drawerShowResolved = showResolvedToggle.checked;
            showResolvedToggle.addEventListener('change', function () {
                drawerShowResolved = !!this.checked;
                applyAlertFiltersAndRender();
            });
        }
    }

    // Modal: move map into modal, body scroll lock, focus management
    var mapWrapperOriginalParent = null;
    var mapWrapperNextSibling = null;
    var modalTriggerElement = null;

    window.openDrawer = function (siteId) {
        activeSiteId = siteId;
        highlightGeofence(siteId, true);
        modalTriggerElement = document.activeElement;

        var modal = document.getElementById('monitoring-modal');
        var modalMapSlot = document.getElementById('monitoring-modal-map-slot');
        var mapWrapper = document.getElementById('monitoring-map-original-slot');
        var loading = document.getElementById('drawer-loading');
        var content = document.getElementById('drawer-content');
        var closeBtn = document.getElementById('monitoring-modal-close');

        document.getElementById('drawer-title').innerText = 'Loading...';
        document.getElementById('drawer-temp').innerText = '--';
        document.getElementById('drawer-condition').innerText = '--';
        document.getElementById('drawer-wind').innerText = '--';
        document.getElementById('drawer-last-sync').textContent = 'Last sync: --';
        var healthEl = document.getElementById('drawer-api-health');
        if (healthEl) { healthEl.classList.add('hidden'); }
        document.getElementById('drawer-alerts-list').innerHTML = '';
        var chartEl = document.getElementById('drawer-trend-chart');
        if (chartEl) chartEl.innerHTML = '';

        if (mapWrapper && modalMapSlot) {
            mapWrapperOriginalParent = mapWrapper.parentNode;
            mapWrapperNextSibling = mapWrapper.nextSibling;
            modalMapSlot.appendChild(mapWrapper);
            if (map && typeof map.invalidateSize === 'function') {
                setTimeout(function () { map.invalidateSize(); }, 50);
            }
        }

        modal.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
        loading.classList.remove('hidden');
        content.classList.add('hidden');
        if (closeBtn) closeBtn.focus();

        fetch('/Client/Risks/GetSiteDetails?id=' + siteId)
            .then(function (res) { return res.json(); })
            .then(function (data) {
                loading.classList.add('hidden');
                content.classList.remove('hidden');
                renderDrawer(data);
            })
            .catch(function (err) {
                console.error(err);
                document.getElementById('drawer-title').innerText = 'Error loading data';
                loading.classList.add('hidden');
            });
    };

    window.closeDrawer = function () {
        var modal = document.getElementById('monitoring-modal');
        var mapWrapper = document.getElementById('monitoring-map-original-slot');
        if (mapWrapper && mapWrapperOriginalParent) {
            mapWrapperOriginalParent.insertBefore(mapWrapper, mapWrapperNextSibling);
            mapWrapperOriginalParent = null;
            mapWrapperNextSibling = null;
            if (map && typeof map.invalidateSize === 'function') {
                setTimeout(function () { map.invalidateSize(); }, 50);
            }
        }
        modal.classList.add('hidden');
        document.body.style.overflow = '';
        activeSiteId = null;
        highlightGeofence(null, false);
        if (modalTriggerElement && typeof modalTriggerElement.focus === 'function') {
            modalTriggerElement.focus();
        }
        modalTriggerElement = null;
    };

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            var modal = document.getElementById('monitoring-modal');
            if (modal && !modal.classList.contains('hidden')) closeDrawer();
        }
    });

    function renderDrawer(data) {
        document.getElementById('drawer-title').innerText = data.name || 'Site';
        document.getElementById('drawer-site-id').value = data.siteId;

        if (data.lastSyncUtc) {
            var d = new Date(data.lastSyncUtc);
            document.getElementById('drawer-last-sync').textContent = 'Last sync: ' + d.toLocaleString();
        } else {
            document.getElementById('drawer-last-sync').textContent = 'Last sync: --';
        }
        var healthEl = document.getElementById('drawer-api-health');
        if (healthEl) {
            healthEl.classList.remove('hidden');
            var dot = healthEl.querySelector('.drawer-health-dot');
            var label = healthEl.querySelector('.drawer-health-label');
            if (dot) dot.className = 'drawer-health-dot w-2 h-2 rounded-full ' + (data.apiHealthOk ? 'bg-emerald-500' : 'bg-amber-500');
            if (label) label.textContent = data.apiHealthOk ? 'API OK' : 'API limited';
        }

        if (data.history && data.history.length > 0) {
            var latest = data.history[data.history.length - 1];
            var temp = latest.tempC != null ? Number(latest.tempC).toFixed(1) : '--';
            var wind = latest.windKmh != null ? Number(latest.windKmh).toFixed(1) : '--';
            document.getElementById('drawer-temp').innerText = temp + '°C';
            document.getElementById('drawer-wind').innerText = wind + ' km/h';
            document.getElementById('drawer-condition').innerText = 'Updated ' + new Date(latest.capturedAtUtc).toLocaleTimeString();
            renderTrendChart(data.history);
        } else {
            document.getElementById('drawer-temp').innerText = '--°C';
            document.getElementById('drawer-wind').innerText = '-- km/h';
            document.getElementById('drawer-condition').innerText = '--';
        }

        drawerAlerts = Array.isArray(data.alerts) ? data.alerts : [];
        drawerAlerts.forEach(function (a) { a.siteId = data.siteId; });
        ensureAlertFiltersInitialized();
        applyAlertFiltersAndRender();
    }

    function renderTrendChart(history) {
        var svg = document.getElementById('drawer-trend-chart');
        if (!svg || !history || history.length < 2) return;
        svg.innerHTML = '';

        var width = 300;
        var height = 60;
        var padding = 5;
        var windThreshold = 40;
        var rainThreshold = 10;

        var windMax = Math.max(windThreshold, Math.max.apply(null, history.map(function (h) { return h.windKmh != null ? Number(h.windKmh) : 0; })));
        var rainMax = Math.max(rainThreshold, Math.max.apply(null, history.map(function (h) { return h.rainMm != null ? Number(h.rainMm) : 0; })));
        if (windMax < 1) windMax = 1;
        if (rainMax < 1) rainMax = 1;

        var step = (width - padding * 2) / (history.length - 1);
        var y0 = height - padding;
        var windPath = 'M ';
        var rainPath = 'M ';
        history.forEach(function (h, i) {
            var x = padding + i * step;
            var wy = y0 - ((h.windKmh != null ? Number(h.windKmh) : 0) / windMax) * (y0 - padding);
            var ry = y0 - ((h.rainMm != null ? Number(h.rainMm) : 0) / rainMax) * (y0 - padding);
            windPath += x + ' ' + wy + (i < history.length - 1 ? ' L ' : '');
            rainPath += x + ' ' + ry + (i < history.length - 1 ? ' L ' : '');
        });

        var windLine = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        windLine.setAttribute('d', windPath);
        windLine.setAttribute('stroke', '#6366f1');
        windLine.setAttribute('stroke-width', '2');
        windLine.setAttribute('fill', 'none');
        svg.appendChild(windLine);

        var rainLine = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        rainLine.setAttribute('d', rainPath);
        rainLine.setAttribute('stroke', '#0ea5e9');
        rainLine.setAttribute('stroke-width', '2');
        rainLine.setAttribute('fill', 'none');
        rainLine.setAttribute('stroke-dasharray', '4,2');
        svg.appendChild(rainLine);

        var threshY = y0 - (windThreshold / windMax) * (y0 - padding);
        var thresh = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        thresh.setAttribute('x1', padding);
        thresh.setAttribute('y1', threshY);
        thresh.setAttribute('x2', width - padding);
        thresh.setAttribute('y2', threshY);
        thresh.setAttribute('stroke', '#94a3b8');
        thresh.setAttribute('stroke-width', '1');
        thresh.setAttribute('stroke-dasharray', '2,2');
        svg.appendChild(thresh);
    }

    // Site selector: pan map without full reload
    var siteSelect = document.getElementById('monitoring-site-select');
    var siteForm = document.getElementById('site-form');
    if (siteSelect && map && siteForm) {
        siteSelect.addEventListener('change', function () {
            var sites = getSitesFromData();
            var val = parseInt(this.value, 10);
            var site = sites.find(function (s) { return s.siteId === val; });
            if (site && map) {
                map.setView([site.latitude, site.longitude], 14);
                var url = '/Client/Risks/Monitoring?siteId=' + val;
                if (typeof history !== 'undefined' && history.replaceState) history.replaceState(null, '', url);
            }
        });
    }

    // Layer toggles
    var layerSitesCb = document.getElementById('layer-sites');
    var layerGeofenceCb = document.getElementById('layer-geofence');
    var layerRisksCb = document.getElementById('layer-risks');
    if (layerSitesCb) {
        layerSitesCb.addEventListener('change', function () { layerSites = this.checked; applyLayerVisibility(); });
    }
    if (layerGeofenceCb) {
        layerGeofenceCb.addEventListener('change', function () { layerGeofence = this.checked; applyLayerVisibility(); });
    }
    if (layerRisksCb) {
        layerRisksCb.addEventListener('change', function () { layerRisksOnly = this.checked; applyLayerVisibility(); });
    }

    // Auto-sync
    var AUTO_SYNC_KEY = 'monitoringAutoSync';
    var INTERVAL_MS = 10 * 60 * 1000;
    var form = document.getElementById('monitoring-sync-form');
    var btnOff = document.getElementById('auto-sync-off');
    var btnOn = document.getElementById('auto-sync-on');
    var statusEl = document.getElementById('auto-sync-status');
    var countdownEl = document.getElementById('auto-sync-countdown');
    var autoSyncTimer = null;
    var countdownTimer = null;
    var nextSyncAt = null;

    function runAutoSync() {
        var syncForm = document.getElementById('drawer-sync-form') || document.getElementById('monitoring-sync-form');
        if (!syncForm) return;
        fetchMapData();
        nextSyncAt = Date.now() + INTERVAL_MS;
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
        if (localStorage.getItem(AUTO_SYNC_KEY) === 'true') startAutoSync();
    } catch (e) {}
})();
