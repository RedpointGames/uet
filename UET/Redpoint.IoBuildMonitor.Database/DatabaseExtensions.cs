namespace Io.Database
{
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading.Tasks;

    public static class DatabaseExtensions
    {
        public static async Task<T?> FindWithIncludeAsync<T>(this DbSet<T> set, Expression<Func<T, bool>> idClause, Func<DbSet<T>, IQueryable<T>> attachIncludes) where T : class
        {
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(idClause);
            ArgumentNullException.ThrowIfNull(attachIncludes);

            var entity = await attachIncludes(set).FirstOrDefaultAsync(idClause);
            if (entity == null)
            {
                entity = set.Local.FirstOrDefault(idClause.Compile());
            }
            return entity;
        }
    }
}
