namespace software_elviejomadero.ViewModels
{
    public class NoticeItemViewModel
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public long CreatedAtUnixMs { get; set; }
        public bool IsRead { get; set; }
    }

    public class NoticeSummaryViewModel
    {
        /// <summary>Cantidad de avisos no leídos (número sobre la campana).</summary>
        public int UnreadCount { get; set; }

        /// <summary>Del más reciente al más antiguo.</summary>
        public List<NoticeItemViewModel> Notices { get; set; } = new();

        /// <summary>Hora del servidor (ms Unix) para calcular "hace X min" sin depender del reloj del navegador.</summary>
        public long ServerNowUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}