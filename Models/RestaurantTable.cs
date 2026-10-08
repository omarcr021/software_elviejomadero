using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.Models
{
    public static class TableStatuses
    {
        public const string Free = "Libre";
        public const string Occupied = "Ocupada";
    }

    public class RestaurantTable
    {
        public int Id { get; set; }
        [Required, MaxLength(12)]
        public string Number { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string Status { get; set; } = TableStatuses.Free;
        public bool IsActive { get; set; } = true;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
