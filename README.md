# FinFlow

Self-hosted web app (plus a standalone CLI) that parses German bank CSV exports, classifies transactions for your Steuererklärung (tax return) and household budget, and exports a color-coded Excel workbook.

Supported banks: **DKB** (checking + credit card), **ING**, **HVB**, **Trade Republic**, **Postbank**, **Berliner Volksbank**.

> **Built with AI assistance.** This project was developed with the help of Claude Code. Review the classification results yourself (especially anything marked "needs review" or "ignored") before relying on them for anything official — see [Legal notes](#legal-notes).

---

## Features

**Web app** (Angular + ASP.NET Core + SQLite — single-tenant, no accounts; run one instance per household):

- CSV import wizard: drop multiple files, each file's bank is auto-detected from its header, override per file where needed; duplicate bookings are skipped automatically on re-import, and every import batch can be rolled back.
- Filterable, paged transaction list with inline category assignment (manual choices survive re-classification), a "needs review" queue, and ignore/delete actions.
- Classification rules (regex → category → status) managed in the UI, with a live pattern test that shows matching transactions as you type, and one-click re-classification.
- Dashboard: income/expenses/net summary, monthly trend, per-category breakdown.
- XLSX/CSV export honoring the current filter — the XLSX is the same tax-return workbook the CLI produces.

**CLI** (stateless — no database, reads CSVs fresh on every run):

- Parse, filter (date/amount/text), print as table/CSV/JSON, and export the tax workbook driven by a local `rules.json`.

**Shared parsing engine:**

- Auto-detects encoding (UTF-8, UTF-16, Windows-1252 fallback), delimiter (`;`, `,`, tab, `|`), header row position, decimal format (`1.234,56` and `1,234.56`), and date format — no manual configuration per file.

---

## Getting started (web app)

No Docker needed — FinFlow runs as a single local app that serves both the UI and the API.

**Easiest: download a release.** Grab the zip for your platform from the [Releases page](https://github.com/tosc2571/FinFlow/releases), unpack it, run `FinFlow.Api.exe` (Windows) or `./FinFlow.Api` (Linux), and open **http://localhost:5199**. The binaries are self-contained — no .NET, Node, or Docker required.

**Or build from source** (requires [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Node.js 24+](https://nodejs.org) — running the built app afterwards needs neither Node nor Docker, only the .NET runtime):

```bash
git clone https://github.com/tosc2571/FinFlow.git
cd FinFlow

# Windows
scripts/publish.ps1

# Linux/macOS
scripts/publish.sh
```

Then start `publish/FinFlow.Api.exe` (Windows) or `dotnet publish/FinFlow.Api.dll` and open **http://localhost:5199** — that's the whole installation. Your data lives in a single SQLite file (`finflow.db`) next to the app, created on first start.

**Typical first session:** Import → drop your bank CSVs (or try `samples/*.csv`) → Categories → create your categories → Rules → add regex rules (the live test shows what they'd match) and re-run classification → Transactions → categorize the rest via the "needs review" filter → Dashboard/Export.

### Development mode

For working on the code there is a dev setup with live reload (API on :5199 with Swagger under `/swagger`, Angular dev server on :4200 proxying `/api`):

```bash
scripts/dev.ps1      # Windows — or scripts/dev.sh on Linux/macOS
```

Or manually: `dotnet run --project backend/FinFlow.Api` in one terminal, `cd frontend && npm install && npm start` in another, then open http://localhost:4200.

---

## Getting started (CLI)

The CLI works without the web app or database — useful for a quick one-off tax export:

```bash
# 1. Personalize the classification rules (the shipped file is a generic template)
cp backend/FinFlow.Cli/rules.example.json backend/FinFlow.Cli/rules.json

# 2. Run against a folder of CSV exports (mixing banks is fine — auto-detected per file)
dotnet run --project backend/FinFlow.Cli -- --dir ./my-exports --year 2025 --export taxes-2025.xlsx

# Or try it immediately against the bundled synthetic sample data:
dotnet run --project backend/FinFlow.Cli -- --dir ./samples --year 2025 --export sample-export.xlsx --rules backend/FinFlow.Cli/rules.example.json
```

### CLI reference

```
Input:    -f/--file <path> (repeatable) | -d/--dir <folder>
          -b/--bank <dkb|ing|hvb|traderepublic|postbank|volksbank|auto>
              Applies to all following --file arguments (sticky).
              Set before --dir: applies to every file in the folder.
Filter:   --from <yyyy-MM-dd> | --to <yyyy-MM-dd> | -y/--year <yyyy>
          --min <n> | --max <n> | -c/--contains <text> | --counterparty <text>
Output:   --sort <date|date-desc|amount|amount-desc> | --format <table|csv|json>
          -e/--export <file.xlsx> | -r/--rules <rules.json>
          -h/--help
```

---

## Architecture

```
                      ┌────────────────────┐
CSV (DKB) ─┐          │ FinFlow.Core       │        ┌─ FinFlow.Cli ── rules.json ──► taxes-2025.xlsx
CSV (ING) ─┼─► parse ─│ parsers/classifier │─► use ─┤
CSV (…)   ─┘          │ filter/export      │        └─ FinFlow.Api ── SQLite ◄──► Angular frontend
                      └────────────────────┘
```

| Project/Folder | Responsibility |
|---|---|
| `backend/FinFlow.Core` | Parsers, classifier, filter, XLSX/CSV export — plain class library, no CLI/ASP.NET/EF references |
| `backend/FinFlow.Cli` | Thin, stateless CLI entry point (`Program.cs` + `rules.example.json`) |
| `backend/FinFlow.Api` | REST API: EF Core + SQLite persistence, import pipeline (parse → dedupe → classify → persist), rules/categories/dashboard/export endpoints, Swagger in dev |
| `backend/FinFlow.Tests` | xUnit tests for parsers, data layer, import pipeline, filters, dashboard aggregates |
| `frontend/` | Angular 21 SPA (standalone components, zoneless) — import wizard, transactions, rules, categories, dashboard; dev server proxies `/api` to the backend |

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

Both the CLI and the web app's import pick the new bank up automatically (detection and the import wizard's bank dropdown are driven by the registry).

---

## Classification

Every transaction is matched against your rules — first match wins, matched against counterparty + purpose:

- **Web app:** rules live in the database and are managed on the *Rules* page (pattern, category, status, priority). Unmatched transactions land in *needs review*; manually assigned categories are marked as manual overrides and survive re-classification. Statuses: auto / needs review / ignore.
- **CLI:** rules come from a local `rules.json` (`pattern`/`category`/`status`, statuses `auto`/`prüfen`/`ignorieren`). `backend/FinFlow.Cli/rules.example.json` ships as a sanitized template — copy it to `rules.json` (gitignored) and tailor it to your own vendors and employer.

Category labels like Spenden, Kinderbetreuung, Kirchensteuer are intentionally kept in German since they map to actual German tax-return line items.

The exported workbook (identical for CLI and web export) contains:

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
- Your data stays local: the SQLite database, your bank CSV exports, and your real `rules.json` never leave your machine and are gitignored. The app has no user accounts — anyone who can reach the API can use it, so keep it on your own machine or behind your own reverse proxy/VPN when self-hosting.
- Use at your own risk.

---

## Contributing

Issues and PRs are welcome. Run `dotnet test` (backend) and `cd frontend && npx ng test` (frontend) before submitting.

---

## License

MIT, see [LICENSE](LICENSE).
