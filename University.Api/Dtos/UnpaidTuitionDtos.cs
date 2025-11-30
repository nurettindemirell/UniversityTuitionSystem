namespace University.Api.Dtos
{
    public class UnpaidTuitionItemDto
    {
        public string StudentNo { get; set; } = default!;
        public string? StudentName { get; set; }
        public string Term { get; set; } = default!;
        public decimal TotalAmount { get; set; }
        public decimal Balance { get; set; }
    }

    public class UnpaidTuitionPagedResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<UnpaidTuitionItemDto> Items { get; set; } = new();
    }
}
