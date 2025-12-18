/*
 * Rental Agreement PDF Generator
 * Generates PDF documents for rental agreements using QuestPDF
 * Copyright (c) 2025 Alexander Orlov.
 */

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace CarRental.Api.Services;

/// <summary>
/// Data for PDF generation
/// </summary>
public class RentalAgreementPdfData
{
    public string AgreementNumber { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseState { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public DateTime PickupDate { get; set; }
    public string? PickupLocation { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? ReturnLocation { get; set; }
    public decimal RentalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string SignatureImage { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    public string TermsText { get; set; } = string.Empty;
    public string NonRefundableText { get; set; } = string.Empty;
    public string DamagePolicyText { get; set; } = string.Empty;
    public string CardAuthorizationText { get; set; } = string.Empty;
}

public class RentalAgreementPdfGenerator
{
    public byte[] Generate(RentalAgreementPdfData data)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Column(column =>
                    {
                        column.Item().Text($"Rental Agreement - {data.AgreementNumber}")
                            .FontSize(20)
                            .Bold()
                            .AlignCenter();
                        
                        column.Item().PaddingTop(10).Text(data.CompanyName)
                            .FontSize(14)
                            .SemiBold()
                            .AlignCenter();
                        
                        if (!string.IsNullOrEmpty(data.CompanyAddress))
                        {
                            column.Item().Text(data.CompanyAddress).FontSize(9).AlignCenter();
                        }
                        
                        if (!string.IsNullOrEmpty(data.CompanyPhone))
                        {
                            column.Item().Text($"Phone: {data.CompanyPhone}").FontSize(9).AlignCenter();
                        }
                        
                        if (!string.IsNullOrEmpty(data.CompanyEmail))
                        {
                            column.Item().Text($"Email: {data.CompanyEmail}").FontSize(9).AlignCenter();
                        }
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        // Agreement Information
                        column.Item().PaddingBottom(5).Text("Agreement Information").FontSize(14).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Element(CellStyle).Text("Agreement Number:");
                            table.Cell().Element(CellStyle).Text(data.AgreementNumber);
                            
                            table.Cell().Element(CellStyle).Text("Date Signed:");
                            table.Cell().Element(CellStyle).Text(data.SignedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                        });

                        column.Item().PaddingTop(10);

                        // Customer Information
                        column.Item().PaddingBottom(5).Text("Customer Information").FontSize(14).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Element(CellStyle).Text("Name:");
                            table.Cell().Element(CellStyle).Text(data.CustomerName);
                            
                            table.Cell().Element(CellStyle).Text("Email:");
                            table.Cell().Element(CellStyle).Text(data.CustomerEmail);
                            
                            if (!string.IsNullOrEmpty(data.CustomerPhone))
                            {
                                table.Cell().Element(CellStyle).Text("Phone:");
                                table.Cell().Element(CellStyle).Text(data.CustomerPhone);
                            }
                            
                            if (!string.IsNullOrEmpty(data.CustomerAddress))
                            {
                                table.Cell().Element(CellStyle).Text("Address:");
                                table.Cell().Element(CellStyle).Text(data.CustomerAddress);
                            }
                            
                            if (!string.IsNullOrEmpty(data.DriverLicenseNumber))
                            {
                                table.Cell().Element(CellStyle).Text("Driver License:");
                                table.Cell().Element(CellStyle).Text($"{data.DriverLicenseNumber} ({data.DriverLicenseState ?? "N/A"})");
                            }
                        });

                        column.Item().PaddingTop(10);

                        // Vehicle Information
                        column.Item().PaddingBottom(5).Text("Vehicle Information").FontSize(14).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Element(CellStyle).Text("Vehicle:");
                            table.Cell().Element(CellStyle).Text(data.VehicleName);
                            
                            if (!string.IsNullOrEmpty(data.VehiclePlate))
                            {
                                table.Cell().Element(CellStyle).Text("License Plate:");
                                table.Cell().Element(CellStyle).Text(data.VehiclePlate);
                            }
                        });

                        column.Item().PaddingTop(10);

                        // Rental Details
                        column.Item().PaddingBottom(5).Text("Rental Details").FontSize(14).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Element(CellStyle).Text("Pickup Date:");
                            table.Cell().Element(CellStyle).Text(data.PickupDate.ToString("yyyy-MM-dd HH:mm"));
                            
                            if (!string.IsNullOrEmpty(data.PickupLocation))
                            {
                                table.Cell().Element(CellStyle).Text("Pickup Location:");
                                table.Cell().Element(CellStyle).Text(data.PickupLocation);
                            }
                            
                            table.Cell().Element(CellStyle).Text("Return Date:");
                            table.Cell().Element(CellStyle).Text(data.ReturnDate.ToString("yyyy-MM-dd HH:mm"));
                            
                            if (!string.IsNullOrEmpty(data.ReturnLocation))
                            {
                                table.Cell().Element(CellStyle).Text("Return Location:");
                                table.Cell().Element(CellStyle).Text(data.ReturnLocation);
                            }
                            
                            table.Cell().Element(CellStyle).Text("Rental Amount:");
                            table.Cell().Element(CellStyle).Text($"{GetCurrencySymbol(data.Currency)}{data.RentalAmount:N2}");
                            
                            table.Cell().Element(CellStyle).Text("Security Deposit:");
                            table.Cell().Element(CellStyle).Text($"{GetCurrencySymbol(data.Currency)}{data.DepositAmount:N2}");
                        });

                        column.Item().PaddingTop(10);

                        // Terms and Conditions
                        column.Item().PaddingBottom(5).Text("Terms and Conditions").FontSize(14).Bold();
                        
                        if (!string.IsNullOrEmpty(data.TermsText))
                        {
                            column.Item().PaddingTop(5).Text("1. Terms and Conditions").FontSize(12).Bold();
                            column.Item().PaddingBottom(5).Text(data.TermsText).FontSize(9);
                        }
                        
                        if (!string.IsNullOrEmpty(data.NonRefundableText))
                        {
                            column.Item().PaddingTop(5).Text("2. Non-Refundable Policy").FontSize(12).Bold();
                            column.Item().PaddingBottom(5).Text(data.NonRefundableText).FontSize(9);
                        }
                        
                        if (!string.IsNullOrEmpty(data.DamagePolicyText))
                        {
                            column.Item().PaddingTop(5).Text("3. Damage Responsibility").FontSize(12).Bold();
                            column.Item().PaddingBottom(5).Text(data.DamagePolicyText).FontSize(9);
                        }
                        
                        if (!string.IsNullOrEmpty(data.CardAuthorizationText))
                        {
                            column.Item().PaddingTop(5).Text("4. Card Authorization").FontSize(12).Bold();
                            column.Item().PaddingBottom(5).Text(data.CardAuthorizationText).FontSize(9);
                        }

                        column.Item().PaddingTop(10);

                        // Signature
                        column.Item().PaddingBottom(5).Text("Customer Signature").FontSize(14).Bold();
                        
                        if (!string.IsNullOrEmpty(data.SignatureImage))
                        {
                            try
                            {
                                // Decode base64 signature image
                                var base64Data = data.SignatureImage;
                                if (base64Data.StartsWith("data:image"))
                                {
                                    base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
                                }
                                
                                var imageBytes = Convert.FromBase64String(base64Data);
                                column.Item().Height(80).Image(imageBytes).FitArea();
                            }
                            catch
                            {
                                column.Item().Text("[Signature image could not be displayed]").FontSize(9).Italic();
                            }
                        }
                        
                        column.Item().PaddingTop(5).Text($"Signed: {data.SignedAt.ToString("yyyy-MM-dd HH:mm:ss UTC")}").FontSize(9);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
            });
        })
        .GeneratePdf();
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(5);
    }

    private static string GetCurrencySymbol(string currency)
    {
        return currency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "BRL" => "R$",
            "JPY" => "¥",
            _ => currency
        };
    }
}

