# Card Reader Payment Integration Guide

This guide shows how to integrate the simplified `CardReaderPayment` component into your application for quick, streamlined card-present payments.

## Component Overview

The `CardReaderPayment` component provides a clean, user-friendly interface for:
- Discovering and connecting to Stripe Terminal readers
- Processing card-present payments
- Authorization and capture workflow
- Auto-capture or manual capture modes

## Quick Start

### Basic Usage (Fixed Amount)

```jsx
import CardReaderPayment from '../components/CardReaderPayment';

function BookingPaymentPage() {
  return (
    <CardReaderPayment
      defaultAmount={50000} // $500.00 in cents
      currency="usd"
      description="Security deposit for Honda Civic"
      bookingId="booking-123"
      onPaymentComplete={(paymentIntent) => {
        console.log('Payment completed:', paymentIntent);
        // Update booking status, redirect, etc.
      }}
      onPaymentError={(error) => {
        console.error('Payment failed:', error);
      }}
    />
  );
}
```

### Usage with Custom Amount Input

```jsx
import CardReaderPayment from '../components/CardReaderPayment';

function FlexiblePaymentPage() {
  return (
    <CardReaderPayment
      defaultAmount={10000} // Default: $100.00
      currency="usd"
      showAmountInput={true} // Show input field to change amount
      description="Payment for rental services"
      onPaymentComplete={(paymentIntent) => {
        alert('Payment successful!');
      }}
    />
  );
}
```

### Auto-Capture Mode

```jsx
import CardReaderPayment from '../components/CardReaderPayment';

function QuickPaymentPage() {
  return (
    <CardReaderPayment
      defaultAmount={25000} // $250.00
      currency="usd"
      autoCapture={true} // Automatically capture payment
      description="Final payment for vehicle rental"
      bookingId="booking-456"
      onPaymentComplete={(paymentIntent) => {
        // Payment is already captured
        console.log('Payment captured:', paymentIntent.id);
      }}
    />
  );
}
```

## Component Props

| Prop | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `defaultAmount` | number | 5000 | No | Amount in cents (e.g., 5000 = $50.00) |
| `currency` | string | 'usd' | No | Currency code (e.g., 'usd', 'eur') |
| `showAmountInput` | boolean | false | No | Show input field to change amount |
| `autoCapture` | boolean | false | No | Automatically capture payment after authorization |
| `description` | string | '' | No | Payment description |
| `bookingId` | string | null | No | Associated booking ID |
| `onPaymentComplete` | function | null | No | Callback when payment completes |
| `onPaymentError` | function | null | No | Callback when payment fails |

## Integration Examples

### 1. Admin Dashboard - Security Deposits

Add to your `AdminDashboard.js` for processing security deposits:

```jsx
import CardReaderPayment from '../components/CardReaderPayment';

// Inside your AdminDashboard component
<Modal show={showPaymentModal} onClose={() => setShowPaymentModal(false)}>
  <CardReaderPayment
    defaultAmount={selectedBooking?.securityDeposit || 50000}
    currency="usd"
    description={`Security deposit for ${selectedBooking?.vehicleName}`}
    bookingId={selectedBooking?.id}
    autoCapture={false} // Manual capture
    onPaymentComplete={(paymentIntent) => {
      // Update booking with payment intent ID
      updateBooking({
        ...selectedBooking,
        securityDepositPaymentId: paymentIntent.id,
        securityDepositStatus: 'authorized'
      });
      setShowPaymentModal(false);
    }}
    onPaymentError={(error) => {
      console.error('Payment error:', error);
    }}
  />
</Modal>
```

### 2. Booking Page - Final Payment

Add to your `BookPage.js` or checkout flow:

```jsx
import { useState } from 'react';
import CardReaderPayment from '../components/CardReaderPayment';

function CheckoutPage() {
  const [step, setStep] = useState('details'); // 'details' or 'payment'
  const totalAmount = calculateTotalAmount(); // Your calculation

  return (
    <div>
      {step === 'details' && (
        <>
          {/* Booking details form */}
          <button onClick={() => setStep('payment')}>
            Proceed to Payment
          </button>
        </>
      )}

      {step === 'payment' && (
        <CardReaderPayment
          defaultAmount={totalAmount}
          currency="usd"
          description="Rental payment"
          bookingId={bookingId}
          autoCapture={true} // Capture immediately
          onPaymentComplete={(paymentIntent) => {
            // Booking confirmed
            completeBooking(paymentIntent);
            navigate('/booking-confirmation');
          }}
        />
      )}
    </div>
  );
}
```

### 3. Vehicle Return - Damage Charges

For processing additional charges at vehicle return:

