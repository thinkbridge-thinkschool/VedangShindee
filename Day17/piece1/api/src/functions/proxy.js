'use strict';

const { app } = require('@azure/functions');
const { DefaultAzureCredential } = require('@azure/identity');

const BACKEND_URL =
  process.env['BACKEND_API_URL'] ||
  'https://quotes-api.happyflower-7fa5126b.centralindia.azurecontainerapps.io';

// The Azure AD ClientId of the Container Apps API App Registration (QuotesApi).
// Used as the token audience — no secret, just the public identifier.
const BACKEND_CLIENT_ID =
  process.env['BACKEND_CLIENT_ID'] || 'bcc023d6-5651-4caa-b2c4-1a390427a3c5';

// Singleton credential — reuses cached token across invocations.
// DefaultAzureCredential picks up the SWA Managed Identity automatically in Azure.
const credential = new DefaultAzureCredential();

app.http('proxy', {
  methods: ['GET', 'POST', 'DELETE', 'PUT', 'PATCH'],
  authLevel: 'anonymous',
  route: '{*restOfPath}',
  handler: async (request, context) => {
    const restOfPath = request.params['restOfPath'] ?? '';
    const urlObj = new URL(request.url);
    const targetUrl = `${BACKEND_URL}/api/${restOfPath}${urlObj.search}`;

    context.log(`Proxying ${request.method} → ${targetUrl}`);

    // Acquire Managed Identity token at runtime.
    // No secret is stored anywhere — DefaultAzureCredential uses the SWA system-assigned MI.
    const tokenResponse = await credential.getToken(`${BACKEND_CLIENT_ID}/.default`);

    const forwardHeaders = {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${tokenResponse.token}`,
    };

    const body = ['GET', 'HEAD', 'DELETE'].includes(request.method.toUpperCase())
      ? undefined
      : await request.text();

    const backendResponse = await fetch(targetUrl, {
      method: request.method,
      headers: forwardHeaders,
      body,
    });

    const responseBody = await backendResponse.text();

    return {
      status: backendResponse.status,
      headers: {
        'Content-Type': backendResponse.headers.get('Content-Type') ?? 'application/json',
      },
      body: responseBody,
    };
  },
});
