using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using University.Api.Dtos;
using University.Api.Models;

namespace University.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tuition")]
[Authorize] // Add Tuition, Batch, Unpaid hepsi JWT gerektiriyor
public class AdminTuitionController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminTuitionController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/v1/admin/tuition
    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> AddTuition([FromBody] AddTuitionRequest request)
    {
        var student = await _db.Students
            .FirstOrDefaultAsync(s => s.StudentNo == request.StudentNo);

        if (student == null)
        {
            student = new Student
            {
                StudentNo = request.StudentNo,
                FullName = null
            };
            _db.Students.Add(student);
            await _db.SaveChangesAsync();
        }

        var existing = await _db.Tuitions
            .FirstOrDefaultAsync(t => t.StudentId == student.Id && t.Term == request.Term);

        if (existing != null)
        {
            return BadRequest(new TransactionResponse
            {
                Status = "Error",
                Message = "Tuition already exists for student and term"
            });
        }

        var tuition = new Tuition
        {
            StudentId = student.Id,
            Term = request.Term,
            TotalAmount = request.TotalAmount,
            Balance = request.TotalAmount
        };

        _db.Tuitions.Add(tuition);
        await _db.SaveChangesAsync();

        return Ok(new TransactionResponse
        {
            Status = "Successful",
            Message = "Tuition added"
        });
    }

    // POST /api/v1/admin/tuition/batch
    // CSV formatı: StudentNo,Term,TotalAmount (header dahil)
    [HttpPost("batch")]
    public async Task<ActionResult<TransactionResponse>> AddTuitionBatch(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new TransactionResponse
            {
                Status = "Error",
                Message = "CSV file is required"
            });
        }

        var added = 0;
        using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<AddTuitionRequest>();
            foreach (var record in records)
            {
                var student = await _db.Students
                    .FirstOrDefaultAsync(s => s.StudentNo == record.StudentNo);

                if (student == null)
                {
                    student = new Student
                    {
                        StudentNo = record.StudentNo
                    };
                    _db.Students.Add(student);
                    await _db.SaveChangesAsync();
                }

                var exists = await _db.Tuitions
                    .AnyAsync(t => t.StudentId == student.Id && t.Term == record.Term);

                if (exists)
                    continue;

                var tuition = new Tuition
                {
                    StudentId = student.Id,
                    Term = record.Term,
                    TotalAmount = record.TotalAmount,
                    Balance = record.TotalAmount
                };
                _db.Tuitions.Add(tuition);
                added++;
            }
            await _db.SaveChangesAsync();
        }

        return Ok(new TransactionResponse
        {
            Status = "Successful",
            Message = $"Batch completed. Added {added} records."
        });
    }

    // GET /api/v1/admin/tuition/unpaid?term=2024-FALL&pageNumber=1&pageSize=20
    [HttpGet("unpaid")]
    public async Task<ActionResult<PagedUnpaidResponse>> GetUnpaid(
        [FromQuery] string term,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 20;

        var query = _db.Tuitions
            .Include(t => t.Student)
            .Where(t => t.Term == term && t.Balance > 0);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(t => t.Student.StudentNo)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new UnpaidStudentItem
            {
                StudentNo = t.Student.StudentNo,
                FullName = t.Student.FullName,
                Term = t.Term,
                Balance = t.Balance
            })
            .ToListAsync();

        var response = new PagedUnpaidResponse
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };

        return Ok(response);
    }
}
