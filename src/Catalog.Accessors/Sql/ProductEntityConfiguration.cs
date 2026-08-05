using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Accessors.Sql;

internal sealed class ProductEntityConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "Products",
            table => table.HasCheckConstraint("CK_Products_Price_NonNegative", "[Price] >= 0"));
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasMaxLength(128)
            .ValueGeneratedNever();
        builder.Property(product => product.Sku)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(product => product.Sku)
            .IsUnique()
            .HasDatabaseName("UX_Products_Sku");
        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(product => product.Description)
            .HasMaxLength(2000);
        builder.Property(product => product.Price)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(product => product.IsActive)
            .IsRequired();
        builder.Property(product => product.CreatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(product => product.UpdatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();
    }
}
