using Dalan.Identity.Keys;
using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Account;
using Dalan.Identity.DevLogin;
using Dalan.Identity.Passwords;
using Dalan.Identity.Sessions;
using Dalan.Identity.Codes;
using Dalan.Identity.EmailLogin;
using Dalan.Identity.GitHubLogin;
using Dalan.Identity.Merge;
using Dalan.Identity.OAuth;
using Dalan.Identity.SecondFactor;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dalan.Identity;

/// <summary>Entry point of the Identity service. The host calls these methods.</summary>
public static class IdentityService
{
    public static IServiceCollection AddIdentityService(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<IdentityDb>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Dalan"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDb.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton<UserDirectory>();
        services.AddSingleton<IUserDirectory>(provider => provider.GetRequiredService<UserDirectory>());
        services.AddSingleton<IUserPhones>(provider => provider.GetRequiredService<UserDirectory>());
        services.AddSingleton<SuspendedUsers>();
        services.AddSingleton<UserAdmin>();
        services.AddSingleton<IUserAdmin>(provider => provider.GetRequiredService<UserAdmin>());

        services.AddOptions<IdentityOptions>().BindConfiguration("Identity");
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SessionKey>();
        services.AddSingleton<SessionTokens>();
        services.AddSingleton<SecurityLog>();
        services.AddSingleton<ProofOfWork>();
        services.AddSingleton<SmsCodes>();
        services.AddSingleton<EmailCodes>();
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<PasswordTries>();
        services.AddSingleton<SecondFactors>();
        services.AddSingleton<TrustedDevices>();
        services.AddSingleton<AccountMerger>();
        services.AddSingleton<MergeFollowUp>();
        services.AddSingleton<OAuthKey>();
        services.AddSingleton<OAuthStore>();
        services.AddSingleton<IProxyLogin, ProxyLogin>();
        services.AddHostedService(provider => provider.GetRequiredService<MergeFollowUp>());
        services.AddHttpClient<GitHubClient>((provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptionsMonitor<IdentityOptions>>().CurrentValue.GitHub.TimeoutSeconds);
            // GitHub refuses API calls without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Dalan");
        }).RemoveAllLoggers();

        // The session is checked by the standard JWT handler. Other services only ask ASP.NET Core who the user is.
        // AUTH-22: access keys are a second scheme that only the builder doors ask for.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, AccessKeyHandler>(AccessKeys.Scheme, configureOptions: null);
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SessionTokens>((jwt, sessions) => sessions.Configure(jwt));
        services.AddAuthorization(authorization => authorization.AddPolicy(AccessKeys.SessionOrKeyPolicy, policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AccessKeys.Scheme)
            .RequireAuthenticatedUser()));
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityService(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSessions();
        endpoints.MapChallenge();
        endpoints.MapSmsLogin();
        endpoints.MapEmailLogin();
        endpoints.MapGitHubLogin();
        endpoints.MapPasswordLogin();
        endpoints.MapAccount();
        endpoints.MapDeleteAccount();
        endpoints.MapSessionList();
        endpoints.MapSecondFactor();
        endpoints.MapTrustedDevices();
        endpoints.MapMerge();
        endpoints.MapAccessKeys();
        endpoints.MapAuthorize();
        endpoints.MapTokens();
        endpoints.MapOAuthClients();
        endpoints.MapDevLogin();
        return endpoints;
    }

    /// <summary>OPS-07: applies this service's migrations. The host calls it before the server is ready.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<IdentityDb>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<SuspendedUsers>().LoadAsync(cancellationToken);
    }
}
