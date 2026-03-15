namespace BillsWeb.Models;

public class HouseholdViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<HouseholdMemberViewModel> Members { get; set; } = new();
}

public class HouseholdMemberViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
