namespace BillsWeb.Models;

public class HouseholdViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<HouseholdMemberViewModel> Members { get; set; } = new();
}

public class HouseholdMemberViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}
