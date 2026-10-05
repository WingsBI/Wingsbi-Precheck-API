using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Precheck.Models.DataModel;
using Precheck.Models.DTOs.Login;
using Precheck.Models.DTOs.Register;
using Precheck.Models.DTOs.Reset;

namespace Precheck.Service.Service.AuthService
{
    public interface IAuthService
    {      
        Task<AuthResponse> LoginAsync(LoginRequest request);

        Task<bool> RegisterAsync(RegisterRequest request);

        Task<bool> ResetAsync(ResetRequest request);

       }
}
