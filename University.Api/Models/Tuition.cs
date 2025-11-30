namespace University.Api.Models;

public class Tuition
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string Term { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Student Student { get; set; } = null!;
}
