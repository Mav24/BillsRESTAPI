namespace BillsApi.Models;

public record ResetPasswordRequest(string UserId, string Token, string NewPassword);
