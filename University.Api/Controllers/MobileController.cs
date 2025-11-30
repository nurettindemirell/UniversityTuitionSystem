using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using University.Api.Dtos;
using University.Api.Models;
using Microsoft.AspNetCore.Authorization;

/////////  sonradan gelenlerdi limitin için
using System.Collections.Concurrent;
using University.Api.RateLimiting;



namespace University.Api.Controllers;

[ApiController]
[Route("api/v1/mobile")]
public class MobileController : ControllerBase
{
     private readonly AppDbContext _db;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _rateStore;

    public MobileController(
        AppDbContext db,
        ConcurrentDictionary<string, RateLimitEntry> rateStore)
    {
        _db = db;
        _rateStore = rateStore;
    }

    
    [AllowAnonymous] // Mobile Query Tuition için auth Yyok ve olmaz
    [HttpGet("tuition")]
    public async Task<ActionResult<TuitionQueryResponse>> GetTuition([FromQuery] string studentNo)
    {
        
        //////////Rate limiting başlangıç

         var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var key = $"mobile:tuition:{studentNo}";

    var entry = _rateStore.GetOrAdd(key, _ => new RateLimitEntry
    {
        Date = today,
        Count = 0
    });

    if (entry.Date != today)
    {
        entry.Date = today;
        entry.Count = 0;
    }

    entry.Count++;
    if (entry.Count > 3)
    {
        return StatusCode(StatusCodes.Status429TooManyRequests,
            new
            {
                status = "Error",
                message = "Daily limit (3) for this student is exceeded."
            });
    }

        /////////////RateLimit bitişi

        var student = await _db.Students
            .Include(s => s.Tuitions)
            .FirstOrDefaultAsync(s => s.StudentNo == studentNo);

        if (student == null)
            return NotFound();

        // Varsayım: birden fazla dönem olabilir, toplam harç ve bakiye hepsinin toplamı
        var tuitionTotal = student.Tuitions.Sum(t => t.TotalAmount);
        var balance = student.Tuitions.Sum(t => t.Balance);

        return Ok(new TuitionQueryResponse
        {
            TuitionTotal = tuitionTotal,
            Balance = balance
        });
    }
}
