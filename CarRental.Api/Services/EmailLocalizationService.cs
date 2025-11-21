using CarRental.Api.Models;
using System.Collections.Generic;

namespace CarRental.Api.Services;

/// <summary>
/// Service for email content localization
/// Contains all translated strings for email templates
/// </summary>
public class EmailLocalizationService
{
    private readonly Dictionary<EmailLanguage, Dictionary<string, string>> _translations;

    public EmailLocalizationService()
    {
        _translations = InitializeTranslations();
    }

    public string Get(string key, EmailLanguage language)
    {
        if (_translations.TryGetValue(language, out var languageDict))
        {
            if (languageDict.TryGetValue(key, out var translation))
            {
                return translation;
            }
        }

        // Fallback to English
        if (_translations.TryGetValue(EmailLanguage.English, out var englishDict))
        {
            if (englishDict.TryGetValue(key, out var englishTranslation))
            {
                return englishTranslation;
            }
        }

        return key; // Return key if no translation found
    }

    private Dictionary<EmailLanguage, Dictionary<string, string>> InitializeTranslations()
    {
        return new Dictionary<EmailLanguage, Dictionary<string, string>>
        {
            // ENGLISH
            {
                EmailLanguage.English,
                new Dictionary<string, string>
                {
                    // Common
                    { "dear", "Dear" },
                    { "thank_you", "Thank you for choosing" },
                    { "if_questions", "If you have any questions, please don't hesitate to contact us." },
                    { "automated_message", "This is an automated message, please do not reply to this email." },
                    { "all_rights_reserved", "All rights reserved." },
                    
                    // Booking Confirmation
                    { "booking_confirmation", "Booking Confirmation" },
                    { "booking_confirmed", "Your booking has been confirmed." },
                    { "booking_details", "Booking Details" },
                    { "booking_id", "Booking ID" },
                    { "vehicle", "Vehicle" },
                    { "pickup_date", "Pickup Date" },
                    { "return_date", "Return Date" },
                    { "duration", "Duration" },
                    { "days", "days" },
                    { "whats_next", "What's Next?" },
                    { "pickup_reminder_24h", "You will receive a pickup reminder 24 hours before your pickup time" },
                    { "bring_license", "Please bring a valid driver's license and credit card" },
                    { "arrive_early", "Arrive 15 minutes early to complete paperwork" },
                    { "look_forward", "We look forward to serving you!" },
                    
                    // Pickup Reminder
                    { "pickup_reminder", "Pickup Reminder" },
                    { "pickup_tomorrow", "This is a friendly reminder that your vehicle pickup is scheduled for tomorrow!" },
                    { "pickup_information", "Pickup Information" },
                    { "pickup_location", "Pickup Location" },
                    { "important_reminders", "Important Reminders" },
                    { "bring_drivers_license", "Bring a valid driver's license" },
                    { "bring_credit_card", "Bring the credit card used for booking" },
                    { "arrive_15_min", "Arrive 15 minutes early" },
                    { "review_vehicle", "Review the vehicle condition before leaving" },
                    { "see_you_tomorrow", "See you tomorrow!" },
                    
                    // Return Reminder
                    { "return_reminder", "Return Reminder" },
                    { "return_tomorrow", "This is a friendly reminder that your vehicle return is scheduled for tomorrow." },
                    { "return_information", "Return Information" },
                    { "return_location", "Return Location" },
                    { "before_returning", "Before Returning" },
                    { "refill_gas", "Refill the gas tank to the level it was at pickup" },
                    { "remove_belongings", "Remove all personal belongings" },
                    { "clean_interior", "Clean the interior if needed" },
                    { "avoid_late_fees", "Return the vehicle on time to avoid late fees" },
                    
                    // Payment Confirmation
                    { "payment_confirmation", "Payment Confirmation" },
                    { "payment_received", "We have successfully received your payment. Thank you!" },
                    { "payment_details", "Payment Details" },
                    { "amount_paid", "Amount Paid" },
                    { "payment_method", "Payment Method" },
                    { "payment_date", "Payment Date" },
                    { "invoice_sent", "A detailed invoice has been sent to you separately." },
                    { "payment_questions", "If you have any questions about this payment, please contact us." },
                    
                    // Invoice
                    { "invoice", "Invoice" },
                    { "invoice_attached", "Please find your invoice attached to this email." },
                    { "invoice_summary", "Invoice Summary" },
                    { "invoice_id", "Invoice ID" },
                    { "invoice_date", "Invoice Date" },
                    { "total_amount", "Total Amount" },
                    { "invoice_pdf_attached", "The invoice PDF is attached to this email for your records." },
                    { "thank_you_business", "Thank you for your business!" },
                    
                    // Overdue Return
                    { "overdue_return", "URGENT: Overdue Vehicle Return" },
                    { "vehicle_not_returned", "Our records indicate that your rental vehicle has not been returned as scheduled." },
                    { "overdue_details", "Overdue Details" },
                    { "expected_return_date", "Expected Return Date" },
                    { "days_overdue", "Days Overdue" },
                    { "late_fee", "Late Fee" },
                    { "immediate_action", "IMMEDIATE ACTION REQUIRED" },
                    { "return_immediately", "Please return the vehicle immediately" },
                    { "contact_if_issues", "Contact us if there are any issues" },
                    { "fees_continue", "Additional late fees will continue to accrue daily" },
                    { "vehicle_stolen_note", "Failure to return the vehicle may result in reporting the vehicle as stolen to local authorities." },
                    { "understand_circumstances", "We understand that unexpected circumstances can occur. Please contact us immediately so we can assist you." },
                    
                    // Password Reset
                    { "password_reset", "Password Reset Request" },
                    { "password_reset_requested", "We received a request to reset your password." },
                    { "reset_instructions", "Reset Instructions" },
                    { "click_button_reset", "Click the button below to reset your password:" },
                    { "reset_password", "Reset Password" },
                    { "link_expires", "This link will expire in 24 hours." },
                    { "not_requested", "If you didn't request this password reset, please ignore this email and your password will remain unchanged." },
                    { "security_tip", "For security reasons, never share your password with anyone." },
                    
                    // Email Verification
                    { "email_verification", "Email Verification" },
                    { "verify_email", "Please verify your email address to complete your registration." },
                    { "verification_instructions", "Verification Instructions" },
                    { "click_button_verify", "Click the button below to verify your email address:" },
                    { "verify_email_button", "Verify Email Address" },
                    { "verification_expires", "This verification link will expire in 48 hours." },
                    { "already_verified", "If you've already verified your email, you can ignore this message." },
                    { "welcome_aboard", "Welcome aboard! We're excited to have you with us." },
                    { "booking_invitation", "Booking Confirmation & Account Access" },
                    { "booking_paid_invitation", "Your booking has been paid and confirmed. Your account has been created with the details below." },
                    { "your_account", "Your Account Information" },
                    { "temporary_password", "Temporary Password" },
                    { "change_password_after_login", "Please change your password after logging in for security." },
                    { "click_button_login", "Click the button below to log in to your account:" },
                    { "login_button", "Log In" },
                    { "email", "Email" }
                }
            },

            // SPANISH
            {
                EmailLanguage.Spanish,
                new Dictionary<string, string>
                {
                    // Common
                    { "dear", "Estimado/a" },
                    { "thank_you", "Gracias por elegir" },
                    { "if_questions", "Si tiene alguna pregunta, no dude en contactarnos." },
                    { "automated_message", "Este es un mensaje automático, por favor no responda a este correo." },
                    { "all_rights_reserved", "Todos los derechos reservados." },
                    
                    // Booking Confirmation
                    { "booking_confirmation", "Confirmación de Reserva" },
                    { "booking_confirmed", "Su reserva ha sido confirmada." },
                    { "booking_details", "Detalles de la Reserva" },
                    { "booking_id", "ID de Reserva" },
                    { "vehicle", "Vehículo" },
                    { "pickup_date", "Fecha de Recogida" },
                    { "return_date", "Fecha de Devolución" },
                    { "duration", "Duración" },
                    { "days", "días" },
                    { "whats_next", "¿Qué Sigue?" },
                    { "pickup_reminder_24h", "Recibirá un recordatorio 24 horas antes de su hora de recogida" },
                    { "bring_license", "Por favor traiga una licencia de conducir válida y tarjeta de crédito" },
                    { "arrive_early", "Llegue 15 minutos antes para completar el papeleo" },
                    { "look_forward", "¡Esperamos servirle!" },
                    
                    // Pickup Reminder
                    { "pickup_reminder", "Recordatorio de Recogida" },
                    { "pickup_tomorrow", "¡Este es un recordatorio amistoso de que su recogida de vehículo está programada para mañana!" },
                    { "pickup_information", "Información de Recogida" },
                    { "pickup_location", "Ubicación de Recogida" },
                    { "important_reminders", "Recordatorios Importantes" },
                    { "bring_drivers_license", "Traiga una licencia de conducir válida" },
                    { "bring_credit_card", "Traiga la tarjeta de crédito usada para la reserva" },
                    { "arrive_15_min", "Llegue 15 minutos antes" },
                    { "review_vehicle", "Revise la condición del vehículo antes de salir" },
                    { "see_you_tomorrow", "¡Nos vemos mañana!" },
                    
                    // Return Reminder
                    { "return_reminder", "Recordatorio de Devolución" },
                    { "return_tomorrow", "Este es un recordatorio amistoso de que la devolución de su vehículo está programada para mañana." },
                    { "return_information", "Información de Devolución" },
                    { "return_location", "Ubicación de Devolución" },
                    { "before_returning", "Antes de Devolver" },
                    { "refill_gas", "Rellene el tanque de gasolina al nivel que estaba al recogerlo" },
                    { "remove_belongings", "Retire todas sus pertenencias personales" },
                    { "clean_interior", "Limpie el interior si es necesario" },
                    { "avoid_late_fees", "Devuelva el vehículo a tiempo para evitar cargos por retraso" },
                    
                    // Payment Confirmation
                    { "payment_confirmation", "Confirmación de Pago" },
                    { "payment_received", "Hemos recibido su pago exitosamente. ¡Gracias!" },
                    { "payment_details", "Detalles del Pago" },
                    { "amount_paid", "Monto Pagado" },
                    { "payment_method", "Método de Pago" },
                    { "payment_date", "Fecha de Pago" },
                    { "invoice_sent", "Se le ha enviado una factura detallada por separado." },
                    { "payment_questions", "Si tiene preguntas sobre este pago, por favor contáctenos." },
                    
                    // Invoice
                    { "invoice", "Factura" },
                    { "invoice_attached", "Encuentre su factura adjunta a este correo." },
                    { "invoice_summary", "Resumen de Factura" },
                    { "invoice_id", "ID de Factura" },
                    { "invoice_date", "Fecha de Factura" },
                    { "total_amount", "Monto Total" },
                    { "invoice_pdf_attached", "El PDF de la factura está adjunto a este correo para sus registros." },
                    { "thank_you_business", "¡Gracias por su negocio!" },
                    
                    // Overdue Return
                    { "overdue_return", "URGENTE: Devolución de Vehículo Atrasada" },
                    { "vehicle_not_returned", "Nuestros registros indican que su vehículo de alquiler no ha sido devuelto según lo programado." },
                    { "overdue_details", "Detalles del Retraso" },
                    { "expected_return_date", "Fecha de Devolución Esperada" },
                    { "days_overdue", "Días de Retraso" },
                    { "late_fee", "Cargo por Retraso" },
                    { "immediate_action", "SE REQUIERE ACCIÓN INMEDIATA" },
                    { "return_immediately", "Por favor devuelva el vehículo inmediatamente" },
                    { "contact_if_issues", "Contáctenos si hay algún problema" },
                    { "fees_continue", "Los cargos por retraso continuarán acumulándose diariamente" },
                    { "vehicle_stolen_note", "No devolver el vehículo puede resultar en reportarlo como robado a las autoridades locales." },
                    { "understand_circumstances", "Entendemos que pueden ocurrir circunstancias inesperadas. Por favor contáctenos inmediatamente para que podamos ayudarle." },
                    
                    // Password Reset
                    { "password_reset", "Solicitud de Restablecimiento de Contraseña" },
                    { "password_reset_requested", "Recibimos una solicitud para restablecer su contraseña." },
                    { "reset_instructions", "Instrucciones de Restablecimiento" },
                    { "click_button_reset", "Haga clic en el botón de abajo para restablecer su contraseña:" },
                    { "reset_password", "Restablecer Contraseña" },
                    { "link_expires", "Este enlace expirará en 24 horas." },
                    { "not_requested", "Si no solicitó este restablecimiento de contraseña, ignore este correo y su contraseña permanecerá sin cambios." },
                    { "security_tip", "Por razones de seguridad, nunca comparta su contraseña con nadie." },
                    
                    // Email Verification
                    { "email_verification", "Verificación de Correo Electrónico" },
                    { "verify_email", "Por favor verifique su dirección de correo electrónico para completar su registro." },
                    { "verification_instructions", "Instrucciones de Verificación" },
                    { "click_button_verify", "Haga clic en el botón de abajo para verificar su dirección de correo:" },
                    { "verify_email_button", "Verificar Correo Electrónico" },
                    { "verification_expires", "Este enlace de verificación expirará en 48 horas." },
                    { "already_verified", "Si ya verificó su correo, puede ignorar este mensaje." },
                    { "welcome_aboard", "¡Bienvenido a bordo! Estamos emocionados de tenerlo con nosotros." },
                    { "booking_invitation", "Confirmación de Reserva y Acceso a la Cuenta" },
                    { "booking_paid_invitation", "Su reserva ha sido pagada y confirmada. Su cuenta ha sido creada con los detalles a continuación." },
                    { "your_account", "Información de Su Cuenta" },
                    { "temporary_password", "Contraseña Temporal" },
                    { "change_password_after_login", "Por favor cambie su contraseña después de iniciar sesión por seguridad." },
                    { "click_button_login", "Haga clic en el botón de abajo para iniciar sesión en su cuenta:" },
                    { "login_button", "Iniciar Sesión" },
                    { "email", "Correo Electrónico" }
                }
            },

            // PORTUGUESE
            {
                EmailLanguage.Portuguese,
                new Dictionary<string, string>
                {
                    // Common
                    { "dear", "Prezado/a" },
                    { "thank_you", "Obrigado por escolher" },
                    { "if_questions", "Se tiver alguma dúvida, não hesite em nos contactar." },
                    { "automated_message", "Esta é uma mensagem automática, por favor não responda a este e-mail." },
                    { "all_rights_reserved", "Todos os direitos reservados." },
                    
                    // Booking Confirmation
                    { "booking_confirmation", "Confirmação de Reserva" },
                    { "booking_confirmed", "Sua reserva foi confirmada." },
                    { "booking_details", "Detalhes da Reserva" },
                    { "booking_id", "ID da Reserva" },
                    { "vehicle", "Veículo" },
                    { "pickup_date", "Data de Retirada" },
                    { "return_date", "Data de Devolução" },
                    { "duration", "Duração" },
                    { "days", "dias" },
                    { "whats_next", "O Que Vem a Seguir?" },
                    { "pickup_reminder_24h", "Você receberá um lembrete 24 horas antes da sua hora de retirada" },
                    { "bring_license", "Por favor traga uma carteira de motorista válida e cartão de crédito" },
                    { "arrive_early", "Chegue 15 minutos mais cedo para completar a documentação" },
                    { "look_forward", "Estamos ansiosos para atendê-lo!" },
                    
                    // Pickup Reminder
                    { "pickup_reminder", "Lembrete de Retirada" },
                    { "pickup_tomorrow", "Este é um lembrete amigável de que a retirada do seu veículo está agendada para amanhã!" },
                    { "pickup_information", "Informações de Retirada" },
                    { "pickup_location", "Local de Retirada" },
                    { "important_reminders", "Lembretes Importantes" },
                    { "bring_drivers_license", "Traga uma carteira de motorista válida" },
                    { "bring_credit_card", "Traga o cartão de crédito usado na reserva" },
                    { "arrive_15_min", "Chegue 15 minutos mais cedo" },
                    { "review_vehicle", "Revise a condição do veículo antes de sair" },
                    { "see_you_tomorrow", "Até amanhã!" },
                    
                    // Return Reminder
                    { "return_reminder", "Lembrete de Devolução" },
                    { "return_tomorrow", "Este é um lembrete amigável de que a devolução do seu veículo está agendada para amanhã." },
                    { "return_information", "Informações de Devolução" },
                    { "return_location", "Local de Devolução" },
                    { "before_returning", "Antes de Devolver" },
                    { "refill_gas", "Reabasteça o tanque ao nível que estava na retirada" },
                    { "remove_belongings", "Remova todos os seus pertences pessoais" },
                    { "clean_interior", "Limpe o interior se necessário" },
                    { "avoid_late_fees", "Devolva o veículo no prazo para evitar taxas de atraso" },
                    
                    // Payment Confirmation
                    { "payment_confirmation", "Confirmação de Pagamento" },
                    { "payment_received", "Recebemos seu pagamento com sucesso. Obrigado!" },
                    { "payment_details", "Detalhes do Pagamento" },
                    { "amount_paid", "Valor Pago" },
                    { "payment_method", "Método de Pagamento" },
                    { "payment_date", "Data do Pagamento" },
                    { "invoice_sent", "Uma fatura detalhada foi enviada separadamente." },
                    { "payment_questions", "Se tiver dúvidas sobre este pagamento, por favor nos contacte." },
                    
                    // Invoice
                    { "invoice", "Fatura" },
                    { "invoice_attached", "Por favor encontre sua fatura anexada a este e-mail." },
                    { "invoice_summary", "Resumo da Fatura" },
                    { "invoice_id", "ID da Fatura" },
                    { "invoice_date", "Data da Fatura" },
                    { "total_amount", "Valor Total" },
                    { "invoice_pdf_attached", "O PDF da fatura está anexado a este e-mail para seus registros." },
                    { "thank_you_business", "Obrigado pelo seu negócio!" },
                    
                    // Overdue Return
                    { "overdue_return", "URGENTE: Devolução de Veículo Atrasada" },
                    { "vehicle_not_returned", "Nossos registros indicam que seu veículo alugado não foi devolvido conforme agendado." },
                    { "overdue_details", "Detalhes do Atraso" },
                    { "expected_return_date", "Data de Devolução Esperada" },
                    { "days_overdue", "Dias de Atraso" },
                    { "late_fee", "Taxa de Atraso" },
                    { "immediate_action", "AÇÃO IMEDIATA NECESSÁRIA" },
                    { "return_immediately", "Por favor devolva o veículo imediatamente" },
                    { "contact_if_issues", "Contacte-nos se houver algum problema" },
                    { "fees_continue", "Taxas de atraso adicionais continuarão a acumular diariamente" },
                    { "vehicle_stolen_note", "Não devolver o veículo pode resultar em reportá-lo como roubado às autoridades locais." },
                    { "understand_circumstances", "Entendemos que circunstâncias inesperadas podem ocorrer. Por favor contacte-nos imediatamente para que possamos ajudá-lo." },
                    
                    // Password Reset
                    { "password_reset", "Solicitação de Redefinição de Senha" },
                    { "password_reset_requested", "Recebemos uma solicitação para redefinir sua senha." },
                    { "reset_instructions", "Instruções de Redefinição" },
                    { "click_button_reset", "Clique no botão abaixo para redefinir sua senha:" },
                    { "reset_password", "Redefinir Senha" },
                    { "link_expires", "Este link expirará em 24 horas." },
                    { "not_requested", "Se você não solicitou esta redefinição de senha, ignore este e-mail e sua senha permanecerá inalterada." },
                    { "security_tip", "Por razões de segurança, nunca compartilhe sua senha com ninguém." },
                    
                    // Email Verification
                    { "email_verification", "Verificação de E-mail" },
                    { "verify_email", "Por favor verifique seu endereço de e-mail para completar seu registro." },
                    { "verification_instructions", "Instruções de Verificação" },
                    { "click_button_verify", "Clique no botão abaixo para verificar seu endereço de e-mail:" },
                    { "verify_email_button", "Verificar Endereço de E-mail" },
                    { "verification_expires", "Este link de verificação expirará em 48 horas." },
                    { "already_verified", "Se você já verificou seu e-mail, pode ignorar esta mensagem." },
                    { "welcome_aboard", "Bem-vindo a bordo! Estamos empolgados em tê-lo conosco." }
                }
            },

            // FRENCH
            {
                EmailLanguage.French,
                new Dictionary<string, string>
                {
                    // Common
                    { "dear", "Cher/Chère" },
                    { "thank_you", "Merci d'avoir choisi" },
                    { "if_questions", "Si vous avez des questions, n'hésitez pas à nous contacter." },
                    { "automated_message", "Ceci est un message automatique, veuillez ne pas répondre à cet e-mail." },
                    { "all_rights_reserved", "Tous droits réservés." },
                    
                    // Booking Confirmation
                    { "booking_confirmation", "Confirmation de Réservation" },
                    { "booking_confirmed", "Votre réservation a été confirmée." },
                    { "booking_details", "Détails de la Réservation" },
                    { "booking_id", "ID de Réservation" },
                    { "vehicle", "Véhicule" },
                    { "pickup_date", "Date de Prise en Charge" },
                    { "return_date", "Date de Retour" },
                    { "duration", "Durée" },
                    { "days", "jours" },
                    { "whats_next", "Quelle est la Suite?" },
                    { "pickup_reminder_24h", "Vous recevrez un rappel 24 heures avant votre heure de prise en charge" },
                    { "bring_license", "Veuillez apporter un permis de conduire valide et une carte de crédit" },
                    { "arrive_early", "Arrivez 15 minutes à l'avance pour compléter les formalités" },
                    { "look_forward", "Nous avons hâte de vous servir!" },
                    
                    // Pickup Reminder
                    { "pickup_reminder", "Rappel de Prise en Charge" },
                    { "pickup_tomorrow", "Ceci est un rappel amical que votre prise en charge de véhicule est prévue pour demain!" },
                    { "pickup_information", "Informations de Prise en Charge" },
                    { "pickup_location", "Lieu de Prise en Charge" },
                    { "important_reminders", "Rappels Importants" },
                    { "bring_drivers_license", "Apportez un permis de conduire valide" },
                    { "bring_credit_card", "Apportez la carte de crédit utilisée pour la réservation" },
                    { "arrive_15_min", "Arrivez 15 minutes à l'avance" },
                    { "review_vehicle", "Vérifiez l'état du véhicule avant de partir" },
                    { "see_you_tomorrow", "À demain!" },
                    
                    // Return Reminder
                    { "return_reminder", "Rappel de Retour" },
                    { "return_tomorrow", "Ceci est un rappel amical que le retour de votre véhicule est prévu pour demain." },
                    { "return_information", "Informations de Retour" },
                    { "return_location", "Lieu de Retour" },
                    { "before_returning", "Avant le Retour" },
                    { "refill_gas", "Faites le plein au niveau qu'il était lors de la prise en charge" },
                    { "remove_belongings", "Retirez tous vos effets personnels" },
                    { "clean_interior", "Nettoyez l'intérieur si nécessaire" },
                    { "avoid_late_fees", "Retournez le véhicule à temps pour éviter les frais de retard" },
                    
                    // Payment Confirmation
                    { "payment_confirmation", "Confirmation de Paiement" },
                    { "payment_received", "Nous avons reçu votre paiement avec succès. Merci!" },
                    { "payment_details", "Détails du Paiement" },
                    { "amount_paid", "Montant Payé" },
                    { "payment_method", "Méthode de Paiement" },
                    { "payment_date", "Date du Paiement" },
                    { "invoice_sent", "Une facture détaillée vous a été envoyée séparément." },
                    { "payment_questions", "Si vous avez des questions sur ce paiement, veuillez nous contacter." },
                    
                    // Invoice
                    { "invoice", "Facture" },
                    { "invoice_attached", "Veuillez trouver votre facture jointe à cet e-mail." },
                    { "invoice_summary", "Résumé de la Facture" },
                    { "invoice_id", "ID de Facture" },
                    { "invoice_date", "Date de Facture" },
                    { "total_amount", "Montant Total" },
                    { "invoice_pdf_attached", "Le PDF de la facture est joint à cet e-mail pour vos archives." },
                    { "thank_you_business", "Merci pour votre confiance!" },
                    
                    // Overdue Return
                    { "overdue_return", "URGENT: Retour de Véhicule en Retard" },
                    { "vehicle_not_returned", "Nos registres indiquent que votre véhicule de location n'a pas été retourné comme prévu." },
                    { "overdue_details", "Détails du Retard" },
                    { "expected_return_date", "Date de Retour Prévue" },
                    { "days_overdue", "Jours de Retard" },
                    { "late_fee", "Frais de Retard" },
                    { "immediate_action", "ACTION IMMÉDIATE REQUISE" },
                    { "return_immediately", "Veuillez retourner le véhicule immédiatement" },
                    { "contact_if_issues", "Contactez-nous s'il y a des problèmes" },
                    { "fees_continue", "Des frais de retard supplémentaires continueront de s'accumuler quotidiennement" },
                    { "vehicle_stolen_note", "Ne pas retourner le véhicule peut entraîner sa déclaration comme volé aux autorités locales." },
                    { "understand_circumstances", "Nous comprenons que des circonstances imprévues peuvent survenir. Veuillez nous contacter immédiatement afin que nous puissions vous aider." },
                    
                    // Password Reset
                    { "password_reset", "Demande de Réinitialisation du Mot de Passe" },
                    { "password_reset_requested", "Nous avons reçu une demande de réinitialisation de votre mot de passe." },
                    { "reset_instructions", "Instructions de Réinitialisation" },
                    { "click_button_reset", "Cliquez sur le bouton ci-dessous pour réinitialiser votre mot de passe:" },
                    { "reset_password", "Réinitialiser le Mot de Passe" },
                    { "link_expires", "Ce lien expirera dans 24 heures." },
                    { "not_requested", "Si vous n'avez pas demandé cette réinitialisation de mot de passe, ignorez cet e-mail et votre mot de passe restera inchangé." },
                    { "security_tip", "Pour des raisons de sécurité, ne partagez jamais votre mot de passe avec qui que ce soit." },
                    
                    // Email Verification
                    { "email_verification", "Vérification d'E-mail" },
                    { "verify_email", "Veuillez vérifier votre adresse e-mail pour compléter votre inscription." },
                    { "verification_instructions", "Instructions de Vérification" },
                    { "click_button_verify", "Cliquez sur le bouton ci-dessous pour vérifier votre adresse e-mail:" },
                    { "verify_email_button", "Vérifier l'Adresse E-mail" },
                    { "verification_expires", "Ce lien de vérification expirera dans 48 heures." },
                    { "already_verified", "Si vous avez déjà vérifié votre e-mail, vous pouvez ignorer ce message." },
                    { "welcome_aboard", "Bienvenue à bord! Nous sommes ravis de vous avoir avec nous." },
                    { "booking_invitation", "Confirmation de Réservation et Accès au Compte" },
                    { "booking_paid_invitation", "Votre réservation a été payée et confirmée. Votre compte a été créé avec les détails ci-dessous." },
                    { "your_account", "Informations de Votre Compte" },
                    { "temporary_password", "Mot de Passe Temporaire" },
                    { "change_password_after_login", "Veuillez changer votre mot de passe après la connexion pour des raisons de sécurité." },
                    { "click_button_login", "Cliquez sur le bouton ci-dessous pour vous connecter à votre compte:" },
                    { "login_button", "Se Connecter" },
                    { "email", "E-mail" }
                }
            },

            // GERMAN
            {
                EmailLanguage.German,
                new Dictionary<string, string>
                {
                    // Common
                    { "dear", "Sehr geehrte/r" },
                    { "thank_you", "Vielen Dank, dass Sie sich für" },
                    { "if_questions", "Wenn Sie Fragen haben, zögern Sie bitte nicht, uns zu kontaktieren." },
                    { "automated_message", "Dies ist eine automatische Nachricht, bitte antworten Sie nicht auf diese E-Mail." },
                    { "all_rights_reserved", "Alle Rechte vorbehalten." },
                    
                    // Booking Confirmation
                    { "booking_confirmation", "Buchungsbestätigung" },
                    { "booking_confirmed", "Ihre Buchung wurde bestätigt." },
                    { "booking_details", "Buchungsdetails" },
                    { "booking_id", "Buchungs-ID" },
                    { "vehicle", "Fahrzeug" },
                    { "pickup_date", "Abholungsdatum" },
                    { "return_date", "Rückgabedatum" },
                    { "duration", "Dauer" },
                    { "days", "Tage" },
                    { "whats_next", "Wie geht es weiter?" },
                    { "pickup_reminder_24h", "Sie erhalten 24 Stunden vor Ihrer Abholzeit eine Erinnerung" },
                    { "bring_license", "Bitte bringen Sie einen gültigen Führerschein und eine Kreditkarte mit" },
                    { "arrive_early", "Kommen Sie 15 Minuten früher, um die Formalitäten zu erledigen" },
                    { "look_forward", "Wir freuen uns darauf, Sie zu bedienen!" },
                    
                    // Pickup Reminder
                    { "pickup_reminder", "Abholungserinnerung" },
                    { "pickup_tomorrow", "Dies ist eine freundliche Erinnerung, dass Ihre Fahrzeugabholung für morgen geplant ist!" },
                    { "pickup_information", "Abholungsinformationen" },
                    { "pickup_location", "Abholungsort" },
                    { "important_reminders", "Wichtige Erinnerungen" },
                    { "bring_drivers_license", "Bringen Sie einen gültigen Führerschein mit" },
                    { "bring_credit_card", "Bringen Sie die für die Buchung verwendete Kreditkarte mit" },
                    { "arrive_15_min", "Kommen Sie 15 Minuten früher" },
                    { "review_vehicle", "Überprüfen Sie den Fahrzeugzustand vor der Abfahrt" },
                    { "see_you_tomorrow", "Bis morgen!" },
                    
                    // Return Reminder
                    { "return_reminder", "Rückgabeerinnerung" },
                    { "return_tomorrow", "Dies ist eine freundliche Erinnerung, dass Ihre Fahrzeugrückgabe für morgen geplant ist." },
                    { "return_information", "Rückgabeinformationen" },
                    { "return_location", "Rückgabeort" },
                    { "before_returning", "Vor der Rückgabe" },
                    { "refill_gas", "Tanken Sie auf das Niveau bei der Abholung" },
                    { "remove_belongings", "Entfernen Sie alle persönlichen Gegenstände" },
                    { "clean_interior", "Reinigen Sie den Innenraum bei Bedarf" },
                    { "avoid_late_fees", "Geben Sie das Fahrzeug pünktlich zurück, um Verspätungsgebühren zu vermeiden" },
                    
                    // Payment Confirmation
                    { "payment_confirmation", "Zahlungsbestätigung" },
                    { "payment_received", "Wir haben Ihre Zahlung erfolgreich erhalten. Vielen Dank!" },
                    { "payment_details", "Zahlungsdetails" },
                    { "amount_paid", "Gezahlter Betrag" },
                    { "payment_method", "Zahlungsmethode" },
                    { "payment_date", "Zahlungsdatum" },
                    { "invoice_sent", "Eine detaillierte Rechnung wurde Ihnen separat zugesandt." },
                    { "payment_questions", "Wenn Sie Fragen zu dieser Zahlung haben, kontaktieren Sie uns bitte." },
                    
                    // Invoice
                    { "invoice", "Rechnung" },
                    { "invoice_attached", "Bitte finden Sie Ihre Rechnung im Anhang dieser E-Mail." },
                    { "invoice_summary", "Rechnungszusammenfassung" },
                    { "invoice_id", "Rechnungs-ID" },
                    { "invoice_date", "Rechnungsdatum" },
                    { "total_amount", "Gesamtbetrag" },
                    { "invoice_pdf_attached", "Das Rechnungs-PDF ist dieser E-Mail für Ihre Unterlagen beigefügt." },
                    { "thank_you_business", "Vielen Dank für Ihr Vertrauen!" },
                    
                    // Overdue Return
                    { "overdue_return", "DRINGEND: Überfällige Fahrzeugrückgabe" },
                    { "vehicle_not_returned", "Unsere Aufzeichnungen zeigen, dass Ihr Mietfahrzeug nicht wie geplant zurückgegeben wurde." },
                    { "overdue_details", "Details zur Verspätung" },
                    { "expected_return_date", "Erwartetes Rückgabedatum" },
                    { "days_overdue", "Tage überfällig" },
                    { "late_fee", "Verspätungsgebühr" },
                    { "immediate_action", "SOFORTIGES HANDELN ERFORDERLICH" },
                    { "return_immediately", "Bitte geben Sie das Fahrzeug sofort zurück" },
                    { "contact_if_issues", "Kontaktieren Sie uns bei Problemen" },
                    { "fees_continue", "Zusätzliche Verspätungsgebühren werden weiterhin täglich anfallen" },
                    { "vehicle_stolen_note", "Die Nichtrückgabe des Fahrzeugs kann dazu führen, dass es bei den örtlichen Behörden als gestohlen gemeldet wird." },
                    { "understand_circumstances", "Wir verstehen, dass unerwartete Umstände auftreten können. Bitte kontaktieren Sie uns sofort, damit wir Ihnen helfen können." },
                    
                    // Password Reset
                    { "password_reset", "Anfrage zur Passwortzurücksetzung" },
                    { "password_reset_requested", "Wir haben eine Anfrage zur Zurücksetzung Ihres Passworts erhalten." },
                    { "reset_instructions", "Anweisungen zur Zurücksetzung" },
                    { "click_button_reset", "Klicken Sie auf die Schaltfläche unten, um Ihr Passwort zurückzusetzen:" },
                    { "reset_password", "Passwort Zurücksetzen" },
                    { "link_expires", "Dieser Link läuft in 24 Stunden ab." },
                    { "not_requested", "Wenn Sie diese Passwortzurücksetzung nicht angefordert haben, ignorieren Sie diese E-Mail und Ihr Passwort bleibt unverändert." },
                    { "security_tip", "Aus Sicherheitsgründen teilen Sie Ihr Passwort niemals mit jemandem." },
                    
                    // Email Verification
                    { "email_verification", "E-Mail-Verifizierung" },
                    { "verify_email", "Bitte verifizieren Sie Ihre E-Mail-Adresse, um Ihre Registrierung abzuschließen." },
                    { "verification_instructions", "Verifizierungsanweisungen" },
                    { "click_button_verify", "Klicken Sie auf die Schaltfläche unten, um Ihre E-Mail-Adresse zu verifizieren:" },
                    { "verify_email_button", "E-Mail-Adresse Verifizieren" },
                    { "verification_expires", "Dieser Verifizierungslink läuft in 48 Stunden ab." },
                    { "already_verified", "Wenn Sie Ihre E-Mail bereits verifiziert haben, können Sie diese Nachricht ignorieren." },
                    { "welcome_aboard", "Willkommen an Bord! Wir freuen uns, Sie bei uns zu haben." },
                    { "booking_invitation", "Buchungsbestätigung & Kontozugang" },
                    { "booking_paid_invitation", "Ihre Buchung wurde bezahlt und bestätigt. Ihr Konto wurde mit den untenstehenden Details erstellt." },
                    { "your_account", "Ihre Kontoinformationen" },
                    { "temporary_password", "Temporäres Passwort" },
                    { "change_password_after_login", "Bitte ändern Sie Ihr Passwort nach der Anmeldung aus Sicherheitsgründen." },
                    { "click_button_login", "Klicken Sie auf die Schaltfläche unten, um sich bei Ihrem Konto anzumelden:" },
                    { "login_button", "Anmelden" },
                    { "email", "E-Mail" }
                }
            }
        };
    }
}
