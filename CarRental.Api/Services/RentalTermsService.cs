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
 * Author: Alexander Orlov Aegis AO Soft
 *
 */

namespace CarRental.Api.Services;

/// <summary>
/// Unified service for generating rental agreement terms and conditions.
/// Used by both preview and final/signed agreements to ensure consistency.
/// </summary>
public static class RentalTermsService
{
    /// <summary>
    /// Get Rules of Action and Full Terms texts by language.
    /// This method is used by both preview and final rental agreements to ensure consistency.
    /// </summary>
    /// <param name="language">Language code (e.g., "en", "es", "pt", "fr", "de")</param>
    /// <returns>Tuple containing Rules text and Full Terms text</returns>
    public static (string RulesText, string FullTermsText) GetRulesAndTermsTexts(string language)
    {
        var lang = language?.ToLower().Substring(0, Math.Min(2, language?.Length ?? 0)) ?? "en";

        return lang switch
        {
            "es" => (
                RulesText: "REGLAS DE ACCIÓN\n\n" +
                    "1. CONDUCTOR AUTORIZADO: Está absolutamente PROHIBIDO usar u operar este vehículo alquilado por cualquier persona que no esté listada en el contrato de alquiler. Cada conductor debe ser precalificado en persona con licencia de conducir válida y tener un mínimo de 21 años de edad. Si el conductor es menor de 25 años pueden aplicar tarifas adicionales.\n\n" +
                    "2. ALCOHOL Y DROGAS: Está absolutamente PROHIBIDO usar u operar este vehículo alquilado por cualquier persona que esté bajo la influencia de ALCOHOL o cualquier NARCÓTICO.\n\n" +
                    "3. NO FUMAR: NO está permitido FUMAR en el vehículo alquilado bajo ninguna circunstancia. Cualquier evidencia encontrada de cualquier tipo de fumar en el vehículo aplicará una multa independientemente de si hay daño o no.\n\n" +
                    "4. LLAVES PERDIDAS: En caso de LLAVES PERDIDAS, DAÑADAS o BLOQUEADAS DENTRO que requieran recuperación de llaves, puede aplicar un cargo máximo.\n\n" +
                    "5. CAPACIDAD DE PASAJEROS: La capacidad de pasajeros de este vehículo está determinada por el número de cinturones de seguridad y, por ley, NO debe EXCEDERSE. Mientras esté en el vehículo, siempre use el cinturón de seguridad. ¡Es la ley!\n\n" +
                    "6. TARIFA DE LIMPIEZA: En caso de que el vehículo sea devuelto excepcionalmente sucio, puede aplicar una TARIFA DE LIMPIEZA.\n\n" +
                    "7. NEUMÁTICOS: Los NEUMÁTICOS pinchados o dañados son responsabilidad del Arrendatario y son la responsabilidad financiera del Arrendatario o Conductor(es) Autorizado(s).\n\n" +
                    "8. MULTAS: TODAS LAS MULTAS, PENALIZACIONES, TARIFAS y otros recibidos durante el período de alquiler, causados como resultado del arrendatario y/o período de alquiler deben ser pagados por el Arrendatario.\n\n" +
                    "9. PERÍODO DE 24 HORAS: Los Días de Alquiler se basan en Períodos de Alquiler de 24 horas. Por favor devuelva a tiempo. Cada hora pasada del Período de Alquiler se calculará a ¼ del cargo diario incluyendo todos los impuestos y tarifas y hasta un día completo.\n\n" +
                    "10. USO DE CELULAR: No usar el celular mientras opera el Vehículo a menos que sea un dispositivo manos libres como Bluetooth. Es la ley, el conductor tiene prohibido manejar cualquier dispositivo electrónico mientras conduce.\n\n" +
                    "11. AUTORIZACIÓN DE TARJETA: Autorizo a la empresa de alquiler a cargar en mi tarjeta de crédito/débito: el monto del alquiler, depósito de seguridad, cargos de combustible si aplica, infracciones de tráfico, multas de estacionamiento, cargos de peaje, reparaciones por daños, tarifas de limpieza y cualquier otro cargo incurrido durante o como resultado de este alquiler.",
                FullTermsText: GetFullTermsTextSpanish()
            ),
            _ => ( // English (default)
                RulesText: "RULES OF ACTION\n\n" +
                    "1. AUTHORIZED DRIVERS ONLY: It is absolutely PROHIBITED to use or operate this rented vehicle by anyone not listed on the rental agreement. Each driver must be pre-qualified in person with valid driver's license and be a minimum of 21 years of age. If Driver is under the age of 25 additional fees may apply.\n\n" +
                    "2. NO ALCOHOL OR NARCOTICS: It is absolutely PROHIBITED to use or operate this rented vehicle by anyone who is under the influence of ALCOHOL or any NARCOTICS.\n\n" +
                    "3. NO SMOKING POLICY: There is absolutely NO SMOKING allowed in the rented vehicle under any circumstances. Any found evidence of any kind of smoking in the vehicle will result in a fine regardless of damage or not.\n\n" +
                    "4. LOST KEYS POLICY: In case of LOST, DAMAGED or LOCKED INSIDE KEYS requiring key recovery, a maximum charge may apply.\n\n" +
                    "5. PASSENGER CAPACITY: The passenger capacity of this vehicle is determined by the number of seatbelts and, by law, must NOT be EXCEEDED. While in the Vehicle, please always fasten your seatbelts. It is the law!\n\n" +
                    "6. CLEANING FEE: In case the Vehicle will be returned exceptionally dirty, a CLEANING FEE may apply.\n\n" +
                    "7. TIRES: Flat or damaged TIRES are the responsibility of the Renter and are the financial responsibility of the Renter or Authorized Driver(s).\n\n" +
                    "8. TICKETS AND FINES: ALL TICKETS, FINES, FEES and others received during the rental period, caused as a result of the renter and/or rental period must be paid by the Renter.\n\n" +
                    "9. 24-HOUR RENTAL PERIOD: Rental Days are based on 24-hour Rental Periods. Please return at the proper time. Each hour past the Rental Period will be calculated at ¼ of a day charge including all taxes and fees and up to a full day.\n\n" +
                    "10. NO CELL PHONE USE: No cell phone use while operating the Vehicle unless it is a hands-free device such as Bluetooth. It's the law - the driver is prohibited from handling any electronic device while driving regardless of the behavior of use.\n\n" +
                    "11. CARD AUTHORIZATION: I authorize the rental company to charge my credit/debit card on file for: the rental amount, security deposit, fuel charges if applicable, traffic violations, parking tickets, toll charges, damage repairs, cleaning fees, and any other charges incurred during or as a result of this rental.",
                FullTermsText: GetFullTermsTextEnglish()
            )
        };
    }

