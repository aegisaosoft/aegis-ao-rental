# Stripe Terminal Integration

This document explains how to set up and use Stripe Terminal for card-present payments in the rental car application.

## Overview

The Stripe Terminal integration allows companies to accept in-person card payments using physical card readers. This is useful for:
- Security deposits at vehicle pickup
- Final payments upon vehicle return
- Additional charges (damages, extra services, etc.)

## Prerequisites

1. **Stripe Connect Account**: Each rental company must have a Stripe Connect account with Terminal enabled
2. **Stripe Terminal Hardware**: Physical card reader (e.g., BBPOS WisePOS E, Stripe Reader M2)
3. **Stripe API Keys**: Your platform's Stripe secret key (stored in `appsettings.json`)

## Backend Setup

### 1. API Configuration

The backend is already configured with the `TerminalController` which provides these endpoints:

- `POST /api/Terminal/connection-token` - Creates connection token for reader
- `POST /api/Terminal/create-payment-intent` - Creates payment intent
- `POST /api/Terminal/capture-payment-intent` - Captures authorized payment
- `POST /api/Terminal/cancel-payment-intent` - Cancels payment

### 2. Database Requirements

Each company must have a `StripeAccountId` in the `Company` table:

```sql
UPDATE "Company" 
SET "StripeAccountId" = 'acct_xxxxxxxxxxxxx'
WHERE "Id" = 'company-guid';
```

### 3. Stripe Configuration

Add your Stripe secret key to `appsettings.json`:

```json
{
  "Stripe": {
    "SecretKey": "sk_test_xxxxxxxxxxxxx",
    "PublishableKey": "pk_test_xxxxxxxxxxxxx"
  }
}
```

## Frontend Setup

### 1. Install Required Packages

First, install the Stripe Terminal JS package:

```bash
cd aegis-ao-rental_web/client
npm install @stripe/terminal-js
```

The package is already added to `package.json`.

### 2. Two Integration Approaches

You can use Stripe Terminal in two ways:

#### **Approach A: Component-Based (Easier)**

Use the pre-built `StripeTerminal` component for quick integration:

```jsx
import StripeTerminal from '../components/StripeTerminal';

<StripeTerminal
  amount={50000}
  currency="usd"
  bookingId={bookingId}
  onSuccess={(paymentIntent) => console.log('Success:', paymentIntent)}
  onError={(error) => console.error('Error:', error)}
/>
```

#### **Approach B: Hook-Based (More Flexible)**

Use the `useStripeTerminal` hook for custom UI and advanced control:

```jsx
import { useStripeTerminal } from '../hooks/useStripeTerminal';

function CustomPaymentPage() {
  const {
    terminal,
    reader,
    loading,
    error,
    discoveredReaders,
    discoverReaders,
    connectReader,
    collectPayment,
    capturePayment,
    cancelPayment,
    isConnected
  } = useStripeTerminal({
    simulated: false,
    onReaderDisconnect: () => console.log('Reader disconnected')
  });

  const handlePayment = async () => {
    const paymentIntent = await collectPayment(50000, {
      currency: 'usd',
      description: 'Security deposit',
      bookingId: 'booking-id',
      captureMethod: 'manual'
    });
    console.log('Payment authorized:', paymentIntent.id);
  };

  return (
    <div>
      {!isConnected && (
        <button onClick={discoverReaders}>Discover Readers</button>
      )}
      {discoveredReaders.map(r => (
        <button key={r.id} onClick={() => connectReader(r)}>
          Connect to {r.label}
        </button>
      ))}
      {isConnected && (
        <button onClick={handlePayment} disabled={loading}>
          Charge Card
        </button>
      )}
    </div>
  );
}
```

See `StripeTerminalHookExample.js` for a complete working example.

### 3. Component Props (Approach A)

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `amount` | number | Yes | Amount in cents (e.g., 50000 = $500.00) |
| `currency` | string | No | Currency code (default: 'usd') |
| `bookingId` | string | No | Associated booking ID |
| `description` | string | No | Payment description |
| `metadata` | object | No | Custom metadata |
| `onSuccess` | function | No | Called when payment succeeds |
| `onError` | function | No | Called when payment fails |
| `onCancel` | function | No | Called when payment is cancelled |

### 4. Hook API Reference (Approach B)

The `useStripeTerminal` hook returns the following:

#### Options (passed to hook):
```javascript
useStripeTerminal({
  simulated: false,           // Set true for testing without physical reader
  locationId: null,           // Optional: Stripe location ID
  onReaderDisconnect: null,   // Callback when reader disconnects
  onError: null               // Global error callback
})
```

#### Returned Values:

| Property | Type | Description |
|----------|------|-------------|
| `terminal` | object | Stripe Terminal instance |
| `reader` | object | Currently connected reader |
| `loading` | boolean | Loading state for operations |
| `error` | Error | Last error that occurred |
| `discoveredReaders` | array | List of discovered readers |
| `isConnected` | boolean | Whether a reader is connected |
| `isInitialized` | boolean | Whether terminal is initialized |

#### Returned Functions:

| Function | Parameters | Returns | Description |
|----------|-----------|---------|-------------|
| `discoverReaders` | none | `Promise<Reader[]>` | Discover available card readers |
| `connectReader` | `reader: Reader` | `Promise<boolean>` | Connect to a specific reader |
| `disconnectReader` | none | `Promise<boolean>` | Disconnect current reader |
| `collectPayment` | `amount: number, options: object` | `Promise<PaymentIntent>` | Collect payment from card |
| `capturePayment` | `paymentIntentId: string, amount?: number` | `Promise<object>` | Capture authorized payment |
| `cancelPayment` | `paymentIntentId: string` | `Promise<object>` | Cancel/refund payment |
| `clearError` | none | `void` | Clear current error state |

