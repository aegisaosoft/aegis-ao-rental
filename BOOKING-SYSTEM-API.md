# Token-Based Booking System API

This document describes the comprehensive token-based booking system for the Car Rental API, including secure booking links, payment processing, and email notifications.

## Overview

The Token-Based Booking System provides a secure, email-driven booking process where:
1. **Company creates a booking token** with vehicle and pricing information
2. **Customer receives an email** with a secure booking link
3. **Customer completes payment** through the booking link
4. **System creates reservation** and sends confirmation emails
5. **Customer email is automatically added** to the database

## Base URL

```
/api/booking
```

## Authentication

Most endpoints require JWT authentication. Include the Bearer token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

## Workflow

### 1. Company Creates Booking Token
```
POST /api/booking/create-token
```

### 2. Customer Receives Email
- Secure booking link sent to customer email
- Link contains all booking details and pricing
- Token expires after specified time (default 24 hours)

### 3. Customer Completes Payment
```
POST /api/booking/process
```

### 4. System Creates Reservation
- Payment processed through Stripe
- Reservation created in database
- Customer automatically added to database
- Confirmation emails sent

## API Endpoints

### 1. Create Booking Token

**POST** `/api/booking/create-token`

Creates a secure booking token and sends email to customer.

#### Request Body

```json
{
  "companyId": "uuid",
  "customerEmail": "customer@example.com",
  "vehicleId": "uuid",
  "bookingData": {
    "pickupDate": "2025-01-15T10:00:00Z",
    "returnDate": "2025-01-17T10:00:00Z",
    "pickupLocation": "Downtown Branch",
    "returnLocation": "Downtown Branch",
    "dailyRate": 75.00,
    "totalDays": 2,
    "subtotal": 150.00,
    "taxAmount": 12.00,
    "insuranceAmount": 25.00,
    "additionalFees": 0.00,
    "totalAmount": 187.00,
    "notes": "Customer requested GPS"
  },
  "expirationHours": 24
}
```

#### Response

```json
{
  "tokenId": "uuid",
  "companyId": "uuid",
  "customerEmail": "customer@example.com",
  "vehicleId": "uuid",
  "token": "secure-token-string",
  "bookingData": {
    "pickupDate": "2025-01-15T10:00:00Z",
    "returnDate": "2025-01-17T10:00:00Z",
    "pickupLocation": "Downtown Branch",
    "returnLocation": "Downtown Branch",
    "dailyRate": 75.00,
    "totalDays": 2,
    "subtotal": 150.00,
    "taxAmount": 12.00,
    "insuranceAmount": 25.00,
    "additionalFees": 0.00,
    "totalAmount": 187.00,
    "vehicleInfo": {
      "make": "Toyota",
      "model": "Camry",
      "year": 2023,
      "color": "Silver",
      "licensePlate": "ABC123",
      "imageUrl": "https://example.com/camry.jpg",
      "features": ["GPS", "Bluetooth", "Backup Camera"]
    },
    "companyInfo": {
      "name": "Premium Rentals Inc",
      "email": "info@premiumrentals.com",
      "phone": "+1-555-0100",
      "address": "123 Main St",
      "city": "New York",
      "state": "NY",
      "country": "USA"
    },
    "notes": "Customer requested GPS"
  },
  "expiresAt": "2025-01-16T10:00:00Z",
  "isUsed": false,
  "usedAt": null,
  "createdAt": "2025-01-15T10:00:00Z",
  "updatedAt": "2025-01-15T10:00:00Z",
  "companyName": "Premium Rentals Inc",
  "vehicleName": "Toyota Camry (2023)"
}
```

### 2. Get Booking Token Details

**GET** `/api/booking/token/{token}`

Retrieves booking token details for customer to review before payment.

#### Response

Same as Create Booking Token response.

### 3. Process Booking with Payment

**POST** `/api/booking/process`

Processes the booking with payment and creates reservation.

#### Request Body

