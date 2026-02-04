namespace OrderXChange.BackgroundJobs;

public class StagingMenuGroupSummaryDto
{
    public string GroupId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    public int TotalProducts { get; set; }
    public int TotalCategories { get; set; }

    /// <summary>
    /// Indicates if this menu group is mapped to a Talabat account (has FoodicsGroupId configured)
    /// </summary>
    public bool IsMappedToTalabatAccount { get; set; }
}

