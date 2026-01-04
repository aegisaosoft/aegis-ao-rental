/*
 * Rental Agreement PDF Generator
 * Generates PDF documents for rental agreements using QuestPDF
 * Copyright (c) 2025 Alexander Orlov.
 */

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CarRental.Api.Services;

/// <summary>
/// Data for PDF generation
/// </summary>
public class RentalAgreementPdfData
{
    public string AgreementNumber { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    
    // Company Info
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }
    
    // Customer / Primary Renter
    public string CustomerFirstName { get; set; } = string.Empty;
    public string? CustomerMiddleName { get; set; }
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty; // Full name (legacy)
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseState { get; set; }
    public DateTime? DriverLicenseExpiration { get; set; }
    public DateTime? CustomerDateOfBirth { get; set; }
    
    // Additional Driver
    public string? AdditionalDriverFirstName { get; set; }
    public string? AdditionalDriverMiddleName { get; set; }
    public string? AdditionalDriverLastName { get; set; }
    public string? AdditionalDriverEmail { get; set; }
    public string? AdditionalDriverPhone { get; set; }
    public string? AdditionalDriverLicenseNumber { get; set; }
    public string? AdditionalDriverLicenseState { get; set; }
    public DateTime? AdditionalDriverLicenseExpiration { get; set; }
    public DateTime? AdditionalDriverDateOfBirth { get; set; }
    public string? AdditionalDriverAddress { get; set; }
    
    // Rental Vehicle
    public string? VehicleType { get; set; }
    public string VehicleName { get; set; } = string.Empty; // Make/Model
    public int? VehicleYear { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleVin { get; set; }
    public int? OdometerStart { get; set; }
    public int? OdometerEnd { get; set; }
    
    // Rental Period
    public DateTime PickupDate { get; set; }
    public string? PickupTime { get; set; }
    public string? PickupLocation { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? ReturnTime { get; set; }
    public string? ReturnLocation { get; set; }
    public DateTime? DueDate { get; set; }
    
    // Fuel Level
    public string? FuelAtPickup { get; set; }
    public string? FuelAtReturn { get; set; }
    
    // Financial
    public decimal RentalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal DailyRate { get; set; }
    public int RentalDays { get; set; } = 1;
    public string Currency { get; set; } = "USD";
    
    // Additional Services
    public List<AdditionalServiceItem> AdditionalServices { get; set; } = new();
    public decimal AdditionalServicesTotal { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalCharges { get; set; }
    
    // Additional Charges
    public decimal LateReturnFee { get; set; }
    public decimal DamageFee { get; set; }
    public decimal FuelServiceFee { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal Refund { get; set; }
    public decimal BalanceDue { get; set; }
    
    // Signature
    public string SignatureImage { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    
    // Rules of Action (short rules shown in modal)
    public string RulesText { get; set; } = string.Empty;
    
    // Full Terms and Conditions (legal text from /rental-terms)
    public string FullTermsText { get; set; } = string.Empty;
    
    // Legacy fields (for backward compatibility)
    public string TermsText { get; set; } = string.Empty;
    public string NonRefundableText { get; set; } = string.Empty;
    public string DamagePolicyText { get; set; } = string.Empty;
    public string CardAuthorizationText { get; set; } = string.Empty;
}

public class AdditionalServiceItem
{
    public string Name { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public int Days { get; set; }
    public decimal Total { get; set; }
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
                    .PaddingVertical(0.5f, Unit.Centimetre)
                    .Column(column =>
                    {
                        var dash = "-";
                        var currencySymbol = GetCurrencySymbol(data.Currency);
                        
                        // CUSTOMER / PRIMARY RENTER
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("CUSTOMER / PRIMARY RENTER").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("First Name:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerFirstName)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Middle Name:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerMiddleName)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Last Name:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerLastName)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Email:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerEmail)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Phone:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerPhone)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("DL#:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.DriverLicenseNumber)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("State:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.DriverLicenseState)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("License Exp:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(data.DriverLicenseExpiration?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Date of Birth:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(data.CustomerDateOfBirth?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Address:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerAddress)).FontSize(9);
                        });

                        column.Item().PaddingTop(8);

                        // ADDITIONAL DRIVER - only show if any field is filled
                        var hasAdditionalDriver = !string.IsNullOrWhiteSpace(data.AdditionalDriverFirstName) ||
                                                   !string.IsNullOrWhiteSpace(data.AdditionalDriverLastName) ||
                                                   !string.IsNullOrWhiteSpace(data.AdditionalDriverEmail) ||
                                                   !string.IsNullOrWhiteSpace(data.AdditionalDriverLicenseNumber);
                        
                        if (hasAdditionalDriver)
                        {
                            column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("ADDITIONAL DRIVER").FontSize(11).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Cell().Element(CellStyle).Text("First Name:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverFirstName)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("Middle Name:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverMiddleName)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("Last Name:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverLastName)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("Email:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverEmail)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("Phone:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverPhone)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("DL#:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverLicenseNumber)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("State:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverLicenseState)).FontSize(9);
                                table.Cell().Element(CellStyle).Text("License Exp:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(data.AdditionalDriverLicenseExpiration?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                                table.Cell().Element(CellStyle).Text("Date of Birth:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(data.AdditionalDriverDateOfBirth?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                                table.Cell().Element(CellStyle).Text("Address:").FontSize(9);
                                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverAddress)).FontSize(9);
                            });

                            column.Item().PaddingTop(8);
                        }

                        // RENTAL VEHICLE
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("RENTAL VEHICLE").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("Vehicle Type:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.VehicleType)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Make/Model:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.VehicleName)).FontSize(9);
                            
                            var yearColorLicense = string.Join(" / ", new[] {
                                data.VehicleYear?.ToString() ?? dash,
                                OrDash(data.VehicleColor),
                                OrDash(data.VehiclePlate)
                            });
                            table.Cell().Element(CellStyle).Text("Year/Color/License:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(yearColorLicense).FontSize(9);
                            table.Cell().Element(CellStyle).Text("VIN:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.VehicleVin)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Odometer:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(data.OdometerStart?.ToString("N0") ?? dash).FontSize(9);
                        });

                        column.Item().PaddingTop(8);

                        // RENTAL PERIOD
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("RENTAL PERIOD").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("Start Date:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(data.PickupDate.ToString("yyyy-MM-dd")).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Start Time:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.PickupTime)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Due Date:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(data.DueDate?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Return Date:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(data.ReturnDate.ToString("yyyy-MM-dd")).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Return Time:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.ReturnTime)).FontSize(9);
                        });

                        column.Item().PaddingTop(8);

                        // FUEL LEVEL
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("FUEL LEVEL").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("Fuel at Pickup:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.FuelAtPickup)).FontSize(9);
                            table.Cell().Element(CellStyle).Text("Fuel at Return:").FontSize(9);
                            table.Cell().Element(CellStyle).Text(OrDash(data.FuelAtReturn)).FontSize(9);
                        });

                        column.Item().PaddingTop(8);

                        // SECURITY DEPOSIT
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("SECURITY DEPOSIT").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("Security Deposit:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.DepositAmount:N2}").FontSize(9);
                        });

                        column.Item().PaddingTop(8);

                        // RENTAL RATE AND INVOICE
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("RENTAL RATE AND INVOICE").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text($"Rate per Day @ {currencySymbol}{data.DailyRate:N2}/day × {data.RentalDays} Days:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.RentalAmount:N2}").FontSize(9);
                        });

                        // Additional Services
                        if (data.AdditionalServices.Count > 0)
                        {
                            column.Item().PaddingTop(3).PaddingLeft(10).Text("Additional Services").FontSize(10).Italic();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                });

