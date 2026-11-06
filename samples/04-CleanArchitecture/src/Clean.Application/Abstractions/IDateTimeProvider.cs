namespace Clean.Application.Abstractions;

// Saat bile bir "dış bağımlılık"tır: testlerde sabit zaman verebilmek için soyutlanır.
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