#### Payment Options Object:
```javascript
{
  currency: 'usd',              // Currency code
  description: 'Payment desc',  // Payment description
  bookingId: 'booking-id',      // Associated booking
  metadata: {},                 // Custom metadata
  captureMethod: 'manual'       // 'manual' or 'automatic'
}
```

## Workflow

### Authorization and Capture

The system uses a two-step payment flow:

1. **Authorization**: Card is authorized but not charged
   - Used for security deposits at pickup
   - Payment can be cancelled if not needed
   - Amount is held on customer's card

2. **Capture**: Previously authorized payment is captured
   - Used when vehicle is returned
   - Can capture full or partial amount
   - Finalizes the charge

### Example Use Cases

#### 1. Security Deposit at Pickup

```jsx
<StripeTerminal
  amount={50000} // $500 security deposit
  currency="usd"
  bookingId={bookingId}
  description="Security deposit"
  onSuccess={(paymentIntent) => {
    // Store payment intent ID in booking
    updateBooking({ 
      securityDepositPaymentId: paymentIntent.id,
      securityDepositStatus: 'authorized'
    });
  }}
/>
```

#### 2. Capture or Release at Return

```jsx
// Option A: Capture if there are damages
await apiService.capturePaymentIntent(
  companyId,
  paymentIntentId,
  20000 // Capture $200 for damages
);

// Option B: Cancel if no damages
await apiService.cancelPaymentIntent(
  companyId,
  paymentIntentId
);
```

## Multi-Tenant Architecture

Each company operates with its own Stripe Connect account:

- **Platform Level**: Main Stripe account
- **Company Level**: Separate Connect accounts
- **Payment Flow**: Uses `StripeAccount` header for Connect

Benefits:
- Isolated finances per company
- Individual reporting and payouts
- Company-specific settings and fees

## Testing

### Test Card Readers

For development/testing without physical hardware:

1. Use Stripe's simulated reader in Dashboard
2. Test cards work with Terminal endpoints
3. Can simulate various scenarios (success, decline, etc.)

### Test Cards

- **Success**: `4242 4242 4242 4242`
- **Decline**: `4000 0000 0000 0002`
- **3D Secure**: `4000 0027 6000 3184`

## Error Handling

Common errors and solutions:

| Error | Cause | Solution |
|-------|-------|----------|
| No readers found | Reader not powered on | Check reader power and internet |
| Connection failed | Network issues | Verify internet connection |
| Payment declined | Insufficient funds | Customer needs different card |
| Reader disconnected | Lost connection | Reconnect reader |

## Security Considerations

1. **PCI Compliance**: Stripe Terminal is PCI-compliant by design
2. **HTTPS Required**: All Terminal requests must use HTTPS
3. **Connection Tokens**: Short-lived tokens for reader connection
4. **Payment Intents**: Server-side creation prevents tampering
5. **Amount Verification**: Always verify amounts server-side

## API Reference

### Create Connection Token

```http
POST /api/Terminal/connection-token
Authorization: Bearer <token>
Content-Type: application/json

{
  "companyId": "00000000-0000-0000-0000-000000000000"
}
```

### Create Payment Intent

```http
POST /api/Terminal/create-payment-intent
Authorization: Bearer <token>
Content-Type: application/json

{
  "companyId": "00000000-0000-0000-0000-000000000000",
  "amount": 50000,
  "currency": "usd",
  "captureMethod": "manual",
  "description": "Security deposit",
  "bookingId": "booking-id",
  "metadata": {
    "vehicleId": "vehicle-id",
    "customerId": "customer-id"
  }
}
```

### Capture Payment

```http
POST /api/Terminal/capture-payment-intent
Authorization: Bearer <token>
Content-Type: application/json

{
  "companyId": "00000000-0000-0000-0000-000000000000",
  "paymentIntentId": "pi_xxxxxxxxxxxxx",
  "amountToCapture": 20000
}
```

### Cancel Payment

```http
POST /api/Terminal/cancel-payment-intent
Authorization: Bearer <token>
Content-Type: application/json

{
  "companyId": "00000000-0000-0000-0000-000000000000",
  "paymentIntentId": "pi_xxxxxxxxxxxxx"
}
```

## Troubleshooting

### Reader Won't Connect

1. Ensure reader is powered on
2. Check internet connection
3. Verify Stripe account has Terminal enabled
4. Check if reader is registered in Stripe Dashboard

### Payment Fails

1. Check card expiration and CVV
2. Verify sufficient funds
3. Ensure correct currency
4. Check for international card restrictions

### Connection Token Errors

1. Verify company has valid `StripeAccountId`
2. Check API credentials in backend config
3. Ensure user is authenticated
4. Verify company ID is correct

## Production Checklist

- [ ] Production Stripe API keys configured
- [ ] Each company has Stripe Connect account
- [ ] Physical card readers ordered and registered
- [ ] Staff trained on using readers
- [ ] Error handling and logging in place
- [ ] Test transactions completed successfully
- [ ] Refund/cancellation process documented
- [ ] Customer receipt generation implemented

## Resources

- [Stripe Terminal Docs](https://stripe.com/docs/terminal)
- [Stripe Connect Docs](https://stripe.com/docs/connect)
- [Terminal Hardware](https://stripe.com/terminal/readers)
- [Testing Guide](https://stripe.com/docs/terminal/testing)

## Support

For issues or questions:
1. Check Stripe Dashboard logs
2. Review backend API logs
3. Test with simulated reader
4. Contact Stripe support if needed

