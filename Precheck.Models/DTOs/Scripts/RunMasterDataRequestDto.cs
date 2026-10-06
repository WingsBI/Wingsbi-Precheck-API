using System.ComponentModel.DataAnnotations;

namespace Precheck.Models.DTOs.Scripts
{
    public class RunMasterDataRequestDto
    {
        public List<string> FileName { get; set; } = new();
    }
}
