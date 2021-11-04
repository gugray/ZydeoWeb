using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class CreateUserResult
    {
        public bool Success { get; set; }
        public bool EmailExists { get; set; }
        public bool UserNameExists { get; set; }
    }
}
