using System.Collections.Generic;

namespace Precheck.Models.DTOs.QRCodeDetails
{
    public class ExportQrCodeRequestDto
    {
        public List<string>? QRCodeNumbers { get; set; }
        public List<string>? BatchIdNumbers { get; set; }
        public List<string>? SelectedColumns { get; set; }
    }
}
