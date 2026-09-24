using FaqCms.Models;
using Microsoft.EntityFrameworkCore;

namespace FaqCms.Data;

public static class SeedData
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Payroll", Slug = "payroll", Icon = "💰", SortOrder = 1, Description = "Payroll processing and management guides", Color = "#1F7A4D" },
            new Category { Id = 2, Name = "Employee Management", Slug = "employee-management", Icon = "👥", SortOrder = 2, Description = "Employee onboarding, records, and HR processes", Color = "#38B476" },
            new Category { Id = 3, Name = "Transactions", Slug = "transactions", Icon = "📋", SortOrder = 3, Description = "Financial transactions and reporting", Color = "#5B6B60" }
        );

        modelBuilder.Entity<Article>().HasData(
            new Article
            {
                Id = 1,
                CategoryId = 1,
                Title = "How to create payroll",
                Slug = "how-to-create-payroll",
                Summary = "Run a new pay cycle for your team in a few steps — from setting the pay period to reviewing totals before it's finalized.",
                SortOrder = 1,
                IsPublished = true,
                IsFeatured = true,
                ViewCount = 0,
                CreatedAt = new DateTime(2026, 9, 2),
                UpdatedAt = new DateTime(2026, 9, 2),
                PublishedAt = new DateTime(2026, 9, 2),
                Tags = "payroll,basics,getting-started"
            },
            new Article
            {
                Id = 2,
                CategoryId = 1,
                Title = "Payroll loans",
                Slug = "payroll-loans",
                Summary = "Add a staff loan to a pay run, set repayment terms, and track the remaining balance each cycle.",
                SortOrder = 2,
                IsPublished = true,
                ViewCount = 0,
                CreatedAt = new DateTime(2026, 8, 20),
                UpdatedAt = new DateTime(2026, 8, 20),
                PublishedAt = new DateTime(2026, 8, 20),
                Tags = "loans,deductions,staff"
            },
            new Article
            {
                Id = 3,
                CategoryId = 1,
                Title = "Payroll adjustment",
                Slug = "payroll-adjustment",
                Summary = "Correct an already-confirmed pay run, reissue a payslip, and log the reason for the change.",
                SortOrder = 3,
                IsPublished = true,
                ViewCount = 0,
                CreatedAt = new DateTime(2026, 8, 15),
                UpdatedAt = new DateTime(2026, 8, 15),
                PublishedAt = new DateTime(2026, 8, 15),
                Tags = "corrections,adjustments,payslips"
            }
        );

        modelBuilder.Entity<ArticleStep>().HasData(
            new ArticleStep
            {
                Id = 1,
                ArticleId = 1,
                SortOrder = 1,
                Title = "Open the Payroll module",
                Description = "Go to Payroll from the sidebar, then select New Pay Run in the top-right corner.",
                EstimatedMinutes = 2
            },
            new ArticleStep
            {
                Id = 2,
                ArticleId = 1,
                SortOrder = 2,
                Title = "Set the pay period",
                Description = "Choose the start and end dates. Employees included in this range are added automatically.",
                EstimatedMinutes = 3
            },
            new ArticleStep
            {
                Id = 3,
                ArticleId = 1,
                SortOrder = 3,
                Title = "Review and confirm",
                Description = "Check hours, deductions, and totals for each employee, then select Confirm Payroll to finalize.",
                EstimatedMinutes = 5
            }
        );

        modelBuilder.Entity<Tag>().HasData(
            new Tag { Id = 1, Name = "Getting Started", Slug = "getting-started", Color = "#1F7A4D" },
            new Tag { Id = 2, Name = "Payroll Basics", Slug = "payroll-basics", Color = "#38B476" },
            new Tag { Id = 3, Name = "Loans", Slug = "loans", Color = "#B8462E" },
            new Tag { Id = 4, Name = "Corrections", Slug = "corrections", Color = "#5B6B60" },
            new Tag { Id = 5, Name = "Advanced", Slug = "advanced", Color = "#0B120F" }
        );

        modelBuilder.Entity<ArticleTag>().HasData(
            new ArticleTag { ArticleId = 1, TagId = 1 },
            new ArticleTag { ArticleId = 1, TagId = 2 },
            new ArticleTag { ArticleId = 2, TagId = 2 },
            new ArticleTag { ArticleId = 2, TagId = 3 },
            new ArticleTag { ArticleId = 3, TagId = 2 },
            new ArticleTag { ArticleId = 3, TagId = 4 }
        );
    }
}
