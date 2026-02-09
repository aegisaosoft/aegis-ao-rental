/*
 * Rental Agreement PDF Generator
 * Generates PDF documents for rental agreements using QuestPDF
 * Supports bilingual output (national language + English) for non-English agreements
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
    public string BookingNumber { get; set; } = string.Empty;
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
    public string SmsConsentText { get; set; } = string.Empty;
}

public class AdditionalServiceItem
{
    public string Name { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public int Days { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// Translations for PDF labels
/// </summary>
public static class PdfTranslations
{
    public static Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["rentalAgreement"] = "Rental Agreement",
            ["phone"] = "Phone",
            ["email"] = "Email",
            ["customerPrimaryRenter"] = "CUSTOMER / PRIMARY RENTER",
            ["firstName"] = "First Name",
            ["middleName"] = "Middle Name",
            ["lastName"] = "Last Name",
            ["driverLicense"] = "DL#",
            ["state"] = "State",
            ["licenseExp"] = "License Exp",
            ["dateOfBirth"] = "Date of Birth",
            ["address"] = "Address",
            ["additionalDriver"] = "ADDITIONAL DRIVER",
            ["rentalVehicle"] = "RENTAL VEHICLE",
            ["vehicleType"] = "Vehicle Type",
            ["makeModel"] = "Make/Model",
            ["yearColorLicense"] = "Year/Color/License",
            ["vin"] = "VIN",
            ["odometer"] = "Odometer",
            ["rentalPeriod"] = "RENTAL PERIOD",
            ["startDate"] = "Start Date",
            ["startTime"] = "Start Time",
            ["dueDate"] = "Due Date",
            ["returnDate"] = "Return Date",
            ["returnTime"] = "Return Time",
            ["fuelLevel"] = "FUEL LEVEL",
            ["fuelAtPickup"] = "Fuel at Pickup",
            ["fuelAtReturn"] = "Fuel at Return",
            ["securityDeposit"] = "SECURITY DEPOSIT",
            ["securityDepositLabel"] = "Security Deposit",
            ["rentalRateInvoice"] = "RENTAL RATE AND INVOICE",
            ["ratePerDay"] = "Rate per Day",
            ["day"] = "day",
            ["days"] = "Days",
            ["additionalServices"] = "Additional Services",
            ["subtotal"] = "Subtotal",
            ["totalCharges"] = "TOTAL CHARGES",
            ["additionalCharges"] = "ADDITIONAL CHARGES",
            ["lateReturnFee"] = "Late Return Fee",
            ["damageFee"] = "Damage Fee",
            ["fuelServiceFee"] = "Fuel Service Fee",
            ["cleaningFee"] = "Cleaning Fee",
            ["refund"] = "Refund",
            ["balanceDue"] = "BALANCE DUE",
            ["rulesOfAction"] = "RULES OF ACTION",
            ["seeTermsBelow"] = "(see also Terms and Conditions below)",
            ["iHaveReadAndAgree"] = "I have read and agree to the",
            ["termsAndConditions"] = "Terms and Conditions",
            ["customerSignature"] = "CUSTOMER SIGNATURE",
            ["signatureWillAppear"] = "[Signature will appear here]",
            ["signed"] = "Signed",
            ["rentalTermsConditions"] = "RENTAL AGREEMENT TERMS AND CONDITIONS",
            ["page"] = "Page",
            ["of"] = "of",
        },
        ["es"] = new Dictionary<string, string>
        {
            ["rentalAgreement"] = "Contrato de Alquiler",
            ["phone"] = "Teléfono",
            ["email"] = "Correo",
            ["customerPrimaryRenter"] = "CLIENTE / ARRENDATARIO PRINCIPAL",
            ["firstName"] = "Nombre",
            ["middleName"] = "Segundo Nombre",
            ["lastName"] = "Apellido",
            ["driverLicense"] = "Licencia",
            ["state"] = "Estado",
            ["licenseExp"] = "Venc. Licencia",
            ["dateOfBirth"] = "Fecha de Nacimiento",
            ["address"] = "Dirección",
            ["additionalDriver"] = "CONDUCTOR ADICIONAL",
            ["rentalVehicle"] = "VEHÍCULO DE ALQUILER",
            ["vehicleType"] = "Tipo de Vehículo",
            ["makeModel"] = "Marca/Modelo",
            ["yearColorLicense"] = "Año/Color/Placa",
            ["vin"] = "VIN",
            ["odometer"] = "Odómetro",
            ["rentalPeriod"] = "PERÍODO DE ALQUILER",
            ["startDate"] = "Fecha de Inicio",
            ["startTime"] = "Hora de Inicio",
            ["dueDate"] = "Fecha de Vencimiento",
            ["returnDate"] = "Fecha de Devolución",
            ["returnTime"] = "Hora de Devolución",
            ["fuelLevel"] = "NIVEL DE COMBUSTIBLE",
            ["fuelAtPickup"] = "Combustible al Recoger",
            ["fuelAtReturn"] = "Combustible al Devolver",
            ["securityDeposit"] = "DEPÓSITO DE SEGURIDAD",
            ["securityDepositLabel"] = "Depósito de Seguridad",
            ["rentalRateInvoice"] = "TARIFA DE ALQUILER Y FACTURA",
            ["ratePerDay"] = "Tarifa por Día",
            ["day"] = "día",
            ["days"] = "Días",
            ["additionalServices"] = "Servicios Adicionales",
            ["subtotal"] = "Subtotal",
            ["totalCharges"] = "CARGOS TOTALES",
            ["additionalCharges"] = "CARGOS ADICIONALES",
            ["lateReturnFee"] = "Cargo por Devolución Tardía",
            ["damageFee"] = "Cargo por Daños",
            ["fuelServiceFee"] = "Cargo por Combustible",
            ["cleaningFee"] = "Cargo por Limpieza",
            ["refund"] = "Reembolso",
            ["balanceDue"] = "SALDO PENDIENTE",
            ["rulesOfAction"] = "REGLAS DE ACCIÓN",
            ["seeTermsBelow"] = "(véase también los Términos y Condiciones a continuación)",
            ["iHaveReadAndAgree"] = "He leído y acepto los",
            ["termsAndConditions"] = "Términos y Condiciones",
            ["customerSignature"] = "FIRMA DEL CLIENTE",
            ["signatureWillAppear"] = "[La firma aparecerá aquí]",
            ["signed"] = "Firmado",
            ["rentalTermsConditions"] = "TÉRMINOS Y CONDICIONES DEL CONTRATO DE ALQUILER",
            ["page"] = "Página",
            ["of"] = "de",
        },
        ["pt"] = new Dictionary<string, string>
        {
            ["rentalAgreement"] = "Contrato de Locação",
            ["phone"] = "Telefone",
            ["email"] = "E-mail",
            ["customerPrimaryRenter"] = "CLIENTE / LOCATÁRIO PRINCIPAL",
            ["firstName"] = "Nome",
            ["middleName"] = "Nome do Meio",
            ["lastName"] = "Sobrenome",
            ["driverLicense"] = "CNH",
            ["state"] = "Estado",
            ["licenseExp"] = "Venc. CNH",
            ["dateOfBirth"] = "Data de Nascimento",
            ["address"] = "Endereço",
            ["additionalDriver"] = "MOTORISTA ADICIONAL",
            ["rentalVehicle"] = "VEÍCULO DE LOCAÇÃO",
            ["vehicleType"] = "Tipo de Veículo",
            ["makeModel"] = "Marca/Modelo",
            ["yearColorLicense"] = "Ano/Cor/Placa",
            ["vin"] = "Chassi",
            ["odometer"] = "Odômetro",
            ["rentalPeriod"] = "PERÍODO DE LOCAÇÃO",
            ["startDate"] = "Data de Início",
            ["startTime"] = "Hora de Início",
            ["dueDate"] = "Data de Vencimento",
            ["returnDate"] = "Data de Devolução",
            ["returnTime"] = "Hora de Devolução",
            ["fuelLevel"] = "NÍVEL DE COMBUSTÍVEL",
            ["fuelAtPickup"] = "Combustível na Retirada",
            ["fuelAtReturn"] = "Combustível na Devolução",
            ["securityDeposit"] = "DEPÓSITO DE SEGURANÇA",
            ["securityDepositLabel"] = "Depósito de Segurança",
            ["rentalRateInvoice"] = "TARIFA DE LOCAÇÃO E FATURA",
            ["ratePerDay"] = "Diária",
            ["day"] = "dia",
            ["days"] = "Dias",
            ["additionalServices"] = "Serviços Adicionais",
            ["subtotal"] = "Subtotal",
            ["totalCharges"] = "TOTAL",
            ["additionalCharges"] = "ENCARGOS ADICIONAIS",
            ["lateReturnFee"] = "Taxa por Atraso",
            ["damageFee"] = "Taxa por Danos",
            ["fuelServiceFee"] = "Taxa de Combustível",
            ["cleaningFee"] = "Taxa de Limpeza",
            ["refund"] = "Reembolso",
            ["balanceDue"] = "SALDO DEVEDOR",
            ["rulesOfAction"] = "REGRAS DE AÇÃO",
            ["seeTermsBelow"] = "(ver também os Termos e Condições abaixo)",
            ["iHaveReadAndAgree"] = "Li e concordo com os",
            ["termsAndConditions"] = "Termos e Condições",
            ["customerSignature"] = "ASSINATURA DO CLIENTE",
            ["signatureWillAppear"] = "[A assinatura aparecerá aqui]",
            ["signed"] = "Assinado",
            ["rentalTermsConditions"] = "TERMOS E CONDIÇÕES DO CONTRATO DE LOCAÇÃO",
            ["page"] = "Página",
            ["of"] = "de",
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["rentalAgreement"] = "Contrat de Location",
            ["phone"] = "Téléphone",
            ["email"] = "E-mail",
            ["customerPrimaryRenter"] = "CLIENT / LOCATAIRE PRINCIPAL",
            ["firstName"] = "Prénom",
            ["middleName"] = "Deuxième Prénom",
            ["lastName"] = "Nom",
            ["driverLicense"] = "Permis",
            ["state"] = "État",
            ["licenseExp"] = "Exp. Permis",
            ["dateOfBirth"] = "Date de Naissance",
            ["address"] = "Adresse",
            ["additionalDriver"] = "CONDUCTEUR SUPPLÉMENTAIRE",
            ["rentalVehicle"] = "VÉHICULE DE LOCATION",
            ["vehicleType"] = "Type de Véhicule",
            ["makeModel"] = "Marque/Modèle",
            ["yearColorLicense"] = "Année/Couleur/Plaque",
            ["vin"] = "NIV",
            ["odometer"] = "Compteur",
            ["rentalPeriod"] = "PÉRIODE DE LOCATION",
            ["startDate"] = "Date de Début",
            ["startTime"] = "Heure de Début",
            ["dueDate"] = "Date d'Échéance",
            ["returnDate"] = "Date de Retour",
            ["returnTime"] = "Heure de Retour",
            ["fuelLevel"] = "NIVEAU DE CARBURANT",
            ["fuelAtPickup"] = "Carburant au Départ",
            ["fuelAtReturn"] = "Carburant au Retour",
            ["securityDeposit"] = "DÉPÔT DE GARANTIE",
            ["securityDepositLabel"] = "Dépôt de Garantie",
            ["rentalRateInvoice"] = "TARIF DE LOCATION ET FACTURE",
            ["ratePerDay"] = "Tarif Journalier",
            ["day"] = "jour",
            ["days"] = "Jours",
            ["additionalServices"] = "Services Supplémentaires",
            ["subtotal"] = "Sous-total",
            ["totalCharges"] = "TOTAL",
            ["additionalCharges"] = "FRAIS SUPPLÉMENTAIRES",
            ["lateReturnFee"] = "Frais de Retour Tardif",
            ["damageFee"] = "Frais de Dommages",
            ["fuelServiceFee"] = "Frais de Carburant",
            ["cleaningFee"] = "Frais de Nettoyage",
            ["refund"] = "Remboursement",
            ["balanceDue"] = "SOLDE DÛ",
            ["rulesOfAction"] = "RÈGLES D'ACTION",
            ["seeTermsBelow"] = "(voir aussi les Conditions Générales ci-dessous)",
            ["iHaveReadAndAgree"] = "J'ai lu et j'accepte les",
            ["termsAndConditions"] = "Conditions Générales",
            ["customerSignature"] = "SIGNATURE DU CLIENT",
            ["signatureWillAppear"] = "[La signature apparaîtra ici]",
            ["signed"] = "Signé",
            ["rentalTermsConditions"] = "CONDITIONS GÉNÉRALES DU CONTRAT DE LOCATION",
            ["page"] = "Page",
            ["of"] = "de",
        },
        ["de"] = new Dictionary<string, string>
        {
            ["rentalAgreement"] = "Mietvertrag",
            ["phone"] = "Telefon",
            ["email"] = "E-Mail",
            ["customerPrimaryRenter"] = "KUNDE / HAUPTMIETER",
            ["firstName"] = "Vorname",
            ["middleName"] = "Zweiter Vorname",
            ["lastName"] = "Nachname",
            ["driverLicense"] = "Führerschein",
            ["state"] = "Bundesland",
            ["licenseExp"] = "Führerschein Abl.",
            ["dateOfBirth"] = "Geburtsdatum",
            ["address"] = "Adresse",
            ["additionalDriver"] = "ZUSÄTZLICHER FAHRER",
            ["rentalVehicle"] = "MIETFAHRZEUG",
            ["vehicleType"] = "Fahrzeugtyp",
            ["makeModel"] = "Marke/Modell",
            ["yearColorLicense"] = "Jahr/Farbe/Kennzeichen",
            ["vin"] = "FIN",
            ["odometer"] = "Kilometerstand",
            ["rentalPeriod"] = "MIETZEIT",
            ["startDate"] = "Startdatum",
            ["startTime"] = "Startzeit",
            ["dueDate"] = "Fälligkeitsdatum",
            ["returnDate"] = "Rückgabedatum",
            ["returnTime"] = "Rückgabezeit",
            ["fuelLevel"] = "KRAFTSTOFFSTAND",
            ["fuelAtPickup"] = "Kraftstoff bei Abholung",
            ["fuelAtReturn"] = "Kraftstoff bei Rückgabe",
            ["securityDeposit"] = "KAUTION",
            ["securityDepositLabel"] = "Kaution",
            ["rentalRateInvoice"] = "MIETPREIS UND RECHNUNG",
            ["ratePerDay"] = "Tagespreis",
            ["day"] = "Tag",
            ["days"] = "Tage",
            ["additionalServices"] = "Zusätzliche Leistungen",
            ["subtotal"] = "Zwischensumme",
            ["totalCharges"] = "GESAMTBETRAG",
            ["additionalCharges"] = "ZUSÄTZLICHE GEBÜHREN",
            ["lateReturnFee"] = "Gebühr für verspätete Rückgabe",
            ["damageFee"] = "Schadensgebühr",
            ["fuelServiceFee"] = "Kraftstoffgebühr",
            ["cleaningFee"] = "Reinigungsgebühr",
            ["refund"] = "Erstattung",
            ["balanceDue"] = "FÄLLIGER BETRAG",
            ["rulesOfAction"] = "VERHALTENSREGELN",
            ["seeTermsBelow"] = "(siehe auch die Allgemeinen Geschäftsbedingungen unten)",
            ["iHaveReadAndAgree"] = "Ich habe die folgenden gelesen und stimme zu",
            ["termsAndConditions"] = "Allgemeine Geschäftsbedingungen",
            ["customerSignature"] = "KUNDENUNTERSCHRIFT",
            ["signatureWillAppear"] = "[Unterschrift erscheint hier]",
            ["signed"] = "Unterschrieben",
            ["rentalTermsConditions"] = "ALLGEMEINE GESCHÄFTSBEDINGUNGEN DES MIETVERTRAGS",
            ["page"] = "Seite",
            ["of"] = "von",
        },
    };

    public static string Get(string lang, string key)
    {
        if (Labels.TryGetValue(lang, out var langLabels) && langLabels.TryGetValue(key, out var value))
            return value;
        if (Labels.TryGetValue("en", out var enLabels) && enLabels.TryGetValue(key, out var enValue))
            return enValue;
        return key;
    }
}

public class RentalAgreementPdfGenerator
{
    public byte[] Generate(RentalAgreementPdfData data)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        
        var isEnglish = data.Language == "en" || string.IsNullOrEmpty(data.Language);
        
        return Document.Create(container =>
        {
            // If not English, generate bilingual: national language first, then English
            if (!isEnglish)
            {
                // First: National language version
                GeneratePage(container, data, data.Language, false);
                
                // Second: English version
                GeneratePage(container, data, "en", true);
            }
            else
            {
                // English only
                GeneratePage(container, data, "en", false);
            }
        })
        .GeneratePdf();
    }

    private void GeneratePage(IDocumentContainer container, RentalAgreementPdfData data, string lang, bool isSecondVersion)
    {
        var t = (string key) => PdfTranslations.Get(lang, key);
        
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header()
                .Column(column =>
                {
                    var titleSuffix = isSecondVersion ? "" : (lang != "en" ? $" ({GetLanguageName(lang)})" : "");
                    column.Item().Text($"{t("rentalAgreement")} - {data.BookingNumber}{titleSuffix}")
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
                        column.Item().Text($"{t("phone")}: {data.CompanyPhone}").FontSize(9).AlignCenter();
                    }
                    
                    if (!string.IsNullOrEmpty(data.CompanyEmail))
                    {
                        column.Item().Text($"{t("email")}: {data.CompanyEmail}").FontSize(9).AlignCenter();
                    }
                });

            page.Content()
                .PaddingVertical(0.5f, Unit.Centimetre)
                .Column(column =>
                {
                    GenerateContent(column, data, lang, t);
                });

            page.Footer()
                .AlignCenter()
                .Text(x =>
                {
                    x.Span($"{t("page")} ").FontSize(9).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                    x.Span($" {t("of")} ").FontSize(9).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                });
        });
    }

    private void GenerateContent(ColumnDescriptor column, RentalAgreementPdfData data, string lang, Func<string, string> t)
    {
        var dash = "-";
        var currencySymbol = GetCurrencySymbol(data.Currency);
        
        // CUSTOMER / PRIMARY RENTER
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("customerPrimaryRenter")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("firstName")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerFirstName)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("middleName")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerMiddleName)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("lastName")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerLastName)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("email")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerEmail)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("phone")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.CustomerPhone)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("driverLicense")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.DriverLicenseNumber)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("state")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.DriverLicenseState)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("licenseExp")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(data.DriverLicenseExpiration?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("dateOfBirth")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(data.CustomerDateOfBirth?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("address")}:").FontSize(9);
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
            column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("additionalDriver")).FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Cell().Element(CellStyle).Text($"{t("firstName")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverFirstName)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("middleName")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverMiddleName)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("lastName")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverLastName)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("email")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverEmail)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("phone")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverPhone)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("driverLicense")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverLicenseNumber)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("state")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverLicenseState)).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("licenseExp")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(data.AdditionalDriverLicenseExpiration?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("dateOfBirth")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(data.AdditionalDriverDateOfBirth?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
                table.Cell().Element(CellStyle).Text($"{t("address")}:").FontSize(9);
                table.Cell().Element(CellStyle).Text(OrDash(data.AdditionalDriverAddress)).FontSize(9);
            });

            column.Item().PaddingTop(8);
        }

        // RENTAL VEHICLE
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("rentalVehicle")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("vehicleType")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.VehicleType)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("makeModel")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.VehicleName)).FontSize(9);
            
            var yearColorLicense = string.Join(" / ", new[] {
                data.VehicleYear?.ToString() ?? dash,
                OrDash(data.VehicleColor),
                OrDash(data.VehiclePlate)
            });
            table.Cell().Element(CellStyle).Text($"{t("yearColorLicense")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(yearColorLicense).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("vin")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.VehicleVin)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("odometer")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(data.OdometerStart?.ToString("N0") ?? dash).FontSize(9);
        });

        column.Item().PaddingTop(8);

        // RENTAL PERIOD
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("rentalPeriod")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("startDate")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(data.PickupDate.ToString("yyyy-MM-dd")).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("startTime")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.PickupTime)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("dueDate")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(data.DueDate?.ToString("yyyy-MM-dd") ?? dash).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("returnDate")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(data.ReturnDate.ToString("yyyy-MM-dd")).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("returnTime")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.ReturnTime)).FontSize(9);
        });

        column.Item().PaddingTop(8);

        // FUEL LEVEL
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("fuelLevel")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("fuelAtPickup")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.FuelAtPickup)).FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("fuelAtReturn")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text(OrDash(data.FuelAtReturn)).FontSize(9);
        });

        column.Item().PaddingTop(8);

        // SECURITY DEPOSIT
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("securityDeposit")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("securityDepositLabel")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.DepositAmount:N2}").FontSize(9);
        });

        column.Item().PaddingTop(8);

        // RENTAL RATE AND INVOICE
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("rentalRateInvoice")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("ratePerDay")} @ {currencySymbol}{data.DailyRate:N2}/{t("day")} × {data.RentalDays} {t("days")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.RentalAmount:N2}").FontSize(9);
        });

        // Additional Services
        if (data.AdditionalServices.Count > 0)
        {
            column.Item().PaddingTop(3).PaddingLeft(10).Text(t("additionalServices")).FontSize(10).Italic();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                foreach (var service in data.AdditionalServices)
                {
                    table.Cell().Element(CellStyle).Text($"{service.Name} ({currencySymbol}{service.DailyRate:N2}/{t("day")} × {service.Days}):").FontSize(9);
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

            table.Cell().Element(CellStyle).Text($"{t("subtotal")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.Subtotal:N2}").FontSize(9);
            table.Cell().Element(CellStyleBold).Text($"{t("totalCharges")}:");
            table.Cell().Element(CellStyleBold).Text($"{currencySymbol}{data.TotalCharges:N2}");
        });

        column.Item().PaddingTop(8);

        // ADDITIONAL CHARGES
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("additionalCharges")).FontSize(11).Bold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            table.Cell().Element(CellStyle).Text($"{t("lateReturnFee")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.LateReturnFee:N2}").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("damageFee")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.DamageFee:N2}").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("fuelServiceFee")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.FuelServiceFee:N2}").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("cleaningFee")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.CleaningFee:N2}").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{t("refund")}:").FontSize(9);
            table.Cell().Element(CellStyle).Text($"{currencySymbol}{data.Refund:N2}").FontSize(9);
            table.Cell().Element(CellStyleBold).Text($"{t("balanceDue")}:");
            table.Cell().Element(CellStyleBold).Text($"{currencySymbol}{data.BalanceDue:N2}");
        });

        column.Item().PaddingTop(10);

        // Rules of Action with checkboxes
        if (!string.IsNullOrEmpty(data.RulesText))
        {
            column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("rulesOfAction")).FontSize(11).Bold();
            column.Item().Text(t("seeTermsBelow")).FontSize(8).Italic();
            column.Item().PaddingTop(5);
            
            // Split rules text by double newline and render each with checkbox
            var rules = data.RulesText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule)) continue;
                
                // Skip the header if present
                var ruleText = rule.Trim();
                if (ruleText.StartsWith("RULES OF ACTION") || ruleText.StartsWith("REGLAS DE ACCIÓN") ||
                    ruleText.StartsWith("REGRAS DE AÇÃO") || ruleText.StartsWith("RÈGLES D'ACTION") ||
                    ruleText.StartsWith("VERHALTENSREGELN")) continue;
                
                // Remove leading number and dot (e.g., "1. " or "11. ")
                var cleanRule = Regex.Replace(ruleText, @"^\d+\.\s*", "");
                
                column.Item().Row(row =>
                {
                    // Large checked checkbox using Unicode symbol
                    row.ConstantItem(18).Text("☑").FontSize(14);
                    row.RelativeItem().Text(cleanRule).FontSize(8);
                });
                column.Item().PaddingBottom(2);
            }
            
            column.Item().PaddingTop(3);
        }

        // SMS Consent - moved to second-to-last position
        if (!string.IsNullOrEmpty(data.SmsConsentText))
        {
            Console.WriteLine($"PDF Generator: Rendering SMS Consent section with text: {data.SmsConsentText}");
            column.Item().PaddingTop(8).Row(row =>
            {
                // Large checked checkbox using Unicode symbol
                row.ConstantItem(18).Text("☑").FontSize(14);
                row.RelativeItem().Text(data.SmsConsentText).FontSize(10);
            });
        }
        else
        {
            Console.WriteLine("PDF Generator: SMS Consent text is empty, skipping section");
        }

        // "I have read and agree" checkbox - now last
        column.Item().PaddingTop(8).Row(row =>
        {
            row.ConstantItem(18).Text("☑").FontSize(14);
            row.RelativeItem().Text(text =>
            {
                text.Span($"{t("iHaveReadAndAgree")} ").FontSize(10).Bold();
                text.Span(t("termsAndConditions")).FontSize(10).Bold().Underline();
            });
        });

        column.Item().PaddingTop(12);

        // Signature
        column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("customerSignature")).FontSize(11).Bold();
        
        if (!string.IsNullOrEmpty(data.SignatureImage))
        {
            try
            {
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
            column.Item().Height(60).Border(1).BorderColor(Colors.Grey.Medium).Background(Colors.Grey.Lighten4)
                .AlignCenter().AlignMiddle().Text(t("signatureWillAppear")).FontSize(9).Italic().FontColor(Colors.Grey.Medium);
        }
        
        column.Item().PaddingTop(5).Text($"{t("signed")}: {data.SignedAt.ToString("yyyy-MM-dd HH:mm:ss UTC")}").FontSize(9);

        column.Item().PaddingTop(15);

        // Full Terms and Conditions
        if (!string.IsNullOrEmpty(data.FullTermsText))
        {
            column.Item().PageBreak();
            column.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(t("rentalTermsConditions")).FontSize(11).Bold();
            column.Item().PaddingTop(5).Text(data.FullTermsText).FontSize(8);
        }
        else if (!string.IsNullOrEmpty(data.TermsText) || !string.IsNullOrEmpty(data.NonRefundableText) ||
                 !string.IsNullOrEmpty(data.DamagePolicyText) || !string.IsNullOrEmpty(data.CardAuthorizationText))
        {
            column.Item().PageBreak();
            column.Item().PaddingBottom(5).Text(t("termsAndConditions")).FontSize(14).Bold();
            
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
    }

    private static string GetLanguageName(string lang)
    {
        return lang switch
        {
            "es" => "Español",
            "pt" => "Português",
            "fr" => "Français",
            "de" => "Deutsch",
            _ => lang.ToUpper()
        };
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
