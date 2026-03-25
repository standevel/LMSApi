using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Data.Repositories;

public abstract class BaseRepository<TEntity>(LmsDbContext dbContext) : IBaseRepository<TEntity>
    where TEntity : class
{
    protected readonly LmsDbContext DbContext = dbContext;
    protected readonly DbSet<TEntity> DbSet = dbContext.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet.ToListAsync(ct);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        var entry = DbContext.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            // Try to find if the entity is already being tracked by ID
            var primaryKey = DbContext.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey();
            if (primaryKey != null)
            {
                var keyValues = primaryKey.Properties
                    .Select(p => entry.Property(p.Name).CurrentValue)
                    .ToArray();

                var existing = DbSet.Local.FirstOrDefault(e =>
                {
                    var eEntry = DbContext.Entry(e);
                    return primaryKey.Properties.All(p =>
                        Equals(eEntry.Property(p.Name).CurrentValue, entry.Property(p.Name).CurrentValue));
                });

                if (existing != null)
                {
                    DbContext.Entry(existing).CurrentValues.SetValues(entity);
                    return Task.CompletedTask;
                }
            }

            DbSet.Update(entity);
        }
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await DbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync(ct);
                if (databaseValues == null)
                {
                    // Entity was deleted by someone else, so we detach it from our tracker
                    entry.State = EntityState.Detached;
                }
                else
                {
                    // Entity was modified by someone else, refresh original values to match DB 
                    // and allow our current changes to be applied on top (Client Wins)
                    entry.OriginalValues.SetValues(databaseValues);
                }
            }

            // Retry the operation once
            await DbContext.SaveChangesAsync(ct);
        }
    }
}
