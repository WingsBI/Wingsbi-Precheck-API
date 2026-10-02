using System.ComponentModel;
using Godrej.Precheck.Models.DataModel;
using Godrej.Precheck.Models.DataModel.Precheck;
using Godrej.Precheck.Models.DTOs.Chatbot;
using Godrej.Precheck.Models.DTOs.DrawingNumber;
using Godrej.Precheck.Models.DTOs.IRNumber;
using Godrej.Precheck.Models.DTOs.MSNNumber;
using Godrej.Precheck.Models.DTOs.Precheck;
using Godrej.Precheck.Models.DTOs.QRCodeDetails;
using Godrej.Precheck.Service.Service.CommonService;
using Godrej.Precheck.Service.Service.MaterialRequisitionService;
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
        private readonly IMaterialRequisitionService _materialRequisitionService;

        public ChatbotTools(
            ICommonService commonService,
            IProductionOrderService productionOrderService,
            IQRCodeService qrCodeService,
            IPrecheckService precheckService,
            IMaterialRequisitionService materialRequisitionService)
        {
            _commonService = commonService;
            _productionOrderService = productionOrderService;
            _qrCodeService = qrCodeService;
            _precheckService = precheckService;
            _materialRequisitionService = materialRequisitionService;
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

        [Description("Get the Production Order MASTER record (overall status, quantity, drawing, dates) by its exact " +
            "Production Order number. This does NOT include per-unit Precheck verification records (scans, rejections, " +
            "remarks) - for 'precheck details'/'precheck records' of a PO, use search_precheck_records or " +
            "get_precheck_by_po_series_id instead.")]
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

        // get_ir_numbers/get_msn_numbers used to wrap ICommonService.IRNumberService/MSNNumberService,
        // but that underlying query requires an exact departmentid match with no null-guard - since
        // this tool never has a department ID to supply, it always returned zero rows. Replaced with
        // the "ByDrawingNumber" repository methods instead, which null-guard every filter (drawing
        // number, production series) and don't have that department requirement.
        [Description("List/filter IR numbers, optionally by drawing number and/or Production Series (e.g. 'H'). " +
            "At least one of drawingNumber or productionSeries should be provided.")]
        public async Task<List<IRNumberSummaryDto>> GetIrNumbersAsync(
            [Description("Drawing number to filter by (partial match). Omit if not specified.")] string? drawingNumber,
            [Description("Production Series name to filter by (exact match, e.g. 'H'). Omit if not specified.")] string? productionSeries)
        {
            var results = await _commonService.IRNumberByDrawingNumberService(new GetIRNumberByDrawingNumberRequest
            {
                DrawingNumber = drawingNumber,
                Productionseries = productionSeries
            });
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

        [Description("List/filter MSN numbers, optionally by drawing number and/or Production Series (e.g. 'H'). " +
            "At least one of drawingNumber or productionSeries should be provided.")]
        public async Task<List<MSNNumberSummaryDto>> GetMsnNumbersAsync(
            [Description("Drawing number to filter by (partial match). Omit if not specified.")] string? drawingNumber,
            [Description("Production Series name to filter by (exact match, e.g. 'H'). Omit if not specified.")] string? productionSeries)
        {
            var results = await _commonService.MSNNumberByDrawingNumberService(new GetMSNNumberByDrawingNumberRequest
            {
                DrawingNumber = drawingNumber,
                Productionseries = productionSeries
            });
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

        // get_precheck_status (removed) used to wrap IPrecheckService.GetPrecheckStatusDetailsService,
        // but that query ignores ProductionOrderNumber entirely - it actually filters on
        // DrawingNumberId + ProdSeriesId + IdNumbers (a specific unit, not a PO), none of which this
        // tool had, so it always returned null. get_production_order_status/get_production_order_details
        // already return Status/PrecheckStatusName for a PO, making a separate tool for this redundant
        // even once fixed, so it was removed rather than patched.

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

        // The underlying query (GET_AVAILABLE_COMPONENT_ORDER) filters only on drawingnumberid - it
        // does not use ProdSeriesId at all, so making ProdSeriesId the required parameter (as this
        // tool originally did) meant the real filter was always left null and the result was always
        // empty. drawingNumberId is now the required parameter; since that's an internal numeric ID
        // a chat user won't know, the description steers the model to resolve it via
        // get_drawing_numbers first.
        // ---- Precheck records (View Precheck) ----

        private static PrecheckRecordSummaryDto ToSummary(ViewPreCheckResponse r) => new()
        {
            ProductionOrderNumber = r.ProductionOrderNumber,
            ProductionSeries = r.ProductionSeries,
            DrawingNumber = r.DrawingNumber,
            IdNumber = r.IdNumber,
            QrCodeNumber = r.QrCodeNumber,
            LnItemCode = r.LnItemCode,
            ComponentType = r.ComponentType,
            Quantity = r.Quantity,
            RemainingQuantity = r.RemainingQuantity,
            Unit = r.Unit,
            PrecheckStatus = r.PrecheckStatus,
            IsPrecheckComplete = r.IsPrecheckComplete,
            IsRejected = r.IsRejected,
            Remarks = r.Remarks,
            PrecheckDate = r.PrecheckDate,
            IrNumber = r.IrNumber,
            MsnNumber = r.MsnNumber
        };

        [Description("Get precheck verification details for ONE specific unit, identified by Production Order number, " +
            "Production Series name, AND ID number (all three required). Only use this when the user has given all " +
            "three - if they've only given a PO number, use search_precheck_records instead.")]
        public async Task<List<PrecheckRecordSummaryDto>> GetPrecheckByPoSeriesIdAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber,
            [Description("The Production Series name (e.g. 'H'). Resolved internally to its ID - pass the name, not a number.")] string productionSeries,
            [Description("The ID number of the specific unit within the production order.")] int idNumber)
        {
            var series = await _commonService.ProductionSeriesByNameService(productionSeries);
            if (series == null)
            {
                return new List<PrecheckRecordSummaryDto>();
            }

            var results = await _precheckService.ViewPrecheckDetailsService(new ViewPreCheckRequestDto
            {
                ProductionOrderNumber = productionOrderNumber,
                ProductionSeriesId = series.Id,
                Id = idNumber
            });

            return results.Select(ToSummary).ToList();
        }

        [Description("Search/list Precheck verification records (scan results, PrecheckStatus, rejections, remarks per " +
            "unit) - optionally filtered by free-text search (PO number/drawing number/LN item code), Production Series, " +
            "status, and/or a date range. Use this whenever the user asks for 'precheck details'/'precheck records' for " +
            "a Production Order and has NOT given a specific Production Series + ID number - pass the PO number as " +
            "searchQuery to get all precheck records for that PO.")]
        public async Task<List<PrecheckRecordSummaryDto>> SearchPrecheckRecordsAsync(
            [Description("Free-text search term matched against PO number, drawing number, LN item code. Omit if not specified.")] string? searchQuery,
            [Description("Production Series names to filter by. Omit if not specified.")] List<string>? prodSeries,
            [Description("Status values to filter by: 'Completed', 'Pending', or 'Updated'. Omit to get all statuses.")] List<string>? status,
            [Description("Start of date range to filter by. Omit if not specified.")] DateTime? fromDate,
            [Description("End of date range to filter by. Omit if not specified.")] DateTime? toDate)
        {
            var result = await _precheckService.ViewPrecheckByParametersService(new ViewPrecheckFilterRequestDto
            {
                SearchQuery = searchQuery,
                ProdSeries = prodSeries,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate
            }, pageNumber: 1, pageSize: 200);

            return result.Data.Select(ToSummary).ToList();
        }

        [Description("Get available (verified, not-yet-consumed) components, filtered by an exact QR code OR by a " +
            "combination of free-text search, drawing number, Production Series, and/or status. Broader than " +
            "get_available_components, which requires a resolved drawing number ID.")]
        public async Task<List<AvailableComponentByFilterSummaryDto>> GetAvailableComponentsByFilterAsync(
            [Description("Exact QR code to look up. When provided, the other filters below are ignored.")] string? qrCode,
            [Description("Free-text search matched against Production Order number and ID number. Ignored when qrCode is provided.")] string? searchQuery,
            [Description("Drawing number to filter by. Ignored when qrCode is provided.")] string? drawingNumber,
            [Description("Production Series names to filter by. Ignored when qrCode is provided.")] List<string>? prodSeries,
            [Description("Status to filter by: 'Partial' or 'Pending'. Ignored when qrCode is provided.")] string? status)
        {
            var results = await _precheckService.AvailableComponentDetailsService(new AvailableComponentFilterDto
            {
                QrCode = qrCode ?? string.Empty,
                SearchQuery = searchQuery,
                DrawingNumber = drawingNumber,
                ProdSeries = prodSeries,
                Status = status
            });

            return results.Select(r => new AvailableComponentByFilterSummaryDto
            {
                IdNumber = r.IdNumber,
                Quantity = r.Quantity,
                DrawingNumber = r.DrawingNumber,
                ProductionSeries = r.ProductionSeries,
                Nomenclature = r.Nomenclature,
                ProductionOrderNumber = r.ProductionOrderNumber,
                PrecheckStatus = r.PrecheckStatus
            }).ToList();
        }

        [Description("Get the Precheck BOM/checklist template for a given assembly number - the list of components " +
            "required for that assembly's verification.")]
        public Task<List<PrecheckTemplateResponseDto>> GetPrecheckAssemblyTemplateAsync(
            [Description("The exact assembly number.")] string assemblyNumber)
            => _precheckService.GetPrecheckAssemblyTemplate(assemblyNumber);

        [Description("Get components available for precheck for a given drawing number ID. If you only have a " +
            "drawing number string, call get_drawing_numbers first to resolve its ID.")]
        public async Task<List<AvailableComponentSummaryDto>> GetAvailableComponentsAsync(
            [Description("The internal drawing number ID (from get_drawing_numbers' 'id' field) to look up available components for.")] int drawingNumberId)
        {
            var results = await _precheckService.GetAvailableComponentService(new GetAvailableComponentsRequest
            {
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

        // ---- Downstream Traceability: Stored In / Material Requisition / Swapping ----

        [Description("Get components that have been 'stored in' - i.e. verified, real-time stock on hand after passing Precheck - " +
            "optionally filtered by the date they were stored in and/or drawing number.")]
        public async Task<List<QrCodeSummaryDto>> GetStoredInComponentsAsync(
            [Description("Filter to components stored in on this exact date. Omit to get all dates.")] DateTime? storeInDate,
            [Description("Drawing number to filter by. Omit if not specified.")] string? drawingNumber)
        {
            var results = await _qrCodeService.GetComponentStoreInByDateService(new StoredInQrCodeRequest
            {
                StoreInDate = storeInDate,
                DrawingNumber = drawingNumber
            });

            return results.Select(r => new QrCodeSummaryDto
            {
                QrCodeNumber = r.QrCodeNumber,
                QrCodeStatus = r.QrCodeStatus,
                ProductionOrderNumber = r.ProductionOrderNumber,
                RackLocation = r.RackLocation,
                IdNumber = r.IdNumber,
                ExpiryDate = r.ExpiryDate
            }).ToList();
        }

        [Description("List Material Requisitions - components issued out of verified stock into production - optionally " +
            "filtered by status. To see the exact status values in use, call this once with no status filter first.")]
        public async Task<List<MaterialRequisitionSummaryDto>> GetMaterialRequisitionsAsync(
            [Description("Exact status text to filter by (e.g. as seen on an unfiltered call's Status field). Omit to get all requisitions.")] string? status)
        {
            var results = string.IsNullOrWhiteSpace(status)
                ? await _materialRequisitionService.GetMaterialRequisitions()
                : await _materialRequisitionService.GetMaterialRequisitionsByStatus(status, 0);

            return results.Select(r => new MaterialRequisitionSummaryDto
            {
                RequestNumber = r.RequestNumber,
                Status = r.Status,
                ProductionOrderNumber = r.ProductionOrderNumber,
                DrawingNumber = r.DrawingNumber,
                LnItemCode = r.LnItemCode,
                Quantity = r.Quantity,
                Unit = r.Unit,
                IsRejected = r.IsRejected,
                RequestOwner = r.RequestOwner,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        [Description("List component swapping records - where one physical component (by ID number) was swapped for " +
            "another against a drawing number and/or Production Order.")]
        public async Task<List<SwappingDetailSummaryDto>> GetSwappingDetailsAsync()
        {
            var results = await _materialRequisitionService.GetSwappingDetails();
            return results.Select(r => new SwappingDetailSummaryDto
            {
                SwappedDrawingNumber = r.SwappedDrawingNumber,
                FromSwappedIdNumber = r.FromSwappedIdNumber,
                ToSwappedIdNumber = r.ToSwappedIdNumber,
                SwappedFromPONumber = r.SwappedFromPONumber,
                SwappedToPONumber = r.SwappedToPONumber,
                CreatedDate = r.CreatedDate
            }).ToList();
        }

        [Description("Get available QR codes (verified stock not yet consumed), grouped by drawing number and LN item " +
            "code with total and remaining quantities and the actual QR code numbers in each group, optionally " +
            "filtered by a search term and/or Production Series.")]
        public async Task<List<AvailableQrGroupSummaryDto>> GetAvailableQrCodesAsync(
            [Description("Free-text search matched against LN item code/drawing number. Omit if not specified.")] string? searchQuery,
            [Description("Production Series names to filter by. Omit if not specified.")] List<string>? prodSeries)
        {
            var grouped = await _qrCodeService.GetAvailableQrPagedService(
                new GetAvailableQrRequest { SearchQuery = searchQuery, ProdSeries = prodSeries },
                pageNumber: 1, pageSize: 200);

            var result = new List<AvailableQrGroupSummaryDto>();
            foreach (var group in grouped.Data)
            {
                // The grouped query aggregates counts/quantities per drawing number and doesn't carry
                // individual QR code numbers - fetch them per group via the same per-component lookup
                // get_available_components uses, keyed by the group's drawing number ID.
                var components = await _precheckService.GetAvailableComponentService(new GetAvailableComponentsRequest
                {
                    DrawingNumberId = group.DrawingNumberId
                });

                result.Add(new AvailableQrGroupSummaryDto
                {
                    DrawingNumber = group.DrawingNumber,
                    LnItemCode = group.LnItemCode,
                    ProductionSeries = group.ProductionSeries,
                    ComponentType = group.ComponentType,
                    TotalQuantity = group.TotalQuantity,
                    TotalRemainingQuantity = group.TotalRemainingQuantity,
                    QrCount = group.QrCount,
                    QrCodeNumbers = components
                        .Select(c => c.QrCodeNumber)
                        .Where(q => !string.IsNullOrEmpty(q))
                        .ToList()!
                });
            }

            return result;
        }
    }
}
