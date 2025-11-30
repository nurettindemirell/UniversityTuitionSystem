namespace University.Api.RateLimiting
{
    public class RateLimitEntry
    {
        public int Count { get; set; }      // Bugün kaç kere çağrıldı
        public DateOnly Date { get; set; }  // Hangi güne ait sayaç
    }
}
