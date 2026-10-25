using System.Text.Json;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;

public sealed record CheckoutRequest(string RequestKey,string Method,string? City,string? Street,string? Apartment,string? Name,string? Phone,string? Cafe,string Timing,string? ScheduledLocal,string? Comment,string PaymentMethod,bool Consent);
public sealed record OrderLineSnapshot(string ProductId,string Name,string Image,string Portion,string Configuration,string[] Summary,int Quantity,int UnitPrice,int Total);
public sealed record OrderEventView(string Status,string Note,DateTime CreatedUtc);
public sealed record RefundLine(int Index,int Quantity);
public sealed record RefundView(Guid Id,int Amount,int Delivery,string Reason,RefundLine[] Lines,DateTime CreatedUtc);
public sealed record OrderView(Guid Id,string Number,string Status,OrderLineSnapshot[] Items,CheckoutRequest Checkout,int Subtotal,int Delivery,int Total,DateTime CreatedUtc,string PaymentMethod,string Fulfillment="New",int Refunded=0,OrderEventView[]? Events=null,RefundView[]? Refunds=null,string Source="Web",string[]? AllowedTransitions=null,bool Guest=false,string? GuestAccessToken=null);
public sealed record PaymentRequest(string AttemptKey,string Result);
public sealed record TransitionRequest(string Status,string? Reason);
public sealed record RefundRequest(string RequestKey,string Reason,RefundLine[] Lines,bool Delivery);
public sealed record ReceiptRequest(string RequestKey,string Method);
public sealed record ManualOrderRequest(Guid? CustomerId,CheckoutRequest Checkout,CartChange[] Lines);
public interface IPaymentProvider { Task<string> ResolveAsync(string result,CancellationToken ct); }
public sealed class LocalPaymentProvider:IPaymentProvider
{
    public Task<string> ResolveAsync(string result,CancellationToken ct)
    {ct.ThrowIfCancellationRequested();if(result is not("success" or "error" or "cancel"))throw new ShopException("Неизвестный результат оплаты.");return Task.FromResult(result);}
}
public sealed class OrderService(ShopDbContext db,CartService carts,SiteService site,IPaymentProvider payments,TimeProvider clock)
{
    public static OrderLineSnapshot[] Lines(Order o)=>JsonSerializer.Deserialize<OrderLineSnapshot[]>(o.LinesJson,JsonDefaults.Options)??[];
    // StudioV2 added this column with an empty default for older paid orders.
    // Their historic cost is unknown, rather than zero or today's product cost.
    public static int?[] Costs(Order o)=>string.IsNullOrWhiteSpace(o.CostsJson)?[]:JsonSerializer.Deserialize<int?[]>(o.CostsJson,JsonDefaults.Options)??[];
    public static OrderView View(Order o,OrderEventView[]? events=null,RefundView[]? refunds=null)
    {
        var checkout=JsonSerializer.Deserialize<CheckoutRequest>(o.CheckoutJson,JsonDefaults.Options)!;
        return new(o.PublicId,$"MH-{o.Id+1040}",o.Status,Lines(o),checkout,o.Subtotal,o.Delivery,o.Total,o.CreatedUtc,o.PaymentMethod,o.Fulfillment,o.Refunded,events,refunds,o.Source,OrderWorkflow.Next(o,checkout?.Method),o.Source=="Guest");
    }
    public async Task<OrderView> CreateAsync(Guid customer,CheckoutRequest r,CancellationToken ct=default)=>await CreateCoreAsync(customer,"u:"+customer,r,"Web",ct);
    public async Task<OrderView> CreateGuestAsync(Guid visitor,CheckoutRequest r,CancellationToken ct=default)=>await CreateCoreAsync(visitor,"g:"+visitor,r,"Guest",ct);
    private static string PurchaseOwner(Order order)
    {
        if(order.Source!="Guest")return "u:"+order.CustomerId;
        // Signing in may move the cart without attaching the guest order to the account.
        using var snapshot=JsonDocument.Parse(order.CheckoutJson);
        if(snapshot.RootElement.TryGetProperty("_cartOwner",out var stored)&&stored.ValueKind==JsonValueKind.String&&
           stored.GetString() is {} owner&&owner.StartsWith("u:",StringComparison.Ordinal)&&Guid.TryParse(owner[2..],out _))return owner;
        return "g:"+order.CustomerId;
    }
    private async Task<OrderView> CreateCoreAsync(Guid customer,string owner,CheckoutRequest request,string source,CancellationToken ct)
    {
        ValidateKey(request.RequestKey);
        var old=await db.Orders.SingleOrDefaultAsync(x=>x.CustomerId==customer&&x.RequestKey==request.RequestKey,ct);if(old is not null)return View(old);
        var settings=await site.GetAsync(ct);ValidateCheckout(request,settings);FulfillmentPolicy.Validate(request,settings,FulfillmentPolicy.LocalNow(clock));request=request with{Phone="+"+Inputs.Phone(request.Phone),Name=request.Name!.Trim()};
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var cart=await carts.ViewAsync(owner,ct);
        if(cart.Items.Length==0)throw new ShopException("Корзина пуста. Добавьте любимые блюда.",409);
        if(cart.Items.Any(x=>!x.Available))throw new ShopException("Есть недоступные позиции. Измените их перед оформлением.",409);
        var lines=cart.Items.Select(x=>new OrderLineSnapshot(x.Product.Id,x.Product.Name,x.Product.Image,x.Product.Portion,Pricing.Calculate(x.Product,x.Selections).Canonical,x.Summary,x.Quantity,x.UnitPrice,x.Total)).ToArray();
        var costs=new List<int?>();
        foreach(var line in cart.Items)
        {
            var p=await db.Products.FindAsync([line.Product.Id],ct);int? cost=p!.Cost;
            foreach(var group in line.Product.Groups)foreach(var id in line.Selections[group.Id])
            {var option=group.Options.Single(x=>x.Id==id);if(cost.HasValue){if(option.Cost.HasValue)cost+=option.Cost;else if(option.Price!=0)cost=null;}}
            costs.Add(cost);
        }
        var delivery=request.Method=="delivery"?settings.DeliveryPrice:0;
        var order=new Order{CustomerId=customer,RequestKey=request.RequestKey,Subtotal=cart.Subtotal,Delivery=delivery,Total=checked(cart.Subtotal+delivery),LinesJson=JsonSerializer.Serialize(lines,JsonDefaults.Options),CostsJson=JsonSerializer.Serialize(costs,JsonDefaults.Options),CheckoutJson=JsonSerializer.Serialize(request,JsonDefaults.Options),PaymentMethod=request.PaymentMethod,Source=source};
        db.Orders.Add(order);await db.SaveChangesAsync(ct);
        if(PaymentMethods.AtPickup(request.PaymentMethod)){await ClearPurchasedAsync(owner,order,ct);db.OrderEvents.Add(new(){OrderId=order.Id,Status="New",Note="Заказ оформлен. Оплата при получении."});}
        else db.OrderEvents.Add(new(){OrderId=order.Id,Status="Pending",Note="Ожидает оплаты."});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return View(order);
    }
    public async Task<OrderView> GetAsync(Guid customer,Guid id,CancellationToken ct=default)=>await DetailedAsync(await FindAsync(customer,id,ct),ct);
    public async Task<OrderView[]> HistoryAsync(Guid customer,CancellationToken ct=default)=>(await db.Orders.Where(x=>x.CustomerId==customer&&x.Source!=DemoBusiness.ArchivedSource).OrderByDescending(x=>x.CreatedUtc).ThenByDescending(x=>x.Id).ToArrayAsync(ct)).Select(x=>View(x)).ToArray();
    public async Task<OrderView> DetailedAsync(Order o,CancellationToken ct)
    {
        var events=await db.OrderEvents.Where(x=>x.OrderId==o.Id).OrderBy(x=>x.Id).Select(x=>new OrderEventView(x.Status,x.Note,x.CreatedUtc)).ToArrayAsync(ct);
        var refunds=(await db.OrderRefunds.Where(x=>x.OrderId==o.Id).OrderBy(x=>x.CreatedUtc).ToArrayAsync(ct)).Select(x=>new RefundView(x.Id,x.Amount,x.Delivery,x.Reason,JsonSerializer.Deserialize<RefundLine[]>(x.LinesJson,JsonDefaults.Options)??[],x.CreatedUtc)).ToArray();
        return View(o,events,refunds);
    }
    public async Task<OrderView> PayAsync(Guid customer,Guid id,PaymentRequest r,CancellationToken ct=default)
    {
        ValidateKey(r.AttemptKey);var result=await payments.ResolveAsync(r.Result,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);var order=await FindAsync(customer,id,ct);
        if(order.PaidUtc is not null)return View(order);
        if(order.Fulfillment=="Rejected")throw new ShopException("Заказ отклонён. Оплата недоступна.",409);
        if(PaymentMethods.AtPickup(order.PaymentMethod))throw new ShopException("Этот заказ оплачивается при получении.");
        if(await db.PaymentAttempts.AnyAsync(x=>x.OrderId==order.Id&&x.AttemptKey==r.AttemptKey,ct))return View(order);
        db.PaymentAttempts.Add(new(){OrderId=order.Id,AttemptKey=r.AttemptKey,Result=result});order.Revision++;
        if(result=="success"){order.Status="Paid";order.PaidUtc=DateTime.UtcNow;await ClearPurchasedAsync(PurchaseOwner(order),order,ct);db.OrderEvents.Add(new(){OrderId=order.Id,Status="New",Note="Оплата подтверждена. Заказ передан в кафе."});}
        else order.Status=result=="error"?"Failed":"Cancelled";
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return View(order);
    }
    private async Task ClearPurchasedAsync(string owner,Order order,CancellationToken ct)
    {
        var cart=await carts.GetAsync(owner,ct);
        foreach(var purchased in Lines(order)){var line=cart.Lines.FirstOrDefault(x=>x.ProductId==purchased.ProductId&&x.Configuration==purchased.Configuration);if(line is null)continue;if(line.Quantity<=purchased.Quantity)db.CartLines.Remove(line);else line.Quantity-=purchased.Quantity;}cart.Revision++;
    }
    public async Task<OrderView> ChangeMethodAsync(Guid customer,Guid id,string method,CancellationToken ct=default)
    {
        var s=await site.GetAsync(ct);if(!s.PaymentMethods.Contains(method))throw new ShopException("Способ оплаты недоступен.");
        var o=await FindAsync(customer,id,ct);if(o.PaidUtc is not null||PaymentMethods.AtPickup(o.PaymentMethod)||o.Fulfillment=="Rejected")throw new ShopException("Способ оплаты этого заказа уже нельзя изменить.",409);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        o.PaymentMethod=method;o.Status="Pending";o.Revision++;
        if(PaymentMethods.AtPickup(method)){await ClearPurchasedAsync(PurchaseOwner(o),o,ct);db.OrderEvents.Add(new(){OrderId=o.Id,Status="New",Note="Выбрана оплата при получении."});}
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return View(o);
    }
    public async Task<string[]> RepeatAsync(Guid customer,Guid id,CancellationToken ct=default)
    {
        var o=await FindAsync(customer,id,ct);var warnings=new List<string>();await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var line in Lines(o)){try{await carts.AddAsync("u:"+customer,new(line.ProductId,Pricing.Parse(line.Configuration),line.Quantity),ct);}catch(ShopException e){warnings.Add($"{line.Name}: {e.Message}");}}await tx.CommitAsync(ct);return warnings.ToArray();
    }
    public async Task<Order> AdminFindAsync(Guid id,CancellationToken ct)=>await db.Orders.SingleOrDefaultAsync(x=>x.PublicId==id&&x.Source!=DemoBusiness.ArchivedSource,ct)??throw new ShopException("Заказ не найден.",404);
    public async Task TransitionAsync(Guid id,TransitionRequest r,Guid actor,CancellationToken ct)
    {
        var o=await AdminFindAsync(id,ct);if(o.Fulfillment==r.Status)return;
        var checkout=View(o).Checkout;
        var allowed=new[]{"New","Accepted","Preparing","Ready","OutForDelivery","Completed","Rejected"};
        if(!allowed.Contains(r.Status)||o.Fulfillment is "Completed" or "Rejected")throw new ShopException("Заказ завершён либо указан неизвестный статус.",409);
        if(r.Status=="OutForDelivery"&&checkout.Method!="delivery")throw new ShopException("Самовывоз не передают курьеру.",409);
        if(r.Status!="Rejected"&&o.PaidUtc is null&&!PaymentMethods.AtPickup(o.PaymentMethod))throw new ShopException("Сначала дождитесь оплаты заказа.",409);
        if(r.Status=="Completed"&&o.PaidUtc is null)throw new ShopException("Перед завершением отметьте оплату при получении.",409);
        if(!OrderWorkflow.Next(o,checkout.Method).Contains(r.Status))throw new ShopException("Этот переход недоступен. Двигайтесь по этапам заказа без пропусков и возврата назад.",409);
        var reason=r.Status=="Rejected"?Inputs.Required(r.Reason,"причину отказа",300):"";
        o.Fulfillment=r.Status;o.Revision++;db.OrderEvents.Add(new(){OrderId=o.Id,ActorId=actor,Status=r.Status,Note=reason});Audit.Log(db,actor,"order.status",o.PublicId.ToString(),r.Status);await db.SaveChangesAsync(ct);
    }
    public async Task ReceiptAsync(Guid id,ReceiptRequest r,Guid actor,CancellationToken ct)
    {
        ValidateKey(r.RequestKey);var o=await AdminFindAsync(id,ct);if(o.PaidUtc is not null)return;
        if(!PaymentMethods.AtPickup(o.PaymentMethod)||!PaymentMethods.AtPickup(r.Method)||o.Fulfillment=="Rejected")throw new ShopException("Оплату при получении нельзя отметить для этого заказа.",409);
        o.Status="Paid";o.PaidUtc=DateTime.UtcNow;o.PaymentMethod=r.Method;o.Revision++;db.PaymentAttempts.Add(new(){OrderId=o.Id,AttemptKey=r.RequestKey,Result="receipt"});
        db.OrderEvents.Add(new(){OrderId=o.Id,ActorId=actor,Status="Paid",Note="Оплата при получении отмечена сотрудником."});Audit.Log(db,actor,"order.receipt",id.ToString());await db.SaveChangesAsync(ct);
    }
    public async Task RefundAsync(Guid id,RefundRequest r,Guid actor,CancellationToken ct)
    {
        ValidateKey(r.RequestKey);var o=await AdminFindAsync(id,ct);if(await db.OrderRefunds.AnyAsync(x=>x.OrderId==o.Id&&x.RequestKey==r.RequestKey,ct))return;
        var reason=Inputs.Required(r.Reason,"причину возврата",300);
        if(o.PaidUtc is null||r.Lines is null||r.Lines.Any(x=>x is null)||r.Lines.Length>100||r.Lines.Select(x=>x.Index).Distinct().Count()!=r.Lines.Length)throw new ShopException("Проверьте оплаченный заказ и позиции возврата.");
        var lines=Lines(o);var previous=await db.OrderRefunds.Where(x=>x.OrderId==o.Id).ToArrayAsync(ct);
        var returned=previous.SelectMany(x=>JsonSerializer.Deserialize<RefundLine[]>(x.LinesJson,JsonDefaults.Options)??[]).GroupBy(x=>x.Index).ToDictionary(x=>x.Key,x=>x.Sum(y=>y.Quantity));
        var amount=0;
        foreach(var l in r.Lines){if(l.Index<0||l.Index>=lines.Length||l.Quantity<1||l.Quantity+returned.GetValueOrDefault(l.Index)>lines[l.Index].Quantity)throw new ShopException("Количество возврата превышает оставшееся в заказе.");amount=checked(amount+l.Quantity*lines[l.Index].UnitPrice);}
        var delivery=r.Delivery?o.Delivery-previous.Sum(x=>x.Delivery):0;amount+=delivery;
        if(amount<=0||amount>o.Total-o.Refunded)throw new ShopException("Сумма возврата должна быть в пределах оплаченного остатка.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        db.OrderRefunds.Add(new(){OrderId=o.Id,ActorId=actor,RequestKey=r.RequestKey,Reason=reason,Amount=amount,Delivery=delivery,LinesJson=JsonSerializer.Serialize(r.Lines,JsonDefaults.Options)});
        o.Refunded+=amount;o.Status=o.Refunded==o.Total?"Refunded":"PartiallyRefunded";o.Revision++;
        db.OrderEvents.Add(new(){OrderId=o.Id,ActorId=actor,Status="Refund",Note=$"Возврат {amount} ₽: {reason}"});Audit.Log(db,actor,"order.refund",id.ToString(),amount.ToString());
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task<OrderView> ManualAsync(ManualOrderRequest r,Guid actor,CancellationToken ct)
    {
        if(r.Checkout is null||r.Lines is null||r.Lines.Any(x=>x is null))throw new ShopException("Заполните заказ и добавьте товары.");
        ValidateKey(r.Checkout.RequestKey);var customer=r.CustomerId??Guid.Empty;
        if(r.CustomerId.HasValue&&!await db.Customers.AnyAsync(x=>x.Id==customer&&x.Role=="Customer"&&!x.Blocked,ct))throw new ShopException("Выбранный клиент недоступен.");
        var old=await db.Orders.SingleOrDefaultAsync(x=>x.CustomerId==customer&&x.RequestKey==r.Checkout.RequestKey,ct);if(old is not null)return View(old);
        if(r.Lines.Length is <1 or >50)throw new ShopException("Добавьте от 1 до 50 позиций.");
        var owner="staff:"+actor+":"+r.Checkout.RequestKey;await carts.RemoveAsync(owner,null,ct);foreach(var line in r.Lines)await carts.AddAsync(owner,line,ct);
        var result=await CreateCoreAsync(customer,owner,r.Checkout,"Staff",ct);Audit.Log(db,actor,"order.manual",result.Id.ToString());await db.SaveChangesAsync(ct);return result;
    }
    private async Task<Order> FindAsync(Guid customer,Guid id,CancellationToken ct)=>await db.Orders.SingleOrDefaultAsync(x=>x.PublicId==id&&x.CustomerId==customer&&x.Source!=DemoBusiness.ArchivedSource,ct)??throw new ShopException("Заказ не найден.",404);
    public static void ValidateKey(string? key){if(!Guid.TryParse(key,out _))throw new ShopException("Обновите страницу и повторите действие.");}
    private static void ValidateCheckout(CheckoutRequest r,CafeSettings s)
    {
        Inputs.Required(r.Name,"имя",80,"name");Inputs.Phone(r.Phone);
        if(r.Method is not("pickup" or "delivery"))throw new ShopException("Выберите способ получения.",field:"method");
        if(r.Method=="pickup"&&r.Cafe is not("demo-house" or "mellow-house"))throw new ShopException("Выберите кафе Mellow House.",field:"cafe");
        if(r.Method=="delivery"){Inputs.Required(r.City,"город",80,"city");if(r.City!.Trim().Equals("вне зоны",StringComparison.OrdinalIgnoreCase))throw new ShopException("Адрес вне зоны доставки. Выберите самовывоз.",field:"city");Inputs.Required(r.Street,"улицу и дом",160,"street");if(r.Apartment?.Length>30)throw new ShopException("Номер квартиры слишком длинный.",field:"apartment");}
        if(r.Timing is not("asap" or "scheduled"))throw new ShopException("Выберите время.",field:"timing");
        if(r.Comment?.Length>500)throw new ShopException("Комментарий должен быть короче 500 символов.",field:"comment");
        if(!s.PaymentMethods.Contains(r.PaymentMethod))throw new ShopException("Выберите доступный способ оплаты.",field:"paymentMethod");
        if(!r.Consent)throw new ShopException("Примите условия оформления заказа.",field:"consent");
    }
}

