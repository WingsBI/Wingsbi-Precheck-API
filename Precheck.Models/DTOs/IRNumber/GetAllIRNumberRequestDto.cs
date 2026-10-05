using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Precheck.Models.DTOs.IRNumber
{
    public class GetAllIRNumberRequestDto
    {

        public string? query { get; set; }

        public int? userId { get; set; }

        public int? departmentId { get; set; }
    }
}
