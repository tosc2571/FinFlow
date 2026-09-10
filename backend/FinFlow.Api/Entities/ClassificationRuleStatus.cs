namespace FinFlow.Api.Entities;

public enum ClassificationRuleStatus
{
    Auto,
    NeedsReview,
    Ignore,
    /// <summary>A matching rule marks the transaction as a transfer between the user's own
    /// accounts (see #66) — the fallback for banks whose export doesn't reliably populate the
    /// counterparty IBAN that TransferDetectionService normally relies on.</summary>
    InternalTransfer,
}
