namespace BillsApi.Models;

/// <summary>
/// Input DTO for creating/updating bills (without Id).
/// </summary>
public class BillInput
{
    /// <summary>
    /// Name/description of the bill (e.g., "Electricity", "Gas", "Water").
    /// </summary>
    public required string BillName { get; set; }

    /// <summary>
    /// The full bill amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date of the bill (ISO 8601 format).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Amount over the minimum threshold (user-entered value).
    /// </summary>
    public decimal AmountOverMinimum { get; set; }
}
