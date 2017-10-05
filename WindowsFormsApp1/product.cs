namespace WindowsFormsApp1
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("product")]
    public partial class product
    {
        public int id { get; set; }

        [StringLength(1000)]
        public string name { get; set; }

        [Required]
        [StringLength(50)]
        public string productId { get; set; }

        [Column(TypeName = "money")]
        public decimal? price { get; set; }

        [Column(TypeName = "money")]
        public decimal? auctionLowPrice { get; set; }

        [Column(TypeName = "money")]
        public decimal? auctionTopPrice { get; set; }

        [Column(TypeName = "money")]
        public decimal? auctionAvgPrice { get; set; }

        public int? auctionCount { get; set; }

        public int? auctionEndCount { get; set; }

        public DateTime? createTime { get; set; }

        public DateTime? updateTime { get; set; }
    }
}
