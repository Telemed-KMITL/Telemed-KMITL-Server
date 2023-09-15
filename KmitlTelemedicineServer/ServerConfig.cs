﻿namespace KmitlTelemedicineServer;

/// <summary>
/// Wrapper for `appsettings.json`
/// </summary>
internal record ServerConfig(
    string DefaultWaitingRoomId,
    string VisitIdDateFormat,
    string PathBase)
{
    public const string Section = "ServerConfig";
    
    public ServerConfig()
        : this("", "", "/") 
    { }
}

internal static class ServerConfigExtension
{
    public static IServiceCollection AddServerConfig(
        this IServiceCollection services, IConfiguration config)
    {
        return services.Configure<ServerConfig>(config.GetSection(ServerConfig.Section));
    }
}