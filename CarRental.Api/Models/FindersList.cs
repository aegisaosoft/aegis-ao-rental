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
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace CarRental.Api.Models;

[Table("finders_list")]
public class FindersList
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("finders_list", TypeName = "jsonb")]
    public string FindersListJson { get; set; } = "[]";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }

    // Helper property to work with the JSON as a list of strings
    [NotMapped]
    public List<string> StateCodes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FindersListJson))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(FindersListJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        set
        {
            FindersListJson = JsonSerializer.Serialize(value ?? new List<string>());
        }
    }
}
