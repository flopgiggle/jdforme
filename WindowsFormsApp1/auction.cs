namespace WindowsFormsApp1
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("auction")]
    public partial class auction
    {
        public int id { get; set; }

        [Required]
        [StringLength(50)]
        public string auctionId { get; set; }

        [StringLength(50)]
        public string productId { get; set; }

        public long? startTime { get; set; }

        public long? endTime { get; set; }

        [Column(TypeName = "money")]
        public decimal? currentPrice { get; set; }

        public int? bidCount { get; set; }

        public int? auctionStatus { get; set; }

        [Column(TypeName = "money")]
        public decimal? endPrice { get; set; }

        public bool? isCount { get; set; }

        public DateTime? createTime { get; set; }

        public DateTime? updateTime { get; set; }
    }
}
