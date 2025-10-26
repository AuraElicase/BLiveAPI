using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLiveAPI.Models
{
    public class OnlineRankListItemModel
    {
        public long Uid { get; set; }
        public string Face { get; set; }
        public string Score { get; set; }
        public string Uname { get; set; }
        public int Rank { get; set; }
        public int GuardLevel { get; set; }
    }
}
