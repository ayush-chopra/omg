# Windows console deployment (.NET Framework 4.8)

## Recommended topology

```text
Cloud9 public HTTPS callback
          ↓
IIS HTTPS reverse proxy
          ↓
http://localhost:8090/api/cloud9/ship-job-callback
          ↓
Cloud9.Omg.Connector console process
          ├── 15-minute OMG polling timer
          └── LiteDB state/audit database
```

The console process is self-hosted with OWIN Web API 2 and also contains the scheduled worker. This avoids relying on an IIS application pool for the 15-minute job. IIS is used only for TLS termination and controlled public access.

## Prerequisites

- Supported Windows Server release
- .NET Framework 4.8 runtime
- Visual Studio 2022 Build Tools plus .NET Framework 4.8 Developer Pack for building
- IIS with URL Rewrite and Application Request Routing if IIS will reverse-proxy the callback
- Dedicated least-privilege Windows account for running the console process

## Installation outline

1. Build `Release` on Windows.
2. Copy the full output directory to a versioned deployment directory.
3. Configure secrets as environment variables for the runtime account, or protect the executable configuration file with appropriate ACLs.
4. Give the runtime account modify permission only on the configured database/log directory.
5. Reserve the local listener URL for the runtime account if Windows requires it:

   ```powershell
   netsh http add urlacl url=http://localhost:8090/ user="DOMAIN\ConnectorUser"
   ```

6. Start the application from a terminal in its deployment directory:

   ```powershell
   cd C:\Apps\Cloud9OmgConnector
   .\Cloud9.Omg.Connector.exe
   ```

7. Keep the console process running and confirm `GET http://localhost:8090/health` locally.
8. Configure the IIS reverse proxy and public TLS certificate.
9. Register the public HTTPS callback URL with Cloud9.

## Verification

Run tests on the Windows build agent:

```powershell
dotnet test Cloud9.Omg.Connector.sln --configuration Release
```

Before production, verify:

- A newly created Company Store order appears once in Cloud9.
- Updating an OMG order updates the existing Cloud9 ship job rather than creating a duplicate.
- Shipping in Cloud9 writes one tracking number to OMG and records cost internally.
- Replaying the same callback does not create another OMG shipment.
- Cloud9 receives a successful response for duplicates.
- Multi-package, void, return, and international cases enter manual review.
- Credentials never appear in logs.
- Restarting the console application preserves the cursor and idempotency state.

## Rollback

Press `Ctrl+C` to stop the application, restore the previous versioned deployment directory and its matching executable configuration, then launch it again. Preserve the LiteDB database so callback and order idempotency history is not lost.
