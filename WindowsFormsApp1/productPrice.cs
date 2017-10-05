namespace WindowsFormsApp1
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("productPrice")]
    public partial class productPrice
    {
        public int id { get; set; }

        [StringLength(50)]
        public string productId { get; set; }

        [Column(TypeName = "money")]
        public decimal? price { get; set; }

        public DateTime? createTime { get; set; }
    }
}
