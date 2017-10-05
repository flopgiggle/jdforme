namespace WindowsFormsApp1
{
    public class PaimaiItem
    {
        public string paimaiId;
        public string detailUrl;
        public string describe;

        public string productId { get;  set; }
        public int auctionStatus { get;  set; }
        public int bidCount { get;  set; }
        public long startTime { get;  set; }
        public long endTime { get;  set; }
        public long remainTime { get; set; }
        public double currentPrice { get; set; }
    }
}