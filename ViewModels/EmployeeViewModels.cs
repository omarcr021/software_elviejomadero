using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.ViewModels
{
    public class CreateEmployeeViewModel
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
        [Display(Name = "Nombre completo")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener exactamente 8 dígitos numéricos.")]
        [Display(Name = "DNI")]
        public string DNI { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "El teléfono debe tener exactamente 9 dígitos numéricos.")]
        [Display(Name = "Teléfono")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Nombre de usuario")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio.")]
        [Display(Name = "Rol")]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña inicial")]
        public string InitialPassword { get; set; } = "Madero2026*";

        [Display(Name = "Empleado activo")]
        public bool IsActive { get; set; } = true;
    }

    public class EmployeeItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string DNI { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class EmployeeListViewModel
    {
        public List<EmployeeItemViewModel> Employees { get; set; } = new();
        public int TotalCount { get; set; }
        public string? SelectedRole { get; set; }
        public List<string> AvailableRoles { get; set; } = new();
        public CreateEmployeeViewModel NewEmployee { get; set; } = new();
    }
}
