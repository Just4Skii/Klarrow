# Klarrow frontend

React + TypeScript + Vite operations UI for the Klarrow evidence-reconciliation prototype.

## Development

From this directory:

```powershell
npm ci
npm run dev
```

The Vite server defaults to `http://localhost:5173`. The API is a separate .NET project; from the repository root, start it with:

```powershell
dotnet run --project src/RecallGraph.Api --urls http://127.0.0.1:5099
```

Alternatively, run `start-klarrow.cmd` from the repository root on Windows.

## Checks

```powershell
npm run lint
npm run build
npx playwright test
```

Playwright tests launch Vite and use Microsoft Edge in headless mode, so Edge must be installed in the test environment. The end-to-end tests also expect the API and synthetic demo workspace to be available as described in the repository root README.

The default workspace contains clearly labelled synthetic demonstration records. Do not import real or confidential supplier records into a shared or exposed demo instance.
