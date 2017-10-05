using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1
{
    public static class SessionDic
    {
        public static Dictionary<string, string> userCookies = new Dictionary<string, string>();
        public static List<PaimaiItem> CurrentAuctionList = new List<PaimaiItem>();
    }

    public class AccountInfo
    {
        public string realAccount;
        public string virtualAccount;
        public string password;
        public bool isLogin;
        public string cookies;
        public DateTime loginTime;
        public PaimaiItem paimaiLog;
    }
}
