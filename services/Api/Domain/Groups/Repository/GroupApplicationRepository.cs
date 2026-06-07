using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupApplicationRepository : IGroupApplicationRepository
    {
        private readonly AppDbContext _context;

        public GroupApplicationRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task CreateApplicationAsync(GroupApplication application)
        {
            _context.GroupApplications.Add(application);
            return _context.SaveChangesAsync();
        }

        public Task<GroupApplication?> GetByIdAsync(long id) => _context.GroupApplications.FindAsync(id).AsTask();

        public Task<GroupApplication?> GetForUserAsync(long groupId, long applicantId) =>
            _context.GroupApplications.FirstOrDefaultAsync(a =>
                a.GroupId == groupId && a.ApplicantId == applicantId);

        public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId) =>
            _context.GroupApplications.FirstOrDefaultAsync(a =>
                a.GroupId == groupId && a.ApplicantId == applicantId && a.IsPending);

        public Task<List<GroupApplication>> GetPendingByGroupAsync(long groupId) =>
            _context.GroupApplications
                .Where(a => a.GroupId == groupId && a.IsPending)
                .ToListAsync();

        public Task<List<GroupApplication>> GetIgnoredByGroupAsync(long groupId) =>
            _context.GroupApplications
                .Where(a => a.GroupId == groupId && !a.IsPending)
                .ToListAsync();

        public Task<List<GroupApplication>> GetPendingForApplicantAsync(long applicantId) =>
            _context.GroupApplications
                .Where(a => a.ApplicantId == applicantId && a.IsPending)
                .ToListAsync();

        public Task<List<GroupApplication>> GetIgnoredOutgoingForApplicantAsync(long applicantId) =>
            _context.GroupApplications
                .Where(a => a.ApplicantId == applicantId && !a.IsPending)
                .ToListAsync();

        public Task UpdateApplicationAsync(GroupApplication application) => _context.SaveChangesAsync();

        public Task DeleteApplicationAsync(GroupApplication application)
        {
            _context.GroupApplications.Remove(application);
            return _context.SaveChangesAsync();
        }

        public async Task DeleteAllForUserAndGroupAsync(long groupId, long userId)
        {
            var applications = await _context.GroupApplications
                .Where(a => a.GroupId == groupId && a.ApplicantId == userId)
                .ToListAsync();
            if (applications.Count == 0) return;
            _context.GroupApplications.RemoveRange(applications);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByGroupAsync(long groupId)
        {
            var applications = await _context.GroupApplications.Where(a => a.GroupId == groupId).ToListAsync();
            if (applications.Count == 0) return;
            _context.GroupApplications.RemoveRange(applications);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByUserAsync(long userId)
        {
            var applications = await _context.GroupApplications.Where(a => a.ApplicantId == userId).ToListAsync();
            if (applications.Count == 0) return;
            _context.GroupApplications.RemoveRange(applications);
            await _context.SaveChangesAsync();
        }
    }
}
