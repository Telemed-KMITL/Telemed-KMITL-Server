using KmitlTelemedicineServer.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace KmitlTelemedicineServer;

public static class FirebaseAuthenticationExtensions
{
    public static IServiceCollection AddFirebaseAuthentication(
        this IServiceCollection services,
        ServerConfig config)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://securetoken.google.com/{config.FirebaseProjectId}";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://securetoken.google.com/{config.FirebaseProjectId}",
                    ValidateAudience = true,
                    ValidAudience = config.FirebaseProjectId,
                    ValidateLifetime = true
                };
            });
        return services;
    }
}