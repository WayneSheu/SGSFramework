using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Infrastructure.Repositories
{
    public sealed class LaboratoryRepository : ILaboratoryRepository
    {
        private readonly ORGDbContext _context;

        public LaboratoryRepository(ORGDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Organization?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Organizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<Organization?> GetTreeByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .Include(x => x.Children)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Organizations.AsNoTracking().ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Organization laboratory, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(laboratory, nameof(laboratory));
            await _context.Organizations.AddAsync(laboratory, cancellationToken);
        }

        public void Update(Organization laboratory)
        {
            ArgumentNullException.ThrowIfNull(laboratory, nameof(laboratory));
            _context.Organizations.Update(laboratory);
        }

        public void Delete(Organization laboratory)
        {
            ArgumentNullException.ThrowIfNull(laboratory, nameof(laboratory));
            _context.Organizations.Remove(laboratory);
        }

        public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken) > 0;
        }
    }
}
