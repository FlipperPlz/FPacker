using System;
using System.Collections.Generic;
using FPBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FPBackend.Context
{
    public partial class FPDataContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public FPDataContext()
        {
        }

        public FPDataContext(DbContextOptions<FPDataContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
