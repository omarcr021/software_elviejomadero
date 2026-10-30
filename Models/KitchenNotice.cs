using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.Models
{
    /// <summary>
    /// HU-13: aviso visual para el repartidor cuando cocina empieza a preparar un pedido de delivery.
    /// Hay un solo aviso por pedido; cada repartidor lo marca como leído por separado (KitchenNoticeRead).
    /// </summary>
    public class KitchenNotice
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        /// <summary>Texto mostrado en "Avisos de cocina" (copia fija al momento de generarse).</summary>
        [Required]
        [MaxLength(300)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<KitchenNoticeRead> Reads { get; set; } = new List<KitchenNoticeRead>();
    }
}