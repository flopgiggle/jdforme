using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1
{
    public static class EnumType
    {
        public enum AuctionStatus
        {
            WaitForAuction = 0,
            Auctioning = 1,
            EndAuction = 2,
            ErrorNotFoundAuction = 3,
        }

        public enum SendPriceStatus
        {
            end = 3,
            low = 2,
            ok = 1,
            notEnoughJindou = 4,//京东为负数
            error = -1,
            tooFast = 5,//出价频率过快，
            notLogin = 6

        }

        public enum JobType
        {
            SearchAuctionByProduct = 1,
            SendPriceJob = 1
        }

        public enum controlerType{
            textBox = 1,
            button = 2
        }

        public enum auctionQualityType
        {
            NotUse = 1,
            Uesed = 2,
            Fixed = 3,
        }
    }
}
