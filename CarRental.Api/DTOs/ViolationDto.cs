/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using System.ComponentModel.DataAnnotations;

namespace CarRental.Api.DTOs;

public class ViolationDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    [MaxLength(255)]
    public string? CitationNumber { get; set; }

    [MaxLength(255)]
    public string? NoticeNumber { get; set; }

    public int Provider { get; set; }

    [MaxLength(255)]
    public string? Agency { get; set; }

    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Tag { get; set; }

    [MaxLength(10)]
    public string? State { get; set; }

    public DateTime? IssueDate { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; }

    public int PaymentStatus { get; set; }

    public int FineType { get; set; }

    public string? Note { get; set; }

    public string? Link { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Computed properties for display
    public string ViolationNumber => CitationNumber ?? NoticeNumber ?? Id.ToString().Substring(0, 8);
    public DateTime ViolationDate => IssueDate ?? StartDate ?? CreatedAt;
    public string Type => FineType > 0 ? $"Fine Type {FineType}" : "Violation";
    public string Status => GetStatusString(PaymentStatus);
    public string? Description => Note;

    private string GetStatusString(int paymentStatus)
    {
        return paymentStatus switch
        {
            0 => "pending",
            1 => "paid",
            2 => "overdue",
            3 => "cancelled",
            _ => "pending"
        };
    }
}
