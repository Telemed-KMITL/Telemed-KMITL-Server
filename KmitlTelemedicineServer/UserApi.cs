using System.ComponentModel.DataAnnotations;
using System.Text;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace KmitlTelemedicineServer;

public static class UserApiExtension
{
    public static IEndpointRouteBuilder MapUserApiEndpoints(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost(UserApi.BasePath + "/me/register", UserApi.RegisterPatientAsync)
            .RequireAuthorization()
            .WithName("RegisterPatientUser")
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        builder
            .MapMethods(UserApi.BasePath + "/me", new[] { "PATCH" }, UserApi.UpdatePatientAsync)
            .RequireAuthorization("OnlyForPatients")
            .WithName("UpdatePatientUser")
            .Produces<UpdatePatientUserSuccessResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        builder
            .MapMethods(UserApi.BasePath, new[] { "PUT" }, UserApi.CreateUserAsync)
            .RequireAuthorization("RequireEmailVerified", "OnlyForAdmins")
            .WithName("CreateUser")
            .Accepts<CreateUserRequest>("application/json")
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        builder
            .MapPost(UserApi.BasePath + "/{userId}/register", UserApi.RegisterUserAsync)
            .RequireAuthorization("RequireEmailVerified", "OnlyForAdmins")
            .WithName("RegisterUser")
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        builder
            .MapGet(UserApi.BasePath + "/{userId}/role", UserApi.GetUserRoleAsync)
            .RequireAuthorization("RequireEmailVerified")
            .WithName("GetUserRole")
            .Produces<UserRoleResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        builder
            .MapMethods(UserApi.BasePath + "/{userId}/role", new[] { "PATCH" }, UserApi.SetUserRoleAsync)
            .RequireAuthorization("RequireEmailVerified")
            .WithName("UpdateUserRole")
            .Produces<UserRoleResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return builder;
    }
}

public class UserApi
{
    public const string BasePath = "/users";

    public static async Task<IResult> RegisterPatientAsync(
        [StringLength(100, MinimumLength = 1)] string firstName,
        [StringLength(100, MinimumLength = 1)] string lastName,
        FirestoreDb firestore,
        FirebaseUserAccessor userAccessor,
        ILogger<UserApi> logger,
        ServerConfig config)
    {
        const UserRole role = UserRole.Patient;

        if (CheckUserDataErrors(config, ref firstName!, ref lastName!, out var error)) return Results.BadRequest(error);

        var uid = userAccessor.UserId!;
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Hn = null,
            Role = role,
            Status = UserStatus.Active
        };

        try
        {
            await CreateUserRecordAsync(firestore, config, logger, uid, user);
        }
        catch (FirebaseAuthException e)
        {
            return Results.BadRequest(e.Message);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.AlreadyExists)
        {
            return Results.BadRequest("User already registered");
        }

