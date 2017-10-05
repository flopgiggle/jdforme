using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1
{
    public class Config
    {
        /// <summary>
        /// 提前多久时间开始出价,精确到毫秒
        /// </summary>
        public static int beforeBeginTime = 3500;

        /// <summary>
        /// 是否设置代理服务器，以便观察网络情况
        /// </summary>
        public static bool isSetProxy = true;
        /// <summary>
        /// 每个拍卖出价动作间隔的时间
        /// </summary>
        public static int everySendPriceSpanTime = 80;
        public static string userAccout = "13689001585|855133&lskyqwe|123!@#qwe&18980668835|855133";
        //每次加价的幅度
        public static int everyTimePulsPrice = 2;
        //public static string userAccout = "13689001585|855133";
        //public static string userAccout = "13689001585|855133&lskyqwe|123!@#qwe";
        //public static string userAccout = "13689001585|855133";
        /// <summary>
        /// 设置一个账号多次登录，的最大个数
        /// </summary>
        public static int xloginNum = 1;
        /// <summary>
        /// 最迟发送拍卖请求的时间,例如50,距离拍卖50ms时发送抢拍请求
        /// </summary>
        public static int zeroTimeForPaimai = 100;
    }

    
}
