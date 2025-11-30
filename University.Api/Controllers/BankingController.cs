using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using University.Api.Dtos;
using University.Api.Models;

namespace University.Api.Controllers;

[ApiController]
[Route("api/v1/banking")]
[Authorize]
public class BankingController : ControllerBase
{
    private readonly AppDbContext _db;

    public BankingController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/v1/banking/tuition?studentNo=123
    // Authentication REQUIRED
    [HttpGet("tuition")]
    [Authorize]
    public async Task<ActionResult<TuitionQueryResponse>> GetTuition([FromQuery] string studentNo)
    {
        var student = await _db.Students
            .Include(s => s.Tuitions)
            .FirstOrDefaultAsync(s => s.StudentNo == studentNo);

        if (student == null)
            return NotFound();

        var tuitionTotal = student.Tuitions.Sum(t => t.TotalAmount);
        var balance = student.Tuitions.Sum(t => t.Balance);

        return Ok(new TuitionQueryResponse
        {
            TuitionTotal = tuitionTotal,
            Balance = balance
        });
    }

    // POST /api/v1/banking/pay
    // Authentication: NO (ödev tablosuna göre)
    [HttpPost("pay")]
    [AllowAnonymous]
    public async Task<ActionResult<PaymentResponse>> PayTuition([FromBody] PaymentRequest request)
    {
        var student = await _db.Students
            .Include(s => s.Tuitions)
            .FirstOrDefaultAsync(s => s.StudentNo == request.StudentNo);

        if (student == null)
        {
            return BadRequest(new PaymentResponse
            {
                Status = "Error",
                Message = "Student not found"
            });
        }

        var tuition = student.Tuitions.FirstOrDefault(t => t.Term == request.Term);
        if (tuition == null)
        {
            return BadRequest(new PaymentResponse
            {
                Status = "Error",
                Message = "Tuition for given term not found"
            });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new PaymentResponse
            {
                Status = "Error",
                Message = "Amount must be positive"
            });
        }

        // Eksik ödeme destekleniyor: Balance güncellenir
        var newBalance = tuition.Balance - request.Amount;
        if (newBalance < 0)
        {
            return BadRequest(new PaymentResponse
            {
                Status = "Error",
                Message = "Amount exceeds balance"
            });
        }

        tuition.Balance = newBalance;

        var payment = new Payment
        {
            StudentId = student.Id,
            Term = request.Term,
            Amount = request.Amount,
            Status = "Successful"
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return Ok(new PaymentResponse
        {
            Status = "Successful",
            Message = $"Remaining balance: {tuition.Balance}"
        });
    }
}
