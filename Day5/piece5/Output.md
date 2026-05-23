# Day 5 – Piece 5: Verify in App Insights with your first KQL

## App Insights Resource

- **Name:** quotes-api-insights-2
- **Resource Group:** quotes-api2
- **Location:** Southeast Asia
- **Subscription:** Azure for Students (`6b3f49de-c9ab-436d-b896-27ebc13a1e3a`)
- **ApplicationId:** `7767bee3-e43f-4ed1-b5bf-58d0c452957d`

---

## Endpoints Hit (to generate telemetry)

The following requests were sent to the locally running app (port 5051), which is configured to forward OTel telemetry to `quotes-api-insights-2` via `ApplicationInsights:ConnectionString`:

| Endpoint | # Hits | Notes |
|---|---|---|
| `GET /health` | 5 | Basic liveness check |
| `GET /api/quotes?page=...&size=...` | 3 | Paged list (3 different page/size combos) |
| `GET /api/quotes/{id}` | 15 | IDs 1–5 (5x), IDs 1–5 random (10x more) |
| `GET /api/quotes/999` | 1 | Intentional 404 |
| `POST /api/auth/login` | 1 (success) + 3 (bad creds) | Generates 200 + 401 events |
| `POST /api/quotes` | 3 | Authenticated, uses custom OTel span |
| `POST /api/auth/refresh` | 1 | Token rotation |

---

## KQL Query

```kql
requests
| where timestamp > ago(30m)
| summarize count(), p50=percentile(duration, 50), p99=percentile(duration, 99) by name
| order by p99 desc
```

---

## KQL Result (queried programmatically via Azure Monitor Query SDK)

```
name                                                      count_     p50 (ms)     p99 (ms)
-------------------------------------------------------------------------------------------
GET /health                                                    6         1.31      1701.20
POST /api/auth/login                                           4       374.89      1235.79
GET /api/quotes/                                               3       158.11       437.99
POST /api/quotes/                                              9         5.87       187.85
GET /api/quotes/{id:int}                                      10        47.28        70.90
POST /api/auth/refresh                                         3         2.44        35.73
```

---

## Observation – Surprising Endpoint
The interesting thing was that the /health endpoint appeared to be the slowest, even though it only returns a simple response and doesn't access the database. This happened because the first request after the application starts has extra work to do, such as loading the ASP.NET Core pipeline, initializing OpenTelemetry, and creating the first connection to Application Insights. That startup work made the first health check much slower than normal. After the application was warmed up, the endpoint responded in just a few milliseconds. A good way to avoid this in production is to keep at least one instance running or use startup probes so users don't experience the initial delay.
---

## Saved Function

The KQL was saved as a reusable function named **`endpoint_perf_summary`** in the Log Analytics workspace `DefaultWorkspace-6b3f49de-c9ab-436d-b896-27ebc13a1e3a-SEA`.

To call it from App Insights Logs:
```kql
endpoint_perf_summary
```
Screenshot of KQL Query -
![alt text](<Screenshot (247).png>) ![alt text](<Screenshot (248).png>) 

### What I Learnt -
The main thing that clicked for me was that OpenTelemetry is not doing anything magical behind the scenes. Once the Application Insights connection string is configured, traces are automatically sent to Azure and can be viewed using KQL queries. I also learned that ASP.NET Core instrumentation automatically tracks requests without needing extra logging code. Looking at the metrics made the concept clearer—the /health endpoint had a very high p99 response time because of a single slow startup request, while most requests completed in about 1 ms after the application warmed up.



### What could go wrong - 
A few things can make the monitoring data misleading. If Container Apps scales to zero, the first user request can be slow because the application has to start again. Storing the Application Insights connection string in source control is also risky because others could send fake telemetry to the resource. Incorrect system time on a container can cause telemetry to appear outside the selected query range, making data seem missing. Finally, percentile metrics such as p99 are not very reliable when only a few requests have been recorded, so more traffic is needed before drawing conclusions from them.