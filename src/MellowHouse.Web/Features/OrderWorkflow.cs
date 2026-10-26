using MellowHouse.Web.Data;
namespace MellowHouse.Web.Features;

public static class OrderWorkflow
{
    public static string Label(string stage,string? method)=>stage switch
    {
        "New"=>"Заказ получен","Accepted"=>"Принят","Preparing"=>"Готовится",
        "Ready"=>method=="delivery"?"Готов к отправке":"Готов к выдаче",
        "OutForDelivery"=>"У курьера","Completed"=>method=="delivery"?"Доставлен":"Выдан",
        "Rejected"=>"Отклонён","Cancelled"=>"Отменён клиентом",_=>stage
    };
    public static string[] Next(Order order,string? method)
    {
        if(order.Fulfillment is "Completed" or "Rejected" or "Cancelled")return [];
        if(order.Status=="Refunded"||order.PaidUtc is null&&!PaymentMethods.AtPickup(order.PaymentMethod))return ["Rejected"];
        var next=order.Fulfillment switch
        {
            "New"=>"Accepted","Accepted"=>"Preparing","Preparing"=>"Ready",
            "Ready" when method=="delivery"=>"OutForDelivery",
            "Ready" when order.PaidUtc is not null=>"Completed",
            "OutForDelivery" when method=="delivery"&&order.PaidUtc is not null=>"Completed",_=>null
        };
        return next is null?["Rejected"]:[next,"Rejected"];
    }
}
