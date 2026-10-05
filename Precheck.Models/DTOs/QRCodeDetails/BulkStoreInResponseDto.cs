using System.Collections.Generic;

namespace Precheck.Models.DTOs.QRCodeDetails
{
    public class BulkStoreInResultDto
    {
        public string QrCodeNumber { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public QRCodeDetailsResponseDto Data { get; set; }
    }

    public class BulkStoreInResponseDto
    {
        public List<BulkStoreInResultDto> Results { get; set; } = new List<BulkStoreInResultDto>();
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }
}
