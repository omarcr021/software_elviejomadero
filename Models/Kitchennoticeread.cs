using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.Models
{
    /// <summary>Marca que un usuario (repartidor) ya leyó un aviso.</summary>
    public class KitchenNoticeRead
    {
        public int NoticeId { get; set; }
        public KitchenNotice? Notice { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
    }
}