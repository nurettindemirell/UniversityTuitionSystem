namespace University.Api.Models;

public class Student
{
    public int Id { get; set; }
    public string StudentNo { get; set; } = null!;
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Tuition> Tuitions { get; set; } = new List<Tuition>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
