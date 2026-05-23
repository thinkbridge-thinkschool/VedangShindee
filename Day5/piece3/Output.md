# Piece 3 – Azure Container Apps Fundamentals

## az commands

### 1. Create the resource group


az group create -n thinkschool-rg -l centralindia


### 2. Create the Container Apps environment


az containerapp env create -n thinkschool-env -g thinkschool-rg -l centralindia


### 3. Show the environment (JSON)


az containerapp env show -n thinkschool-env -g thinkschool-rg


Output:

```json
{
  "id": "/subscriptions/6b3f49de-c9ab-436d-b896-27ebc13a1e3a/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env",
  "location": "Central India",
  "name": "thinkschool-env",
  "properties": {
    "appInsightsConfiguration": null,
    "appLogsConfiguration": {
      "destination": "log-analytics",
      "logAnalyticsConfiguration": {
        "customerId": "90de10dd-9271-4c82-be10-d794ce6eb58a",
        "sharedKey": null
      }
    },
    "customDomainConfiguration": {
      "certificateKeyVaultProperties": null,
      "certificatePassword": null,
      "certificateValue": null,
      "customDomainVerificationId": "48A633C3D89FAA11C1889285C26CDC5207E2A8FAD9167F65B27FD15505C56DCE",
      "dnsSuffix": null,
      "expirationDate": null,
      "subjectName": null,
      "thumbprint": null
    },
    "daprAIConnectionString": null,
    "daprAIInstrumentationKey": null,
    "daprConfiguration": {
      "version": "1.16.4-msft.6"
    },
    "defaultDomain": "greenbay-3b4cda8d.centralindia.azurecontainerapps.io",
    "eventStreamEndpoint": "https://centralindia.azurecontainerapps.dev/subscriptions/6b3f49de-c9ab-436d-b896-27ebc13a1e3a/resourceGroups/thinkschool-rg/managedEnvironments/thinkschool-env/eventstream",
    "infrastructureResourceGroup": null,
    "ingressConfiguration": null,
    "kedaConfiguration": {
      "version": "2.18.1"
    },
    "openTelemetryConfiguration": null,
    "peerAuthentication": {
      "mtls": {
        "enabled": false
      }
    },
    "peerTrafficConfiguration": {
      "encryption": {
        "enabled": false
      }
    },
    "provisioningState": "Succeeded",
    "publicNetworkAccess": "Enabled",
    "staticIp": "4.224.99.71",
    "vnetConfiguration": null,
    "workloadProfiles": [
      {
        "enableFips": false,
        "name": "Consumption",
        "workloadProfileType": "Consumption"
      }
    ],
    "zoneRedundant": false
  },
  "resourceGroup": "thinkschool-rg",
  "systemData": {
    "createdAt": "2026-05-23T08:52:23.9472441",
    "createdBy": "E2K22169@ms.pict.edu",
    "createdByType": "User",
    "lastModifiedAt": "2026-05-23T08:52:23.9472441",
    "lastModifiedBy": "E2K22169@ms.pict.edu",
    "lastModifiedByType": "User"
  },
  "type": "Microsoft.App/managedEnvironments"
}
```

---

## What I learned this session

The main thing I learned is that a Container Apps Environment acts like a shared space for multiple apps. Apps in the same environment can talk to each other easily and use the same networking and monitoring setup.

I also learned that Azure uses revisions. When a new version is deployed, Azure creates a new revision instead of replacing the old one. This makes it easy to switch back to a previous version if something goes wrong.

---

## What would break this

One thing I learned is that small configuration mistakes can cause deployment problems. If the target port does not match the port used by the application, the app will fail health checks and not start correctly. If the container image is private and registry credentials are missing, Azure cannot pull the image. Enabling scale-to-zero can make the first request slow because a new container must start. Storing data inside the container is risky because the data is lost whenever the container restarts. Forgetting to enable external ingress means the app will not get a public URL. Also, placing dependent services in different Azure regions can increase request latency and slow down the application.