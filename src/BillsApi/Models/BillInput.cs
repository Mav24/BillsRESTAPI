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

    /// <summary>
    /// Indicates whether the bill has been paid.
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// Date when the bill was paid (null if not paid).
    /// </summary>
    public DateTime? PaidDate { get; set; }

    /// <summary>
    /// Controls who can see this bill: "private" (just you), "household" (whole household),
    /// or a specific user's email to share with that person only.
    /// Defaults to "household" if the user is in one, otherwise "private".
    /// </summary>
    public string? ShareWith { get; set; }
}

