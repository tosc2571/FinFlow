# FinFlow — test environment

## Running tests

```bash
# All tests
dotnet test tests/FinFlow.Tests

# Test a single parser only
dotnet test tests/FinFlow.Tests --filter "ClassName=FinFlow.Tests.HvbCsvParserTests"

# Only CanParse tests (header checks)
dotnet test tests/FinFlow.Tests --filter "MethodName~CanParse"
```

Run from the repository root, or with a full path.

---

## Understanding the output

```
Passed! : Failed: 0, Passed: 63, Skipped: 0, Total: 63
```

On failure, xUnit shows directly which test failed and why:

```
Failed FinFlow.Tests.HvbCsvParserTests.Parse_OneRow_MapsFieldsCorrectly
  Assert.Equal() Failure
  Expected: -1200,00
  Actual:   0
```

---

## Test classes and what they check

| Test class | Parser | Focus |
|---|---|---|
| `HvbCsvParserTests` | HypoVereinsbank (checking) | Semicolon delimiter, `Empfaenger 1/2` (old header) and their absence (new header — see below) |
| `HvbKreditkarteCsvParserTests` | HypoVereinsbank (credit card) | `Kartennummer` header — byte-identical to DKB's credit card export, documented as a known auto-detect ambiguity |
| `IngCsvParserTests` | ING DiBa | Metadata rows before the header, optional balance column, `Valuta`/`Wertstellungsdatum` variants |
| `DkbCsvParserTests` | DKB checking account | Incoming vs. outgoing (`Zahlungspflichtige*r`) |
| `DkbKreditkarteCsvParserTests` | DKB credit card | `Kartennummer` header, empty amount → row skipped |
| `TradeRepublicCsvParserTests` | Trade Republic | Comma delimiter, securities fallback to asset name |
| `PostbankCsvParserTests` | Postbank Berlin | `Umsatzart` header, fallback to `Soll`/`Haben` when `Betrag` is empty |
| `VolksbankCsvParserTests` | Berliner Volksbank | `Zahlungsbeteiligter` columns, `Saldo nach Buchung` |

---

## Testing a changed header

If a bank changes its export format, update the header string in the matching test class and run `CanParse_ValidHeader_ReturnsTrue`:

```csharp
// HvbCsvParserTests.cs
private const string Header =
    "Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Empfaenger 2;Verwendungszweck;Betrag;Waehrung";
```

If `CanParse` then fails, update `HeaderSignature` in the parser.

---

## Adding a new parser's tests

1. Create a new class `YourBankCsvParserTests.cs` inheriting from `CsvParserTestBase`.
2. Fill in a `Header` constant with the real header from that bank's export.
3. Copy and adapt the minimum set of tests:
   - `CanParse_ValidHeader_ReturnsTrue`
   - `CanParse_OtherBankHeader_ReturnsFalse` (other banks as `[InlineData]`)
   - `Parse_OneRow_MapsFieldsCorrectly`
