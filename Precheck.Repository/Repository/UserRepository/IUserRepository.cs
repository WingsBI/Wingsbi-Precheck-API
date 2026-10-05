using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Precheck.Models.DataModel;
using Precheck.Models.DTOs.Reset;

namespace Precheck.Repository.Repository.UserRepository
{
    public interface IUserRepository
    {
        Task<User?> GetUserByEmail(string email);

        Task<User?> GetUserByUserName(string UserName);

        Task<User?> GetUserByUserid(string userid);

        Task UpdateUserAsync(ResetModel user);

        Task<User> RegisterUserAsync(User Usermodel);
        Task AddUserAsync(User user);

        Task AddRefreshTokenAsync(RefreshToken refreshToken);

        Task<User?> GetUserByIdAsync(int userId);
        Task UpdateRefreshTokenAsync(RefreshToken refreshToken);
        Task AddUserRoleAsync(UserRole userRole);
        Task<RefreshToken> GetRefreshTokenAsync(string refreshToken);
        Task<User> GetUserByUserIdAsync(string userid);
        Task RecordFailedLoginAsync(string userid, int maxAttempts, int lockoutMinutes);
        Task ResetFailedLoginAsync(string userid);
    }
}
