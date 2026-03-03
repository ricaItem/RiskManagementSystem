# Risk Monitoring Hub – Architecture & API

## 1. Data models and flow

### Entities (existing)
- **MonitoringSite** – OrgId, SiteId (FK to Sites), Name, Latitude, Longitude. No geofence radius in DB; radius is UI-only (500 m in `monitoring.js`).
- **MonitoringSnapshot** – Time series per site: CapturedAtUtc, Temperature, WindSpeed, Humidity, RainMm, Condition, RawJson. Ingested from weather API by site lat/lng.
- **MonitoringRule** – Org-scoped: Metric, Threshold, Operator, Severity, CooldownMinutes, Enabled. Evaluated against snapshot values.
- **MonitoringAlert** – Site + Rule (or rule code): RuleId/RuleCode, RuleName, MeasuredValues, Severity, Status (Active/Resolved), TriggeredAt, ResolvedAtUtc, AcknowledgedAtUtc, AcknowledgedByUserId, **RiskId** (link to risk registry).

*WeatherEvent*: Not a separate entity. “Event” is represented by MonitoringSnapshot (time series) + MonitoringAlert when a rule fires. No new table added.

### Flow
1. **Ingest** – `MonitoringSyncHostedService` (config: `Monitoring:SyncIntervalMinutes`, `Monitoring:SyncOrgIds`) or manual **Sync** calls `MonitoringHubService.RunSyncForSiteAsync(orgId, siteId, userId)`.
2. **Weather** – `IOpenWeatherService.GetWeatherAsync(lat, lng)` → WeatherSnapshot (in-memory DTO) → persisted as **MonitoringSnapshot**.
3. **Rules** – DB rules (MonitoringRules) + static `MonitoringRuleEngine.Evaluate(WeatherSnapshot)` (thunderstorm, wind, rain, heat index). Each triggered rule produces a result; dedupe by (siteId, ruleId/ruleCode).
4. **Dedupe** – One active alert per (MonitoringSiteId, RuleId or RuleName). If same rule still triggered, existing alert updated (MeasuredValues, TriggeredAt). Cooldown: after resolve, same rule can re-fire only after CooldownMinutes.
5. **Resolve** – In same sync cycle, alerts whose rule no longer triggers are set Status=Resolved, ResolvedAtUtc=now.
6. **Risk link** – High/Critical alerts: `GetExistingOpenRiskIdForSiteRuleAsync` (dedupe window AutoRiskDedupeHours=12). If found, alert.RiskId linked and risk updated; else `CreateRiskFromMonitoringAsync` and link. “Open Risk” in UI uses existing RiskId or `CreateRiskFromAlertAndLinkAsync`.
7. **Audit** – AddAuditLog(entityType, entityId, actionType, message) for BackgroundSync, AutoRiskCreated, AcknowledgeAlert, ResolveAlert (and rule edits if implemented).

---

## 2. API endpoints and shapes

### GET /Client/Risks/GetMapData
- **Auth**: Required (org from user).
- **Response**: `200` – JSON array of **MonitoringMapItem**:
  - `siteId`, `name`, `latitude`, `longitude`, `activeAlertCount`, `maxSeverity` ("None"|"Medium"|"High"|"Critical"), `tempC`, `condition`, `lastSyncUtc`.

### GET /Client/Risks/GetSiteDetails?id={siteId}
- **Response**: `200` – **MonitoringSiteDetailsDto**:
  - `siteId`, `name`, `alerts` (array of MonitoringAlertViewModel), `history` (array of MonitoringSnapshotDto: `capturedAtUtc`, `tempC`, `windKmh`, `rainMm`).
- **Extended (this upgrade)**: Include `lastSyncUtc`, `apiHealthOk` (from latest snapshot or sync state) so drawer can show “Last sync” and health.

### POST /Client/Risks/MonitoringSync
- **Body**: siteId (int), __RequestVerificationToken.
- **Response**: Redirect to Monitoring with TempData LastSyncUtc, ApiHealthOk.

### POST /Client/Risks/AcknowledgeAlert
- **Body**: alertId, siteId (optional), __RequestVerificationToken.
- **Response**: Redirect back; audit log “AlertAcknowledged”.

### POST /Client/Risks/CreateMitigationPlanFromAlert
- **Body**: alertId, __RequestVerificationToken. Creates/links risk then redirects to Mitigation Board.

### POST /Client/Risks/ResolveAlert
- **Body**: alertId, siteId (optional), __RequestVerificationToken.
- **Response**: Redirect to Monitoring (or 404); sets Status=Resolved, ResolvedAtUtc; audit “AlertResolved”.

---

## 3. Code changes (paths and rationale)

| Path | Change |
|------|--------|
| **docs/RISK_MONITORING_HUB_ARCHITECTURE.md** | New: architecture note + API list (this file). |
| **Services/MonitoringHubService.cs** | GetSiteDetailsAsync: return lastSyncUtc + apiHealthOk. AcknowledgeAlertAsync: call AddAuditLog(…, "AlertAcknowledged"). Add ResolveAlertAsync + AddAuditLog(…, "AlertResolved"). |
| **Areas/Client/Controllers/RisksController.cs** | Add ResolveAlert POST action. GetSiteDetails: ensure response includes lastSyncUtc/apiHealthOk (via DTO). |
| **wwwroot/js/monitoring.js** | Site selector: on change pan/zoom map to site (no full reload if map already loaded). Layer toggles: Sites / Alerts / Risks (show-hide marker layers). Geofence: hover/selected class (soft glow). Drawer: open from marker or geofence click; selecting alert in list highlights map marker + brief pulse. Respect prefers-reduced-motion (skip pulse/long transitions). |
| **Areas/Client/Views/Risks/Monitoring.cshtml** | Pass selectedSiteId and sites list to JS for initial pan; layer toggles UI; drawer alert list with data-alert-id for highlight; optional loading skeletons. |
| **Areas/Client/Views/Shared/_LayoutClient.cshtml** or **Monitoring.cshtml** | Global or page-level `@media (prefers-reduced-motion: reduce)` for animation duration/transition overrides. |

No new NuGet packages. Leaflet already present; no Mapbox/Google. Weather: OpenWeather via existing IOpenWeatherService. Polling: client setInterval (monitoring.js) + server MonitoringSyncHostedService.

---

## 4. Tests

- **Dedupe**: One active alert per (siteId, ruleId/ruleCode); cooldown after resolve.
- **Rule evaluation**: EvalRule (>, >=, <, <=, =) and MonitoringRuleEngine.Evaluate (thunderstorm, wind, rain, heat) with given WeatherSnapshot.

Tests added under **WEB_Sentro.Tests** (or project’s test assembly): e.g. `MonitoringRuleEngineTests.cs`, `MonitoringHubDedupeTests.cs` (if test DB available) or unit tests for rule engine only.
