using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.People.Persistence;

internal sealed class PeopleDbContext(DbContextOptions<PeopleDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "people";

    public override string Schema => SchemaName;

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
}

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id).ValueGeneratedNever();
        builder.Property(employee => employee.JobTitle).HasMaxLength(100);
        builder.Property(employee => employee.Department).HasMaxLength(100);
        builder.Property(employee => employee.Phone).HasMaxLength(30);
        builder.Property(employee => employee.HourlyCost).HasPrecision(18, 2);
        builder.HasIndex(employee => new { employee.TenantId, employee.UserId }).IsUnique();
    }
}

internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(request => request.Days).HasPrecision(5, 1);
        builder.Property(request => request.Note).HasMaxLength(LeaveRequest.NoteMaxLength);
        builder.Property(request => request.DecisionNote).HasMaxLength(LeaveRequest.NoteMaxLength);
        builder.HasIndex(request => new { request.TenantId, request.UserId, request.StartDate });
        builder.HasIndex(request => new { request.TenantId, request.Status, request.StartDate });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class PeopleDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PeopleDbContext>
{
    public PeopleDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PeopleDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, PeopleDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PeopleDbContext(options, new TenantContext());
    }
}