```jsx
import CardReaderPayment from '../components/CardReaderPayment';

function VehicleReturnPage({ booking }) {
  const [showDamagePayment, setShowDamagePayment] = useState(false);
  const [damageAmount, setDamageAmount] = useState(0);

  return (
    <div>
      <h2>Vehicle Return</h2>
      
      {/* Damage assessment */}
      <button onClick={() => setShowDamagePayment(true)}>
        Charge for Damages
      </button>

      {showDamagePayment && (
        <CardReaderPayment
          defaultAmount={damageAmount}
          currency="usd"
          showAmountInput={true} // Allow adjustment
          description="Vehicle damage charge"
          bookingId={booking.id}
          autoCapture={true}
          onPaymentComplete={(paymentIntent) => {
            // Record damage charge
            addChargeToBooking(booking.id, {
              type: 'damage',
              amount: damageAmount,
              paymentIntentId: paymentIntent.id
            });
            setShowDamagePayment(false);
          }}
        />
      )}
    </div>
  );
}
```

### 4. Capture Previously Authorized Payment

If you authorized a payment earlier (e.g., security deposit) and want to capture it:

```jsx
// This is done server-side, but you can trigger it from the frontend
import apiService from '../services/api';

async function captureSecurityDeposit(booking) {
  try {
    await apiService.capturePaymentIntent(
      booking.companyId,
      booking.securityDepositPaymentId,
      booking.damageAmount || null // null = capture full amount
    );
    
    console.log('Security deposit captured');
  } catch (error) {
    console.error('Capture failed:', error);
  }
}
```

### 5. Cancel Previously Authorized Payment

If you want to release an authorized payment (no damages):

```jsx
import apiService from '../services/api';

async function releaseSecurityDeposit(booking) {
  try {
    await apiService.cancelPaymentIntent(
      booking.companyId,
      booking.securityDepositPaymentId
    );
    
    console.log('Security deposit released');
  } catch (error) {
    console.error('Cancel failed:', error);
  }
}
```

## Workflow Examples

### Security Deposit Workflow

1. **At Vehicle Pickup:**
   ```jsx
   <CardReaderPayment
     defaultAmount={50000} // $500 deposit
     autoCapture={false} // Only authorize
     bookingId={bookingId}
     onPaymentComplete={(intent) => {
       savePaymentIntent(intent.id);
     }}
   />
   ```

2. **At Vehicle Return (No Damages):**
   ```javascript
   // Cancel the authorization
   await apiService.cancelPaymentIntent(companyId, paymentIntentId);
   ```

3. **At Vehicle Return (With Damages):**
   ```javascript
   // Capture partial or full amount
   await apiService.capturePaymentIntent(companyId, paymentIntentId, damageAmount);
   ```

### Final Payment Workflow

1. **At Checkout:**
   ```jsx
   <CardReaderPayment
     defaultAmount={totalAmount}
     autoCapture={true} // Charge immediately
     bookingId={bookingId}
     onPaymentComplete={(intent) => {
       confirmBooking(intent.id);
     }}
   />
   ```

## Styling

The component uses `CardReaderPayment.css` for styling. You can customize:

### Color Scheme

```css
/* In your global CSS or component CSS */
.card-reader-payment {
  /* Change primary color */
  --primary-color: #007bff;
  --success-color: #28a745;
  --error-color: #dc3545;
}
```

### Custom Styling

```css
/* Override specific elements */
.card-reader-payment .btn-collect {
  background: your-brand-color;
}

.card-reader-payment .reader-card {
  border-radius: 15px;
}
```

## Testing

### Simulated Reader

For testing without physical hardware:

```jsx
import { useStripeTerminal } from '../hooks/useStripeTerminal';

// In your test environment
const { discoverReaders } = useStripeTerminal({
  simulated: true // Use simulated reader
});
```

### Test Cards

Use these test cards with Stripe Terminal:
- **Success:** 4242 4242 4242 4242
- **Decline:** 4000 0000 0000 0002
- **3D Secure:** 4000 0027 6000 3184

## Troubleshooting

### Reader Not Found

1. Ensure reader is powered on
2. Check internet connection
3. Verify reader is registered in Stripe Dashboard
4. Try power cycling the reader

### Payment Fails

1. Check card expiration
2. Verify sufficient funds
3. Try a different card
4. Check Stripe Dashboard logs

### Connection Timeout

1. Check network connectivity
2. Ensure Stripe API keys are correct
3. Verify company has valid Stripe Account ID
4. Check backend logs for errors

## Security Best Practices

1. **Never store card numbers** - Stripe Terminal handles this
2. **Verify amounts server-side** - Don't trust client amounts
3. **Use HTTPS** - Required for Stripe Terminal
4. **Log transactions** - Keep audit trail
5. **Handle errors gracefully** - Show user-friendly messages

## Additional Resources

- [Stripe Terminal Documentation](https://stripe.com/docs/terminal)
- [useStripeTerminal Hook](../hooks/useStripeTerminal.js)
- [Full Setup Guide](./STRIPE_TERMINAL_SETUP.md)
- [API Reference](./STRIPE_TERMINAL_SETUP.md#api-reference)

## Support

For issues or questions:
1. Check component props and usage
2. Review browser console for errors
3. Check Stripe Dashboard logs
4. Test with simulated reader
5. Contact Stripe support if needed

