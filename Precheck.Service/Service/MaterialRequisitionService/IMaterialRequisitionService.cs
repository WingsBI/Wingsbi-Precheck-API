using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Precheck.Models.DataModel.MaterialRequisition;
using Precheck.Models.DTOs.MaterialRequisition;

namespace Precheck.Service.Service.MaterialRequisitionService
{
    public interface IMaterialRequisitionService
    {
        Task<List<MaterialRequisitionResponse>> GetMaterialRequisitions();
        Task<List<SwappingDetailsResponse>> GetSwappingDetails();
        Task<List<MaterialRequisitionResponse>> GetMaterialRequisitionsByStatus(string status,int statusId);
        Task<int> UpdateMaterialRequisition(UpdateMaterialRequisitionRequestDto request, int modifiedBy);
        Task<int> CancelMaterialRequisition(CancelMaterialRequisitionRequestDto request, int modifiedBy);
        Task<(int NewId, string RequestNumber)> CreateMaterialRequisition(CreateMaterialRequisitionRequestDto request, int createdBy);
        Task<int> CreateSwappedDrawingNumber(CreateSwappedDrawingNumberRequestDto request, int createdBy);
        byte[] ExportToExcel(List<MaterialRequisitionResponse> materialRequisitions, List<string>? selectedColumns = null);
    }
}

