namespace FaqCms.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Icon { get; set; } = "📁";
    public string? Description { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Soft-delete / Recycle Bin support — see AppDbContext's query filter.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Null = a top-level category. Set = this is a subcategory of that category.
    // Only one level deep is supported (a subcategory can't itself have subcategories) —
    // see ArticleService.CreateCategoryAsync/UpdateCategoryAsync for the check.
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public List<Category> SubCategories { get; set; } = new();

    public List<Article> Articles { get; set; } = new();
}