        return Results.Ok(new UserResponse
        (
            uid,
            user
        ));
    }

    public static async Task<IResult> UpdatePatientAsync(
        [StringLength(100, MinimumLength = 1)] string? firstName,
        [StringLength(100, MinimumLength = 1)] string? lastName,
        FirestoreDb firestore,
        FirebaseUserAccessor userAccessor,
        ILogger<UserApi> logger,
        ServerConfig config)
    {
        if (CheckUserDataErrors(config, ref firstName, ref lastName, out var error)) return Results.BadRequest(error);

        var uid = userAccessor.UserId!;

        var patch = new Dictionary<string, object>();
        if (firstName != null) patch.Add("firstName", firstName);
        if (lastName != null) patch.Add("lastName", lastName);
        if (patch.Count == 0) return Results.BadRequest();

        try
        {
            await firestore
                .Collection("users")
                .Document(uid)
                .UpdateAsync(patch);
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            return Results.BadRequest("User is not registered");
        }

        return Results.Ok(new UpdatePatientUserSuccessResponse
        (
            uid
        ));
    }

    public static async Task<IResult> GetUserRoleAsync(
        [StringLength(128, MinimumLength = 1)] string userId,
        ILogger<UserApi> logger,
        ServerConfig config)
    {
        logger.LogInformation("[GetUserRoleAsync] userId: \"{}\"", userId);

        UserRecord user;
        try
        {
            user = await FirebaseAuth.DefaultInstance.GetUserAsync(userId);
        }
        catch (FirebaseAuthException)
        {
            return Results.BadRequest("User not found");
        }

        var role = user.CustomClaims.GetValueOrDefault(config.JwtRoleClaimName) as string;
        return Results.Ok(new UserRoleResponse
        (
            userId,
            string.IsNullOrEmpty(role) ? null : EnumNameConverter<UserRole>.Parse(role)
        ));
    }

    public static async Task<IResult> SetUserRoleAsync(
        [StringLength(128, MinimumLength = 1)] string userId,
        [FromQuery(Name = "role")] string roleName,
        FirestoreDb firestore,
        ILogger<UserApi> logger,
        ServerConfig config)
    {
        UserRole role;
        try
        {
            role = EnumNameConverter<UserRole>.Parse(roleName);
        }
        catch (ArgumentException e)
        {
            return Results.BadRequest($"role: {e.Message}");
        }

        logger.LogInformation("[GetUserRoleAsync] userId: \"{}\"", userId);

        try
        {
            await UpdateUserRole(config, userId, role);
        }
        catch (FirebaseAuthException)
        {
            return Results.BadRequest("User not found");
        }

        try
        {
            await firestore
                .Collection("users")
                .Document(userId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "role", EnumNameConverter<UserRole>.GetStringValue(role) }
                });
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            // Skip errors when document "/users/(uid)" is not exist
        }

        return Results.Ok(new UserRoleResponse
        (
            userId,
            role
        ));
    }

    public static async Task<IResult> CreateUserAsync(
        [FromBody] CreateUserRequest req,
        FirestoreDb firestore,
        ServerConfig config,
        ILogger<UserApi> logger)
    {
        if (CheckUserDataErrors(config, req.user, out string error))
        {
            return Results.BadRequest(error);
        }

        UserRecord firebaseUser;
        try
        {
            firebaseUser = await FirebaseAuth.DefaultInstance.CreateUserAsync(new UserRecordArgs
            {
                Email = req.email,
                Password = req.password,
                EmailVerified = req.emailVerified ?? false
            });
        }
        catch (ArgumentException e)
        {
            return Results.BadRequest(e.Message);
        }
        catch (FirebaseAuthException e) when (e.AuthErrorCode is AuthErrorCode.EmailAlreadyExists)
        {
            return Results.BadRequest(e.Message);
        }

        await CreateUserRecordAsync(firestore, config, logger, firebaseUser.Uid, req.user);

        return Results.Ok(new UserResponse
        (
            userId: firebaseUser.Uid,
            user: req.user
        ));
    }

    public static async Task<IResult> RegisterUserAsync(
        [FromRoute] string userId,
        [FromBody] User user,
        FirestoreDb firestore,
        ServerConfig config,
        ILogger<UserApi> logger)
    {
        if (CheckUserDataErrors(config, user, out string error))
        {
            return Results.BadRequest(error);
        }

        try
        {
            await CreateUserRecordAsync(firestore, config, logger, userId, user);
        }
        catch (FirebaseAuthException e)
        {
            return Results.BadRequest(e.Message);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.AlreadyExists)
        {
            return Results.BadRequest("User already registered");
        }

        return Results.Ok(new UserResponse
        (
            userId: userId,
            user: user
        ));
    }

    /// <exception cref="FirebaseAuthException">When userid is not exist</exception>>
    /// <exception cref="Grpc.Core.RpcException">StatusCode="AlreadyExists"</exception>
    private static async Task CreateUserRecordAsync(
        FirestoreDb firestore,
        ServerConfig config,
        ILogger logger,
        string uid,
        User user)
    {
        logger.LogInformation("[CreateUserRecord] ID: {}, Role: {}", uid, user.Role);

        if (user.Role != null) await UpdateUserRole(config, uid, user.Role.Value);

        await firestore
            .Collection("users")
            .Document(uid)
            .CreateAsync(user);
    }

    private static async Task UpdateUserRole(ServerConfig config, string uid, UserRole role)
    {
        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(uid, new Dictionary<string, object>
        {
            { config.JwtRoleClaimName, EnumNameConverter<UserRole>.GetStringValue(role) }
        });
    }

    private static bool CheckUserDataErrors(ServerConfig config, User user, out string errorMsg)
    {
        string firstName = user.FirstName;
        string lastName = user.LastName;

        if (CheckUserDataErrors(config, ref firstName!, ref lastName!, out errorMsg))
        {
            return true;
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        return false;
    }

    private static bool CheckUserDataErrors(
        ServerConfig config,
        ref string? firstName,
        ref string? lastName,
        out string errorMsg)
    {
        if (firstName != null)
        {
            firstName = NormalizeString(firstName);
            if (firstName.Length < 1 && config.UserNameMaxLength < firstName.Length)
            {
                errorMsg = "Invalid format: FirstName";
                return true;
            }
        }

        if (lastName != null)
        {
            lastName = NormalizeString(lastName);
            if (lastName.Length < 1 && config.UserNameMaxLength < lastName.Length)
            {
                errorMsg = "Invalid format: LastName";
                return true;
            }
        }

        errorMsg = "";
        return false;
    }

    private static string NormalizeString(string input)
    {
        return new string(input.Normalize(NormalizationForm.FormKC).Trim().Where(c => !char.IsControl(c)).ToArray());
    }
}

public record CreateUserRequest(string email, string password, bool? emailVerified, User user);

public record UpdatePatientUserSuccessResponse(string userId);

public record UserResponse(string userId, User user);

public record UserRoleResponse(string userId, UserRole? role);