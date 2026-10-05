using System.Collections.Generic;
using Precheck.Models.DTOs.Precheck;

namespace Precheck.Models.DTOs.ProductionOrder
{
    public class ProductionOrderDetailsDto
    {
        public ProductionOrderMasterDto Master { get; set; } = new();
        public List<MakeOrderResponseDto> BomItems { get; set; } = new();
    }
}
