//using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
//using Microsoft.IHttpContextAccessor;
using SalonBooking.Application.Interfaces;

namespace SalonBooking.Infrastructure.Authentication;

public class  CurrentUserService : ICurrentUserService
 {
     private readonly IHttpContextAccessor _httpContextAccessor;

     public CurrentUserService(IHttpContextAccessor httpContextAccessor)
     {
         _httpContextAccessor = httpContextAccessor;
     }

     public long UserId => GetClaimValue<long>(ClaimTypes.NameIdentifier);

     public long TenantId => GetClaimValue<long>("TenantId");
    //public long TenantId =>  long.Parse(
    //    _httpContextAccessor.HttpContext!
    //    .User.FindFirst("TenantId")!.Value);

     public long BranchId => GetClaimValue<long>("BranchId");

     public string Username => GetClaimValue<string>(ClaimTypes.Name);

     public string Role => GetClaimValue<string>(ClaimTypes.Role);

     private T GetClaimValue<T>(string claimType)
     {
         var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType);
         if (claim == null)
         {
             throw new Exception($"Claim '{claimType}' not found.");
         }

         return (T)Convert.ChangeType(claim.Value, typeof(T));
     }
 }