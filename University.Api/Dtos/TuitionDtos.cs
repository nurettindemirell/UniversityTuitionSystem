namespace University.Api.Dtos;

public class TuitionQueryResponse
{
    public decimal TuitionTotal { get; set; }
    public decimal Balance { get; set; }
}

public class PaymentRequest
{
    public string StudentNo { get; set; } = null!;
    public string Term { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class PaymentResponse
{
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
}

public class AddTuitionRequest
{
    public string StudentNo { get; set; } = null!;
    public string Term { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}

public class TransactionResponse
{
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
}

public class UnpaidStudentItem
{
    public string StudentNo { get; set; } = null!;
    public string? FullName { get; set; }
    public string Term { get; set; } = null!;
    public decimal Balance { get; set; }
}

public class PagedUnpaidResponse
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<UnpaidStudentItem> Items { get; set; } = new();
}
