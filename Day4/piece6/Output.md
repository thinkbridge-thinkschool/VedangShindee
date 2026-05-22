# Piece 6 — Azure App Insights via OpenTelemetry

## App Insights Connection Setup



### Program.cs — Key Vault overlay (connection string never hardcoded)

```csharp
var builder = WebApplication.CreateBuilder(args);

// In non-Development environments, overlay Key Vault secrets onto configuration.
// The App Service managed identity must have "Key Vault Secrets User" on the vault.
// Secret naming convention: ApplicationInsights--ConnectionString → ApplicationInsights:ConnectionString
var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
```

The connection string lives in Key Vault as a secret named **`ApplicationInsights--ConnectionString`**.
Azure maps `--` → `:` when projecting secrets into `IConfiguration`, so the app reads it as
`ApplicationInsights:ConnectionString` — the same key whether the value came from Key Vault or a local
environment variable during development.


### . appsettings.json — placeholder keys

```json
"ApplicationInsights": {
  "ConnectionString": "InstrumentationKey=578ff516-9458-4852-a85f-59f44628c965;IngestionEndpoint=https://southeastasia-1.in.applicationinsights.azure.com/;LiveEndpoint=https://southeastasia.livediagnostics.monitor.azure.com/;ApplicationId=7767bee3-e43f-4ed1-b5bf-58d0c452957d"
},
"Azure": {
  "KeyVaultUri": "https://quotes-api-keyvault2.vault.azure.net/
  "
}
```


---

## KQL — 10 slowest requests in the last hour

requests
| where timestamp > ago(1h)
| project
    timestamp,
    name,
    url,
    duration,
    resultCode,
    success,
    operation_Id
| top 10 by duration desc

Run this in **App Insights → Logs** (or the portal's **Investigate → Transaction search**).
`operation_Id` correlates to the OpenTelemetry `TraceId`, so you can pivot straight into the
end-to-end trace for any slow request.

---

## Alert: POST /api/quotes avg response time > 500 ms over 5 minutes

In Azure Portal → App Insights → Alerts → Create → Metric alert:

| Field | Value |
|---|---|
| Signal | `requests/duration` |
| Filter | `request/name = POST /api/quotes` |
| Aggregation | Average |
| Threshold | Static — Greater than **500 ms** |
| Evaluation period | 5 minutes |
| Frequency | 1 minute |
| Action group | Email: `verekhs@gmail.com` |

Alert fires only when the average over the 5-minute window exceeds the threshold — a single slow
outlier will not page; a sustained degradation will.

---

## Reflection

### What did you learn this session?

I learned that OpenTelemetry is not tied to a single monitoring tool. It collects telemetry data and then sends it wherever you configure, such as Jaeger during development or Application Insights in production. The application code stays the same; only the exporter configuration changes.

I also understood how Key Vault fits into the configuration system. By adding Key Vault early in startup, the application can read secrets from the vault just like any other configuration value. This keeps sensitive information out of source control, reduces the risk of accidentally exposing secrets, and allows secret values to be updated without changing the application code.

### What would break this?
If the application's managed identity does not have permission to read secrets from Key Vault, the app will fail during startup and won't run. Since Application Insights is not configured yet at that stage, the error won't appear there and can only be found in the App Service logs.

Another problem is when the Azure:KeyVaultUri setting is missing in production. The app simply skips loading secrets from Key Vault without showing an error. As a result, Application Insights is never configured, and the application runs without sending any logs, traces, or monitoring data, making it difficult to detect issues later.


## Screenshots -

![alt text](<Screenshot (235).png>) ![alt text](<Screenshot (234).png>)