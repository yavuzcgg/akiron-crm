using System.Text.Json;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Crm.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Crm.Persistence;

internal sealed class CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "crm";

    public override string Schema => SchemaName;

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyContact> Contacts => Set<PartyContact>();

    public DbSet<CustomField> CustomFields => Set<CustomField>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<LeadForm> LeadForms => Set<LeadForm>();
}

internal static class CrmConstraints
{
    public const string PartyCodeUnique = "ix_parties_tenant_id_code";
    public const string CustomFieldKeyUnique = "ix_custom_fields_tenant_id_key";
}

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads");
        builder.HasKey(lead => lead.Id);
        builder.Property(lead => lead.Id).ValueGeneratedNever();
        builder.Property(lead => lead.Title).HasMaxLength(Lead.TitleMaxLength);
        builder.Property(lead => lead.CompanyName).HasMaxLength(200);
        builder.Property(lead => lead.ContactName).HasMaxLength(200);
        builder.Property(lead => lead.Email).HasMaxLength(254);
        builder.Property(lead => lead.Phone).HasMaxLength(30);
        builder.Property(lead => lead.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(lead => lead.Stage).HasConversion<string>().HasMaxLength(20);
        builder.Property(lead => lead.EstimatedValue).HasPrecision(18, 2);
        builder.Property(lead => lead.Currency).HasConversion(currency => currency.Code, code => Akiron.BuildingBlocks.Domain.Currency.From(code)).HasMaxLength(3);
        builder.Property(lead => lead.OwnerName).HasMaxLength(200);
        builder.Property(lead => lead.NextStep).HasMaxLength(300);
        builder.Property(lead => lead.Notes).HasMaxLength(Lead.NotesMaxLength);
        builder.Property(lead => lead.LostReason).HasMaxLength(500);
        builder.Ignore(lead => lead.IsOpen);
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
        builder.HasIndex(lead => new { lead.TenantId, lead.Stage, lead.Rank });
        builder.HasIndex(lead => new { lead.TenantId, lead.PartyId });
        builder.HasIndex(lead => new { lead.NextStepDate, lead.FollowUpSentFor });
    }
}

internal sealed class LeadFormConfiguration : IEntityTypeConfiguration<LeadForm>
{
    public void Configure(EntityTypeBuilder<LeadForm> builder)
    {
        builder.ToTable("lead_forms");
        builder.HasKey(form => form.Id);
        builder.Property(form => form.Id).ValueGeneratedNever();
        builder.Property(form => form.Key).HasMaxLength(64);
        builder.Property(form => form.RedirectUrl).HasMaxLength(500);
        builder.Property(form => form.OwnerName).HasMaxLength(200);

        // Found by key across tenants (the public form); one form per tenant.
        builder.HasIndex(form => form.Key).IsUnique();
        builder.HasIndex(form => form.TenantId).IsUnique();
    }
}

internal sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");
        builder.HasKey(party => party.Id);
        builder.Property(party => party.Id).ValueGeneratedNever();
        builder.Property(party => party.Code).HasMaxLength(Party.CodeMaxLength);
        builder.Property(party => party.Kind).HasConversion<string>().HasMaxLength(20);
        // Sorted the Turkish way: Çelik after Cengiz, İnci next to Irmak, not after Z.
        builder.Property(party => party.Name).HasMaxLength(Party.NameMaxLength).UseCollation("tr-x-icu");
        builder.Property(party => party.TaxNumber).HasMaxLength(11);
        builder.Property(party => party.TaxOffice).HasMaxLength(100);
        builder.Property(party => party.Email).HasMaxLength(254);
        builder.Property(party => party.Phone).HasMaxLength(30);
        builder.Property(party => party.Website).HasMaxLength(200);
        builder.Property(party => party.City).HasMaxLength(100);
        builder.Property(party => party.District).HasMaxLength(100);
        builder.Property(party => party.AddressLine).HasMaxLength(500);
        builder.Property(party => party.SearchText).HasMaxLength(1200);

        // A small key → value map; jsonb keeps it queryable later (filters on custom fields).
        builder.Property(party => party.CustomValues)
            .HasColumnType("jsonb")
            .HasConversion(
                values => JsonSerializer.Serialize(values, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonSerializerOptions.Default) ?? new Dictionary<string, string>(),
                new ValueComparer<IReadOnlyDictionary<string, string>>(
                    (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
                    values => values.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    values => new Dictionary<string, string>(values)));

        // Archived parties free their code: a new card may reuse it.
        builder.HasIndex(party => new { party.TenantId, party.Code })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(CrmConstraints.PartyCodeUnique);
        builder.HasIndex(party => new { party.TenantId, party.Name });
        builder.HasIndex(party => new { party.TenantId, party.TaxNumber });
    }
}

internal sealed class PartyContactConfiguration : IEntityTypeConfiguration<PartyContact>
{
    public void Configure(EntityTypeBuilder<PartyContact> builder)
    {
        builder.ToTable("party_contacts");
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id).ValueGeneratedNever();
        builder.Property(contact => contact.FullName).HasMaxLength(PartyContact.FullNameMaxLength);
        builder.Property(contact => contact.Title).HasMaxLength(100);
        builder.Property(contact => contact.Email).HasMaxLength(254);
        builder.Property(contact => contact.Phone).HasMaxLength(30);
        builder.HasOne<Party>().WithMany().HasForeignKey(contact => contact.PartyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(contact => new { contact.TenantId, contact.PartyId });
    }
}

internal sealed class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.ToTable("custom_fields");
        builder.HasKey(field => field.Id);
        builder.Property(field => field.Id).ValueGeneratedNever();
        builder.Property(field => field.Key).HasMaxLength(40);
        builder.Property(field => field.Label).HasMaxLength(CustomField.LabelMaxLength);
        builder.Property(field => field.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(field => field.Options).HasColumnType("text[]");

        // A removed field keeps its key: old values stay attached to it, and a new field cannot reuse it.
        builder.HasIndex(field => new { field.TenantId, field.Key }).IsUnique().HasDatabaseName(CrmConstraints.CustomFieldKeyUnique);
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class CrmDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, CrmDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CrmDbContext(options, new TenantContext());
    }
}
