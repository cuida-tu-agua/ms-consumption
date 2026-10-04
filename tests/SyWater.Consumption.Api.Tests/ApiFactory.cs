using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SyWater.Consumption.Api.Security;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Infrastructure.Persistence;
using SyWater.Consumption.Infrastructure.Persistence.Entities;

namespace SyWater.Consumption.Api.Tests;

/// <summary>SQLite does not do decimal math nor NEWSEQUENTIALID: only for the tests.</summary>
public sealed class SqliteConsumptionDbContext(DbContextOptions options) : ConsumptionDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder builder) =>
        builder.Properties<decimal>().HaveConversion<double>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ReadingEntity>().Property(r => r.Id).HasDefaultValueSql(null).ValueGeneratedOnAdd();
        modelBuilder.Entity<HourlyEntity>().Property(h => h.Id).HasDefaultValueSql(null).ValueGeneratedOnAdd();
    }
}

public sealed class FakePlaceAccess : IPlaceAccessChecker
{
    public HashSet<Guid> Mine { get; } = [];
    public Task<bool> CanReadAsync(Guid placeId, CancellationToken ct) => Task.FromResult(Mine.Contains(placeId));
}

public sealed class NoRevocations : ITokenRevocationChecker
{
    public HashSet<string> Revoked { get; } = [];
    public Task<bool> IsRevokedAsync(string? tokenId, string? userId, long? issuedAt) =>
        Task.FromResult(tokenId is not null && Revoked.Contains(tokenId));
}

/// <summary>The real API (Program.cs) with SQLite, a test RSA key and fake ms-places / Redis.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://iam.cuidatuagua.local";
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly SqliteConnection _sqlite = new("DataSource=:memory:");
    private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"iam-public-{Guid.NewGuid():N}.pem");

    public FakePlaceAccess Places { get; } = new();
    public NoRevocations Revocations { get; } = new();

    public ApiFactory()
    {
        _sqlite.Open();
        File.WriteAllText(_publicKeyPath, _rsa.ExportSubjectPublicKeyInfoPem());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Consumption", "unused-in-tests");
        builder.UseSetting("Jwt:PublicKeyPath", _publicKeyPath);
        builder.UseSetting("RabbitMq:Host", "");   // no consumer: the tests call the use case directly

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ConsumptionDbContext>();
            services.RemoveAll<DbContextOptions<ConsumptionDbContext>>();
            var options = new DbContextOptionsBuilder<ConsumptionDbContext>().UseSqlite(_sqlite).Options;
            services.AddScoped<ConsumptionDbContext>(_ => new SqliteConsumptionDbContext(options));

            services.RemoveAll<IPlaceAccessChecker>();
            services.AddSingleton<IPlaceAccessChecker>(Places);
            services.RemoveAll<ITokenRevocationChecker>();
            services.AddSingleton<ITokenRevocationChecker>(Revocations);
        });
    }

    public void CreateSchema()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ConsumptionDbContext>().Database.EnsureCreated();
    }

    /// <summary>An access token like the ones ms-iam issues (RS256, same issuer and claims).</summary>
    public string TokenFor(Guid userId, string? tokenId = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["jti"] = tokenId ?? Guid.NewGuid().ToString(),
                ["name"] = "Juan Ome",
            },
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = "iam-key-1" }, SecurityAlgorithms.RsaSha256),
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _sqlite.Dispose();
        File.Delete(_publicKeyPath);
    }
}
