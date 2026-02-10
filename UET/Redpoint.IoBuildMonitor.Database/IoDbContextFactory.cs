using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Diagnostics.CodeAnalysis;

namespace Io.Database
{
    [RequiresUnreferencedCode("EF Core is not compatible with trimming.")]
    public class IoDbContextFactory : IDesignTimeDbContextFactory<IoDbContext>
    {
        public IoDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IoDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=io;Username=postgres;Password=postgres", IoDbContextExtensions.ConfigureNpgsql);
            return new IoDbContext(optionsBuilder.Options);
        }
    }
}
