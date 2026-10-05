using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Precheck.Models.DTOs.DrawingNumber;

namespace Precheck.Service.Service.DrawingNumberService
{
    public interface IDrawingNumberService
    {
        Task<DrawingMappingResponseDto> InsertDrawingMappingsAsync(InsertDrawingMappingDto request);
        Task<GetDrawingMappingDto> GetDrawingMappingsAsync(int drawingNumberId);
    }
}

