using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterface
{
    public class LoginDTO
    {
        public string AccountNumber { get; set; }
        public bool IsContractSigned { get; set; }
        public int ContractType { get; set; }
        //public DateTime? DismissalDate { get; set; }
        public DateTime EmploymentDate { get; set; }
        public string Id { get; set; }
        public bool IsDeleted { get; set; }
        public string Login { get; set; }
        public string LoginSiebel { get; set; }
        public string Name { get; set; }
        public string PersonnelNumber { get; set; }
        public string ShiftId { get; set; }
    }
}
