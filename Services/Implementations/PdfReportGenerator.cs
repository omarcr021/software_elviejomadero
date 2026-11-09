using System.Globalization;
using System.Text;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public enum KpiIconType
    {
        Clipboard,
        TrendingUp,
        People
    }

    // HU-20: Exportar Reportes a PDF (Rol: Administrador)
    public class PdfReportGenerator : IPdfReportGenerator
    {
        public byte[] GenerateSalesReportPdf(ReportViewModel model)
        {
            var pdfDoc = new SimplePdfDocument();
            var page = pdfDoc.NewPage();

            double pageHeight = 841.89; // A4
            double pageWidth = 595.28;
            double margin = 40;
            double contentWidth = pageWidth - (margin * 2);
            double y = pageHeight - margin;

            // --- 1. ENCABEZADO (CA-02: Nombre del restaurante, fecha/hora, rango y filtros) ---
            page.DrawText("EL VIEJO MADERO", margin, y - 18, 18, true, "#24160e");

            // Mini icono vectorial de gráfico de barras en el subtítulo (como en la web)
            page.DrawLine(margin, y - 32, margin, y - 27, 2.0, "#cf6b29");
            page.DrawLine(margin + 3.5, y - 32, margin + 3.5, y - 23, 2.0, "#cf6b29");
            page.DrawLine(margin + 7.0, y - 32, margin + 7.0, y - 28, 2.0, "#cf6b29");
            page.DrawText("Reporte de Ventas y Pedidos", margin + 13, y - 34, 12, true, "#cf6b29");
            y -= 45;

            // Caja de metadatos de auditoría
            page.DrawRect(margin, y - 48, contentWidth, 48, "#fbf7f2", "#e8dec8", 1);
            var nowStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            var dateRangeStr = model.Filter.Date.HasValue
                ? model.Filter.Date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "Todas las fechas";
            var statusFilterStr = string.IsNullOrWhiteSpace(model.Filter.Status) ? "Todos los estados" : model.Filter.Status;
            var typeFilterStr = string.IsNullOrWhiteSpace(model.Filter.OrderType) ? "Todos los tipos" : model.Filter.OrderType;

            page.DrawText($"Fecha y hora de generación: {nowStr}", margin + 10, y - 16, 8.5, false, "#554437");
            page.DrawText($"Rango de fechas: {dateRangeStr}", margin + 10, y - 29, 8.5, false, "#554437");
            page.DrawText($"Filtros aplicados: Estado: {statusFilterStr} | Tipo: {typeFilterStr}", margin + 10, y - 42, 8.5, true, "#4a3a2f");
            y -= 60;

            // --- 2. INDICADORES KPI CON ÍCONOS VECTORIALES (CA-01) ---
            double kpiWidth = (contentWidth - 20) / 3;
            double kpiHeight = 52;

            // KPI 1: Pedidos filtrados (Ícono de portapapeles/comandas)
            DrawKpiBox(page, margin, y - kpiHeight, kpiWidth, kpiHeight, "Pedidos filtrados", model.FilteredOrdersCount.ToString(), "#ede6dc", "#24160e", KpiIconType.Clipboard);
            // KPI 2: Ventas (Ícono de gráfico ascendente con flecha)
            DrawKpiBox(page, margin + kpiWidth + 10, y - kpiHeight, kpiWidth, kpiHeight, "Ventas", $"S/ {model.TotalSales:N2}", "#cf6b29", "#cf6b29", KpiIconType.TrendingUp);
            // KPI 3: Ticket promedio (Ícono de personas/usuarios)
            DrawKpiBox(page, margin + (kpiWidth * 2) + 20, y - kpiHeight, kpiWidth, kpiHeight, "Ticket promedio", $"S/ {model.AverageTicket:N2}", "#ede6dc", "#24160e", KpiIconType.People);
            y -= (kpiHeight + 15);

            // --- 3. SECCIONES DE DESGLOSE FINANCIERO (CA-01) ---
            double breakdownColWidth = (contentWidth - 14) / 2;
            double breakdownHeight = 92;

            // Bloque: Ventas por método de pago
            page.DrawRect(margin, y - breakdownHeight, breakdownColWidth, breakdownHeight, "#ffffff", "#e8dec8", 1);
            page.DrawRect(margin, y - 22, breakdownColWidth, 22, "#ede5d6", null, 0);
            page.DrawText("VENTAS POR MÉTODO DE PAGO", margin + 8, y - 15, 8.5, true, "#4a3a2f");
            double py = y - 37;
            DrawBreakdownRow(page, margin + 8, py, breakdownColWidth - 16, "Yape", $"S/ {model.SalesByPaymentMethod.Yape:N2}");
            py -= 18;
            DrawBreakdownRow(page, margin + 8, py, breakdownColWidth - 16, "Plin", $"S/ {model.SalesByPaymentMethod.Plin:N2}");
            py -= 18;
            DrawBreakdownRow(page, margin + 8, py, breakdownColWidth - 16, "Efectivo", $"S/ {model.SalesByPaymentMethod.Cash:N2}");

            // Bloque: Ventas por tipo de pedido
            double bx2 = margin + breakdownColWidth + 14;
            page.DrawRect(bx2, y - breakdownHeight, breakdownColWidth, breakdownHeight, "#ffffff", "#e8dec8", 1);
            page.DrawRect(bx2, y - 22, breakdownColWidth, 22, "#ede5d6", null, 0);
            page.DrawText("VENTAS POR TIPO DE PEDIDO", bx2 + 8, y - 15, 8.5, true, "#4a3a2f");
            double ty = y - 37;
            foreach (var item in model.SalesByOrderType)
            {
                DrawBreakdownRow(page, bx2 + 8, ty, breakdownColWidth - 16, $"{item.OrderType} ({item.OrdersCount} ped)", $"S/ {item.TotalSales:N2}");
                ty -= 18;
            }
            y -= (breakdownHeight + 18);

            // --- 4. TABLA DETALLADA DE PEDIDOS (CA-01) ---
            page.DrawText("DETALLE DE PEDIDOS", margin, y - 12, 10, true, "#24160e");
            y -= 18;

            double tableHeaderHeight = 20;
            double rowHeight = 17;

            void DrawTableHeader(SimplePdfPage p, double topY)
            {
                p.DrawRect(margin, topY - tableHeaderHeight, contentWidth, tableHeaderHeight, "#ede5d6", "#e8dec8", 1);
                p.DrawText("CÓDIGO", margin + 8, topY - 14, 8, true, "#6d5f53");
                p.DrawText("TIPO", margin + 70, topY - 14, 8, true, "#6d5f53");
                p.DrawText("CLIENTE", margin + 160, topY - 14, 8, true, "#6d5f53");
                p.DrawText("ESTADO", margin + 285, topY - 14, 8, true, "#6d5f53");
                p.DrawText("PAGO", margin + 375, topY - 14, 8, true, "#6d5f53");
                p.DrawText("TOTAL", margin + contentWidth - 45, topY - 14, 8, true, "#6d5f53");
            }

            DrawTableHeader(page, y);
            y -= tableHeaderHeight;

            if (model.Orders.Count == 0)
            {
                page.DrawRect(margin, y - 25, contentWidth, 25, "#ffffff", "#f0e7db", 1);
                page.DrawText("No hay pedidos que coincidan con los filtros seleccionados.", margin + 15, y - 16, 9, false, "#7b6e62");
                y -= 25;
            }
            else
            {
                for (int i = 0; i < model.Orders.Count; i++)
                {
                    var order = model.Orders[i];

                    // Paginación si se alcanza el margen inferior
                    if (y < margin + 45)
                    {
                        DrawFooter(page, pdfDoc.Pages.Count);
                        page = pdfDoc.NewPage();
                        y = pageHeight - margin;

                        page.DrawText("EL VIEJO MADERO - Reporte de Ventas y Pedidos (Continuación)", margin, y - 14, 10, true, "#24160e");
                        y -= 22;
                        DrawTableHeader(page, y);
                        y -= tableHeaderHeight;
                    }

                    string bg = (i % 2 == 1) ? "#faf7f2" : "#ffffff";
                    page.DrawRect(margin, y - rowHeight, contentWidth, rowHeight, bg, "#f0e7db", 0.5);

                    page.DrawText(order.Code, margin + 8, y - 12, 8.5, true, "#24160e");
                    page.DrawText(order.TypeDisplay, margin + 70, y - 12, 8, false, "#38281d");
                    page.DrawText(Truncate(order.CustomerName, 22), margin + 160, y - 12, 8, false, "#38281d");
                    page.DrawText(order.StatusDisplay, margin + 285, y - 12, 8, true, GetStatusColor(order.StatusDisplay));
                    page.DrawText(order.PaymentMethodDisplay, margin + 375, y - 12, 8, false, "#38281d");
                    page.DrawText($"S/ {order.TotalAmount:N2}", margin + contentWidth - 55, y - 12, 8.5, true, "#24160e");

                    y -= rowHeight;
                }
            }

            // Pie de página en todas las páginas
            for (int i = 0; i < pdfDoc.Pages.Count; i++)
            {
                DrawFooter(pdfDoc.Pages[i], i + 1, pdfDoc.Pages.Count);
            }

            return pdfDoc.Save();
        }

        private static void DrawKpiBox(
            SimplePdfPage page,
            double x,
            double y,
            double w,
            double h,
            string label,
            string value,
            string iconBg,
            string valColor,
            KpiIconType iconType)
        {
            page.DrawRect(x, y, w, h, "#ffffff", "#ebe2d3", 1);
            // Cuadro para el ícono lateral (36x36 pt)
            page.DrawRect(x + 8, y + 8, 36, 36, iconBg, null, 0);

            // Dibujar ícono vectorial exacto
            switch (iconType)
            {
                case KpiIconType.Clipboard:
                    page.DrawVectorIconClipboard(x + 8, y + 8, "#3f3126");
                    break;
                case KpiIconType.TrendingUp:
                    page.DrawVectorIconTrendingUp(x + 8, y + 8, "#ffffff");
                    break;
                case KpiIconType.People:
                    page.DrawVectorIconPeople(x + 8, y + 8, "#3f3126");
                    break;
            }

            page.DrawText(label, x + 52, y + 33, 7.5, false, "#7b6e62");
            page.DrawText(value, x + 52, y + 16, 12.5, true, valColor);
        }

        private static void DrawBreakdownRow(SimplePdfPage page, double x, double y, double w, string label, string value)
        {
            page.DrawText(label, x, y, 8, false, "#4a3c31");
            page.DrawText(value, x + w - 55, y, 8.5, true, "#24160e");
            page.DrawLine(x, y - 4, x + w, y - 4, 0.5, "#f0e7db");
        }

        private static void DrawFooter(SimplePdfPage page, int currentPage, int totalPages = 1)
        {
            page.DrawLine(40, 32, 555.28, 32, 0.5, "#d5c8b5");
            page.DrawText("El Viejo Madero - Sistema de Gestión de Comandas y Delivery", 40, 20, 7.5, false, "#8a7c71");
            page.DrawText($"Página {currentPage} de {totalPages}", 500, 20, 7.5, false, "#8a7c71");
        }

        private static string GetStatusColor(string status) => status switch
        {
            "Entregado" or "Servido" => "#27522d",
            "Listo" => "#434d35",
            "En preparación" or "En cocina" => "#874d1a",
            _ => "#554437"
        };

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max - 2) + "..";
        }
    }

    /// <summary>
    /// Generador de documentos PDF 1.4 binario ligero, multiplataforma y sin dependencias externas.
    /// </summary>
    internal class SimplePdfDocument
    {
        public List<SimplePdfPage> Pages { get; } = new();

        public SimplePdfPage NewPage()
        {
            var p = new SimplePdfPage(this);
            Pages.Add(p);
            return p;
        }

        public byte[] Save()
        {
            using var ms = new MemoryStream();
            using var w = new StreamWriter(ms, Encoding.ASCII);

            var offsets = new List<long> { 0 };

            w.Write("%PDF-1.4\n%âãÏÓ\n");
            w.Flush();

            // Objeto 1: Catálogo
            offsets.Add(ms.Position);
            w.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            w.Flush();

            int pagesObjId = 2;
            int fontRegularId = 3;
            int fontBoldId = 4;
            int nextObjId = 5;

            var pageObjectIds = new List<(int PageId, int ContentId)>();
            for (int i = 0; i < Pages.Count; i++)
            {
                pageObjectIds.Add((nextObjId++, nextObjId++));
            }

            offsets.Add(ms.Position);
            var kidsStr = string.Join(" ", pageObjectIds.Select(p => $"{p.PageId} 0 R"));
            w.Write($"{pagesObjId} 0 obj\n<< /Type /Pages /Kids [ {kidsStr} ] /Count {Pages.Count} >>\nendobj\n");
            w.Flush();

            offsets.Add(ms.Position);
            w.Write($"{fontRegularId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
            w.Flush();

            offsets.Add(ms.Position);
            w.Write($"{fontBoldId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
            w.Flush();

            for (int i = 0; i < Pages.Count; i++)
            {
                var p = Pages[i];
                var ids = pageObjectIds[i];

                offsets.Add(ms.Position);
                w.Write($"{ids.PageId} 0 obj\n");
                w.Write("<< /Type /Page\n");
                w.Write($"   /Parent {pagesObjId} 0 R\n");
                w.Write("   /MediaBox [ 0 0 595.28 841.89 ]\n");
                w.Write($"   /Resources << /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R >> >>\n");
                w.Write($"   /Contents {ids.ContentId} 0 R\n");
                w.Write(">>\nendobj\n");
                w.Flush();

                var streamBytes = p.BuildContentStreamBytes();
                offsets.Add(ms.Position);
                w.Write($"{ids.ContentId} 0 obj\n");
                w.Write($"<< /Length {streamBytes.Length} >>\nstream\n");
                w.Flush();
                ms.Write(streamBytes, 0, streamBytes.Length);
                w.Write("\nendstream\nendobj\n");
                w.Flush();
            }

            long xrefOffset = ms.Position;
            int totalObjects = nextObjId;
            w.Write($"xref\n0 {totalObjects}\n");
            w.Write("0000000000 65535 f \n");
            for (int i = 1; i < totalObjects; i++)
            {
                w.Write($"{offsets[i]:D10} 00000 n \n");
            }

            w.Write($"trailer\n<< /Size {totalObjects} /Root 1 0 R >>\n");
            w.Write($"startxref\n{xrefOffset}\n%%EOF\n");
            w.Flush();

            return ms.ToArray();
        }
    }

    internal class SimplePdfPage
    {
        private readonly StringBuilder _sb = new();
        public SimplePdfDocument Document { get; }

        public SimplePdfPage(SimplePdfDocument doc)
        {
            Document = doc;
        }

        public void DrawText(string text, double x, double y, double fontSize, bool bold, string? rgbHex = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            string fontName = bold ? "/F2" : "/F1";
            var escaped = EscapePdfText(text);

            _sb.Append("BT\n");
            if (!string.IsNullOrEmpty(rgbHex))
            {
                var (r, g, b) = ParseRgb(rgbHex);
                _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} rg\n", r, g, b);
            }
            else
            {
                _sb.Append("0 0 0 rg\n");
            }
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1:F2} Tf\n", fontName, fontSize);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} Td\n", x, y);
            _sb.AppendFormat("({0}) Tj\n", escaped);
            _sb.Append("ET\n");
        }

        public void DrawRect(double x, double y, double w, double h, string? fillHex, string? strokeHex, double lineWidth = 1)
        {
            _sb.Append("q\n");
            if (lineWidth > 0)
            {
                _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} w\n", lineWidth);
            }

            bool hasFill = !string.IsNullOrEmpty(fillHex);
            bool hasStroke = !string.IsNullOrEmpty(strokeHex) && lineWidth > 0;

            if (hasFill)
            {
                var (r, g, b) = ParseRgb(fillHex!);
                _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} rg\n", r, g, b);
            }

            if (hasStroke)
            {
                var (r, g, b) = ParseRgb(strokeHex!);
                _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG\n", r, g, b);
            }

            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re\n", x, y, w, h);

            if (hasFill && hasStroke) _sb.Append("B\n");
            else if (hasFill) _sb.Append("f\n");
            else if (hasStroke) _sb.Append("S\n");
            else _sb.Append("n\n");

            _sb.Append("Q\n");
        }

        public void DrawLine(double x1, double y1, double x2, double y2, double width, string? strokeHex)
        {
            _sb.Append("q\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} w\n", width);
            if (!string.IsNullOrEmpty(strokeHex))
            {
                var (r, g, b) = ParseRgb(strokeHex);
                _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG\n", r, g, b);
            }
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", x1, y1);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l\n", x2, y2);
            _sb.Append("S\n");
            _sb.Append("Q\n");
        }

        public void DrawVectorIconClipboard(double x, double y, string strokeHex)
        {
            double cx = x + 18;
            double cy = y + 18;
            var (r, g, b) = ParseRgb(strokeHex);

            _sb.Append("q\n");
            _sb.Append("1 J 1 j\n"); // Round cap y join
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG\n", r, g, b);

            // Tablero rectangular exterior (15 x 19 pt)
            _sb.Append("1.4 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} 15.00 19.00 re S\n", cx - 7.5, cy - 9.5);

            // Clip superior (8 x 3.5 pt) con fondo
            var (bgR, bgG, bgB) = ParseRgb("#ede6dc");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} rg\n", bgR, bgG, bgB);
            _sb.Append("1.0 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} 8.00 3.50 re B\n", cx - 4.0, cy + 8.0);

            // Checkmark interior
            _sb.Append("1.4 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", cx - 4.0, cy + 1.0);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l\n", cx - 1.5, cy - 2.0);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l S\n", cx + 4.5, cy + 4.5);

            // Línea interior inferior
            _sb.Append("1.1 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m {2:F2} {1:F2} l S\n", cx - 4.0, cy - 5.5, cx + 4.0);

            _sb.Append("Q\n");
        }

        public void DrawVectorIconTrendingUp(double x, double y, string strokeHex)
        {
            double cx = x + 18;
            double cy = y + 18;
            var (r, g, b) = ParseRgb(strokeHex);

            _sb.Append("q\n");
            _sb.Append("1 J 1 j\n"); // Round cap y join
            _sb.Append("2.2 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG\n", r, g, b);

            // Línea de tendencia ascendente en zigzag
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", cx - 10.0, cy - 6.0);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l\n", cx - 3.0, cy + 1.0);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l\n", cx + 2.0, cy - 3.0);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l S\n", cx + 9.5, cy + 6.5);

            // Flecha en la punta superior derecha
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", cx + 4.5, cy + 6.5);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l\n", cx + 9.5, cy + 6.5);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} l S\n", cx + 9.5, cy + 1.5);

            _sb.Append("Q\n");
        }

        public void DrawVectorIconPeople(double x, double y, string strokeHex)
        {
            double cx = x + 18;
            double cy = y + 18;
            var (r, g, b) = ParseRgb(strokeHex);

            _sb.Append("q\n");
            _sb.Append("1 J 1 j\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG\n", r, g, b);

            // Persona 1 (principal a la izquierda)
            // Cabeza (círculo Bézier)
            double h1x = cx - 2.5;
            double h1y = cy + 4.0;
            double hr = 3.2;
            double k = hr * 0.5522847498;
            _sb.Append("1.3 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", h1x, h1y + hr);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c\n", h1x + k, h1y + hr, h1x + hr, h1y + k, h1x + hr, h1y);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c\n", h1x + hr, h1y - k, h1x + k, h1y - hr, h1x, h1y - hr);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c\n", h1x - k, h1y - hr, h1x - hr, h1y - k, h1x - hr, h1y);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c S\n", h1x - hr, h1y + k, h1x - k, h1y + hr, h1x, h1y + hr);

            // Hombros/cuerpo persona 1 (arco curvo)
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", h1x - 7.0, cy - 7.5);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c S\n", h1x - 6.5, cy - 2.0, h1x + 6.5, cy - 2.0, h1x + 7.0, cy - 7.5);

            // Persona 2 (secundaria a la derecha)
            double h2x = cx + 5.5;
            double h2y = cy + 6.0;
            double hr2 = 2.4;
            double k2 = hr2 * 0.5522847498;
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", h2x, h2y + hr2);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c\n", h2x + k2, h2y + hr2, h2x + hr2, h2y + k2, h2x + hr2, h2y);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c\n", h2x + hr2, h2y - k2, h2x + k2, h2y - hr2, h2x, h2y - hr2);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c\n", h2x - k2, h2y - hr2, h2x - hr2, h2y - k2, h2x - hr2, h2y);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c S\n", h2x - hr2, h2y + k2, h2x - k2, h2y + hr2, h2x, h2y + hr2);

            // Hombro persona 2
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m\n", h2x - 1.0, cy - 1.0);
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} c S\n", h2x + 3.0, cy + 0.5, h2x + 7.0, cy - 2.5, h2x + 7.5, cy - 6.0);

            _sb.Append("Q\n");
        }

        public byte[] BuildContentStreamBytes()
        {
            return Encoding.Latin1.GetBytes(_sb.ToString());
        }

        private static (double R, double G, double B) ParseRgb(string hex)
        {
            var clean = hex.TrimStart('#');
            if (clean.Length == 6)
            {
                int ri = int.Parse(clean.Substring(0, 2), NumberStyles.HexNumber);
                int gi = int.Parse(clean.Substring(2, 2), NumberStyles.HexNumber);
                int bi = int.Parse(clean.Substring(4, 2), NumberStyles.HexNumber);
                return (ri / 255.0, gi / 255.0, bi / 255.0);
            }
            return (0, 0, 0);
        }

        private static string EscapePdfText(string text)
        {
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '(': sb.Append("\\("); break;
                    case ')': sb.Append("\\)"); break;
                    case '\\': sb.Append("\\\\"); break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
