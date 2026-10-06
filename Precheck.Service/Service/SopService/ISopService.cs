using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Precheck.Models.DataModel.Sop;
using Precheck.Models.DTOs.Sop;
using Precheck.Models.DTOs.Bom;

namespace Precheck.Service.Service.SopService
{
    public interface ISopService
    {
        Task<List<SopAssemblyResponseDto>> GetAllAssembly();
        Task<List<GetSopResponseDto>> GetSopForAssembly(GetSopRequestDto request);
        Task<List<GetSopResponseDto>> GetSopForAssembly(GetSopRequestDto request, bool excludeRawMaterial);
        byte[] ExportToExcel(List<GetSopResponseDto> items, string projectId, List<string>? selectedColumns = null);

        Task<List<BomDetailsResponseDto>> GetBomDetails(string assemblyNumber);
        Task<List<AssemblySearchResponseDto>> SearchAssemblyNumbers(string searchText);
        byte[] ExportBomToExcel(List<BomDetailsResponseDto> items, string assemblyNumber, List<string>? selectedColumn = null);
    }
}

