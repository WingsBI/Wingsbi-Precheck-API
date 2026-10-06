using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Precheck.Models.DataModel;
using Precheck.Models.DTOs.IRNumber;
using Precheck.Models.DTOs.MSNNumber;

namespace Precheck.Service.Service.IdentifierService
{
    public interface IIdentifierService
    {
        Task<IRNumbers> InsertIRNumberAsync(IRNumberDto irNumberDto);

        Task<MSNNumbers> InsertMSNNumberAsync(MSNNumberDto msnNumberDto);

        Task<IRNumbers> InsertStandardIRNumberAsync(StandardIRNumberDto standardIRNumberDto);

        Task<MSNNumbers> InsertStandardMSNNumberAsync(StandardMSNNumberDto standardMSNNumberDto);

        Task<IRNumbers> UpadateIRNumberAsync(UpdateIRDto updateIR);

        Task<MSNNumbers> UpadateMSNNumber(UpdateMSNDto updateMSN);

        byte[] GenerateDownloadMSNMemoPdf(Precheck.Models.DTOs.Identifier.DownloadMSNMemoDto request);

        Task<string> GenerateDownloadMSNMemoHtml(Precheck.Models.DTOs.Identifier.DownloadMSNMemoDto request);
    }
}
