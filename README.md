# Cloud9 Express ↔ OMG Company Store Connector

Standalone C# connector targeting **.NET Framework 4.8**. It is separate from the WooCommerce project.

## Current scope

The connector implements the confirmed Company Store workflow:

1. Every 15 minutes, request OMG orders updated since the last successful cursor.
2. Fetch each complete order and map it to Cloud9 `Data/AddShipJob`.
3. Let fulfillment staff rate and ship the staged order in Cloud9.
4. Receive Cloud9's JSON callback containing the tracking number and cost.
5. Save callback cost and audit data in a local embedded LiteDB database.
6. Create an OMG order shipment containing the tracking number and shipped line items.

The default configuration is disabled. It will not contact OMG or Cloud9 until credentials are supplied and `Enabled` is set to `true`.

## Supported storefront

Only **OMG Company Stores** (the Bright Sites platform) are supported. The implementation uses:

- `GET /api/v2.7.0/orders`
- `GET /api/v2.7.0/orders/{order_id}`
- `GET /api/v2.7.0/orders/{order_id}/shipments`
- `POST /api/v2.7.0/orders/{order_id}/shipments`

Do not enable this connector for Pop-up Stores or OMG Websites. Those are different products and do not expose the same confirmed shipment-writeback contract.

## Important limitations

- The OMG create-shipment API has no documented writable cost field. Cloud9 cost is retained in the internal shipment-event record; only tracking and shipment details are sent to OMG.
- OMG order data does not represent the final packed carton. Default package weight and dimensions are used to stage the Cloud9 job. Fulfillment must verify the package before rating/shipping.
- International orders are rejected until customs mapping is approved.
- Returns, voids, and multi-package callbacks are marked `manual_review`. Automatically assigning all line-item quantities to multiple tracking numbers could over-ship the order.
- This is an implementation baseline, not a guarantee of production compatibility. OMG and Cloud9 must supply credentials, approve the workflow, and participate in sandbox/UAT testing.

## Solution structure

```text
Cloud9.Omg.Connector.sln
├── src/Cloud9.Omg.Connector
│   ├── Clients          OMG and Cloud9 HTTP clients
│   ├── Configuration    .NET Framework app settings
│   ├── Hosting          Windows Service and 15-minute timer
│   ├── Models           API JSON contracts
│   ├── Persistence      LiteDB cursor, audit and idempotency state
│   ├── Services         order mapping, synchronization and callback logic
│   └── Web              OWIN Web API 2 callback endpoint
└── tests/Cloud9.Omg.Connector.Tests
```

## Build

Use a Windows machine with Visual Studio 2022 Build Tools and the .NET Framework 4.8 Developer Pack:

```powershell
msbuild Cloud9.Omg.Connector.sln /restore /p:Configuration=Release
```

The code also compiles from the .NET SDK when the reference-assemblies package is restored. The production Windows Service deployment requires Windows, while development builds and tests can run through Mono.

For development verification on macOS, Mono can execute the Framework-targeted binary and xUnit console runner:

```bash
dotnet build Cloud9.Omg.Connector.sln --configuration Release
mono ~/.nuget/packages/xunit.runner.console/2.9.3/tools/net48/xunit.console.exe \
  tests/Cloud9.Omg.Connector.Tests/bin/Release/net48/Cloud9.Omg.Connector.Tests.dll -noshadow
mono src/Cloud9.Omg.Connector/bin/Release/net48/Cloud9.Omg.Connector.exe --console
```

Production remains Windows-only, but the connector, HTTP host, scheduler, and pure-managed LiteDB store are testable on macOS through Mono.

## Configuration

Copy deployment values into `Cloud9.Omg.Connector.exe.config`, or preferably supply environment variables using `C9OMG_` plus the app-setting name. Examples:

```text
C9OMG_Enabled=true
C9OMG_OmgBaseUrl=https://client-store.mybrightsites.com
C9OMG_OmgApplicationToken=...
C9OMG_Cloud9UserId=...
C9OMG_Cloud9Password=...
C9OMG_Cloud9LocationId=100
C9OMG_Cloud9ShopCode=...
C9OMG_Cloud9CallbackToken=<at-least-32-random-characters>
```

Set either `Cloud9ShopCode`, or both `Cloud9CarrierScac` and `Cloud9CarrierServiceCode`. The complete setting list and safe defaults are in [App.config](src/Cloud9.Omg.Connector/App.config).

## Run modes

Interactive development host:

```powershell
Cloud9.Omg.Connector.exe --console
```

Run one polling cycle without starting the callback listener:

```powershell
Cloud9.Omg.Connector.exe --run-once
```

Health endpoint:

```text
GET http://localhost:8090/health
```

Cloud9 callback endpoint:

```text
POST /api/cloud9/ship-job-callback
```

The preferred authentication is either `Authorization: Bearer <token>` or `X-Cloud9-Callback-Token: <token>`. A `?token=` fallback exists only if Cloud9 cannot send a custom header; query-string secrets are more likely to appear in proxy logs.

## Before enabling

- Obtain a Company Store hostname and application token from OMG.
- Obtain the Cloud9 API URL, user, password, location ID, and shop code/service mapping.
- Ask Cloud9 to confirm callback authentication and register the public HTTPS callback URL.
- Put IIS or another TLS reverse proxy in front of the local OWIN listener.
- Confirm the service account can write the configured embedded-database directory.
- Agree on package defaults and the manual handling process.
- Test new orders, updated orders, duplicate callbacks, one-package shipments, multiple packages, voids, returns, and API outages in non-production environments.

See [Windows deployment](docs/WINDOWS-DEPLOYMENT.md) and [API decisions](docs/API-DECISIONS.md) for the production handoff.
