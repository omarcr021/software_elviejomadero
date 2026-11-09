using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IPdfReportGenerator
    {
        byte[] GenerateSalesReportPdf(ReportViewModel model);
    }
}
