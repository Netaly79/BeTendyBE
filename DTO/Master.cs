namespace BeTendyBE.DTO;
public sealed class MasterResponse
{
  public Guid Id { get; init; }          // Id записи мастера
  public Guid UserId { get; init; }      // Id пользователя

  public string FullName { get; init; } = string.Empty; // Имя + Фамилия
  public string? About { get; init; }                    // О себе

  public List<string> Skills { get; init; } = new();

  // Минимальный адрес (расширим позже, если нужно)
  public string? Address { get; init; }

  // Немного “витринных” полей (опционально, если есть в модели)
  //public double? Rating { get; init; }       // средняя оценка
  //public int ReviewsCount { get; init; }     // кол-во отзывов
  public string? AvatarUrl { get; init; }    // аватар из профиля
}