using System.Security.Claims;
using System.Text.Json.Nodes;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google;
using Google.Cloud.Firestore;
using KmitlTelemedicineServer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;

internal class Program
{
    public static void Main(string[] args)
    {
        var app = BuildApplication(args);

        InitializeFirebase(app);
        Configure(app);

        app.Run();
    }

    private static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration.GetServerConfig();

        builder.Services.AddServerConfig(config);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });
        builder.Services.AddSingleton<FirestoreDb>(provider => FirestoreDb.Create(config.FirebaseProjectId));
        builder.Services.AddFirebaseAuthentication(config.FirebaseProjectId);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireEmailVerified", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var user = context.User;

                    var email = user.FindFirstValue(ClaimTypes.Email);
                    var emailVerified = user.FindFirstValue("email_verified")?.ToLower() == "true";

                    return string.IsNullOrEmpty(email) || emailVerified;
                });
            });
        });
        builder.Services.AddSwaggerGen(options =>
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "FirebaseJwtBarer",
                Type = SecuritySchemeType.Http,
                // NOTE: openapi-generator's BearerAuthInterceptor accepts only "bearer" (not "Bearer")
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            };

            options.AddSecurityDefinition(securityScheme.Name, securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = securityScheme.Name
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(b =>
            {
                b.AllowAnyOrigin();
                b.AllowAnyHeader();
                b.AllowAnyMethod();
            });
        });
        builder.Services.AddTransient<FirebaseDbUserProvider>();

        return builder.Build();
    }

    private static void Configure(WebApplication app)
    {
        var config = app.Services.GetRequiredService<ServerConfig>();

        app.UseHttpLogging();

        if (!string.IsNullOrWhiteSpace(config.PathBase))
        {
            app.Logger.LogInformation("Path base: {PathBase}", config.PathBase);
            app.UsePathBase(config.PathBase);
            app.UseRouting();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors();

            app.MapGet("/dev/getToken", async (string uid) =>
            {
                var customToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(uid);

                if (customToken == null) return Results.BadRequest();

                var requestUrl =
                    "https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken?" +
                    $"key={config.OnlyForDevelopment_FirebaseWebApiKey}";

                var body = new
                {
                    token = customToken,
                    returnSecureToken = true
                };

                var apiResponse = await new HttpClient().PostAsJsonAsync(requestUrl, body);
                var content = await apiResponse.Content.ReadAsStringAsync();

                app.Logger.LogDebug("POST \"{}\":\n{}", requestUrl, content);

                var response = JsonNode.Parse(content)!["idToken"]!.GetValue<string>();
                return Results.Text(response);
            });
        }

        app.MapVisitApiEndpoints();
    }

    private static void InitializeFirebase(WebApplication app)
    {
        ApplicationContext.RegisterLogger(new GoogleLoggingWrapper(app));
        FirebaseApp.Create();
    }
}