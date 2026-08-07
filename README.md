# FinFlow

Self-hosted web app that parses German bank CSV exports, classifies transactions for your Steuererklärung (tax return) and household budget, and exports a color-coded Excel workbook.

Supported banks: **DKB** (checking + credit card), **ING**, **HVB** (checking + credit card), **Trade Republic**, **Postbank**, **Berliner Volksbank**.

> **Built with AI assistance.** This project was developed with the help of Claude Code. Review the classification results yourself (especially anything marked "needs review" or "ignored") before relying on them for anything official — see [Legal notes](#legal-notes).

---

## Features

**Web app** (Angular + ASP.NET Core + SQLite — single-tenant, no accounts; run one instance per household):

- CSV import wizard: drop multiple files, each file's bank is auto-detected from its header, override per file where needed; duplicate bookings are skipped automatically on re-import, and every import batch can be rolled back.
- Filterable, paged transaction list with inline category assignment (manual choices survive re-classification), a "needs review" queue, and ignore/delete actions.
- Classification rules (regex → category → status) managed in the UI, with a live pattern test that shows matching transactions as you type, and one-click re-classification.
- Dashboard: income/expenses/net summary, monthly trend, per-category breakdown, and a multi-month forecast driven by your contracts.
- Contracts: track recurring payments (rent, insurance, salary, subscriptions) by period and due date. Expected amounts are a rolling average of recent matched payments rather than a fixed value, so fluctuating income (bonuses, overtime) doesn't trigger false deviations. Missing or off-amount payments are surfaced for review, not silently ignored. Create one from scratch or from an existing transaction with one click.
- Accounts: register your own bank accounts (by IBAN) so money moved between them — even across different banks — is recognized as an internal transfer and excluded from income/expense statistics, instead of inflating both sides as fake spending and earning.
- Notes: a space for free-form personal notes — financial strategy write-ups, or documenting how you've set up your own categories/rules — stored as plain `.md` files in a `notes/` folder next to the database, not locked into SQLite. Edit/Preview toggle renders Markdown, including `mermaid` fenced code blocks as actual diagrams (flowcharts, sequence diagrams, etc.).
- XLSX/CSV export honoring the current filter.

**Parsing engine:**

- Auto-detects encoding (UTF-8, UTF-16, Windows-1252 fallback), delimiter (`;`, `,`, tab, `|`), header row position, decimal format (`1.234,56` and `1,234.56`), and date format — no manual configuration per file.

---

## Getting started (web app)

FinFlow runs as a single app that serves both the UI and the API — no Docker required, though it's supported for self-hosting (e.g. on a NAS), see [Docker](#docker) below.

**Easiest: use the launcher.** Download just one file and run it — no Git, PowerShell, or command line needed:

- Windows: [`finflow.bat`](scripts/finflow.bat) — double-click it. (Or [`finflow.ps1`](scripts/finflow.ps1) if you prefer running it yourself; `finflow.bat` just wraps it so double-clicking works without a "Run with PowerShell" step.)
- Linux/macOS: [`finflow.sh`](scripts/finflow.sh) — run it from a terminal.

It checks GitHub for the latest release, downloads and SHA256-verifies it into a local `app` folder next to itself (only on first run or when a newer version is out), then starts FinFlow and opens **http://localhost:5199**. Run it again any time: already up to date → it just starts the app; already running → it just opens the browser. No .NET, Node, or Docker required — the binaries are self-contained.

<details>
<summary>Manual install (no launcher)</summary>

Grab the zip/tar.gz for your platform from the [Releases page](https://github.com/tosc2571/FinFlow/releases), unpack it, run `FinFlow.Api.exe` (Windows) or `./FinFlow.Api` (Linux), and open http://localhost:5199. Updating means repeating these steps with a newer release — the launcher does this part for you automatically.

</details>

**Or build from source** (requires [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Node.js 24+](https://nodejs.org) — running the built app afterwards needs neither Node nor Docker, only the .NET runtime):

```bash
git clone https://github.com/tosc2571/FinFlow.git
cd FinFlow

# Windows
scripts/publish.ps1

# Linux/macOS
scripts/publish.sh
```

Then start `publish/FinFlow.Api.exe` (Windows) or `dotnet publish/FinFlow.Api.dll` and open **http://localhost:5199** — that's the whole installation.

Your data lives in a single SQLite file in a stable, per-user data directory — `%APPDATA%\FinFlow\finflow.db` on Windows, `~/.local/share/finflow/finflow.db` on Linux — independent of where the app itself is installed. That means **downloading a newer release and running it from a different folder keeps your existing data**; the app also logs the exact path it's using on startup. A dated backup (`backups/finflow-<date>.db`, next to the database) is taken automatically — at most once a day, and only if the database actually changed since the last one — so a bad update or accidental change is always recoverable. This is checked at startup and periodically while the app keeps running (so a long-running instance, e.g. in Docker, doesn't need to be restarted to get a new day's backup), and only the 10 most recent backups are kept; older ones are pruned automatically.

**Typical first session:** Import → drop your bank CSVs (or try `samples/*.csv`) → Categories → create your categories → Rules → add regex rules (the live test shows what they'd match) and re-run classification → Transactions → categorize the rest via the "needs review" filter, and create contracts for recurring payments → Dashboard/Export.

### Development mode

For working on the code there is a dev setup with live reload (API on :5199 with Swagger under `/swagger`, Angular dev server on :4200 proxying `/api`):

```bash
scripts/dev.ps1      # Windows — or scripts/dev.sh on Linux/macOS
```

Or manually: `dotnet run --project backend/FinFlow.Api` in one terminal, `cd frontend && npm install && npm start` in another, then open http://localhost:4200.

### Docker

An alternative to the launcher/manual-install paths above — useful for self-hosting on a NAS or any machine you'd rather not install .NET/Node on directly. No repo clone or local build needed — a ready-built image is published to GHCR on every release:

```bash
curl -O https://raw.githubusercontent.com/tosc2571/FinFlow/main/docker-compose.yml
curl -O https://raw.githubusercontent.com/tosc2571/FinFlow/main/.env.example
cp .env.example .env    # edit FINFLOW_PORT / FINFLOW_DATA_LOCATION / FINFLOW_VERSION if needed

docker compose pull
docker compose up -d
```

Open **http://localhost:5199**. The database (plus its daily backup and `settings.json`) lives in a plain host folder — `./data` next to `docker-compose.yml` by default, or wherever `FINFLOW_DATA_LOCATION` in `.env` points — so it's easy to find, back up, or point your NAS's own backup tooling at, and it survives `docker compose down`/image updates regardless.

Updating means pulling the newer image and recreating the container — your data folder is untouched:

```bash
docker compose pull
docker compose up -d
```

Everything configurable lives in `.env`, never in `docker-compose.yml` or inside the image — that's the **only** place you change the exposed port, the data location, or which release to run (`latest` tracks newest; pin it to `v0.3.0`-style tags to control upgrades yourself).

> **No authentication.** Same caveat as every other install path (see [Legal notes](#legal-notes)) — don't expose the container directly to the internet; put it behind a VPN or an authenticated reverse proxy.

<details>
<summary>Building the image yourself instead of pulling from GHCR</summary>

```bash
git clone https://github.com/tosc2571/FinFlow.git
cd FinFlow
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

</details>

---

## Architecture

```
                      ┌────────────────────┐
CSV (DKB) ─┐          │ FinFlow.Core       │
CSV (ING) ─┼─► parse ─│ parsers, XLSX/CSV  │─► use ─► FinFlow.Api ── SQLite ◄──► Angular frontend
CSV (…)   ─┘          │ export             │
                      └────────────────────┘
```

| Project/Folder | Responsibility |
|---|---|
| `backend/FinFlow.Core` | Parsers, XLSX/CSV export — plain class library, no ASP.NET/EF references |
| `backend/FinFlow.Api` | REST API: EF Core + SQLite persistence, import pipeline (parse → dedupe → classify → persist), rules/categories/contracts/dashboard/export endpoints, Swagger in dev |
| `backend/FinFlow.Tests` | xUnit tests for parsers, data layer, import pipeline, filters, dashboard aggregates, contract matching/forecast |
| `frontend/` | Angular 21 SPA (standalone components, zoneless) — import wizard, transactions, rules, categories, contracts, dashboard; dev server proxies `/api` to the backend |

CI runs per area via path filters: `.github/workflows/backend-ci.yml` (`dotnet build` + `dotnet test`) and `frontend-ci.yml` (`ng build` + `ng test`).

Adding a new bank means writing one new parser class — see [Adding a new bank parser](#adding-a-new-bank-parser).

---

## Adding a new bank parser

1. Create a new class in `backend/FinFlow.Core/Parsing`, inheriting from `BankCsvParserBase`.
2. Set `BankName` (e.g. `"comdirect"`).
3. Define `HeaderSignature`: column names that appear together **only** in this bank's export.
4. Implement `MapRow(...)`: which column maps to which field on `LegacyTransaction`.
5. Register the new parser in `ParserRegistry`.
6. Add a matching test class in `backend/FinFlow.Tests` — see [backend/FinFlow.Tests/TESTS.md](backend/FinFlow.Tests/TESTS.md) for the pattern (header-signature test, cross-bank false-positive test, one-row mapping test).

The web app's import picks the new bank up automatically (detection and the import wizard's bank dropdown are driven by the registry).

---

## Classification

Every transaction is matched against your rules — first match wins, matched against counterparty + purpose. Rules live in the database and are managed on the *Rules* page (pattern, category, status, priority). Unmatched transactions land in *needs review*; manually assigned categories are marked as manual overrides and survive re-classification. Statuses: auto / needs review / ignore.

Category labels like Spenden, Kinderbetreuung, Kirchensteuer are intentionally kept in German since they map to actual German tax-return line items.

The exported workbook contains:

| Sheet | Content |
|---|---|
| **Übersicht** | Totals per category (auto / prüfen) |
| One sheet per category | Transactions in that category (green = auto/manually confirmed, yellow = please review) |
| **Zu prüfen** | All yellow transactions across every category |
| **Ignoriert** | Grouped summary of excluded transactions, for cross-checking |

---

## Legal notes

- This is not tax or financial advice. The classification is regex-based and can misclassify or miss transactions — always review the "needs review" queue (web) or the "Zu prüfen"/"Ignoriert" sheets (export) before filing.
- No warranty on correctness or completeness of the classification or the exported figures.
- Your data stays local: the SQLite database and your bank CSV exports never leave your machine and are gitignored. The app has no user accounts — anyone who can reach the API can use it, so keep it on your own machine or behind your own reverse proxy/VPN when self-hosting.
- Use at your own risk.

---

## Contributing

Issues and PRs are welcome. Run `dotnet test` (backend) and `cd frontend && npx ng test` (frontend) before submitting.

---

## License

MIT, see [LICENSE](LICENSE).
