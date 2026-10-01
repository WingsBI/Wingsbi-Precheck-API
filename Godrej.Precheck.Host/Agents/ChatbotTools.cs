using System.ComponentModel;
using Godrej.Precheck.Models.DataModel;
using Godrej.Precheck.Models.DTOs.Chatbot;
using Godrej.Precheck.Models.DTOs.DrawingNumber;
using Godrej.Precheck.Models.DTOs.IRNumber;
using Godrej.Precheck.Models.DTOs.MSNNumber;
using Godrej.Precheck.Models.DTOs.Precheck;
using Godrej.Precheck.Service.Service.CommonService;
using Godrej.Precheck.Service.Service.PrecheckService;
using Godrej.Precheck.Service.Service.ProductionOrderService;
using Godrej.Precheck.Service.Service.QRCodeService;

namespace Godrej.Precheck.Host.Agents
{
    // Registered AddScoped in Program.cs and resolved per-request in ChatbotController, same as
    // the service dependencies below - these are scoped dependencies, so this class must never
    // be constructed once at startup (that would fail to resolve its scoped dependencies).
    public class ChatbotTools
    {
        private readonly ICommonService _commonService;
        private readonly IProductionOrderService _productionOrderService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IPrecheckService _precheckService;

        public ChatbotTools(
            ICommonService commonService,
            IProductionOrderService productionOrderService,
            IQRCodeService qrCodeService,
            IPrecheckService precheckService)
        {
            _commonService = commonService;
            _productionOrderService = productionOrderService;
            _qrCodeService = qrCodeService;
            _precheckService = precheckService;
        }

