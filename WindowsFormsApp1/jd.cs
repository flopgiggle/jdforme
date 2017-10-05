namespace WindowsFormsApp1
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class jd : DbContext
    {
        public jd()
            : base("name=jd")
        {
        }

        public virtual DbSet<auction> auctions { get; set; }
        public virtual DbSet<product> products { get; set; }
        public virtual DbSet<productPrice> productPrices { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<auction>()
                .Property(e => e.auctionId)
                .IsUnicode(false);

            modelBuilder.Entity<auction>()
                .Property(e => e.productId)
                .IsUnicode(false);

            modelBuilder.Entity<auction>()
                .Property(e => e.currentPrice)
                .HasPrecision(19, 4);

            modelBuilder.Entity<auction>()
                .Property(e => e.endPrice)
                .HasPrecision(19, 4);

            modelBuilder.Entity<product>()
                .Property(e => e.name)
                .IsUnicode(false);

            modelBuilder.Entity<product>()
                .Property(e => e.productId)
                .IsUnicode(false);

            modelBuilder.Entity<product>()
                .Property(e => e.price)
                .HasPrecision(19, 4);

            modelBuilder.Entity<product>()
                .Property(e => e.auctionLowPrice)
                .HasPrecision(19, 4);

            modelBuilder.Entity<product>()
                .Property(e => e.auctionTopPrice)
                .HasPrecision(19, 4);

            modelBuilder.Entity<product>()
                .Property(e => e.auctionAvgPrice)
                .HasPrecision(19, 4);

            modelBuilder.Entity<productPrice>()
                .Property(e => e.productId)
                .IsUnicode(false);

            modelBuilder.Entity<productPrice>()
                .Property(e => e.price)
                .HasPrecision(19, 4);
        }
    }
}
