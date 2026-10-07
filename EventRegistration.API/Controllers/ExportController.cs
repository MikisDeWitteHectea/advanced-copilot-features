using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ClosedXML.Excel;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api/events")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExportController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{eventId}/export/json")]
    public async Task<IActionResult> ExportAsJson(int eventId, [FromQuery] string? status = null, [FromQuery] int? ticketTypeId = null, [FromQuery] string? sortBy = null)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var records = await GetExportRecords(eventId, status, ticketTypeId, sortBy);

        return File(
            Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(records)),
            "application/json",
            $"attendees-{eventId}.json"
        );
    }

    [HttpGet("{eventId}/export/csv")]
    public async Task<IActionResult> ExportAsCsv(int eventId, [FromQuery] string? status = null, [FromQuery] int? ticketTypeId = null, [FromQuery] string? sortBy = null)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var records = await GetExportRecords(eventId, status, ticketTypeId, sortBy);

        var csv = new StringBuilder();
        csv.AppendLine("RegistrationId,AttendeeName,Email,PhoneNumber,TicketType,RegistrationDate,AmountPaid,Status,DiscountCodeUsed");

        foreach (var record in records)
        {
            csv.AppendLine($"{record.RegistrationId},\"{EscapeCsv(record.AttendeeName)}\",\"{EscapeCsv(record.Email)}\",\"{EscapeCsv(record.PhoneNumber)}\",\"{EscapeCsv(record.TicketType)}\",{record.RegistrationDate:yyyy-MM-dd HH:mm:ss},{record.AmountPaid},\"{EscapeCsv(record.Status)}\",\"{EscapeCsv(record.DiscountCodeUsed)}\"");
        }

        return File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            $"attendees-{eventId}.csv"
        );
    }

    [HttpGet("{eventId}/export/excel")]
    public async Task<IActionResult> ExportAsExcel(int eventId, [FromQuery] string? status = null, [FromQuery] int? ticketTypeId = null, [FromQuery] string? sortBy = null)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var records = await GetExportRecords(eventId, status, ticketTypeId, sortBy);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Attendees");

        // Headers
        worksheet.Cell(1, 1).Value = "RegistrationId";
        worksheet.Cell(1, 2).Value = "AttendeeName";
        worksheet.Cell(1, 3).Value = "Email";
        worksheet.Cell(1, 4).Value = "PhoneNumber";
        worksheet.Cell(1, 5).Value = "TicketType";
        worksheet.Cell(1, 6).Value = "RegistrationDate";
        worksheet.Cell(1, 7).Value = "AmountPaid";
        worksheet.Cell(1, 8).Value = "Status";
        worksheet.Cell(1, 9).Value = "DiscountCodeUsed";

        // Data
        int row = 2;
        foreach (var record in records)
        {
            worksheet.Cell(row, 1).Value = record.RegistrationId;
            worksheet.Cell(row, 2).Value = record.AttendeeName;
            worksheet.Cell(row, 3).Value = record.Email;
            worksheet.Cell(row, 4).Value = record.PhoneNumber ?? "";
            worksheet.Cell(row, 5).Value = record.TicketType;
            worksheet.Cell(row, 6).Value = record.RegistrationDate;
            worksheet.Cell(row, 7).Value = record.AmountPaid;
            worksheet.Cell(row, 8).Value = record.Status;
            worksheet.Cell(row, 9).Value = record.DiscountCodeUsed ?? "";
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"attendees-{eventId}.xlsx"
        );
    }

    private async Task<List<AttendeeExportRecord>> GetExportRecords(int eventId, string? status, int? ticketTypeId, string? sortBy)
    {
        var query = _context.Registrations
            .Include(r => r.TicketType)
            .Where(r => r.EventId == eventId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (ticketTypeId.HasValue)
        {
            query = query.Where(r => r.TicketTypeId == ticketTypeId.Value);
        }

        var registrations = await query.ToListAsync();

        var records = registrations.Select(r => new AttendeeExportRecord(
            r.Id,
            $"{r.FirstName} {r.LastName}",
            r.Email,
            r.PhoneNumber,
            r.TicketType.Name,
            r.RegistrationDate,
            r.TotalAmount,
            r.Status,
            r.DiscountCodeUsed
        )).ToList();

        // Apply sorting
        if (!string.IsNullOrEmpty(sortBy))
        {
            records = sortBy.ToLower() switch
            {
                "name" => records.OrderBy(r => r.AttendeeName).ToList(),
                "registrationdate" => records.OrderBy(r => r.RegistrationDate).ToList(),
                _ => records
            };
        }

        return records;
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("\"", "\"\"");
    }
}
