using BillsApi.Data;
using BillsApi.DTOs;
using BillsApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BillsApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class HouseholdsController : ControllerBase
{
    private readonly BillsDbContext _db;
    private readonly ILogger<HouseholdsController> _logger;

    public HouseholdsController(BillsDbContext db, ILogger<HouseholdsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get the current user's household information
    /// </summary>
    [HttpGet("my-household")]
    public async Task<IActionResult> GetMyHousehold()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _db.Users
            .Include(u => u.Household)
            .ThenInclude(h => h!.Members)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.Household is null)
        {
            return Ok(new { household = (object?)null });
        }

        var householdDto = new HouseholdDto
        {
            Id = user.Household.Id,
            Name = user.Household.Name,
            CreatedAt = user.Household.CreatedAt,
            Members = user.Household.Members.Select(m => new HouseholdMemberDto
            {
                Id = m.Id,
                Username = m.Username,
                Email = m.Email
            }).ToList()
        };

        return Ok(new { household = householdDto });
    }

    /// <summary>
    /// Create a new household (user must not already be in one)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateHousehold([FromBody] CreateHouseholdDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { error = "Household name is required" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _db.Users.FindAsync(userId);

        if (user is null)
        {
            return NotFound(new { error = "User not found" });
        }

        if (user.HouseholdId is not null)
        {
            return BadRequest(new { error = "You are already in a household. Leave your current household first." });
        }

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Households.Add(household);
        
        // Add user to household
        user.HouseholdId = household.Id;

        // Migrate existing bills to household
        var userBills = await _db.Bills.Where(b => b.UserId == userId).ToListAsync();
        foreach (var bill in userBills)
        {
            bill.HouseholdId = household.Id;
        }

        await _db.SaveChangesAsync();

        var householdDto = new HouseholdDto
        {
            Id = household.Id,
            Name = household.Name,
            CreatedAt = household.CreatedAt,
            Members = new List<HouseholdMemberDto>
            {
                new HouseholdMemberDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email
                }
            }
        };

        return CreatedAtAction(nameof(GetMyHousehold), new { id = household.Id }, householdDto);
    }

    /// <summary>
    /// Invite a user to your household by email
    /// </summary>
    [HttpPost("invite")]
    public async Task<IActionResult> InviteToHousehold([FromBody] InviteToHouseholdDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { error = "Email is required" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var currentUser = await _db.Users
            .Include(u => u.Household)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (currentUser?.HouseholdId is null)
        {
            return BadRequest(new { error = "You must be in a household to invite others" });
        }

        var invitedUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (invitedUser is null)
        {
            return NotFound(new { error = "No user found with that email" });
        }

        if (invitedUser.HouseholdId is not null)
        {
            return BadRequest(new { error = "That user is already in a household" });
        }

        // Add user to household
        invitedUser.HouseholdId = currentUser.HouseholdId;

        // Migrate their bills to household
        var invitedUserBills = await _db.Bills.Where(b => b.UserId == invitedUser.Id).ToListAsync();
        foreach (var bill in invitedUserBills)
        {
            bill.HouseholdId = currentUser.HouseholdId;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"User {invitedUser.Username} added to household successfully" });
    }

    /// <summary>
    /// Leave your current household
    /// </summary>
    [HttpPost("leave")]
    public async Task<IActionResult> LeaveHousehold()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _db.Users
            .Include(u => u.Household)
            .ThenInclude(h => h!.Members)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.HouseholdId is null)
        {
            return BadRequest(new { error = "You are not in a household" });
        }

        var householdId = user.HouseholdId.Value;
        
        // Remove user from household
        user.HouseholdId = null;

        // Keep their bills but remove household association
        var userBills = await _db.Bills.Where(b => b.UserId == userId).ToListAsync();
        foreach (var bill in userBills)
        {
            bill.HouseholdId = null;
        }

        // Check if household is now empty
        var remainingMembers = await _db.Users.CountAsync(u => u.HouseholdId == householdId);
        if (remainingMembers == 0)
        {
            var household = await _db.Households.FindAsync(householdId);
            if (household is not null)
            {
                _db.Households.Remove(household);
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Left household successfully" });
    }

    /// <summary>
    /// Remove a member from your household (only works if you're in the household)
    /// </summary>
    [HttpDelete("members/{memberId}")]
    public async Task<IActionResult> RemoveMember(string memberId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (currentUser?.HouseholdId is null)
        {
            return BadRequest(new { error = "You are not in a household" });
        }

        var memberToRemove = await _db.Users.FirstOrDefaultAsync(u => u.Id == memberId);

        if (memberToRemove is null)
        {
            return NotFound(new { error = "Member not found" });
        }

        if (memberToRemove.HouseholdId != currentUser.HouseholdId)
        {
            return BadRequest(new { error = "That user is not in your household" });
        }

        // Remove member
        memberToRemove.HouseholdId = null;

        // Keep their bills but remove household association
        var memberBills = await _db.Bills.Where(b => b.UserId == memberId).ToListAsync();
        foreach (var bill in memberBills)
        {
            bill.HouseholdId = null;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Member removed from household successfully" });
    }
}