using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Accessors.Sql;

internal sealed class CategoryEntityConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
    public void Configure(EntityTypeBuilder<CategoryEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasMaxLength(128)
            .ValueGeneratedNever();
        builder.Property(category => category.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(category => category.NormalizedName)
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(category => category.NormalizedName)
            .IsUnique()
            .HasDatabaseName("UX_Categories_NormalizedName");
        builder.Property(category => category.Description)
            .HasMaxLength(2000);
        builder.Property(category => category.CreatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(category => category.UpdatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();
    }
}
