using System;
using System.Collections.Generic;

namespace Precheck.Models.DTOs.QRCodeDetails
{
    /// <summary>
    /// Paginated response wrapper for POST /api/QRCode/GetAvailableQr
    /// </summary>
    public class GetAvailableQrPagedResponse
    {
        public List<GetAvailableQrGroupedResponse> Data { get; set; } = new List<GetAvailableQrGroupedResponse>();
        public int TotalRecords { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 0;
        public bool HasNextPage => PageNumber < TotalPages;
        public bool HasPreviousPage => PageNumber > 1;
    }
}
