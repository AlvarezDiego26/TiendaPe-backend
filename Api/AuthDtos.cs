namespace TiendaPe.Api;

public sealed record BootstrapRequest(string FullName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(Guid UserId, string FullName, string Email, string Token, DateTime ExpiresAt);
