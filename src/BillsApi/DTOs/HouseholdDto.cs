namespace BillsApi.DTOs;

public class HouseholdDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<HouseholdMemberDto> Members { get; set; } = new();
}

public class HouseholdMemberDto
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
}