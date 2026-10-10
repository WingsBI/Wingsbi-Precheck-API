namespace Precheck.Models.DTOs.QRCodeDetails
{
    // One row of the QR code status summary: how many QR codes are in a given status.
    public class QrCodeStatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
