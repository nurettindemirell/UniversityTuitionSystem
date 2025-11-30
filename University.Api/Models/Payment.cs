namespace University.Api.Models;

public class Payment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string Term { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Successful";

    public Student Student { get; set; } = null!;
}
