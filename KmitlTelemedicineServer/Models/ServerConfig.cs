namespace KmitlTelemedicineServer.Models;

/// <summary>
///     Wrapper for `appsettings.json`
/// </summary>
public record ServerConfig(
    string DefaultWaitingRoomId,
    string VisitIdDateFormat,
    string PathBase,
    string FirebaseProjectId,
    string JwtRoleClaimName,
    int UserNameMaxLength,
    // ReSharper disable once InconsistentNaming
    string OnlyForDevelopment_FirebaseWebApiKey)
{
    public const string Section = "ServerConfig";

    public ServerConfig()
        : this(
            "",
            "",
            "/",
            "",
            "role",
            100,
            ""
        )
    {
    }
}

public static class ServerConfigExtension
{
    public static ServerConfig GetServerConfig(this IConfiguration config)
    {
        return config.GetSection(ServerConfig.Section).Get<ServerConfig>();
    }

    public static IServiceCollection AddServerConfig(
        this IServiceCollection services, ServerConfig config)
    {
        return services.AddTransient(_ => config with { } /*make copies*/);
    }
}