using WarcraftArchive.Api.Contracts;
using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string email, string password, string? userAgent, string? deviceName);
    Task<RefreshResponse?> RefreshAsync(string rawRefreshToken, string? userAgent, string? deviceName);
    Task<bool> LogoutAsync(string rawRefreshToken);
    Task<int> LogoutAllAsync(Guid userId);
    Task<User?> CreateUserAsync(string email, string userName, string password, bool isAdmin);
    Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest req);
    Task<bool> DeleteUserAsync(Guid id);
    Task<List<UserDto>> GetAllUsersAsync();
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
