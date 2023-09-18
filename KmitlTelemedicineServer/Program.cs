using AspNetCore.Firebase.Authentication.Extensions;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using KmitlTelemedicineServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

internal class Program
{
    private static string _projectId;

    public static void Main(string[] args)
    {
        FirebaseApp.Create();
        _projectId =
            (FirebaseApp.DefaultInstance.Options.Credential.UnderlyingCredential
                as ServiceAccountCredential)!
            .ProjectId;

        WebApplication app = BuildApplication(args);

        Configure(app);

        app.Run();
    }

    static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });
        builder.Services.AddServerConfig(builder.Configuration);
        builder.Services.AddSingleton<FirestoreDb>(_ => FirestoreDb.Create(_projectId));
        builder.Services.AddFirebaseAuthentication(_projectId);
        builder.Services.AddAuthorization();
        builder.Services.AddSwaggerGen(options =>
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "FirebaseJwtBarer",
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme
            };

            options.AddSecurityDefinition(securityScheme.Name, securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin();
                builder.AllowAnyHeader();
                builder.AllowAnyMethod();
            });
        });

        return builder.Build();
    }

    static void Configure(WebApplication app)
    {
        var config = app.Services.GetRequiredService<IOptions<ServerConfig>>().Value;

        if (!string.IsNullOrWhiteSpace(config.PathBase))
        {
            app.Logger.LogInformation("Path base: {PathBase}", config.PathBase);
            app.UsePathBase(config.PathBase);
            app.UseRouting();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors();
        }

        app.UseForwardedHeaders();

        app.MapApiEndpoints();
    }
}