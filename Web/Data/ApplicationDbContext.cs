using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NhaTot.Models;

namespace NhaTot.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();
    public DbSet<AgentApplication> AgentApplications => Set<AgentApplication>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<PropertyType> PropertyTypes => Set<PropertyType>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<PropertyAmenity> PropertyAmenities => Set<PropertyAmenity>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<ModerationRecord> ModerationRecords => Set<ModerationRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FullName).HasMaxLength(120);
            entity.HasOne(user => user.AgentProfile).WithOne(profile => profile.User)
                .HasForeignKey<AgentProfile>(profile => profile.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<AgentApplication>(entity =>
        {
            entity.HasIndex(application => application.UserId).HasFilter("[Status] = 0").IsUnique();
            entity.HasOne(application => application.User).WithMany(user => user.AgentApplications)
                .HasForeignKey(application => application.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(application => application.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Province>(entity =>
        {
            entity.HasIndex(province => province.Code).IsUnique(); entity.HasIndex(province => province.Name).IsUnique();
            entity.HasData(new Province { Id = 1, Code = "HCM", Name = "Thành phố Hồ Chí Minh" }, new Province { Id = 2, Code = "HN", Name = "Hà Nội" }, new Province { Id = 3, Code = "DN", Name = "Đà Nẵng" });
        });
        builder.Entity<District>(entity =>
        {
            entity.HasIndex(district => new { district.ProvinceId, district.Code }).IsUnique();
            entity.HasOne(district => district.Province).WithMany(province => province.Districts).HasForeignKey(district => district.ProvinceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasData(new District { Id = 1, ProvinceId = 1, Code = "Q1", Name = "Quận 1" }, new District { Id = 2, ProvinceId = 2, Code = "CG", Name = "Cầu Giấy" }, new District { Id = 3, ProvinceId = 3, Code = "HC", Name = "Hải Châu" });
        });
        builder.Entity<PropertyType>(entity =>
        {
            entity.HasIndex(type => type.Slug).IsUnique();
            entity.HasData(new PropertyType { Id = 1, Name = "Căn hộ", Slug = "can-ho", IsActive = true }, new PropertyType { Id = 2, Name = "Nhà riêng", Slug = "nha-rieng", IsActive = true }, new PropertyType { Id = 3, Name = "Đất nền", Slug = "dat-nen", IsActive = true }, new PropertyType { Id = 4, Name = "Văn phòng", Slug = "van-phong", IsActive = true });
        });
        builder.Entity<Property>(entity =>
        {
            entity.Property(property => property.Price).HasPrecision(18, 2); entity.Property(property => property.AreaSquareMeters).HasPrecision(12, 2);
            entity.HasIndex(property => property.Slug).IsUnique(); entity.HasIndex(property => new { property.Status, property.Purpose, property.ProvinceId, property.DistrictId }); entity.HasIndex(property => new { property.Status, property.Price });
            entity.HasOne(property => property.AgentUser).WithMany(user => user.Properties).HasForeignKey(property => property.AgentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(property => property.PropertyType).WithMany(type => type.Properties).HasForeignKey(property => property.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(property => property.Province).WithMany(province => province.Properties).HasForeignKey(property => property.ProvinceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(property => property.District).WithMany(district => district.Properties).HasForeignKey(property => property.DistrictId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(property => property.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PropertyImage>(entity => { entity.HasIndex(image => new { image.PropertyId, image.DisplayOrder }); entity.HasOne(image => image.Property).WithMany(property => property.Images).HasForeignKey(image => image.PropertyId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<Amenity>(entity => { entity.HasIndex(amenity => amenity.Slug).IsUnique(); entity.HasData(new Amenity { Id = 1, Name = "Ban công", Slug = "ban-cong" }, new Amenity { Id = 2, Name = "Chỗ đỗ xe", Slug = "cho-do-xe" }, new Amenity { Id = 3, Name = "Bảo vệ", Slug = "bao-ve" }); });
        builder.Entity<PropertyAmenity>(entity => { entity.HasKey(item => new { item.PropertyId, item.AmenityId }); entity.HasOne(item => item.Property).WithMany(property => property.PropertyAmenities).HasForeignKey(item => item.PropertyId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(item => item.Amenity).WithMany(amenity => amenity.PropertyAmenities).HasForeignKey(item => item.AmenityId).OnDelete(DeleteBehavior.Restrict); });
        builder.Entity<Favorite>(entity => { entity.HasIndex(favorite => new { favorite.UserId, favorite.PropertyId }).IsUnique(); entity.HasOne(favorite => favorite.User).WithMany(user => user.Favorites).HasForeignKey(favorite => favorite.UserId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(favorite => favorite.Property).WithMany(property => property.Favorites).HasForeignKey(favorite => favorite.PropertyId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<ContactRequest>(entity => { entity.HasIndex(request => new { request.PropertyId, request.Status, request.CreatedAtUtc }); entity.HasOne(request => request.RequesterUser).WithMany(user => user.ContactRequests).HasForeignKey(request => request.RequesterUserId).OnDelete(DeleteBehavior.Restrict); entity.HasOne(request => request.Property).WithMany(property => property.ContactRequests).HasForeignKey(request => request.PropertyId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<ModerationRecord>(entity => { entity.HasIndex(record => new { record.PropertyId, record.CreatedAtUtc }); entity.HasOne(record => record.Property).WithMany(property => property.ModerationRecords).HasForeignKey(record => record.PropertyId).OnDelete(DeleteBehavior.Cascade); entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(record => record.ModeratorUserId).OnDelete(DeleteBehavior.Restrict); });
    }
}
