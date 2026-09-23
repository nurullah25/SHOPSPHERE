using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopSphere.Api.Common;
using ShopSphere.Api.Features.Auth;

namespace ShopSphere.Api.Features.Account;

[ApiController]
[Route("api/account")]
[Authorize(Policy = Policies.Customer)]
public class AccountController : ControllerBase
{
    private readonly AccountService _accountService;

    public AccountController(AccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileDto>> GetProfile()
    {
        return Ok(await _accountService.GetProfileAsync(User.GetUserId()));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(UpdateProfileRequest request)
    {
        return Ok(await _accountService.UpdateProfileAsync(User.GetUserId(), request));
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        // The cookie identifies this session, so it can stay signed in
        var currentRefreshToken = Request.Cookies[AuthController.RefreshCookieName];
        await _accountService.ChangePasswordAsync(User.GetUserId(), request, currentRefreshToken);
        return NoContent();
    }

    [HttpGet("addresses")]
    public async Task<ActionResult<List<AddressDto>>> GetAddresses()
    {
        return Ok(await _accountService.GetAddressesAsync(User.GetUserId()));
    }

    [HttpPost("addresses")]
    public async Task<ActionResult<AddressDto>> CreateAddress(AddressRequest request)
    {
        return Ok(await _accountService.CreateAddressAsync(User.GetUserId(), request));
    }

    [HttpPut("addresses/{id:int}")]
    public async Task<ActionResult<AddressDto>> UpdateAddress(int id, AddressRequest request)
    {
        return Ok(await _accountService.UpdateAddressAsync(User.GetUserId(), id, request));
    }

    [HttpDelete("addresses/{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        await _accountService.DeleteAddressAsync(User.GetUserId(), id);
        return NoContent();
    }
}
