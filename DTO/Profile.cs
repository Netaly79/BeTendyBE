using BeTendyBE.Domain;

namespace BeTendyBE.Contracts;

public record ProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Phone = "",
    string? AvatarUrl = null,
    UserRole Role = UserRole.Client,
    MasterProfileResponse? Master = null // добавлено: блок мастерского профиля
);

public record UpdateClientProfileRequest(string FirstName, string LastName, string Phone);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// master DTO block
public record MasterProfileResponse(string? About, string? Skills, int? YearsExperience, string? Address = null);
public record UpsertMasterProfileRequest(string? About, string? Skills, int? ExperienceYears, string? Address = null);


public static class ClientProfileMapping
{
    public static ProfileResponse ToDto(User u)
    {
        var masterBlock = u.Role == UserRole.Master && u.Master is not null
            ? new MasterProfileResponse(
                u.Master.About,
                u.Master.Skills,
                u.Master.ExperienceYears,
                u.Master.Address
              )
            : null;
    
        return new ProfileResponse(
            Id: u.Id,
            Email: u.Email,
            FirstName: u.FirstName,
            LastName: u.LastName,
            Phone: u.Phone ?? "",
            AvatarUrl: u.AvatarUrl,
            Role: u.Role,
            Master: masterBlock
        );
    }
}
