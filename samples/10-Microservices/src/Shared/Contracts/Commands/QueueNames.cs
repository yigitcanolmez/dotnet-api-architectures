// [Paylaşılan Sözleşme — Adresleme] Command'ların gideceği kuyruk adları da sözleşmenin parçasıdır.
// Gönderen (saga) alıcının hangi servis/makine olduğunu bilmez; sadece mantıksal kuyruk adını bilir.
namespace Contracts.Commands;

public static class QueueNames
{
    public const string ReserveStock = "reserve-stock";
    public const string ReleaseStock = "release-stock";
    public const string ProcessPayment = "process-payment";

    public static Uri For(string queue) => new($"queue:{queue}");
}
