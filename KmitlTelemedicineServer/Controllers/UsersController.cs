using System.ComponentModel.DataAnnotations;
using System.Text;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Grpc.Core;
using KmitlTelemedicineServer.Models;
using KmitlTelemedicineServer.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace KmitlTelemedicineServer.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly ServerConfig _config;
    private readonly FirestoreDb _firestore;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ILogger<UsersController> logger, FirestoreDb firestore, ServerConfig config)
    {
        _logger = logger;
        _firestore = firestore;
        _config = config;
    }

    [HttpPost("register/me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<User>> RegisterMyselfAsync(
        [FromBody] [Required] UserRegisterMyselfRequest req,
        [FromServices] FirebaseUserAccessor fbUser)
    {
        var user = new User
        {
            FirstName = req.FirstName,
            LastName = req.LastName,
            Hn = null,
            Role = UserRole.Patient,
            Status = UserStatus.Active
        };

        if (NormalizeFirestoreUser(user) is { } r1) return r1;

        UserRecord myUserRecord;
        try
        {
            myUserRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(fbUser.UserId!);
        }
        catch (FirebaseAuthException e)
        {
            return BadRequest("User not found");
        }

        if (await RegisterUser(myUserRecord, user) is { } r2) return r2;

        ForceClientRefreshToken();

        return Ok(user);
    }

    [Authorize("RequireEmailVerified", Roles = "admin")]
    [HttpPost("register/userid")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<User>> RegisterByUserIdAsync(
        [FromQuery(Name = "userid")] [StringLength(128, MinimumLength = 1)] [Required]
        string unvalidatedUserId,
        [FromBody] [Required] User user)
    {
        if (NormalizeFirestoreUser(user) is { } r1) return r1;

        UserRecord userRecord;
        try
        {
            userRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(unvalidatedUserId);
        }
        catch (FirebaseAuthException)
        {
            return BadRequest("User not found");
        }

        if (await RegisterUser(userRecord, user) is { } r2) return r2;

        return Ok(user);
    }

    [Authorize("RequireEmailVerified", Roles = "admin")]
    [HttpPost("register/email")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<User>> RegisterByEmail(
        [FromQuery] [Required] string email,
        [FromBody] [Required] User user)
    {
        if (NormalizeFirestoreUser(user) is { } r1) return r1;

        UserRecord userRecord;
        try
        {
            userRecord = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(email);
        }
        catch (FirebaseAuthException e)
        {
            return BadRequest("User not found");
        }

        if (await RegisterUser(userRecord, user) is { } r2) return r2;

        return Ok(user);
    }

    [Authorize("RequireEmailVerified", Roles = "admin")]
    [HttpPost]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<User>> CreateUser(
        [FromBody] [Required] CreateUserRequest req)
    {
        if (NormalizeFirestoreUser(req.User) is { } r1) return r1;

        var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(new UserRecordArgs
        {
            Email = req.Email,
            Password = req.Password,
            EmailVerified = req.EmailVerified ?? false,
            Disabled = req.User.Status == UserStatus.InActive
        });

        if (await RegisterUser(userRecord, req.User) is { } r2) return r2;

        return Ok(req.User);
    }

    [Authorize("RequireEmailVerified", Roles = "admin")]
    [HttpDelete("{userid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> LogicalDeleteUser(
        [FromRoute(Name = "userid")] [StringLength(128, MinimumLength = 1)] [Required]
        string unvalidatedUserId)
    {
        UserRecord userRecord;
        try
        {
            userRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(unvalidatedUserId);
        }
        catch (FirebaseAuthException)
        {
            return NotFound("User not found");
        }

        try
        {
            await _firestore
                .Collection("users")
                .Document(userRecord.Uid)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "status", EnumNameConverter<UserStatus>.GetStringValue(UserStatus.InActive) }
                }, cancellationToken: HttpContext.RequestAborted);
        }
        catch (RpcException e) when (e.StatusCode is Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogTrace("User Record(id=\"{}\") not found", userRecord.Uid);
        }

        if (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return NoContent();
        }

        await FirebaseAuth.DefaultInstance.UpdateUserAsync(new UserRecordArgs
        {
            Uid = userRecord.Uid,
            Disabled = true
        });

        _logger.LogInformation("[LogicalDeleteUser] UID=\"{}\"", userRecord.Uid);

        return Ok();
    }

    private void ForceClientRefreshToken(bool value = true)
    {
        Response.Headers["X-Force-Refresh-Token"] = new StringValues(value.ToString());
    }

    private async Task<ActionResult?> RegisterUser(UserRecord firebaseUser, User firestoreUser)
    {
        if (firebaseUser.Disabled) return BadRequest("User is disabled");

        try
        {
            await _firestore
                .Collection("users")
                .Document(firebaseUser.Uid)
                .CreateAsync(firestoreUser, HttpContext.RequestAborted);
        }
        catch (RpcException e) when (e.StatusCode is Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict("User is already registered");
        }

        if (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return NoContent();
        }

        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(firebaseUser.Uid, new Dictionary<string, object>
        {
            { "role", EnumNameConverter<UserRole>.GetStringValue(firestoreUser.Role) }
        });

        _logger.LogInformation("[RegisterUser] UID=\"{}\", Role={}",
            firebaseUser.Uid, firestoreUser.Role);

        return null;
    }

    private ActionResult? NormalizeFirestoreUser(User user)
    {
        user.FirstName = NormalizeString(user.FirstName);
        if (user.FirstName.Length < 1 ||
            _config.UserNameMaxLength < user.FirstName.Length)
            return BadRequest("Invalid format: FirstName");

        user.LastName = NormalizeString(user.LastName);
        if (user.LastName.Length < 1 ||
            _config.UserNameMaxLength < user.LastName.Length)
            return BadRequest("Invalid format: LastName");

        return null;
    }

    private static string NormalizeString(string input)
    {
        return new string(input.Normalize(NormalizationForm.FormKC).Trim().Where(c => !char.IsControl(c)).ToArray());
    }
}