using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Social.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Social.Persistence;

internal sealed class SocialDbContext(DbContextOptions<SocialDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "social";

    public override string Schema => SchemaName;

    public DbSet<ContentPost> Posts => Set<ContentPost>();
}

internal sealed class ContentPostConfiguration : IEntityTypeConfiguration<ContentPost>
{
    public void Configure(EntityTypeBuilder<ContentPost> builder)
    {
        builder.ToTable("content_posts");
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Id).ValueGeneratedNever();
        builder.Property(post => post.PartyName).HasMaxLength(250);
        builder.Property(post => post.Title).HasMaxLength(ContentPost.TitleMaxLength);
        builder.Property(post => post.Caption).HasMaxLength(ContentPost.CaptionMaxLength);
        builder.Property(post => post.Hashtags).HasMaxLength(ContentPost.HashtagsMaxLength);
        builder.Property(post => post.PlatformCodes).HasMaxLength(100);
        builder.Property(post => post.Format).HasMaxLength(20);
        builder.Property(post => post.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(post => post.ClientNote).HasMaxLength(ContentPost.NoteMaxLength);
        builder.Property(post => post.ReviewNote).HasMaxLength(ContentPost.NoteMaxLength);
        builder.Property(post => post.DecidedByName).HasMaxLength(200);
        builder.Property(post => post.PublishedUrl).HasMaxLength(500);
        builder.Property(post => post.OwnerName).HasMaxLength(200);
        builder.Ignore(post => post.Platforms);
        builder.Ignore(post => post.IsVisibleToClient);
        builder.Ignore(post => post.IsEditable);

        // A client answering while someone on the team edits: the later save loses and is told so.
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
        builder.HasIndex(post => new { post.TenantId, post.PlannedDate });
        builder.HasIndex(post => new { post.TenantId, post.PartyId, post.PlannedDate });
        builder.HasIndex(post => new { post.Status, post.PlannedDate });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class SocialDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SocialDbContext>
{
    public SocialDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SocialDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, SocialDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SocialDbContext(options, new TenantContext());
    }
}
