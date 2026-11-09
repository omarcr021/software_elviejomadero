using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IReportService
    {
        Task<ReportViewModel> GetSalesReportAsync(ReportFilterInputModel filter);
    }
}