```json
{
  "token": "secure-token-string",
  "paymentMethodId": "pm_1234567890",
  "customerNotes": "Please have the car ready at 9:30 AM"
}
```

#### Response

```json
{
  "confirmationId": "uuid",
  "bookingTokenId": "uuid",
  "reservationId": "uuid",
  "customerEmail": "customer@example.com",
  "confirmationNumber": "CONF-20250115-ABC12345",
  "bookingDetails": {
    "pickupDate": "2025-01-15T10:00:00Z",
    "returnDate": "2025-01-17T10:00:00Z",
    "pickupLocation": "Downtown Branch",
    "returnLocation": "Downtown Branch",
    "dailyRate": 75.00,
    "totalDays": 2,
    "subtotal": 150.00,
    "taxAmount": 12.00,
    "insuranceAmount": 25.00,
    "additionalFees": 0.00,
    "totalAmount": 187.00,
    "vehicleInfo": {
      "make": "Toyota",
      "model": "Camry",
      "year": 2023,
      "color": "Silver",
      "licensePlate": "ABC123",
      "imageUrl": "https://example.com/camry.jpg",
      "features": ["GPS", "Bluetooth", "Backup Camera"]
    },
    "companyInfo": {
      "name": "Premium Rentals Inc",
      "email": "info@premiumrentals.com",
      "phone": "+1-555-0100",
      "address": "123 Main St",
      "city": "New York",
      "state": "NY",
      "country": "USA"
    },
    "notes": "Customer requested GPS"
  },
  "paymentStatus": "completed",
  "stripePaymentIntentId": "pi_1234567890",
  "confirmationSent": true,
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-01-15T10:30:00Z"
}
```

### 4. Get Booking Confirmation

**GET** `/api/booking/confirmation/{confirmationNumber}`

Retrieves booking confirmation details.

#### Response

Same as Process Booking response.

## Email Templates

### Booking Link Email

The system sends a professional HTML email with:
- **Company branding** and contact information
- **Vehicle details** with image and features
- **Booking summary** with dates, locations, and pricing
- **Secure booking link** with expiration notice
- **Company contact information** for support

### Payment Success Email

Sent immediately after successful payment:
- **Payment confirmation** with amount paid
- **Vehicle information** for reference
- **Next steps** for pickup process
- **Company contact** for any questions

### Booking Confirmation Email

Sent after reservation creation:
- **Confirmation number** for reference
- **Complete booking details** with all information
- **Pickup instructions** and requirements
- **Company contact** and support information

## Database Schema

### Booking Tokens Table