    /// <summary>
    /// Get SMS Consent text by language
    /// </summary>
    public static string GetSmsConsentText(string language)
    {
        var lang = language?.ToLower().Substring(0, Math.Min(2, language?.Length ?? 0)) ?? "en";

        return lang switch
        {
            "es" => "Consiento recibir notificaciones SMS y actualizaciones relacionadas con mi alquiler.",
            "pt" => "Consinto em receber notificações SMS e atualizações relacionadas ao meu aluguel.",
            "fr" => "Je consens à recevoir des notifications SMS et des mises à jour relatives à ma location.",
            "de" => "Ich stimme zu, SMS-Benachrichtigungen und Updates zu meiner Anmietung zu erhalten.",
            _ => "I consent to receive SMS notifications and updates related to my rental." // English (default)
        };
    }

    private static string GetFullTermsTextEnglish()
    {
        return @"3. ELECTRONIC COMMUNICATIONS AND TELEMATICS

(a) You agree that the Company may use electronic or verbal means to contact You. You agree that the Company may use any email address or telephone number You provide to contact You, including manual calling, voice messages, text messages, emails or automatic telephone dialing systems. The Car may be equipped with global positioning technology or other telematics systems and a transmitter that allows the Company to track or otherwise locate the Car and privacy is not guaranteed. Information collected by any such technology or telematics is governed by the Company's privacy policy. You acknowledge that the data derived from the in-Car telematics and other devices may contain personal information and You authorize the Company to share that data with the device manufacturer, the original equipment manufacturer and its affiliates (collectively, ""OEM""), service providers, and other third parties to whom the Company or OEM grants access. To the extent permitted by law, You authorize the Company, the OEM and any third-party service provider's use of the technology included in the Car, including to track the location of the Car, to disable the Car and to assist in the repossession of the Car. It is Your responsibility to delete any Bluetooth synced data from the Car upon Your return. You acknowledge and agree that, to the extent permitted by applicable law, the Company, the OEM and any third-party service provider may collect, process, charge on the basis of, add to Your customer profile and take disciplinary action on the basis of the data derived from in-Car telematics and other devices and gauges. Actions may include suspension or termination of Your ability to continue to rent Cars from the Company or its affiliates.

(b) The Car may have telematics, tracking, and related services in which case, You understand that Your access and use of the Car or the services are subject to the Car, service provider's or device manufacturer's terms and privacy statement, which may include other terms, service limitations, warranty exclusions, limitations of liability, wireless service provider terms and privacy practices.

(c) Upon return, if the Car requires more than the Company's standard cleaning on its return, the Company may charge You for the actual costs incurred by the Company to have the Car cleaned.

(d) In California: electronic service technology included in the Car may be activated if the Car is not returned within 72 hours after the contracted return date or extension of the return date.

(e) For rentals commencing in Arizona, it is required by law that You acknowledge Your understanding that it will be a violation of Arizona Statutes 131806 if the Car is not returned within 72 hours of the due date and time specified on the rental record and that You shall be subject to a maximum penalty not to exceed US$150,000 and/or imprisonment of 2.25 years. By renting a Car under this agreement, You acknowledge that You have received and understand this notice.

(f) For rentals in the District of Columbia, it is required by law that You be notified that if You fail to return the Car in accordance with this Agreement, it may result in a criminal penalty of up to 3 years in jail.

4. YOUR RESPONSIBILITY FOR LOSS OF OR DAMAGE TO THE CAR AND OPTIONAL LOSS DAMAGE WAIVER

(a) Except as stated below, You are responsible for any and all loss of or damage to the Car resulting from any cause including but not limited to collision, rollover, theft, vandalism, seizure, fire, flood, hail or other acts of nature or god regardless of fault.

(b) Except as stated below, Your responsibility will not exceed the greater of the retail fair market value of the Car and its manufacturer buyback program value at the time the Car is lost or damaged, less its salvage value, plus actual towing, storage and impound fees, diminution of value of the Car as determined by the Company, an administrative charge and a charge for loss of use, regardless of fleet utilization. As more generally provided in paragraph 6, the Company may, where permitted under applicable law, process one or more vouchers or payment slips against Your credit, charge or debit Card for these losses, costs and charges, together with any other applicable charges, at or following the completion of the rental.

(c) Your responsibility for damage due to theft or otherwise is limited by law in certain jurisdictions. As of June 1, 2020, the following limitations exist. Should the laws imposing these regulations be repealed, the provisions of subparagraphs 4(a) and 4(b) shall apply without such limitations.

CALIFORNIA: For rentals commencing in California: (A) You are only responsible for loss of or damage to the Car resulting from collision, rollover, theft or vandalism, (B) Your responsibility for loss or damage to the Car will in no event exceed the fair market value of the Car at the time it is lost or damaged, plus actual charges for towing, storage and impound fees, and an administrative charge, (C) Your responsibility for loss of or damage to the Car resulting from vandalism unrelated to the theft of the Car will not exceed US$500 and (D) You are not responsible for loss of or damage to the Car resulting from theft unless it results from a failure to exercise ordinary Care by You or any Authorized Operator.

ILLINOIS: For rentals commencing in Illinois, for a Car with an MSRP of $50,000 or less, Your responsibility for loss or damage due to causes other than theft will not exceed $19,500 through May 31, 2021, which limit will increase by $500 per year starting June 1, 2021; and Your responsibility for theft will not exceed $2,000 unless it is established that You or an Authorized Operator failed to exercise ordinary Care while in possession of the Car or committed or aided in the commission of the theft. For a Car with an MSRP of more than $50,000, Your responsibility for loss or damage due to causes other than theft, and for theft, will not exceed $50,000 through September 30, 2020, which limit will increase by $1,000 per year starting October 1, 2020.

INDIANA: For rentals in Indiana, You will be responsible for no more than: (A) loss or damage to the Car up to its fair market value resulting from the collision, theft or vandalism, (B) loss of use of the Car, if You are liable for damage, (C) actual charges for towing, storage, and impound fees paid by the Company, if You are liable for the damage, and (D) an administrative charge.

NEVADA: For rentals in Nevada: (A) Your responsibility for loss or damage to the Car will not exceed the fair market value of the Car at the time the Car is lost or damaged plus actual towing, storage and impound fees, an administrative charge and a reasonable charge for loss of use, regardless of fleet utilization; (B) Your responsibility for damage to the Car and loss of use of the Car resulting from vandalism not related to the theft of the Car and not caused by You will not exceed $2500; and (C) You are not responsible for loss of or damage to the Car resulting from theft or vandalism related to the theft if You have possession of the ignition key or You establish that the ignition key was not in the Car at the time of the theft, You file an official report of the theft with the police within 24 hours of learning of the theft and You cooperate with the Company and the police in providing information regarding the theft, and neither You nor an Authorized Operator committed or aided and abetted the commission of the theft.

NEW YORK: For rentals commencing in New York: (A) This Agreement offers, for an additional charge, optional vehicle protection to cover Your financial responsibility for damage or loss to the Car. The purchase of optional vehicle protection is optional and may be declined. You are advised to carefully consider whether to purchase this protection if You have rental vehicle collision coverage provided by Your credit Card or automobile insurance policy. Before deciding whether to purchase optional vehicle protection, You may wish to determine whether Your credit Card or Your vehicle insurance affords You coverage for damage to the Car and the amount of deductible under such coverage. (B) Your responsibility for loss or damage to the Car will not exceed the lesser of (a) the actual and reasonable costs incurred by the Company to repair the Car or which the Company would have incurred if the Car was repaired, which shall reflect any discounts, price reductions or adjustments available to the Company; or (b) the fair market value of the Car at the time the Car is lost or damaged, less any net disposal proceeds. You will not be responsible for damages incurred by the Company for the loss of use of the Car, related administrative charges, or amounts that the Company recovers from any other party. You are not responsible for mechanical damage unrelated to an accident or that could reasonably be expected from normal use of the Car except in instances where abuse or neglect by You or an Authorized Operator is shown.

WISCONSIN: For rentals commencing in Wisconsin, (A) You are not responsible for any damage to the Car other than damage (x) resulting from an accident occurring while the Car is under this agreement or (y) caused intentionally by, or by the reckless or wanton misconduct of, You or an Authorized Operator; and (B) Your responsibility will in no event exceed the fair market value of the Car immediately before the damage occurs, less its salvage value, plus actual towing fees and storage fees for no more than 2 days.

(d) You grant the Company a limited power of attorney to present claims for damage to or loss of the Car to Your insurance Carrier. For rentals which commence in New Mexico or New York, if such coverage exists under Your automobile insurance policy, You may require that the Company submit any claims to Your insurance Carrier as Your agent.

5. PROHIBITED USE OF THE CAR

Neither You nor any Authorized Operator may:

• Permit the use of the Car by anyone other than You or an Authorized Operator;
• Intentionally destroy, damage or aid in the theft of the Car;
• Take or attempt to take the Car into Mexico or to anywhere else outside of the United States or Canada, except as expressly permitted under this agreement;
• Engage in any willful or wanton misconduct, which, among other things, may include reckless conduct such as: the failure to use seat belts, the failure to use child seats or other child restraints where legally required, use of the Car when overloaded or carrying passengers in excess of the number of seat belts in the Car, use off paved roads or on roads which are not regularly maintained, refueling the Car with the wrong type of fuel, leaving the Car and failing to remove the keys, or failing to close and lock all doors, Car windows or the trunk;
• Use or permit the use of the Car while legally intoxicated or under the influence of alcohol, drugs or other absorbed elements which may adversely affect a person's ability to drive safely;
• Use the Car for any purpose that could properly be charged as a crime, such as the illegal transportation of persons, drugs or contraband or any act of terrorism;
• Use the Car to tow or push anything, in a speed test, speed contest, race, rally, or in driver training activity;
• Use the Car to carry persons or property for hire (for a charge or fee), unless specifically authorized in writing by the Company.

Any use of the Car in a manner prohibited in this paragraph 5: (i) to the extent permitted by applicable law, will cause You to lose the benefit of any limitation on Your liability for loss of or damage to the Car, even if You have accepted LDW; (ii) to the extent permitted by applicable law, void personal accident insurance (""PAI"") and personal effects coverage (""PEC""), liability insurance supplement (""LIS"") coverage, emergency sickness protection (""ESP"") and any liability protection provided by the Company under this agreement; and (iii) will constitute a breach of this agreement, making You responsible, to the fullest extent permitted by law, for the actual and consequential damages to the Company caused by the breach, together with the Company's related costs and attorneys' fees.

6. PAYMENT OF CHARGES

(a) You and any person, corporation or other entity to whom, with the Company's consent, You expressly direct the charges in any way incurred under this Agreement (""Charges"") to be billed, are jointly and severally responsible for payment of all charges. If You direct Charges to be billed to any person, corporation or other entity, You represent that You are authorized to do so. Charges not paid on time as required by this Agreement may be subject to a late payment fee. You may also be charged a fee for any check used for payment of Charges that is returned to the Company unpaid or for any credit, charge, debit or stored value/prepaid/gift Card charges which are not honored by the Card issuer.

(b) Payment for all Charges is due at the completion of the rental in cash or by a credit Card, charge Card, debit Card or other device acceptable to the Company. IF YOU PRESENT A CREDIT, CHARGE CARD OR DEBIT/CHECK CARD AT THE COMMENCEMENT OF THE RENTAL, YOU AUTHORIZE THE COMPANY TO RESERVE CREDIT WITH, OR OBTAIN AN AUTHORIZATION FROM, THE CARD ISSUER AT THE TIME OF RENTAL, IN AN AMOUNT THAT MAY BE GREATER THAN THE ESTIMATED CHARGES. IF YOU USE A DEBIT/CHECK CARD TO QUALIFY FOR A RENTAL, THE COMPANY WILL NOT BE LIABLE FOR OVERDRAFT CHARGES, OR FOR ANY OTHER LOSSES OR LIABILITIES WHICH YOU MAY INCUR.

(c) The Company may from time to time issue prepaid vouchers, coupons represented either by documents or by entries in the Company's records (""Vouchers"") which may be used to pay rental charges subject to the terms and conditions of the Vouchers. Vouchers must be submitted at the time that the rental commences. Restrictions on the use of Vouchers may apply.

7. COMPUTATION OF CHARGES

(a) TIME CHARGES are computed at the rates specified on the Rental Record for days, weeks, months, extra hours and extra days. THE MINIMUM RENTAL CHARGE IS FOR ONE RENTAL DAY. RENTAL DAYS CONSIST OF CONSECUTIVE 24 HOUR PERIODS STARTING AT THE TIME THE RENTAL BEGINS. RENTAL RATE IS SUBJECT TO INCREASE IF YOU RETURN THE CAR MORE THAN 24 HOURS BEFORE OR 24 HOURS AFTER THE SCHEDULED RETURN TIME. LATE RETURNS BEYOND 29 MINUTE GRACE PERIOD SUBJECT TO EXTRA HOUR AND/OR EXTRA DAY CHARGES.

(b) MILEAGE CHARGES, including those for extra miles, if any, are based on the per mile rate specified on the Rental Record. The number of miles driven is determined by subtracting the Car's odometer reading at the beginning of the rental from the reading when the Car is returned, excluding tenths of miles.

(c) A SERVICE CHARGE may be applied if You return the Car to any location other than the location from which it is rented.

(d) LDW, PERS, PAI/PEC, ESP and LIS CHARGES, if applicable, are due and payable in full for each full or partial rental day, at the rates specified on the Rental Record.

(e) TAXES, TAX REIMBURSEMENTS, VEHICLE LICENSING FEES, AIRPORT AND/OR HOTEL RELATED FEES AND FEE RECOVERIES, GOVERNMENTAL OR OTHER SURCHARGES AND SIMILAR FEES are charged/recovered at the rates specified on the Rental Record or as otherwise required by applicable law.

(f) TOLL, PARKING & TRAFFIC OCCURRENCES/VIOLATIONS: YOU WILL BE RESPONSIBLE FOR AND PAY ALL TOLL OCCURRENCES, ALL PARKING, TRAFFIC AND TOLL VIOLATIONS, OTHER EXPENSES AND PENALTIES, ALL TOWING, STORAGE AND IMPOUND FEES AND ALL TICKETS CHARGED TO THE CAR ARISING OUT OF THE USE, POSSESSION OR OPERATION OF THE CAR BY YOU OR BY AN AUTHORIZED OPERATOR. You agree to pay, upon billing, applicable service fees (typically $30) and other fees related to such toll occurrences or toll, parking or traffic violations. For rentals throughout the U.S., including Hawaii: The amount of the service fee which You will be charged if the Company or the Toll Payment Processor is required to pay for such an infraction or toll occurrence is up to $42.00 per toll occurrence or citation.

(g) RECOVERY EXPENSE consists of all costs of any kind incurred by the Company in recovering the Car either under this Agreement, or if it is seized by governmental authorities as a result of its use by You, any Authorized Operator or any other operator with Your permission, including, but not limited to, all attorneys' fees and court costs.

(h) COLLECTION EXPENSE consists of all costs of any kind incurred by the Company in collecting Charges from You or the person to whom they are billed, including, but not limited to, all attorneys' fees and court costs.

(i) LATE PAYMENT FEES may be applied to any balance due for Charges that are not paid within 30 days of the Company's mailing an invoice for such Charges to You or the person to whom they are to be billed.

(j) FINES AND OTHER EXPENSES include, but are not limited to, fines, penalties, attorneys' fees and court costs assessed against or paid by the Company resulting from the use of the Car by You, any Authorized Operator or any other operator with Your permission.

(k) CHARGES FOR ADDITIONAL SERVICES, such as In Car Navigation System, alternative GPS or other navigation systems, and infant and toddler Car seats, if applicable, will be charged at the rates specified on the Rental Record.

(l) RETURN CHANGE FEE: A one-time Return Change Fee will be applied if You desire to extend Your rental or return the Car to a different location and You do not notify the Company at least 12 hours prior to Your scheduled return date/time. Failure to notify the Company of any change in Your scheduled return date/time or location will result in a one-time fee plus the cost of the rental based on the actual day and location of return.

(m) LOST KEYS/KEY FOBS/LOCKOUTS: If You lose the keys/key fob to the Car, the Company may charge You for the cost of replacing the keys or key fob and for the cost of delivering replacement keys/key fob (if possible) or towing the Car to the nearest Company location.

(n) LOST/BROKEN GPS UNITS, CAR SEATS, ETC.: If GPS units, Car Seats, or any other separately provided product is lost, stolen, or broken while on rent, You must notify the Company, and You will be responsible for replacement, delivery, and service costs.

(o) SMOKING FEE: In the event it is determined by Company personnel that You smoked in the Car (based on odor, test strips, or other mechanisms) or the Car smells of cigarette, marijuana, or other smoke, You will be charged a fee.

8. REFUELING OPTIONS

Most rentals come with a full tank of gas, but that is not always the case. There are three refueling options:

(1) IF YOU DO NOT PURCHASE FUEL FROM THE COMPANY AT THE BEGINNING OF YOUR RENTAL AND YOU RETURN THE CAR WITH AT LEAST AS MUCH FUEL AS WAS IN IT WHEN YOU RECEIVED IT, You will not pay the Company a charge for fuel.

(2) IF YOU DO NOT PURCHASE FUEL FROM THE COMPANY AT THE BEGINNING OF YOUR RENTAL AND YOU RETURN THE CAR WITH LESS FUEL THAN WAS IN IT WHEN YOU RECEIVED IT, the Company will charge You a Fuel and Service Charge at the applicable per-mile/kilometer or per-gallon rate specified on the Rental Record.

(3) IF YOU CHOOSE TO PURCHASE FUEL FROM THE COMPANY AT THE BEGINNING OF YOUR RENTAL BY SELECTING THE FUEL PURCHASE OPTION, You will be charged as shown on the Rental Record for that purchase. IF YOU CHOOSE THIS OPTION, YOU WILL NOT INCUR AN ADDITIONAL FUEL AND SERVICE CHARGE, BUT YOU WILL NOT RECEIVE ANY CREDIT FOR FUEL LEFT IN THE TANK AT THE TIME OF RETURN.

THE PER GALLON COST OF THE FUEL PURCHASE OPTION WILL ALWAYS BE LOWER THAN THE FUEL AND SERVICE CHARGE. BUT IF YOU ELECT THE FUEL PURCHASE OPTION YOU WILL NOT RECEIVE CREDIT FOR FUEL LEFT IN THE TANK AT THE TIME OF RETURN. THE COST OF REFUELING THE CAR YOURSELF AT A LOCAL SERVICE STATION WILL GENERALLY BE LOWER THAN THE FUEL AND SERVICE CHARGE OR THE FUEL PURCHASE OPTION.

9. ARBITRATION AND CLASS ACTION WAIVER

THIS AGREEMENT REQUIRES ARBITRATION OR A SMALL CLAIMS COURT CASE ON AN INDIVIDUAL BASIS, RATHER THAN JURY TRIALS OR CLASS ACTIONS. BY ENTERING INTO THIS AGREEMENT, YOU AGREE TO THIS ARBITRATION PROVISION. Except for claims for property damage, personal injury or death, ANY DISPUTES BETWEEN YOU AND US MUST BE RESOLVED ONLY BY ARBITRATION OR IN A SMALL CLAIMS COURT ON AN INDIVIDUAL BASIS; CLASS ARBITRATIONS AND CLASS ACTIONS ARE NOT ALLOWED. YOU AND WE EACH WAIVE THE RIGHT TO A TRIAL BY JURY OR TO PARTICIPATE IN A CLASS ACTION. The arbitration will take place in the county of Your billing address unless agreed otherwise. The American Arbitration Association (""AAA"") will administer any arbitration pursuant to its Consumer Arbitration Rules.

10. RESPONSIBILITY FOR PROPERTY

You agree that the Company is not responsible to You, any Authorized Operators or anyone else for any loss of or damage to Your or their personal property caused by Your or their acts or omissions, those of any third party or, to the extent permitted by law, by the Company's negligence. You and any Authorized Operators hereby waive any claim against the Company, its agents or employees, for loss of or damage to Your or anyone else's personal property, which includes, without limitation, property left in any Company vehicle or brought on the Company's premises. You and any Authorized Operators agree to indemnify and hold the Company harmless from any claim against the Company for loss of or damage to personal property that is connected with any rental under this agreement.

11. LIABILITY PROTECTION

(a) Within the limits stated in this subparagraph, the Company will indemnify, hold harmless, and defend You and any other Authorized Operators from and against liability to third parties for bodily injury (including death) and property damage, if the accident results from the use of the Car as permitted by this Agreement. The limits of this protection, including owner's liability, are the same as the minimum limits required by the automobile financial responsibility law of the jurisdiction in which the accident occurs. This protection will conform to the basic requirements of any applicable mandatory ""no fault"" law but does not include ""uninsured motorist,"" ""underinsured motorist,"" ""supplementary no fault"" or any other optional coverage.

(b) IF YOU DO NOT PURCHASE LIABILITY INSURANCE SUPPLEMENT (LIS) AT THE COMMENCEMENT OF THE RENTAL AND AN ACCIDENT RESULTS FROM THE USE OF THE CAR, YOUR INSURANCE AND THE INSURANCE OF THE OPERATOR OF THE CAR WILL BE PRIMARY. WHERE PERMITTED BY LAW, THE COMPANY DOES NOT PROVIDE ANY THIRD-PARTY LIABILITY PROTECTION COVERING THIS RENTAL. YOU AGREE THAT YOU AND YOUR INSURANCE COMPANY WILL BE RESPONSIBLE FOR HANDLING, DEFENDING AND PAYING ALL THIRD-PARTY CLAIMS.

FOR RENTALS COMMENCING IN FLORIDA: Florida law requires the Company's liability protection and personal injury protection to be primary unless otherwise stated. Therefore, the Company hereby informs You that the valid and collectible liability insurance and personal injury protection insurance of any authorized rental or leasing driver is primary for the limits of liability and personal injury protection coverage required by Florida Statutes.

(c) YOU AND ALL OPERATORS WILL INDEMNIFY AND HOLD THE COMPANY, ITS AGENTS, EMPLOYEES AND AFFILIATES HARMLESS FROM AND AGAINST ANY AND ALL LOSS, LIABILITY, CLAIM, DEMAND, CAUSE OF ACTION, ATTORNEYS' FEES AND EXPENSE OF ANY KIND ARISING FROM THE USE OR POSSESSION OF THE CAR BY YOU OR ANY OTHER OPERATOR(S), UNLESS SUCH LOSS ARISES OUT OF THE COMPANY'S SOLE NEGLIGENCE.

12. ACCIDENTS, THEFT AND VANDALISM

You must promptly and properly report any accident, theft or vandalism involving the Car to the Company and to the police in the jurisdiction in which such incident takes place. You should obtain details of witnesses and other vehicles involved and their drivers, owners and relevant insurances wherever possible. If You or any Authorized Operator receive any papers relating to such an incident, those papers must be promptly given to the Company. You and any Authorized Operators must cooperate fully with the Company's investigation of such incident and defense of any resulting claim. FAILURE TO COOPERATE FULLY MAY VOID ALL LIABILITY PROTECTION, PAI/PEC, LIS, AND LDW. You and any Authorized Operators authorize the Company to obtain any records or information relating to any incident, consent to the jurisdiction of the courts of the jurisdiction in which the incident occurs and waive any right to object to such jurisdiction.

13. LIMITS ON LIABILITY

The Company will not be liable to You or any Authorized Operators for any indirect, special or consequential damages (including lost profits) arising in any way out of any matter covered by this Agreement.

14. PRIVACY

The Company may collect and use personal data about You in accordance with the Company's Privacy Policy. Pursuant to the Privacy Policy, You have the option to limit use or sharing by the Company of personal data about You for marketing purposes and You may access and correct data about You. The Privacy Policy explains these options and provides information about how to choose an option.

15. WAIVER OF CHANGE OF TERMS/GOVERNING LAW

(a) No term of this Agreement may be waived or changed except by a writing signed by an expressly authorized representative of the Company. Rental representatives are not authorized to waive or change any term of this Agreement.

(b) This Agreement is governed by the substantive law of the jurisdiction in which the rental commences, without giving effect to the choice of law rules thereof, and You irrevocably and unconditionally consent and submit to the nonexclusive jurisdiction of the courts located in that jurisdiction.

(c) If any provision of this Agreement conflicts with any applicable law or regulation in any jurisdiction, then that provision shall be deemed to be modified as to the jurisdiction to be consistent with such law or regulation, or to be deleted if modification is impossible, and shall not affect the remainder of this Agreement, which shall continue in full force and effect.

16. PAYMENTS TO INTERMEDIARIES

If You arranged for this rental through a travel agent, internet travel site, broker or other intermediary acting on Your behalf, the Company or an affiliate of the Company's licensor may have paid commissions or other payments to that party to compensate it for arranging such rentals. That compensation may be based in part on the overall volume of business that party books with the Company or its affiliates and licensees. For details on such compensation, You should contact that party.

17. MIAMI-DADE COUNTY WAIVER

Unless waived, a renter in Miami-Dade County must be furnished a county approved visitor information map. These maps are generally furnished at all Company locations in Miami-Dade County. Each renter must either acknowledge receipt of the map at the commencement of each rental or waive his or her right to receive the map. By renting a Car under this Agreement, You waive Your right to receive such a map.

18. RECOVERY OF COSTS

Except if prohibited by applicable law or arbitration rule, in any arbitration or other legal proceeding between You and us, the prevailing party shall be entitled to receive from the other party the prevailing party's costs and expenses incurred in such arbitration or legal proceeding, including reasonable attorneys' fees, arbitration or court costs, and arbitrator's fees.";
    }

    private static string GetFullTermsTextSpanish()
    {
        return @"3. COMUNICACIONES ELECTRÓNICAS Y TELEMÁTICA

[Spanish terms content - similar comprehensive structure as English but in Spanish]
...

18. RECUPERACIÓN DE COSTOS

[Rest of Spanish terms...]";
    }
}