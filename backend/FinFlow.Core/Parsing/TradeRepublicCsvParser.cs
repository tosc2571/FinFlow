using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// Trade Republic transaction export (App: Profile → Account statements → Transaction export).
/// Header: datetime,date,account_type,category,type,asset_class,name,symbol,shares,price,
///         amount,fee,tax,currency,original_amount,original_currency,fx_rate,description,
///         transaction_id,counterparty_name,counterparty_iban,payment_reference,mcc_code
/// </summary>
public sealed class TradeRepublicCsvParser : BankCsvParserBase
{
    public override string BankName => "traderepublic";

    protected override string[] HeaderSignature =>
        ["transaction_id", "asset_class", "mcc_code"];

    protected override LegacyTransaction? MapRow(CsvReader csv) => new()
    {
        SourceBank = BankName,
        BookingDate = ParseDate(Field(csv, "date")),
        // datetime includes a time component — only use the date part
        ValueDate = ParseDate(Field(csv, "datetime")?.Split('T')[0].Split(' ')[0]),
        Amount = ParseAmount(Field(csv, "amount")),
        Currency = Field(csv, "currency") ?? "EUR",
        // For securities transactions counterparty_name is empty → fall back to the asset name
        CounterpartyName = Field(csv, "counterparty_name", "name"),
        CounterpartyIban = Field(csv, "counterparty_iban"),
        Purpose = Field(csv, "description", "payment_reference"),
        BookingType = Field(csv, "type", "category"),
    };
}
