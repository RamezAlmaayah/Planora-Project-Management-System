using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Planora.Web.Security;

// A separate operational store keeps the application model and its migrations unchanged.
public sealed class DataProtectionKeyDbContext(DbContextOptions<DataProtectionKeyDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
