using software_elviejomadero.Models;

namespace software_elviejomadero.ViewModels
{
    public class KitchenBoardViewModel
    {
        public List<KitchenOrderViewModel> NewOrders { get; set; } = new();
        public List<KitchenOrderViewModel> InPreparationOrders { get; set; } = new();
        public List<KitchenOrderViewModel> ReadyOrders { get; set; } = new();

        /// <summary>Hora del servidor (ms Unix) para que el contador del navegador no dependa del reloj del cliente.</summary>
        public long ServerNowUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// HU-10: ordena "Nuevos" y "En preparación" por prioridad.
        /// 1) Mayor tiempo de espera primero (minutos transcurridos desde que se envió a cocina).
        /// 2) Si el tiempo de espera es igual, el que se envió antes (hora de envío exacta).
        /// 3) Si aún empatan, el menor Id (orden de registro).
        /// "Listos" no se reordena.
        /// </summary>
        public void ApplyPriorityOrder()
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            NewOrders = Prioritize(NewOrders, nowMs);
            InPreparationOrders = Prioritize(InPreparationOrders, nowMs);
        }

        public static List<KitchenOrderViewModel> Prioritize(IEnumerable<KitchenOrderViewModel> orders, long nowMs)
        {
            return orders
                .OrderByDescending(o => o.WaitMinutes(nowMs))
                .ThenBy(o => o.CreatedAt)
                .ThenBy(o => o.Id)
                .ToList();
        }
    }

    public class KitchenOrderItemViewModel
    {
        public string DishName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class KitchenOrderViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public OrderType Type { get; set; }
        public int? TableNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? Note { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<KitchenOrderItemViewModel> Items { get; set; } = new();

        public long CreatedAtUnixMs =>
            new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        /// <summary>Minutos completos de espera desde que se envió a cocina (HU-10).</summary>
        public int WaitMinutes(long nowMs) => (int)Math.Max(0, (nowMs - CreatedAtUnixMs) / 60000);

        /// <summary>"Mesa 4", "Para llevar" o "Delivery".</summary>
        public string OriginLabel => Type switch
        {
            OrderType.Mesa => TableNumber.HasValue ? $"Mesa {TableNumber}" : "Mesa",
            OrderType.ParaLlevar => "Para llevar",
            OrderType.Delivery => "Delivery",
            _ => "Pedido"
        };

        /// <summary>Estado "listo" según el tipo de pedido.</summary>
        public string ReadyStatusLabel => Type switch
        {
            OrderType.Mesa => "Listo para servir",
            OrderType.ParaLlevar => "Listo para recoger",
            OrderType.Delivery => "Listo para reparto",
            _ => "Listo"
        };

        /// <summary>Responsable de entregar el pedido una vez listo.</summary>
        public string DeliveryResponsible => Type switch
        {
            OrderType.Mesa => "Mozo",
            OrderType.ParaLlevar => "Recepcionista",
            OrderType.Delivery => "Repartidor",
            _ => "Personal de atención"
        };

        public string ElapsedText => FormatElapsed(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - CreatedAtUnixMs);

        // Misma lógica que formatElapsed() en Index.cshtml
        public static string FormatElapsed(long elapsedMs)
        {
            var totalMinutes = (int)Math.Max(0, elapsedMs / 60000);
            if (totalMinutes < 1) return "recién";
            if (totalMinutes < 60) return $"hace {totalMinutes} min";
            return $"hace {totalMinutes / 60} h {totalMinutes % 60:00} min";
        }
    }
}