```sql
CREATE TABLE booking_tokens (
    token_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID NOT NULL REFERENCES rental_companies(company_id),
    customer_email VARCHAR(255) NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES vehicles(vehicle_id),
    token VARCHAR(255) UNIQUE NOT NULL,
    booking_data JSONB NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    is_used BOOLEAN DEFAULT false,
    used_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Email Notifications Table

```sql
CREATE TABLE email_notifications (
    notification_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    booking_token_id UUID REFERENCES booking_tokens(token_id),
    customer_email VARCHAR(255) NOT NULL,
    notification_type VARCHAR(50) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(50) DEFAULT 'pending',
    sent_at TIMESTAMP,
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Booking Confirmations Table

```sql
CREATE TABLE booking_confirmations (
    confirmation_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    booking_token_id UUID NOT NULL REFERENCES booking_tokens(token_id),
    reservation_id UUID REFERENCES reservations(reservation_id),
    customer_email VARCHAR(255) NOT NULL,
    confirmation_number VARCHAR(50) UNIQUE NOT NULL,
    booking_details JSONB NOT NULL,
    payment_status VARCHAR(50) NOT NULL,
    stripe_payment_intent_id VARCHAR(255),
    confirmation_sent BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## Security Features

### Token Security
- **Cryptographically secure tokens** using RandomNumberGenerator
- **Time-based expiration** (default 24 hours, configurable)
- **Single-use tokens** (marked as used after processing)
- **Secure token format** (Base64URL encoded)

### Payment Security
- **Stripe integration** for secure payment processing
- **Payment intent confirmation** before reservation creation
- **Automatic customer creation** in Stripe if needed
- **Payment method validation** and security checks

### Email Security
- **Email validation** for customer addresses
- **Secure booking links** with token-based access
- **Email delivery tracking** and error handling
- **Professional email templates** to prevent phishing

## Business Rules

### Token Management
1. **Token Expiration** - Tokens expire after specified hours (default 24)
2. **Single Use** - Tokens can only be used once
3. **Vehicle Availability** - Vehicle must be available when token is created
4. **Company Validation** - Company must be active and verified

### Payment Processing
1. **Stripe Integration** - All payments processed through Stripe
2. **Customer Creation** - Customers automatically created if not exists
3. **Payment Confirmation** - Payment must be confirmed before reservation
4. **Amount Validation** - Payment amount must match booking total

### Email Notifications
1. **Automatic Sending** - Emails sent automatically at each step
2. **Delivery Tracking** - Email delivery status tracked
3. **Error Handling** - Failed emails logged and retried
4. **Professional Templates** - HTML emails with company branding

## Error Handling

### Token Errors
- **Token Not Found** - 404 with clear message
- **Token Expired** - 400 with expiration information
- **Token Already Used** - 400 with usage information
- **Invalid Token Format** - 400 with format requirements

### Payment Errors
- **Payment Failed** - 400 with failure reason
- **Stripe Errors** - 500 with error logging
- **Amount Mismatch** - 400 with amount details
- **Customer Creation Failed** - 500 with retry suggestion

### Email Errors
- **Email Send Failed** - Logged but booking continues
- **Invalid Email Address** - 400 with validation message
- **Template Errors** - 500 with error logging
- **Delivery Failures** - Tracked for retry attempts

## Rate Limiting

- **Token Creation** - 10 requests per minute per company
- **Payment Processing** - 5 requests per minute per customer
- **Email Sending** - 20 requests per minute per company
- **Confirmation Retrieval** - 100 requests per minute

## Examples

### Complete Booking Flow

```bash
# 1. Company creates booking token
curl -X POST "https://api.carrental.com/api/booking/create-token" \
  -H "Authorization: Bearer <company-jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "companyId": "company-uuid",
    "customerEmail": "customer@example.com",
    "vehicleId": "vehicle-uuid",
    "bookingData": {
      "pickupDate": "2025-01-15T10:00:00Z",
      "returnDate": "2025-01-17T10:00:00Z",
      "dailyRate": 75.00,
      "totalAmount": 187.00
    }
  }'

# 2. Customer receives email with booking link
# Email contains: https://api.carrental.com/booking/secure-token-string

# 3. Customer processes booking with payment
curl -X POST "https://api.carrental.com/api/booking/process" \
  -H "Content-Type: application/json" \
  -d '{
    "token": "secure-token-string",
    "paymentMethodId": "pm_1234567890"
  }'

# 4. System creates reservation and sends confirmations
# Customer receives confirmation email with booking details
```

### Get Booking Details

```bash
# Get booking token details
curl -X GET "https://api.carrental.com/api/booking/token/secure-token-string"

# Get booking confirmation
curl -X GET "https://api.carrental.com/api/booking/confirmation/CONF-20250115-ABC12345"
```

## Integration Notes

### Frontend Integration
- **Booking Link Pages** - Create customer-facing booking pages
- **Payment Forms** - Integrate Stripe Elements for payment
- **Email Templates** - Customize email templates for branding
- **Error Handling** - Implement proper error handling and user feedback

### Backend Integration
- **Webhook Handling** - Process Stripe webhooks for payment updates
- **Email Service** - Integrate with email service provider (SendGrid, etc.)
- **Database Monitoring** - Monitor token usage and email delivery
- **Security Auditing** - Log all booking and payment activities

This token-based booking system provides a secure, user-friendly way for companies to send booking links to customers and process payments automatically while maintaining full audit trails and email confirmations.
