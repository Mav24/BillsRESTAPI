using System.ComponentModel.DataAnnotations;

namespace BillsWeb.Models;

public class BillViewModel
{
    public int Id { get; set; }

    [Required]
    public string BillName { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.Now;

    public decimal AmountOverMinimum { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaidDate { get; set; }

    public Guid? HouseholdId { get; set; }

    public string? SharedBy { get; set; }

    public bool IsShared { get; set; }

    public string? ShareWith { get; set; }
}

public class BillShareViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; }
}
