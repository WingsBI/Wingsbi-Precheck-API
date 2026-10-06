using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Precheck.Models.DataModel;
using Precheck.Models.DataModel.Precheck;
using Precheck.Models.DTOs.Barcode;
using Precheck.Models.DTOs.Chatbot;
using Precheck.Models.DTOs.DrawingNumber;
using Precheck.Models.DTOs.IRNumber;
using Precheck.Models.DTOs.MSNNumber;
using Precheck.Models.DTOs.Precheck;
using Precheck.Models.DTOs.QRCodeDetails;
using Precheck.Service.Service.CommonService;
using Precheck.Service.Service.IdentifierService;
using Precheck.Service.Service.MaterialRequisitionService;
using Precheck.Service.Service.PrecheckService;
using Precheck.Service.Service.ProductionOrderService;
using Precheck.Service.Service.QRCodeService;
using Microsoft.AspNetCore.Http;

namespace Precheck.Host.Agents
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
        private readonly IIdentifierService _identifierService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatbotTools(
            ICommonService commonService,
            IProductionOrderService productionOrderService,
            IQRCodeService qrCodeService,
            IPrecheckService precheckService,
            IMaterialRequisitionService materialRequisitionService,
            IIdentifierService identifierService,
            IHttpContextAccessor httpContextAccessor)
        {
            _commonService = commonService;
            _productionOrderService = productionOrderService;
            _qrCodeService = qrCodeService;
            _precheckService = precheckService;
            _materialRequisitionService = materialRequisitionService;
            _identifierService = identifierService;
            _httpContextAccessor = httpContextAccessor;
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
        public Task<Precheck.Models.DTOs.ProductionOrder.ProductionOrderMasterDto?> GetProductionOrderDetailsAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber)
            => _productionOrderService.GetByProductionOrderNumberAsync(productionOrderNumber);

        [Description("List all Component Types in the system.")]
        public Task<List<Precheck.Models.DataModel.ComponentsType>> GetComponentTypesAsync()
            => _commonService.ComponentTypeService();

        [Description("Look up a single Component Type by its exact name.")]
        public Task<Precheck.Models.DataModel.ComponentsType> GetComponentTypeByNameAsync(
            [Description("The exact Component Type name.")] string name)
            => _commonService.ComponentTypeByNameService(name);

        [Description("List or search Drawing Numbers, optionally filtered by Component Type and/or a search term.")]
        public Task<List<Precheck.Models.DTOs.DrawingNumber.GetAllDrawingResponseDto>> GetDrawingNumbersAsync(
            [Description("Component Type name to filter by. Omit if not specified.")] string? componentType,
            [Description("Free-text search term to filter drawing numbers by. Omit if not specified.")] string? search)
            => _commonService.GetAllDrawingNumberService(new GetAllDrawingRequestDto
            {
                ComponentType = componentType,
                Search = search
            });

        [Description("List all Assemblies in the system.")]
        public Task<List<Precheck.Models.DataModel.Assembly.AssemblyNumbers>> GetAssembliesAsync()
            => _commonService.GetAllAssembly();

        [Description("Get Assembly-to-Drawing mappings, optionally filtered by LN item code.")]
        public Task<List<Precheck.Models.DTOs.Assembly.AssemblyDrawingMappingDto>> GetAssemblyDrawingMappingsAsync(
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

        // ---- Create IR/MSN numbers (first write/action tools) ----

        private static (string Raw, int Start, int End) ParseIdNumberRange(string idNumbers)
        {
            var raw = idNumbers.Trim();
            if (raw.Contains('-') && !raw.Contains(','))
            {
                var parts = raw.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out var s) && int.TryParse(parts[1], out var e))
                {
                    return (raw, Math.Min(s, e), Math.Max(s, e));
                }
            }

            var nums = raw.Split(',')
                .Select(p => int.TryParse(p.Trim(), out var n) ? n : (int?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            if (nums.Count > 0)
            {
                return (raw, nums.Min(), nums.Max());
            }

            throw new FormatException($"Could not parse ID number(s): '{idNumbers}'. Use a single number, a range like 1-5, or a comma list like 1,2,3.");
        }

        private static int? ResolveStageId(List<Stage> stages, string stageName) =>
            stages.FirstOrDefault(s => string.Equals(s.StageName, stageName, StringComparison.OrdinalIgnoreCase))?.Id;

        private (int? CreatedBy, int? DepartmentId, string? DepartmentName) GetCurrentUserClaims()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            int.TryParse(user?.FindFirst("id")?.Value, out var createdBy);
            int.TryParse(user?.FindFirst("deptid")?.Value, out var deptId);
            return (createdBy > 0 ? createdBy : null, deptId > 0 ? deptId : null, user?.FindFirst("department")?.Value);
        }

        [Description("Create a new IR (Inspection Report) number for a Manufacturing Item, identified by Production Order " +
            "number. ALWAYS call this first with confirmed=false to preview (no record is created yet) - show the user the " +
            "resolved Part Number/Item Code/Production Series and all entered values, and ask them to confirm. Only call " +
            "again with confirmed=true after the user has explicitly confirmed (e.g. 'yes', 'go ahead', 'confirm') in their " +
            "most recent message - never set confirmed=true otherwise.")]
        public async Task<CreateIrMsnResponseDto> CreateIrNumberAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber,
            [Description("ID number(s): a single number, a range like '1-5', or a comma list like '1,2,3'.")] string idNumbers,
            [Description("The inspection Stage name (e.g. as returned by the stages list).")] string stage,
            [Description("Quantity. Omit if not specified.")] int? quantity,
            [Description("Build number. Omit if not specified.")] string? buildNumber,
            [Description("Operation number. Omit if not specified.")] string? operationNumber,
            [Description("Remark/notes. Omit if not specified.")] string? remark,
            [Description("Set true ONLY after the user has explicitly confirmed the previewed details.")] bool confirmed)
        {
            var po = await _productionOrderService.GetByProductionOrderNumberAsync(productionOrderNumber);
            if (po == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Production Order '{productionOrderNumber}' was not found." };
            }
            if (po.ProdSeriesId == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Production Order '{productionOrderNumber}' has no resolved Production Series - cannot generate a number for it." };
            }

            var stages = await _commonService.GetIRStagesService();
            var stageId = ResolveStageId(stages, stage);
            if (stageId == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Stage '{stage}' not recognized. Valid stages: {string.Join(", ", stages.Select(s => s.StageName))}." };
            }

            (string raw, int start, int end) ids;
            try { ids = ParseIdNumberRange(idNumbers); }
            catch (FormatException ex) { return new CreateIrMsnResponseDto { Message = ex.Message }; }

            var preview = new CreateIrMsnResponseDto
            {
                ProductionOrderNumber = productionOrderNumber,
                PartNumber = po.DrawingNumber,
                ItemCode = po.LnItemCode,
                ProductionSeries = po.ProductionSeries,
                ProjectNumber = po.ProjectNumber,
                IdNumbers = ids.raw,
                Quantity = quantity,
                BuildNumber = buildNumber,
                Stage = stage,
                OperationNumber = operationNumber,
                Remark = remark
            };

            if (!confirmed)
            {
                preview.Message = "Review the details above and confirm to generate the IR number.";
                return preview;
            }

            var (createdBy, deptId, deptName) = GetCurrentUserClaims();
            try
            {
                var result = await _identifierService.InsertIRNumberAsync(new IRNumberDto
                {
                    ProductionOrderNumber = productionOrderNumber,
                    DrawingNumberId = po.DrawingNumberId,
                    ProdSeriesId = po.ProdSeriesId.Value,
                    LnItemCode = po.LnItemCode,
                    ItemDescription = po.ItemDescription,
                    ProjectNumber = po.ProjectNumber,
                    IdNumberRange = ids.raw,
                    IdNumberStart = ids.start,
                    IdNumberEnd = ids.end,
                    Quantity = quantity,
                    BuildNumber = buildNumber,
                    StageId = stageId,
                    OperationNumber = operationNumber,
                    Remark = remark,
                    CreatedBy = createdBy,
                    DepartmentId = deptId,
                    DepartmentName = deptName
                });
                preview.Created = true;
                preview.GeneratedNumber = result.IrNumber;
                preview.Message = $"IR number {result.IrNumber} created successfully.";
            }
            catch (ValidationException ex)
            {
                preview.Message = ex.Message;
            }

            return preview;
        }

        [Description("Create a new MSN (Memo Stage Number) for a Manufacturing Item, identified by Production Order " +
            "number. ALWAYS call this first with confirmed=false to preview (no record is created yet) - show the user " +
            "the resolved Part Number/Item Code/Production Series and all entered values, and ask them to confirm. Only " +
            "call again with confirmed=true after the user has explicitly confirmed (e.g. 'yes', 'go ahead', 'confirm') " +
            "in their most recent message - never set confirmed=true otherwise.")]
        public async Task<CreateIrMsnResponseDto> CreateMsnNumberAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber,
            [Description("ID number(s): a single number, a range like '1-5', or a comma list like '1,2,3'.")] string idNumbers,
            [Description("The inspection Stage name (e.g. as returned by the stages list).")] string stage,
            [Description("Quantity. Omit if not specified.")] int? quantity,
            [Description("Build number. Omit if not specified.")] string? buildNumber,
            [Description("Operation number. Omit if not specified.")] string? operationNumber,
            [Description("Description. Omit if not specified.")] string? description,
            [Description("Remark/notes. Omit if not specified.")] string? remark,
            [Description("Set true ONLY after the user has explicitly confirmed the previewed details.")] bool confirmed)
        {
            var po = await _productionOrderService.GetByProductionOrderNumberAsync(productionOrderNumber);
            if (po == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Production Order '{productionOrderNumber}' was not found." };
            }
            if (po.ProdSeriesId == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Production Order '{productionOrderNumber}' has no resolved Production Series - cannot generate a number for it." };
            }

            var stages = await _commonService.GetMSNStagesService();
            var stageId = ResolveStageId(stages, stage);
            if (stageId == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Stage '{stage}' not recognized. Valid stages: {string.Join(", ", stages.Select(s => s.StageName))}." };
            }

            (string raw, int start, int end) ids;
            try { ids = ParseIdNumberRange(idNumbers); }
            catch (FormatException ex) { return new CreateIrMsnResponseDto { Message = ex.Message }; }

            var preview = new CreateIrMsnResponseDto
            {
                ProductionOrderNumber = productionOrderNumber,
                PartNumber = po.DrawingNumber,
                ItemCode = po.LnItemCode,
                ProductionSeries = po.ProductionSeries,
                ProjectNumber = po.ProjectNumber,
                IdNumbers = ids.raw,
                Quantity = quantity,
                BuildNumber = buildNumber,
                Stage = stage,
                OperationNumber = operationNumber,
                Remark = remark
            };

            if (!confirmed)
            {
                preview.Message = "Review the details above and confirm to generate the MSN number.";
                return preview;
            }

            var (createdBy, deptId, deptName) = GetCurrentUserClaims();
            try
            {
                var result = await _identifierService.InsertMSNNumberAsync(new MSNNumberDto
                {
                    ProductionOrderNumber = productionOrderNumber,
                    DrawingNumberId = po.DrawingNumberId,
                    ProdSeriesId = po.ProdSeriesId.Value,
                    LnItemCode = po.LnItemCode,
                    ItemDescription = po.ItemDescription,
                    ProjectNumber = po.ProjectNumber,
                    IdNumberRange = ids.raw,
                    IdNumberStart = ids.start,
                    IdNumberEnd = ids.end,
                    Quantity = quantity,
                    BuildNumber = buildNumber,
                    StageId = stageId,
                    OperationNumber = operationNumber,
                    Description = description,
                    Remark = remark,
                    CreatedBy = createdBy,
                    DepartmentId = deptId,
                    DepartmentName = deptName
                });
                preview.Created = true;
                preview.GeneratedNumber = result.MsnNumber;
                preview.Message = $"MSN number {result.MsnNumber} created successfully.";
            }
            catch (ValidationException ex)
            {
                preview.Message = ex.Message;
            }

            return preview;
        }

        [Description("Create a new IR (Inspection Report) number for a Purchase Item, identified by Purchase Order " +
            "number, drawing/part number, and Production Series (no Production Order lookup is available for purchase " +
            "items, so these must be given explicitly). ALWAYS call this first with confirmed=false to preview (no " +
            "record is created yet) and ask the user to confirm. Only call again with confirmed=true after the user " +
            "has explicitly confirmed - never set confirmed=true otherwise.")]
        public async Task<CreateIrMsnResponseDto> CreateStandardIrNumberAsync(
            [Description("The exact Purchase Order number.")] string purchaseOrderNumber,
            [Description("The drawing number (= Part Number).")] string drawingNumber,
            [Description("The Production Series name (e.g. 'H').")] string productionSeries,
            [Description("ID number(s): a single number, a range like '1-5', or a comma list like '1,2,3'.")] string idNumbers,
            [Description("Quantity (required).")] int quantity,
            [Description("The inspection Stage name (e.g. as returned by the stages list).")] string stage,
            [Description("Supplier name. Omit if not specified.")] string? supplier,
            [Description("Operation number. Omit if not specified.")] string? operationNumber,
            [Description("Remark/notes. Omit if not specified.")] string? remark,
            [Description("Set true ONLY after the user has explicitly confirmed the previewed details.")] bool confirmed)
        {
            var drawings = await _commonService.GetAllDrawingNumberService(new GetAllDrawingRequestDto { Search = drawingNumber });
            var drawing = drawings.FirstOrDefault(d => string.Equals(d.DrawingNumber, drawingNumber, StringComparison.OrdinalIgnoreCase));
            if (drawing == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Drawing/Part number '{drawingNumber}' was not found." };
            }

            var series = await _commonService.ProductionSeriesByNameService(productionSeries);
            if (series == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Production Series '{productionSeries}' was not found." };
            }

            var stages = await _commonService.GetIRStagesService();
            var stageId = ResolveStageId(stages, stage);
            if (stageId == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Stage '{stage}' not recognized. Valid stages: {string.Join(", ", stages.Select(s => s.StageName))}." };
            }

            (string raw, int start, int end) ids;
            try { ids = ParseIdNumberRange(idNumbers); }
            catch (FormatException ex) { return new CreateIrMsnResponseDto { Message = ex.Message }; }

            var preview = new CreateIrMsnResponseDto
            {
                PurchaseOrderNumber = purchaseOrderNumber,
                PartNumber = drawing.DrawingNumber,
                ItemCode = drawing.LnItemCode,
                ProductionSeries = productionSeries,
                IdNumbers = ids.raw,
                Quantity = quantity,
                Stage = stage,
                OperationNumber = operationNumber,
                Remark = remark
            };

            if (!confirmed)
            {
                preview.Message = "Review the details above and confirm to generate the IR number.";
                return preview;
            }

            var (createdBy, deptId, deptName) = GetCurrentUserClaims();
            try
            {
                var result = await _identifierService.InsertStandardIRNumberAsync(new StandardIRNumberDto
                {
                    PurchaseOrderNumber = purchaseOrderNumber,
                    DrawingNumberId = drawing.Id,
                    LnItemCode = drawing.LnItemCode,
                    ItemDescription = drawing.Nomenclature,
                    ProdSeriesId = series.Id,
                    IdNumberRange = ids.raw,
                    IdNumberStart = ids.start,
                    IdNumberEnd = ids.end,
                    Quantity = quantity,
                    StageId = stageId,
                    Supplier = supplier,
                    OperationNumber = operationNumber,
                    Remark = remark,
                    CreatedBy = createdBy,
                    DepartmentId = deptId,
                    DepartmentName = deptName
                });
                preview.Created = true;
                preview.GeneratedNumber = result.IrNumber;
                preview.Message = $"IR number {result.IrNumber} created successfully.";
            }
            catch (ValidationException ex)
            {
                preview.Message = ex.Message;
            }

            return preview;
        }

        [Description("Create a new MSN (Memo Stage Number) for a Purchase Item, identified by Purchase Order number, " +
            "drawing/part number, and Production Series (no Production Order lookup is available for purchase items, " +
            "so these must be given explicitly). ALWAYS call this first with confirmed=false to preview (no record is " +
            "created yet) and ask the user to confirm. Only call again with confirmed=true after the user has " +
            "explicitly confirmed - never set confirmed=true otherwise.")]
        public async Task<CreateIrMsnResponseDto> CreateStandardMsnNumberAsync(
            [Description("The exact Purchase Order number.")] string purchaseOrderNumber,
            [Description("The drawing number (= Part Number).")] string drawingNumber,
            [Description("The Production Series name (e.g. 'H').")] string productionSeries,
            [Description("ID number(s): a single number, a range like '1-5', or a comma list like '1,2,3'.")] string idNumbers,
            [Description("Quantity (required).")] int quantity,
            [Description("The inspection Stage name (e.g. as returned by the stages list).")] string stage,
            [Description("Supplier name. Omit if not specified.")] string? supplier,
            [Description("Operation number. Omit if not specified.")] string? operationNumber,
            [Description("Remark/notes. Omit if not specified.")] string? remark,
            [Description("Set true ONLY after the user has explicitly confirmed the previewed details.")] bool confirmed)
        {
            var drawings = await _commonService.GetAllDrawingNumberService(new GetAllDrawingRequestDto { Search = drawingNumber });
            var drawing = drawings.FirstOrDefault(d => string.Equals(d.DrawingNumber, drawingNumber, StringComparison.OrdinalIgnoreCase));
            if (drawing == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Drawing/Part number '{drawingNumber}' was not found." };
            }

            var series = await _commonService.ProductionSeriesByNameService(productionSeries);
            if (series == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Production Series '{productionSeries}' was not found." };
            }

            var stages = await _commonService.GetMSNStagesService();
            var stageId = ResolveStageId(stages, stage);
            if (stageId == null)
            {
                return new CreateIrMsnResponseDto { Message = $"Stage '{stage}' not recognized. Valid stages: {string.Join(", ", stages.Select(s => s.StageName))}." };
            }

            (string raw, int start, int end) ids;
            try { ids = ParseIdNumberRange(idNumbers); }
            catch (FormatException ex) { return new CreateIrMsnResponseDto { Message = ex.Message }; }

            var preview = new CreateIrMsnResponseDto
            {
                PurchaseOrderNumber = purchaseOrderNumber,
                PartNumber = drawing.DrawingNumber,
                ItemCode = drawing.LnItemCode,
                ProductionSeries = productionSeries,
                IdNumbers = ids.raw,
                Quantity = quantity,
                Stage = stage,
                OperationNumber = operationNumber,
                Remark = remark
            };

            if (!confirmed)
            {
                preview.Message = "Review the details above and confirm to generate the MSN number.";
                return preview;
            }

            var (createdBy, deptId, deptName) = GetCurrentUserClaims();
            try
            {
                var result = await _identifierService.InsertStandardMSNNumberAsync(new StandardMSNNumberDto
                {
                    PurchaseOrderNumber = purchaseOrderNumber,
                    DrawingNumberId = drawing.Id,
                    LnItemCode = drawing.LnItemCode,
                    ItemDescription = drawing.Nomenclature,
                    ProdSeriesId = series.Id,
                    IdNumberRange = ids.raw,
                    IdNumberStart = ids.start,
                    IdNumberEnd = ids.end,
                    Quantity = quantity,
                    StageId = stageId,
                    Supplier = supplier,
                    OperationNumber = operationNumber,
                    Remark = remark,
                    CreatedBy = createdBy,
                    DepartmentId = deptId,
                    DepartmentName = deptName
                });
                preview.Created = true;
                preview.GeneratedNumber = result.MsnNumber;
                preview.Message = $"MSN number {result.MsnNumber} created successfully.";
            }
            catch (ValidationException ex)
            {
                preview.Message = ex.Message;
            }

            return preview;
        }

        // ---- Create QR Code (Manufacturing Item only - ID/FIM/SI/BATCH component types) ----

        [Description("Generate QR code(s) for a Manufacturing Item component, identified by Production Order number " +
            "and Component Type (ID, FIM, SI, or BATCH). For ComponentType 'ID', provide idNumbers (one or more serial " +
            "ID numbers for the drawing); for 'FIM'/'SI'/'BATCH', provide quantity instead. ALWAYS call this first with " +
            "confirmed=false to preview (no QR codes are generated yet) - show the user the resolved Part Number/Item " +
            "Code/Production Series and all entered values, and ask them to confirm. Only call again with " +
            "confirmed=true after the user has explicitly confirmed (e.g. 'yes', 'go ahead', 'confirm') in their most " +
            "recent message - never set confirmed=true otherwise. Note: for 'ID' components on certain drawings, QR " +
            "generation can be blocked if Precheck isn't complete yet for that ID - if so, tell the user which " +
            "components still need precheck. For ComponentType 'ID', valid ID numbers are constrained to the " +
            "Production Order's own ID range - if you don't yet know it, call with idNumbers omitted first; the " +
            "tool will tell you the exact valid range to offer the user, rather than giving arbitrary examples. " +
            "The result distinguishes GeneratedQrCodeNumbers (newly created) from AlreadyExistingQrCodeNumbers (that " +
            "ID already had a QR code, so nothing new was made for it) - report both clearly, never claim an " +
            "already-existing QR code was newly generated. An IR number is REQUIRED and gets linked to the QR code(s): " +
            "if irNumber is omitted the tool returns the IR numbers available for this Production Order's drawing/series " +
            "- show them and ask the user which one to use; never guess or pick one for them. An MSN number is ALSO " +
            "REQUIRED and linked the same way: if msnNumber is omitted the tool returns the available MSN numbers - ask " +
            "the user to pick one, or to answer 'NA' if no MSN applies.")]
        public async Task<CreateQrCodeResponseDto> CreateQrCodeAsync(
            [Description("The exact Production Order number.")] string productionOrderNumber,
            [Description("Component Type: ID, FIM, SI, or BATCH.")] string componentType,
            [Description("Required for ComponentType 'ID': one or more ID numbers - a single number, a range like " +
                "'1-5', or a comma list like '1,2,3'. Omit for FIM/SI/BATCH.")] string? idNumbers,
            [Description("Required for ComponentType 'FIM'/'SI'/'BATCH': the quantity. Omit for 'ID'.")] decimal? quantity,
            [Description("Build number. Omit if not specified.")] string? buildNumber,
            [Description("Operation number. Omit if not specified.")] string? operationNumber,
            [Description("Remark/notes. Omit if not specified.")] string? remark,
            [Description("The exact IR number to link to the QR code(s), as chosen by the user. Required - omit only to " +
                "get the list of available IR numbers.")] string? irNumber,
            [Description("The exact MSN number to link, as chosen by the user, or 'NA' if the user says none applies. " +
                "Required - omit only to get the list of available MSN numbers.")] string? msnNumber,
            [Description("Set true ONLY after the user has explicitly confirmed the previewed details.")] bool confirmed)
        {
            var po = await _productionOrderService.GetByProductionOrderNumberAsync(productionOrderNumber);
            if (po == null)
            {
                return new CreateQrCodeResponseDto { Message = $"Production Order '{productionOrderNumber}' was not found." };
            }
            if (po.ProdSeriesId == null || po.DrawingNumberId == null)
            {
                return new CreateQrCodeResponseDto { Message = $"Production Order '{productionOrderNumber}' has no resolved Production Series/Drawing Number - cannot generate a QR code for it." };
            }

            var componentTypeRecord = await _commonService.ComponentTypeByNameService(componentType);
            if (componentTypeRecord == null)
            {
                var validTypes = await _commonService.ComponentTypeService();
                return new CreateQrCodeResponseDto { Message = $"Component Type '{componentType}' not recognized. Valid types: {string.Join(", ", validTypes.Select(t => t.ComponentType))}." };
            }

            var isIdType = string.Equals(componentTypeRecord.ComponentType, "ID", StringComparison.OrdinalIgnoreCase);
            if (isIdType && string.IsNullOrWhiteSpace(idNumbers))
            {
                var rangeHint = po.StartIdNumber.HasValue && po.EndIdNumber.HasValue
                    ? $" This Production Order covers ID numbers {po.StartIdNumber} to {po.EndIdNumber} - pick one or more within that range (single, range like '{po.StartIdNumber}-{po.EndIdNumber}', or a comma list)."
                    : string.Empty;
                return new CreateQrCodeResponseDto { Message = $"ID number(s) are required for Component Type 'ID'.{rangeHint}" };
            }
            if (!isIdType && quantity == null)
            {
                return new CreateQrCodeResponseDto { Message = $"Quantity is required for Component Type '{componentTypeRecord.ComponentType}'." };
            }

            if (isIdType && po.StartIdNumber.HasValue && po.EndIdNumber.HasValue)
            {
                try
                {
                    var (_, reqStart, reqEnd) = ParseIdNumberRange(idNumbers!);
                    if (reqStart < po.StartIdNumber.Value || reqEnd > po.EndIdNumber.Value)
                    {
                        return new CreateQrCodeResponseDto { Message = $"ID number(s) '{idNumbers}' fall outside this Production Order's valid range ({po.StartIdNumber}-{po.EndIdNumber}). Please specify ID number(s) within that range." };
                    }
                }
                catch (FormatException ex)
                {
                    return new CreateQrCodeResponseDto { Message = ex.Message };
                }
            }

            // IR number is mandatory on the QR Code form and is stored against every generated QR code.
            var availableIrNumbers = (await _commonService.IRNumberByDrawingNumberService(new GetIRNumberByDrawingNumberRequest
            {
                DrawingNumber = po.DrawingNumber,
                Productionseries = po.ProductionSeries
            })).Where(r => r.Id != null && !string.IsNullOrWhiteSpace(r.IrNumber)).ToList();

            if (string.IsNullOrWhiteSpace(irNumber))
            {
                var listing = availableIrNumbers.Count > 0
                    ? $" Available IR numbers: {string.Join(", ", availableIrNumbers.Select(r => r.IrNumber).Distinct())}."
                    : " No IR numbers exist yet for this drawing/series - create one first.";
                return new CreateQrCodeResponseDto { Message = $"An IR number is required to generate a QR code - ask the user which one to link.{listing}" };
            }

            var selectedIr = availableIrNumbers.FirstOrDefault(r => string.Equals(r.IrNumber, irNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selectedIr == null)
            {
                var listing = availableIrNumbers.Count > 0
                    ? $" Available IR numbers: {string.Join(", ", availableIrNumbers.Select(r => r.IrNumber).Distinct())}."
                    : string.Empty;
                return new CreateQrCodeResponseDto { Message = $"IR number '{irNumber}' was not found for drawing '{po.DrawingNumber}' / series '{po.ProductionSeries}'.{listing}" };
            }

            // MSN number is also mandatory on the form, but "NA" is a valid answer (nothing is linked).
            var availableMsnNumbers = (await _commonService.MSNNumberByDrawingNumberService(new GetMSNNumberByDrawingNumberRequest
            {
                DrawingNumber = po.DrawingNumber,
                Productionseries = po.ProductionSeries
            })).Where(r => r.Id != null && !string.IsNullOrWhiteSpace(r.MsnNumber)).ToList();

            if (string.IsNullOrWhiteSpace(msnNumber))
            {
                var listing = availableMsnNumbers.Count > 0
                    ? $" Available MSN numbers: {string.Join(", ", availableMsnNumbers.Select(r => r.MsnNumber).Distinct())}, or 'NA'."
                    : " No MSN numbers exist for this drawing/series - the user can answer 'NA'.";
                return new CreateQrCodeResponseDto { Message = $"An MSN number is required to generate a QR code - ask the user which one to link.{listing}" };
            }

            MSNNumbers? selectedMsn = null;
            if (!string.Equals(msnNumber.Trim(), "NA", StringComparison.OrdinalIgnoreCase))
            {
                selectedMsn = availableMsnNumbers.FirstOrDefault(r => string.Equals(r.MsnNumber, msnNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                if (selectedMsn == null)
                {
                    var listing = availableMsnNumbers.Count > 0
                        ? $" Available MSN numbers: {string.Join(", ", availableMsnNumbers.Select(r => r.MsnNumber).Distinct())}, or 'NA'."
                        : " Only 'NA' is possible.";
                    return new CreateQrCodeResponseDto { Message = $"MSN number '{msnNumber}' was not found for drawing '{po.DrawingNumber}' / series '{po.ProductionSeries}'.{listing}" };
                }
            }

            var preview = new CreateQrCodeResponseDto
            {
                ProductionOrderNumber = productionOrderNumber,
                PartNumber = po.DrawingNumber,
                ItemCode = po.LnItemCode,
                ProductionSeries = po.ProductionSeries,
                IrNumber = selectedIr.IrNumber,
                MsnNumber = selectedMsn?.MsnNumber ?? "NA",
                Unit = po.UnitName,
                ComponentType = componentTypeRecord.ComponentType,
                IdNumbers = isIdType ? idNumbers : null,
                Quantity = isIdType ? null : quantity,
                BuildNumber = buildNumber,
                OperationNumber = operationNumber,
                Remark = remark
            };

            if (!confirmed)
            {
                preview.Message = "Review the details above and confirm to generate the QR code(s).";
                return preview;
            }

            var (createdBy, _, _) = GetCurrentUserClaims();
            try
            {
                var results = await _qrCodeService.InsertQRCodeDetailsAsync(new QRCodeDetailsDto
                {
                    ProductionSeriesId = po.ProdSeriesId.Value,
                    DrawingNumberId = po.DrawingNumberId.Value,
                    ComponentTypeId = componentTypeRecord.ID,
                    IrNumberId = selectedIr.Id,
                    MsnNumberId = selectedMsn?.Id,
                    UnitId = po.UnitId,
                    ProductionOrderNumber = productionOrderNumber,
                    CustomIdRange = isIdType ? idNumbers : null,
                    Quantity = isIdType ? 1 : quantity!.Value,
                    BuildNumber = buildNumber,
                    OperationNo = operationNumber,
                    Remarks = remark,
                    CreatedBy = createdBy,
                    IsActive = true
                });

                var nonNullResults = results.Where(r => r != null).Select(r => r!).ToList();
                preview.GeneratedQrCodeNumbers = nonNullResults.Where(r => r.IsNewQrCode).Select(r => r.QrCodeNumber).ToList();
                preview.AlreadyExistingQrCodeNumbers = nonNullResults.Where(r => !r.IsNewQrCode).Select(r => r.QrCodeNumber).ToList();

                var messageParts = new List<string>();
                if (preview.GeneratedQrCodeNumbers.Count > 0)
                {
                    preview.Created = true;
                    messageParts.Add($"Generated {preview.GeneratedQrCodeNumbers.Count} new QR code(s): {string.Join(", ", preview.GeneratedQrCodeNumbers)}.");
                }
                if (preview.AlreadyExistingQrCodeNumbers.Count > 0)
                {
                    messageParts.Add($"{preview.AlreadyExistingQrCodeNumbers.Count} of the requested ID(s) already had a QR code - no duplicate was created: {string.Join(", ", preview.AlreadyExistingQrCodeNumbers)}.");
                }
                preview.Message = messageParts.Count > 0
                    ? string.Join(" ", messageParts)
                    : "No QR codes were generated.";
            }
            catch (ValidationException ex)
            {
                preview.Message = TryParsePrecheckValidationMessage(ex.Message) ?? ex.Message;
            }

            return preview;
        }

        private static string? TryParsePrecheckValidationMessage(string rawMessage)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Validation.PrecheckValidationError>(rawMessage);
                if (parsed?.UnsubmitedComponents is { Count: > 0 })
                {
                    var drawings = string.Join(", ", parsed.UnsubmitedComponents.Select(c => c.DrawingNumber));
                    return $"Precheck is not completed for the following component(s): {drawings}. Complete precheck for these first before generating QR codes.";
                }
            }
            catch (JsonException)
            {
                // Not a structured precheck-validation error - fall back to the raw message.
            }
            return null;
        }

        // ---- Make Precheck (the core scan-and-verify action) ----

        [Description("Scan and verify ONE component (by its QR code) against a specific line in a target assembly's BOM - " +
            "the core Precheck verification action. The 'target assembly' (the unit being verified) is identified by " +
            "Production Order number + Production Series + its own assembly ID number + its own assembly drawing " +
            "number; the 'component' being scanned is identified by its own, separate component drawing number and " +
            "QR code. Before calling this, consider calling get_precheck_by_po_series_id for the target assembly to " +
            "see which component drawing(s) are still Pending, and get_available_components_by_filter or " +
            "get_qr_code_details to confirm the QR code is available. ALWAYS call this first with confirmed=false to " +
            "preview (nothing is written yet) - show the user everything resolved and ask them to confirm. Only call " +
            "again with confirmed=true after the user has explicitly confirmed (e.g. 'yes', 'go ahead', 'confirm') in " +
            "their most recent message - never set confirmed=true otherwise. This tool only ACCEPTS a scanned " +
            "component; it does not reject one - rejecting a non-conforming component with remarks is a separate " +
            "action not yet available.")]
        public async Task<MakePrecheckResponseDto> MakePrecheckAsync(
            [Description("The exact Production Order number of the target assembly.")] string productionOrderNumber,
            [Description("The Production Series name of the target assembly (e.g. 'H').")] string productionSeries,
            [Description("The target assembly's own ID number (which unit is being verified).")] int assemblyIdNumber,
            [Description("The target assembly's own drawing number.")] string assemblyDrawingNumber,
            [Description("The specific component's drawing number - the BOM line being scanned/verified.")] string componentDrawingNumber,
            [Description("The scanned component's exact QR code number.")] string qrCodeNumber,
            [Description("Component Type: ID, FIM, SI, or BATCH.")] string componentType,
            [Description("The quantity being consumed from this scanned component.")] decimal updatedQuantity,
            [Description("Remarks/notes. Omit if not specified.")] string? remarks,
            [Description("Set true ONLY after the user has explicitly confirmed the previewed details.")] bool confirmed)
        {
            var series = await _commonService.ProductionSeriesByNameService(productionSeries);
            if (series == null)
            {
                return new MakePrecheckResponseDto { Message = $"Production Series '{productionSeries}' was not found." };
            }

            // The target assembly's project (tbl_projectdetails) is keyed by PO + series + ID number and is
            // stored against the Production Order's OWN drawing. MakePrecheck filters on that drawing id
            // (ConsumedInDrawingNumberID), so it must come from the PO itself - not from a name the model
            // supplied, which can be a different/parent drawing and makes MakePrecheck fail with
            // "Drawing ID ... is not valid" even though the same call works via the API.
            var po = await _productionOrderService.GetByProductionOrderNumberAsync(productionOrderNumber);
            if (po == null || po.DrawingNumberId == null || string.IsNullOrWhiteSpace(po.DrawingNumber))
            {
                return new MakePrecheckResponseDto { Message = $"Production Order '{productionOrderNumber}' was not found or has no drawing number." };
            }
            if (po.ProdSeriesId != series.Id)
            {
                return new MakePrecheckResponseDto { Message = $"Production Order '{productionOrderNumber}' belongs to Production Series '{po.ProductionSeries}', not '{productionSeries}'." };
            }
            if (!string.Equals(po.DrawingNumber, assemblyDrawingNumber, StringComparison.OrdinalIgnoreCase))
            {
                return new MakePrecheckResponseDto { Message = $"Assembly drawing '{assemblyDrawingNumber}' does not match Production Order '{productionOrderNumber}', whose assembly drawing is '{po.DrawingNumber}'. Use '{po.DrawingNumber}' as the assembly drawing number." };
            }
            var assemblyDrawing = new { Id = po.DrawingNumberId.Value, DrawingNumber = po.DrawingNumber };

            var componentDrawings = await _commonService.GetAllDrawingNumberService(new GetAllDrawingRequestDto { Search = componentDrawingNumber });
            var componentDrawing = componentDrawings.FirstOrDefault(d => string.Equals(d.DrawingNumber, componentDrawingNumber, StringComparison.OrdinalIgnoreCase));
            if (componentDrawing == null)
            {
                return new MakePrecheckResponseDto { Message = $"Component drawing number '{componentDrawingNumber}' was not found." };
            }

            var componentTypeRecord = await _commonService.ComponentTypeByNameService(componentType);
            if (componentTypeRecord == null)
            {
                var validTypes = await _commonService.ComponentTypeService();
                return new MakePrecheckResponseDto { Message = $"Component Type '{componentType}' not recognized. Valid types: {string.Join(", ", validTypes.Select(t => t.ComponentType))}." };
            }

            // The underlying UPDATE_PROJECT_PRECHECK_DETAIL query sets isprecheckcomplete purely from
            // the @remainingquantity parameter we supply (= 0 -> complete) - it does NOT compute it
            // from UpdatedQuantity itself. We must find the BOM line's current remaining quantity and
            // compute what it becomes after this scan ourselves, or the line silently never completes
            // (RemainingQuantity stays null -> "NULL = 0" is never true in SQL).
            var assemblyPrecheckRows = await _precheckService.ViewPrecheckDetailsService(new ViewPreCheckRequestDto
            {
                ProductionOrderNumber = productionOrderNumber,
                ProductionSeriesId = series.Id,
                DrawingNumberId = assemblyDrawing.Id,
                Id = assemblyIdNumber
            });
            var targetLine = assemblyPrecheckRows.FirstOrDefault(r => r.DrawingNumberId == componentDrawing.Id && !r.IsPrecheckComplete);
            if (targetLine == null)
            {
                return new MakePrecheckResponseDto { Message = $"No pending Precheck line found for component drawing '{componentDrawing.DrawingNumber}' on this assembly (PO {productionOrderNumber}, Series {productionSeries}, Assembly ID {assemblyIdNumber}). It may already be complete, or was never set up as part of this assembly's BOM." };
            }

            // The precheck row is updated from the scanned QR's own details (drawing, unit, IR/MSN/MRIR, ID),
            // exactly as the Excel import does - the API's UPDATE overwrites those columns, so leaving them
            // out would blank them on the row.
            var qrDetails = await _qrCodeService.GetQRCodeDetailsService(qrCodeNumber, null);
            if (qrDetails == null)
            {
                return new MakePrecheckResponseDto { Message = $"QR code '{qrCodeNumber}' was not found." };
            }
            if (qrDetails.DrawingNumberId != componentDrawing.Id)
            {
                return new MakePrecheckResponseDto { Message = $"QR code '{qrCodeNumber}' is tagged for drawing '{qrDetails.DrawingNumber}', not '{componentDrawing.DrawingNumber}'. Scan a QR code that belongs to the component drawing." };
            }

            var currentRemainingQuantity = targetLine.RemainingQuantity ?? targetLine.Quantity ?? 0;
            // Never consume more than the line still needs; a smaller scan leaves a shortfall that is carried
            // forward to a new row after the scan (same as the Excel import).
            var consumedQuantity = Math.Min(updatedQuantity, currentRemainingQuantity);
            var newRemainingQuantity = currentRemainingQuantity - consumedQuantity;

            var preview = new MakePrecheckResponseDto
            {
                ProductionOrderNumber = productionOrderNumber,
                ProductionSeries = productionSeries,
                AssemblyIdNumber = assemblyIdNumber,
                AssemblyDrawingNumber = assemblyDrawing.DrawingNumber,
                ComponentDrawingNumber = componentDrawing.DrawingNumber,
                QrCodeNumber = qrCodeNumber,
                ComponentType = componentTypeRecord.ComponentType,
                UpdatedQuantity = consumedQuantity,
                Remarks = remarks,
                RemainingQuantity = newRemainingQuantity
            };

            if (!confirmed)
            {
                preview.Message = newRemainingQuantity == 0
                    ? "Review the details above and confirm to verify (scan) this component. This will fully consume the required quantity and mark the line complete."
                    : $"Review the details above and confirm to verify (scan) this component. {newRemainingQuantity} will remain outstanding on this line after this scan (not yet complete).";
                return preview;
            }

            var (createdBy, _, _) = GetCurrentUserClaims();
            try
            {
                var results = await _precheckService.MakePrecheck(new List<PrecheckRequestDto>
                {
                    new PrecheckRequestDto
                    {
                        // Id is the PRECHECK ROW id (tbl_projectprecheckdetails.Id) being updated - not the
                        // assembly ID number. Passing the assembly ID made the UPDATE hit whichever row
                        // happened to have that id, so the real line never completed.
                        Id = targetLine.PrecheckDetailsId,
                        QrCodeNumber = qrCodeNumber,
                        ConsumedDrawingNo = qrDetails.DrawingNumber,
                        DrawingNumberId = componentDrawing.Id,
                        ComponentType = componentTypeRecord.ComponentType,
                        Quantity = consumedQuantity,
                        UpdatedQuantity = consumedQuantity,
                        RemainingQuantity = newRemainingQuantity,
                        Unit = qrDetails.UnitName,
                        IrNumber = qrDetails.IrNumber,
                        MsnNumber = qrDetails.MsnNumber,
                        MrirNumber = qrDetails.MRIRNumber,
                        LnItemCode = qrDetails.LnItemCode,
                        IdNumbers = qrDetails.IdNumber,
                        Remarks = remarks,
                        ProductionOrderNumber = productionOrderNumber,
                        ConsumeInProductionOrderNumber = productionOrderNumber,
                        AssemblyDrawingNo = assemblyDrawing.DrawingNumber,
                        ConsumedInId = assemblyIdNumber,
                        ConsumedInProdSeriesID = series.Id,
                        ConsumedInDrawingNumberID = assemblyDrawing.Id,
                        CreatedBy = createdBy ?? 0
                    }
                });

                if (newRemainingQuantity > 0)
                {
                    // Partial scan: close this row and open a fresh one carrying the shortfall forward.
                    await _precheckService.PrecheckForRemainingQuantityService(new RejectPrecheckRequestDto
                    {
                        PrecheckDetailsId = targetLine.PrecheckDetailsId,
                        DrawingNumberId = componentDrawing.Id,
                        ProductionSeriesId = series.Id,
                        IdNumber = assemblyIdNumber.ToString(),
                        ComponentType = componentTypeRecord.ComponentType,
                        RemainingQuantity = newRemainingQuantity,
                        DuplicateRemarks = "Auto-duplicated: remaining quantity carried forward from chatbot scan",
                        CreatedBy = createdBy ?? 0
                    });
                }

                var matched = results.FirstOrDefault(r => r.DrawingNumberId == componentDrawing.Id) ?? results.LastOrDefault();
                preview.Created = true;
                preview.PrecheckStatus = matched?.PrecheckStatus;
                preview.IsPrecheckComplete = matched?.IsPrecheckComplete;
                preview.RemainingQuantity = matched?.RemainingQuantity;
                preview.Message = matched != null
                    ? $"Component verified. Precheck status for this line: {matched.PrecheckStatus}."
                    : "Component verified successfully.";
            }
            catch (ApplicationException ex)
            {
                preview.Message = ex.Message;
            }
            catch (Exception ex)
            {
                // ProcessSinglePrecheckItem's QR-validation step throws plain Exception (not
                // ApplicationException/ValidationException) for things like "QR not active"/"already
                // consumed" - these are business messages, not infrastructure failures, so they're
                // surfaced the same way rather than left to bubble into the controller's generic 502.
                preview.Message = ex.Message;
            }

            return preview;
        }
    }
}