                                foreach (var service in data.AdditionalServices)
                                {
                                    table.Cell().Element(CellStyle).Text($"{service.Name} ({currencySymbol}{service.DailyRate:N2}/day × {service.Days}):").FontSize(9);
                                    table.Cell().Element(CellStyle).Text($"{currencySymbol}{service.Total:N2}").FontSize(9);
                                }
                            });
                        }

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("Subtotal:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.Subtotal:N2}").FontSize(9);
                            table.Cell().Element(CellStyleBold).Text("TOTAL CHARGES:");
                            table.Cell().Element(CellStyleBold).Text($"{currencySymbol}{data.TotalCharges:N2}");
                        });

                        column.Item().PaddingTop(8);

                        // ADDITIONAL CHARGES
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text("ADDITIONAL CHARGES").FontSize(11).Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(CellStyle).Text("Late Return Fee:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.LateReturnFee:N2}").FontSize(9);
                            table.Cell().Element(CellStyle).Text("Damage Fee:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.DamageFee:N2}").FontSize(9);
                            table.Cell().Element(CellStyle).Text("Fuel Service Fee:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.FuelServiceFee:N2}").FontSize(9);
                            table.Cell().Element(CellStyle).Text("Cleaning Fee:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.CleaningFee:N2}").FontSize(9);
                            table.Cell().Element(CellStyle).Text("Refund:").FontSize(9);
                            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.Refund:N2}").FontSize(9);
                            table.Cell().Element(CellStyleBold).Text("BALANCE DUE:");
                            table.Cell().Element(CellStyleBold).Text($"{currencySymbol}{data.BalanceDue:N2}");
                        });

                        column.Item().PaddingTop(10);

                        // Rules of Action with checkboxes
                        if (!string.IsNullOrEmpty(data.RulesText))
                        {
                            var rulesHeader = data.Language == "es" ? "REGLAS DE ACCIÓN" : "RULES OF ACTION";
                            var rulesSubheader = data.Language == "es" 
                                ? "(véase también los Términos y Condiciones a continuación)" 
                                : "(see also Terms and Conditions below)";
                            var agreeText = data.Language == "es" 
                                ? "He leído y acepto los " 
                                : "I have read and agree to the ";
                            var termsLinkText = data.Language == "es" 
                                ? "Términos y Condiciones" 
                                : "Terms and Conditions";
                            
                            column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(rulesHeader).FontSize(11).Bold();
                            column.Item().Text(rulesSubheader).FontSize(8).Italic();
                            column.Item().PaddingTop(5);
                            
                            // Split rules text by double newline and render each with checkbox
                            var rules = data.RulesText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var rule in rules)
                            {
                                if (string.IsNullOrWhiteSpace(rule)) continue;
                                
                                // Skip the header "RULES OF ACTION" if present
                                var ruleText = rule.Trim();
                                if (ruleText.StartsWith("RULES OF ACTION") || ruleText.StartsWith("REGLAS DE ACCIÓN")) continue;
                                
                                // Remove leading number and dot (e.g., "1. " or "11. ")
                                var cleanRule = Regex.Replace(ruleText, @"^\d+\.\s*", "");
                                
                                column.Item().Row(row =>
                                {
                                    // Large checked checkbox using Unicode symbol (customer agreed by signing)
                                    row.ConstantItem(18).Text("☑").FontSize(14);
                                    row.RelativeItem().Text(cleanRule).FontSize(8);
                                });
                                column.Item().PaddingBottom(2);
                            }
                            
                            column.Item().PaddingTop(3);
                        }

                        // "I have read and agree" checkbox
                        column.Item().PaddingTop(8).Row(row =>
                        {
                            // Large checked checkbox using Unicode symbol (customer agreed by signing)
                            row.ConstantItem(18).Text("☑").FontSize(14);
                            row.RelativeItem().Text(text =>
                            {
                                var agreePrefix = data.Language == "es" ? "He leído y acepto los " : "I have read and agree to the ";
                                var termsLink = data.Language == "es" ? "Términos y Condiciones" : "Terms and Conditions";
                                text.Span(agreePrefix).FontSize(10).Bold();
                                text.Span(termsLink).FontSize(10).Bold().Underline();
                            });
                        });

                        column.Item().PaddingTop(12);

                        // Signature (BEFORE Terms and Conditions)
                        var signatureHeader = data.Language == "es" ? "FIRMA DEL CLIENTE" : "CUSTOMER SIGNATURE";
                        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(signatureHeader).FontSize(11).Bold();
                        
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
                        else
                        {
                            // Empty signature box for preview
                            var signaturePlaceholder = data.Language == "es" ? "[La firma aparecerá aquí]" : "[Signature will appear here]";
                            column.Item().Height(60).Border(1).BorderColor(Colors.Grey.Medium).Background(Colors.Grey.Lighten4)
                                .AlignCenter().AlignMiddle().Text(signaturePlaceholder).FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                        }
                        
                        var signedLabel = data.Language == "es" ? "Firmado" : "Signed";
                        column.Item().PaddingTop(5).Text($"{signedLabel}: {data.SignedAt.ToString("yyyy-MM-dd HH:mm:ss UTC")}").FontSize(9);

                        column.Item().PaddingTop(15);

                        // Full Terms and Conditions (AFTER signature)
                        if (!string.IsNullOrEmpty(data.FullTermsText))
                        {
                            column.Item().PageBreak();
                            var termsHeader = data.Language == "es" 
                                ? "TÉRMINOS Y CONDICIONES DEL CONTRATO DE ALQUILER" 
                                : "RENTAL AGREEMENT TERMS AND CONDITIONS";
                            column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(termsHeader).FontSize(11).Bold();
                            column.Item().PaddingTop(5).Text(data.FullTermsText).FontSize(8);
                        }
                        // Fallback to legacy format if FullTermsText is empty
                        else if (!string.IsNullOrEmpty(data.TermsText) || !string.IsNullOrEmpty(data.NonRefundableText) || 
                                 !string.IsNullOrEmpty(data.DamagePolicyText) || !string.IsNullOrEmpty(data.CardAuthorizationText))
                        {
                            column.Item().PageBreak();
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
                        }
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
            .PaddingVertical(3)
            .PaddingHorizontal(5);
    }

    private static IContainer CellStyleBold(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(5)
            .DefaultTextStyle(x => x.Bold());
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
    
    private static string OrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}

