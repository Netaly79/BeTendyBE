using BeTendyBE.Domain;

namespace BeTendyBE.Contracts;

public record ProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Phone = "",
    string? AvatarUrl = null,
    bool IsMaster = false,
    MasterProfileResponse? Master = null // добавлено: блок мастерского профиля
);

public record UpdateClientProfileRequest(string FirstName, string LastName, string Phone);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// master DTO block
public record MasterProfileResponse(string? About, List<string> Skills, int? YearsExperience, string? Address = null, List<ServiceListItemResponse>? Services = null);
public record UpsertMasterProfileRequest(string? About, List<string> Skills, int? ExperienceYears, string? Address = null);


public static class ClientProfileMapping
{
    public static ProfileResponse ToDto(User u)
    {
        var masterBlock = u.IsMaster && u.Master is not null
            ? new MasterProfileResponse(
                u.Master.About,
                u.Master.Skills,
                u.Master.ExperienceYears,
                u.Master.Address,
                u.Master.Services
                    .OrderBy(s => s.Name)
                    .Select(s => new ServiceListItemResponse
                    {
                      Name = s.Name,
                      Price = s.Price,
                      DurationMinutes = s.DurationMinutes,
                      Description = s.Description,
                    })
                    .ToList()

              )
            : null;
    
        return new ProfileResponse(
            Id: u.Id,
            Email: u.Email,
            FirstName: u.FirstName,
            LastName: u.LastName,
            Phone: u.Phone ?? "",
            AvatarUrl: u.AvatarUrl,
            IsMaster: u.IsMaster,
            Master: masterBlock
        );
    }
}
