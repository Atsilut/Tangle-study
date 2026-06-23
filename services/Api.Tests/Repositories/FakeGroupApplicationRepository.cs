using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeGroupApplicationRepository : IGroupApplicationRepository
{
    private readonly List<GroupApplication> _applications = [];
    private long _nextId = 1;

    public Task CreateApplicationAsync(GroupApplication application)
    {
        typeof(GroupApplication)
            .GetProperty(nameof(GroupApplication.Id))!
            .SetValue(application, _nextId++);
        _applications.Add(application);
        return Task.CompletedTask;
    }

    public Task<GroupApplication?> GetByIdAsync(long id) =>
        Task.FromResult(_applications.FirstOrDefault(a => a.Id == id));

    public Task<GroupApplication?> GetForUserAsync(long groupId, long applicantId) =>
        Task.FromResult(_applications.FirstOrDefault(a =>
            a.GroupId == groupId && a.ApplicantId == applicantId));

    public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId) =>
        Task.FromResult(_applications.FirstOrDefault(a =>
            a.GroupId == groupId && a.ApplicantId == applicantId && a.IsPending));

    public Task<List<GroupApplication>> GetPendingByGroupAsync(long groupId) =>
        Task.FromResult(_applications.Where(a => a.GroupId == groupId && a.IsPending).ToList());

    public Task<List<GroupApplication>> GetIgnoredByGroupAsync(long groupId) =>
        Task.FromResult(_applications.Where(a => a.GroupId == groupId && !a.IsPending).ToList());

    public Task<List<GroupApplication>> GetPendingForApplicantAsync(long applicantId) =>
        Task.FromResult(_applications.Where(a => a.ApplicantId == applicantId && a.IsPending).ToList());

    public Task<List<GroupApplication>> GetIgnoredOutgoingForApplicantAsync(long applicantId) =>
        Task.FromResult(_applications.Where(a => a.ApplicantId == applicantId && !a.IsPending).ToList());

    public Task UpdateApplicationAsync(GroupApplication application) => Task.CompletedTask;

    public Task DeleteApplicationAsync(GroupApplication application)
    {
        _applications.Remove(application);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAndGroupAsync(long groupId, long userId)
    {
        _applications.RemoveAll(a => a.GroupId == groupId && a.ApplicantId == userId);
        return Task.CompletedTask;
    }

    public Task DeleteAllByGroupAsync(long groupId)
    {
        _applications.RemoveAll(a => a.GroupId == groupId);
        return Task.CompletedTask;
    }

    public Task DeleteAllByUserAsync(long userId)
    {
        _applications.RemoveAll(a => a.ApplicantId == userId);
        return Task.CompletedTask;
    }
}
