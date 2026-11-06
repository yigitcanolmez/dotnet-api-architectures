namespace Layered.DataAccess.Entities;

// Durum enum'u bile DataAccess'te yaşıyor; Business ve Api buna mecburen bağımlı.
public enum OrderStatus
{
    Placed,
    Shipped,
    Cancelled
}