        [Description("Get Production Orders filtered by status, PO number, LN item code, and/or drawing number. " +
            "precheckStatus values: 1=Pending, 2=Partial, 3=Completed, 4=Pending-Planner. Map casual terms like " +
            "'in progress'/'open' to Pending (1) unless the user is more specific.")]
        public async Task<List<ProductionOrderSummaryDto>> GetProductionOrderStatusAsync(
            [Description("Precheck status: 1=Pending, 2=Partial, 3=Completed, 4=Pending-Planner. Omit to not filter by status.")] int? precheckStatus,
            [Description("Exact or partial Production Order number to filter by. Omit if not specified.")] string? poNumber,
            [Description("LN item code to filter by. Omit if not specified.")] string? lnItemCode,
            [Description("Drawing number to filter by. Omit if not specified.")] string? drawingNumber)
        {
            var results = await _productionOrderService.GetAllProductionOrdersAsync(
                dateFilterType: null, filterDate: null, fromDate: null, toDate: null,
                precheckStatus: precheckStatus, poNumber: poNumber, lnItemCode: lnItemCode,
                roleId: 0, drawingNumber: drawingNumber);

            // Trimmed to the fields useful for a chat answer - the full ProductionOrderMasterDto
            // (audit columns, BOM components, etc.) blows the model's token budget in one request
            // when a broad query matches thousands of rows (e.g. "all pending POs" ~ 2100 rows).
            return results.Select(r => new ProductionOrderSummaryDto
            {
                ProductionOrderNumber = r.ProductionOrderNumber,
                Status = r.PrecheckStatusName,
                LnItemCode = r.LnItemCode,
                DrawingNumber = r.DrawingNumber,
                Quantity = r.Quantity,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        [Description("Get full details for a single Production Order by its exact Production Order number.")]
        public Task<Godrej.Precheck.Models.DTOs.ProductionOrder.ProductionOrderMasterDto?> GetProductionOrderDetailsAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber)
            => _productionOrderService.GetByProductionOrderNumberAsync(productionOrderNumber);

        [Description("List all Component Types in the system.")]
        public Task<List<Godrej.Precheck.Models.DataModel.ComponentsType>> GetComponentTypesAsync()
            => _commonService.ComponentTypeService();

        [Description("Look up a single Component Type by its exact name.")]
        public Task<Godrej.Precheck.Models.DataModel.ComponentsType> GetComponentTypeByNameAsync(
            [Description("The exact Component Type name.")] string name)
            => _commonService.ComponentTypeByNameService(name);

        [Description("List or search Drawing Numbers, optionally filtered by Component Type and/or a search term.")]
        public Task<List<Godrej.Precheck.Models.DTOs.DrawingNumber.GetAllDrawingResponseDto>> GetDrawingNumbersAsync(
            [Description("Component Type name to filter by. Omit if not specified.")] string? componentType,
            [Description("Free-text search term to filter drawing numbers by. Omit if not specified.")] string? search)
            => _commonService.GetAllDrawingNumberService(new GetAllDrawingRequestDto
            {
                ComponentType = componentType,
                Search = search
            });

        [Description("List all Assemblies in the system.")]
        public Task<List<Godrej.Precheck.Models.DataModel.Assembly.AssemblyNumbers>> GetAssembliesAsync()
            => _commonService.GetAllAssembly();

        [Description("Get Assembly-to-Drawing mappings, optionally filtered by LN item code.")]
        public Task<List<Godrej.Precheck.Models.DTOs.Assembly.AssemblyDrawingMappingDto>> GetAssemblyDrawingMappingsAsync(
            [Description("LN item code to filter by. Omit to get all mappings.")] string? lnItemCode)
            => _commonService.GetAllAssemblyDrawingMappingsAsync(lnItemCode);

        [Description("Search LN item codes by a free-text search term.")]
        public Task<List<string>> SearchLnItemCodeAsync(
            [Description("The search term to match LN item codes against.")] string search)
            => _commonService.GetAllLnItemCode(search);

        // ---- QC domain: QR Code + IR/MSN ----

        [Description("Look up a single QR code's details and status by its exact QR code number.")]
        public async Task<QrCodeSummaryDto?> GetQrCodeDetailsAsync(
            [Description("The exact QR code number.")] string qrCodeNumber)
        {
            var result = await _qrCodeService.GetQRCodeDetailsService(qrCodeNumber, null);
            if (result == null) return null;
            return new QrCodeSummaryDto
            {
                QrCodeNumber = result.QrCodeNumber,
                QrCodeStatus = result.QrCodeStatus,
                ProductionOrderNumber = result.ProductionOrderNumber,
                RackLocation = result.RackLocation,
                IdNumber = result.IdNumber,
                ExpiryDate = result.ExpiryDate
            };
        }

        [Description("Look up a single standard QR code's details and status by its exact QR code number.")]
        public async Task<QrCodeSummaryDto> GetStandardQrCodeDetailsAsync(
            [Description("The exact QR code number.")] string qrCodeNumber)
        {
            var result = await _qrCodeService.GetStandardQRCodeDetailsService(qrCodeNumber);
            return new QrCodeSummaryDto
            {
                QrCodeNumber = result.QrCodeNumber,
                QrCodeStatus = result.QrCodeStatus,
                ProductionOrderNumber = result.ProductionOrderNumber,
                RackLocation = result.RackLocation,
                IdNumber = result.IdNumber,
                ExpiryDate = result.ExpiryDate
            };
        }

        [Description("Search/list QR codes, optionally filtered by a search term, production series, and/or date range.")]
        public async Task<List<QrCodeSummaryDto>> SearchQrCodesAsync(
            [Description("Free-text search term. Omit if not specified.")] string? searchQuery,
            [Description("Production series names to filter by. Omit if not specified.")] List<string>? prodSeries,
            [Description("Start of date range to filter by created date. Omit if not specified.")] DateTime? fromDate,
            [Description("End of date range to filter by created date. Omit if not specified.")] DateTime? toDate)
        {
            var paged = await _qrCodeService.GetBarcodeDetailsWithParametersService(
                searchQuery, prodSeries, null, fromDate, toDate, pageNumber: 1, pageSize: null);

            return paged.Data.Select(r => new QrCodeSummaryDto
            {
                QrCodeNumber = r.QrCodeNumber,
                QrCodeStatus = r.QrCodeStatus,
                ProductionOrderNumber = r.ProductionOrderNumber,
                RackLocation = r.RackLocation,
                IdNumber = r.IdNumber,
                ExpiryDate = r.ExpiryDate
            }).ToList();
        }

        [Description("List/filter IR numbers by a free-text search query.")]
        public async Task<List<IRNumberSummaryDto>> GetIrNumbersAsync(
            [Description("Free-text search term to match IR numbers against. Omit if not specified.")] string? search)
        {
            var results = await _commonService.IRNumberService(new GetAllIRNumberRequestDto { query = search });
            return results.Select(r => new IRNumberSummaryDto
            {
                IrNumber = r.IrNumber,
                ProductionOrderNumber = r.ProductionOrderNumber,
                DrawingNumberIdName = r.DrawingNumberIdName,
                Stage = r.Stage,
                Quantity = r.Quantity,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        [Description("List/filter MSN numbers by a free-text search query.")]
        public async Task<List<MSNNumberSummaryDto>> GetMsnNumbersAsync(
            [Description("Free-text search term to match MSN numbers against. Omit if not specified.")] string? search)
        {
            var results = await _commonService.MSNNumberService(new GetAllMSNNumberRequestDto { query = search });
            return results.Select(r => new MSNNumberSummaryDto
            {
                MsnNumber = r.MsnNumber,
                ProductionOrderNumber = r.ProductionOrderNumber,
                DrawingNumberIdName = r.DrawingNumberIdName,
                Stage = r.Stage,
                Quantity = r.Quantity,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        [Description("Get IR numbers for a specific drawing number.")]
        public async Task<List<IRNumberSummaryDto>> GetIrNumbersByDrawingAsync(
            [Description("The drawing number to look up IR numbers for.")] string drawingNumber)
        {
            var results = await _commonService.IRNumberByDrawingNumberService(new GetIRNumberByDrawingNumberRequest { DrawingNumber = drawingNumber });
            return results.Select(r => new IRNumberSummaryDto
            {
                IrNumber = r.IrNumber,
                ProductionOrderNumber = r.ProductionOrderNumber,
                DrawingNumberIdName = r.DrawingNumberIdName,
                Stage = r.Stage,
                Quantity = r.Quantity,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        [Description("Get MSN numbers for a specific drawing number.")]
        public async Task<List<MSNNumberSummaryDto>> GetMsnNumbersByDrawingAsync(
            [Description("The drawing number to look up MSN numbers for.")] string drawingNumber)
        {
            var results = await _commonService.MSNNumberByDrawingNumberService(new GetMSNNumberByDrawingNumberRequest { DrawingNumber = drawingNumber });
            return results.Select(r => new MSNNumberSummaryDto
            {
                MsnNumber = r.MsnNumber,
                ProductionOrderNumber = r.ProductionOrderNumber,
                DrawingNumberIdName = r.DrawingNumberIdName,
                Stage = r.Stage,
                Quantity = r.Quantity,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        // ---- Store domain: Precheck ----

        [Description("Get the Precheck status code for a Production Order. Returns a status code: " +
            "1=Pending, 2=Partial, 3=Completed, 4=Pending-Planner, or null if not found.")]
        public Task<int?> GetPrecheckStatusAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber)
            => _precheckService.GetPrecheckStatusDetailsService(new ViewPreCheckRequestDto
            {
                ProductionOrderNumber = productionOrderNumber
            });

        [Description("List Production Orders with pending prechecks, optionally filtered by PO number or LN item code.")]
        public async Task<List<PendingPrecheckSummaryDto>> GetPendingPrechecksAsync(
            [Description("Exact or partial Production Order number to filter by. Omit if not specified.")] string? poNumber,
            [Description("LN item code to filter by. Omit if not specified.")] string? lnItemCode)
        {
            var results = await _precheckService.GetPendingPrecheckAsync(new PendingPrecheckRequestDto
            {
                ProductionOrderNumber = poNumber,
                LnItemCode = lnItemCode
            });

            return results.Select(r => new PendingPrecheckSummaryDto
            {
                ProductionOrderNumber = r.ProductionOrderNumber,
                DrawingNumber = r.DrawingNumber,
                LnItemCode = r.LnItemCode,
                PendingUnitCount = r.PendingIdNumbers.Count
            }).ToList();
        }

        [Description("Get components consumed for a specific drawing number.")]
        public Task<List<ConsumedInComponentsResponseDto>> GetConsumedInComponentsAsync(
            [Description("The drawing number ID to look up consumed components for.")] int drawingNumberId)
            => _precheckService.GetConsumedInComponentsAsync(drawingNumberId);

        [Description("Get components available for precheck for a given production series.")]
        public async Task<List<AvailableComponentSummaryDto>> GetAvailableComponentsAsync(
            [Description("The production series ID to look up available components for.")] int prodSeriesId,
            [Description("Drawing number ID to filter by. Omit if not specified.")] int? drawingNumberId)
        {
            var results = await _precheckService.GetAvailableComponentService(new GetAvailableComponentsRequest
            {
                ProdSeriesId = prodSeriesId,
                DrawingNumberId = drawingNumberId
            });

            return results.Select(r => new AvailableComponentSummaryDto
            {
                DrawingNumber = r.DrawingNumber,
                LnItemCode = r.LnItemCode,
                Location = r.Location,
                QrCodeNumber = r.QrCodeNumber,
                IdNumber = r.IdNumber,
                Quantity = r.Quantity,
                RemainingQuantity = r.RemainingQuantity,
                ProductionOrderNumber = r.ProductionOrderNumber,
                Status = r.Status
            }).ToList();
        }
    }
}
