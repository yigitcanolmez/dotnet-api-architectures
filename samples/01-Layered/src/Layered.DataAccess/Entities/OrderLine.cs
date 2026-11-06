namespace Layered.DataAccess.Entities;

// EF'in istediği şekil: kendi Id'si, FK (OrderId) ve geri navigation (Order).
public class OrderLine
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
